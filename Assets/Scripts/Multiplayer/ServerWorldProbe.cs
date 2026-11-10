// Opt-in server-side integration probe for REAL gameplay entities.
// Enable only on Multiplayer_Test with -mp-world-probe. It never replaces
// Solo SelectionManager, AOS, Warrior, TugMatch, camera or MapLoader.
using System;
using System.Collections.Generic;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    /// <summary>
    /// Discovers existing live Warriors on the actual Tug map and assigns
    /// server-owned actor IDs. It is NOT a multiplayer transport, client
    /// renderer, or replacement for the existing AOS/combat logic.
    ///
    /// This component is intentionally never auto-added in story scenes.
    /// Later, the authenticated transport can call TryIssueFromConnection
    /// and use the OrderResolved event for broadcast/snapshots.
    /// </summary>
    public sealed class ServerWorldProbe : MonoBehaviour
    {
        private sealed class Entry
        {
            public Warrior Warrior;
            public long ActorId;
            public NetworkActorRole Role;
            public int Owner;
        }

        private readonly Dictionary<int, Entry> _registered =
            new Dictionary<int, Entry>();
        private readonly Dictionary<string, Warrior> _lords =
            new Dictionary<string, Warrior>(StringComparer.Ordinal);
        private readonly List<int> _expired = new List<int>();
        private float _scanElapsed;
        private float _tickCarry;
        private float _snapshotCarry;
        private long _tick;
        private bool _initialized;
        private LanRoomServer _room;
        private bool _ownsRoom;

        public MatchAuthority Authority { get; private set; }
        public ServerWarriorOrderBridge Orders { get; private set; }
        public int RegisteredCount { get { return _registered.Count; } }
        public long Tick { get { return _tick; } }

        public event Action<AuthorizedGameOrder, bool> OrderResolved;

        // Optional authoritative, PER-PLAYER visibility supplied by future
        // multiplayer fog of war. Null means ONLY owned entities are sent.
        // Never use FogOfWar.Hides(): it is one local player's render mask.
        public Func<int, Warrior, bool> CanObserveActorOnServer { get; set; }

        private void Awake()
        {
            // Extra protection: this is strictly a test-scene feature,
            // never a globally active component in the solo campaign.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                != "Multiplayer_Test")
            {
                enabled = false;
                return;
            }
            Initialize();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mp-world-host") >= 0)
                StartWorldHost();
        }

        public void Initialize()
        {
            if (_initialized) return;
            var map = MapLoader.Current;
            if (map == null || map.Crypts.Count != 2)
                throw new InvalidOperationException(
                    "Real-world probe requires the two-crypt multiplayer QA map");
            Authority = new MatchAuthority(Guid.NewGuid().ToString("N"),
                map.Size / 2f, 2, 3);
            for (int side = 0; side < 2; side++)
                Authority.RegisterPlayer(side + 1, side,
                    Loc.F("Греховод стороны {0}", side + 1));

            Orders = new ServerWarriorOrderBridge(Authority);
            Orders.OrderResolved += (order, applied) =>
                OrderResolved?.Invoke(order, applied);
            _initialized = true;
            Debug.Log("[Sinbinder MP Probe] Real Warrior registry enabled. Match=" +
                Authority.MatchId + " (transport only with explicit -mp-world-host).");
        }

        private void Update()
        {
            if (!_initialized) return;
            if (_ownsRoom && _room != null) _room.Pump(Time.unscaledDeltaTime);
            _scanElapsed += Time.unscaledDeltaTime;
            if (_scanElapsed >= 0.5f)
            {
                _scanElapsed = 0f;
                Scan(UnityEngine.Object.FindObjectsByType<Warrior>(
                    FindObjectsSortMode.None));
            }
            // Delayed AOS proposals are delivered at 20 server ticks/sec.
            // Existing AOS/Combat/Unity NavMesh still run only in this
            // authoritative Unity process, not on remote clients.
            Advance(Time.unscaledDeltaTime);
            if (_ownsRoom && _room != null)
            {
                _snapshotCarry += Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.25f);
                if (_snapshotCarry >= 0.25f)
                {
                    _snapshotCarry = 0f;
                    _room.PublishPrivateWorldSnapshot();
                }
            }
        }

        /// <summary>
        /// Attach the existing TCP room without changing its capsule mode.
        /// Only explicit Multiplayer_Test hosts may call this. A future
        /// gameplay transport can replace this adapter without touching AOS.
        /// </summary>
        public void AttachRoom(LanRoomServer room)
        {
            if (!_initialized || room == null)
                throw new InvalidOperationException("World probe must be initialized");
            if (_room != null)
                throw new InvalidOperationException("A world room is already attached");
            _room = room;
            room.GameMatchId = Authority.MatchId;
            room.ResolveLordActorId = GetLordActorId;
            room.ResolveLastCommandId = Authority.LastAcceptedCommandId;
            room.PrivateWorldActors = GetPrivateWorldActors;
            room.ResolveWorldTick = () => _tick;
            room.RouteGameOrder = (connectionId, intent) =>
            {
                if (TryIssueFromConnection(connectionId, intent, null,
                    out var approved, out var rejection))
                    return LanGameOrderCodec.Accepted(approved);
                // Targeted Attack currently fails closed: server-side FOW
                // still needs integration before it can approve real combat.
                return LanGameOrderCodec.Rejected(intent.CommandId, rejection);
            };
            room.PeerConnectionChanged = (id, connected) =>
                Authority.SetConnected(id, connected);
            Orders.OrderResolved += room.NotifyGameOrderResolved;
        }

        /// <summary>
        /// Projection of real live positions into the authenticated player's
        /// private roster. By default it contains their Lord and at most
        /// 23 affiliated Warriors. The neutral roster, rival Lord and hidden
        /// enemies are NOT transmitted until a server-side visibility policy
        /// explicitly approves them.
        /// </summary>
        public IReadOnlyList<LanWorldActorState> GetPrivateWorldActors(int connectionId)
        {
            var result = new List<LanWorldActorState>();
            if (!_initialized || !Authority.TryGetLord(connectionId, out _))
                return result;

            var owned = new List<Entry>();
            var observable = new List<Entry>();
            foreach (var entry in _registered.Values)
            {
                var w = entry.Warrior;
                if (w == null || w.IsDead) continue;
                if (entry.Owner == connectionId && entry.Owner != 0)
                    owned.Add(entry);
                else if (CanObserveActorOnServer != null &&
                         CanObserveActorOnServer(connectionId, w))
                    observable.Add(entry);
            }
            owned.Sort((a, b) =>
            {
                if (a.Role == NetworkActorRole.Lord && b.Role != NetworkActorRole.Lord) return -1;
                if (b.Role == NetworkActorRole.Lord && a.Role != NetworkActorRole.Lord) return 1;
                return a.ActorId.CompareTo(b.ActorId);
            });
            observable.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
            foreach (var entry in owned)
            {
                if (result.Count >= LanRoomServer.MaxWorldActorsPerSnapshot) break;
                result.Add(new LanWorldActorState(entry.ActorId, entry.Role,
                    entry.Warrior.transform.position));
            }
            foreach (var entry in observable)
            {
                if (result.Count >= LanRoomServer.MaxWorldActorsPerSnapshot) break;
                result.Add(new LanWorldActorState(entry.ActorId, entry.Role,
                    entry.Warrior.transform.position));
            }
            return result;
        }

        public long GetLordActorId(int connectionId)
        {
            foreach (var entry in _registered.Values)
                if (entry.Role == NetworkActorRole.Lord &&
                    entry.Owner == connectionId && entry.Warrior != null &&
                    !entry.Warrior.IsDead)
                    return entry.ActorId;
            return 0;
        }

        private void StartWorldHost()
        {
            int port = 7777;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-port" && !int.TryParse(args[i + 1], out port))
                {
                    Debug.LogError("[Sinbinder MP Probe] Invalid port");
                    return;
                }

            var room = new LanRoomServer();
            AttachRoom(room);
            if (!room.Start(port))
            {
                Debug.LogError("[Sinbinder MP Probe] " + room.Error);
                DetachRoom();
                return;
            }
            _ownsRoom = true;
            Debug.Log("[Sinbinder MP Probe] REAL-world LAN order host on TCP " +
                port + "; no remote world rendering or combat sync yet.");
        }

        private void DetachRoom()
        {
            if (_room == null) return;
            Orders.OrderResolved -= _room.NotifyGameOrderResolved;
            _room.RouteGameOrder = null;
            _room.ResolveLordActorId = null;
            _room.ResolveLastCommandId = null;
            _room.PrivateWorldActors = null;
            _room.ResolveWorldTick = null;
            _room.PeerConnectionChanged = null;
            _room.GameMatchId = null;
            _room = null;
        }

        public void Advance(float dt)
        {
            if (!_initialized) return;
            _tickCarry += Mathf.Clamp(dt, 0f, 0.25f);
            int steps = 0;
            while (_tickCarry >= 0.05f && steps++ < 5)
            {
                _tickCarry -= 0.05f;
                Orders.ProcessDue(++_tick);
            }
            if (steps > 5) _tickCarry = 0f;
        }

        /// <summary>Server-only command entry. No untrusted JSON may
        /// specify which connection authenticated the sender.</summary>
        public bool TryIssueFromConnection(int authenticatedConnection,
            NetworkOrderIntent intent, Func<Warrior, Warrior, bool> visible,
            out AuthorizedGameOrder accepted, out OrderRejection reason)
        {
            accepted = default;
            reason = OrderRejection.NotAllowed;
            if (!_initialized) return false;
            return Orders.TryIssue(authenticatedConnection, intent, _tick,
                visible, ResolveMuffle, out accepted, out reason);
        }

        private float ResolveMuffle(Warrior actor, string lord)
        {
            if (!_lords.TryGetValue(lord, out var speaker) ||
                speaker == null || speaker.IsDead || actor == null)
                return 1f;
            var displacement = speaker.transform.position - actor.transform.position;
            float distance = Mathf.Sqrt(displacement.x * displacement.x +
                                         displacement.z * displacement.z);
            return Voice.Muffle(distance);
        }

        /// <summary>
        /// Called periodically during play, or explicitly in unit tests.
        /// Binds original Warrior instances, including dynamically spawned
        /// residents and newly resurrected Lords, without replacing them.
        /// </summary>
        public void Scan(IEnumerable<Warrior> liveWarriors)
        {
            if (!_initialized || liveWarriors == null) return;
            int previouslyRegistered = _registered.Count;
            _expired.Clear();
            foreach (var pair in _registered)
                if (pair.Value.Warrior == null || pair.Value.Warrior.IsDead)
                    _expired.Add(pair.Key);
            foreach (int key in _expired) Unregister(key);

            foreach (var warrior in liveWarriors)
            {
                if (warrior == null || warrior.IsDead) continue;
                int key = warrior.GetInstanceID();
                if (_registered.ContainsKey(key)) continue;

                var rival = warrior.GetComponent<RivalLord>();
                NetworkActorRole role;
                int owner;
                if (warrior is SinbinderPlayer)
                {
                    role = NetworkActorRole.Lord;
                    owner = 1;
                }
                else if (rival != null)
                {
                    if (rival.Side < 0 || rival.Side > 1) continue;
                    role = NetworkActorRole.Lord;
                    owner = rival.Side + 1;
                }
                else if (warrior.Team == Team.Neutral)
                {
                    role = NetworkActorRole.NeutralWarrior;
                    owner = 0;
                }
                else
                {
                    role = NetworkActorRole.AffiliatedWarrior;
                    owner = warrior.Team == Team.Player ? 1 : 2;
                }

                try
                {
                    long id = Orders.Register(warrior, role, owner);
                    _registered.Add(key, new Entry {
                        Warrior = warrior, ActorId = id,
                        Role = role, Owner = owner
                    });
                    if (role == NetworkActorRole.Lord &&
                        Authority.TryGetLord(owner, out var name))
                        _lords[name] = warrior;
                }
                catch (Exception error)
                {
                    Debug.LogWarning("[Sinbinder MP Probe] Could not register " +
                        warrior.gameObject.name + ": " + error.Message);
                }
            }
            if (_registered.Count != previouslyRegistered)
                Debug.Log("[Sinbinder MP Probe] Registered live entities: " +
                    _registered.Count + " / tick " + _tick);
            // A Lord can die and respawn between two scans while the
            // total entity count stays identical. Re-send identity only
            // when the authoritative Lord actor ID actually changed.
            if (_room != null) _room.RefreshGameIdentity();
        }

        private void Unregister(int key)
        {
            if (!_registered.TryGetValue(key, out var entry)) return;
            Orders.Unregister(entry.ActorId);
            _registered.Remove(key);
            if (entry.Role == NetworkActorRole.Lord &&
                Authority.TryGetLord(entry.Owner, out var name) &&
                _lords.TryGetValue(name, out var current) &&
                current == entry.Warrior)
                _lords.Remove(name);
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            if (_ownsRoom && _room != null) _room.Stop();
            _ownsRoom = false;
            DetachRoom();
            Orders.CancelPending();
            _expired.Clear();
            foreach (var key in _registered.Keys)
                _expired.Add(key);
            foreach (var key in _expired)
                Unregister(key);
            _lords.Clear();
            _initialized = false;
        }
    }
}
