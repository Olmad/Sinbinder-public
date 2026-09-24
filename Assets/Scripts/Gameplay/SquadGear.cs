// Assets/Scripts/Gameplay/SquadGear.cs
using Sinbinder.Core;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Обмен вещами между запасами отряда и руками воина (docs/34-GEAR.md).
    ///
    /// Автор, 24 сентября: «у нас нет панели инвентаря и снаряжения…
    /// снаряжение и инвентарь должны быть у обычных воинов». Руки у воина
    /// были (<see cref="Warrior.Carried"/>), движок их читал — вещь спорит
    /// у воина в голове (TemptationResolver), с §80 она ещё и бьёт и держит
    /// удар, — но вложить её было негде.
    ///
    /// <b>Обмен — не нейтрален.</b> Отдать и забрать — поступки, и воин
    /// отвечает на них как душа: унылый не берёт лишнего, гневный не носит
    /// того, чем нельзя ударить, жадный не отдаёт своего. Правила те же,
    /// что у раздачи добычи (<see cref="LootCarrySystem"/>): один характер —
    /// одни руки. Каждая передача остаётся в памяти, а из памяти
    /// вычисляются отношения.
    ///
    /// Без жребия: одна и та же вещь одной и той же душе — один и тот же
    /// ответ. Ответ — словами, без чисел.
    /// </summary>
    public static class SquadGear
    {
        /// <summary>
        /// Сколько вещей несёт воин. Немного нарочно: руки, которые вмещают
        /// всё, — это склад, а склад ничего не решает.
        /// </summary>
        public const int Hands = 3;

        /// <summary>Возьмёт ли воин вещь — и что скажет.</summary>
        public static bool WillTake(Warrior w, InventoryItem item, out string word)
        {
            var soul = w.Soul;
            bool weapon = item.AttackBonus > 0f;

            if (w.Carried.Count >= Hands) { word = "руки заняты"; return false; }

            if (soul.Sin == SinType.Sloth && soul.Get(SinType.Sloth) > 40f)
            {
                word = "не хочет нести лишнего";
                return false;
            }

            if (soul.Sin == SinType.Wrath && soul.Get(SinType.Wrath) > 40f && !weapon)
            {
                word = "не носит того, чем нельзя ударить";
                return false;
            }

            word = Glad(w, item) ? "берёт охотно" : "берёт";
            return true;
        }

        /// <summary>Отдаст ли воин вещь из рук — и что скажет.</summary>
        public static bool WillGive(Warrior w, InventoryItem item, out string word)
        {
            var soul = w.Soul;

            bool valuable = item.TemptationValue > 0f
                         || item.Type == ItemType.Artifact
                         || item.Type == ItemType.Gold;

            if (soul.Sin == SinType.Greed && soul.Get(SinType.Greed) > 50f && valuable)
            {
                word = "не отдаёт — теперь это его";
                return false;
            }

            if (soul.Sin == SinType.Pride && soul.Get(SinType.Pride) > 60f && item.AttackBonus > 0f)
            {
                word = "не отдаёт оружия — отнять его у него значит унизить";
                return false;
            }

            word = "отдаёт";
            return true;
        }

        /// <summary>Рада ли душа этой вещи: вещь тянет её же греху.</summary>
        private static bool Glad(Warrior w, InventoryItem item)
        {
            var sin = w.Soul.Sin;
            if (item.TemptationValue > 0f && item.TemptationSin == sin) return true;
            if (sin == SinType.Wrath && item.AttackBonus > 0f) return true;
            if (sin == SinType.Greed && (item.Type == ItemType.Artifact || item.Type == ItemType.Gold)) return true;
            return false;
        }

        /// <summary>
        /// Что вещь делает с тем, кто её несёт, — словами. Числа удара
        /// и защиты игрок не видит, он видит, что топор «бьёт тяжелее».
        /// </summary>
        public static string Effect(InventoryItem item)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (item.AttackBonus > 0f) parts.Add("бьёт тяжелее");
            if (item.DefenseBonus > 0f) parts.Add("держит удар");
            if (item.TemptationValue > 0f) parts.Add("искушает");
            if (item.Type == ItemType.Provision) parts.Add("припас");
            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }

        /// <summary>Золото словом: игрок не видит чисел, и казны тоже.</summary>
        public static string GoldWord(int gold)
        {
            if (gold <= 0) return "ни монеты";
            if (gold < 30) return "горсть монет";
            if (gold < 100) return "кошель";
            return "сундук золота";
        }

        /// <summary>
        /// Передать вещь из запасов в руки. Ложь — воин не взял, и вещь
        /// осталась в запасах. Взятое записывается в память.
        /// </summary>
        public static bool Hand(Warrior w, InventoryItem item, PlayerInventory store, out string word)
        {
            if (!WillTake(w, item, out word)) return false;
            if (!store.RemoveItem(item.Id)) { word = "этого в запасах уже нет"; return false; }

            w.Give(item);
            Remember(w, "SinbinderGaveMe");
            return true;
        }

        /// <summary>
        /// Забрать вещь из рук в запасы. Ложь — не отдал или запасы полны.
        /// Отнятое записывается в память: отношения к Греховоду считаются
        /// и из этого.
        /// </summary>
        public static bool Take(Warrior w, InventoryItem item, PlayerInventory store, out string word)
        {
            if (!WillGive(w, item, out word)) return false;
            if (!store.AddItem(item)) { word = "в запасах нет места"; return false; }

            w.Drop(item);
            Remember(w, "SinbinderTookFromMe");
            return true;
        }

        private static void Remember(Warrior w, string what)
        {
            if (AOS.MemoryProcessor.Instance == null || !SinbinderPlayer.Exists) return;
            AOS.MemoryProcessor.Instance.RecordInteraction(w, SinbinderPlayer.Instance, what);
        }
    }
}
