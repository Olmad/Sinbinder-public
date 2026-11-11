using System;
using System.Collections.Generic;
using System.Linq;
using Sinbinder.Core;
using Sinbinder.Gameplay;

static class Program
{
    private static int count;
    private static void Check(bool pass, string why)
    {
        if (!pass) throw new Exception("FAIL: " + why);
        count++;
    }

    private static CampCardRound Game(int seed, params (string Name, SinType Sin, bool Human)[] players)
        => new CampCardRound(players, seed);

    public static void Main()
    {
        // Authentic card points, 36 distinct physical cards.
        for (int suit = 0; suit < 4; suit++)
        {
            Check(new CampCard(6, suit).Points == 6, "6=6");
            Check(new CampCard(10, suit).Points == 10, "10=10");
            Check(new CampCard(11, suit).Points == 2, "jack=2");
            Check(new CampCard(12, suit).Points == 3, "queen=3");
            Check(new CampCard(13, suit).Points == 4, "king=4");
            Check(new CampCard(14, suit).Points == 11, "ace=11");
        }

        Check(!CampCardRound.WantsCard(SinType.Sloth, 16), "cautious Sloth stands at 16");
        Check(CampCardRound.WantsCard(SinType.Wrath, 16), "risk-taking Wrath hits at 16");
        Check(!CampCardRound.WantsCard(SinType.Wrath, 21), "nobody hits at 21");
        Check(CampCardRound.WantsCard(SinType.Greed, 17), "Greed chases higher score");
        Check(!CampCardRound.WantsCard(SinType.Sloth, 15), "Sloth normally stands at 15");
        Check(CampCardRound.WantsCard(SinType.Sloth, 15, 1),
            "remembered loss may encourage one more draw");
        Check(!CampCardRound.WantsCard(SinType.Sloth, 14, -1),
            "remembered win may encourage cautious standing");
        Check(CampCardRound.WantsCard(SinType.Sloth, 15, 200) ==
              CampCardRound.WantsCard(SinType.Sloth, 15, 2),
            "memory bias clamped to avoid fake impossible bravery");

        var p = Game(120, ("Игрок", SinType.Pride, true), ("Вейн", SinType.Sloth, false));
        var p2 = Game(120, ("Игрок", SinType.Pride, true), ("Вейн", SinType.Sloth, false));
        Check(p.Hands.Count == 2 && !p.Finished, "two players and a live round");
        Check(p.Hands[0].Cards.Count == 1 && p.Hands[1].Cards.Count == 1,
            "real initial deal of one card per hand");
        Check(p.Hands[0].Cards[0].Face == p2.Hands[0].Cards[0].Face, "same seed same shuffled first card");
        Check(p.Hands[1].Cards[0].Face == p2.Hands[1].Cards[0].Face, "seeded deal stable for second player");
        Check(p.Turn == 0 && p.Current.Human, "human can act");
        Check(p.Play(false) && p.Turn == 1 && p.Hands[0].Standing, "standing moves to next player");
        Check(!p.Play(true) == false || p.CardsDrawn >= 2, "NPC can take card");
        while (!p.Finished) p.Play(false);
        Check(p.Winners().Count <= 2, "winner count bounded");
        Check(!p.Play(true), "finished rounds reject moves");

        // Cards are physical objects: every draw comes from one non-repeating deck.
        for (int seed = 0; seed < 90; seed++)
        {
            var round = Game(seed,
                ("1", SinType.Greed, false), ("2", SinType.Pride, false),
                ("3", SinType.Wrath, false), ("4", SinType.Sloth, true));
            while (!round.Finished)
            {
                if (!round.Play(true)) throw new Exception("round did not progress");
            }
            var all = round.Hands.SelectMany(h => h.Cards).Select(c => c.Face).ToArray();
            Check(all.Distinct().Count() == all.Length, "cards never duplicate within a round");
            Check(all.Length <= 36, "deck capacity never exceeded");
            Check(round.Winners().All(i => round.Hands[i].Score <= 21), "bust hands cannot win");
        }

        var solo = Game(7, ("Греховод", SinType.Pride, true));
        Check(solo.Hands.Count == 1, "practice can be played without seated NPC");
        while (!solo.Finished) solo.Play(false);
        Check(solo.Winners().Count == 0 || solo.Hands[0].Score >= 17,
            "practice is won only at 17-21, not merely for showing up");

        var tieExamples = 0;
        var differentSeed = 0;
        for (int seed = 1; seed < 400; seed++)
        {
            var g = Game(seed, ("A", SinType.Greed, true), ("B", SinType.Sloth, false));
            if (g.Hands[0].Cards[0].Face != p2.Hands[0].Cards[0].Face) differentSeed++;
            while (!g.Finished) g.Play(CampCardRound.WantsCard(g.Current.Sin, g.Current.Score));
            if (g.Winners().Count > 1) tieExamples++;
            Check(g.Finished && g.Turn == g.Hands.Count, "finite game, all turns consumed");
        }
        Check(differentSeed > 300, "different seeds genuinely change deal");
        Check(tieExamples > 0, "ties are possible, not silently assigned to first player");
        Console.WriteLine($"PASS: {count} production card game assertions");
    }
}
