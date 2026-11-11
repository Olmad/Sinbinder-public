using System;
using Sinbinder.Gameplay;

internal static class Program
{
    private static int _checks;

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    public static int Main()
    {
        // Two-plate crypt puzzle. A distant ally must never activate a seal.
        Check(DreamTutorialChecks.OnSeal(-1.8f, 25.8f, -1.8f, 25.8f), "warrior stands on seal center");
        Check(DreamTutorialChecks.OnSeal(-0.45f, 25.8f, -1.8f, 25.8f), "seal includes 1.35m edge");
        Check(!DreamTutorialChecks.OnSeal(-0.44f, 25.8f, -1.8f, 25.8f), "seal excludes beyond radius");
        Check(!DreamTutorialChecks.OnSeal(-5f, 17f, -1.8f, 25.8f), "distant warrior cannot fake seal occupancy");
        Check(!DreamTutorialChecks.OnSeal(1.8f, 25.8f, -1.8f, 25.8f), "player's separate seal is not warrior's");

        float held = 0f;
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, false, true, true, 4f) && held == 0f,
            "counterweight lock blocks both correct occupants");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, true, true, false, 0.8f),
            "single occupant cannot count as both");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, true, true, true, 0.9f),
            "partial hold not enough");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, true, false, true, 0.1f) && held == 0f,
            "stepping off resets hold");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, true, true, true, 0.7f),
            "hold must be continuous");
        Check(DreamTutorialChecks.AdvanceSeals(ref held, true, true, true, 0.5f),
            "both held for total 1.2 seconds opens puzzle");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, false, true, true, 100f),
            "relocking mechanism clears pre-unlock hold");

        // New AOS decision is required after the *specific* order.
        Check(DreamTutorialChecks.FreshOrderedDecision(true, 10f, 10f, true, true),
            "same-frame order is accepted after a new context");
        Check(!DreamTutorialChecks.FreshOrderedDecision(true, 9f, 10f, true, true),
            "old order rejected");
        Check(!DreamTutorialChecks.FreshOrderedDecision(false, 11f, 10f, true, true),
            "wrong target or action rejected");
        Check(!DreamTutorialChecks.FreshOrderedDecision(true, 11f, 10f, false, true),
            "old AOS context rejected");
        Check(!DreamTutorialChecks.FreshOrderedDecision(true, 11f, 10f, true, false),
            "idle vote without command rejected");

        // Production battle-end gate: one real victory per enemy wave,
        // never a second victory when an ally dies after the enemy is gone.
        var battle = new BattleEndGate();
        Check(!battle.TryComplete(false), "empty scene is not an earned victory");
        battle.EnemyRegistered();
        Check(!battle.TryComplete(true), "living enemy prevents victory");
        Check(battle.TryComplete(false), "last enemy death triggers exactly one victory");
        Check(!battle.TryComplete(false), "later allied death cannot replay victory");
        battle.EnemyRegistered();
        Check(!battle.TryComplete(true), "new enemy wave remains active");
        Check(battle.TryComplete(false), "new wave earns its own victory once");
        Check(!battle.TryComplete(false), "second wave cannot duplicate rewards");
        var combatSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/CombatManager.cs");
        Check(combatSource.Contains("_battleEndGate.EnemyRegistered()") &&
              combatSource.Contains("_battleEndGate.TryComplete(_enemyUnits.Exists("),
              "real CombatManager registers enemy waves and gates OnBattleEnd");

        // Source-linked visual causality: removing soul B must not delete A's light.
        // Runtime Play Mode with two simultaneous souls is still required.
        var soulManagerSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/SoulManager.cs");
        var soulIndicatorSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/SoulIndicator.cs");
        Check(soulIndicatorSource.Contains("public FadingSoul Soul => _soul;") &&
              soulManagerSource.Contains("ReferenceEquals(indicator.Soul, soul)"),
              "fading soul removal targets its own indicator identity");
        Check(!soulManagerSource.Contains("if (_indicators[i] != null && _indicators[i].gameObject != null)"),
              "soul removal no longer deletes an arbitrary surviving light");

        // Death callbacks must not create duplicate souls or invalid null souls.
        Check(soulManagerSource.Contains("!_deathRegistered.Add(warrior)") &&
              soulManagerSource.Contains("warrior == null || warrior.Soul == null"),
              "repeated or invalid death cannot register a second fading soul");
        Check(soulManagerSource.Contains("_deathRegistered.Clear()") &&
              soulManagerSource.Contains("OnSceneUnloaded("),
              "death identity gate resets when the physical battle scene unloads");

        // A rescue requires an actual positive hit from a real attacker.
        Check(RescueCreditGate.IsRealAttack(2.1f, true), "real attacker and positive hit qualify");
        Check(!RescueCreditGate.IsRealAttack(0f, true), "zero-damage VFX cannot earn rescue credit");
        Check(!RescueCreditGate.IsRealAttack(2.1f, false), "missing attacker cannot earn rescue credit");
        Check(!RescueCreditGate.IsRealAttack(-1f, true), "negative healing cannot earn rescue credit");
        Check(!RescueCreditGate.IsRealAttack(float.NaN, true), "invalid damage cannot earn rescue credit");
        Check(!RescueCreditGate.IsRealAttack(float.PositiveInfinity, true), "infinite damage cannot earn rescue credit");

        // AOS real protection and cooldown. No grant occurs in the approach path.
        var credit = new RescueCreditGate();
        Check(credit.TryCredit("guard-a:victim-a", 10f), "first verified interception earns credit");
        Check(!credit.TryCredit("guard-a:victim-a", 10.1f), "repeated blow cannot farm rescue credit");
        Check(!credit.TryCredit("guard-a:victim-a", 24.99f), "cooldown enforced to final fraction");
        Check(credit.TryCredit("guard-a:victim-b", 11f), "another victim is a different event");
        Check(credit.TryCredit("guard-b:victim-a", 11f), "another guard is a different event");
        Check(credit.TryCredit("guard-a:victim-a", 25f), "credit allowed at cooldown boundary");
        Check(!credit.TryCredit("guard-a:victim-a", 23f), "clock rollback cannot bypass cooldown");
        Check(!credit.TryCredit("", 30f), "empty incident key rejected");
        Check(!credit.TryCredit("guard-a:victim-a", float.NaN), "invalid time rejected");
        credit.Clear();
        Check(credit.TryCredit("guard-a:victim-a", 0f), "session reset clears earlier credits");

        // Explain an AOS refusal in a way a player can act on. These are
        // actual gameplay copy strings, not CI-only duplicates.
        Check(OrderAdvice.Next("Distance").Contains("ближе"), "distance refusal suggests moving closer");
        Check(OrderAdvice.Next("Debt").Contains("долг"), "unpaid wages suggest resolving the debt");
        Check(OrderAdvice.Next("Temptation").Contains("снаряжение"), "tempting item suggests changing equipment");
        Check(OrderAdvice.Next("AllyInDanger").Contains("товарища"), "ally in danger suggests rescue first");
        Check(OrderAdvice.Next("Fatigue").Contains("передышку"), "fatigue suggests a rest or replacement");
        Check(OrderAdvice.Next("Surrounded").Contains("окружения"), "encirclement suggests reducing danger");
        Check(OrderAdvice.Next("Murderer").Contains("убийц"), "revenge suggests removing remembered threat");
        Check(OrderAdvice.Next(null, "Fear").Contains("опасность"), "fear voice suggests safer circumstances");
        Check(OrderAdvice.Next(null, "Memory").Contains("прошлом"), "memory voice suggests changing reminder");
        Check(OrderAdvice.Next(null, "Morality").Contains("убеждений"), "conscience voice suggests another order");
        Check(OrderAdvice.Next(null, "Pride").Contains("задачу"), "pride voice suggests a different assignment");
        Check(!string.IsNullOrWhiteSpace(OrderAdvice.Next("Unknown", "Unknown")),
            "unrecognized refusal still gives a non-guaranteed alternative");
        Check(OrderAdvice.Short("Distance") == "Подойдите ближе",
            "distance has a readable one-line command-panel hint");
        Check(OrderAdvice.Short("Debt") == "Выплатите долг",
            "debt has a readable one-line command-panel hint");
        Check(OrderAdvice.Short("Temptation").Contains("снаряжение"),
            "temptation short hint exposes a real player lever");
        Check(OrderAdvice.Short(null, "Memory").Length <= 40,
            "memory hint fits the one-line lower strip");
        Check(OrderAdvice.Short("Unknown", "Unknown").Length <= 40,
            "fallback hint is short enough for the lower strip");

        // The save slots panel pauses gameplay, so errors must be visible inside
        // that panel, not exclusively in the obscured battle log (L0 only).
        var slotsUx = System.IO.File.ReadAllText("Assets/Scripts/UI/SaveSlotsPanel.cs");
        Check(slotsUx.Contains("private string _status;") &&
              slotsUx.Contains("_title.text = !string.IsNullOrEmpty(_status)") &&
              slotsUx.Contains("else Draw();"),
              "failed load keeps panel open and renders its feedback in the panel");
        Check(slotsUx.Contains("_status = Loc.T(\"Запись не прочиталась.") &&
              slotsUx.Contains("_status = loaded") &&
              slotsUx.Contains("Say(_status);"),
              "unreadable and rejected saves both explain failure in the paused panel");
        Check(slotsUx.Contains("_status = Save(name); Draw();") &&
              slotsUx.Contains("private static string Save(string slot)") &&
              slotsUx.Contains("_status = null;"),
              "save failures appear in panel and reopening clears stale feedback");

        // Save-load source contracts: malformed input must not partially mutate
        // the squad/shelf or escape JsonUtility's ArgumentException path.
        // This is L0 source verification, NOT a Unity deserialization test.
        var saveSource = System.IO.File.ReadAllText("Assets/Scripts/Core/SaveSystem.cs");
        int validateSave = saveSource.IndexOf("if (save.Squad == null || save.Shelf == null");
        int firstMutation = saveSource.IndexOf("Gameplay.CardTable.BeforeSaveOrLoad();", saveSource.IndexOf("public static bool Restore(SaveGame save)"));
        Check(validateSave >= 0 && firstMutation > validateSave &&
              saveSource.Contains("save.Squad.Exists(m => m == null)") &&
              saveSource.Contains("save.Shelf.Exists(s => s == null)"),
              "corrupt squad or soul shelf rejected before Restore changes live state");
        Check(saveSource.Contains("catch (System.ArgumentException e)") &&
              saveSource.Contains("catch (System.UnauthorizedAccessException e)"),
              "malformed JSON and denied access fail safely in SaveSystem.Read");
        Check(saveSource.Contains("foreach (var m in save.Squad)") &&
              saveSource.Contains("foreach (var s in save.Shelf)"),
              "valid squad and soul restoration paths remain present");

        // A missing destination scene must fail before Restore mutates live state.
        // Positive paths: same part without reload and valid raid-host scenes
        // still reach Restore. This is L0 source-linked, not Unity Play Mode.
        int returnToStart = saveSource.IndexOf("public static bool ReturnTo(SaveGame save, bool reload = false)");
        int restoreStart = saveSource.IndexOf("public static bool Restore(SaveGame save)", returnToStart);
        string returnToBody = returnToStart >= 0 && restoreStart > returnToStart
            ? saveSource.Substring(returnToStart, restoreStart - returnToStart) : "";
        int sceneGuard = returnToBody.IndexOf("!Application.CanStreamedLevelBeLoaded(RaidEvent.HostOf(save.Scene))");
        int restoreCall = returnToBody.IndexOf("if (!Restore(save)) return false;");
        Check(sceneGuard >= 0 && restoreCall > sceneGuard &&
              returnToBody.Substring(sceneGuard, restoreCall - sceneGuard).Contains("return false;"),
              "missing destination scene rejects save before live state restoration");
        Check(returnToBody.Contains("(reload || save.Scene != Here)") &&
              returnToBody.Contains("if (save.Scene == Here && !reload) return true;") &&
              returnToBody.Contains("string scene = RaidEvent.HostOf(save.Scene);") &&
              returnToBody.Contains("SceneManager.LoadScene(scene);"),
              "same-scene and valid hosted raid destination still follow original restore paths");
        Check(!returnToBody.Contains("состояние возвращено, место — нет") &&
              !returnToBody.Contains("return true;\n            }\n\n            Arriving = true;"),
              "missing scene cannot be reported as successful partial restoration");

        // Solo save durability: a failed write must not truncate the existing slot.
        // L0 source-linked checks; the actual Unity filesystem needs separate QA.
        int writeStart = saveSource.IndexOf("public static bool Write(SaveGame save, string path)");
        int readStart = saveSource.IndexOf("public static SaveGame Read(string path)", writeStart);
        string writeBody = writeStart >= 0 && readStart > writeStart
            ? saveSource.Substring(writeStart, readStart - writeStart) : "";
        int stage = writeBody.IndexOf("new FileStream(temp, FileMode.CreateNew");
        int flush = writeBody.IndexOf("stream.Flush(true)");
        int replace = writeBody.IndexOf("File.Replace(temp, fullPath, null)");
        int move = writeBody.IndexOf("File.Move(temp, fullPath)");
        Check(stage >= 0 && flush > stage && replace > flush && move > flush,
              "save JSON staged and flushed before replacing the existing slot");
        Check(writeBody.Contains("File.Exists(fullPath)") &&
              !writeBody.Contains("File.WriteAllText(path,") &&
              !writeBody.Contains("File.Delete(fullPath)"),
              "existing slot never truncated or deleted before safe replacement");
        Check(writeBody.Contains("finally") && writeBody.Contains("File.Delete(temp)") &&
              writeBody.Contains("catch (System.PlatformNotSupportedException e)"),
              "failed save cleans temporary file and rejects unsupported replacement");

        // Save slots UX: failed restore must not dismiss the only recovery UI.
        // Source-linked L0 contract; not a Unity button/play-mode test.
        var slotsSource = System.IO.File.ReadAllText("Assets/Scripts/UI/SaveSlotsPanel.cs");
        Check(slotsSource.Contains("if (Load(name)) Close(); else Draw();") &&
              !slotsSource.Contains("Load(name); Close();"),
              "failed slot load keeps the save selection panel available");
        Check(slotsSource.Contains("if (save == null)") &&
              slotsSource.Contains("Запись не прочиталась.") &&
              slotsSource.Contains("bool loaded = SaveSystem.ReturnTo(save);") &&
              slotsSource.Contains("return loaded;"),
              "corrupt read and restore rejection are distinct and only success closes");
        Check(slotsSource.Contains("if (!Commitment.CanLoad)") &&
              slotsSource.Contains("Commitment.WhyNoLoad") &&
              slotsSource.Contains("if (Load(name)) Close();"),
              "commitment mode still denies loading and valid loads retain close behavior");

        // An unreadable on-disk slot must not be presented as an empty one.
        // Missing files remain empty; valid saves retain their own labels.
        Check(slotsSource.Contains("bool unreadable = !has && SaveSystem.Exists(SaveSystem.PathOf(slot))") &&
              slotsSource.Contains("unreadable ? Loc.T(\"Запись не читается\") : Loc.T(\"Пусто\")"),
              "existing unreadable save is distinguished from a truly empty slot");
        Check(slotsSource.Contains("bool has = save != null;") &&
              slotsSource.Contains("has ? () => { if (Load(name)) Close(); else Draw(); } : (System.Action)null") &&
              slotsSource.Contains("string.IsNullOrEmpty(save.Label) ? Loc.T(\"Запись\") : save.Label"),
              "unreadable save cannot be loaded; valid labels and load action remain");

        // A damaged on-disk save may still be recoverable outside the game.
        // The normal save action must not silently replace it; genuinely empty
        // slots remain writable and valid slots retain their save behavior.
        Check(slotsSource.Contains("unreadable ? (System.Action)null : () => { _status = Save(name); Draw(); }"),
              "unreadable slot is not overwritten through the save panel");
        Check(slotsSource.Contains("bool unreadable = !has && SaveSystem.Exists(SaveSystem.PathOf(slot))") &&
              slotsSource.Contains("unreadable ? Loc.T(\\\"Запись не читается\\\") : Loc.T(\\\"Пусто\\\")"),
              "unreadable slot is identified separately from an empty slot");

        Console.WriteLine("PASS: " + _checks + " production tutorial rule assertions");
        return 0;
    }
}
