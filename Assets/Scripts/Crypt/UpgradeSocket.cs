// Assets/Scripts/Crypt/UpgradeSocket.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Гнездо под улучшение склепа. Их два, и больше не будет.
    ///
    /// Пустое гнездо — это <b>обещание</b>, и обещание честное: игрок
    /// видит, что место есть, и знает, что принести туда что-то можно
    /// только с вылазки. Пустая комната без гнёзд ничего не обещала бы,
    /// а комната с десятью гнёздами обещала бы систему строительства,
    /// которой не будет (<c>05-BOUNDS.md</c>).
    ///
    /// Поставленная Казна остаётся рабочей: к ней подходят и платят
    /// отряду. Улучшение, которое после установки только украшает
    /// стену, — это не рычаг, а трофей.
    /// </summary>
    public class UpgradeSocket : CryptInteractable
    {
        [Tooltip("Что стоит в этом гнезде. Пусто, пока не принесли.")]
        [SerializeField] private bool _filled;

        [SerializeField] private Upgrade _holds = Upgrade.Cellar;

        public override string Label
        {
            get
            {
                if (!_filled) return "Пустое гнездо\nпринесите с вылазки";

                return _holds == Upgrade.Treasury
                    ? $"{CryptUpgrades.Name(_holds)}\nзаплатить отряду"
                    : $"{CryptUpgrades.Name(_holds)}\n{CryptUpgrades.Does(_holds)}";
            }
        }

        protected override bool Ready
            => _filled ? _holds == Upgrade.Treasury : CryptUpgrades.AnyBrought;

        protected override string Hint
        {
            get
            {
                if (_filled && _holds == Upgrade.Treasury)
                {
                    int owed = Owed();
                    return owed == 0
                        ? "Казна. Никому не должны."
                        : $"Казна. Есть кому заплатить — {_key}.";
                }

                if (_filled) return Flat(Label);

                return CryptUpgrades.AnyBrought
                    ? $"Пустое гнездо. Поставить {CryptUpgrades.Name(CryptUpgrades.FirstBrought())} — {_key}."
                    : "Пустое гнездо. Ставить пока нечего.";
            }
        }

        void Start()
        {
            // Гнездо переживает перезапуск сцены, а поставленное —
            // нет: правда о том, что стоит в склепе, живёт
            // в CryptUpgrades, а гнездо — только её вид.
            if (!_filled) return;
            if (!CryptUpgrades.Installed(_holds)) _filled = false;
        }

        protected override void Use()
        {
            if (_filled)
            {
                if (_holds == Upgrade.Treasury) Pay();
                return;
            }

            if (!CryptUpgrades.AnyBrought)
            {
                Say("Ставить нечего. Такое приносят с вылазки.");
                return;
            }

            var what = CryptUpgrades.FirstBrought();
            if (!CryptUpgrades.Install(what)) return;

            _filled = true;
            _holds = what;

            Say($"{CryptUpgrades.Name(what)} поставлен. {CryptUpgrades.Does(what)}");
        }

        /// <summary>Сколько всего невыплат по отряду.</summary>
        private static int Owed()
        {
            int owed = 0;
            foreach (var m in SquadRoster.Members) owed += m.UnpaidMissions;
            return owed;
        }

        /// <summary>
        /// Заплатить всем.
        ///
        /// Это тот самый рычаг: долг — единственная причина отказа,
        /// которую игрок может убрать <b>заранее</b>, а не пережить.
        /// До казны рычаг существовал в замерах и не существовал в руках.
        /// </summary>
        private void Pay()
        {
            int owed = Owed();

            if (owed == 0) { Say("Никому не должны."); return; }

            var purse = Inventory.PlayerInventory.Instance;
            int cost = owed * CoinPerMission;

            if (purse != null && !purse.SpendGold(cost))
            {
                Say("В казне столько не наберётся. Придётся идти должниками.");
                return;
            }

            SquadRoster.PayEveryone();
            Say("Отряду заплачено. Долгов за вами нет.");
        }

        /// <summary>Сколько стоит закрыть одну невыплату.</summary>
        private const int CoinPerMission = 10;

        /// <summary>Настроить из сборщика сцены.</summary>
        public void SetEmpty() => _filled = false;
    }
}
