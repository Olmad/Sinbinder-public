// Automatic squads choose what is worth carrying and may secretly keep
// ordinary coins. No souls or hidden secrets are awarded by this process.
using System;
using System.Collections.Generic;

namespace Sinbinder.Gameplay
{
    public enum ExpeditionGreed { Greed, Pride, Envy, Other }

    public readonly struct ExpeditionLootPerson
    {
        public readonly string Name;
        public readonly ExpeditionGreed Drive;
        public readonly float Loyalty;
        public ExpeditionLootPerson(string name, ExpeditionGreed drive,float loyalty)
        { Name=name;Drive=drive;Loyalty=loyalty; }
    }

    public readonly struct ExpeditionLootShare
    {
        public readonly string Name;
        public readonly int Coins;
        public ExpeditionLootShare(string name,int coins) { Name=name;Coins=coins; }
    }

    public sealed class ExpeditionLootOutcome
    {
        public int ReturnedCoins;
        public readonly List<ExpeditionLootShare> PersonalCoins=new();
    }

    public static class ExpeditionAutoLoot
    {
        public static ExpeditionLootOutcome Allocate(int total,
                  IReadOnlyList<ExpeditionLootPerson> survivors)
        {
            if(total<0)throw new ArgumentOutOfRangeException(nameof(total));
            var result=new ExpeditionLootOutcome {ReturnedCoins=total};
            if(survivors==null || survivors.Count==0)return result;

            // Only important loot tempts somebody into keeping a portion.
            // A devoted warrior does not steal from the Lord's share.
            // No fixed gold is minted: every personal share comes from
            // the exact coins that would have reached the communal purse.
            for(int i=0;i<survivors.Count;i++)
            {
                var person=survivors[i];
                if(string.IsNullOrWhiteSpace(person.Name) || person.Loyalty>=75f)continue;
                int want=0;
                switch(person.Drive)
                {
                    case ExpeditionGreed.Greed:
                        if(total>=3)want=Math.Min(10,Math.Max(1,total/5));
                        break;
                    case ExpeditionGreed.Envy:
                        if(total>=15 && person.Loyalty<60f)want=Math.Min(4,total/10);
                        break;
                    case ExpeditionGreed.Pride:
                        if(total>=30 && person.Loyalty<45f)want=Math.Min(2,total/20);
                        break;
                }
                int kept=Math.Min(result.ReturnedCoins,want);
                if(kept==0)continue;
                result.ReturnedCoins-=kept;
                result.PersonalCoins.Add(new ExpeditionLootShare(person.Name,kept));
            }
            return result;
        }
    }
}
