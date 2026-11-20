// Local favourites for choosing a known LAN/VPS server by host and port.
// No passwords, credentials or session tokens are ever saved here.
// This is manual server selection, NOT a public server directory.
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    [Serializable]
    public sealed class LanServerBookmark
    {
        public string name;
        public string host;
        public int port;
    }

    [Serializable]
    internal sealed class LanServerBookmarkSet
    {
        public List<LanServerBookmark> entries = new List<LanServerBookmark>();
    }

    public static class LanServerBookmarks
    {
        public const int MaxEntries = 10;
        private const string StorageKey = "Sinbinder.Multiplayer.SavedServers.v1";

        public static List<LanServerBookmark> Load()
        {
            var safe = new List<LanServerBookmark>();
            string json = PlayerPrefs.GetString(StorageKey, "");
            if (string.IsNullOrEmpty(json)) return safe;
            try
            {
                var saved = JsonUtility.FromJson<LanServerBookmarkSet>(json);
                if (saved?.entries == null) return safe;
                foreach (var entry in saved.entries)
                {
                    if (entry == null || !ValidHost(entry.host) ||
                        entry.port < 1 || entry.port > 65535 ||
                        string.IsNullOrEmpty(entry.name) ||
                        entry.name.Length > 60)
                        continue;
                    bool duplicate = false;
                    foreach (var current in safe)
                        if (current.port == entry.port &&
                            string.Equals(current.host, entry.host,
                                StringComparison.OrdinalIgnoreCase))
                        { duplicate = true; break; }
                    if (!duplicate) safe.Add(entry);
                    if (safe.Count == MaxEntries) break;
                }
            }
            catch (Exception)
            {
                // Corrupt local preferences are never a reason to prevent
                // the multiplayer menu or single-player game from opening.
            }
            return safe;
        }

        public static bool Add(string host, int port, string name = null)
        {
            host = host?.Trim();
            if (!ValidHost(host) || port < 1 || port > 65535)
                return false;
            if (string.IsNullOrEmpty(name)) name = host + ":" + port;
            name = name.Trim();
            if (name.Length < 1 || name.Length > 60) return false;
            var entries = Load();
            entries.RemoveAll(e => e.port == port &&
                string.Equals(e.host, host, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, new LanServerBookmark {
                name = name, host = host, port = port
            });
            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            Save(entries);
            return true;
        }

        public static bool Remove(string host, int port)
        {
            var entries = Load();
            int removed = entries.RemoveAll(e => e.port == port &&
                string.Equals(e.host, host, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) Save(entries);
            return removed > 0;
        }

        private static void Save(List<LanServerBookmark> entries)
        {
            PlayerPrefs.SetString(StorageKey,
                JsonUtility.ToJson(new LanServerBookmarkSet { entries = entries }));
            PlayerPrefs.Save();
        }

        public static bool ValidHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host) || host.Length > 253 ||
                host != host.Trim())
                return false;
            if (IPAddress.TryParse(host, out _)) return true;
            // Hostnames, not URLs or shell commands; addresses are passed
            // directly to TcpClient.ConnectAsync(host,port).
            return Uri.CheckHostName(host) == UriHostNameType.Dns;
        }
    }
}
