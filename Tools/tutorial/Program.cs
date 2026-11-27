using System;
using System.Linq;
using Sinbinder.Gameplay;
using Sinbinder.UI;
using Sinbinder.AOS;

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
        // Production OrderIntegrity code, compiled directly from Assets.
        // A matching autonomous choice must stay autonomous even when
        // the warrior previously accepted a Whisper bargain.
        Check(!OrderIntegrity.MustCompelByWhisper(true, true, false, true) &&
              !OrderIntegrity.MustCompelByWhisper(false, true, false, false) &&
              !OrderIntegrity.MustCompelByWhisper(true, false, false, false),
            "whisper cannot replace a matching natural vote, absent order or unpersuaded choice");
        Check(OrderIntegrity.MustCompelByWhisper(true, true, false, false) &&
              OrderIntegrity.MustCompelByWhisper(true, true, true, true),
            "whisper compels only a conflicting or hesitant decision under an active order");
        Check(OrderIntegrity.MayApplyWhisperOverride(false, true, true, true) &&
              !OrderIntegrity.MayApplyWhisperOverride(true, true, true, true) &&
              !OrderIntegrity.MayApplyWhisperOverride(false, false, true, true) &&
              !OrderIntegrity.MayApplyWhisperOverride(false, true, false, true) &&
              !OrderIntegrity.MayApplyWhisperOverride(false, true, true, false),
            "execution never forces a matching, stale, un-compelled or expired whisper action");

        var wrapperSource = System.IO.File.ReadAllText("Assets/Scripts/AOS Engine/AOSWarriorWrapper.cs");
        Check(wrapperSource.Contains("LastWhisperCompelled = OrderIntegrity.MustCompelByWhisper(") &&
              wrapperSource.Contains("context.SatisfiedBy(decision.Action)") &&
              wrapperSource.Contains("decision.Hesitated, context.SatisfiedBy(decision.Action)") &&
              wrapperSource.Contains("OrderIntegrity.MayApplyWhisperOverride(") &&
              wrapperSource.Contains("HasCurrentCommandDecision,") &&
              !wrapperSource.Contains("decision.Action != ActionType.ObeyCommand;") &&
              !wrapperSource.Contains("action != ActionType.ObeyCommand && _warrior != null && Gameplay.Whisper.Persuaded"),
            "both real AOS vote and action execution use the guarded Whisper integrity rules");

        // The first five minutes are measured from the actual new-game click.
        // This is source-linked instrumentation: Unity must still produce logs
        // before anybody may claim to have measured a player's experience.
        var dreamTrace = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/DreamTutorialDirector.cs");
        var startPanelTrace = System.IO.File.ReadAllText("Assets/Scripts/UI/StartPanel.cs");
        Check(startPanelTrace.Contains("Gameplay.DreamTutorialDirector.BeginFirstFiveTrace();") &&
              dreamTrace.Contains("Time.realtimeSinceStartup - _firstFiveStartedAt") &&
              dreamTrace.Contains("TraceFirstFive(\"five_minute_mark stage=\" + _stage)"),
            "new game click starts unscaled 5-minute clock, including pauses and load");
        Check(dreamTrace.Contains("TraceFirstFive(\"first_command_detected\")") &&
              dreamTrace.Contains("TraceFirstFive(\"first_verdict action=\"") &&
              dreamTrace.Contains("TraceFirstFive(\"court_requested\")") &&
              dreamTrace.Contains("TraceFirstFive(\"court_completed duration_s=\"") &&
              dreamTrace.Contains("TraceFirstFive(\"court_not_shown\")") &&
              dreamTrace.Contains("TraceFirstFive(\"second_verdict action=\""),
            "dream traces player action, real AOS verdict, court attempt and independent retry");
        // 20.11: obedience is the engine's own rule (DecisionContext.SatisfiedBy) — Attack on
        // "chase" IS the order; independent Flee/Attack that the order did not ask for is not.
        // Before, the dream said "did not chase even when ordered" while the court said "obeyed".
        var contextSource = System.IO.File.ReadAllText("Assets/Scripts/AOS Engine/DecisionContext.cs");
        Check(dreamTrace.Contains("!d.RefusedCommand && !d.Hesitated &&") &&
              dreamTrace.Contains("(d.Action == AOS.ActionType.ObeyCommand || (c != null && c.SatisfiedBy(d.Action)))") &&
              dreamTrace.Contains("bool obeyed = ActuallyObeyed(d, c);") &&
              dreamTrace.Contains("RefusalResult = d.RefusedCommand ?") &&
              dreamTrace.Contains("ActuallyObeyed(d, c) ? \"послушался\" : \"выбрал своё\"") &&
              !dreamTrace.Contains("ActuallyObeyed(d)") &&
              contextSource.Contains("case \"FallBack\": return action == ActionType.Flee;") &&
              !contextSource.Contains("case \"Move\""),
            "dream calls obedience only what the engine's order accepts, never a free Flee/Attack");

        // Exercise the exact pure rule used by both dream lesson paths.
        Check(DreamTutorialChecks.MatchesOrderedDecision(true, 42u, 42u),
            "dream accepts a recorded decision for its exact order even after completion");
        Check(!DreamTutorialChecks.MatchesOrderedDecision(true, 42u, 41u) &&
              !DreamTutorialChecks.MatchesOrderedDecision(true, 42u, 43u) &&
              !DreamTutorialChecks.MatchesOrderedDecision(false, 42u, 42u),
            "dream rejects previous orders, replacement orders and decisions without a command");
        Check(dreamTrace.Contains("mind.LastDecisionCommandRevision") &&
              dreamTrace.Contains("mind.LastContext != null && mind.LastContext.HasCommand") &&
              dreamTrace.Contains("if (HasLessonDecision(mind))") &&
              dreamTrace.Contains("bool fresh = HasLessonDecision(mind);") &&
              dreamTrace.Split(new[] { "_decisionOrderRevision = _zombie.CommandRevision;" },
                  StringSplitOptions.None).Length == 4 &&
              !dreamTrace.Contains("_mindBefore"),
            "both first court paths and the retest capture the actual command revision");
        Check(dreamTrace.Contains("!_decisionManualDecideTried") &&
              dreamTrace.Contains("_decisionManualDecideTried = true;") &&
              dreamTrace.Split(new[] { "_zombie.CommandRevision == _decisionOrderRevision" },
                  StringSplitOptions.None).Length == 3,
            "manual decision fallback runs once and never votes on a replacement order");

        Check(DreamTutorialChecks.MayOperateLever(false, true, false, false, true),
            "lever accepts a current effective obey decision");
        Check(!DreamTutorialChecks.MayOperateLever(true, true, false, false, true) &&
              !DreamTutorialChecks.MayOperateLever(false, false, false, false, true) &&
              !DreamTutorialChecks.MayOperateLever(false, true, true, false, true) &&
              !DreamTutorialChecks.MayOperateLever(false, true, false, true, true) &&
              !DreamTutorialChecks.MayOperateLever(false, true, false, false, false),
            "lever rejects duplicate, stale, refused, hesitant and non-obey decisions");
        Check(dreamTrace.Contains("_cmdMarkRevision = _zombie.CommandRevision;") &&
              dreamTrace.Contains("return _zombie.CommandRevision != _cmdMarkRevision;") &&
              dreamTrace.Contains("_stage != Stage.Bridge || LeverOrderedSinceMark()") &&
              dreamTrace.Contains("_stage != Stage.Retest || LeverOrderedSinceMark()") &&
              dreamTrace.Contains("if (!_retestOrdered && LeverOrderedSinceMark())") &&
              dreamTrace.Contains("_zombie.Command.Kind == CommandKind.Move") &&
              dreamTrace.Contains("DreamRoad.AtLever(_zombie.Command.Point, 3f)") &&
              dreamTrace.Contains("if (ChaseOrderedSinceMark())") &&
              dreamTrace.Contains("k.gameObject == target") &&
              !dreamTrace.Contains("_retestSince"),
            "lesson ring detects identical reissued commands and waits for the specified action");
        string roadMap = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/DreamRoadMap.cs");
        Check(roadMap.Contains("c.Kind != CommandKind.Move") &&
              roadMap.Contains("DreamTutorialChecks.MayOperateLever(alreadyServed, mind.HasCurrentCommandDecision,") &&
              roadMap.Contains("_leverServed[w] = revision;") &&
              !roadMap.Contains("_leverSeen") && !roadMap.Contains("Whisper.Persuaded(w)"),
            "physical lever uses command revision and actual effective AOS result");

        // Five seals (plan 69 §17, 25.11): only living active allies occupy seals,
        // one person holds one seal. These are source-linked integration guards.
        Check(dreamTrace.Contains("w.gameObject.activeInHierarchy && w.Team == Team.Player") &&
              dreamTrace.Contains("bool alone = !EligibleSealWarrior(_zombie);") &&
              dreamTrace.Contains("if (!EligibleSealWarrior(w)) return;") &&
              dreamTrace.Contains("if (_sealOn[i] == null && OnSeal(w.transform.position, DreamRoad.SealAt[i])) { _sealOn[i] = w; return; }") &&
              dreamTrace.Contains("if (EligibleSealWarrior(_zombie)) return true;"),
            "only living active allies count as seal occupants; one warrior holds one seal");
        Check(dreamTrace.Contains("_sealLit[(int)kind] = Fits(kind, _sealOn[(int)kind]);") &&
              dreamTrace.Contains("DreamTutorialChecks.KinPair(left.Soul?.Kin, left.Soul?.PersonId, right.Soul?.Kin, right.Soul?.PersonId)") &&
              dreamTrace.Contains("DreamTutorialChecks.AdvanceSeals(ref _sealHeld, true, warriors, _sealLit[0], Time.deltaTime)") &&
              dreamTrace.Contains("DreamTutorialChecks.FitsWrath(soul.Sin == SinType.Wrath, soul.Get(SinType.Wrath))") &&
              dreamTrace.Contains("return Trades.OfOrder(soul.Trade);") &&
              dreamTrace.Contains("DreamGuards.Make(_tutorialSeed, names)"),
            "gate opens only when all five seals hold their real condition together");
        string soulData = System.IO.File.ReadAllText("Assets/Scripts/Core/Soul/SoulData.cs");
        Check(soulData.Contains("_kin = other._kin;") && soulData.Contains("_personId = other.PersonId;") &&
              soulData.Contains("!SamePerson(a, b) &&") && soulData.Contains("a.Kin.Length > 0 && a.Kin == b.Kin"),
            "kinship survives soul copy; different instances of the same person never count as brothers (source wiring)");
        Check(DreamTutorialChecks.FitsWrath(true, 25f) && DreamTutorialChecks.FitsWrath(true, 90f) &&
              !DreamTutorialChecks.FitsWrath(true, 24.9f) && !DreamTutorialChecks.FitsWrath(true, -70f) &&
              !DreamTutorialChecks.FitsWrath(false, 90f) && !DreamTutorialChecks.FitsWrath(true, float.NaN),
            "sword seal: dominant wrath on the vice side, at least 'smoulders'; meekness does not count");
        Check(DreamTutorialChecks.KinPair("k", "a", "k", "b") &&
              !DreamTutorialChecks.KinPair("k", "a", "k", "a") &&
              !DreamTutorialChecks.KinPair("", "a", "", "b") &&
              !DreamTutorialChecks.KinPair(null, "a", null, "b") &&
              !DreamTutorialChecks.KinPair("k", "a", "j", "b") &&
              !DreamTutorialChecks.KinPair("k", "a", "k", null),
            "brother seals need two different souls sharing one kin mark");
        Check(DreamTutorialChecks.DistinctSealPeople("w", "o", "b1", "b2") &&
              !DreamTutorialChecks.DistinctSealPeople("w", "o", "b1", "b1") &&
              !DreamTutorialChecks.DistinctSealPeople("w", "w", "b1", "b2") &&
              !DreamTutorialChecks.DistinctSealPeople("w", "o", "w", "b2") &&
              !DreamTutorialChecks.DistinctSealPeople("w", "o", "b1", null) &&
              !DreamTutorialChecks.DistinctSealPeople("w", "o", "", "b2"),
            "four seals require four distinct nonempty personal identities");
        Check(dreamTrace.Contains("bool warriors = DreamTutorialChecks.DistinctSealPeople(") &&
              dreamTrace.Contains("_sealOn[(int)DreamRoad.SealKind.Wrath]?.Soul?.PersonId") &&
              dreamTrace.Contains("_sealOn[(int)DreamRoad.SealKind.Order]?.Soul?.PersonId") &&
              dreamTrace.Contains("left?.Soul?.PersonId, right?.Soul?.PersonId)"),
            "gate uses unique personal identities across all four warrior roles (source wiring)");
        Check(DreamTutorialChecks.KinSeal(true, true, true) == 2 &&
              DreamTutorialChecks.KinSeal(true, true, false) == 1 &&
              DreamTutorialChecks.KinSeal(true, false, false) == 0 &&
              DreamTutorialChecks.KinSeal(false, false, false) == 0,
            "brother seal: lit for the pair, half while one waits, dark for a stranger");
        {
            float fiveHeld = 0f;
            bool early = DreamTutorialChecks.AdvanceSeals(ref fiveHeld, true, true, true, 0.6f);
            bool broken = DreamTutorialChecks.AdvanceSeals(ref fiveHeld, true, false, true, 0.6f);
            bool resetHeld = fiveHeld == 0f;
            DreamTutorialChecks.AdvanceSeals(ref fiveHeld, true, true, true, 0.6f);
            bool opened = DreamTutorialChecks.AdvanceSeals(ref fiveHeld, true, true, true, 0.6f);
            Check(!early && !broken && resetHeld && opened, "five seals must hold together; a gap resets the hold");
        }
        Check(dreamTrace.Contains("EligibleSealWarrior(w) && w.HasCommand && w.Command.Kind == CommandKind.Move") &&
              dreamTrace.Contains("_sealOrderedWarrior != ordered || _sealOrderRevision != ordered.CommandRevision") &&
              dreamTrace.Contains("_sealOrderedWarrior = null;") &&
              dreamTrace.Contains("_sealOrderAt = -1f;") &&
              !dreamTrace.Contains("_sealOrderIssued") && !dreamTrace.Contains("SealRefusal"),
            "seal wait tracks the actual warrior and move-order revision, not a stale timestamp");
        Check(dreamTrace.Contains("Time.unscaledTime - _sealOrderAt > SealWait") &&
              dreamTrace.Contains("|| Time.unscaledTime - _sealsAskedAt > SealPatience)) OfferRaise();") &&
              !dreamTrace.Contains("OfferRaise(ordered)") &&
              dreamTrace.Contains("mind.LastDecisionCommandRevision == revision") &&
              dreamTrace.Contains("mind.LastDecisionDetail.RefusedCommand && !Whisper.Persuaded(w)") &&
              dreamTrace.Contains("RefusedSealOrder(_sealOrderedWarrior, _sealOrderRevision)"),
            "seal delay offers an alternative; 'his will' only after the soul's real refusal of that order");

        // Canonical UI uses Lesson.Hint rather than the old scene Text panel.
        string movementUI = System.IO.File.ReadAllText("Assets/Scripts/UI/MovementHintUI.cs");
        string lessonUI = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/Lesson.cs");
        Check(movementUI.Contains("var (key, words) = Line();") &&
              movementUI.Contains("Gameplay.Lesson.RefreshHint(this, key, words);") &&
              movementUI.Contains("!view.FirstPersonNow") &&
              !movementUI.Contains("if (_showing) return;"),
            "movement plate refreshes both key and words from the real camera mode");
        string roleRecovery = dreamTrace.Split("private (string key, string words)? RoleRecoveryHint()", 2)[1]
            .Split("private float _nudgeSent", 2)[0];
        int tacticalRecovery = roleRecovery.IndexOf("if (!view.FirstPersonNow) return (\"Tab\"", StringComparison.Ordinal);
        int roleSwitch = roleRecovery.IndexOf("if (!view.WarriorModeNow) return (\"V\"", StringComparison.Ordinal);
        Check(tacticalRecovery >= 0 && roleSwitch > tacticalRecovery &&
              dreamTrace.Contains("UI.TaskList.Step(recovery.words);") &&
              dreamTrace.Contains("Lesson.Hint(this, recovery.key, recovery.words);"),
            "both dream goals and lesson plates recover the actual camera role before weapon controls");
        int refreshBegin = lessonUI.IndexOf("public static void RefreshHint(", StringComparison.Ordinal);
        int refreshEnd = lessonUI.IndexOf("public static void Unhint(", StringComparison.Ordinal);
        string refreshHint = refreshBegin >= 0 && refreshEnd > refreshBegin
            ? lessonUI.Substring(refreshBegin, refreshEnd - refreshBegin) : "";
        Check(refreshHint.Contains("!ReferenceEquals(_instance._hintOwner, owner)") &&
              refreshHint.Contains("_instance._hintKey = key") &&
              refreshHint.Contains("_instance._hintWords = words") &&
              !refreshHint.Contains("_instance._hintOwner = owner"),
            "refresh cannot steal another owner's active hint");
        Check(lessonUI.Contains("(agent.hasPath || (saveRoute && agent.pathPending))") &&
              lessonUI.Contains("if (saveRoute) _harvestRoute = (visit, hero, point);") &&
              lessonUI.Contains("_instance._leash.Flat(point) > _instance._leash.Radius"),
            "harvest preserves an existing world route and separates clicks outside the lesson circle");
        string playerWalk = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/PlayerWalk.cs");
        Check(lessonUI.Contains("ReferenceEquals(route.Visit, PersonalExpedition.DialogueVisit)") &&
              lessonUI.Contains("route.Hero != SinbinderPlayer.Instance || route.Hero.IsDead") &&
              lessonUI.Contains("agent.isStopped || agent.hasPath || agent.pathPending") &&
              lessonUI.Contains("path.status == NavMeshPathStatus.PathComplete") &&
              lessonUI.Contains("agent.SetPath(path);") &&
              playerWalk.Contains("Lesson.CancelHarvestRoute();"),
            "harvest restores only a reachable route for the same hero and visit unless replaced or cancelled");
        string combatWrapper = System.IO.File.ReadAllText("Assets/Scripts/AOS Engine/AOSWarriorWrapper.cs");
        int hunterRetreat = combatWrapper.IndexOf("CombatManager.Instance?.ReportEnemyRetreat(_self);", StringComparison.Ordinal);
        int hunterDespawn = combatWrapper.IndexOf("Destroy(gameObject);", StringComparison.Ordinal);
        Check(hunterRetreat >= 0 && hunterDespawn > hunterRetreat,
            "confirmed hunter retreat enters the battle resolver before the fleeing body is destroyed");
        string viewHintUI = System.IO.File.ReadAllText("Assets/Scripts/UI/ViewHintUI.cs");
        int campScope = viewHintUI.IndexOf("SceneManager.GetActiveScene().name != \"Prologue_Camp\"", StringComparison.Ordinal);
        int camTransition = viewHintUI.IndexOf("if (_wasEyes && !eyes) _pending = true;", StringComparison.Ordinal);
        Check(campScope >= 0 && camTransition > campScope &&
              viewHintUI.Contains("if (cam == null || cam.FirstPersonNow)") &&
              viewHintUI.Contains("Lesson.Unhint(this);") &&
              viewHintUI.Contains("if (_pending && !eyes && !busy) Show();"),
            "camp invitation is scoped before view transitions and removed on return to first person");
        string commandHintUI = System.IO.File.ReadAllText("Assets/Scripts/UI/CommandHintUI.cs");
        string hintLang = System.IO.File.ReadAllText("Assets/Resources/Lang/en.txt");
        // 25.11: the authored hint stays until the author decides (bot rewrite «ЛКМ… приказ двигаться» listed for him).
        Check(commandHintUI.Contains("Щёлкните по воину, затем правой кнопкой по земле — он пойдёт туда.") &&
              hintLang.Contains("Click a warrior, then right-click the ground — he'll go there."),
            "command hint exists in RU and EN");

        // AOS can legitimately be silent due to a broken scene or missing
        // component. Retest must try the real vote once then recover with a
        // truthful missing-verdict result, rather than hanging forever or
        // inventing an ObeyCommand. Source-level only, not Play Mode.
        int dreamUpdate = dreamTrace.IndexOf("private void Update()", StringComparison.Ordinal);
        int retestStart = dreamTrace.IndexOf("case Stage.Retest:", dreamUpdate, StringComparison.Ordinal);
        int retestEnd = dreamTrace.IndexOf("case Stage.FirstPerson:", retestStart, StringComparison.Ordinal);
        string retest = retestStart >= 0 && retestEnd > retestStart
            ? dreamTrace.Substring(retestStart, retestEnd - retestStart) : "";
        Check(retest.Contains("!_retestManualDecideTried") &&
              retest.Contains("_retestManualDecideTried = true;") &&
              retest.Contains("mind.Decide();") &&
              retest.Contains("Time.unscaledTime - _decideSince > 1.2f"),
            "retest invokes production AOS exactly once when natural verdict is missing");
        Check(retest.Contains("Time.unscaledTime - _decideSince > 6f") &&
              retest.Contains("RetestResult = \"решение не поступило\";") &&
              retest.Contains("Set(Stage.Jars, JarsLine);") &&
              !retest.Contains("RetestResult = \"послушался\";"),
            "retest recovers from missing verdict without manufacturing obedience");

        // The second order is a controlled contrast, not proof of causation.
        // Changing circumstances alone cannot prove that a particular voice
        // or old motive disappeared. Both spoken text and hint must remain
        // faithful to the actual decision and observed leading voice.
        int contrastStart = dreamTrace.IndexOf("private IEnumerator Contrast(", StringComparison.Ordinal);
        int contrastEnd = dreamTrace.IndexOf("private ", contrastStart + 12, StringComparison.Ordinal);
        string contrastText = contrastStart >= 0
            ? dreamTrace.Substring(contrastStart,
                (contrastEnd > contrastStart ? contrastEnd : dreamTrace.Length) - contrastStart)
            : "";
        Check(dreamTrace.Contains("Убийц рядом больше нет. Что решит душа теперь?") &&
              !dreamTrace.Contains("Причины больше нет — что решит душа теперь?") &&
              contrastText.Contains("bool obeyed = ActuallyObeyed(d, c);") &&
              contrastText.Contains("voice == _courtVoice") &&
              contrastText.Contains("его решение совпало с вашим") &&
              contrastText.Contains("её решение совпало с вашим") &&
              contrastText.Contains("Среди голосов вновь выделяется {0}") &&
              contrastText.Contains("Теперь среди голосов выделяется {0}") &&
              !contrastText.Contains("Причины больше нет") &&
              !contrastText.Contains("нашлась другая причина") &&
              !contrastText.Contains("причина другая"),
            "dream retest reports action and observed voice, not an unproved vanished or changed cause");

        // На Windows git с autocrlf отдаёт каталог с \r\n — запись та же, сверяем по \n.
        var langSource = System.IO.File.ReadAllText("Assets/Resources/Lang/en.txt").Replace("\r\n", "\n");
        (string ru, string en)[] retestTranslations =
        {
            ("Убийц рядом больше нет. Что решит душа теперь?",
             "The killers are no longer nearby. What will the soul decide now?"),
            ("Тот же приказ — и теперь его решение совпало с вашим. Убийц рядом больше нет.",
             "The same order, and now his decision matches yours. The killers are no longer nearby."),
            ("Тот же приказ — и теперь её решение совпало с вашим. Убийц рядом больше нет.",
             "The same order, and now her decision matches yours. The killers are no longer nearby."),
            ("И снова нет. Убийц рядом больше нет; какой голос перевесил, неизвестно.",
             "No again. The killers are no longer nearby; which voice tipped the balance is unknown."),
            ("И снова нет. Среди голосов вновь выделяется {0}.",
             "No again. Among the voices, {0} stands out once more."),
            ("И снова нет. Теперь среди голосов выделяется {0}.",
             "No again. Now {0} stands out among the voices."),
        };
        bool translated = true;
        foreach (var item in retestTranslations)
            translated &= langSource.Contains("msgid " + '"' + item.ru + '"' + "\nmsgstr " + '"' + item.en + '"');
        Check(translated, "six honest retest prompts and verdicts have direct EN catalog entries");

        // Short-Court onboarding must reuse the production SoulCourt and
        // the exact original verdict; consent changes automatic events only.
        string courtSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/SoulCourt.cs");
        Check(SoulCourtVerdict.DeathReaction(SoulCourtVerdictKind.Refused, true) == "Неповиновение." &&
              SoulCourtVerdict.DeathReaction(SoulCourtVerdictKind.Refused, false) == null &&
              SoulCourtVerdict.DeathReaction(SoulCourtVerdictKind.Hesitated, true) == null &&
              SoulCourtVerdict.DeathReaction(SoulCourtVerdictKind.IndependentMatch, true) == null &&
              SoulCourtVerdict.DeathReaction(SoulCourtVerdictKind.Obeyed, true) == null,
            "Death speaks only on recorded disobedience, not hesitation or independent matching");
        Check(courtSource.Contains("SoulCourtVerdict.DeathReaction(verdict, c != null && c.HasCommand)") &&
              courtSource.Contains("yield return Line(box, host, verdictTitle, sentence);"),
            "Death's reaction does not replace the factual court verdict");
        string deathAudio = System.IO.File.ReadAllText("Assets/Scripts/Audio/DeathVoice.cs");
        Check(deathAudio.Contains("ticket != _lessonTicket") &&
              deathAudio.Contains("public static void QuietLesson()") &&
              deathAudio.Contains("_lessonVoice.Stop();") &&
              dreamTrace.Contains("_deathSpoken.Add(_stage)") &&
              dreamTrace.Contains("UpdateDeathLesson(quiet);") &&
              dreamTrace.Contains("Audio.DeathVoice.QuietLesson();"),
            "dream cue is once per stage, cancelled separately from the Court");
        Check(dreamTrace.Contains("WithDeath(Loc.T(_line))") &&
              dreamTrace.Contains("case Stage.Harvest: case Stage.Rebind:") &&
              !dreamTrace.Contains("он пойдёт."),
            "tutorial captions and location markers support the action without promising obedience");
        // Production classifier, not a duplicate copy of its arithmetic.
        // A flag saying "obeyed" cannot turn Attack into a completed Hold.
        Check(SoulCourtVerdict.Classify(true, false, false, false, false) ==
              SoulCourtVerdictKind.Refused &&
              SoulCourtVerdict.Classify(true, false, true, false, true) ==
              SoulCourtVerdictKind.Refused,
            "Court cannot call an action obeyed just because refusal flag is missing");
        Check(SoulCourtVerdict.Classify(true, true, true, false, true) ==
              SoulCourtVerdictKind.Obeyed &&
              SoulCourtVerdict.Classify(true, true, true, false, false) ==
              SoulCourtVerdictKind.Obeyed,
            "Court recognizes a deliberate obey decision that matches the command");
        Check(SoulCourtVerdict.Classify(true, true, false, false, false) ==
              SoulCourtVerdictKind.IndependentMatch &&
              SoulCourtVerdict.Classify(true, true, false, false, true) ==
              SoulCourtVerdictKind.IndependentMatch,
            "Court recognizes an independently chosen action that matches the command");
        Check(SoulCourtVerdict.Classify(true, true, true, true, false) ==
              SoulCourtVerdictKind.Hesitated &&
              SoulCourtVerdict.Classify(true, false, false, true, true) ==
              SoulCourtVerdictKind.Hesitated,
            "hesitation is never misreported as completed obedience or refusal");
        Check(SoulCourtVerdict.Classify(false, false, false, false, true) ==
              SoulCourtVerdictKind.Refused &&
              SoulCourtVerdict.Classify(false, false, false, false, false) ==
              SoulCourtVerdictKind.Obeyed,
            "legacy no-command court outcome is not manufactured from absent context");


        string preferencesSource = System.IO.File.ReadAllText("Assets/Scripts/Core/Preferences.cs");
        string selectionSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/SelectionManager.cs");
        string courtCallerSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/CourtCaller.cs");
        string pauseMenuSource = System.IO.File.ReadAllText("Assets/Scripts/UI/PauseMenu.cs");
        // Our dream (20.11): the first court is full, later ones brief — each on the SAME
        // decision and the votes recorded at the moment of the decision (Ballot), not reweighed.
        // The bots' "show the court?" IMGUI prompt was not taken: the court toggle lives in the
        // pause menu (checked below), and the dream court is a lesson, so it is always forced.
        Check(dreamTrace.Contains("StartCoroutine(Judge(mind.LastDecisionDetail, mind.LastContext, Ballot(mind)));") &&
              dreamTrace.Contains("yield return SoulCourt.Convene(_zombie, d, c, _courtOrder, brief: briefCourt,") &&
              dreamTrace.Contains("bool briefCourt = _courts > 0;") &&
              dreamTrace.Contains("if (SoulCourt.Completed > completedBefore)") &&
              dreamTrace.Contains("_courts++;") &&
              // 23.11 (529cad48): the Judge parameter is named originalVotes, as in ScalesOverhead and CourtCaller.
              dreamTrace.Contains("originalVotes: originalVotes, force: true);"),
            "dream court judges the SAME AOS decision with its original ballot, full first then brief");
        // Source-backed evidence, not a guessed duration: only the fully
        // finished Court increments Completed and produces a Unity wall-clock
        // duration. Early yield breaks do neither. The dream's first Court
        // stays full until that concrete completion happens.
        int completionPos = courtSource.IndexOf("Completed++;", StringComparison.Ordinal);
        int finishPos = courtSource.LastIndexOf("yield return fader.To(0f, 0.6f);", StringComparison.Ordinal);
        int lastExitPos = courtSource.LastIndexOf("Step = \"\";", StringComparison.Ordinal);
        Check(courtSource.Contains("public static int Completed { get; private set; }") &&
              courtSource.Contains("public static float LastDurationSeconds { get; private set; } = -1f;") &&
              courtSource.Contains("float startedAt = Time.realtimeSinceStartup;") &&
              courtSource.Contains("LastDurationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - startedAt);") &&
              courtSource.Contains("[СУД ЗАМЕР]") &&
              finishPos >= 0 && completionPos > finishPos &&
              lastExitPos > finishPos && completionPos > lastExitPos,
            "real Court timing is recorded only after camera and scene restoration");

        Check(courtSource.Contains("public static bool Seen => Completed > 0") &&
              courtSource.Contains("Held = 0; Completed = 0; LastDurationSeconds = -1f;") &&
              dreamTrace.Contains("int completedBefore = SoulCourt.Completed;") &&
              dreamTrace.Contains("bool briefCourt = _courts > 0;") &&
              dreamTrace.Contains("if (SoulCourt.Completed > completedBefore)") &&
              dreamTrace.Contains("TraceFirstFive(\"court_not_shown\")") &&
              dreamTrace.Contains("T8 суд не состоялся") &&
              !dreamTrace.Contains("brief: _courts++ > 0") &&
              !dreamTrace.Contains("TraceFirstFive(\"court_coroutine_finished\")"),
            "an aborted Court is not logged as a lesson and cannot shorten the next first Court");

        Check(courtSource.Contains("var verdict = Verdict(d, c);") &&
              courtSource.Contains("c.SatisfiedBy(d.Action)") &&
              courtSource.Contains("bool recordedRefusal = d.RefusedCommand;") &&
              courtSource.Contains("d.RefusedCommand = verdict == SoulCourtVerdictKind.Refused;") &&
              courtSource.Contains("Outcome = verdict == SoulCourtVerdictKind.Hesitated") &&
              courtSource.Contains("Причина отказа не установлена.") &&
              !courtSource.Contains("Outcome = d.RefusedCommand ?") &&
              courtSource.Contains("if (originalVotes == null && !d.Weighed &&"),
            "real Soul Court classifies chosen action using original command and preserves historical votes");
        Check(courtSource.Contains("recordedRefusal != d.RefusedCommand)") &&
              courtSource.Contains("d.Weighed = false;") &&
              courtSource.Contains("The original vote and original cause are authoritative.") &&
              courtSource.Contains("return d.Weighed ? Loc.T("),
            "Court never attributes a stale counterfactual cause to a corrected action verdict");
        Check(courtSource.Split(new[] { "var verdict = Verdict(d, c);" },
              StringSplitOptions.None).Length == 3 &&
              courtSource.Contains("\n            verdict = Verdict(d, c);") &&
              courtSource.Contains("bool against = d.RefusedCommand || d.Hesitated || independent;") &&
              courtSource.Contains("host._aim = Final(d, total, reach, independent);") &&
              courtSource.Contains("lean = Final(d, total, reach, independent);") &&
              courtSource.Contains("Loudest(judges, d.RefusedCommand || d.Hesitated || independent)"),
            "court, overhead leading voice and scale direction share the same action-based verdict");

        Check(!dreamTrace.Contains("DrawCourtChoice") &&
              !dreamTrace.Contains("_courtChoicePending") &&
              courtSource.Contains("The original vote and original cause are authoritative.") &&
              courtSource.Contains("if (originalVotes == null && !d.Weighed &&"),
            "no extra court prompt in the dream; recorded votes are never reweighed");
        // 26.11: settings go through Sinbinder.Core.Prefs (off the work PC, Core/DataFolder), not PlayerPrefs.
        Check(preferencesSource.Contains("CourtChoiceMade => Sinbinder.Core.Prefs.GetInt(CourtChoiceKey, 0) != 0") &&
              preferencesSource.Contains("Sinbinder.Core.Prefs.SetInt(CourtChoiceKey, 1);") &&
              preferencesSource.Contains("public static void SetAutomaticCourt(bool enabled)") &&
              preferencesSource.Contains("ToggleCourt() => SetAutomaticCourt(!Court)"),
            "choice persists and the pause-menu toggle is the same preference");
        Check(courtSource.Contains("bool force = false") &&
              courtSource.Contains("(!Preferences.Court && !force)") &&
              selectionSource.Contains("force: true));") &&
              !selectionSource.Contains("if (!Core.Preferences.Court)") &&
              courtCallerSource.Contains("if (!Preferences.Court || SoulCourt.Sitting") &&
              pauseMenuSource.Contains("Автоматический Суд души"),
            "manual U review and required tutorial work when automatic CourtCaller is disabled");

        // Source-linked AOS explanation viewport fitting: the *real* Unity
        // tooltip uses this solver, not a duplicated CI-only calculation.
        Check(TooltipViewportFit.Scale(280f, 130f, 1280f, 720f, 12f) == 1f,
            "readable normal tooltip keeps its native scale");
        Check(TooltipViewportFit.Scale(1700f, 2000f, 1280f, 720f, 12f) < 0.35f,
            "oversized explanations must shrink rather than clip above the screen");
        Check(TooltipViewportFit.Scale(200f, 900f, 1280f, 720f, 12f) < 1f,
            "narrow but tall AOS explanation is still fitted vertically");
        float halfScreenFit = TooltipViewportFit.Scale(900f, 1100f, 800f, 600f, 12f);
        float fullScreenFit = TooltipViewportFit.Scale(900f, 1100f, 1920f, 1080f, 12f);
        Check(fullScreenFit > halfScreenFit &&
              halfScreenFit == TooltipViewportFit.Scale(900f, 1100f, 800f, 600f, 12f),
            "viewport resize recomputes from native size instead of compounding shrinkage");
        bool fits = true;
        foreach (float sw in new[] { 320f, 800f, 1280f, 1920f })
        foreach (float sh in new[] { 240f, 600f, 720f, 1080f })
        foreach (float pw in new[] { 200f, 400f, 1600f, 2400f })
        foreach (float ph in new[] { 60f, 500f, 1250f, 3000f })
        {
            float fit = TooltipViewportFit.Scale(pw, ph, sw, sh, 12f);
            if (fit <= 0f || fit > 1f || pw * fit > sw - 24.0f + 0.01f ||
                ph * fit > sh - 24.0f + 0.01f) fits = false;
        }
        Check(fits, "tooltip solver keeps 256 large and small geometries within 12px margins");
        string tooltipSource = System.IO.File.ReadAllText("Assets/Scripts/UI/WarriorTooltipUI.cs");
        Check(tooltipSource.Contains("TooltipViewportFit.Scale(width, height, Screen.width, Screen.height, margin)") &&
              tooltipSource.Contains("_nativePanelScale.x * fit") &&
              tooltipSource.Contains("_nativePanelScale.y * fit"),
            "WarriorTooltipUI uses source-tested viewport fit and restores native proportions");

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

        CombatIntegrationChecks.Run(Check);

        // Source-linked visual causality: removing soul B must not delete A's light.
        // Runtime Play Mode with two simultaneous souls is still required.
        var soulManagerSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/SoulManager.cs");
        var soulIndicatorSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/SoulIndicator.cs");
        Check(soulIndicatorSource.Contains("public FadingSoul Soul => _soul;") &&
              soulManagerSource.Contains("ReferenceEquals(it.Soul, soul)"),
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
        // Author 16.11: original wording restored («обезопасьте раненого товарища»).
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

        // Tug tutorial: hovering over a neutral while the game is paused must not
        // earn 0.8s of reading credit. Source-linked L0, Unity pause UX still unknown.
        var tugSource = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/TugTutorial.cs");
        // Tug lesson 3/9 must distinguish command delivery from an effective AOS vote.
        // Source-linked L0 only: Unity scene and novice acceptance remain unverified.
        int orderStart = tugSource.IndexOf("private static bool AnyOrdered()");
        int orderEnd = tugSource.IndexOf("private static Crypt.BindingDevice Device()", orderStart);
        string orderGate = orderStart >= 0 && orderEnd > orderStart
            ? tugSource.Substring(orderStart, orderEnd - orderStart) : "";
        Check(orderGate.Contains("w.HasCommand && w.Command.Lord == Lords.Player") &&
              orderGate.Contains("mind.HasCurrentCommandDecision"),
              "neutral order requires a current AOS decision, not delivery alone");
        Check(orderGate.Contains("mind.LastDecisionDetail.Action == Sinbinder.AOS.ActionType.ObeyCommand") &&
              orderGate.Contains("!mind.LastDecisionDetail.RefusedCommand") &&
              orderGate.Contains("!mind.LastDecisionDetail.Hesitated"),
              "pending, refused, hesitant and alternative actions cannot complete order lesson");
        Check(!orderGate.Contains("return w.HasCommand;") &&
              !orderGate.Contains("if (w.HasCommand) return true;"),
              "old delivery-only tutorial shortcut is absent");


        int attitudeStart = tugSource.IndexOf("private void WatchAttitude()");
        int attitudeEnd = tugSource.IndexOf("#if UNITY_EDITOR", attitudeStart);
        string attitudeBody = attitudeStart >= 0 && attitudeEnd > attitudeStart
            ? tugSource.Substring(attitudeStart, attitudeEnd - attitudeStart) : "";
        Check(attitudeBody.Contains("_attitudeReadFor += Time.deltaTime;"),
            "neutral attitude requires active gameplay time to complete");
        Check(!attitudeBody.Contains("Time.unscaledDeltaTime"),
            "paused frames cannot accumulate neutral attitude read time");

        // Save-load source contracts: malformed input must not partially mutate
        // the squad/shelf or escape JsonUtility's ArgumentException path.
        // This is L0 source verification, NOT a Unity deserialization test.
        var saveSource = System.IO.File.ReadAllText("Assets/Scripts/Core/SaveSystem.cs");
        int validateSave = saveSource.IndexOf("if (save.Squad == null || save.Shelf == null");
        int firstMutation = saveSource.IndexOf("Gameplay.CardTable.BeforeSaveOrLoad();", saveSource.IndexOf("public static bool Restore(SaveGame save)"));
        Check(validateSave >= 0 && firstMutation > validateSave &&
              saveSource.Contains("save.Squad.Exists(m => m == null || (m.HasEquipmentLayout && InvalidEquipment(m.Equipment))") &&
              saveSource.Contains("(m.HasSoulSpectra && !SoulData.IsValidRosterSpectra(m.SoulSpectra)))") &&
              saveSource.Contains("save.Shelf.Exists(s => !IsValidSoul(s))") &&
              saveSource.Contains("!SoulCustody.IsValid(save.SoulCustody, save.Shelf)"),
              "corrupt squad, equipment and soul ownership rejected before Restore changes live state");
        Check(saveSource.Contains("catch (System.ArgumentException e)") &&
              saveSource.Contains("catch (System.UnauthorizedAccessException e)"),
              "malformed JSON and denied access fail safely in SaveSystem.Read");
        Check(saveSource.Contains("foreach (var m in save.Squad)") &&
              saveSource.Contains("foreach (var s in save.Shelf)"),
              "valid squad and soul restoration paths remain present");
        int arrivedStart = saveSource.IndexOf("private static void Arrived(");
        int arrivedSandbox = saveSource.IndexOf("if (scene.name == CryptMap.SceneName && restored.OpensCryptSandbox)", arrivedStart);
        int arrivedSouls = saveSource.IndexOf("RestoreSouls(restored, scene);", arrivedStart);
        int checkpoint = saveSource.IndexOf("MarkCheckpoint(scene.name, scene, captureLiveEquipment: false);", arrivedStart);
        Check(arrivedSandbox > arrivedStart && arrivedSouls > arrivedSandbox && checkpoint > arrivedSouls,
              "the destination reopens the saved crypt sandbox and receives its souls before the arrival checkpoint");
        var deviceSource = System.IO.File.ReadAllText("Assets/Scripts/Crypt/BindingDevice.cs");
        Check(deviceSource.Contains("if (!SaveSystem.Arriving)") &&
              deviceSource.Contains("SoulManager.Instance?.PutBack(new SoulManager.Kept(_soul, _quality));"),
              "unloading a socket returns its soul only on ordinary scene transitions");

        // A missing destination scene must fail before Restore mutates live state.
        // Positive paths: same part without reload and valid raid-host scenes
        // still reach Restore. This is L0 source-linked, not Unity Play Mode.
        int returnToStart = saveSource.IndexOf("public static bool ReturnTo(SaveGame save, bool reload = false)");
        int restoreStart = saveSource.IndexOf("public static bool Restore(SaveGame save)", returnToStart);
        string returnToBody = returnToStart >= 0 && restoreStart > returnToStart
            ? saveSource.Substring(returnToStart, restoreStart - returnToStart) : "";
        int sceneGuard = returnToBody.IndexOf("changeScene && !Application.CanStreamedLevelBeLoaded(scene)");
        int restoreCall = returnToBody.IndexOf("if (!Restore(save, keepScene: !changeScene)) return false;");
        int arrivalGuard = returnToBody.IndexOf("if (save == null || Arriving) return false;");
        Check(arrivalGuard >= 0 && arrivalGuard < restoreCall,
              "a second load cannot replace a scene transition already in progress");
        Check(sceneGuard >= 0 && restoreCall > sceneGuard &&
              returnToBody.Substring(sceneGuard, restoreCall - sceneGuard).Contains("return false;"),
              "missing destination scene rejects save before live state restoration");
        Check(returnToBody.Contains("reload || save.Scene != Here || sandboxChanged") &&
              returnToBody.Contains("WorkshopRecruitsChanged(save)") &&
              returnToBody.Contains("if (!changeScene) return true;") &&
              returnToBody.Contains("string scene = changeScene ? RaidEvent.HostOf(save.Scene) : null;") &&
              returnToBody.Contains("SceneManager.LoadScene(scene);"),
              "same-scene and valid hosted raid destination still follow original restore paths");
        Check(returnToBody.Contains("save.Scene == CryptMap.SceneName && save.OpensCryptSandbox != CryptMap.Opened"),
              "loading a different crypt story stage rebuilds the scene and restores the saved sandbox state");
        int cryptSquadGuard = returnToBody.IndexOf("CryptSquadChanged(save)");
        Check(cryptSquadGuard >= 0 && cryptSquadGuard < sceneGuard &&
              returnToBody.Contains("CryptSquadChanged(SaveGame save) => save.OpensCryptSandbox") &&
              returnToBody.Contains("warrior.gameObject.activeInHierarchy"),
              "quickload checks the saved crypt squad before choosing whether to rebuild the scene");
        Check(saveSource.Contains("w.Id == m.WarriorId") &&
              saveSource.Contains("restoreCryptCondition && !member.IsAway") &&
              saveSource.Contains("warrior.RestoreExpeditionCondition(member);"),
              "same-lineup crypt quickload restores the saved condition of fighters in the crypt");
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


        // Revalidate a slot at click time: a save may become unreadable after
        // the F9 panel was drawn. Empty and valid slots still take Write().
        int saveMethod = slotsSource.IndexOf("private static string Save(string slot)");
        int loadMethod = slotsSource.IndexOf("private bool Load(string slot)", saveMethod);
        string saveAction = saveMethod >= 0 && loadMethod > saveMethod
            ? slotsSource.Substring(saveMethod, loadMethod - saveMethod) : "";
        int existsGuard = saveAction.IndexOf("SaveSystem.Exists(path) && SaveSystem.Read(path) == null");
        int writeAction = saveAction.IndexOf("SaveSystem.Write(SaveSystem.Snapshot(), path)");
        Check(existsGuard >= 0 && writeAction > existsGuard &&
              saveAction.Substring(existsGuard, writeAction - existsGuard).Contains("return warning;"),
              "slot becoming unreadable after UI draw is rejected before overwrite");
        Check(saveAction.Contains("string path = SaveSystem.PathOf(slot);") &&
              saveAction.Contains("bool ok = SaveSystem.Write(SaveSystem.Snapshot(), path);") &&
              saveAction.Contains("ok ? Loc.F("),
              "empty and readable slot save paths still reach normal write result");

        Console.WriteLine("PASS: " + _checks + " production tutorial rule assertions");
        return 0;
    }
}
