using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>One atomically replaced recovery snapshot per editor session.</summary>
    internal sealed class MapDraftJournal : IDisposable
    {
        [Serializable]
        internal sealed class Snapshot
        {
            public int Version;
            public string MapJson;
            public string BaseFingerprint;
        }

        private const int CurrentVersion = 1;
        private readonly string _path;
        private FileStream _lease;

        private MapDraftJournal(string path)
        {
            _path = path;
            _lease = new FileStream(path + ".lease", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private static string MapFolder(string root, string sourcePath)
        {
            string source = string.IsNullOrEmpty(sourcePath) ? "new" : "file:" + Path.GetFullPath(sourcePath);
            if (Path.DirectorySeparatorChar == '\\') source = source.ToUpperInvariant();
            using var hash = SHA256.Create();
            string key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "");
            return Path.Combine(root, key);
        }

        public static MapDraftJournal Create(string root, string sourcePath)
        {
            string folder = MapFolder(root, sourcePath);
            Directory.CreateDirectory(folder);
            return new MapDraftJournal(Path.Combine(folder, Guid.NewGuid().ToString("N") + ".draft"));
        }

        public static MapDraftJournal FindLatest(string root, string sourcePath, out Snapshot snapshot)
        {
            snapshot = null;
            var folder = new DirectoryInfo(MapFolder(root, sourcePath));
            if (!folder.Exists) return null;
            var files = folder.GetFiles("*.draft");
            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            foreach (var file in files)
            {
                MapDraftJournal journal;
                try { journal = new MapDraftJournal(file.FullName); }
                catch (IOException) { continue; } // A live editor owns this snapshot.

                try
                {
                    var saved = JsonUtility.FromJson<Snapshot>(File.ReadAllText(file.FullName));
                    if (saved != null && saved.Version == CurrentVersion && !string.IsNullOrWhiteSpace(saved.MapJson) &&
                        MapData.ReadJson(saved.MapJson, out _) != null)
                    {
                        snapshot = saved;
                        return journal;
                    }
                }
                catch (Exception error)
                {
                    Debug.LogWarning("[WorldForge] Cannot read draft: " + error.Message);
                }
                journal.Dispose();
            }
            return null;
        }

        public void Write(string mapJson, string baseFingerprint)
        {
            if (_lease == null) throw new ObjectDisposedException(nameof(MapDraftJournal));
            var snapshot = new Snapshot { Version = CurrentVersion, MapJson = mapJson, BaseFingerprint = baseFingerprint };
            string temp = MapData.TemporaryMapPath(Path.GetDirectoryName(_path));
            try
            {
                MapData.WriteTemporaryFile(temp, Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot)));
                if (File.Exists(_path)) File.Replace(temp, _path, null);
                else File.Move(temp, _path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public void Discard()
        {
            if (_lease == null) throw new ObjectDisposedException(nameof(MapDraftJournal));
            File.Delete(_path);
            Dispose();
            // No new editor ever writes this ID after its snapshot has been removed.
            try { File.Delete(_path + ".lease"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public void Dispose()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }
}
