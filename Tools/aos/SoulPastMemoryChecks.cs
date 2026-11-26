using System;
using System.IO;
using System.Collections.Generic;
using Sinbinder.Core;
using Sinbinder.AOS;
using UnityEngine;

internal static class SoulPastMemoryChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        var source = new SoulData("Warrior", SinType.Wrath, MoralType.Neutral, 1);
        var snapshot = SoulData.CaptureDeath(source, "");
        var anger = new SavedMemory { Owner = "old-body", EventType = "AllyBetrayedMe",
            TargetID = "enemy-body", TargetPersonId = "enemy-person", Emotion = (int)EmotionType.Anger, Importance = 0.8f,
            DecayRate = 0.01f, Age = 10f };
        var self = new SavedMemory { Owner = "old-body", EventType = "SelfEvent",
            TargetID = "old-body", Emotion = (int)EmotionType.Joy, Importance = 0.6f,
            DecayRate = 0.02f, Age = 3f };
        var invalid = new SavedMemory { Owner = "old-body", EventType = "Bad",
            Emotion = (int)EmotionType.Anger, Importance = float.NaN, DecayRate = 0.01f };
        snapshot.SetPastMemories(new[] { anger, self, invalid }, 100f);
        anger.TargetID = "wrong";
        anger.Importance = 0f;
        check(snapshot.PastMemoriesAt(105f).Count == 2 &&
              snapshot.PastMemoriesAt(105f)[0].TargetID == "enemy-body" &&
              snapshot.PastMemoriesAt(105f)[0].Importance == 0.8f &&
              snapshot.PastMemoriesAt(105f)[0].Age == 15f,
            "death snapshot copies valid experienced events and advances age");
        float previousTime = Time.time;
        try
        {
            Time.time = 110f;
            var shock = SoulDecay.Harvest(snapshot, SoulQuality.Shock);
            var accepted = SoulDecay.Harvest(snapshot, SoulQuality.Acceptance);
            var fading = SoulDecay.Harvest(snapshot, SoulQuality.Fading);
            var dissolved = SoulDecay.Harvest(snapshot, SoulQuality.Dissolved);
            check(shock.PastMemoriesAt(110f).Count == 2 && accepted.PastMemoriesAt(110f).Count == 2,
                "early harvest carries experienced events even without innate biography");
            check(fading.PastMemoriesAt(110f).Count == 0 && dissolved.PastMemoriesAt(110f).Count == 0,
                "late harvest cannot recover forgotten experienced events");
            var records = shock.PastMemoriesAt(130f, "new-body");
            check(records[0].Owner == "new-body" && records[0].TargetID == "enemy-body" &&
                  records[0].TargetPersonId == "enemy-person" &&
                  records[0].Age == 40f && records[0].Emotion == (int)EmotionType.Anger &&
                  records[1].TargetID == "new-body",
                "transfer remaps only owner and self target, keeps other targets and age");
            var live = records[0].ToLive(130f);
            check(live.CreationTime == 90f && live.Importance == 0.8f &&
                  live.GetCurrentStrength(130f) < 0f &&
                  Math.Abs(live.GetCurrentStrength(130f) - (0.8f * -1.3f / 1.4f)) < 0.0001f,
                "negative memory preserves original importance, age and signed relationship strength");
            var current = MemoryRecord.UniqueLivingBodies(new[]
            {
                new KeyValuePair<string, string>("enemy-person", "new-enemy-body"),
                new KeyValuePair<string, string>("other-person", "same-name-body")
            });
            var projected = live.ProjectTarget(current);
            check(projected.TargetID == "new-enemy-body" && projected.TargetPersonId == "enemy-person" &&
                  live.TargetID == "enemy-body" && projected.CreationTime == live.CreationTime &&
                  projected.GetCurrentStrength(130f) == live.GetCurrentStrength(130f),
                "recognize a new body without rewriting historical address or refreshing memory");
            check(live.MatchesTarget("new-enemy-body", "enemy-person") &&
                  !live.MatchesTarget("enemy-body", "other-person") &&
                  !live.MatchesTarget("same-name-body", "other-person"),
                "repeat event matches known person, never an unrelated identity");
            var legacy = new MemoryRecord("enemy-body", "Legacy", EmotionType.Anger, 0.8f);
            check(legacy.ProjectTarget(current).TargetID == "enemy-body" &&
                  !legacy.MatchesTarget("new-enemy-body", "enemy-person"),
                "legacy address without known identity is not guessed");
            var duplicate = MemoryRecord.UniqueLivingBodies(new[]
            {
                new KeyValuePair<string, string>("enemy-person", "body-a"),
                new KeyValuePair<string, string>("enemy-person", "body-b"),
                new KeyValuePair<string, string>("enemy-person", "body-a"),
            });
            check(live.ProjectTarget(duplicate).TargetID == "" && live.TargetID == "enemy-body",
                "ambiguous active projections cannot select an arbitrary body");
            var saved = shock.PastMemoriesAt(130f);
            var loaded = new SoulData("Warrior", SinType.Wrath, MoralType.Neutral, 1);
            loaded.SetPastMemories(saved, 0f);
            saved[0].TargetID = "mutated save";
            check(loaded.PastMemoriesAt(5f)[0].Age == 45f &&
                  loaded.PastMemoriesAt(5f)[0].TargetID == "enemy-body" &&
                  loaded.PastMemoriesAt(5f)[0].TargetPersonId == "enemy-person",
                "new session clock resumes saved age without aliasing records");
            records[0].Importance = 0f;
            shock.ClearPastMemories();
            check(shock.PastMemoriesAt(140f).Count == 0 &&
                  accepted.PastMemoriesAt(140f)[0].Importance == 0.8f &&
                  snapshot.PastMemoriesAt(140f)[0].Importance == 0.8f,
                "consume once; independent snapshots and harvested payloads");
        }
        finally { Time.time = previousTime; }

        string Read(string path) => File.ReadAllText(Path.Combine(root, path));
        string manager = Read("Assets/Scripts/Gameplay/SoulManager.cs");
        check(manager.Contains("AOS.MemoryProcessor.Snapshot(new[] { warrior.Id }), Time.time);"),
            "death captures only the deceased body's experienced events (source)");
        string save = Read("Assets/Scripts/Core/SaveSystem.cs");
        check(save.Contains("PastMemories = soul.PastMemoriesAt(Time.time),") &&
              save.Contains("if (SoulDecay.KeepsMemory((SoulQuality)s.Quality))") &&
              save.Contains("soul.SetPastMemories(s.PastMemories, Time.time);") &&
              save.Contains("m.IsValidForTransfer()"),
            "custody save and load retain validated age and enforce decay policy (source)");
        string raising = Read("Assets/Scripts/Gameplay/Raising.cs");
        check(raising.IndexOf("warrior.Initialize(", StringComparison.Ordinal) <
              raising.IndexOf("InstallRaisedMemories(warrior);", StringComparison.Ordinal) &&
              raising.IndexOf("InstallRaisedMemories(warrior);", StringComparison.Ordinal) <
              raising.IndexOf("setup.SetupWarrior(go);", StringComparison.Ordinal),
            "fresh body receives memories before AOS setup (source)");
        string processor = Read("Assets/Scripts/AOS Engine/MemoryProcessor.cs");
        check(processor.Contains("if (_memories.ContainsKey(warrior.Id))") &&
              processor.Contains("_memories.Add(warrior.Id, saved.Select(record => record.ToLive(Time.time)).ToList());") &&
              processor.Contains("warrior.Soul.ClearPastMemories();"),
            "install consumes payload without overwriting existing ledger (source)");
        check(processor.Contains("TargetPersonId = m.TargetPersonId,") &&
              processor.Contains("list.Add(s.ToLive(now));") &&
              Read("Assets/Scripts/AOS Engine/MemoryRecord.cs").Contains("TargetPersonId = TargetPersonId, Emotion = (EmotionType)Emotion,") &&
              processor.Contains("m.MatchesTarget(targetId, personId)") &&
              processor.Contains("return vivid.Select(m => m.ProjectTarget(current)).ToList();"),
            "actual ledger captures and restores stable target and projects it only for current decisions (source)");
        check(Read("Assets/Scripts/Gameplay/Warrior.cs").Contains(
                  "AOS.MemoryProcessor.RegisterBodyIdentity(_id, soul.PersonId);") &&
              processor.Contains("_bodyPeople.Clear();") &&
              processor.Contains("else if (known != personId) _bodyPeople[bodyId] = null;"),
            "identity registration covers pre-combat bodies, rejects conflicting mappings and resets on new game (source)");
    }
}
