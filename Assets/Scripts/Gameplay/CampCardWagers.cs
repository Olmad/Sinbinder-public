// Pure, production-linked escrow for camp card wagers. No Unity dependency.
using System;
using System.Collections.Generic;

namespace Sinbinder.Gameplay
{
    public interface ICampCardWallet
    {
        int Balance { get; }
        bool Spend(int amount);
        void Credit(int amount);
    }

    /// <summary>
    /// Every stake is debited before play and is paid out exactly once.
    /// No winner or interrupted rounds refund every participant.
    /// </summary>
    public sealed class CampCardPot
    {
        private readonly List<ICampCardWallet> _wallets;
        public int Stake { get; }
        public int Total => checked(Stake * _wallets.Count);
        public bool Closed { get; private set; }

        private CampCardPot(List<ICampCardWallet> wallets, int stake)
        {
            _wallets = wallets;
            Stake = stake;
        }

        public static bool TryOpen(IReadOnlyList<ICampCardWallet> wallets, int stake,
                                   out CampCardPot pot)
        {
            pot = null;
            if (wallets == null || wallets.Count < 2 || wallets.Count > 4 ||
                stake < 1 || stake > 10) return false;

            // Enough for this stake and room for the largest possible payout.
            // This prevents wallet overflow at int.MaxValue.
            int maximumPayout = checked(wallets.Count * stake);
            var copy = new List<ICampCardWallet>(wallets.Count);
            var distinct = new HashSet<ICampCardWallet>();
            foreach (var wallet in wallets)
            {
                if (wallet == null || !distinct.Add(wallet) ||
                    wallet.Balance < stake ||
                    wallet.Balance > int.MaxValue - maximumPayout) return false;
                copy.Add(wallet);
            }

            int paid = 0;
            foreach (var wallet in copy)
            {
                if (!wallet.Spend(stake))
                {
                    // An unexpected concurrent debit must never steal coins.
                    for (int j = 0; j < paid; j++) copy[j].Credit(stake);
                    return false;
                }
                paid++;
            }
            pot = new CampCardPot(copy, stake);
            return true;
        }

        public void Settle(IReadOnlyList<int> winners)
        {
            if (Closed) return;
            if (winners == null) throw new ArgumentNullException(nameof(winners));
            var unique = new HashSet<int>();
            foreach (int i in winners)
                if (i < 0 || i >= _wallets.Count || !unique.Add(i))
                    throw new ArgumentException("Invalid or duplicate winner.");

            if (unique.Count == 0)
            {
                Refund();
                return;
            }
            int share = Total / unique.Count;
            int remainder = Total % unique.Count;
            // Deterministic, fair coin allocation: the first remaining
            // winners in table order receive a single extra coin.
            for (int i = 0; i < _wallets.Count; i++)
                if (unique.Contains(i))
                    _wallets[i].Credit(share + (remainder-- > 0 ? 1 : 0));

            Closed = true;
        }

        public void Refund()
        {
            if (Closed) return;
            foreach (var wallet in _wallets) wallet.Credit(Stake);
            Closed = true;
        }
    }
}
