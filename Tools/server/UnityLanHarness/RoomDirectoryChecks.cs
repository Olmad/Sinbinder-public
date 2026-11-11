using System;
using Sinbinder.Multiplayer;

internal static class RoomDirectoryChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Room directory regression: " + message);
    }

    private static void ExpectError(Action action, string message)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }

    public static void Run()
    {
        var rooms = new LanRoomDirectory();
        string privateRoom = rooms.Create(11, "Тайная нора", "EU", 2,
            "хороший_пароль!12", PingBalanceMode.Conservative);
        string otherRoom = rooms.Create(21, "Open lobby", "Local", 4,
            null, PingBalanceMode.Off);
        Check(privateRoom != otherRoom && privateRoom.Length >= 16,
            "room IDs are unique random opaque values");
        var listed = rooms.List();
        Check(listed.Length == 2, "both lobbies in listing");
        LanRoomDescription secured = null;
        foreach (var row in listed)
            if (row.Id == privateRoom) secured = row;
        Check(secured != null && secured.Name == "Тайная нора" &&
            secured.PasswordRequired && secured.Players == 1 &&
            secured.Capacity == 2 &&
            secured.PingBalance == PingBalanceMode.Conservative,
            "public listing exposes settings, not password");
        Check(secured.GetType().GetProperty("Hash") == null &&
            secured.GetType().GetProperty("Salt") == null &&
            secured.GetType().GetProperty("Password") == null,
            "public room DTO has no secrets");
        Check(rooms.TryJoin(12, privateRoom, "invalid!") ==
            RoomJoinStatus.WrongPassword, "invalid password rejected");
        Check(rooms.TryJoin(12, privateRoom, "хороший_пароль!12") ==
            RoomJoinStatus.Accepted, "correct salted password accepted");
        Check(rooms.TryJoin(12, privateRoom, null) ==
            RoomJoinStatus.AlreadyJoined, "duplicate occupant rejected");
        Check(rooms.TryJoin(13, privateRoom, "хороший_пароль!12") ==
            RoomJoinStatus.RoomFull, "room capacity enforced");
        Check(!rooms.Start(12, privateRoom) && rooms.Start(11, privateRoom),
            "only authenticated owner starts a populated room");
        Check(rooms.TryJoin(14, privateRoom, "хороший_пароль!12") ==
            RoomJoinStatus.Closed, "no late join to running room");

        Check(rooms.TryJoin(22, otherRoom, null) == RoomJoinStatus.Accepted,
            "open room accepts connection with no password");
        for (int i = 0; i < 5; i++)
            Check(rooms.TryJoin(25, privateRoom + "not-real", "wrong") ==
                RoomJoinStatus.UnknownRoom, "unknown ID does not leak secret");
        string locked = rooms.Create(31, "Closed club", "EU", 2,
            "strong-password-999", PingBalanceMode.Balanced);
        for (int i = 0; i < 5; i++)
            Check(rooms.TryJoin(32, locked, "wrong-pass") ==
                RoomJoinStatus.WrongPassword, "password guess " + i + " rejected");
        Check(rooms.TryJoin(32, locked, "strong-password-999") ==
            RoomJoinStatus.RateLimited, "failed attempts trigger cooldown");
        Check(rooms.TryJoin(33, locked, "strong-password-999") ==
            RoomJoinStatus.Accepted, "unrelated legitimate connection not blocked");
        Check(!rooms.Start(32, locked), "attacker cannot start room");
        Check(rooms.Leave(31, locked), "owner can leave");
        Check(rooms.TryJoin(33, locked, "strong-password-999") ==
            RoomJoinStatus.UnknownRoom, "room removed when owner quits");

        // Each distinct authenticated ID consumes at most one bounded attempt slot.
        // Null passwords exercise the rejection path without expensive KDF work.
        var bounded = new LanRoomDirectory();
        string boundedId = bounded.Create(1000, "Bounded private", "EU", 2,
            "another-secure-password");
        for (int i = 0; i < 128; i++)
            Check(bounded.TryJoin(2000 + i, boundedId, null) ==
                RoomJoinStatus.WrongPassword, "tracked attempt slot " + i);
        Check(bounded.TryJoin(9999, boundedId, "another-secure-password") ==
            RoomJoinStatus.RateLimited, "new identities fail closed at bound");
        Check(bounded.TryJoin(2000, boundedId, "another-secure-password") ==
            RoomJoinStatus.Accepted, "existing legitimate identity can still join");
        Check(bounded.TryJoin(2001, boundedId, null) ==
            RoomJoinStatus.WrongPassword, "existing attempt state survives saturation");

        ExpectError(() => rooms.Create(41, "x", "EU", 2, null),
            "invalid short name blocked");
        ExpectError(() => rooms.Create(41, "Game\nInjected", "EU", 2, null),
            "control characters rejected");
        ExpectError(() => rooms.Create(41, "Valid", "EU", 7, null),
            "overcapacity prohibited");
        ExpectError(() => rooms.Create(41, "Valid", "EU", 2, "123"),
            "short password prohibited");
        ExpectError(() => rooms.Create(11, "Duplicate host", "EU", 2, null),
            "owner cannot create second room");
        Check(rooms.Leave(11, privateRoom) && rooms.Count == 1,
            "room owner cleanup is complete");
        Console.WriteLine("PASS: future private room IDs, public list privacy, PBKDF2 passwords, capacity, ownership and throttled guessing");
    }
}
