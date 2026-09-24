// Assets/Scripts/Gameplay/LootChain.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Цепь добычи (14-HANDOFF §41): бой кончился — отряд собирает с тел
    /// врагов то, что тянет его грех, и взятое <b>доходит до рук</b>.
    ///
    /// Раздача по грехам была написана давно и правильно
    /// (<see cref="LootCarrySystem"/>: жадный подбирает золото, гордый
    /// забирает трофей, унылый не несёт ничего), но её никто не звал,
    /// а результат уходил в никуда. Здесь — те два звена, которые §41
    /// назвал наименьшим честным закрытием:
    ///
    /// <list type="number">
    /// <item>раздача зовётся в конце боя и берёт только тела врагов;</item>
    /// <item>трофей надевает тот, кто его забрал (<see cref="SquadGear.Pick"/>):
    /// клинок бьёт тяжелее и тешит гордыню — добыча спорит в голове у того,
    /// кто её взял. Не взял (<see cref="SquadGear.WillTake"/>) — трофей
    /// уходит в мешок Греховода; сменённое оружие — туда же.</item>
    /// </list>
    ///
    /// <b>Золото делит тот, кто подобрал</b> (docs/34-GEAR.md §9.3): часть —
    /// себе в карман, по силе жадности, но не больше половины; остальное —
    /// в кошель Греховода. Щедрый отдаёт всё. Карман читает Жадность.
    ///
    /// Выключатель: до вечернего прогона 24 сентября выключено, как голос
    /// и удар. Включается командой «добыча» в консоли (~).
    /// </summary>
    public static class LootChain
    {
        public static bool Enabled { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Enabled = false;

        /// <summary>
        /// Раздать добычу живым своим с тел врагов. Повторный вызов в том же
        /// бою безвреден: собранное тело второй раз ничего не даёт.
        /// </summary>
        public static void Share(List<Damageable> ours, List<HarvestableBody> bodies)
        {
            if (!Enabled || ours == null || bodies == null) return;

            var squad = new List<Warrior>();
            foreach (var d in ours)
            {
                if (d == null || d.IsDead || d.Warrior == null) continue;
                if (d.Warrior is SinbinderPlayer) continue;   // Греховод добычу не делит — он её жнёт
                squad.Add(d.Warrior);
            }

            var foes = bodies.FindAll(b => b != null && b.Foe && !b.IsCollected);
            if (squad.Count == 0 || foes.Count == 0) return;

            var loot = LootCarrySystem.DistributeLoot(squad, foes);
            var store = PlayerInventory.Instance;
            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();

            foreach (var (who, what) in loot.Trophies)
            {
                var trophy = Trophy(what);

                SquadGear.Place(who, trophy, out _, out var old);
                if (SquadGear.Pick(who, trophy, store, out string word))
                {
                    string swap = old == null ? "" : $" Прежнее — {old.Name.ToLowerInvariant()} — уходит в мешок Греховода.";
                    log?.Write(Grammar.For(who.Gender,
                        $"{who.DisplayName} забирает трофей: {what.ToLowerInvariant()}.{swap}"));
                }
                else
                {
                    store?.AddItem(trophy);
                    string would = Grammar.Pick(who.Gender, "взял бы", "взяла бы");
                    log?.Write(Grammar.For(who.Gender,
                        $"{who.DisplayName} {would} трофей, но {word} — {what.ToLowerInvariant()} уходит в мешок Греховода."));
                }
            }

            // Золото делит тот, кто подобрал: часть себе в карман, остальное —
            // в кошель Греховода (docs/34-GEAR.md §9.3). Журнал — словами.
            if (store != null)
            {
                foreach (var (who, gold) in loot.GoldBy)
                {
                    int kept = SquadGear.Kept(who, gold);
                    who.Pocket(kept);
                    store.AddGold(gold - kept);

                    string found = SquadGear.GoldWord(gold);
                    log?.Write(Grammar.For(who.Gender, kept == 0
                        ? $"{who.DisplayName} подбирает {found} и отдаёт всё в кошель Греховода."
                        : $"{who.DisplayName} подбирает {found}: часть — в кошель Греховода, остальное — себе в карман."));
                }
            }
        }

        /// <summary>
        /// Трофей с тела. Клинок: бьёт тяжелее и тешит гордыню — ради
        /// гордого он и поднят. Тешит, а не велит: искушение — один голос
        /// в голосовании (TemptationResolver), как любой другой.
        /// </summary>
        public static InventoryItem Trophy(string name)
            => new InventoryItem(name,
                   "Снято с павшего. Тому, кто взял, — знак, что победил он.",
                   ItemType.Equipment, temptationSin: SinType.Pride, temptationValue: 20f,
                   attack: 1f);
    }
}
