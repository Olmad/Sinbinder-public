// Assets/Scripts/Crypt/SoulJar.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Банка с душой на полке.
    ///
    /// Сосуды — не выдумка ради сцены: <c>00-GDD.md</c> §3 определяет
    /// Сборщиков как «гильдию, запечатывающую души в сосуды». Полка
    /// в склепе — это то, что Греховод у них купил или отнял.
    ///
    /// <b>Что написано на банке.</b> Имя, самая громкая шкала словом
    /// и то, насколько душа свежая. Последнее выглядит украшением,
    /// но это <b>условие выбора тела</b>: истлевшая душа тяжёлого тела
    /// не выдержит (<see cref="ShellChoice"/>). Не сказав об этом здесь,
    /// мы отправили бы игрока к устройству с гарантированным отказом
    /// и без объяснения — а отказ без причины и есть та самая
    /// несправедливость, против которой построена вся игра.
    ///
    /// Цифр нет: только слова.
    /// </summary>
    public class SoulJar : CryptInteractable
    {
        private SoulShelf _shelf;
        private int _slot = -1;

        public SoulData Soul { get; private set; }
        public SoulQuality Quality { get; private set; }

        public override string Label
            => Soul == null
                ? Loc.T("Пустая банка")
                : $"{Soul.Name}\n{SoulData.GetSinName(Soul.Sin)}\n{Freshness(Quality)}";

        /// <summary>Занять банку душой. Зовёт полка, когда расставляет их.</summary>
        public void Fill(SoulShelf shelf, int slot, SoulData soul, SoulQuality quality)
        {
            _shelf = shelf;
            _slot = slot;
            Soul = soul;
            Quality = quality;
        }

        protected override bool Ready => Soul != null;

        protected override string Hint
            => CryptHands.Empty
                ? Loc.F("{0} — взять на {1}.", Flat(Label), _key)
                : Loc.F("{0}. В руках {1}.", Flat(Label), CryptHands.What);

        protected override void Use()
        {
            if (Soul == null) return;

            if (!CryptHands.Empty)
            {
                Say(Loc.F("Руки заняты: {0}.", CryptHands.What));
                return;
            }

            // Душа уходит с полки по-настоящему, а не копируется: иначе
            // одну и ту же можно было бы вселить дважды, и связывание
            // перестало бы что-либо стоить.
            var souls = SoulManager.Instance;
            if (souls == null || _shelf == null) return;

            var kept = souls.TakeHarvested(_shelf.IndexOf(_slot));
            if (kept.Soul == null) { _shelf.Rebuild(); return; }

            CryptHands.TakeSoul(kept.Soul, kept.Quality);
            Say(Loc.F("Взято: {0}. {1}", Loc.Name(kept.Soul.Name), Freshness(kept.Quality)));

            _shelf.Rebuild();
        }

        /// <summary>Сколько от души осталось — теми же словами, что и на экране выбора тела.</summary>
        public static string Freshness(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return Loc.T("Она ещё вся здесь");
                case SoulQuality.Acceptance: return Loc.T("Крайности уже сгладились");
                case SoulQuality.Fading:     return Loc.T("Характер тускнеет");
                default:                     return Loc.T("Осталась одна воля");
            }
        }
    }
}
