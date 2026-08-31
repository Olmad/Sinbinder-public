using System.Collections.Generic;
using Sinbinder.Core;

namespace Sinbinder.Inventory
{
    /// <summary>
    /// Предметы-искусители демо.
    ///
    /// Содержание взято из 08-FLOOR §3.3 и 11-MISSING §2.2 дословно:
    /// золочёный клинок — Жадность, верёвка монаха — минус Гордыня,
    /// фляга — Чревоугодие. Это последний из восьми рычагов игрока,
    /// и единственный, который действует медленно и обратимо: душу
    /// не переписывают, ей вкладывают в руку вещь и смотрят, что будет.
    ///
    /// Каталог живёт в коде, а не в ассетах, потому что типа-ассета
    /// у предметов нет: InventoryItem — обычный сериализуемый класс,
    /// не ScriptableObject. Заводить ради трёх вещей новый вид ассета
    /// дороже, чем оно стоит; когда предметов станет два десятка,
    /// это будет первое, что надо вынести в данные.
    /// </summary>
    public static class TemptationCatalog
    {
        /// <summary>
        /// Отрицательное искушение тянет ОТ греха: верёвка монаха
        /// не делает смиренным, но мешает гордиться.
        /// </summary>
        public static IEnumerable<InventoryItem> Demo()
        {
            yield return new InventoryItem(
                "Золочёный клинок",
                "Слишком красив для работы. Его хочется не обнажать, а показывать.",
                ItemType.Equipment, 1, SinType.Greed, 60f);

            yield return new InventoryItem(
                "Верёвка монаха",
                "Простая, грубая, режет ладонь. Носивший её не считал себя выше других.",
                ItemType.Equipment, 1, SinType.Pride, -55f);

            yield return new InventoryItem(
                "Фляга",
                "Плещется. На привале о ней думают раньше, чем о карауле.",
                ItemType.Provision, 1, SinType.Gluttony, 50f);
        }
    }
}
