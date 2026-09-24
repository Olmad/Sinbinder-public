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
    /// <item>трофей вкладывается в руки тому, кто его забрал
    /// (<see cref="Warrior.Give"/>): клинок бьёт тяжелее и тешит гордыню —
    /// добыча спорит в голове у того, кто её взял. Не взял (руки полны,
    /// <see cref="SquadGear"/>) — трофей уходит в запасы отряда.</item>
    /// </list>
    ///
    /// <b>Золото — в казну отряда.</b> Чьё оно на самом деле, решает автор
    /// (§41.4, п. 4); пока так, и журнал говорит, кто его подобрал.
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

                if (SquadGear.WillTake(who, trophy, out string word))
                {
                    who.Give(trophy);
                    log?.Write(Grammar.For(who.Gender,
                        $"{who.DisplayName} забирает трофей: {what.ToLowerInvariant()}."));
                }
                else
                {
                    store?.AddItem(trophy);
                    string would = Grammar.Pick(who.Gender, "взял бы", "взяла бы");
                    log?.Write(Grammar.For(who.Gender,
                        $"{who.DisplayName} {would} трофей, но {word} — {what.ToLowerInvariant()} уходит в запасы."));
                }
            }

            if (loot.Gold > 0 && store != null)
            {
                store.AddGold(loot.Gold);
                foreach (var (who, gold) in loot.GoldBy)
                    log?.Write(Grammar.For(who.Gender,
                        $"{who.DisplayName} подбирает {SquadGear.GoldWord(gold)} — в казну."));
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
