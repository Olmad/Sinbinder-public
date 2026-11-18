using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

static class Program
{
    static int passed;
    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("FAIL: " + name);
        passed++;
    }
    static object Invoke(object target, string name, params object[] parameters)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(name);
        return method.Invoke(target, parameters);
    }
    static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(name);
        field.SetValue(target, value);
    }
    static MapData Map()
    {
        var map = new MapData { Name = "Unit test map", Author = "Tests", Size = 300 };
        map.Crypts.Add(new MapData.Crypt { X = 0, Z = -110 });
        map.Crypts.Add(new MapData.Crypt { X = 0, Z = 110 });
        map.Regions.Add(new MapData.Region { Id = "r1", Name = "Test region", X = 1, Z = 1, Radius = 15 });
        map.Things.Add(new MapData.Thing { Id = "p1", Prop = "Pine", X = 3, Z = 4, Scale = 1 });
        return map;
    }
    static void LegacyJsonAndAtomicWrite()
    {
        string root = Path.Combine(Path.GetTempPath(), "Sinbinder.MapEditorChecks." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "legacy.json");
            File.WriteAllText(path, "{\"Name\":\"Legacy\",\"Size\":300,\"Crypts\":[{\"X\":0,\"Z\":-100},{\"X\":0,\"Z\":100}]}");
            var loaded = MapData.Load(path);
            Check(loaded != null && loaded.Players == 2, "load old maps without v2 fields");
            Check(loaded.Regions != null && loaded.Triggers != null, "optional v2 lists normalized");
            Check(loaded.TerrainStrokes != null && loaded.TerrainStrokes.Count == 0, "old maps default to flat terrain");
            loaded.Regions.Add(new MapData.Region { Id = "r1", Radius = 12, Name = "Patrol" });
            loaded.Triggers.Add(new MapData.Trigger
            {
                Id = "t1", RegionId = "r1", Event = "OnEnterRegion",
                Action = "ShowMessage", Message = "A traveller approaches."
            });
            loaded.Save(path);
            var reread = MapData.Load(path);
            Check(reread.Regions.Count == 1 && reread.Triggers.Count == 1,
                  "roundtrip region + ECA trigger");
            string original = File.ReadAllText(path);
            loaded.Name = "Updated";
            loaded.Save(path);
            Check(File.Exists(path + ".bak") && File.ReadAllText(path + ".bak") == original,
                  "atomic overwrite retains the exact previous JSON");
            Check(MapData.Load(path).Name == "Updated", "updated JSON is loadable");
            Check(loaded.Triggers[0].Message == "A traveller approaches.", "literal preserved");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
    static void RulesRuntime()
    {
        var map = Map();
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "startFlag", Event = "OnStart", Action = "SetFlag", Flag = "welcomed"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "startText", Event = "OnStart", Condition = "FlagIsTrue",
            Flag = "welcomed", Action = "ShowMessage", Message = "Welcome"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "regionText", Event = "OnEnterRegion", RegionId = "r1",
            Action = "ShowMessage", Message = "Entering the clearing"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "objectText", Event = "OnInteractObject", TargetId = "p1",
            Action = "ShowMessage", Message = "A cold stone"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "bad", Event = "OnStart", Action = "ExecuteCSharp",
            Message = "System.IO.File.DeleteAll();"
        });
        var runner = new MapLogicRunner();
        Herald.Lines.Clear();
        runner.Configure(map);
        Invoke(runner, "Start");
        Check(Herald.Lines.Count == 1 && Herald.Lines[0] == "Welcome",
              "start event sets flag and conditionally shows message");
        Invoke(runner, "Start");
        Check(Herald.Lines.Count == 1, "one-shot start event does not repeat");
        SinbinderPlayer.Exists = true;
        SinbinderPlayer.Where = new Vector3(1, 0, 1);
        Invoke(runner, "Update");
        Invoke(runner, "Update");
        Check(Herald.Lines.Count == 2 && Herald.Lines[1] == "Entering the clearing",
              "enter-region fires once without repeated frames");
        runner.RegisterObject("p1", new GameObject("Pine").transform);
        Action interact = null;
        MapLogicRunner.OfferInteractions((miss, label, action) => interact = action);
        Check(interact != null, "map object offers interaction through shared F entry");
        interact();
        Check(Herald.Lines.Count == 3 && Herald.Lines[2] == "A cold stone",
              "object interaction executes its configured action");
        interact = null;
        MapLogicRunner.OfferInteractions((miss, label, action) => interact = action);
        Check(interact == null, "used one-shot map action is no longer offered");
        Check(!Herald.Lines.Exists(line => line.Contains("DeleteAll")),
              "unknown action cannot execute script or display success");
        Invoke(runner, "OnDestroy");
        SinbinderPlayer.Exists = false;
    }
    static void BooleanTriggerBranching()
    {
        var map = Map();
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "before", Event = "OnStart", Condition = "FlagIsFalse",
            Flag = "gate", Action = "ShowMessage", Message = "Gate closed"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "open", Event = "OnStart", Action = "SetFlag", Flag = "gate"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "after", Event = "OnStart", Condition = "FlagIsTrue",
            Flag = "gate", Action = "ShowMessage", Message = "Gate opened"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "clear", Event = "OnStart", Action = "ClearFlag", Flag = "gate"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "closed-again", Event = "OnStart", Condition = "FlagIsFalse",
            Flag = "gate", Action = "ShowMessage", Message = "Gate closed again"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "missing", Event = "OnStart", Condition = "FlagIsFalse",
            Flag = "", Action = "ShowMessage", Message = "Should never show"
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "unknown", Event = "OnStart", Action = "ExecuteCSharp",
            Message = "System.IO.File.DeleteAll();"
        });
        var runner = new MapLogicRunner();
        Herald.Lines.Clear();
        runner.Configure(map);
        Invoke(runner, "Start");
        Check(Herald.Lines.Count == 3, "false/true/cleared flag branches all fire in order");
        Check(Herald.Lines[0] == "Gate closed" && Herald.Lines[1] == "Gate opened"
              && Herald.Lines[2] == "Gate closed again", "ordered condition sequence");
        Check(!Herald.Lines.Exists(line => line.Contains("Should never") ||
              line.Contains("DeleteAll")), "missing flag and arbitrary action fail closed");
        Invoke(runner, "Start");
        Check(Herald.Lines.Count == 3, "once-only boolean branches remain once only");
        Invoke(runner, "OnDestroy");

        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        var errors = (List<string>)Invoke(editor, "Validate");
        Check(errors.Exists(error => error.Contains("допустимый флаг")),
              "invalid empty flag rejected at editor save");
        map.Triggers.RemoveAll(t => t.Id == "missing" || t.Id == "unknown");
        errors = (List<string>)Invoke(editor, "Validate");
        Check(errors.Count == 0, "safe false-flag and clear-flag actions are saveable");
    }
    static void ObjectInteractionWizard()
    {
        var map = Map();
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        var item = map.Things[0];
        Invoke(editor, "AddObjectInteraction", item);
        Check(map.Triggers.Count == 1, "selected prop gains a safe interaction rule");
        var rule = map.Triggers[0];
        Check(rule.TargetId == item.Id && rule.Event == "OnInteractObject" &&
              rule.Action == "ShowMessage" && rule.Condition == "Always",
              "interaction wizard binds the exact object's stable ID");
        Check(rule.Enabled && !rule.Once, "interactive object remains available on repeat use");
        Check(((List<string>)Invoke(editor, "Validate")).Count == 0,
              "generated object rule validates for saving");
        Invoke(editor, "Undo");
        var undone = (MapData)typeof(MapEditor).GetField("_map",
                       BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(undone.Triggers.Count == 0, "quick interaction is undoable");
        Invoke(editor, "Redo");
        var redone = (MapData)typeof(MapEditor).GetField("_map",
                       BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(redone.Triggers.Count == 1 && redone.Triggers[0].TargetId == item.Id,
              "redo restores object interaction and its target ID");
    }
    static void EditorContracts()
    {
        var map = Map();
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        var issues = (List<string>)Invoke(editor, "Validate");
        Check(issues.Count == 0, "valid map has no structural errors");
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "badref", Event = "OnEnterRegion",
            RegionId = "missing", Action = "ShowMessage", Message = "Oops"
        });
        issues = (List<string>)Invoke(editor, "Validate");
        Check(issues.Exists(s => s.Contains("отсутствующую область")),
              "invalid region reference blocks saving");
        map.Triggers[0].RegionId = "r1";
        issues = (List<string>)Invoke(editor, "Validate");
        Check(issues.Count == 0, "valid region reference unblocks saving");
        Invoke(editor, "Record");
        map.Things.Add(new MapData.Thing { Id = "p2", Prop = "Rock", Scale = 1 });
        Invoke(editor, "Undo");
        var saved = (MapData)typeof(MapEditor).GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(saved.Things.Count == 1, "Undo restores object list");
        Invoke(editor, "Redo");
        saved = (MapData)typeof(MapEditor).GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(saved.Things.Count == 2, "Redo restores object list");
    }
    static void TerrainRoundtripAndCollider()
    {
        var map = Map();
        map.Size = 320f; // even grid resolution includes an exact centre vertex
        Check(MapTerrain.Valid(map) && MapTerrain.HeightAt(map, 0f, 0f) == 0f &&
              MapTerrain.BuildMesh(map) == null, "legacy flat map keeps original ground");
        var hill = new MapData.TerrainStroke { X = 0f, Z = 0f, Radius = 20f, Delta = 6f };
        map.TerrainStrokes.Add(hill);
        Check(MapTerrain.Valid(map), "bounded hill passes validation");
        Check(MathF.Abs(MapTerrain.HeightAt(map, 0f, 0f) - 6f) < 0.0001f,
              "hill raises exact centre by authored height");
        Check(MathF.Abs(MapTerrain.HeightAt(map, 10f, 0f) - 3.3456f) < 0.0001f &&
              MapTerrain.HeightAt(map, 20f, 0f) == 0f,
              "triangle-aligned terrain height tapers to unchanged edge");
        var mesh = MapTerrain.BuildMesh(map);
        Check(mesh != null && mesh.vertices.Length == 81 * 81 &&
              mesh.triangles.Length == 80 * 80 * 6,
              "real terrain mesh has bounded triangulated geometry");
        Check(MathF.Abs(mesh.vertices[40 * 81 + 40].y - 6f) < 0.0001f,
              "mesh centre vertex follows the same gameplay height function");
        int a = mesh.triangles[0], b = mesh.triangles[1], c = mesh.triangles[2];
        var va = mesh.vertices[a]; var vb = mesh.vertices[b]; var vc = mesh.vertices[c];
        float up = (vb.z - va.z) * (vc.x - va.x) -
                   (vb.x - va.x) * (vc.z - va.z);
        Check(up > 0f, "terrain triangles face up for rendering and collision");

        var ground = new GameObject("Test ground");
        var original = new Mesh();
        ground.GetComponent<MeshFilter>().sharedMesh = original;
        ground.GetComponent<MeshCollider>().sharedMesh = original;
        Mesh generated = null, flat = null;
        MapTerrain.ApplyGround(ground, map, ref generated, ref flat);
        Check(generated != null && ground.GetComponent<MeshFilter>().sharedMesh == generated &&
              ground.GetComponent<MeshCollider>().sharedMesh == generated &&
              ground.transform.localScale.x == 1f,
              "authored ground replaces flat rendering and physics with matching mesh");
        map.TerrainStrokes.Clear();
        MapTerrain.ApplyGround(ground, map, ref generated, ref flat);
        Check(generated == null && ground.GetComponent<MeshFilter>().sharedMesh == original &&
              ground.GetComponent<MeshCollider>().sharedMesh == original &&
              ground.transform.localScale.x == 32f,
              "undoing last hill restores original flat surface and scale");

        map.TerrainStrokes.Add(hill);
        string dir = Path.Combine(Path.GetTempPath(), "Sinbinder.Terrain." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "terrain.json");
            map.Save(path);
            var loaded = MapData.Load(path);
            Check(loaded != null && loaded.TerrainStrokes.Count == 1 &&
                  MathF.Abs(MapTerrain.HeightAt(loaded, 0f, 0f) - 6f) < 0.0001f,
                  "JSON save and load preserve functional sculpted terrain");
        }
        finally { Directory.Delete(dir, true); }

        hill.Delta = -6f;
        Check(MathF.Abs(MapTerrain.HeightAt(map, 0f, 0f) + 6f) < 0.0001f,
              "negative terrain stroke creates a real ravine below original ground");
        var valley = MapTerrain.BuildMesh(map);
        Check(valley != null && MathF.Abs(valley.vertices[40 * 81 + 40].y + 6f) < 0.0001f,
              "ravine geometry is negative in the same gameplay mesh");
        hill.Delta = 6f;

        hill.Delta = float.NaN;
        Check(!MapTerrain.Valid(map) && MapTerrain.BuildMesh(map) == null &&
              MapTerrain.HeightAt(map, 0f, 0f) == 0f,
              "malformed untrusted height data fails closed to flat");
        hill.Delta = 6f;
        hill.Radius = 200f;
        Check(!MapTerrain.Valid(map), "oversized terrain brush is rejected");
        hill.Radius = 20f;
        map.TerrainStrokes.Clear();
        for (int i = 0; i <= MapTerrain.MaxStrokes; i++)
            map.TerrainStrokes.Add(new MapData.TerrainStroke
            { X = 0f, Z = 0f, Radius = 15f, Delta = 8f });
        Check(!MapTerrain.Valid(map), "untrusted terrain stroke count is bounded");
        map.TerrainStrokes.RemoveAt(map.TerrainStrokes.Count - 1);
        Check(MapTerrain.HeightAt(map, 0f, 0f) == MapTerrain.MaxHeight,
              "overlapping hills clamp max world height");

        map.TerrainStrokes.Clear();
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        Invoke(editor, "Sculpt", new Vector3(12f, 0f, -8f));
        Check(map.TerrainStrokes.Count == 1 &&
              map.TerrainStrokes[0].X == 12f && map.TerrainStrokes[0].Delta == 3f,
              "sculpting records an actual positive height stroke");
        Invoke(editor, "Undo");
        var undone = (MapData)typeof(MapEditor).GetField("_map",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(undone.TerrainStrokes.Count == 0, "terrain sculpt is undoable");
        Invoke(editor, "Redo");
        var redone = (MapData)typeof(MapEditor).GetField("_map",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(redone.TerrainStrokes.Count == 1, "terrain sculpt redo recovers height data");
        redone.TerrainStrokes[0].Radius = -1f;
        Check(((List<string>)Invoke(editor, "Validate")).Exists(e => e.Contains("рельефа")),
              "bad brush parameters block editor save");
    }

    static void Main()
    {
        LegacyJsonAndAtomicWrite();
        RulesRuntime();
        BooleanTriggerBranching();
        ObjectInteractionWizard();
        EditorContracts();
        TerrainRoundtripAndCollider();
        Console.WriteLine("PASS: " + passed + " real C# compiled map-editor assertions (Unity API shim, NOT Unity PlayMode).");
    }
}
