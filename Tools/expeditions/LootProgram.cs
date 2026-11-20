using System;
using System.Linq;
using Sinbinder.Gameplay;
static class Program
{
    static int count;
    static void Check(bool ok,string msg) {if(!ok)throw new Exception("FAIL "+msg);count++;}
    static void Main()
    {
        var squad=new[]{
            new ExpeditionLootPerson("Жадный",ExpeditionGreed.Greed,20f),
            new ExpeditionLootPerson("Завистливый",ExpeditionGreed.Envy,30f),
            new ExpeditionLootPerson("Верный",ExpeditionGreed.Pride,90f),
            new ExpeditionLootPerson("Ленивый",ExpeditionGreed.Other,20f)};
        for(int amount=0;amount<300;amount++)
        {
            var x=ExpeditionAutoLoot.Allocate(amount,squad);
            Check(x.ReturnedCoins>=0,"communal share never negative");
            Check(x.PersonalCoins.Sum(p=>p.Coins)+x.ReturnedCoins==amount,
                "coins conserved, no minted loot");
            Check(x.PersonalCoins.All(p=>p.Coins>0),"no zero/negative personal shares");
            Check(x.PersonalCoins.All(p=>p.Name!="Верный"&&p.Name!="Ленивый"),
                "loyal and indifferent warriors do not siphon loot");
        }
        Check(ExpeditionAutoLoot.Allocate(2,squad).PersonalCoins.Count==0,
            "trivial loot not worth theft");
        var wealth=ExpeditionAutoLoot.Allocate(55,squad);
        Check(wealth.PersonalCoins.Any(x=>x.Name=="Жадный"),"Greed claims silver");
        Check(wealth.PersonalCoins.Any(x=>x.Name=="Завистливый"),"Envy may claim valuable silver");
        Check(ExpeditionAutoLoot.Allocate(55,null).ReturnedCoins==55,
            "no survivors means no arbitrary pocket credit");
        Console.WriteLine($"PASS {count} AOS automatic loot decisions");
    }
}
