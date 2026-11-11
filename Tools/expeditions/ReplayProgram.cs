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
                          "Настоящее действие "+i,damage:i%3==0?5:0);
        replay.Append(ExpeditionReplayKind.Loot,"Марга","Собрали монеты.");
        replay.Append(ExpeditionReplayKind.Return,"Марга","Вернулись.");
        Test(replay.Squad.Count==2,"squad deduplicated, no phantom people");
        Test(replay.HasCompleteRoute,"full route has departure and return");
        Test(replay.Frames.Count==130,"all 125 combat events preserved, not just three");
        Test(replay.Frames.Select(x=>x.Sequence).SequenceEqual(Enumerable.Range(0,130)),
             "every event retains original chronology");
        Test(replay.AllEvents().Count()==130,"full trace accessible");
        Test(replay.Frames[126].Kind==ExpeditionReplayKind.CombatAction,"last attack retained");
        Test(replay.Frames.All(x=>x.Description.Length>0),"every authored step describable");
        var incomplete=new ExpeditionReplayData("Часовня","",null);
        incomplete.Append(ExpeditionReplayKind.CombatAction,"?","No route");
        Test(!incomplete.HasCompleteRoute,"unrealistic partial trace is not claimed complete");
        Console.WriteLine($"PASS: {tests} full expedition trace tests");
    }
}
