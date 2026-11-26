using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

internal static class EmotionSaveChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        var warrior = new Warrior { Id = "squad" };
        var survivor = new Warrior { Id = "survivor" };
        var witness = new Warrior { Id = "witness" };
        var owners = new HashSet<string> { warrior.Id, survivor.Id };
        var saved = new List<SavedEmotion>
        {
            Row(warrior, EmotionType.Fear, 0.6f),
            Row(warrior, EmotionType.Anger, 0.6f),
            Row(survivor, EmotionType.Sadness, 0.3f),
            Row(witness, EmotionType.Fear, 0.8f),
        };
        var options = new JsonSerializerOptions { IncludeFields = true };
        var fromFile = JsonSerializer.Deserialize<List<SavedEmotion>>(JsonSerializer.Serialize(saved, options), options);

        EmotionSystem.ForgetAll();
        EmotionSystem.Restore(new[] { Row(witness, EmotionType.Fear, 1f) },
            new HashSet<string> { witness.Id });
        EmotionSystem.Restore(fromFile, owners);
        fromFile[0] = Row(warrior, EmotionType.Anger, 1f);
        check(EmotionSystem.Snapshot(new HashSet<string> { witness.Id }).Count == 0,
            "a second load before Awake removes owners queued by the previous file");
        check(EmotionSystem.Snapshot(owners).Count == 3 &&
              EmotionSystem.Snapshot(owners).First().Type == EmotionType.Fear,
            "cold load retains a detached snapshot while the emotion manager is absent");

        var system = new EmotionSystem();
        Invoke(system, "Awake");
        check(system.GetDominantEmotion(warrior) == EmotionType.Fear &&
              system.GetEmotionWeight(warrior, "Fear") == 1.5f &&
              system.GetDominantEmotion(survivor) == EmotionType.Sadness &&
              system.GetDominantEmotion(witness) == EmotionType.Calm,
            "cold load restores saved owners, stable tie order and the real vote modifier");

        var checkpoint = EmotionSystem.Snapshot(owners);
        system.TriggerEmotion(warrior, EmotionType.Anger, 0.4f);
        system.TriggerEmotion(witness, EmotionType.Joy, 0.7f);
        check(system.GetDominantEmotion(warrior) == EmotionType.Anger, "future anger overtakes saved fear");

        EmotionSystem.Restore(checkpoint, owners);
        check(system.GetDominantEmotion(warrior) == EmotionType.Fear &&
              system.GetDominantEmotion(witness) == EmotionType.Joy && system.RepeatCount == 1,
            "loading rolls back owned emotions while preserving other NPCs and their decay schedule");

        Invoke(system, "DecayAllEmotions");
        var decayed = EmotionSystem.Snapshot(owners).Single(row => row.Owner == warrior.Id && row.Type == EmotionType.Fear);
        check(Math.Abs(decayed.Intensity - 0.5f) < 0.001f, "restored intensity decays by the normal game step");

        EmotionSystem.Restore(null, owners);
        check(system.GetDominantEmotion(warrior) == EmotionType.Calm &&
              system.GetDominantEmotion(survivor) == EmotionType.Calm &&
              system.GetDominantEmotion(witness) == EmotionType.Joy && system.RepeatCount == 1,
            "legacy saves clear future emotions only for the replaced participants");

        EmotionSystem.Restore(new[]
        {
            Row(warrior, EmotionType.Fear, 0.1f),
            Row(warrior, EmotionType.Fear, 1f),
            Row(warrior, EmotionType.Anger, float.PositiveInfinity),
            Row(warrior, (EmotionType)100, 1f),
        }, owners);
        var sanitized = EmotionSystem.Snapshot(owners);
        check(sanitized.Count == 1 && sanitized[0].Intensity == 0.1f,
            "duplicate and malformed emotion rows cannot create stronger events");

        EmotionSystem.ForgetAll();
        check(system.GetDominantEmotion(warrior) == EmotionType.Calm &&
              system.GetDominantEmotion(witness) == EmotionType.Calm, "a new game discards every live emotion");
        Invoke(system, "OnDestroy");

        string saveSource = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Core/SaveSystem.cs"));
        int pending = saveSource.IndexOf("var arrival = _saveOnArrival;", StringComparison.Ordinal);
        int restoreOnArrival = saveSource.IndexOf("RestoreEmotions(restored, arrival.Value.EmotionOwners);", StringComparison.Ordinal);
        int checkpointOnArrival = saveSource.IndexOf("MarkCheckpoint(scene.name, scene", StringComparison.Ordinal);
        check(saveSource.Contains("save.AosEmotions = Sinbinder.AOS.EmotionSystem.Snapshot(owners);") &&
              saveSource.Contains("if (keepScene) RestoreEmotions(save, previousEmotionOwners, emotionOwnerIds);") &&
              pending >= 0 && restoreOnArrival > pending && checkpointOnArrival > restoreOnArrival,
            "save paths capture owner emotions and restore arrival data before taking the checkpoint");
    }

    private static SavedEmotion Row(Warrior warrior, EmotionType type, float intensity)
        => new SavedEmotion { Owner = warrior.Id, Type = type, Intensity = intensity };

    private static void Invoke(EmotionSystem system, string method)
        => typeof(EmotionSystem).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(system, null);
}
