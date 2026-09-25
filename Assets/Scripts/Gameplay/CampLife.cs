// Assets/Scripts/Gameplay/CampLife.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.AOS.Modules;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Жизнь в лагере, шаг первый (docs/32-CAMP.md): воин, которому нечего
    /// делать, стоит не где попало, а там, куда тянет его душа. Жадный —
    /// у сундука, гордый — в стороне, гневный — в дозоре, унылый —
    /// в палатках, верный — рядом с Греховодом.
    ///
    /// Место не приказ и не отдельная таблица: его выбирают те же модули,
    /// что решают в бою (<see cref="ICampModule"/>), одним кодом с стендом
    /// (<see cref="CampChoice"/>). Голосование решает «стоять», а здесь —
    /// где именно, так же как <see cref="HunterGoal"/> решает, куда идти
    /// охотнику без врага рядом.
    ///
    /// Две работы в игре: грех видно до совета — выбор старшего становится
    /// решением; и первый отказ Марги случается у его сундука.
    ///
    /// По часам, без жребия: раз в полминуты воин пересматривает место,
    /// у каждого свой сдвиг — не встают разом. Место приедается
    /// (<see cref="CampChoice.Tired"/>): жадный постоит у сундука и пойдёт
    /// погреться, гордый — от края к столу, где решают, и обратно; унылый
    /// из палатки выходит редко — ему много не надо. Во время сцен пролога
    /// (провожатый, тревога шара, разгром) лагерь не живёт: сцена ведёт.
    ///
    /// Выключатель: до вечернего прогона выключено; «лагерь» в консоли (~).
    /// </summary>
    public static class CampLife
    {
        public static bool Enabled { get; set; }

        private const float Every = 30f;

        private static readonly List<ICampModule> Voices = new()
        {
            new GreedModule(), new PrideModule(), new WrathModule(), new EnvyModule(),
            new LustModule(), new GluttonyModule(), new SlothModule(), new LoyaltyModule(),
        };

        /// <summary>
        /// Где стоит и с каких пор; откуда ушёл и когда. Из этого — ритм
        /// (<see cref="CampChoice.Tired"/>): место приедается.
        /// </summary>
        private struct Choice
        {
            public CampSpot Spot;
            public int Epoch;
            public float Since;
            public CampSpot? Left;
            public float LeftAt;
        }
        private static readonly Dictionary<Warrior, Choice> Chosen = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            Enabled = false;
            Chosen.Clear();
        }

        /// <summary>
        /// Куда идти воину, выбравшему «стоять». Ложь — лагерь не живёт
        /// (не лагерь, сцена пролога, приказ) или места нет, и тогда он
        /// стоит, где стоял.
        /// </summary>
        public static bool Where(Warrior w, out Vector3 point, out string word)
        {
            point = default;
            word = "";

            if (!Alive(w)) return false;

            var spot = Pick(w);
            if (!Place(spot, out point)) return false;

            // Кольцо вокруг места, у каждого своё: не толпятся в точке.
            // У огня кольцо шире — в костёр не встают.
            float angle = Stable(w.DisplayName) % 12 * 30f * Mathf.Deg2Rad;
            point += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Ring(spot);
            word = Word(spot);
            return true;
        }

        /// <summary>Живёт ли он сейчас лагерем: без приказа, вне сцен пролога.</summary>
        public static bool Idle(Warrior w) => Alive(w);

        /// <summary>Место, которое он выбрал сам. Ложь — не выбирал или лагерь не живёт.</summary>
        public static bool SpotOf(Warrior w, out CampSpot spot)
        {
            spot = CampSpot.Fire;
            if (!Alive(w) || !Chosen.TryGetValue(w, out var c)) return false;
            spot = c.Spot;
            return true;
        }

        /// <summary>Словом — где воин сейчас по своей воле. Пусто — нигде.</summary>
        public static string Now(Warrior w)
            => Alive(w) && Chosen.TryGetValue(w, out var c) ? Word(c.Spot) : "";

        private static float Ring(CampSpot spot)
        {
            switch (spot)
            {
                case CampSpot.Fire:  return 2.6f;
                case CampSpot.Table: return 1.8f;
                case CampSpot.Chest: return 1.4f;
                default:             return 1.2f;
            }
        }

        public static string Word(CampSpot spot)
        {
            switch (spot)
            {
                case CampSpot.Chest:     return "у сундука";
                case CampSpot.Table:     return "у стола, где решают";
                case CampSpot.Watch:     return "в дозоре";
                case CampSpot.Apart:     return "сторонится";
                case CampSpot.Tents:     return "дремлет в палатке";
                case CampSpot.Sinbinder: return "держится рядом с Греховодом";
                default:                 return "греется у огня";
            }
        }

        private static bool Alive(Warrior w)
        {
            if (!Enabled || w == null || w.IsDead) return false;
            if (w.Team != Team.Player || w is SinbinderPlayer || w.HasCommand) return false;

            // Лагерь — там, где идёт его открытие; сцена ведёт, пока она идёт.
            if (Object.FindFirstObjectByType<CampOpening>() == null) return false;
            if (!CampOpening.EscortArrived || CrystalBall.Leading || RaidEvent.Running) return false;
            return true;
        }

        private static CampSpot Pick(Warrior w)
        {
            int epoch = Mathf.FloorToInt((Time.time + Stable(w.DisplayName) % 30) / Every);
            if (Chosen.TryGetValue(w, out var c) && c.Epoch == epoch) return c.Spot;

            // Первый выбор — без ритма: пришёл в лагерь, встал к своему.
            if (!Chosen.TryGetValue(w, out var was))
            {
                var first = CampChoice.Choose(Voices, Soul.FromWarrior(w));
                Chosen[w] = new Choice { Spot = first, Epoch = epoch, Since = Time.time };
                return first;
            }

            var soul = Soul.FromWarrior(w);
            float here = (Time.time - was.Since) / 60f;
            float gone = (Time.time - was.LeftAt) / 60f;
            var spot = CampChoice.Next(Voices, soul, was.Spot, here, was.Left, gone);

            Chosen[w] = spot == was.Spot
                ? new Choice { Spot = spot, Epoch = epoch, Since = was.Since, Left = was.Left, LeftAt = was.LeftAt }
                : new Choice { Spot = spot, Epoch = epoch, Since = Time.time, Left = was.Spot, LeftAt = Time.time };
            return spot;
        }

        /// <summary>
        /// Где место в этой сцене. Опоры — то, что в лагере стоит всегда:
        /// костёр, сундук, стол с шаром, Греховод; дозор, край света
        /// и палатки — от костра, как их ставит сборщик (холм на юге,
        /// охотники с севера).
        /// </summary>
        private static bool Place(CampSpot spot, out Vector3 point)
        {
            point = default;
            var fire = Object.FindFirstObjectByType<CampOpening>();
            if (fire == null) return false;
            var c = fire.transform.position;

            switch (spot)
            {
                case CampSpot.Chest:
                    var chest = Object.FindFirstObjectByType<TrophyChest>();
                    if (chest == null) return false;
                    point = chest.transform.position;
                    return true;

                case CampSpot.Table:
                    var ball = Object.FindFirstObjectByType<CrystalBall>();
                    if (ball == null) return false;
                    point = ball.transform.position;
                    return true;

                case CampSpot.Sinbinder:
                    if (!SinbinderPlayer.Exists) return false;
                    point = SinbinderPlayer.Where - SinbinderPlayer.Instance.transform.forward * 2f;
                    return true;

                case CampSpot.Watch: point = c + new Vector3(0f, 0f, 9f); return true;
                case CampSpot.Apart: point = c + new Vector3(-7f, 0f, 1f); return true;
                case CampSpot.Tents: point = c + new Vector3(0f, 0f, -9.5f); return true;

                default: point = c; return true;
            }
        }

        /// <summary>Число из имени — одно и то же в каждом запуске, в отличие от GetHashCode.</summary>
        private static int Stable(string s)
        {
            int h = 0;
            if (s != null) foreach (char ch in s) h = (h * 31 + ch) & 0x7fffffff;
            return h;
        }
    }
}
