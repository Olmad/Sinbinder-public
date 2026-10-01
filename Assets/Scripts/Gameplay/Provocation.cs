// Assets/Scripts/Gameplay/Provocation.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Провокация: воин зовёт врагов на себя. Решение автора, 30 сентября:
    /// «у Каргана будет навык „провокация“, из-за которого враги не могут
    /// от него отойти и атакуют только его. Сопротивляемость навыку можно
    /// сделать через грехи».
    ///
    /// <b>Кто ведётся</b> — решают души врагов, а не этот класс:
    /// <see cref="AOS.DecisionContext.Provoked"/> читают модули, и
    /// <see cref="AOS.BehaviourResolver.TakesBait"/> складывает их голоса.
    /// Гневный и гордый бьют того, кто зовёт; жадный, завистливый
    /// и ленивый — не ведутся. Охотник и Инквизитор поддаются, Ловчий
    /// и Следопыт идут дальше за Греховодом.
    ///
    /// <b>Стойка.</b> Пока зовёт со щитом в руке — закрывается: защита
    /// выше на ту же меру, что у «Железной стойки» Терпения
    /// (<see cref="PatienceSkills"/>). Без неё зовущий падал за одну-две
    /// секунды, и «выиграю вам время» было бы неправдой (замер —
    /// docs/41-SHOWCASE.md, п. 11).
    ///
    /// Где зовут: телохранитель остаётся прикрывать отход
    /// (<see cref="EscapeZone"/>).
    /// </summary>
    public static class Provocation
    {
        /// <summary>Докуда слышно зов.</summary>
        public const float Radius = 8f;

        /// <summary>Прибавка к защите со щитом — та же, что у «Железной стойки».</summary>
        public const float Stance = 40f;

        private static readonly List<Warrior> Taunting = new();
        private static readonly HashSet<Warrior> Answered = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Clear();

        /// <summary>Начать звать на себя.</summary>
        public static void Begin(Warrior w)
        {
            if (w == null || w.IsDead || Taunting.Contains(w)) return;
            Taunting.Add(w);
        }

        /// <summary>Забыть всех зовущих: сцена кончилась.</summary>
        public static void Clear()
        {
            Taunting.Clear();
            Answered.Clear();
        }

        public static bool IsTaunting(Warrior w)
            => w != null && !w.IsDead && Taunting.Contains(w);

        /// <summary>Есть ли щит во второй руке — без него не закрыться.</summary>
        public static bool HasShield(Warrior w)
        {
            var off = w != null ? w.Worn(Inventory.GearSlot.Offhand) : null;
            return off != null && off.DefenseBonus > 0f;
        }

        /// <summary>Прибавка к защите сейчас: зовёт и со щитом — стойка.</summary>
        public static float StanceBonus(Warrior w)
            => Taunting.Count > 0 && IsTaunting(w) && HasShield(w) ? Stance : 0f;

        /// <summary>
        /// Ближайший враг этого воина, который зовёт на себя и слышен
        /// (<see cref="Radius"/>). Нет — пусто.
        /// </summary>
        public static Warrior Nearest(Warrior listener)
        {
            if (listener == null || Taunting.Count == 0) return null;

            Warrior best = null;
            float bestDist = Radius;
            foreach (var t in Taunting)
            {
                if (t == null || t.IsDead || t.Team == listener.Team) continue;
                float d = Vector3.Distance(listener.transform.position, t.transform.position);
                if (d <= bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        /// <summary>
        /// Враг ответил на зов — словом над головой, один раз. По греху,
        /// чтобы игрок видел, кого зов берёт, а кого нет, и почему.
        /// </summary>
        public static void Answer(Warrior hunter, bool takes)
        {
            if (hunter == null || !Answered.Add(hunter)) return;

            var soul = hunter.Soul;
            var sin = soul.Sin;
            string line;
            if (takes)
            {
                switch (sin)
                {
                    case SinType.Wrath: line = Loc.T("Ну держись!"); break;
                    case SinType.Pride: line = Loc.T("Ты? Против меня? Изволь."); break;
                    default:            line = Loc.T("Сам напросился."); break;
                }
            }
            else
            {
                switch (sin)
                {
                    case SinType.Greed: line = Loc.T("За тебя не платят. Мне нужен Греховод."); break;
                    case SinType.Envy:  line = Loc.T("Кричи, кричи. Мне нужен тот, что с банками."); break;
                    case SinType.Sloth: line = Loc.T("Гоняться за ним? Вот ещё."); break;
                    default:            line = Loc.T("Не поведусь."); break;
                }
            }

            UI.SpeechBubbles.Say(hunter, line);
            Debug.Log($"[ПРОВОКАЦИЯ] {hunter.DisplayName}: {(takes ? "ведётся" : "не ведётся")}.");
        }
    }
}
