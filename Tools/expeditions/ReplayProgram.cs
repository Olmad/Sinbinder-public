using System;
using System.Linq;
using Sinbinder.Gameplay;

static class Program
{
    static int tests;
    static void Test(bool condition, string why) {if(!condition)throw new Exception("FAIL "+why);tests++;}
    static void Main()
    {
        var replay=new ExpeditionReplayData("Соляной обоз","Марга",new[]{"Марга","Вейн","Марга"});
        replay.Append(ExpeditionReplayKind.Departure,"Марга","Вышли из склепа.");
        replay.Append(ExpeditionReplayKind.March,"Марга","Идут по дороге.");
        replay.Append(ExpeditionReplayKind.CommandDecision,"Марга","Решила сразиться.");
        for(int i=0;i<125;i++)
            replay.Append(ExpeditionReplayKind.CombatAction,i%2==0?"Марга":"Вейн",
                          "Настоящее действие "+i,damage:i%3==0?5:0,round:i/5+1);
        replay.Append(ExpeditionReplayKind.Loot,"Марга","Собрали монеты.");
        replay.Append(ExpeditionReplayKind.Return,"Марга","Вернулись.");
        Test(replay.Squad.Count==2,"squad deduplicated, no phantom people");
        Test(replay.HasCompleteRoute,"full route has departure and return");
        Test(replay.Frames.Count==130,"all 125 combat events preserved, not just three");
        Test(replay.Frames.Select(x=>x.Sequence).SequenceEqual(Enumerable.Range(0,130)),
             "every event retains original chronology");
        Test(replay.AllEvents().Count()==130,"full trace accessible");
        Test(replay.Frames[3].Round==1 && replay.Frames[127].Round==25,
             "real AOS rounds survive the original event sequence");
        Test(replay.Frames[0].Round==0 && replay.Frames[129].Round==0,
             "noncombat stages do not invent combat rounds");
        Test(replay.Frames[126].Kind==ExpeditionReplayKind.CombatAction,"last attack retained");
        Test(replay.Frames.All(x=>x.Description.Length>0),"every authored step describable");
        var incomplete=new ExpeditionReplayData("Часовня","",null);
        incomplete.Append(ExpeditionReplayKind.CombatAction,"?","No route");
        Test(!incomplete.HasCompleteRoute,"unrealistic partial trace is not claimed complete");
        ExpeditionReplayArchive.Forget();
        for(int i=0;i<12;i++)
        {
            var trace=new ExpeditionReplayData("Mission "+i,"Chief",new[]{"Scout"});
            trace.Append(ExpeditionReplayKind.Departure,"Chief","Departure");
            trace.Append(ExpeditionReplayKind.March,"Chief","Route");
            trace.Append(ExpeditionReplayKind.Return,"Scout","Home");
            ExpeditionReplayArchive.Add(trace);
        }
        Test(ExpeditionReplayArchive.History.Count==8,"retains exactly eight newest replays");
        Test(ExpeditionReplayArchive.History[0].MissionName=="Mission 4","evicts oldest");
        var snapshot=ExpeditionReplayArchive.Snapshot();
        ExpeditionReplayArchive.Forget();
        Test(ExpeditionReplayArchive.History.Count==0,"new game clears history");
        ExpeditionReplayArchive.Restore(snapshot);
        Test(ExpeditionReplayArchive.History[7].MissionName=="Mission 11","restore retains newest");
        // A saved replay is an independent record, not an alias of a live
        // viewer or a caller-owned object.
        var savedCopy=ExpeditionReplayArchive.Snapshot();
        var originalName=ExpeditionReplayArchive.History[0].MissionName;
        savedCopy[0].MissionName="TAMPERED";
        savedCopy[0].Frames[0].Description="TAMPERED";
        savedCopy[0].Squad.Clear();
        Test(ExpeditionReplayArchive.History[0].MissionName==originalName,
             "snapshot metadata is independent");
        Test(ExpeditionReplayArchive.History[0].Frames[0].Description!="TAMPERED",
             "snapshot frames are independent");
        Test(ExpeditionReplayArchive.History[0].Squad.Count==1,
             "snapshot squad list is independent");
        var callerOwned=new ExpeditionReplayData("Caller","Captain",new[]{"Captain"});
        callerOwned.Append(ExpeditionReplayKind.Departure,"Captain","Departure");
        callerOwned.Append(ExpeditionReplayKind.March,"Captain","March");
        callerOwned.Append(ExpeditionReplayKind.Return,"Captain","Return");
        ExpeditionReplayArchive.Add(callerOwned);
        callerOwned.Frames[0].Description="MUTATED";
        Test(ExpeditionReplayArchive.History.Last().Frames[0].Description=="Departure",
             "adding a replay must copy caller data");
        // UI callers used to receive the actual mutable archive entries,
        // despite the IReadOnlyList interface. A viewer/edit could silently
        // rewrite recorded AOS outcomes and the NEXT save. History must be
        // a detached read model; Add/Snapshot/Restore own persistence.
        var historyView=ExpeditionReplayArchive.History;
        int entryCountBefore=historyView.Count;
        string storedName=ExpeditionReplayArchive.History.Last().MissionName;
        string storedStep=ExpeditionReplayArchive.History.Last().Frames[0].Description;
        historyView[historyView.Count-1].MissionName="FORGED MISSION";
        historyView[historyView.Count-1].Leader="FORGED LEADER";
        historyView[historyView.Count-1].Squad.Clear();
        historyView[historyView.Count-1].Frames[0].Description="FORGED EVENT";
        historyView[historyView.Count-1].Frames.Clear();
        Test(ExpeditionReplayArchive.History.Count==entryCountBefore &&
             ExpeditionReplayArchive.History.Last().MissionName==storedName &&
             ExpeditionReplayArchive.History.Last().Frames[0].Description==storedStep,
            "mutating the public History view cannot rewrite recorded expedition facts");
        Test(ExpeditionReplayArchive.History.Last().Squad.Count==1 &&
             ExpeditionReplayArchive.History.Last().Frames.Count==3 &&
             ExpeditionReplayArchive.History.Last().Leader=="Captain",
            "public History exposes detached squad, leader and complete event chronology");
        Test(ExpeditionReplayArchive.Snapshot().Last().Frames[0].Description==storedStep,
            "after History tampering the next SaveGame replay snapshot is unchanged");
        var historyToRestore=ExpeditionReplayArchive.Snapshot();
        ExpeditionReplayArchive.Forget();
        ExpeditionReplayArchive.Restore(historyToRestore);
        Test(ExpeditionReplayArchive.History.Last().MissionName==storedName &&
             ExpeditionReplayArchive.History.Last().Frames.Count==3,
            "saved expedition chronicle survives archive reset and restore unchanged");

        var roundTrace=new ExpeditionReplayData("Rounds","Captain",new[]{"Captain"});
        roundTrace.Append(ExpeditionReplayKind.Departure,"Captain","Leaving");
        roundTrace.Append(ExpeditionReplayKind.CombatAction,"Captain","Hit",round:7);
        roundTrace.Append(ExpeditionReplayKind.Return,"Captain","Home");
        ExpeditionReplayArchive.Add(roundTrace);
        Test(ExpeditionReplayArchive.Snapshot().Last().Frames[1].Round==7,
            "archive retains actual AOS round across snapshot");
        var broken=new ExpeditionReplayData("Corrupt","Captain",new[]{"Captain"});
        broken.Append(ExpeditionReplayKind.Departure,"Captain","Departure");
        broken.Frames.Add(null);
        broken.Append(ExpeditionReplayKind.Return,"Captain","Return");
        int beforeBroken=ExpeditionReplayArchive.History.Count;
        ExpeditionReplayArchive.Add(broken);
        Test(ExpeditionReplayArchive.History.Count==beforeBroken,
             "corrupt frame is rejected without corrupting the archive");
        ExpeditionReplayArchive.Restore(null);
        Test(ExpeditionReplayArchive.History.Count==0,"old save without history safe");
        // Actual action facts, not nominal attack or attempted heal.
        Test(ExpeditionCombatFacts.ActualDamage(50f,43f)==7f,
            "armour and absorption: record actual seven HP lost, not nominal attack");
        Test(ExpeditionCombatFacts.ActualDamage(50f,50f)==0f,
            "fully blocked attack does not masquerade as damage");
        Test(ExpeditionCombatFacts.ActualDamage(10f,0f)==10f,
            "killing blow records only HP actually remaining");
        Test(ExpeditionCombatFacts.ActualDamage(10f,15f)==0f,
            "target recovering HP does not count as damage");
        Test(ExpeditionCombatFacts.ActualHealing(99f,100f)==1f,
            "healing reports only restored HP rather than attempted five");
        Test(ExpeditionCombatFacts.ActualHealing(30f,30f)==0f,
            "healing at cap does not report a cure");
        Test(ExpeditionCombatFacts.ActualHealing(30f,35f)==5f,
            "real healing delta is retained");
        Test(ExpeditionCombatFacts.ActualDamage(float.NaN,10f)==0f,
            "invalid health values cannot inject imaginary damage");

        // New format: AOS outcome facts survive both archive clone and
        // saved JSON-shaped records. Old records must remain ambiguous,
        // not be misclassified as failed actions by default-false fields.
        var observed=new ExpeditionReplayData("Соляной обоз","Командир",new[]{"Командир"});
        observed.Append(ExpeditionReplayKind.Departure,"Командир","Вышел");
        observed.Append(ExpeditionReplayKind.CombatAction,"Командир","Действительно ударил",
            target:"runtime-guid",damage:7f,round:4,targetName:"Чужой 2",
            hasOutcome:true,resolved:true,targetKilled:true,targetHealthAfter:0f,
            actorId:"recorded-actor-92",actionCode:"Attack");
        observed.Append(ExpeditionReplayKind.Return,"Командир","Вернулся");
        ExpeditionReplayArchive.Add(observed);
        var observedCopy=ExpeditionReplayArchive.Snapshot().Last().Frames[1];
        Test(observedCopy.TargetName=="Чужой 2" && observedCopy.Target=="runtime-guid",
            "real target label and original runtime ID both retained");
        Test(observedCopy.ActorId=="recorded-actor-92" && observedCopy.ActionCode=="Attack",
            "real actor identity and AOS action code survive independent replay archive");
        observed.Frames[1].ActorId="TAMPERED ACTOR";
        observed.Frames[1].ActionCode="FakeAction";
        Test(ExpeditionReplayArchive.Snapshot().Last().Frames[1].ActorId=="recorded-actor-92" &&
             ExpeditionReplayArchive.Snapshot().Last().Frames[1].ActionCode=="Attack",
            "live caller cannot overwrite the saved historical AOS action");
        Test(observedCopy.HasOutcome && observedCopy.Resolved && observedCopy.TargetKilled,
            "verified hit, effective result and fatality survive archive copy");
        Test(observedCopy.Round==4 && observedCopy.Damage==7f &&
             observedCopy.TargetHealthAfter==0f,
            "actual AOS round, post-armour damage and target HP survive copy");
        observed.Frames[1].TargetName="TAMPER";
        observed.Frames[1].Resolved=false;
        Test(ExpeditionReplayArchive.History.Last().Frames[1].Resolved &&
             ExpeditionReplayArchive.History.Last().Frames[1].TargetName=="Чужой 2",
             "changing a live battle event cannot rewrite saved observations");

        var failed=new ExpeditionReplayData("Отказ","Командир",new[]{"Командир"});
        failed.Append(ExpeditionReplayKind.Departure,"Командир","Вышел");
        failed.Append(ExpeditionReplayKind.CombatAction,"Командир","Пытался",
            targetName:"Чужой 1",round:2,hasOutcome:true,resolved:false);
        failed.Append(ExpeditionReplayKind.Return,"Командир","Вернулся");
        ExpeditionReplayArchive.Add(failed);
        var failedCopy=ExpeditionReplayArchive.Snapshot().Last().Frames[1];
        Test(failedCopy.HasOutcome && !failedCopy.Resolved &&
             failedCopy.TargetName=="Чужой 1",
            "failed action survives as observed failure without invented effect");
        var legacy=ExpeditionReplayArchive.Snapshot().Last().Frames[0];
        Test(!legacy.HasOutcome && !legacy.Resolved && !legacy.TargetKilled,
            "route and old-save frames are not mislabeled as failed combat");
        Test(string.IsNullOrEmpty(legacy.ActorId) && string.IsNullOrEmpty(legacy.ActionCode),
            "legacy replay never invents actor IDs or AOS actions");

        // The 3D visualization must be based on the recorded event stream
        // and must not call the AOS resolver or award expedition loot again.
        var viewSource=System.IO.File.ReadAllText("Assets/Scripts/UI/ExpeditionReplayViewer.cs");
        var stageSource=System.IO.File.ReadAllText("Assets/Scripts/UI/ExpeditionReplayStage3D.cs");
        var boardSource=System.IO.File.ReadAllText("Assets/Scripts/Crypt/MissionBoard.cs");
        Test(boardSource.Contains("ev.ActorId,ev.Action.ToString()") &&
             boardSource.Contains("frame.ActorId,frame.ActionCode") &&
             stageSource.Contains("frame.ActionCode") && stageSource.Contains("frame.TargetKilled"),
            "actual AOS action, identity, target and death reach the 3D recorded-event stage");
        Test(viewSource.Contains("new ExpeditionReplayStage3D(data)") &&
             viewSource.Contains("_stage?.Dispose()") &&
             stageSource.Contains("for (int i = 0; i < index; i++) ApplyPast(_data.Frames[i]);") &&
             !stageSource.Contains("AutoBattleResolver.Resolve(") &&
             !stageSource.Contains("CombatDecisionContext.Create("),
            "3D replay reconstructs chronology without rerolling AOS or touching the live battle");
        var polygon=System.IO.File.ReadAllText("Assets/Scripts/Multiplayer/SinbinderLanBootstrap.cs");
        var buildScenes=System.IO.File.ReadAllText("ProjectSettings/EditorBuildSettings.asset");
        var missionUi=System.IO.File.ReadAllText("Assets/Scripts/UI/MissionMapUI.cs");
        var launcher=System.IO.File.ReadAllText("Test-Expeditions.cmd");
        Test(polygon.Contains("Проверка экспедиций (склеп)") &&
             polygon.Contains("SceneManager.LoadScene(\"Crypt_Test\")") &&
             polygon.Contains("-expedition-test-map") &&
             launcher.Contains("-expedition-test-map"),
             "mechanics polygon and dedicated launcher open the actual expedition QA scene");
        Test(buildScenes.Contains("enabled: 1\\n    path: Assets/Scenes/Crypt_Test.unity".Replace("\\n", "\n")) &&
             missionUi.Contains("PersonalExpedition.Enter(_chosen") &&
             missionUi.Contains("ExpeditionReplayViewer.Open(replay)") &&
             missionUi.Contains("MissionBoard.Journeys"),
             "the QA build includes real personal and automatic missions plus recorded replay UI");
        // Execute the actual production console grammar rather than a copy.
        Test(Sinbinder.Dev.ConsoleCommandSyntax.Normalize("  EXPEDITION   SALT ~ ") ==
                 "expedition salt" &&
             Sinbinder.Dev.ConsoleCommandSyntax.Normalize("REPLAY\\tLATEST".Replace("\\t", "\t")) ==
                 "replay latest" &&
             Sinbinder.Dev.ConsoleCommandSyntax.Normalize("ЁMISSIONS`") == "missions",
             "English commands parse deterministically under Russian or US layout");
        Test(Sinbinder.Dev.ConsoleCommandSyntax.IsToggleCharacter('ё') &&
             Sinbinder.Dev.ConsoleCommandSyntax.IsToggleCharacter('Ё') &&
             Sinbinder.Dev.ConsoleCommandSyntax.IsToggleCharacter('~') &&
             Sinbinder.Dev.ConsoleCommandSyntax.IsToggleCharacter('`') &&
             !Sinbinder.Dev.ConsoleCommandSyntax.IsToggleCharacter('e'),
             "the physical tilde/backtick and Russian Ё open the same console");
        var consoleSource=System.IO.File.ReadAllText("Assets/Scripts/Dev/CheatConsole.cs");
        var testChamberSource=System.IO.File.ReadAllText("Assets/Scripts/Crypt/TestChamber.cs");
        var personalSource=System.IO.File.ReadAllText("Assets/Scripts/Gameplay/PersonalExpedition.cs");
        var testScene=System.IO.File.ReadAllText("Assets/Scenes/Crypt_Test.unity");
        string[] requiredCommands={
            "[\"chamber\"]", "[\"chamber reset\"]", "[\"chamber next\"]",
            "[\"chamber status\"]", "[\"missions\"]", "[\"expeditions\"]",
            "[\"expedition salt\"]", "[\"expedition chapel\"]",
            "[\"replay latest\"]", "[\"help\"]"
        };
        Test(requiredCommands.All(consoleSource.Contains) &&
             consoleSource.Contains("ConsoleCommandSyntax.Normalize(typed)") &&
             consoleSource.Contains("ConsoleCommandSyntax.IsToggleCharacter(key)"),
             "existing tilde console routes English TestChamber/expedition commands");
        Test(testChamberSource.Contains("public bool OpenExpeditions(out string error)") &&
             testChamberSource.Contains("SquadRoster.Remember(MissionBoard.Squad())") &&
             testChamberSource.Contains("map.Open();") &&
             testChamberSource.Contains("MissionCatalog.All()") &&
             testChamberSource.Contains("PersonalExpedition.Enter(") &&
             !testChamberSource.Contains("AutoBattleResolver.Resolve("),
             "TestChamber uses actual mission roster, commander UI and personal maps");
        Test(testScene.Contains("guid: 1598b704e9e9c03438f66565d155b3ca") &&
             testScene.Contains("guid: 8428c55a7d80023449a5462753d8a6d4") &&
             testScene.Contains("guid: 04efb9ffcd7da614983f3a951e75cfe4") &&
             buildScenes.Contains("path: Assets/Scenes/Crypt_Test.unity"),
             "test scene really hosts TestChamber, MissionBoard and MissionMapUI");
        Test(personalSource.Contains("var board = CryptMap.Board ??") &&
             personalSource.Contains("board?.RecordPersonal(_mission,") &&
             viewSource.Contains("_open._ownsPause = true;") &&
             viewSource.Contains("pause.Stamp == _pauseStamp"),
             "personal outcomes are reported in test scene and console replays pause safely");
        Console.WriteLine($"PASS: {tests} full expedition trace and archive tests");
    }
}
