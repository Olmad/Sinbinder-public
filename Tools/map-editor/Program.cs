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
    static void Main()
    {
        LegacyJsonAndAtomicWrite();
        RulesRuntime();
        BooleanTriggerBranching();
        ObjectInteractionWizard();
        EditorContracts();
        Console.WriteLine("PASS: " + passed + " real C# compiled map-editor assertions (Unity API shim, NOT Unity PlayMode).");
    }
}
