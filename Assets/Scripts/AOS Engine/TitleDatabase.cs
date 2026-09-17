// Assets/Scripts/AOS Engine/TitleDatabase.cs
using System.Collections.Generic;

namespace Sinbinder.AOS
{
    public static class TitleDatabase
    {
        public static List<TitleRule> Rules = new()
        {
            // ──────────────────────────────────
            // Боевые титулы (действие Kill)
            // ──────────────────────────────────
            new TitleRule { Title = "Убийца Охотников", MainDeed = DeedType.Kill, RequiredCount = 7, RequiredImportance = 3.5f },
            new TitleRule { Title = "Гроза Охотников", MainDeed = DeedType.Kill, RequiredCount = 15, RequiredImportance = 7.5f },
            new TitleRule { Title = "Мститель", MainDeed = DeedType.Kill, RequiredCount = 10, RequiredImportance = 5f },
            new TitleRule { Title = "Каратель", MainDeed = DeedType.KillCommander, RequiredCount = 5, RequiredImportance = 2.5f },
            new TitleRule { Title = "Берсерк", MainDeed = DeedType.NeverRetreat, RequiredCount = 8, RequiredImportance = 4f },
            new TitleRule { Title = "Одинокий Волк", MainDeed = DeedType.LastStand, RequiredCount = 3, RequiredImportance = 3f },

            // ──────────────────────────────────
            // Защитные титулы (действие SaveAlly)
            // ──────────────────────────────────
            new TitleRule { Title = "Спаситель", MainDeed = DeedType.SaveAlly, RequiredCount = 7, RequiredImportance = 4.9f },
            new TitleRule { Title = "Хранитель", MainDeed = DeedType.SaveAlly, RequiredCount = 15, RequiredImportance = 10.5f },
            new TitleRule { Title = "Щит Отряда", MainDeed = DeedType.ProtectCommander, RequiredCount = 5, RequiredImportance = 3.5f },
            new TitleRule { Title = "Телохранитель", MainDeed = DeedType.ProtectCommander, RequiredCount = 8, RequiredImportance = 5.6f },
            new TitleRule { Title = "Защитница", MainDeed = DeedType.SaveAlly, RequiredCount = 12, RequiredImportance = 8.4f },
            new TitleRule { Title = "Наставник", MainDeed = DeedType.SaveAlly, RequiredCount = 10, RequiredImportance = 7f },

            // ──────────────────────────────────
            // Жадные титулы (действие Loot / CollectMostLoot)
            // ──────────────────────────────────
            // Счёт деяний здесь ничего не решал. Важность набирается
            // за тридцать–шестьдесят трупов, а счёт — за пять–двенадцать,
            // и второе условие было мёртвым: имя держала одна важность.
            //
            // Счёт поднят к тому же месту, где стоит важность, и потому
            // титул приходит тогда же, когда приходил: балансовое «когда»
            // не тронуто, починено только «чем». Теперь оба условия
            // связывают, и связывают разных игроков: у того, кто обирает
            // редко, но богато, первым упирается счёт; у того, кто тащит
            // всё подряд, — важность.
            //
            // Замер: Tools/bench → ДОБЫЧА, столбцы «трупов по счёту»
            // и «трупов по важности».
            new TitleRule { Title = "Костекоп", MainDeed = DeedType.CollectMostLoot, RequiredCount = 30, RequiredImportance = 40f },
            new TitleRule { Title = "Золотоискатель", MainDeed = DeedType.CollectMostLoot, RequiredCount = 52, RequiredImportance = 70f },
            new TitleRule { Title = "Мародёр", MainDeed = DeedType.FindTreasure, RequiredCount = 15, RequiredImportance = 18f },
            new TitleRule { Title = "Скупой", MainDeed = DeedType.CollectMostLoot, RequiredCount = 26, RequiredImportance = 35f },
            new TitleRule { Title = "Золотые Руки", MainDeed = DeedType.CollectMostLoot, RequiredCount = 37, RequiredImportance = 50f },

            // ──────────────────────────────────
            // Трусливые / Выживальщики
            // ──────────────────────────────────
            new TitleRule { Title = "Везунчик", MainDeed = DeedType.SurviveMission, RequiredCount = 5, RequiredImportance = 1.5f },
            new TitleRule { Title = "Беглец", MainDeed = DeedType.Escape, RequiredCount = 7, RequiredImportance = 2.1f },
            new TitleRule { Title = "Несломленный", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f },
            new TitleRule { Title = "Тень", MainDeed = DeedType.SurviveMission, RequiredCount = 1, RequiredImportance = 0.3f },
            new TitleRule { Title = "Скиталец", MainDeed = DeedType.SurviveMission, RequiredCount = 20, RequiredImportance = 6f },

            // ──────────────────────────────────
            // Легендарные (особые условия)
            // ──────────────────────────────────
            new TitleRule { Title = "Некромант", MainDeed = DeedType.DigMostSouls, RequiredCount = 1, RequiredImportance = 1f, RequiresSoulCollector = true },
            new TitleRule { Title = "Заклинатель Костей", MainDeed = DeedType.RecruitWarrior, RequiredCount = 5, RequiredImportance = 5f, RequiresNearAltar = true },
            new TitleRule { Title = "Последний Рубеж", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f, RequiresLastAlive = true },
            new TitleRule { Title = "Легенда", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f, RequiresCoreMemory = true },
            new TitleRule { Title = "Страж Забытых Залов", MainDeed = DeedType.Kill, RequiredCount = 20, RequiredImportance = 10f, RequiresNearAltar = true },
            new TitleRule { Title = "Исполнитель", MainDeed = DeedType.ExecuteEnemy, RequiredCount = 10, RequiredImportance = 5f },
        };
    }
}