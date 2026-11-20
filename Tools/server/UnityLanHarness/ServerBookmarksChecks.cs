using System;
using Sinbinder.Multiplayer;

internal static class ServerBookmarksChecks
{
    private static void Check(bool value, string why)
    {
        if (!value) throw new Exception("Saved server regression: " + why);
    }

    public static void Run()
    {
        Check(!LanServerBookmarks.Add("http://dangerous.example", 7786),
            "server address must be a hostname/IP, not a URL");
        Check(!LanServerBookmarks.Add("bad host", 7786) &&
              !LanServerBookmarks.Add("127.0.0.1", -1),
            "invalid hosts and ports are not persisted");
        Check(LanServerBookmarks.Add("127.0.0.1", 7786),
            "local LAN server bookmark");
        Check(LanServerBookmarks.Add("game.example.org", 443, "Dedicated test"),
            "VPS server can be named");
        var entries = LanServerBookmarks.Load();
        Check(entries.Count == 2 && entries[0].name == "Dedicated test" &&
              entries[0].host == "game.example.org" &&
              entries[0].port == 443,
            "saved manual server selection survives JSON/PlayerPrefs roundtrip");
        Check(LanServerBookmarks.Add("game.example.org", 443, "Renamed"),
            "updating saved server succeeds");
        entries = LanServerBookmarks.Load();
        Check(entries.Count == 2 && entries[0].name == "Renamed",
            "same host+port does not duplicate a bookmark");
        for (int i = 0; i < 15; i++)
            Check(LanServerBookmarks.Add("server" + i + ".example.org",
                7777 + i), "add multiple valid endpoint bookmarks");
        entries = LanServerBookmarks.Load();
        Check(entries.Count == LanServerBookmarks.MaxEntries,
            "stored favourites never exceed ten endpoints");
        Check(LanServerBookmarks.Remove(entries[0].host, entries[0].port),
            "server can be removed by user");
        Check(LanServerBookmarks.Load().Count == LanServerBookmarks.MaxEntries - 1,
            "removed endpoint is not retained");
        Check(!LanServerBookmarks.Remove("not-saved.example.org", 7000),
            "unknown bookmark removal does nothing");
        Console.WriteLine("PASS: 10 manual server favourites persisted, validated, updated and removed; no passwords stored");
    }
}
