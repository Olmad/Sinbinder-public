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

    static void MaterialsAndVegetation()
    {
        Check(MapAssets.ValidId("Ground048") && MapAssets.ValidId("User_Rock-2") &&
              !MapAssets.ValidId("../evil") && !MapAssets.ValidId("a/b") &&
              !MapAssets.ValidId("a:b") && !MapAssets.ValidId(""),
              "map asset identifiers reject path traversal and executable references");
        Resources.Register("MapUserProps/CustomFern", new GameObject("CustomFern"));
        Resources.Register("MapUserMaterials/Leaf", new Material(Shader.Find("Lit")) { name = "Leaf" });
        Check(MapAssets.IsUserProp("user:CustomFern") &&
              MapAssets.UserPrefab("user:CustomFern") != null &&
              MapAssets.UserPrefab("user:../../escape") == null,
              "Unity imported prefab is addressable only through safe catalog");
        var instance = new GameObject("instance");
        MapAssets.ApplyObjectMaterial(instance, "Leaf");
        Check(instance.GetComponent<Renderer>().sharedMaterial ==
              MapAssets.ObjectMaterial("Leaf"), "imported object material overrides actual renderer");
        var map = Map();
        map.TerrainPaints.Add(new MapData.TerrainPaint
        { X = 0f, Z = 0f, Radius = 24f, MaterialId = "Ground048" });
        Check(MapTerrain.Valid(map) && MapTerrain.HeightAt(map, 0f, 0f) == 0f,
              "terrain paint on a flat map keeps zero-height physics");
        Check(MapTerrain.PaintedMaterialAt(map, 0f, 0f) == "Ground048" &&
              MapTerrain.PaintedMaterialAt(map, 100f, 100f) == "",
              "paint affects only cells inside its authored radius");
        var mesh = MapTerrain.BuildMesh(map);
        Check(mesh != null && mesh.subMeshCount == 2 &&
              mesh.triangles.Length == 75 * 75 * 6,
              "paint partitions real terrain mesh into material submeshes");
        // Map() has 300 metres, so GridCells = 75.
        var ground = new GameObject("painted");
        var initial = new Material(Shader.Find("Lit")) { name = "OriginalGround" };
        ground.GetComponent<MeshRenderer>().sharedMaterial = initial;
        ground.GetComponent<MeshFilter>().sharedMesh = new Mesh();
        ground.GetComponent<MeshCollider>().sharedMesh = new Mesh();
        Mesh generated = null, original = null;
        MapTerrain.ApplyGround(ground, map, ref generated, ref original);
        var mats = ground.GetComponent<MeshRenderer>().sharedMaterials;
        Check(mats.Length == 2 && mats[0] == initial && mats[1].name == "Ground048" &&
              ground.GetComponent<MeshCollider>().sharedMesh == generated,
              "terrain brush assigns a real bundled Unity material to its mesh and collider");
        map.TerrainPaints.Add(new MapData.TerrainPaint
        { X = 0f, Z = 0f, Radius = 8f, MaterialId = "Ground077" });
        Check(MapTerrain.PaintedMaterialAt(map, 0f, 0f) == "Ground077" &&
              MapTerrain.PaintedMaterialAt(map, 15f, 0f) == "Ground048",
              "newer material strokes win only within their brush radius");
        map.TerrainPaints[1].MaterialId = "../../hack";
        Check(!MapTerrain.Valid(map) && MapTerrain.BuildMesh(map) == null,
              "untrusted terrain material names fail validation");
        map.TerrainPaints[1].MaterialId = "Ground077";
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        Set(editor, "_paintMaterial", "Gravel043");
        Invoke(editor, "PaintTerrain", new Vector3(42f, 0f, 30f));
        Check(map.TerrainPaints.Count == 3 &&
              MapTerrain.PaintedMaterialAt(map, 42f, 30f) == "Gravel043",
              "editor material paint adds persisted actual terrain stroke");
        Invoke(editor, "Undo");
        var undo = (MapData)typeof(MapEditor).GetField("_map",
                       BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(undo.TerrainPaints.Count == 2,
              "terrain material painting is undoable");
        Invoke(editor, "Redo");
        var redo = (MapData)typeof(MapEditor).GetField("_map",
                       BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(redo.TerrainPaints.Count == 3,
              "terrain material painting is redoable");
        redo.TerrainPaints[0].MaterialId = "Ground048";
        string root = Path.Combine(Path.GetTempPath(), "Sinbinder.Paint." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "paint.json");
            redo.Things[0].MaterialId = "Leaf";
            redo.Save(path);
            var loaded = MapData.Load(path);
            Check(loaded.TerrainPaints.Count == 3 &&
                  loaded.TerrainPaints[2].MaterialId == "Gravel043" &&
                  loaded.Things[0].MaterialId == "Leaf",
                  "paint and object material override survive JSON roundtrip");
        }
        finally { Directory.Delete(root, true); }

        var garden = Map();
        var vegetation = new MapEditor();
        Set(vegetation, "_map", garden);
        Set(vegetation, "_name", garden.Name);
        Invoke(vegetation, "ScatterVegetation", new Vector3(40f, 0f, 30f));
        Check(garden.Things.Count == 13 &&
              garden.Things[1].Prop == "GrassTuft" &&
              garden.Things[12].Id != garden.Things[1].Id,
              "vegetation brush places twelve terrain-aware unique map props");
        Invoke(vegetation, "Undo");
        var before = (MapData)typeof(MapEditor).GetField("_map",
                         BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vegetation);
        Check(before.Things.Count == 1, "one undo removes full vegetation cluster");
        Invoke(vegetation, "Redo");
        var after = (MapData)typeof(MapEditor).GetField("_map",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vegetation);
        Check(after.Things.Count == 13, "redo restores full vegetation cluster");
        foreach (var biome in new[] { "Forest", "DeadForest", "Meadow", "Wasteland" })
        {
            var world = Map();
            var biomes = new MapEditor();
            Set(biomes, "_map", world);
            Set(biomes, "_name", world.Name);
            Set(biomes, "_vegetationPreset", biome);
            Invoke(biomes, "ScatterVegetation", new Vector3(40f, 0f, 30f));
            var kinds = new HashSet<string>();
            for (int i = 1; i < world.Things.Count; i++)
                kinds.Add(world.Things[i].Prop);
            Check(world.Things.Count == 13 && kinds.Count >= 2,
                  "biome " + biome + " creates a mixed authored nature group");
            Invoke(biomes, "Undo");
            var clean = (MapData)typeof(MapEditor).GetField("_map",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(biomes);
            Check(clean.Things.Count == 1,
                  "biome " + biome + " is one undoable group command");
        }
    }

    static void DragAndFlattenTools()
    {
        var map = Map();
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        var modeField = typeof(MapEditor).GetField("_mode",
            BindingFlags.Instance | BindingFlags.NonPublic);
        modeField.SetValue(editor, Enum.Parse(modeField.FieldType, "Terrain"));
        var start = new Vector3(30f, 0f, 35f);
        Invoke(editor, "Sculpt", start);
        Set(editor, "_lastBrush", start);
        Invoke(editor, "ContinueBrush", new Vector3(65f, 0f, 35f));
        Check(map.TerrainStrokes.Count > 1 &&
              map.TerrainStrokes.Count <= 1 + 8,
              "held terrain brush adds spaced bounded impressions");
        Invoke(editor, "Undo");
        var undone = (MapData)typeof(MapEditor).GetField("_map",
                       BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(undone.TerrainStrokes.Count == 0,
              "one undo removes entire dragged terrain gesture");
        Invoke(editor, "Redo");
        var redone = (MapData)typeof(MapEditor).GetField("_map",
                       BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(redone.TerrainStrokes.Count > 1,
              "redo restores all samples of dragged terrain gesture");

        Set(editor, "_terrainFlatten", true);
        Invoke(editor, "Sculpt", new Vector3(34f, 0f, 35f));
        Check(redone.TerrainStrokes[redone.TerrainStrokes.Count - 1].Mode == "Flatten",
              "flatten brush produces serialized safe terrain mode");
        Invoke(editor, "Undo");

        var paint = Map();
        var painter = new MapEditor();
        Set(painter, "_map", paint);
        Set(painter, "_name", paint.Name);
        modeField.SetValue(painter, Enum.Parse(modeField.FieldType, "Paint"));
        var at = new Vector3(-15f, 0f, -15f);
        Invoke(painter, "PaintTerrain", at);
        Set(painter, "_lastBrush", at);
        Invoke(painter, "ContinueBrush", new Vector3(25f, 0f, -15f));
        Check(paint.TerrainPaints.Count > 1,
              "paint brush supports continuous real material placement");
        Invoke(painter, "Undo");
        var paintUndone = (MapData)typeof(MapEditor).GetField("_map",
                          BindingFlags.Instance | BindingFlags.NonPublic).GetValue(painter);
        Check(paintUndone.TerrainPaints.Count == 0,
              "one undo removes entire dragged paint gesture");
        Invoke(painter, "ShowBrushCursor", at);
        var preview = (GameObject)typeof(MapEditor).GetField("_brushCursor",
                      BindingFlags.Instance | BindingFlags.NonPublic).GetValue(painter);
        Check(preview != null && preview.activeSelf,
              "brush has visible preview footprint over terrain");
        Invoke(painter, "HideBrushCursor");
        Check(!preview.activeSelf, "brush preview hides outside the paint canvas");
    }

    static void AuthoringImportContracts()
    {
        var importer = typeof(Sinbinder.EditorTools.MapAssetImporter);
        var valid = importer.GetMethod("ValidName", BindingFlags.Static | BindingFlags.NonPublic);
        Check(valid != null && (bool)valid.Invoke(null, new object[] { "Rock_02" }) &&
              (bool)valid.Invoke(null, new object[] { "Forest-Moss" }),
              "real compiled Unity Editor importer accepts safe asset IDs");
        Check(!(bool)valid.Invoke(null, new object[] { "../Secrets" }) &&
              !(bool)valid.Invoke(null, new object[] { "a/b" }) &&
              !(bool)valid.Invoke(null, new object[] { "" }) &&
              !(bool)valid.Invoke(null, new object[] { new string('x', 49) }),
              "real compiled Unity Editor importer blocks traversal and invalid IDs");
    }

    static void ExternalEditConflictAndSafeCopy()
    {
        string folder = Path.Combine(Path.GetTempPath(), "Sinbinder.EditorConflict." +
                                     Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "Map.json");
            var source = Map();
            source.Name = "Original map";
            source.Save(path);
            var originalBytes = File.ReadAllText(path);

            bool collision = false;
            try { source.Save(path, overwrite: false); }
            catch (IOException) { collision = true; }
            Check(collision && File.ReadAllText(path) == originalBytes,
                  "create-only JSON cannot overwrite an existing map");

            var editor = new MapEditor();
            Set(editor, "_map", MapData.Load(path));
            Set(editor, "_name", "Local work");
            Set(editor, "_path", path);
            var fingerprint = typeof(MapEditor).GetMethod("FileFingerprint",
                BindingFlags.Static | BindingFlags.NonPublic);
            Set(editor, "_openedFingerprint",
                fingerprint.Invoke(null, new object[] { path }));
            Invoke(editor, "Save");
            Check(MapData.Load(path).Name == "Local work",
                  "unchanged map can save normally");
            Check(File.Exists(path + ".bak") &&
                  File.ReadAllText(path + ".bak") == originalBytes,
                  "normal save preserves exact previous bytes as backup");

            string conflictingText = File.ReadAllText(path).Replace("Local work", "Someone else");
            File.WriteAllText(path, conflictingText);
            Set(editor, "_name", "Unsaved edits");
            Invoke(editor, "Save");
            Check(File.ReadAllText(path) == conflictingText &&
                  MapData.Load(path).Name == "Someone else",
                  "external edits cannot be silently overwritten");
            var statusField = typeof(MapEditor).GetField("_say",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(((string)statusField.GetValue(editor)).Contains("изменён"),
                  "external-edit conflict reports actionable UI warning");
            Invoke(editor, "SaveCopy");
            var newPath = (string)typeof(MapEditor).GetField("_path",
                          BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            Check(newPath != path && Path.GetDirectoryName(newPath) == folder &&
                  MapData.Load(newPath).Name == "Unsaved edits" &&
                  File.ReadAllText(path) == conflictingText,
                  "save-copy creates a separate map beside original without changing it");
            Invoke(editor, "Save");
            Check(MapData.Load(newPath).Name == "Unsaved edits",
                  "safe copy becomes the tracked map for subsequent saves");

            var damaged = Map();
            damaged.Name = "Readable lobby data";
            MapSession.Choose(path, damaged);
            File.WriteAllText(path, "{not parseable json}");
            var reader = new MapEditor();
            Invoke(reader, "Start");
            Set(reader, "_name", "Recovered work");
            Invoke(reader, "Save");
            Check(File.ReadAllText(path) == "{not parseable json}",
                  "unreadable source remains untouched when saving after opening");
            Invoke(reader, "SaveCopy");
            Check(File.ReadAllText(path) == "{not parseable json}",
                  "copying a damaged source keeps its original bytes intact");

            var newest = Map();
            newest.Name = "Disk version";
            newest.Save(path);
            var stale = Map();
            stale.Name = "Stale lobby version";
            MapSession.Choose(path, stale);
            var freshEditor = new MapEditor();
            Invoke(freshEditor, "Start");
            var active = (MapData)typeof(MapEditor).GetField("_map",
                         BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freshEditor);
            Check(active.Name == "Disk version",
                  "opening editor reads latest disk map, not outdated lobby snapshot");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
            MapSession.Path = null;
            MapSession.Map = null;
            MapSession.Seats.Clear();
        }
    }

    static void FastBrushPreviewAndSelectionHeight()
    {
        var map = Map();
        var brush = new MapEditor();
        Set(brush, "_map", map);
        Set(brush, "_name", map.Name);
        Invoke(brush, "Redraw");
        var previewField = typeof(MapEditor).GetField("_shown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (List<GameObject>)previewField.GetValue(brush);
        Check(original.Count > 1, "existing map provides preview models and markers");
        GameObject firstObject = original[0];
        float initialY = firstObject.transform.position.y;
        Invoke(brush, "Sculpt", new Vector3(3f, 0f, 4f));
        var afterSculpt = (List<GameObject>)previewField.GetValue(brush);
        Check(ReferenceEquals(firstObject, afterSculpt[0]) &&
              afterSculpt.Count == original.Count,
              "height painting preserves existing preview objects and marker count");
        Check(firstObject.transform.position.y > initialY,
              "existing preview objects follow newly sculpted collision height");

        var kindField = typeof(MapEditor).GetField("_selectionKind",
            BindingFlags.Instance | BindingFlags.NonPublic);
        kindField.SetValue(brush, Enum.Parse(kindField.FieldType, "Thing"));
        Set(brush, "_selectionIndex", 0);
        Invoke(brush, "Redraw");
        var shown = (List<GameObject>)previewField.GetValue(brush);
        var selection = shown[shown.Count - 1];
        float expected = MapTerrain.HeightAt(map, map.Things[0].X,
            map.Things[0].Z) + 0.12f;
        Check(MathF.Abs(selection.transform.position.y - expected) < 0.001f,
              "selection indicator follows terrain once without doubled height");

        var painter = new MapEditor();
        Set(painter, "_map", map);
        Set(painter, "_name", map.Name);
        Invoke(painter, "Redraw");
        var beforePaint = (List<GameObject>)previewField.GetValue(painter);
        var existing = beforePaint[0];
        Invoke(painter, "PaintTerrain", new Vector3(10f, 0f, 10f));
        var afterPaint = (List<GameObject>)previewField.GetValue(painter);
        Check(ReferenceEquals(existing, afterPaint[0]) &&
              beforePaint.Count == afterPaint.Count,
              "material painting changes only terrain mesh and never respawns nature props");
        Invoke(painter, "Undo");
        var undone = (MapData)typeof(MapEditor).GetField("_map",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(painter);
        Check(undone.TerrainPaints.Count == 0 &&
              undone.TerrainStrokes.Count == 1,
              "fast paint undo removes paint while preserving sculpted heights");
    }

    static void PaintEraserAndMaterialSlots()
    {
        var map = Map();
        map.TerrainPaints.Add(new MapData.TerrainPaint
        { X = 0f, Z = 0f, Radius = 40f, MaterialId = "Ground048" });
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        Set(editor, "_paintMaterial", MapTerrain.RestoreOriginalMaterial);
        Invoke(editor, "PaintTerrain", new Vector3(0f, 0f, 0f));
        Check(map.TerrainPaints.Count == 2 && MapTerrain.Valid(map),
              "paint eraser adds valid serializable stroke");
        Check(MapTerrain.PaintedMaterialAt(map, 0f, 0f) == "" &&
              MapTerrain.PaintedMaterialAt(map, 25f, 0f) == "Ground048",
              "paint eraser restores original terrain only within brush footprint");
        Check(MapTerrain.PaintMaterialIds(map).Count == 2 &&
              MapTerrain.BuildMesh(map).subMeshCount == 2,
              "eraser does not consume extra material slot or submesh");
        Check(((List<string>)Invoke(editor, "Validate")).Count == 0,
              "paint eraser passes whole editor save validation");
        Invoke(editor, "Undo");
        var undone = (MapData)typeof(MapEditor).GetField("_map",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(undone.TerrainPaints.Count == 1 &&
              MapTerrain.PaintedMaterialAt(undone, 0f, 0f) == "Ground048",
              "erasing a paint stroke is undoable back to previous material");
    }

    static void UnsavedMapExitGuard()
    {
        var map = Map();
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        Set(editor, "_savedSnapshot", JsonUtility.ToJson(map));
        UnityEngine.SceneManagement.SceneManager.LastLoadedScene = -1;
        Invoke(editor, "RequestExit");
        Check(UnityEngine.SceneManagement.SceneManager.LastLoadedScene == 0,
              "clean map exits without redundant warning");

        UnityEngine.SceneManagement.SceneManager.LastLoadedScene = -1;
        Set(editor, "_dirty", true);
        Invoke(editor, "RequestExit");
        var confirm = typeof(MapEditor).GetField("_confirmExit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(UnityEngine.SceneManagement.SceneManager.LastLoadedScene == -1 &&
              (bool)confirm.GetValue(editor),
              "first exit click cannot discard unsaved authored edits");
        Invoke(editor, "Record");
        Check(!(bool)confirm.GetValue(editor),
              "making another edit resets any stale destructive confirmation");
        Invoke(editor, "RequestExit");
        Check(UnityEngine.SceneManagement.SceneManager.LastLoadedScene == -1,
              "exit is still blocked after further edits");
        Invoke(editor, "RequestExit");
        Check(UnityEngine.SceneManagement.SceneManager.LastLoadedScene == 0,
              "second explicit confirmation permits intentional discard");

        var other = new MapEditor();
        var original = Map();
        Set(other, "_map", original);
        Set(other, "_savedSnapshot", JsonUtility.ToJson(original));
        Invoke(other, "Record");
        original.Things.Add(new MapData.Thing { Id = "extra", Prop = "Rock", Scale = 1 });
        Invoke(other, "Undo");
        var dirty = typeof(MapEditor).GetField("_dirty",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(!(bool)dirty.GetValue(other),
              "undoing all edits to last saved snapshot clears unsaved state");
    }

    static void GroupEditingContracts()
    {
        var map = Map();
        map.Things.Add(new MapData.Thing { Id = "p2", Prop = "Rock", X = 20, Z = 4, Scale = 1.2f });
        map.Things.Add(new MapData.Thing { Id = "p3", Prop = "Bush", X = 20, Z = 22, Scale = 0.8f });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "talk1", Event = "OnInteractObject", TargetId = "p1",
            Action = "ShowMessage", Message = "Hello", Enabled = true
        });
        var e = new MapEditor();
        Set(e, "_map", map);
        Set(e, "_name", map.Name);
        var field = typeof(MapEditor).GetField("_selectedThingIds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var selected = (HashSet<string>)field.GetValue(e);

        Invoke(e, "Select", new Vector3(3, 0, 4), false);
        Check(selected.SetEquals(new[] { "p1" }),
              "ordinary selection has exactly one stable Thing ID");
        Invoke(e, "Select", new Vector3(20, 0, 4), true);
        Check(selected.SetEquals(new[] { "p1", "p2" }),
              "Shift selection adds another prop without clearing the first");
        Invoke(e, "Select", new Vector3(20, 0, 4), true);
        Check(selected.SetEquals(new[] { "p1" }),
              "Shift-click on selected prop toggles it off");
        Invoke(e, "SelectAllThings");
        Check(selected.Count == 3 && selected.Contains("p3"),
              "select all selects three props without crypts or regions");

        float cryptX = map.Crypts[0].X, regionX = map.Regions[0].X;
        Invoke(e, "MoveSelectedThings", 7f, -2f);
        Check(map.Things[0].X == 10f && map.Things[1].X == 27f &&
              map.Things[2].Z == 20f && map.Crypts[0].X == cryptX &&
              map.Regions[0].X == regionX,
              "moving group preserves relative positions and does not touch gameplay anchors");
        Invoke(e, "Undo");
        var undone = (MapData)typeof(MapEditor).GetField("_map",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(e);
        Check(undone.Things[0].X == 3 && undone.Things[1].X == 20 &&
              undone.Things[2].Z == 22,
              "one Undo restores all group members to original positions");
        Invoke(e, "Redo");
        var moved = (MapData)typeof(MapEditor).GetField("_map",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(e);
        Check(moved.Things[0].X == 10 && moved.Things[1].X == 27,
              "one Redo restores full group move");

        Invoke(e, "SelectAllThings");
        float cx = 0f, cz = 0f;
        foreach (var t in moved.Things) { cx += t.X; cz += t.Z; }
        cx /= moved.Things.Count; cz /= moved.Things.Count;
        float distance = (moved.Things[0].X - cx) * (moved.Things[0].X - cx) +
                         (moved.Things[0].Z - cz) * (moved.Things[0].Z - cz);
        Invoke(e, "RotateSelectedThings", 90f);
        float newDistance = (moved.Things[0].X - cx) * (moved.Things[0].X - cx) +
                            (moved.Things[0].Z - cz) * (moved.Things[0].Z - cz);
        Check(MathF.Abs(distance - newDistance) < 0.001f &&
              moved.Things[0].Yaw == 90f && moved.Crypts[0].Yaw == 0f,
              "group rotates around its centroid with matching Unity yaw");

        float oldScale = moved.Things[0].Scale;
        Invoke(e, "ScaleSelectedThings", 1.1f);
        Check(MathF.Abs(moved.Things[0].Scale - oldScale * 1.1f) < 0.001f &&
              MathF.Abs(moved.Things[1].Scale - 1.2f * 1.1f) < 0.001f,
              "group scaling changes size of every selected prop");
        float afterScale = moved.Things[0].Scale;
        Invoke(e, "ScaleSelectedThings", 100f);
        Check(MathF.Abs(moved.Things[0].Scale - afterScale) < 0.001f &&
              MathF.Abs(moved.Things[1].Scale - 1.2f * 1.1f) < 0.001f,
              "invalid group scaling fails without partial mutation");
        Resources.Register("MapUserMaterials/Stone", new Material(Shader.Find("Lit"))
        { name = "Stone" });
        Invoke(e, "ApplyGroupMaterial", "Stone");
        Check(moved.Things.TrueForAll(t => t.MaterialId == "Stone"),
              "one material action changes all selected prop renderers");
        Invoke(e, "ApplyGroupMaterial", "DoesNotExist");
        Check(moved.Things.TrueForAll(t => t.MaterialId == "Stone"),
              "missing imported material is rejected without touching group");
        Invoke(e, "DuplicateSelectedThings");
        Check(moved.Things.Count == 6 && moved.Triggers.Count == 2,
              "group copy duplicates three real props plus their F-interaction rules");
        var ids = new HashSet<string>();
        foreach (var t in moved.Things) ids.Add(t.Id);
        Check(ids.Count == 6 && moved.Things[3].Id != "p1" &&
              moved.Things[3].MaterialId == "Stone" &&
              moved.Triggers[1].TargetId == moved.Things[3].Id &&
              moved.Triggers[1].Message == moved.Triggers[0].Message,
              "copied objects and interaction references receive new IDs without losing text");
        Check(selected.SetEquals(new[] { moved.Things[3].Id,
                                        moved.Things[4].Id, moved.Things[5].Id }),
              "group copy selects only new objects");
        Invoke(e, "DeleteSelectedThings");
        Check(moved.Things.Count == 3 && moved.Triggers.Count == 1 &&
              moved.Triggers[0].TargetId == "p1",
              "group delete removes only selected copies and their dependent interactions");
        Invoke(e, "Undo");
        var restored = (MapData)typeof(MapEditor).GetField("_map",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(e);
        Check(restored.Things.Count == 6 && restored.Triggers.Count == 2,
              "group deletion and related rules undo together");

        var nearBorder = Map();
        nearBorder.Things[0].X = 149f;
        var boundaryEditor = new MapEditor();
        Set(boundaryEditor, "_map", nearBorder);
        Set(boundaryEditor, "_name", nearBorder.Name);
        Invoke(boundaryEditor, "SelectAllThings");
        Invoke(boundaryEditor, "MoveSelectedThings", 5f, 0f);
        Check(nearBorder.Things[0].X == 149f,
              "out-of-bounds group action fails before mutating any member");

        var single = Map();
        single.Triggers.Add(new MapData.Trigger
        {
            Id = "linked", Event = "OnInteractObject", TargetId = "p1",
            Action = "ShowMessage", Message = "Test"
        });
        var se = new MapEditor();
        Set(se, "_map", single);
        Set(se, "_name", single.Name);
        Invoke(se, "Select", new Vector3(3,0,4), false);
        Invoke(se, "DeleteSelected");
        Check(single.Things.Count == 0 && single.Triggers.Count == 0,
              "single delete never strands dangling object-interaction rule");
    }

    static void RectangleSelectionContracts()
    {
        var map = Map();
        map.Things.Add(new MapData.Thing { Id = "p2", Prop = "Rock",
            X = 20f, Z = 5f, Scale = 1f });
        map.Things.Add(new MapData.Thing { Id = "p3", Prop = "Pine",
            X = 30f, Z = 35f, Scale = 1f });
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        string original = JsonUtility.ToJson(map);
        var f = typeof(MapEditor).GetField("_selectedThingIds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var selected = (HashSet<string>)f.GetValue(editor);
        Invoke(editor, "SelectRectangle",
            new Vector3(21f, 0f, 10f), new Vector3(0f, 0f, 0f), false);
        Check(selected.SetEquals(new[] { "p1", "p2" }),
              "drag rectangle works with reverse corners and excludes unboxed props");
        Check(map.Crypts.Count == 2 && map.Regions.Count == 1 &&
              JsonUtility.ToJson(map) == original,
              "marquee does not modify map data, crypts or regions");
        Invoke(editor, "SelectRectangle",
            new Vector3(25f, 0f, 25f), new Vector3(35f, 0f, 40f), true);
        Check(selected.SetEquals(new[] { "p1", "p2", "p3" }),
              "Shift drag adds distant props without clearing earlier selection");
        Invoke(editor, "SelectRectangle",
            new Vector3(-40f, 0f, -40f), new Vector3(-10f, 0f, -10f), false);
        Check(selected.Count == 0,
              "dragging an empty rectangle clears selection in normal mode");
    }

    static void MissingPreviewAssetsAreVisible()
    {
        var map = Map();
        map.Things[0].Prop = "NoSuchTree";
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        Invoke(editor, "Redraw");

        var missingField = typeof(MapEditor).GetField("_missingPreviewModels",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var missing = (HashSet<string>)missingField.GetValue(editor);
        var shownField = typeof(MapEditor).GetField("_shown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var shown = (List<GameObject>)shownField.GetValue(editor);
        Check(missing.Contains("NoSuchTree"),
              "missing tree ID appears in the editor's visible diagnostic list");
        Check(shown.Exists(x => x.name == "Отсутствующая модель карты: NoSuchTree"),
              "missing tree asset is represented by a bright named warning marker");
        Check(shown.Count >= map.Things.Count,
              "missing models no longer disappear from authored map preview");

        Resources.Register("MapProps/NoSuchTree", new GameObject("NoSuchTree"));
        Resources.Register("MapProps/Crypt", new GameObject("Crypt"));
        Invoke(editor, "Redraw");
        missing = (HashSet<string>)missingField.GetValue(editor);
        var count = (int)typeof(MapEditor).GetField("_loadedPreviewModels",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(!missing.Contains("NoSuchTree") && count >= 1,
              "reloading a now-available prefab replaces warning with normal model");
    }

    static void AuthoringCameraFallback()
    {
        var oldCam = Camera.main;
        var oldMap = MapSession.Map;
        var oldPath = MapSession.Path;
        try
        {
            Camera.main = null;
            MapSession.Map = Map();
            MapSession.Path = null;
            var editor = new MapEditor();
            Invoke(editor, "Start");
            var camera = (Camera)typeof(MapEditor).GetField("_cam",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            Check(camera != null && camera.enabled &&
                  camera.clearFlags == CameraClearFlags.SolidColor,
                  "editor creates an enabled visible camera when MainCamera is missing");
            Check(camera.backgroundColor.r >= 0.25f,
                  "authoring camera uses a neutral nonblack background");
            Set(editor, "_height", 250f);
            Invoke(editor, "CenterCamera");
            var value = (float)typeof(MapEditor).GetField("_height",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            Check(value == 70f && camera.transform.position.y == 70f,
                  "recenter command restores map camera position");
        }
        finally
        {
            Camera.main = oldCam;
            MapSession.Map = oldMap;
            MapSession.Path = oldPath;
        }
    }

    static void WorldForgeBestImprovementsContracts()
    {
        var map = Map();
        map.Things.Add(new MapData.Thing { Id = "p2", Prop = "Rock", X = 20f, Z = 4f, Scale = 1f });
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        Set(editor, "_snap", 2f);
        Invoke(editor, "SelectAllThings");
        Check((bool)Invoke(editor, "NearSelectedThing", new Vector3(3f, 0f, 4f)),
              "only already-selected props can start a world drag");
        Check(!(bool)Invoke(editor, "NearSelectedThing", new Vector3(100f, 0f, 100f)),
              "clicking empty ground does not start a group drag");
        Invoke(editor, "CommitSelectionDrag",
               new Vector3(3f, 0f, 4f), new Vector3(7.8f, 0f, 7.2f));
        Check(map.Things[0].X == 7f && map.Things[0].Z == 8f &&
              map.Things[1].X == 24f && map.Things[1].Z == 8f,
              "viewport drag moves selected group by a snapped relative delta");
        Check(map.Crypts[0].X == 0f && map.Regions[0].X == 1f &&
              map.Triggers.Count == 0,
              "viewport group move does not mutate gameplay anchors or trigger references");
        var undoField = typeof(MapEditor).GetField("_undo",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(((List<string>)undoField.GetValue(editor)).Count == 1,
              "one viewport group drag makes one undo transaction");
        Invoke(editor, "Undo");
        var reverted = (MapData)typeof(MapEditor).GetField("_map",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(reverted.Things[0].X == 3f && reverted.Things[1].X == 20f,
              "one Undo reverts every moved prop");
        Invoke(editor, "Redo");
        var movedAgain = (MapData)typeof(MapEditor).GetField("_map",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        Check(movedAgain.Things[0].X == 7f && movedAgain.Things[1].X == 24f,
              "Redo restores viewport group move");

        // A near-edge drag must reject the entire move without recording history.
        var edgeMap = Map();
        edgeMap.Things[0].X = 149f;
        var edgeEditor = new MapEditor();
        Set(edgeEditor, "_map", edgeMap);
        Set(edgeEditor, "_name", edgeMap.Name);
        Set(edgeEditor, "_snap", 2f);
        Invoke(edgeEditor, "SelectAllThings");
        Invoke(edgeEditor, "CommitSelectionDrag",
               new Vector3(149f, 0f, 4f), new Vector3(154f, 0f, 4f));
        Check(edgeMap.Things[0].X == 149f &&
              ((List<string>)undoField.GetValue(edgeEditor)).Count == 0,
              "out-of-bounds viewport drags are rejected atomically");

        var camera = new Camera();
        Set(editor, "_cam", camera);
        Invoke(editor, "SelectAllThings");
        Invoke(editor, "FocusSelection");
        Check(MathF.Abs(camera.transform.position.x - 15.5f) < 0.001f &&
              MathF.Abs(camera.transform.position.z - (8f - 70f * 0.35f)) < 0.001f,
              "Ctrl+F focus centers the existing editor camera on selected group centroid");

        var invalid = Map();
        invalid.Things[0].X = 1000f;
        invalid.Regions[0].Radius = -1f;
        var diagnostics = new MapEditor();
        Set(diagnostics, "_map", invalid);
        Set(diagnostics, "_name", invalid.Name);
        int count = (int)Invoke(diagnostics, "RunValidation");
        var issueField = typeof(MapEditor).GetField("_validationIssues",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var issues = (List<string>)issueField.GetValue(diagnostics);
        Check(count >= 2 && issues.Count == count,
              "validation retains all map errors instead of the first only");
        Invoke(diagnostics, "Record");
        Check(issues.Count == 0,
              "a new edit invalidates cached diagnostics rather than showing stale errors");
    }

    static void LayoutPlannerContracts()
    {
        var things = new List<MapData.Thing>
        {
            new MapData.Thing { Id = "one", Prop = "Pine", X = 3, Z = 4, Yaw = 15, Scale = 1.2f },
            new MapData.Thing { Id = "two", Prop = "Rock", X = 9, Z = 8, Yaw = 45, Scale = 0.8f },
            new MapData.Thing { Id = "three", Prop = "Bush", X = 16, Z = 12, Yaw = 90, Scale = 1.5f },
            new MapData.Thing { Id = "four", Prop = "Pine", X = 30, Z = 20, Yaw = 180, Scale = 1f },
        };
        Check(MapObjectArrange.TryPlan(things, 300f, MapObjectArrange.Axis.X,
              MapObjectArrange.Operation.Distribute, out var spaced),
              "spacing planner accepts four nonuniform selected props");
        Check(spaced[0].x == 3f && spaced[1].x == 12f &&
              spaced[2].x == 21f && spaced[3].x == 30f,
              "equal-X spacing keeps extremes and correctly interpolates inner props");
        Check(things[1].X == 9f && spaced[1].y == 8f,
              "planning is pure and leaves the other axis unchanged");
        Check(MapObjectArrange.TryPlan(things, 300f, MapObjectArrange.Axis.Z,
              MapObjectArrange.Operation.Minimum, out var minZ) &&
              minZ[0].y == 4f && minZ[1].y == 4f && minZ[2].y == 4f && minZ[3].y == 4f && minZ[1].x == 9f,
              "Z minimum alignment preserves all X values");
        Check(MapObjectArrange.TryPlan(things, 300f, MapObjectArrange.Axis.X,
              MapObjectArrange.Operation.Center, out var middleX) &&
              MathF.Abs(middleX[0].x - 16.5f) < 0.0001f &&
              MathF.Abs(middleX[3].x - 16.5f) < 0.0001f,
              "X center alignment uses bounding-box midpoint rather than arbitrary first prop");
        Check(MapObjectArrange.TryPlan(things, 300f, MapObjectArrange.Axis.X,
              MapObjectArrange.Operation.Maximum, out var maxX) &&
              maxX[0].x == 30f && maxX[3].x == 30f,
              "maximum alignment targets rightmost selected prop");

        var tie = new List<MapData.Thing>
        {
            new MapData.Thing { Id = "a", X = 3, Z = 0 },
            new MapData.Thing { Id = "b", X = 3, Z = 0 },
            new MapData.Thing { Id = "c", X = 10, Z = 0 },
            new MapData.Thing { Id = "d", X = 20, Z = 0 },
        };
        Check(MapObjectArrange.TryPlan(tie, 300f, MapObjectArrange.Axis.X,
              MapObjectArrange.Operation.Distribute, out var stable) &&
              stable[0].x == 3f && stable[1].x > 3f && stable[2].x > stable[1].x &&
              stable[3].x == 20f,
              "tied coordinates use original selection order for deterministic distribution");
        Check(!MapObjectArrange.TryPlan(new List<MapData.Thing> { things[0], things[1] },
              300f, MapObjectArrange.Axis.X, MapObjectArrange.Operation.Distribute,
              out _),
              "two objects cannot be distributed into intermediate equal gaps");
        Check(!MapObjectArrange.TryPlan(things, float.NaN, MapObjectArrange.Axis.X,
              MapObjectArrange.Operation.Minimum, out _),
              "NaN map size is rejected");
        var invalid = new List<MapData.Thing>(things);
        invalid[1] = new MapData.Thing { Id = "bad", X = float.PositiveInfinity, Z = 3 };
        Check(!MapObjectArrange.TryPlan(invalid, 300f, MapObjectArrange.Axis.Z,
              MapObjectArrange.Operation.Minimum, out _),
              "infinite object coordinate cannot produce partial layout");
        invalid[1] = new MapData.Thing { Id = "outside", X = 151, Z = 3 };
        Check(!MapObjectArrange.TryPlan(invalid, 300f, MapObjectArrange.Axis.X,
              MapObjectArrange.Operation.Center, out _),
              "out-of-bounds source blocks atomic layout");
        var equallySpaced = new List<MapData.Thing>
        {
            new MapData.Thing { Id = "a", X = 0, Z = 0 },
            new MapData.Thing { Id = "b", X = 5, Z = 0 },
            new MapData.Thing { Id = "c", X = 10, Z = 0 },
        };
        Check(!MapObjectArrange.TryPlan(equallySpaced, 300f, MapObjectArrange.Axis.X,
              MapObjectArrange.Operation.Distribute, out _),
              "no-op distribution does not create an unnecessary Undo");

        var map = Map();
        map.Things.Add(new MapData.Thing { Id = "p2", Prop = "Rock", X = 9f, Z = 8f });
        map.Things.Add(new MapData.Thing { Id = "p3", Prop = "Bush", X = 30f, Z = 20f });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "interaction", Event = "OnInteractObject", TargetId = "p2",
            Action = "ShowMessage", Message = "Hello", Enabled = true
        });
        var editor = new MapEditor();
        Set(editor, "_map", map);
        Set(editor, "_name", map.Name);
        Invoke(editor, "SelectAllThings");
        Invoke(editor, "ArrangeSelectedThings", MapObjectArrange.Axis.X,
               MapObjectArrange.Operation.Distribute);
        Check(map.Things[0].X == 3f && map.Things[1].X == 16.5f &&
              map.Things[2].X == 30f,
              "real editor applies equal spacing in the map rather than only preview");
        Check(map.Triggers[0].TargetId == "p2" && map.Triggers[0].Message == "Hello" &&
              map.Crypts[0].X == 0f && map.Regions[0].X == 1f,
              "layout never changes gameplay anchors, triggering rules or text");
        var undo = (List<string>)typeof(MapEditor)
            .GetField("_undo", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(editor);
        Check(undo.Count == 1, "three-object arrangement is exactly one undo step");
        Invoke(editor, "Undo");
        var undone = (MapData)typeof(MapEditor)
            .GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(editor);
        Check(undone.Things[0].X == 3f && undone.Things[1].X == 9f &&
              undone.Things[2].X == 30f,
              "one Undo restores all previous object coordinates");
        Invoke(editor, "Redo");
        var redone = (MapData)typeof(MapEditor)
            .GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(editor);
        Check(redone.Things[1].X == 16.5f &&
              redone.Triggers[0].TargetId == "p2",
              "Redo restores arrangement with intact linked interaction references");
        Invoke(editor, "SelectAllThings");
        Invoke(editor, "ArrangeSelectedThings", MapObjectArrange.Axis.X,
               MapObjectArrange.Operation.Distribute);
        Check(redone.Things[1].X == 16.5f &&
              ((List<string>)typeof(MapEditor)
                .GetField("_undo", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(editor)).Count == 1,
              "no-op layout does not allocate a second undo entry");
    }

    static void SelectionAndGroupGridContracts()
    {
        var all = new List<MapData.Thing>
        {
            new MapData.Thing { Id = "a", Prop = "Pine", MaterialId = null, X = 3, Z = 4 },
            new MapData.Thing { Id = "b", Prop = "Pine", MaterialId = "Stone", X = 12, Z = 8 },
            new MapData.Thing { Id = "c", Prop = "Rock", MaterialId = "Stone", X = 21, Z = 15 },
            new MapData.Thing { Id = "d", Prop = "Bush", MaterialId = "", X = -11, Z = -14 },
            new MapData.Thing { Id = "e", Prop = "Pine", MaterialId = null, X = 0, Z = 0 },
            new MapData.Thing { Id = "", Prop = "Pine", MaterialId = null },
            null,
        };
        var pick = new HashSet<string> { "a" };
        Check(MapObjectSelection.Matching(all, pick, MapObjectSelection.Property.Model)
              .SetEquals(new[] { "a", "b", "e" }),
              "matching models includes all authored identical props, not anonymous entries");
        Check(MapObjectSelection.Matching(all, pick, MapObjectSelection.Property.Material)
              .SetEquals(new[] { "a", "d", "e" }),
              "null and empty original materials are one intentional matching group");
        Check(MapObjectSelection.Matching(all, new HashSet<string> { "a", "c" },
              MapObjectSelection.Property.Material).SetEquals(new[] { "a", "b", "c", "d", "e" }),
              "multi-seed matching expands across every selected material");
        Check(MapObjectSelection.Inverted(all, new HashSet<string> { "a", "c" })
              .SetEquals(new[] { "b", "d", "e" }),
              "inverse selection never includes anonymous or null props");
        Check(MapObjectSelection.Inverted(all, new HashSet<string> { "a", "b", "c", "d", "e" }).Count == 0,
              "inverting a full selection produces an empty selection");
        Check(MapObjectSelection.Inverted(all, new HashSet<string>()).Count == 5,
              "inverting an empty selection selects all valid map prop IDs");
        Check(MapObjectSelection.Matching(all, new HashSet<string> { "stale" },
              MapObjectSelection.Property.Model).Count == 0,
              "missing IDs cannot accidentally select a model family");
        Check(MapObjectSelection.Matching(null, pick, MapObjectSelection.Property.Material).Count == 0 &&
              MapObjectSelection.Inverted(null, pick).Count == 0,
              "selection helpers fail closed on absent map data");
        Check(all[0].X == 3 && all[1].MaterialId == "Stone",
              "selection plans leave authored map data unchanged");

        var map = Map();
        map.Things.Add(new MapData.Thing { Id = "p2", Prop = "Pine", MaterialId = "Stone",
                                           X = 12, Z = 8, Scale = 1 });
        map.Things.Add(new MapData.Thing { Id = "p3", Prop = "Rock", MaterialId = "Stone",
                                           X = 21, Z = 15, Scale = 1 });
        map.Things.Add(new MapData.Thing { Id = "p4", Prop = "Bush", MaterialId = null,
                                           X = -11, Z = -14, Scale = 1 });
        map.Triggers.Add(new MapData.Trigger {
            Id = "F", Event = "OnInteractObject", TargetId = "p3",
            Action = "ShowMessage", Message = "Keep", Enabled = true
        });
        var e = new MapEditor();
        Set(e, "_map", map);
        Set(e, "_name", map.Name);
        var selected = (HashSet<string>)typeof(MapEditor)
            .GetField("_selectedThingIds", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(e);
        var undo = (List<string>)typeof(MapEditor)
            .GetField("_undo", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(e);
        Invoke(e, "Select", new Vector3(3, 0, 4), false);
        Invoke(e, "SelectMatchingThings", MapObjectSelection.Property.Model);
        Check(selected.SetEquals(new[] { "p1", "p2" }),
              "editor expands single Pine selection to all Pine props");
        Invoke(e, "SelectMatchingThings", MapObjectSelection.Property.Material);
        Check(selected.SetEquals(new[] { "p1", "p2", "p3", "p4" }),
              "editor expands multiselection by original and imported materials");
        Check(undo.Count == 0 && map.Things[0].X == 3 && map.Triggers[0].TargetId == "p3",
              "selection changes neither Undo nor positions, anchors or F rules");
        Invoke(e, "InvertThingSelection");
        Check(selected.Count == 0, "editor can invert a full selection");
        Invoke(e, "InvertThingSelection");
        Check(selected.SetEquals(new[] { "p1", "p2", "p3", "p4" }),
              "editor inverts empty selection back to all map props");
        Invoke(e, "SetThingSelection", new HashSet<string> { "p1" });
        Invoke(e, "SelectMatchingThings", MapObjectSelection.Property.Material);
        Check(selected.SetEquals(new[] { "p1", "p4" }),
              "editor matches original materials without changing Stone objects");

        // Snap applies once to all selected Thing objects, with no JSON schema change.
        Invoke(e, "SelectAllThings");
        Set(e, "_snap", 5f);
        Invoke(e, "SnapSelectedThingsToGrid");
        Check(map.Things[0].X == 5 && map.Things[0].Z == 5 &&
              map.Things[1].X == 10 && map.Things[1].Z == 10 &&
              map.Things[2].X == 20 && map.Things[2].Z == 15 &&
              map.Things[3].X == -10 && map.Things[3].Z == -15,
              "group grid snap quantizes X/Z of all selected props");
        Check(undo.Count == 1 &&
              map.Triggers[0].TargetId == "p3" && map.Triggers[0].Message == "Keep" &&
              map.Crypts[0].X == 0 && map.Regions[0].X == 1,
              "one grid snap makes exactly one Undo without altering gameplay anchors");
        Invoke(e, "Undo");
        var prior = (MapData)typeof(MapEditor)
            .GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(e);
        Check(prior.Things[0].X == 3 && prior.Things[1].X == 12 &&
              prior.Things[3].Z == -14,
              "one Undo restores the complete unsnapped group");
        Invoke(e, "Redo");
        var redoMap = (MapData)typeof(MapEditor)
            .GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(e);
        Check(redoMap.Things[0].X == 5 && redoMap.Things[3].Z == -15 &&
              redoMap.Triggers[0].TargetId == "p3",
              "Redo restores grid positions and preserved interaction references");
        Invoke(e, "SelectAllThings");
        Invoke(e, "SnapSelectedThingsToGrid");
        Check(undo.Count == 1 && redoMap.Things[1].X == 10,
              "already-snapped group does not allocate an unnecessary Undo");

        var edge = Map();
        edge.Things[0].X = 149f;
        var edgeEditor = new MapEditor();
        Set(edgeEditor, "_map", edge);
        Set(edgeEditor, "_name", edge.Name);
        Set(edgeEditor, "_snap", 40f);
        Invoke(edgeEditor, "SelectAllThings");
        Invoke(edgeEditor, "SnapSelectedThingsToGrid");
        var edgeUndo = (List<string>)typeof(MapEditor)
            .GetField("_undo", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(edgeEditor);
        Check(edge.Things[0].X == 149f && edge.Things[0].Z == 4f && edgeUndo.Count == 0,
              "snap outside map bounds is rejected atomically without history pollution");
        Check(!MapObjectArrange.TrySnap(edge.Things, edge.Size, float.NaN, out _) &&
              !MapObjectArrange.TrySnap(edge.Things, edge.Size, 0f, out _),
              "non-finite and disabled grid spacing are rejected");
        var invalid = new List<MapData.Thing> {
            new MapData.Thing { Id = "a", X = 3, Z = 4 },
            new MapData.Thing { Id = "bad", X = float.PositiveInfinity, Z = 0 }
        };
        Check(!MapObjectArrange.TrySnap(invalid, 300f, 5f, out _) &&
              invalid[0].X == 3 && float.IsInfinity(invalid[1].X),
              "invalid selected prop cannot cause a partial grid snap");
    }

    static void ReusableTemplateContracts()
    {
        var map = Map();
        map.Things[0].Yaw = 35f;
        map.Things[0].Scale = 1.2f;
        map.Things.Add(new MapData.Thing
        {
            Id = "p2", Prop = "Rock", MaterialId = "Stone",
            X = 13f, Z = 14f, Yaw = 90f, Scale = 0.8f
        });
        map.Triggers.Add(new MapData.Trigger
        {
            Id = "rule1", Name = "Read sign", Event = "OnInteractObject",
            TargetId = "p2", Action = "ShowMessage", Condition = "Always",
            Message = "This is a test.", Once = false, Enabled = true
        });
        var ids = new HashSet<string> { "p1", "p2" };
        var name = "Camp_" + Guid.NewGuid().ToString("N");
        string path = null;
        try
        {
            Check(!MapObjectTemplate.ValidName("../outside") &&
                  !MapObjectTemplate.ValidName("one/two") &&
                  !MapObjectTemplate.ValidName("") &&
                  MapObjectTemplate.ValidName(name),
                  "template filenames are strictly bounded and cannot traverse folders");
            Check(MapObjectTemplate.TryCapture(map, ids, name, out var template),
                  "selected authored props capture into reusable layout");
            Check(template.Items.Count == 2 && template.Interactions.Count == 1 &&
                  template.Items[0].Dx == -5 && template.Items[1].Dx == 5 &&
                  template.Items[0].Dz == -5 && template.Items[1].Dz == 5,
                  "template captures relative positions and linked F rules");
            Check(template.Items[0].Prop == "Pine" && template.Items[0].Yaw == 35f &&
                  template.Items[1].MaterialId == "Stone" && template.Items[1].Scale == 0.8f,
                  "template retains prefabs, materials, rotations and scales");
            Check(template.Interactions[0].TargetRef == template.Items[1].Ref &&
                  template.Interactions[0].Message == "This is a test." &&
                  !template.Interactions[0].Once,
                  "template refers to its own local target, without original map GUID");
            Check(MapObjectTemplate.TrySaveNew(template, out path, out _) &&
                  File.Exists(path),
                  "template is created as separate disk JSON without writing authored map");
            string originalBytes = File.ReadAllText(path);
            Check(!MapObjectTemplate.TrySaveNew(template, out _, out _) &&
                  File.ReadAllText(path) == originalBytes,
                  "existing template cannot be overwritten by repeated saves");
            bool loadedOk = MapObjectTemplate.TryLoad(name, out var loaded, out _);
            Check(MapObjectTemplate.ListNames().Contains(name) && loadedOk &&
                  loaded.Items.Count == 2 && loaded.Interactions.Count == 1,
                  "template is discoverable and roundtrips via separate library");
            Check(!MapObjectTemplate.TryLoad("../outside", out _, out _),
                  "loader rejects path traversal even when called directly");

            Check(MapObjectTemplate.TryPlanPaste(loaded, map, 30f, 40f,
                  out var props, out var rules),
                  "valid template plans a paste in open map bounds");
            Check(props.Count == 2 && rules.Count == 1 &&
                  props[0].X == 25f && props[0].Z == 35f &&
                  props[1].X == 35f && props[1].Z == 45f,
                  "pasted layout preserves shape at clicked anchor");
            Check(MapObjectTemplate.TryProject(loaded, map.Size, 30f, 40f, 90f,
                  out var footprint) && footprint.Count == 2 &&
                  Math.Abs(footprint[0].x - 25f) < 0.001f &&
                  Math.Abs(footprint[0].y - 45f) < 0.001f &&
                  Math.Abs(footprint[1].x - 35f) < 0.001f &&
                  Math.Abs(footprint[1].y - 35f) < 0.001f,
                  "positive Y quarter-turn rotates template footprints around the center");
            Check(MapObjectTemplate.TryPlanPaste(loaded, map, 30f, 40f, 90f,
                  out var rotatedProps, out var rotatedRules) &&
                  Math.Abs(rotatedProps[0].X - footprint[0].x) < 0.001f &&
                  Math.Abs(rotatedProps[0].Z - footprint[0].y) < 0.001f &&
                  Math.Abs(rotatedProps[0].Yaw - 125f) < 0.001f &&
                  Math.Abs(rotatedProps[1].Yaw - 180f) < 0.001f &&
                  rotatedRules[0].TargetId == rotatedProps[1].Id,
                  "rotated paste agrees with ghost footprint and preserves F links");
            Check(MapObjectTemplate.TryPlanPaste(loaded, map, 30f, 40f, -90f,
                  out var counterclockwise, out _) &&
                  Math.Abs(counterclockwise[0].X - 35f) < 0.001f &&
                  Math.Abs(counterclockwise[0].Z - 35f) < 0.001f &&
                  Math.Abs(counterclockwise[0].Yaw - 305f) < 0.001f,
                  "negative rotation counter-rotates layout and normalises object yaw");
            Check(MapObjectTemplate.TryProject(loaded, map.Size, 149f, 149f, 90f,
                  out var invalidFootprint) == false &&
                  invalidFootprint != null && invalidFootprint.Count == 2 &&
                  !MapObjectTemplate.TryPlanPaste(loaded, map, 149f, 149f, 90f,
                  out _, out _),
                  "out-of-bounds rotated preview stays visible but paste fails atomically");
            Check(!MapObjectTemplate.TryProject(loaded, map.Size, 0f, 0f,
                  float.NaN, out var noFootprint) && noFootprint == null &&
                  !MapObjectTemplate.TryPlanPaste(loaded, map, 0f, 0f,
                  float.PositiveInfinity, out _, out _),
                  "non-finite template rotation is rejected before preview or paste");
            Check(props[0].Id != "p1" && props[1].Id != "p2" &&
                  props[0].Id != props[1].Id &&
                  rules[0].Id != "rule1" && rules[0].TargetId == props[1].Id &&
                  rules[0].RegionId == "",
                  "pasted props and F events use fresh remapped IDs without stale region links");
            Check(map.Things.Count == 2 && map.Triggers.Count == 1 &&
                  map.Things[0].X == 3f,
                  "failed or pending template plan does not mutate source map");
            Check(!MapObjectTemplate.TryPlanPaste(loaded, map, 149f, 149f,
                  out _, out _) && map.Things.Count == 2,
                  "entire out-of-bounds template paste is rejected without partial objects");
            var overfull = Map();
            for (int i = 0; i < 128; i++)
                overfull.Triggers.Add(new MapData.Trigger { Id = "limit" + i });
            Check(!MapObjectTemplate.TryPlanPaste(loaded, overfull, 0f, 0f,
                  out _, out _) && overfull.Triggers.Count == 128,
                  "template cannot exceed total 128-map-trigger budget");

            var corrupted = new MapObjectTemplate.Layout { Name = name };
            corrupted.Items.Add(new MapObjectTemplate.Entry
                { Ref = "a", Prop = "../escape", Scale = 1f });
            Check(!MapObjectTemplate.Valid(corrupted),
                  "template cannot contain arbitrary script or traversal asset names");
            corrupted.Items[0].Prop = "Pine";
            corrupted.Items[0].Dx = float.NaN;
            Check(!MapObjectTemplate.Valid(corrupted),
                  "non-finite relative geometry fails closed");
            corrupted.Items[0].Dx = 0f;
            corrupted.Interactions.Add(new MapObjectTemplate.Interaction
                { TargetRef = "unknown", Message = "No target" });
            Check(!MapObjectTemplate.Valid(corrupted),
                  "a dangling interaction target is invalid before any map edit");

            // Existing Unity API doubles must know about visible resources. The
            // production editor uses the same MapLoader/MapAssets lookups.
            // A visible user prefab may contain gameplay scripts and physics.
            // The cursor preview must copy only its mesh hierarchy and original
            // material references, never its behaviours or colliders.
            var sourcePine = new GameObject("Authored pine prefab");
            var pineMesh = new Mesh();
            var pineMat = new Material(Shader.Find("Lit")) { name = "Pine bark" };
            sourcePine.AddComponent<MeshFilter>().sharedMesh = pineMesh;
            sourcePine.AddComponent<MeshRenderer>().sharedMaterials = new[] { pineMat };
            sourcePine.AddComponent<Collider>();
            sourcePine.AddComponent<MonoBehaviour>();
            var pineChild = new GameObject("Pine foliage");
            pineChild.transform.SetParent(sourcePine.transform, false);
            pineChild.transform.localPosition = new Vector3(0f, 3f, 0f);
            pineChild.transform.localScale = new Vector3(2f, 2f, 2f);
            var leavesMesh = new Mesh();
            pineChild.AddComponent<MeshFilter>().sharedMesh = leavesMesh;
            pineChild.AddComponent<MeshRenderer>().sharedMaterials = new[] { pineMat };
            var pureGhost = MapTemplateGhost.Create(sourcePine, null, 48,
                out int pureNodes);
            Check(pureGhost != null && pureNodes == 2 &&
                  pureGhost.TryGetComponent<MeshFilter>(out var clonedMesh) &&
                  clonedMesh.sharedMesh == pineMesh &&
                  pureGhost.TryGetComponent<MeshRenderer>(out var clonedRenderer) &&
                  clonedRenderer.sharedMaterials[0] == pineMat &&
                  pureGhost.transform.childCount == 1,
                  "mesh ghost retains mesh and material references with nested geometry");
            var ghostChild = pureGhost.transform.GetChild(0);
            Check(ghostChild.localPosition.y == 3f &&
                  ghostChild.localScale.x == 2f &&
                  ghostChild.gameObject.TryGetComponent<MeshFilter>(out var clonedLeaf) &&
                  clonedLeaf.sharedMesh == leavesMesh,
                  "mesh ghost preserves child-local position, scale and meshes");
            Check(!pureGhost.TryGetComponent<Collider>(out _) &&
                  !pureGhost.TryGetComponent<MonoBehaviour>(out _) &&
                  !ghostChild.gameObject.TryGetComponent<Collider>(out _),
                  "template ghost cannot run a source prefab script or physics collider");
            var customMaterial = new Material(Shader.Find("Lit")) { name = "User override" };
            var overrideGhost = MapTemplateGhost.Create(sourcePine,
                customMaterial, 48, out _);
            Check(overrideGhost != null &&
                  overrideGhost.TryGetComponent<MeshRenderer>(out var overriddenRenderer) &&
                  overriddenRenderer.sharedMaterials[0] == customMaterial &&
                  sourcePine.GetComponent<MeshRenderer>().sharedMaterials[0] == pineMat,
                  "custom material preview never mutates original prefab materials");
            Check(MapTemplateGhost.Create(sourcePine, null, 1, out _) == null &&
                  MapTemplateGhost.Create(new GameObject("Invisible model"), null,
                  48, out _) == null,
                  "over-budget and meshless prefabs fall back to safe position discs");
            Resources.Register("MapProps/Pine", sourcePine);
            Resources.Register("MapProps/Rock", new GameObject("Rock"));
            Resources.Register("MapUserMaterials/Stone",
                new Material(Shader.Find("Lit")) { name = "Stone" });
            var editor = new MapEditor();
            Set(editor, "_map", map);
            Set(editor, "_name", map.Name);
            var selectionField = typeof(MapEditor).GetField("_selectedThingIds",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var selection = (HashSet<string>)selectionField.GetValue(editor);
            selection.Add("p1"); selection.Add("p2");
            Set(editor, "_templateDraftName", name);
            // A duplicate must fail, never mutate authoring map or Undo.
            Invoke(editor, "SaveSelectedTemplate");
            var undoField = typeof(MapEditor).GetField("_undo",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(((List<string>)undoField.GetValue(editor)).Count == 0 &&
                  map.Things.Count == 2 && File.ReadAllText(path) == originalBytes,
                  "editor template save does not overwrite existing file or mark map dirty");
            Invoke(editor, "ChooseTemplate", name);
            var meshGhostField = typeof(MapEditor).GetField("_templateGhostMeshes",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var previewMeshes = (List<GameObject>)meshGhostField.GetValue(editor);
            Check(previewMeshes.Count == 2 && previewMeshes[0] != null &&
                  previewMeshes[1] == null &&
                  !previewMeshes[0].activeSelf,
                  "editor caches safe 3D mesh where available and retains disc fallback");
            Invoke(editor, "PasteActiveTemplate", new Vector3(60f, 0f, 65f));
            Check(map.Things.Count == 4 && map.Triggers.Count == 2 &&
                  map.Things[2].X == 55f && map.Things[3].X == 65f &&
                  map.Triggers[1].TargetId == map.Things[3].Id,
                  "editor click-to-place inserts two authored props with linked interaction");
            Check(((List<string>)undoField.GetValue(editor)).Count == 1 &&
                  selection.Contains(map.Things[2].Id) &&
                  selection.Contains(map.Things[3].Id),
                  "one template placement is one Undo and selects only inserted group");
            Invoke(editor, "Undo");
            var undone = (MapData)typeof(MapEditor).GetField("_map",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            Check(undone.Things.Count == 2 && undone.Triggers.Count == 1,
                  "Undo fully removes inserted props AND copied F events");
            Invoke(editor, "Redo");
            var redone = (MapData)typeof(MapEditor).GetField("_map",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            Check(redone.Things.Count == 4 && redone.Triggers.Count == 2 &&
                  redone.Triggers[1].TargetId == redone.Things[3].Id,
                  "Redo restores pasted composition and exact new reference mapping");
            // Moving the ghost must not dirty authored map or push Undo history.
            Set(editor, "_yaw", 90f);
            Invoke(editor, "ShowTemplatePreview", new Vector3(60f, 0f, 65f));
            var ghostField = typeof(MapEditor).GetField("_templatePreviewMarkers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ghosts = (List<GameObject>)ghostField.GetValue(editor);
            Check(ghosts.Count == 2 && ghosts[0].activeSelf && ghosts[1].activeSelf &&
                  Math.Abs(ghosts[0].transform.position.x - 55f) < 0.001f &&
                  Math.Abs(ghosts[0].transform.position.z - 70f) < 0.001f &&
                  Math.Abs(ghosts[1].transform.position.x - 65f) < 0.001f &&
                  Math.Abs(ghosts[1].transform.position.z - 60f) < 0.001f,
                  "editor previews every prop at the same rotated coordinates as placement");
            Check(previewMeshes[0].activeSelf &&
                  Math.Abs(previewMeshes[0].transform.position.x - 55f) < 0.001f &&
                  Math.Abs(previewMeshes[0].transform.position.z - 70f) < 0.001f,
                  "visible template mesh ghost follows same footprint as marker");
            Set(editor, "_templateGhostEnabled", false);
            Invoke(editor, "ShowTemplatePreview", new Vector3(60f, 0f, 65f));
            Check(!previewMeshes[0].activeSelf && ghosts[0].activeSelf,
                  "3D preview toggle disables only mesh ghosts, not footprint safety markers");
            Set(editor, "_templateGhostEnabled", true);
            Invoke(editor, "ShowTemplatePreview", new Vector3(60f, 0f, 65f));
            Check(previewMeshes[0].activeSelf,
                  "3D preview can be re-enabled without recreating source prefabs");
            Check(redone.Things.Count == 4 && redone.Triggers.Count == 2 &&
                  ((List<string>)undoField.GetValue(editor)).Count == 1,
                  "moving template ghost never mutates map or Undo history");
            Invoke(editor, "PasteActiveTemplate", new Vector3(80f, 0f, 85f));
            Check(redone.Things.Count == 6 && redone.Triggers.Count == 3 &&
                  Math.Abs(redone.Things[4].X - 75f) < 0.001f &&
                  Math.Abs(redone.Things[4].Z - 90f) < 0.001f &&
                  Math.Abs(redone.Things[4].Yaw - 125f) < 0.001f &&
                  redone.Triggers[2].TargetId == redone.Things[5].Id &&
                  ((List<string>)undoField.GetValue(editor)).Count == 2,
                  "actual editor placement rotates full template as single Undo transaction");
            Invoke(editor, "Undo");
            var unrotated = (MapData)typeof(MapEditor).GetField("_map",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            Check(unrotated.Things.Count == 4 && unrotated.Triggers.Count == 2,
                  "Undo deletes rotated props AND their duplicated interaction");
            Invoke(editor, "Redo");

            // Simulate external corruption and ensure the loader refuses it.
            File.WriteAllText(path, "{bad-json");
            Check(!MapObjectTemplate.TryLoad(name, out _, out _),
                  "corrupted template files are rejected without crashing editor");
        }
        finally
        {
            if (path != null && File.Exists(path)) File.Delete(path);
        }
    }

    static void Main()
    {
        LegacyJsonAndAtomicWrite();
        RulesRuntime();
        BooleanTriggerBranching();
        ObjectInteractionWizard();
        EditorContracts();
        TerrainRoundtripAndCollider();
        MaterialsAndVegetation();
        DragAndFlattenTools();
        AuthoringImportContracts();
        ExternalEditConflictAndSafeCopy();
        FastBrushPreviewAndSelectionHeight();
        PaintEraserAndMaterialSlots();
        UnsavedMapExitGuard();
        GroupEditingContracts();
        RectangleSelectionContracts();
        MissingPreviewAssetsAreVisible();
        AuthoringCameraFallback();
        WorldForgeBestImprovementsContracts();
        LayoutPlannerContracts();
        SelectionAndGroupGridContracts();
        ReusableTemplateContracts();
        Console.WriteLine("PASS: " + passed + " real C# compiled map-editor assertions (Unity API shim, NOT Unity PlayMode).");
    }
}
