// Assets/Scripts/Gameplay/CampLife.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.AOS.Modules;

using Sinbinder.Core;
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
    /// Включено по умолчанию с 26 сентября: автор прошёл демо и просил
    /// живой лагерь, а выключенный лагерь просто стоит у костра. В консоли
    /// «лагерь» (~) по-прежнему переключает.
    /// </summary>
    public static class CampLife
    {
        public static bool Enabled { get; set; } = true;

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
            Enabled = true;
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
            if (!Place(spot, w, out point)) return false;

            // Шаг по месту. Автор, 26 сентября: «Живой лагерь всё ещё
            // не живой — все становятся по своим точкам интереса и стоят».
            // Место меняется раз в несколько минут, а в промежутке воин
            // стоял как вкопанный. Теперь раз в девять — пятнадцать секунд
            // он переходит на соседнюю точку своего же места: у огня и стола
            // обходит круг, в дозоре ходит вдоль края, в стороне топчется.
            // По часам и имени, без жребия: те же минуты — те же шаги.
            int seed = Stable(w.DisplayName);
            float beat = 9f + seed % 7;
            int step = Mathf.FloorToInt((Time.time + seed % 11) / beat) % 3 - 1;   // −1, 0, 1

            // Кольцо вокруг места, у каждого своё: не толпятся в точке.
            // У огня кольцо шире — в костёр не встают.
            float angle = (seed % 12 * 30f + step * Stroll(spot)) * Mathf.Deg2Rad;
            point += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Ring(spot);

            // Дозор — не кольцо, а ход вдоль края, туда и обратно.
            if (spot == CampSpot.Watch) point += new Vector3(step * 3f, 0f, 0f);

            word = Word(spot);
            return true;
        }

        /// <summary>На сколько градусов по кругу места уходит шаг.</summary>
        private static float Stroll(CampSpot spot)
        {
            switch (spot)
            {
                case CampSpot.Fire:  return 28f;
                case CampSpot.Table: return 35f;
                case CampSpot.Chest: return 45f;
                case CampSpot.Apart: return 60f;
                case CampSpot.Tents: return 25f;
                default:             return 0f;   // дозор ходит сам, у Греховода — не топчутся
            }
        }

        /// <summary>
        /// Куда смотреть, стоя на месте: на собеседника, пока говорят
        /// (<see cref="CampTalk.Partner"/>); иначе — на то, ради чего пришёл:
        /// на огонь, на стол, на сундук, в дозоре — наружу, откуда придут,
        /// в стороне — прочь от костра, у палатки — на огонь, рядом
        /// с Греховодом — на него. Ложь — лагерь не живёт.
        /// </summary>
        public static bool Facing(Warrior w, out Vector3 look)
        {
            look = default;
            if (!Alive(w)) return false;

            if (CampTalk.Partner(w, out var other))
            {
                look = other.transform.position;
                return true;
            }

            if (!Chosen.TryGetValue(w, out var c)) return false;

            var fire = Object.FindFirstObjectByType<CampOpening>();
            if (fire == null) return false;
            var centre = fire.transform.position;

            switch (c.Spot)
            {
                case CampSpot.Watch:
                    look = w.transform.position + new Vector3(0f, 0f, 20f);
                    return true;

                case CampSpot.Apart:
                    var away = w.transform.position - centre;
                    away.y = 0f;
                    look = w.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.left) * 10f;
                    return true;

                case CampSpot.Tents:
                    look = centre;
                    return true;

                case CampSpot.Sinbinder:
                    if (!SinbinderPlayer.Exists) return false;
                    look = SinbinderPlayer.Where;
                    return true;

                default:
                    return Place(c.Spot, w, out look);
            }
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
                case CampSpot.Chest:     return Loc.T("у сундука");
                case CampSpot.Table:     return Loc.T("у стола, где решают");
                case CampSpot.Watch:     return Loc.T("в дозоре");
                case CampSpot.Apart:     return Loc.T("сторонится");
                case CampSpot.Tents:     return Loc.T("дремлет в палатке");
                case CampSpot.Sinbinder: return Loc.T("держится рядом с Греховодом");
                default:                 return Loc.T("греется у огня");
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
        private static bool Place(CampSpot spot, Warrior w, out Vector3 point)
        {
            point = default;
            var fire = Object.FindFirstObjectByType<CampOpening>();
            if (fire == null) return false;
            var c = fire.transform.position;

            // У каждого своя палатка, а не одна точка на всех: до 26 сентября
            // «дремлющие в палатке» толпились в одном месте на склоне холма.
            if (spot == CampSpot.Tents && Tent(w, c, out point)) return true;

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

        /// <summary>
        /// Своя палатка воина — у входа, лицом к огню. Палатки ставит сборщик
        /// под «Палатки», брошенные — «Палатка павшего»: к павшим не ходят.
        /// Какая чья — по имени, без жребия.
        /// </summary>
        private static bool Tent(Warrior w, Vector3 fire, out Vector3 point)
        {
            point = default;

            if (_tents == null || _tentsScene != w.gameObject.scene.handle)
            {
                _tents = new List<Transform>();
                _tentsScene = w.gameObject.scene.handle;

                var camp = GameObject.Find("Палатки");
                if (camp != null)
                    foreach (Transform t in camp.transform)
                        if (t.name.StartsWith("Палатка ") && !t.name.Contains("павшего")) _tents.Add(t);
            }

            _tents.RemoveAll(t => t == null);
            if (_tents.Count == 0) return false;

            var tent = _tents[Stable(w.DisplayName) % _tents.Count];
            var toFire = fire - tent.position;
            toFire.y = 0f;
            point = tent.position + (toFire.sqrMagnitude > 0.01f ? toFire.normalized : Vector3.forward) * 1.4f;
            return true;
        }

        private static List<Transform> _tents;
        private static int _tentsScene = -1;

        /// <summary>Число из имени — одно и то же в каждом запуске, в отличие от GetHashCode.</summary>
        private static int Stable(string s)
        {
            int h = 0;
            if (s != null) foreach (char ch in s) h = (h * 31 + ch) & 0x7fffffff;
            return h;
        }
    }
}
