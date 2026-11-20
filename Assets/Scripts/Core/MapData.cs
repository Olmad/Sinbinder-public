// Assets/Scripts/Core/MapData.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Карта «Перетягивания каната» — файлом (54-TUG.md; автор, 27.10: «нужен редактор карт, и меню,
    /// где отображаются карты в определённой папке»). Описание, а не сцена: поле, склепы с тронами,
    /// подлокации, вещи, тропы охотников, места событий. Собирает его в игре <c>Gameplay.MapLoader</c>,
    /// пишет и правит — редактор карт. Формат — JSON (JsonUtility): читается глазами и правится руками.
    ///
    /// Папка — <see cref="Folder"/>: рядом с игрой (в редакторе — в корне проекта). Пусто — кладём
    /// две карты по умолчанию, на двоих и на четверых (<see cref="Defaults"/>).
    /// </summary>
    [System.Serializable]
    public class MapData
    {
        public const int CurrentSchemaVersion = 3;
        // Zero represents JSON written before maps stored a format version.
        public int SchemaVersion;
        public string Name = "";
        public string Author = "";
        /// <summary>Сторона квадрата поля, метры.</summary>
        public float Size = 400f;
        public List<Crypt> Crypts = new();
        public List<Location> Locations = new();
        public List<Thing> Things = new();
        public List<Spot> Trails = new();
        public List<Spot> Events = new();
        // Optional since v2: maps saved before the editor upgrade remain loadable.
        public List<Region> Regions = new();
        public List<Trigger> Triggers = new();
        // Optional since v3: no height strokes means the original, perfectly flat map.
        public List<TerrainStroke> TerrainStrokes = new();
        // Optional v3.2: terrain surface paint, empty for old maps.
        public List<TerrainPaint> TerrainPaints = new();

        /// <summary>Named playable zone for safe event-condition-action logic.</summary>
        [System.Serializable] public class Region
        {
            public string Id = "";
            public string Name = "";
            public float X, Z, Radius = 15f;
        }

        /// <summary>
        /// Safe data-driven ECA rule, NOT executable C#/Lua/JASS supplied by a map.
        /// Event: OnStart / OnEnterRegion / OnInteractObject;
        /// Condition: Always / FlagIsTrue / FlagIsFalse;
        /// Action: ShowMessage / SetFlag / ClearFlag. RegionId, TargetId, and Flag are references.
        /// Executed only by the explicit allowlist in MapLogicRunner.
        /// </summary>
        [System.Serializable] public class Trigger
        {
            public string Id = "";
            public string Name = "";
            public bool Enabled = true;
            public string Event = "OnEnterRegion";
            public string RegionId = "";
            public string TargetId = "";
            public string Condition = "Always";
            public string Flag = "";
            public string Action = "ShowMessage";
            public string Message = "";
            public bool Once = true;
        }

        /// <summary>One bounded, deterministic terrain brush impression (world metres).</summary>
        [System.Serializable] public class TerrainStroke
        {
            public float X, Z;
            public float Radius = 18f;
            public float Delta = 3f;
            // Optional v3.1: old JSON has no Mode, which means Add.
            // Flatten means approach TargetHeight inside the brush radius.
            public string Mode = "Add";
            public float TargetHeight;
        }

        /// <summary>Last paint stroke wins for the terrain cells it covers.</summary>
        [System.Serializable] public class TerrainPaint
        {
            public float X, Z;
            public float Radius = 18f;
            public string MaterialId = "Ground048";
        }

        /// <summary>Склеп игрока с троном: здесь появляется Греховод стороны.</summary>
        [System.Serializable] public class Crypt { public float X, Z, Yaw; }

        /// <summary>Подлокация: деревня, лес, поле… — очаг, у которого живут её воины.</summary>
        [System.Serializable] public class Location { public string Kind = "village"; public string Name = ""; public float X, Z, Radius = 18f; public int People = 12; }

        /// <summary>Вещь: модель из Resources/Props (Pine, Tent, Rock…), место, поворот, размер.</summary>
        [System.Serializable] public class Thing
        {
            public string Id = "";
            public string Prop = "Pine";
            public string MaterialId = ""; // Optional object material override
            public float X, Z, Yaw, Scale = 1f;
        }

        /// <summary>Точка: тропа охотников или событие («обоз», «деревня»…).</summary>
        [System.Serializable] public class Spot { public string Kind = ""; public float X, Z; }

        public int Players => Crypts?.Count ?? 0;

        /// <summary>Normalize nullable lists from old or incomplete JSON maps.</summary>
        public void EnsureLists()
        {
            Crypts ??= new List<Crypt>();
            Locations ??= new List<Location>();
            Things ??= new List<Thing>();
            Trails ??= new List<Spot>();
            Events ??= new List<Spot>();
            Regions ??= new List<Region>();
            Triggers ??= new List<Trigger>();
            TerrainStrokes ??= new List<TerrainStroke>();
            TerrainPaints ??= new List<TerrainPaint>();
        }

        // ---------------------------------------------------------- папка

        /// <summary>Папка карт: рядом с игрой; в редакторе — <c>Maps</c> в корне проекта.</summary>
        public static string Folder
        {
            get
            {
                var root = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(root ?? ".", "Maps");
            }
        }

        /// <summary>Читаемые карты папки или стандартные карты, если ни одной не удалось загрузить.</summary>
        public static List<(string path, MapData map)> List()
        {
            var all = new List<(string path, MapData map)>();
            var folder = Folder;
            string[] paths;
            try
            {
                Directory.CreateDirectory(folder);
                paths = Directory.GetFiles(folder, "*.json");
            }
            catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
            {
                Debug.LogWarning($"[КАРТЫ] {folder}: не удалось открыть папку ({e.Message}).");
                paths = null;
            }

            if (paths != null)
                foreach (var path in paths)
                {
                    var map = Load(path);
                    if (map != null) all.Add((path, map));
                }

            if (all.Count == 0) AddDefaults(all, folder, paths != null && paths.Length == 0);
            all.Sort((a, b) => string.CompareOrdinal(a.Item2.Name, b.Item2.Name));
            return all;
        }

        internal static bool SupportsSchema(int version) => version >= 0 && version <= CurrentSchemaVersion;

        [System.Serializable]
        private sealed class SchemaHeader { public int SchemaVersion = 0; }

        internal static MapData ReadJson(string json, out int? unsupportedSchema)
        {
            unsupportedSchema = null;
            var header = JsonUtility.FromJson<SchemaHeader>(json);
            if (header == null) return null;
            // A newer format can use a different shape even for known fields such as Crypts.
            if (!SupportsSchema(header.SchemaVersion))
            {
                unsupportedSchema = header.SchemaVersion;
                return null;
            }
            return JsonUtility.FromJson<MapData>(json);
        }

        public static MapData Load(string path) => Load(path, out _);

        internal static MapData Load(string path, out int? unsupportedSchema)
            => Load(path, out unsupportedSchema, out _);

        internal static MapData Load(string path, out int? unsupportedSchema, out string fingerprint)
        {
            unsupportedSchema = null;
            fingerprint = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var map = ReadBytes(bytes, out unsupportedSchema);
                if (unsupportedSchema.HasValue)
                {
                    Debug.LogWarning($"[КАРТЫ] {path}: версия формата {unsupportedSchema.Value} не поддерживается (максимум {CurrentSchemaVersion}).");
                    return null;
                }
                if (map == null || map.Size <= 0f || float.IsNaN(map.Size) || float.IsInfinity(map.Size))
                {
                    Debug.LogWarning($"[КАРТЫ] {path}: неверный размер поля или пустое описание.");
                    return null;
                }
                map.EnsureLists();
                if (map.Crypts.Count < 2 || map.Crypts.Exists(crypt => crypt == null))
                {
                    Debug.LogWarning($"[КАРТЫ] {path}: нужны как минимум два склепа и ни одной пустой записи склепа.");
                    return null;
                }
                map.Locations.RemoveAll(location => location == null);
                map.Things.RemoveAll(thing => thing == null);
                map.Trails.RemoveAll(spot => spot == null);
                map.Events.RemoveAll(spot => spot == null);
                map.Regions.RemoveAll(region => region == null);
                map.Triggers.RemoveAll(trigger => trigger == null);
                map.TerrainStrokes.RemoveAll(stroke => stroke == null);
                map.TerrainPaints.RemoveAll(paint => paint == null);
                if (string.IsNullOrEmpty(map.Name)) map.Name = Path.GetFileNameWithoutExtension(path);
                fingerprint = Fingerprint(bytes);
                return map;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[КАРТЫ] {path}: не читается ({e.Message}).");
                return null;
            }
        }

        private static MapData ReadBytes(byte[] bytes, out int? unsupportedSchema)
        {
            using var reader = new StreamReader(new MemoryStream(bytes));
            return ReadJson(reader.ReadToEnd(), out unsupportedSchema);
        }

        private static string Fingerprint(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return System.Convert.ToBase64String(hash.ComputeHash(bytes));
        }

        internal sealed class SaveConflict : IOException
        {
            public SaveConflict(string path) : base("Map changed since opening: " + path) { }
        }

        internal sealed class SaveLockFailure : IOException
        {
            public SaveLockFailure(string path, IOException cause) : base("Cannot lock map for writing: " + path, cause) { }
        }

        internal static string WriteLockPath(string path) => Path.GetFullPath(path) + ".write-lock";

        private static FileStream AcquireWriteLock(string path)
        {
            // Keep the empty sidecar between saves. Only an open handle owns the lock.
            // Removing it on close would let two processes lock different files at once.
            try { return new FileStream(WriteLockPath(path), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error) { throw new SaveLockFailure(path, error); }
        }

        /// <summary>Atomic save with an original .bak file on overwrite; never truncate the only map.</summary>
        public void Save(string path, bool overwrite = true)
            => WriteMap(path, overwrite, checkFingerprint: false, expectedFingerprint: null);

        internal string SaveEditor(string path, bool createNew, string expectedFingerprint)
            => WriteMap(path, overwrite: !createNew, checkFingerprint: !createNew, expectedFingerprint: expectedFingerprint);

        private string WriteMap(string path, bool overwrite, bool checkFingerprint, string expectedFingerprint)
        {
            if (!SupportsSchema(SchemaVersion))
                throw new InvalidDataException("Unsupported map schema: " + SchemaVersion);
            EnsureLists();
            path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            using var writeLock = AcquireWriteLock(path);

            byte[] originalBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (checkFingerprint && (originalBytes == null || expectedFingerprint == null ||
                Fingerprint(originalBytes) != expectedFingerprint))
                throw new SaveConflict(path);
            if (!overwrite && originalBytes != null) throw new IOException("Map already exists: " + path);

            byte[] updatedBytes = System.Text.Encoding.UTF8.GetBytes(CurrentJson());
            string updatedFingerprint = Fingerprint(updatedBytes);
            string temp = TemporaryMapPath(directory);
            try
            {
                WriteTemporaryFile(temp, updatedBytes);
                if (originalBytes != null)
                {
                    var original = ReadBytes(originalBytes, out _);
                    if (original == null || original.Players < 2)
                        throw new InvalidDataException("Refusing to overwrite an unreadable or unsupported map: " + path);
                    if (original.SchemaVersion < CurrentSchemaVersion)
                        PreserveLegacyMap(path, directory, originalBytes);
                    File.Replace(temp, path, path + ".bak");
                }
                else
                {
                    // File.Move(source, dest) does not overwrite a concurrently
                    // created destination, unlike a second File.WriteAllText.
                    File.Move(temp, path);
                }
                SchemaVersion = CurrentSchemaVersion;
                return updatedFingerprint;
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private string CurrentJson()
        {
            int originalVersion = SchemaVersion;
            try
            {
                SchemaVersion = CurrentSchemaVersion;
                return JsonUtility.ToJson(this, true);
            }
            finally { SchemaVersion = originalVersion; }
        }

        internal static string TemporaryMapPath(string directory)
            => Path.Combine(directory, ".sinbinder-map-" + System.Guid.NewGuid().ToString("N") + ".tmp");

        internal static void WriteTemporaryFile(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        private static void PreserveLegacyMap(string path, string directory, byte[] bytes)
        {
            // The rolling .bak is for the last save. This separate file keeps the pre-upgrade bytes.
            string backup = path + ".pre-v" + CurrentSchemaVersion + ".bak";
            if (File.Exists(backup)) return;
            string temp = TemporaryMapPath(directory);
            try
            {
                WriteTemporaryFile(temp, bytes);
                try { File.Move(temp, backup); }
                catch (IOException) when (File.Exists(backup)) { }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        // ---------------------------------------------------------- по умолчанию

        private static void AddDefaults(List<(string path, MapData map)> all, string folder, bool save)
        {
            foreach (var map in Defaults())
            {
                string path = null;
                if (save)
                {
                    var destination = Path.Combine(folder, map.Name + ".json");
                    try
                    {
                        if (!File.Exists(destination))
                        {
                            map.Save(destination);
                            path = destination;
                        }
                    }
                    catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
                    {
                        Debug.LogWarning($"[КАРТЫ] {destination}: не удалось записать стандартную карту ({e.Message}).");
                    }
                }
                all.Add((path, map));
            }
        }

        /// <summary>
        /// Две карты: «Перекрёсток» на двоих и «Четыре склепа» на четверых. Склепы — по краям;
        /// в середине деревня, по сторонам лес и поле; тропы охотников — на середине краёв,
        /// дальше всего от склепов (автор, 27.10); событий — вдвое больше, чем игроков.
        /// Деревья — кольцами по формуле, без жребия.
        /// </summary>
        public static List<MapData> Defaults()
        {
            var two = new MapData { Name = Loc.N("Перекрёсток"), Author = "Sinbinder", Size = 300f };
            two.Crypts.Add(new Crypt { X = 0, Z = -125, Yaw = 0 });
            two.Crypts.Add(new Crypt { X = 0, Z = 125, Yaw = 180 });
            two.Locations.Add(new Location { Kind = "village", Name = Loc.N("Деревня"), X = 0, Z = 0, Radius = 22, People = 14 });
            two.Locations.Add(new Location { Kind = "forest", Name = Loc.N("Бор"), X = -80, Z = 10, Radius = 26, People = 8 });
            two.Locations.Add(new Location { Kind = "field", Name = Loc.N("Поле"), X = 80, Z = -10, Radius = 26, People = 8 });
            two.Trails.Add(new Spot { Kind = "hunters", X = -145, Z = 0 });
            two.Trails.Add(new Spot { Kind = "hunters", X = 145, Z = 0 });
            two.Events.Add(new Spot { Kind = "caravan", X = -40, Z = -60 });
            two.Events.Add(new Spot { Kind = "caravan", X = 40, Z = 60 });
            two.Events.Add(new Spot { Kind = "village", X = -90, Z = -70 });
            two.Events.Add(new Spot { Kind = "village", X = 90, Z = 70 });
            Grove(two, -80, 10, 26);
            Grove(two, 0, 0, 0, tents: 6);

            var four = new MapData { Name = Loc.N("Четыре склепа"), Author = "Sinbinder", Size = 400f };
            four.Crypts.Add(new Crypt { X = -165, Z = -165, Yaw = 45 });
            four.Crypts.Add(new Crypt { X = 165, Z = -165, Yaw = -45 });
            four.Crypts.Add(new Crypt { X = 165, Z = 165, Yaw = -135 });
            four.Crypts.Add(new Crypt { X = -165, Z = 165, Yaw = 135 });
            four.Locations.Add(new Location { Kind = "village", Name = Loc.N("Деревня"), X = 0, Z = 0, Radius = 26, People = 16 });
            four.Locations.Add(new Location { Kind = "forest", Name = Loc.N("Бор"), X = -110, Z = 0, Radius = 28, People = 10 });
            four.Locations.Add(new Location { Kind = "forest", Name = Loc.N("Ельник"), X = 110, Z = 0, Radius = 28, People = 10 });
            four.Locations.Add(new Location { Kind = "field", Name = Loc.N("Северное поле"), X = 0, Z = 110, Radius = 28, People = 10 });
            four.Locations.Add(new Location { Kind = "field", Name = Loc.N("Южное поле"), X = 0, Z = -110, Radius = 28, People = 10 });
            foreach (var (x, z) in new[] { (0f, -195f), (195f, 0f), (0f, 195f), (-195f, 0f) })
                four.Trails.Add(new Spot { Kind = "hunters", X = x, Z = z });
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                four.Events.Add(new Spot { Kind = i % 2 == 0 ? "caravan" : "village", X = Mathf.Cos(a) * 75f, Z = Mathf.Sin(a) * 75f });
            }
            Grove(four, -110, 0, 28);
            Grove(four, 110, 0, 28);
            Grove(four, 0, 0, 0, tents: 8);

            return new List<MapData> { two, four };
        }

        /// <summary>Лес — кольцами сосен; деревня — палатки по кругу. По формуле, не жребием.</summary>
        private static void Grove(MapData map, float cx, float cz, float radius, int tents = 0)
        {
            if (tents > 0)
            {
                for (int i = 0; i < tents; i++)
                {
                    float a = i * 360f / tents;
                    var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 12f);
                    map.Things.Add(new Thing { Prop = "Tent", X = cx + p.x, Z = cz + p.z, Yaw = a + 180f, Scale = 1.5f });
                }
                map.Things.Add(new Thing { Prop = "Campfire", X = cx, Z = cz });
                return;
            }
            for (int ring = 0; ring < 3; ring++)
            {
                int count = 10 + ring * 6;
                float r = radius * (0.35f + ring * 0.3f);
                for (int i = 0; i < count; i++)
                {
                    float a = (i * 360f / count + ring * 17f) * Mathf.Deg2Rad;
                    string kind = (i + ring) % 3 == 0 ? "TallPineA" : (i + ring) % 3 == 1 ? "TallPineB" : "Pine";
                    map.Things.Add(new Thing { Prop = kind, X = cx + Mathf.Cos(a) * r, Z = cz + Mathf.Sin(a) * r, Yaw = i * 37f % 360f, Scale = 0.9f + (i * 7 % 5) * 0.06f });
                }
            }
        }
    }

    /// <summary>
    /// Shared map-authoring terrain contract. Uses exactly the same height function to
    /// construct the editor's ground and the playable map's physics/navigation surface.
    /// The default (missing/empty strokes) retains Unity's original plane mesh.
    /// </summary>
    public static class MapTerrain
    {
        public const int MaxStrokes = 128;
        public const int MaxPaints = 256;
        public const int MaxTerrainMaterials = 8;
        public const string RestoreOriginalMaterial = "_Original";
        public const float MaxHeight = 24f;

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool Valid(MapData map)
        {
            if (map == null || !Finite(map.Size) || map.Size < 150f || map.Size > 800f)
                return false;
            if (map.TerrainPaints != null)
            {
                if (map.TerrainPaints.Count > MaxPaints) return false;
                var materials = new HashSet<string>();
                foreach (var paint in map.TerrainPaints)
                {
                    if (paint == null || !Finite(paint.X) || !Finite(paint.Z) ||
                        !Finite(paint.Radius) || Mathf.Abs(paint.X) > map.Size * 0.5f ||
                        Mathf.Abs(paint.Z) > map.Size * 0.5f ||
                        paint.Radius < 4f || paint.Radius > 100f ||
                        !MapAssets.ValidId(paint.MaterialId)) return false;
                    // The eraser references the already-present base material.
                    // It must never consume one of the seven extra material slots.
                    if (paint.MaterialId != RestoreOriginalMaterial)
                        materials.Add(paint.MaterialId);
                    if (materials.Count >= MaxTerrainMaterials) return false;
                }
            }
            if (map.TerrainStrokes == null || map.TerrainStrokes.Count == 0) return true;
            if (map.TerrainStrokes.Count > MaxStrokes) return false;
            float half = map.Size * 0.5f;
            foreach (var stroke in map.TerrainStrokes)
            {
                if (stroke == null || !Finite(stroke.X) || !Finite(stroke.Z) ||
                    !Finite(stroke.Radius) || !Finite(stroke.Delta) ||
                    !Finite(stroke.TargetHeight) ||
                    Mathf.Abs(stroke.X) > half || Mathf.Abs(stroke.Z) > half ||
                    stroke.Radius < 8f || stroke.Radius > 100f)
                    return false;
                bool flatten = stroke.Mode == "Flatten";
                if (!flatten && !string.IsNullOrEmpty(stroke.Mode) && stroke.Mode != "Add")
                    return false;
                if (flatten)
                {
                    if (Mathf.Abs(stroke.TargetHeight) > MaxHeight) return false;
                }
                else if (Mathf.Abs(stroke.Delta) < 0.01f || Mathf.Abs(stroke.Delta) > 12f)
                    return false;
            }
            return true;
        }

        private static int GridCells(MapData map) =>
            Mathf.Clamp(Mathf.RoundToInt(map.Size / 4f), 32, 128);

        // Evaluate only the authored brush field at grid vertices.
        private static float RawHeight(MapData map, float x, float z)
        {
            if (map.TerrainStrokes == null) return 0f;
            float height = 0f;
            foreach (var stroke in map.TerrainStrokes)
            {
                float dx = x - stroke.X, dz = z - stroke.Z;
                float radiusSq = stroke.Radius * stroke.Radius;
                float d2 = dx * dx + dz * dz;
                if (d2 >= radiusSq) continue;
                float fade = 1f - d2 / radiusSq;
                float weight = fade * fade;
                // Ordered flatten is fully data-driven; no external map code runs.
                if (stroke.Mode == "Flatten")
                    height += (stroke.TargetHeight - height) * weight;
                else height += stroke.Delta * weight;
            }
            return Mathf.Clamp(height, -MaxHeight, MaxHeight);
        }

        /// <summary>
        /// Height ON the generated triangle surface, not the smooth hypothetical brush
        /// curve. Props/spawn points therefore agree with the actual MeshCollider.
        /// Invalid downloaded terrain fails closed to flat.
        /// </summary>
        public static float HeightAt(MapData map, float x, float z)
        {
            if (!Valid(map) || map.TerrainStrokes == null ||
                map.TerrainStrokes.Count == 0) return 0f;
            int cells = GridCells(map);
            float step = map.Size / cells, half = map.Size * 0.5f;
            float gx = Mathf.Clamp((x + half) / step, 0f, cells);
            float gz = Mathf.Clamp((z + half) / step, 0f, cells);
            int ix = Mathf.Clamp((int)gx, 0, cells - 1);
            int iz = Mathf.Clamp((int)gz, 0, cells - 1);
            float fx = gx - ix, fz = gz - iz;
            float wx = ix * step - half, wz = iz * step - half;
            float h00 = RawHeight(map, wx, wz);
            float h10 = RawHeight(map, wx + step, wz);
            float h01 = RawHeight(map, wx, wz + step);
            if (fx + fz <= 1f)
                return h00 * (1f - fx - fz) + h10 * fx + h01 * fz;
            float h11 = RawHeight(map, wx + step, wz + step);
            return h11 * (fx + fz - 1f) + h10 * (1f - fz) + h01 * (1f - fx);
        }

        /// <summary>Real triangular terrain surface for rendering, physics and NavMesh.</summary>
        public static Mesh BuildMesh(MapData map)
        {
            if (!Valid(map) || ((map.TerrainStrokes == null || map.TerrainStrokes.Count == 0) &&
                (map.TerrainPaints == null || map.TerrainPaints.Count == 0))) return null;
            int cells = GridCells(map);
            int stride = cells + 1;
            var vertices = new Vector3[stride * stride];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[cells * cells * 6];
            float step = map.Size / cells;
            float half = map.Size * 0.5f;
            for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                {
                    int index = z * stride + x;
                    float wx = x * step - half, wz = z * step - half;
                    vertices[index] = new Vector3(wx, RawHeight(map, wx, wz), wz);
                    uvs[index] = new Vector2((float)x / cells, (float)z / cells);
                }
            int tri = 0;
            for (int z = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int a = z * stride + x;
                    // Clockwise viewed from above => upward normal in Unity.
                    triangles[tri++] = a;
                    triangles[tri++] = a + stride;
                    triangles[tri++] = a + 1;
                    triangles[tri++] = a + 1;
                    triangles[tri++] = a + stride;
                    triangles[tri++] = a + stride + 1;
                }
            var mesh = new Mesh { name = "Sinbinder authored terrain" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            // Multi-material cells become bounded submeshes; each triangle has exactly
            // one material. Collision stays the same geometry, not decorative overlays.
            if (map.TerrainPaints != null && map.TerrainPaints.Count > 0)
            {
                var ids = PaintMaterialIds(map);
                var groups = new List<int>[ids.Count];
                for (int g = 0; g < groups.Length; g++) groups[g] = new List<int>();
                for (int z = 0; z < cells; z++)
                    for (int x = 0; x < cells; x++)
                    {
                        float cx = (x + 0.5f) * step - half;
                        float cz = (z + 0.5f) * step - half;
                        string id = PaintedMaterialAt(map, cx, cz);
                        int group = ids.IndexOf(id);
                        if (group < 0) group = 0;
                        int t = (z * cells + x) * 6;
                        for (int k = 0; k < 6; k++) groups[group].Add(triangles[t + k]);
                    }
                mesh.subMeshCount = groups.Length;
                for (int g = 0; g < groups.Length; g++)
                    mesh.SetTriangles(groups[g].ToArray(), g);
            }
            else mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Deterministic paint selection: the latest stroke containing a cell wins.
        /// Empty string means original terrain material.
        /// </summary>
        public static string PaintedMaterialAt(MapData map, float x, float z)
        {
            if (map == null || map.TerrainPaints == null) return "";
            for (int i = map.TerrainPaints.Count - 1; i >= 0; i--)
            {
                var p = map.TerrainPaints[i];
                if (p == null) continue;
                float dx = x - p.X, dz = z - p.Z;
                if (dx * dx + dz * dz <= p.Radius * p.Radius)
                    return p.MaterialId == RestoreOriginalMaterial
                        ? "" : MapAssets.ValidId(p.MaterialId) ? p.MaterialId : "";
            }
            return "";
        }

        public static List<string> PaintMaterialIds(MapData map)
        {
            var ids = new List<string> { "" };
            if (map?.TerrainPaints == null) return ids;
            foreach (var p in map.TerrainPaints)
                if (p != null && p.MaterialId != RestoreOriginalMaterial &&
                    MapAssets.ValidId(p.MaterialId) &&
                    !ids.Contains(p.MaterialId) && ids.Count < MaxTerrainMaterials)
                    ids.Add(p.MaterialId);
            return ids;
        }

        /// <summary>
        /// Replace the existing plane mesh, not an overlapping decorative hill.
        /// Preserves its material and collider, so NavMeshSurface sees actual slopes.
        /// The original plane is restored if the last stroke is undone.
        /// </summary>
        public static void ApplyGround(GameObject ground, MapData map,
                                       ref Mesh generated, ref Mesh original)
        {
            if (ground == null) return;
            var filter = ground.GetComponent<MeshFilter>();
            var collider = ground.GetComponent<MeshCollider>();
            if (filter == null || collider == null) return;
            if (original == null) original = filter.sharedMesh;
            Mesh next = BuildMesh(map);
            // Remove old physics mesh before replacing/destroying it.
            collider.sharedMesh = null;
            filter.sharedMesh = next != null ? next : original;
            collider.sharedMesh = next != null ? next : original;
            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var originalMaterial = renderer.sharedMaterial;
                if (next != null && map.TerrainPaints != null && map.TerrainPaints.Count > 0)
                {
                    var ids = PaintMaterialIds(map);
                    var mats = new Material[ids.Count];
                    mats[0] = originalMaterial;
                    for (int i = 1; i < mats.Length; i++)
                        mats[i] = MapAssets.TerrainMaterial(ids[i]) ?? originalMaterial;
                    renderer.sharedMaterials = mats;
                }
                else renderer.sharedMaterials = new[] { originalMaterial };
            }
            ground.transform.localScale = next != null
                ? Vector3.one : new Vector3(map.Size / 10f, 1f, map.Size / 10f);
            if (generated != null) UnityEngine.Object.Destroy(generated);
            generated = next;
        }
    }

    /// <summary>
    /// Serialized map references may point only to known Resource catalogs.
    /// Actual imports happen explicitly inside Unity Editor, never from map JSON.
    /// </summary>
    public static class MapAssets
    {
        public static bool ValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 48) return false;
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    return false;
            return true;
        }

        public static bool IsUserProp(string prop) =>
            prop != null && prop.StartsWith("user:", System.StringComparison.Ordinal) &&
            ValidId(prop.Substring(5));

        public static GameObject UserPrefab(string prop)
        {
            if (!IsUserProp(prop)) return null;
            return Resources.Load<GameObject>("MapUserProps/" + prop.Substring(5));
        }

        public static Material TerrainMaterial(string id) =>
            ValidId(id) ? Resources.Load<Material>("MapTerrainMaterials/" + id) : null;

        public static Material ObjectMaterial(string id) =>
            ValidId(id) ? Resources.Load<Material>("MapUserMaterials/" + id) : null;

        public static void ApplyObjectMaterial(GameObject go, string materialId)
        {
            if (go == null || string.IsNullOrEmpty(materialId)) return;
            var material = ObjectMaterial(materialId);
            if (material == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = material;
        }
    }

    /// <summary>
    /// Что выбрано перед партией: карта и кто играет каждой стороной (лобби, как в Warcraft III).
    /// Живёт между сценами: меню карт → лобби → поле.
    /// </summary>
    public static class MapSession
    {
        public enum Seat { Player, AiWeak, AiMedium, AiStrong, Empty }

        public static string Path { get; set; }
        public static MapData Map { get; set; }
        public static List<Seat> Seats { get; } = new();

        /// <summary>Великая охота на 25-й минуте — включается в лобби (автор, 28.10: «можно включить/выключить»).</summary>
        public static bool GreatHunt { get; set; } = true;

        /// <summary>Идёт обучение (Gameplay.TugTutorial): уроки ведут партию, ИИ и охотники ждут своего урока.</summary>
        public static bool Tutorial { get; set; }

        /// <summary>
        /// Обучение «Перетягиванию» (автор, 28.10: «новому игроку нужно объяснять все правила… отдельный
        /// одиночный обучающий уровень с плашками, уроками»): маленькое поле, свой склеп на юге, деревня рядом,
        /// слабый Греховод-ИИ на севере. Картой из кода, а не из папки: в папке уже лежат карты игрока.
        /// </summary>
        public static void ChooseTutorial()
        {
            var map = new MapData { Name = Loc.N("Обучение"), Author = "Sinbinder", Size = 220f };
            map.Crypts.Add(new MapData.Crypt { X = 0, Z = -85, Yaw = 0 });
            map.Crypts.Add(new MapData.Crypt { X = 0, Z = 85, Yaw = 180 });
            map.Locations.Add(new MapData.Location { Kind = "village", Name = Loc.N("Деревня"), X = -30, Z = -40, Radius = 16, People = 10 });
            map.Locations.Add(new MapData.Location { Kind = "forest", Name = Loc.N("Бор"), X = 35, Z = 20, Radius = 18, People = 6 });
            map.Trails.Add(new MapData.Spot { Kind = "hunters", X = 100, Z = -40 });
            map.Trails.Add(new MapData.Spot { Kind = "hunters", X = -100, Z = 40 });
            Choose(null, map);
            Seats[1] = Seat.AiWeak;
            GreatHunt = false;
            Tutorial = true;
        }

        public static void Choose(string path, MapData map)
        {
            Tutorial = false;
            Path = path;
            Map = map;
            Seats.Clear();
            for (int i = 0; i < map.Players; i++) Seats.Add(i == 0 ? Seat.Player : Seat.AiWeak);
        }
    }
}
