// Pure rules for the camp's 36-card game of "очко" (21).
// This file is compiled unchanged by Unity and Tools/cards .NET smoke tests.
using System;
using System.Collections.Generic;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public readonly struct CampCard
    {
        public int Rank { get; } // 6,7,8,9,10,J=11,Q=12,K=13,A=14
        public int Suit { get; }
        public CampCard(int rank, int suit)
        {
            if (rank < 6 || rank > 14 || suit < 0 || suit > 3)
                throw new ArgumentOutOfRangeException(nameof(rank));
            Rank = rank;
            Suit = suit;
        }

        // Russian "очко": J=2, Q=3, K=4, A=11; pip cards their own value.
        public int Points => Rank == 11 ? 2 : Rank == 12 ? 3 :
                             Rank == 13 ? 4 : Rank == 14 ? 11 : Rank;
        public string Face => (Rank == 11 ? "В" : Rank == 12 ? "Д" :
                               Rank == 13 ? "К" : Rank == 14 ? "Т" :
                               Rank.ToString()) + " " + new[] { "♠", "♥", "♦", "♣" }[Suit];
    }

    public sealed class CampCardHand
    {
        public string Name { get; }
        public SinType Sin { get; }
        public bool Human { get; }
        public List<CampCard> Cards { get; } = new List<CampCard>();
        public bool Standing { get; internal set; }
        public int Score
        {
            get
            {
                int total = 0;
                foreach (var card in Cards) total += card.Points;
                return total;
            }
        }
        public bool Bust => Score > 21;

        internal CampCardHand(string name, SinType sin, bool human)
        {
            Name = name;
            Sin = sin;
            Human = human;
        }
    }

    /// <summary>
    /// Real coins are wagered via CampCardPot; this component only owns
    /// the shuffled deck, score and personality-driven card choices.
    /// A single participant may practise alone; multiplayer rounds use one
    /// shuffled non-repeating 36-card deck and honest maximum <= 21.
    /// </summary>
    public sealed class CampCardRound
    {
        private readonly List<CampCardHand> _hands = new List<CampCardHand>();
        private readonly List<CampCard> _deck = new List<CampCard>(36);
        private int _drawn;
        public IReadOnlyList<CampCardHand> Hands => _hands;
        public int Turn { get; private set; }
        public int CardsDrawn => _drawn;
        public bool Finished => Turn >= _hands.Count;
        public CampCardHand Current => Finished ? null : _hands[Turn];

        public CampCardRound(IEnumerable<(string Name, SinType Sin, bool Human)> seats, int seed)
        {
            foreach (var seat in seats)
            {
                if (_hands.Count == 4) break;
                if (string.IsNullOrWhiteSpace(seat.Name))
                    throw new ArgumentException("Every card player must have a name.");
                _hands.Add(new CampCardHand(seat.Name, seat.Sin, seat.Human));
            }
            if (_hands.Count == 0) throw new ArgumentException("No card participants.");

            for (int suit = 0; suit < 4; suit++)
                for (int rank = 6; rank <= 14; rank++)
                    _deck.Add(new CampCard(rank, suit));

            var rng = new Random(seed);
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = _deck[i];
                _deck[i] = _deck[j];
                _deck[j] = tmp;
            }
            // A real first deal. No NPC starts a round with imaginary cards.
            foreach (var hand in _hands) hand.Cards.Add(_deck[_drawn++]);
        }

        public bool Play(bool draw)
        {
            if (Finished) return false;
            var hand = _hands[Turn];
            if (draw && _drawn < _deck.Count)
            {
                hand.Cards.Add(_deck[_drawn++]);
                if (hand.Score < 21 && _drawn < _deck.Count) return true;
            }
            hand.Standing = true;
            Turn++;
            return true;
        }

        public IReadOnlyList<int> Winners()
        {
            var result = new List<int>();
            if (!Finished) return result;
            int best = _hands.Count == 1 ? 17 : 1;
            for (int i = 0; i < _hands.Count; i++)
            {
                int score = _hands[i].Score;
                if (score > 21 || score < best) continue;
                if (score > best) { best = score; result.Clear(); }
                result.Add(i);
            }
            return result;
        }

        /// <summary>
        /// Soul shapes *risk tolerance* but does not predetermine card draws.
        /// These are a character's choices, not an AOS obedience verdict.
        /// </summary>
        /// <summary>
        /// A soul sets its own maximum stake. It may decline even when
        /// it can afford the coins; bankruptcy is never forced by an order.
        /// </summary>
        public static int MaxStake(SinType sin, int balance)
        {
            if (balance < 2) return 0;
            int appetite = sin switch
            {
                SinType.Greed => 10, SinType.Wrath => 10,
                SinType.Pride => 5, SinType.Envy => 5,
                SinType.Gluttony => 2, SinType.Lust => 2,
                SinType.Sloth => 1, _ => 1
            };
            return Math.Min(appetite, balance / 2);
        }

        public static bool WantsCard(SinType sin, int score, int memoryBias = 0)
        {
            int target = sin switch
            {
                SinType.Greed => 19,
                SinType.Pride => 19,
                SinType.Wrath => 20,
                SinType.Envy => 18,
                SinType.Lust => 17,
                SinType.Gluttony => 18,
                SinType.Sloth => 15,
                _ => 17
            };
            // A remembered win/loss may nudge risk by at most two points.
            // AOS memory cannot manufacture cards or force obedience.
            return score < target + Math.Max(-2, Math.Min(2, memoryBias));
        }
    }
}
