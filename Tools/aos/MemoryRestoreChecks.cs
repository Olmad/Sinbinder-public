using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using UnityEngine;

internal static class MemoryRestoreChecks
{
    private static SavedMemory Row(string owner, string target, string type = "AllySavedMe")
        => new SavedMemory { Owner = owner, TargetID = target, EventType = type,
            Emotion = (int)EmotionType.Joy, Importance = 0.8f, Age = 5f, DecayRate = 0.01f };
    private static void Invoke(MemoryProcessor system, string method)
        => typeof(MemoryProcessor).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(system, null);

    internal static void Run(Action<bool, string> check, string root)
    {
        var allowed = new HashSet<string> { "incoming" };
        var replace = new HashSet<string> { "incoming", "outgoing" };
        MemoryProcessor.ForgetAll();
        MemoryProcessor.Restore(new List<SavedMemory> { Row("outgoing", "old") });
        var incoming = Row("incoming", "known");
        incoming.TargetPersonId = "person-known";
        MemoryProcessor.Restore(new[] { incoming, Row("foreign", "intruder") }, allowed, replace, true);
        incoming.TargetID = "mutated";
        var pending = MemoryProcessor.Snapshot(null);
        check(pending.Count == 1 && pending[0].Owner == "incoming" &&
              pending[0].TargetID == "known" && pending[0].TargetPersonId == "person-known",
            "second cold load drops outgoing and foreign records and detaches payload");
        pending[0].TargetID = "mutated snapshot";
        check(MemoryProcessor.Snapshot(null)[0].TargetID == "known",
            "reading cold snapshot does not mutate pending load");

        float previous = Time.time;
        Time.time = 100f;
        var system = new MemoryProcessor();
        Invoke(system, "Awake");
        try
        {
            check(system.GetMemoriesById("outgoing").Count == 0 &&
                  system.GetMemoriesById("foreign").Count == 0 &&
                  system.GetMemoriesById("incoming").Single().CreationTime == 95f,
                "actual memory manager installs only selected cold state");
            system.CreateMemoryById("outgoing", "Old", "", EmotionType.Joy, 0.9f);
            system.CreateMemoryById("incoming", "Future", "", EmotionType.Anger, 0.9f);
            system.CreateMemoryById("npc", "Witness", "", EmotionType.Joy, 0.9f);
            MemoryProcessor.Restore(new[] { Row("incoming", "saved") }, allowed, replace, false);
            check(system.GetMemoriesById("outgoing").Count == 0 &&
                  system.GetMemoriesById("incoming").Single().TargetID == "saved" &&
                  system.GetMemoriesById("npc").Count == 1,
                "in-place restore drops outgoing and future participant events but retains unrelated current NPC");
            MemoryProcessor.Restore(Array.Empty<SavedMemory>(), allowed, replace, false);
            check(system.GetMemoriesById("incoming").Count == 0 && system.GetMemoriesById("npc").Count == 1,
                "empty participant snapshot does not revive old memories");
            var bad = Row("incoming", "bad");
            bad.Age = float.NaN;
            MemoryProcessor.Restore(new[] { bad, Row("foreign", "bad"), Row("incoming", "fresh") }, allowed, replace, true);
            check(MemoryProcessor.Snapshot(null).Count == 1 &&
                  system.GetMemoriesById("incoming").Single().TargetID == "fresh" &&
                  system.GetMemoriesById("npc").Count == 0,
                "scene reload drops all old ledgers and ignores malformed or foreign records");

            MemoryProcessor.RegisterBodyIdentity("reused", "old-person");
            MemoryProcessor.ResetBodyIdentities();
            MemoryProcessor.RegisterBodyIdentity("reused", "new-person");
            system.CreateMemoryById("incoming", "NewEvent", "reused", EmotionType.Joy, 0.8f);
            check(MemoryProcessor.Snapshot(allowed).Single(m => m.EventType == "NewEvent").TargetPersonId == "new-person",
                "new scene does not inherit prior body identity mappings");
            MemoryProcessor.ForgetAll();
            check(MemoryProcessor.Snapshot(null).Count == 0, "new game discards every accumulated memory");
        }
        finally
        {
            MemoryProcessor.ForgetAll();
            Invoke(system, "OnDestroy");
            Time.time = previous;
        }

        string save = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Core/SaveSystem.cs"));
        int arrival = save.IndexOf("RestoreMemories(restored, null, null, resetAll: true);", StringComparison.Ordinal);
        int checkpoint = save.IndexOf("MarkCheckpoint(scene.name, scene", StringComparison.Ordinal);
        check(arrival >= 0 && checkpoint > arrival &&
              save.Contains("RestoreMemories(save, previousEmotionOwners, emotionOwnerIds, resetAll: !keepScene);") &&
              save.Contains("replace.UnionWith(previousOwners);") &&
              save.Contains("!owners.Contains(owner)"),
            "save wiring filters incoming owners, replaces outgoing and incoming ledgers and repeats reset on arrival");
        check(save.Contains("save.HasAosEmotions = true;") &&
              save.Contains("if (save.HasAosEmotions || save.AosEmotions?.Count > 0)") &&
              save.Contains("Sinbinder.AOS.EmotionSystem.ForgetAll();"),
            "modern empty emotion snapshot wins over legacy and scene reset discards old emotions (source)");
    }
}
