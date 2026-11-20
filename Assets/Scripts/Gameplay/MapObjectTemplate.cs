// Reusable WorldForge composition templates. Safe data only: no scripts or file paths
// are read from a template; authored map JSON remains independent from this library.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    internal static class MapObjectTemplate
    {
        internal const int MaxItems = 64;
        internal const int MaxRules = 64;
        private const int MaxFileBytes = 131072;
        private const int MaxCatalogEntries = 128;
        private const int MaxMapThings = 4096;
        private const int MaxMapRules = 128;

        [Serializable] internal sealed class Layout
        {
            public int Version = 1;
            public string Name = "";
            public List<Entry> Items = new();
            public List<Interaction> Interactions = new();
        }

        [Serializable] internal sealed class Entry
        {
            public string Ref = "";
            public string Prop = "";
            public string MaterialId = "";
            public float Dx, Dz, Yaw, Scale = 1f;
        }

        [Serializable] internal sealed class Interaction
        {
            public string TargetRef = "";
            public string Name = "";
            public bool Enabled = true;
            public bool Once = true;
            public string Condition = "Always";
            public string Flag = "";
            public string Action = "ShowMessage";
            public string Message = "";
        }

        internal static string Folder => Path.Combine(MapData.Folder, "Templates");

        internal static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 48) return false;
            foreach (char c in name)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') || c == '_' || c == '-'))
                    return false;
            return true;
        }

        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static bool ValidProp(string prop) =>
            MapAssets.ValidId(prop) || MapAssets.IsUserProp(prop);

        internal static bool Valid(Layout layout)
        {
            if (layout == null || layout.Version != 1 || !ValidName(layout.Name) ||
                layout.Items == null || layout.Items.Count == 0 ||
                layout.Items.Count > MaxItems || layout.Interactions == null ||
                layout.Interactions.Count > MaxRules) return false;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in layout.Items)
            {
                if (item == null || !MapAssets.ValidId(item.Ref) ||
                    !ids.Add(item.Ref) || !ValidProp(item.Prop) ||
                    (!string.IsNullOrEmpty(item.MaterialId) &&
                     !MapAssets.ValidId(item.MaterialId)) ||
                    !Finite(item.Dx) || !Finite(item.Dz) ||
                    Math.Abs(item.Dx) > 800f || Math.Abs(item.Dz) > 800f ||
                    !Finite(item.Yaw) || !Finite(item.Scale) ||
                    item.Scale < 0.1f || item.Scale > 10f) return false;
            }

            foreach (var rule in layout.Interactions)
            {
                if (rule == null || !ids.Contains(rule.TargetRef) ||
                    rule.Condition != "Always" && rule.Condition != "FlagIsTrue" &&
                    rule.Condition != "FlagIsFalse" ||
                    rule.Action != "ShowMessage" && rule.Action != "SetFlag" &&
                    rule.Action != "ClearFlag" ||
                    (rule.Name?.Length ?? 0) > 80 ||
                    (rule.Message?.Length ?? 0) > 240 ||
                    (rule.Flag?.Length ?? 0) > 80) return false;
            }
            return true;
        }

        // Capture relative offsets, not absolute world positions, and only rules
        // whose target is an explicitly selected regular prop.
        internal static bool TryCapture(MapData map, ISet<string> selectedIds,
            string name, out Layout layout)
        {
            layout = null;
            if (map == null || map.Things == null || map.Triggers == null ||
                selectedIds == null || !ValidName(name)) return false;
            var selected = new List<MapData.Thing>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            double sumX = 0d, sumZ = 0d;
            foreach (var thing in map.Things)
            {
                if (thing == null || string.IsNullOrEmpty(thing.Id) ||
                    !selectedIds.Contains(thing.Id)) continue;
                if (!ids.Add(thing.Id) || selected.Count >= MaxItems ||
                    !Finite(thing.X) || !Finite(thing.Z)) return false;
                selected.Add(thing);
                sumX += thing.X;
                sumZ += thing.Z;
            }
            if (selected.Count == 0) return false;
            float cx = (float)(sumX / selected.Count), cz = (float)(sumZ / selected.Count);
            if (!Finite(cx) || !Finite(cz)) return false;

            var candidate = new Layout { Name = name };
            var refs = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < selected.Count; i++)
            {
                var thing = selected[i];
                string local = "item" + i;
                refs.Add(thing.Id, local);
                candidate.Items.Add(new Entry
                {
                    Ref = local, Prop = thing.Prop, MaterialId = thing.MaterialId ?? "",
                    Dx = thing.X - cx, Dz = thing.Z - cz,
                    Yaw = thing.Yaw, Scale = thing.Scale
                });
            }

            foreach (var rule in map.Triggers)
            {
                if (rule == null || rule.Event != "OnInteractObject" ||
                    !refs.TryGetValue(rule.TargetId ?? "", out string target)) continue;
                if (candidate.Interactions.Count >= MaxRules) return false;
                candidate.Interactions.Add(new Interaction
                {
                    TargetRef = target, Name = rule.Name, Enabled = rule.Enabled,
                    Once = rule.Once, Condition = rule.Condition, Flag = rule.Flag,
                    Action = rule.Action, Message = rule.Message
                });
            }
            if (!Valid(candidate)) return false;
            layout = candidate;
            return true;
        }

        // Pure geometry: Unity positive Y rotation turns +Z towards +X.
        // The preview and the committed paste MUST use the exact same projection.
        // Out-of-bounds geometry returns false but retains finite projected points
        // for red preview markers. Invalid/unsafe input returns no points at all.
        internal static bool TryProject(Layout layout, float mapSize,
            float x, float z, float rotationDegrees, out List<Vector2> positions)
        {
            positions = null;
            if (!Valid(layout) || !Finite(mapSize) || mapSize <= 0f ||
                !Finite(x) || !Finite(z) || !Finite(rotationDegrees))
                return false;

            double radians = ((double)rotationDegrees % 360d) * Math.PI / 180d;
            double cosine = Math.Cos(radians), sine = Math.Sin(radians);
            double half = mapSize * 0.5d;
            var projected = new List<Vector2>(layout.Items.Count);
            bool fits = true;
            foreach (var item in layout.Items)
            {
                double px = (double)x + item.Dx * cosine + item.Dz * sine;
                double pz = (double)z - item.Dx * sine + item.Dz * cosine;
                if (double.IsNaN(px) || double.IsInfinity(px) ||
                    double.IsNaN(pz) || double.IsInfinity(pz) ||
                    Math.Abs(px) > float.MaxValue || Math.Abs(pz) > float.MaxValue)
                    return false;
                if (Math.Abs(px) > half || Math.Abs(pz) > half) fits = false;
                projected.Add(new Vector2((float)px, (float)pz));
            }
            positions = projected;
            return fits;
        }

        // Legacy zero-rotation caller remains equivalent for all existing maps.
        internal static bool TryPlanPaste(Layout layout, MapData map,
            float x, float z, out List<MapData.Thing> things,
            out List<MapData.Trigger> triggers) =>
            TryPlanPaste(layout, map, x, z, 0f, out things, out triggers);

        // No writes on failure. New GUIDs for every pasted object AND F-trigger.
        internal static bool TryPlanPaste(Layout layout, MapData map,
            float x, float z, float rotationDegrees,
            out List<MapData.Thing> things, out List<MapData.Trigger> triggers)
        {
            things = null;
            triggers = null;
            if (map == null || map.Things == null || map.Triggers == null ||
                !Valid(layout) ||
                map.Things.Count + layout.Items.Count > MaxMapThings ||
                map.Triggers.Count + layout.Interactions.Count > MaxMapRules ||
                !TryProject(layout, map.Size, x, z, rotationDegrees, out var positions))
                return false;
            var props = new List<MapData.Thing>(layout.Items.Count);
            var rules = new List<MapData.Trigger>(layout.Interactions.Count);
            var refs = new Dictionary<string, string>(StringComparer.Ordinal);
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var existing in map.Things)
                if (existing != null && !string.IsNullOrEmpty(existing.Id))
                    existingIds.Add(existing.Id);
            foreach (var existing in map.Triggers)
                if (existing != null && !string.IsNullOrEmpty(existing.Id))
                    existingIds.Add(existing.Id);

            for (int i = 0; i < layout.Items.Count; i++)
            {
                var item = layout.Items[i];
                string guid = NextId(existingIds);
                refs.Add(item.Ref, guid);
                // Normalise in double precision before converting back to float:
                // even valid imported template yaws may be very large.
                double yaw = ((double)item.Yaw + rotationDegrees) % 360d;
                if (yaw < 0d) yaw += 360d;
                props.Add(new MapData.Thing
                {
                    Id = guid, Prop = item.Prop, MaterialId = item.MaterialId,
                    X = positions[i].x, Z = positions[i].y,
                    Yaw = (float)yaw, Scale = item.Scale
                });
            }

            foreach (var rule in layout.Interactions)
            {
                rules.Add(new MapData.Trigger
                {
                    Id = NextId(existingIds), Name = rule.Name, Enabled = rule.Enabled,
                    Once = rule.Once, Event = "OnInteractObject", RegionId = "",
                    TargetId = refs[rule.TargetRef], Condition = rule.Condition,
                    Flag = rule.Flag, Action = rule.Action, Message = rule.Message
                });
            }
            things = props;
            triggers = rules;
            return true;
        }

        private static string NextId(HashSet<string> known)
        {
            string id;
            do { id = Guid.NewGuid().ToString("N"); } while (!known.Add(id));
            return id;
        }

        internal static bool TrySaveNew(Layout layout, out string path, out string error)
        {
            path = null;
            error = "Неверный или слишком большой шаблон.";
            if (!Valid(layout)) return false;
            try
            {
                string json = JsonUtility.ToJson(layout, true);
                if (Encoding.UTF8.GetByteCount(json) > MaxFileBytes) return false;
                Directory.CreateDirectory(Folder);
                string destination = Path.Combine(Folder, layout.Name + ".template.json");
                if (File.Exists(destination))
                {
                    error = "Шаблон с таким именем уже существует.";
                    return false;
                }
                string temp = Path.Combine(Folder, "." + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllText(temp, json, new UTF8Encoding(false));
                    // File.Move fails on races; existing templates are never overwritten.
                    File.Move(temp, destination);
                    path = destination;
                    error = "";
                    return true;
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
            }
            catch (Exception e)
            {
                error = "Не удалось сохранить шаблон: " + e.Message;
                return false;
            }
        }

        internal static bool TryLoad(string name, out Layout layout, out string error)
        {
            layout = null;
            error = "Недопустимое имя шаблона.";
            if (!ValidName(name)) return false;
            try
            {
                string path = Path.Combine(Folder, name + ".template.json");
                if (!File.Exists(path) || new FileInfo(path).Length > MaxFileBytes)
                {
                    error = "Шаблон не найден или превышает ограничение размера.";
                    return false;
                }
                var candidate = JsonUtility.FromJson<Layout>(File.ReadAllText(path, Encoding.UTF8));
                if (!Valid(candidate) || candidate.Name != name)
                {
                    error = "Шаблон повреждён или имеет неподдерживаемую версию.";
                    return false;
                }
                layout = candidate;
                error = "";
                return true;
            }
            catch (Exception e)
            {
                error = "Не удалось прочитать шаблон: " + e.Message;
                return false;
            }
        }

        internal static List<string> ListNames()
        {
            var names = new List<string>();
            try
            {
                if (!Directory.Exists(Folder)) return names;
                foreach (string file in Directory.GetFiles(Folder, "*.template.json"))
                {
                    string filename = Path.GetFileName(file);
                    string name = filename.Substring(0, filename.Length - ".template.json".Length);
                    if (!ValidName(name)) continue;
                    if (names.Count >= MaxCatalogEntries) break;
                    names.Add(name);
                }
                names.Sort(StringComparer.Ordinal);
            }
            catch (IOException) { } // inaccessible catalog does not break the editor
            catch (UnauthorizedAccessException) { }
            return names;
        }
    }
}
