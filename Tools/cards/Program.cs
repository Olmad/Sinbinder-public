using System;
using System.Collections.Generic;
using System.Linq;
using Sinbinder.Core;
using Sinbinder.Gameplay;

static class Program
{
    private static int count;
    private static void Check(bool ok, string why)
    {
        if (!ok) throw new Exception("FAIL " + why);
        count++;
    }
    private static CampCardRound Deal(int seed, params (string Name, SinType Sin, bool Human)[] players)
        => new CampCardRound(players, seed);
    private sealed class Wallet : ICampCardWallet
    {
        public int Balance { get; private set; }
        public bool Refuse;
        public Wallet(int value) { Balance = value; }
        public bool Spend(int value)
        {
            if (Refuse || value <= 0 || value > Balance) return false;
            Balance -= value;
            return true;
        }
        public void Credit(int value) { Balance = checked(Balance + value); }
    }
    private static List<ICampCardWallet> W(params Wallet[] wallets)
        => wallets.Cast<ICampCardWallet>().ToList();

    private static void TestStakes()
    {
        var a = new Wallet(9); var b = new Wallet(6);
        Check(CampCardPot.TryOpen(W(a, b), 5, out var pot), "two wallets pay the stake");
        Check(a.Balance == 4 && b.Balance == 1 && pot.Total == 10, "exact escrow amount");
        pot.Settle(new[] { 1 });
        Check(a.Balance == 4 && b.Balance == 11, "winner receives entire pot");
        pot.Settle(new[] { 0 }); pot.Refund();
        Check(a.Balance == 4 && b.Balance == 11, "cannot pay again or refund paid-out pot");
        Check(!CampCardPot.TryOpen(W(a, b), 5, out _), "underfunded wallet rejects stake");
        Check(a.Balance == 4 && b.Balance == 11, "underfunded stake has no side effects");

        a = new Wallet(10); b = new Wallet(10);
        Check(CampCardPot.TryOpen(W(a, b), 5, out pot), "tie setup");
        pot.Settle(new[] { 0, 1 });
        Check(a.Balance == 10 && b.Balance == 10, "tie splits pot exactly");

        var c = new Wallet(10);
        a = new Wallet(10); b = new Wallet(10);
        Check(CampCardPot.TryOpen(W(a, b, c), 1, out pot), "three player pot");
        pot.Settle(new[] { 0, 2 });
        Check(a.Balance == 11 && b.Balance == 9 && c.Balance == 10,
              "odd pot remainder distributed and coins conserved");

        a = new Wallet(9); b = new Wallet(9);
        Check(CampCardPot.TryOpen(W(a, b), 5, out pot), "no-winner setup");
        pot.Settle(Array.Empty<int>());
        Check(a.Balance == 9 && b.Balance == 9, "both bust refunds stakes");

        a = new Wallet(15); b = new Wallet(20);
        Check(CampCardPot.TryOpen(W(a, b), 10, out pot), "cancellation setup");
        pot.Refund(); pot.Refund();
        Check(a.Balance == 15 && b.Balance == 20, "abort refunds exactly once");

        a = new Wallet(10); b = new Wallet(10) { Refuse = true };
        Check(!CampCardPot.TryOpen(W(a, b), 3, out _), "concurrent spend failure rolls back");
        Check(a.Balance == 10 && b.Balance == 10, "no half-open pot");

        Check(!CampCardPot.TryOpen(W(a, a), 1, out _), "same wallet twice forbidden");
        Check(!CampCardPot.TryOpen(W(a), 1, out _), "no stake for solo practice");
        Check(!CampCardPot.TryOpen(W(a, b), 0, out _), "zero stake uses free play");
        Check(!CampCardPot.TryOpen(W(a, b), -1, out _), "negative stake forbidden");
        Check(!CampCardPot.TryOpen(W(a, b), 11, out _), "upper stake limit");
        Check(!CampCardPot.TryOpen(W(new Wallet(int.MaxValue), new Wallet(20)), 1, out _),
              "near-overflow wallet cannot wager");

        a = new Wallet(10); b = new Wallet(10);
        Check(CampCardPot.TryOpen(W(a, b), 1, out pot), "invalid settlement setup");
        bool invalid = false;
        try { pot.Settle(new[] { 3 }); }
        catch (ArgumentException) { invalid = true; }
        Check(invalid && !pot.Closed, "invalid winner cannot close or pay pot");
        pot.Refund();
        Check(a.Balance == 10 && b.Balance == 10, "after invalid settlement refunds safely");
    }

    public static void Main()
    {
        for (int suit = 0; suit < 4; suit++)
        {
            Check(new CampCard(6, suit).Points == 6, "6 points");
            Check(new CampCard(10, suit).Points == 10, "10 points");
            Check(new CampCard(11, suit).Points == 2, "Jack 2");
            Check(new CampCard(12, suit).Points == 3, "Queen 3");
            Check(new CampCard(13, suit).Points == 4, "King 4");
            Check(new CampCard(14, suit).Points == 11, "Ace 11");
        }
        Check(CampCardRound.WantsCard(SinType.Wrath, 17), "Wrath takes risks");
        Check(!CampCardRound.WantsCard(SinType.Sloth, 17), "Sloth stands early");
        Check(CampCardRound.WantsCard(SinType.Sloth, 15, 1), "losing increases risk");
        var g = Deal(19, ("Hero", SinType.Pride, true), ("Enemy", SinType.Greed, false));
        Check(g.Hands.Count == 2 && g.CardsDrawn == 2, "a real opening deal");
        Check(g.Current.Human, "hero actually controls his turn");
        Check(g.Play(false) && g.Turn == 1, "standing passes turn to opponent");
        while (!g.Finished) g.Play(CampCardRound.WantsCard(g.Current.Sin, g.Current.Score));
        Check(g.Finished && g.Winners().Count <= 2, "round ends with coherent winners");
        for (int seed = 0; seed < 200; seed++)
        {
            g = Deal(seed, ("A", SinType.Greed, false), ("B", SinType.Pride, false),
                          ("C", SinType.Sloth, false), ("D", SinType.Envy, true));
            while (!g.Finished)
                g.Play(CampCardRound.WantsCard(g.Current.Sin, g.Current.Score));
            var cards = g.Hands.SelectMany(h => h.Cards).Select(c => c.Face).ToArray();
            Check(cards.Length == cards.Distinct().Count(), "no duplicates in 36-card deck");
            Check(g.Winners().All(i => g.Hands[i].Score <= 21), "no bust winner");
        }
        TestStakes();
        Console.WriteLine($"PASS: {count} real card and escrow assertions");
    }
}
