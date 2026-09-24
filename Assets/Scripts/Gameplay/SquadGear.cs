// Assets/Scripts/Gameplay/SquadGear.cs
using Sinbinder.Core;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Обмен вещами между запасами отряда и воином (docs/34-GEAR.md).
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
    /// <b>Места, а не руки</b> (решение автора, 24 сентября): оружие, щит
    /// или второе оружие, шлем, броня, пояс. Одно место — одна вещь.
    /// Раньше у воина было три «руки», и всё в них складывалось: три топора
    /// били как три. Теперь вторая вещь на занятое место — замена,
    /// и прежняя возвращается; а кто своего не отдаёт, тот и не меняет.
    ///
    /// Без жребия: одна и та же вещь одной и той же душе — один и тот же
    /// ответ. Ответ — словами, без чисел.
    /// </summary>
    public static class SquadGear
    {
        /// <summary>Место словом — для экрана и журнала.</summary>
        public static string SlotWord(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Weapon:  return "Оружие";
                case GearSlot.Offhand: return "Щит или второе оружие";
                case GearSlot.Head:    return "Шлем";
                case GearSlot.Body:    return "Броня";
                case GearSlot.Belt:    return "Пояс";
                default:               return "";
            }
        }

        /// <summary>Места по порядку — как их показывает экран.</summary>
        public static readonly GearSlot[] Slots =
        {
            GearSlot.Weapon, GearSlot.Offhand, GearSlot.Head, GearSlot.Body, GearSlot.Belt,
        };

        /// <summary>
        /// Куда встанет вещь и что она сменит. Свободное место — без замены.
        /// Всё занято — замена: одно место, одна вещь, второй шлем встаёт
        /// вместо первого. Третье оружие сменяет слабейшее из двух; щит
        /// во второй руке оно не трогает — сменяет то, что в главной.
        /// Ложь — вещь не надевают (золото).
        /// </summary>
        public static bool Place(Warrior w, InventoryItem item, out GearSlot slot, out InventoryItem displaced)
        {
            displaced = null;
            slot = w.FreePlaceFor(item);
            if (slot != GearSlot.None) return true;

            var own = item.Slot;
            if (own == GearSlot.None) return false;

            slot = own;
            if (own == GearSlot.Weapon)
            {
                var main = w.Worn(GearSlot.Weapon);
                var second = w.Worn(GearSlot.Offhand);
                if (second != null && second.Slot == GearSlot.Weapon && second.AttackBonus <= main.AttackBonus)
                    slot = GearSlot.Offhand;
            }

            displaced = w.Worn(slot);
            return true;
        }

        /// <summary>Возьмёт ли воин вещь — и что скажет. Замену называет.</summary>
        public static bool WillTake(Warrior w, InventoryItem item, out string word)
        {
            var soul = w.Soul;
            bool weapon = item.AttackBonus > 0f;

            foreach (var carried in w.Carried)
                if (carried == item) { word = "это уже на нём"; return false; }

            if (!Place(w, item, out _, out var old)) { word = "это не надевают"; return false; }

            // Лишнего унылый не несёт, а сменить одно на другое — не лишнее.
            if (old == null && soul.Sin == SinType.Sloth && soul.Get(SinType.Sloth) > 40f)
            {
                word = "не хочет нести лишнего";
                return false;
            }

            if (soul.Sin == SinType.Wrath && soul.Get(SinType.Wrath) > 40f && !weapon)
            {
                word = "не носит того, чем нельзя ударить";
                return false;
            }

            // Замена — это ещё и отдать своё. Кто своего не отдаёт, тот
            // и не меняет: гордец не выпустит оружия, жадный — ценного.
            if (old != null && !WillGive(w, old, out string keeps))
            {
                word = $"не сменит {Lower(old)}, {keeps}";
                return false;
            }

            word = Glad(w, item) ? "берёт охотно" : "берёт";
            if (old != null) word += $", взамен отдаёт {Lower(old)}";
            return true;
        }

        private static string Lower(InventoryItem item) => item.Name.ToLowerInvariant();

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
        /// Передать вещь из запасов воину. Ложь — воин не взял, и вещь
        /// осталась в запасах. Сменённая вещь возвращается в запасы.
        /// Взятое записывается в память.
        /// </summary>
        public static bool Hand(Warrior w, InventoryItem item, PlayerInventory store, out string word)
        {
            if (!WillTake(w, item, out word)) return false;
            if (!store.RemoveItem(item.Id)) { word = "этого в запасах уже нет"; return false; }

            if (!Wear(w, item, store)) { store.AddItem(item); word = "не взял"; return false; }
            Remember(w, "SinbinderGaveMe");
            return true;
        }

        /// <summary>
        /// Взять вещь, которая ни у кого не лежит: трофей с тела. Сменённая
        /// уходит в запасы. Ложь — не взял; куда деть вещь, решает зовущий.
        /// </summary>
        public static bool Pick(Warrior w, InventoryItem item, PlayerInventory store, out string word)
        {
            if (!WillTake(w, item, out word)) return false;
            if (!Wear(w, item, store)) { word = "не взял"; return false; }
            return true;
        }

        /// <summary>Надеть — сняв то, что место занимало, в запасы.</summary>
        private static bool Wear(Warrior w, InventoryItem item, PlayerInventory store)
        {
            if (!Place(w, item, out _, out var old)) return false;

            if (old != null)
            {
                // Сменённое не пропадает: нет места в запасах — не меняет.
                if (store == null || !store.AddItem(old)) return false;
                w.Drop(old);
            }

            // На освободившееся место — через Give, а не на то, что назвал
            // Place: снятие могло переложить оружие из второй руки в главную.
            if (w.Give(item)) return true;

            if (old != null) { store.RemoveItem(old.Id); w.Give(old); }
            return false;
        }

        /// <summary>
        /// Забрать вещь у воина в запасы. Ложь — не отдал или запасы полны.
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
