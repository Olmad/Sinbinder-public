// Assets/Scripts/Inventory/TrophyCatalog.cs
// Перевод: текст через Loc
using System.Collections.Generic;

using Sinbinder.Core;
namespace Sinbinder.Inventory
{
    /// <summary>
    /// Что Марга Копатель выкопал возле лагеря. Сцена 3 пролога.
    ///
    /// Простое снаряжение и ничего больше. Искушения у вещей — рычаг
    /// полной версии, а не демо (docs/09-PROLOGUE.md §7), поэтому здесь
    /// у всех <c>TemptationValue</c> ноль: движок такую вещь видит,
    /// но ему нечего усиливать.
    ///
    /// Вещи названы так, чтобы про каждую было понятно, откуда она,
    /// без единого слова объяснения. Это и есть обучение снаряжению
    /// без слова «снаряжение».
    /// </summary>
    public static class TrophyCatalog
    {
        /// <summary>Содержимое сундука, в том порядке, в каком его достают.</summary>
        public static IEnumerable<InventoryItem> Chest()
        {
            yield return new InventoryItem(
                Loc.N("Кольчужный ворот"),
                Loc.N("Чужой, ушитый по чужой шее. Но шея у всех одна."),
                ItemType.Equipment, defense: 2f, slot: GearSlot.Body);

            yield return new InventoryItem(
                Loc.N("Топор с новым топорищем"),
                Loc.N("Лезвие старше топорища втрое. Кто-то очень не хотел его бросать."),
                ItemType.Equipment, attack: 2f, slot: GearSlot.Weapon);

            yield return new InventoryItem(
                Loc.N("Связка вяленого мяса"),
                Loc.N("Перевязана бечёвкой дважды. Марга считал."),
                ItemType.Provision, 3);

            yield return new InventoryItem(
                Loc.N("Горсть монет"),
                Loc.N("Позеленевшие. Их выкапывали, а не зарабатывали."),
                ItemType.Gold, 24);
        }
    }
}
