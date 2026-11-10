// SERVER-ONLY room catalog foundation for future LAN/VPS lobbies.
// Not yet connected to the public TCP protocol: DO NOT transmit passwords
// over the current unencrypted LAN channel. Use TLS + authenticated
// connections and move expensive password checks off the Unity main thread
// BEFORE enabling internet-facing join/create commands.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Sinbinder.Multiplayer
{
    public enum RoomJoinStatus
    {
        Accepted, UnknownRoom, AlreadyJoined, RoomFull,
        WrongPassword, RateLimited, Closed
    }

    public sealed class LanRoomDescription
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Region { get; internal set; }
        public int Players { get; internal set; }
        public int Capacity { get; internal set; }
        public bool PasswordRequired { get; internal set; }
        public bool Running { get; internal set; }
        public PingBalanceMode PingBalance { get; internal set; }
        // This public DTO deliberately has no hash, salt, password or
        // secret field. Listing rooms must never disclose credentials.
    }

    /// <summary>
    /// In-memory, single-threaded room registry for a future dedicated
    /// server. Player IDs MUST come from authenticated server sessions.
    /// It is not an open-to-internet lobby and creates no sockets.
    /// </summary>
    public sealed class LanRoomDirectory
    {
        public const int MaxRooms = 32;
        public const int MinimumCapacity = 2;
        public const int MaximumCapacity = 4;
        private const int KdfIterations = 120000;
        private const int MaxFailedPasswordAttempts = 5;

        private sealed class Attempt
        {
            public int Failed;
            public DateTime BlockedUntilUtc;
        }

        private sealed class Room
        {
            public string Id, Name, Region;
            public int OwnerConnectionId, Capacity;
            public PingBalanceMode Balance;
            public byte[] Salt;
            public byte[] Hash;
            public bool Running;
            public readonly HashSet<int> Members = new HashSet<int>();
            public readonly Dictionary<int, Attempt> Attempts =
                new Dictionary<int, Attempt>();
        }

        private readonly Dictionary<string, Room> _rooms =
            new Dictionary<string, Room>(StringComparer.Ordinal);

        public int Count { get { return _rooms.Count; } }

        public string Create(int authenticatedOwnerId, string name,
            string region, int capacity, string password,
            PingBalanceMode balance = PingBalanceMode.Off)
        {
            if (authenticatedOwnerId <= 0) throw new ArgumentOutOfRangeException(nameof(authenticatedOwnerId));
            if (_rooms.Count >= MaxRooms) throw new InvalidOperationException("Room capacity reached");
            ValidateLabel(name, 2, 40, "Room name");
            ValidateLabel(region, 2, 16, "Region");
            if (capacity < MinimumCapacity || capacity > MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!Enum.IsDefined(typeof(PingBalanceMode), balance))
                throw new ArgumentOutOfRangeException(nameof(balance));
            if (password != null && (password.Length < 6 ||
                                     password.Length > 128))
                throw new ArgumentException("Password length must be 6..128 characters");
            foreach (var existing in _rooms.Values)
                if (existing.OwnerConnectionId == authenticatedOwnerId)
                    throw new InvalidOperationException("One hosted room per connection");
            var room = new Room {
                Id = NewSecureId(), Name = name.Trim(), Region = region.Trim(),
                Capacity = capacity, OwnerConnectionId = authenticatedOwnerId,
                Balance = balance
            };
            if (password != null)
            {
                room.Salt = new byte[16];
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(room.Salt);
                room.Hash = Derive(password, room.Salt);
            }
            room.Members.Add(authenticatedOwnerId);
            _rooms.Add(room.Id, room);
            return room.Id;
        }

        public LanRoomDescription[] List()
        {
            var result = new List<LanRoomDescription>(_rooms.Count);
            foreach (var room in _rooms.Values)
                result.Add(new LanRoomDescription {
                    Id = room.Id, Name = room.Name, Region = room.Region,
                    Players = room.Members.Count, Capacity = room.Capacity,
                    PasswordRequired = room.Hash != null, Running = room.Running,
                    PingBalance = room.Balance
                });
            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result.ToArray();
        }

        public RoomJoinStatus TryJoin(int authenticatedConnectionId,
            string roomId, string password)
        {
            if (authenticatedConnectionId <= 0 || roomId == null ||
                !_rooms.TryGetValue(roomId, out var room))
                return RoomJoinStatus.UnknownRoom;
            if (room.Members.Contains(authenticatedConnectionId))
                return RoomJoinStatus.AlreadyJoined;
            if (room.Running) return RoomJoinStatus.Closed;
            if (room.Members.Count >= room.Capacity)
                return RoomJoinStatus.RoomFull;

            if (room.Hash != null)
            {
                if (!room.Attempts.TryGetValue(authenticatedConnectionId, out var attempt))
                {
                    attempt = new Attempt();
                    room.Attempts.Add(authenticatedConnectionId, attempt);
                }
                if (DateTime.UtcNow < attempt.BlockedUntilUtc)
                    return RoomJoinStatus.RateLimited;
                bool correct = password != null &&
                    FixedTimeEqual(room.Hash, Derive(password, room.Salt));
                if (!correct)
                {
                    attempt.Failed++;
                    if (attempt.Failed >= MaxFailedPasswordAttempts)
                    {
                        attempt.Failed = 0;
                        attempt.BlockedUntilUtc = DateTime.UtcNow.AddSeconds(30);
                    }
                    return RoomJoinStatus.WrongPassword;
                }
                room.Attempts.Remove(authenticatedConnectionId);
            }

            room.Members.Add(authenticatedConnectionId);
            return RoomJoinStatus.Accepted;
        }

        public bool Start(int authenticatedOwnerId, string roomId)
        {
            if (roomId == null || !_rooms.TryGetValue(roomId, out var room) ||
                room.OwnerConnectionId != authenticatedOwnerId ||
                room.Members.Count < 2 || room.Running)
                return false;
            room.Running = true;
            return true;
        }

        public bool Leave(int authenticatedConnectionId, string roomId)
        {
            if (roomId == null || !_rooms.TryGetValue(roomId, out var room) ||
                !room.Members.Remove(authenticatedConnectionId))
                return false;
            room.Attempts.Remove(authenticatedConnectionId);
            if (room.OwnerConnectionId == authenticatedConnectionId ||
                room.Members.Count == 0)
                _rooms.Remove(roomId);
            // Running games need a separate reconnect / AI-takeover policy.
            // Do not implicitly allocate another player to a live game.
            return true;
        }

        private static void ValidateLabel(string value, int min,
            int max, string label)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Trim().Length < min || value.Trim().Length > max)
                throw new ArgumentException(label + " length is invalid");
            foreach (char ch in value)
                if (char.IsControl(ch))
                    throw new ArgumentException(label + " has control characters");
        }

        private static string NewSecureId()
        {
            var bytes = new byte[18];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] Derive(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password, salt, KdfIterations, HashAlgorithmName.SHA256))
                return pbkdf2.GetBytes(32);
        }

        private static bool FixedTimeEqual(byte[] first, byte[] second)
        {
            if (first.Length != second.Length) return false;
            int difference = 0;
            for (int i = 0; i < first.Length; i++)
                difference |= first[i] ^ second[i];
            return difference == 0;
        }
    }
}
