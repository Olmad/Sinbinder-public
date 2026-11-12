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
            hasOutcome:true,resolved:true,targetKilled:true,targetHealthAfter:0f);
        observed.Append(ExpeditionReplayKind.Return,"Командир","Вернулся");
        ExpeditionReplayArchive.Add(observed);
        var observedCopy=ExpeditionReplayArchive.Snapshot().Last().Frames[1];
        Test(observedCopy.TargetName=="Чужой 2" && observedCopy.Target=="runtime-guid",
            "real target label and original runtime ID both retained");
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
        Console.WriteLine($"PASS: {tests} full expedition trace and archive tests");
    }
}
