// Assets/_Project/Scripts/AOS/TitleDatabase.cs
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
            new TitleRule { Title = "Убийца Охотников", MainDeed = DeedType.Kill, RequiredCount = 7, RequiredImportance = 40 },
            new TitleRule { Title = "Гроза Охотников", MainDeed = DeedType.Kill, RequiredCount = 15, RequiredImportance = 70 },
            new TitleRule { Title = "Мститель", MainDeed = DeedType.Kill, RequiredCount = 10, RequiredImportance = 60 },
            new TitleRule { Title = "Каратель", MainDeed = DeedType.KillCommander, RequiredCount = 5, RequiredImportance = 50 },
            new TitleRule { Title = "Берсерк", MainDeed = DeedType.NeverRetreat, RequiredCount = 8, RequiredImportance = 35 },
            new TitleRule { Title = "Одинокий Волк", MainDeed = DeedType.LastStand, RequiredCount = 3, RequiredImportance = 80 },

            // ──────────────────────────────────
            // Защитные титулы (действие SaveAlly)
            // ──────────────────────────────────
            new TitleRule { Title = "Спаситель", MainDeed = DeedType.SaveAlly, RequiredCount = 7, RequiredImportance = 35 },
            new TitleRule { Title = "Хранитель", MainDeed = DeedType.SaveAlly, RequiredCount = 15, RequiredImportance = 60 },
            new TitleRule { Title = "Щит Отряда", MainDeed = DeedType.ProtectCommander, RequiredCount = 5, RequiredImportance = 45 },
            new TitleRule { Title = "Телохранитель", MainDeed = DeedType.ProtectCommander, RequiredCount = 8, RequiredImportance = 60 },
            new TitleRule { Title = "Защитница", MainDeed = DeedType.SaveAlly, RequiredCount = 12, RequiredImportance = 70 },
            new TitleRule { Title = "Наставник", MainDeed = DeedType.SaveAlly, RequiredCount = 10, RequiredImportance = 50 },

            // ──────────────────────────────────
            // Жадные титулы (действие Loot / CollectMostLoot)
            // ──────────────────────────────────
            new TitleRule { Title = "Костекоп", MainDeed = DeedType.CollectMostLoot, RequiredCount = 5, RequiredImportance = 40 },
            new TitleRule { Title = "Золотоискатель", MainDeed = DeedType.CollectMostLoot, RequiredCount = 12, RequiredImportance = 70 },
            new TitleRule { Title = "Мародёр", MainDeed = DeedType.FindTreasure, RequiredCount = 15, RequiredImportance = 40 },
            new TitleRule { Title = "Скупой", MainDeed = DeedType.CollectMostLoot, RequiredCount = 8, RequiredImportance = 35 },
            new TitleRule { Title = "Золотые Руки", MainDeed = DeedType.CollectMostLoot, RequiredCount = 6, RequiredImportance = 50 },

            // ──────────────────────────────────
            // Трусливые / Выживальщики
            // ──────────────────────────────────
            new TitleRule { Title = "Везунчик", MainDeed = DeedType.SurviveMission, RequiredCount = 5, RequiredImportance = 40 },
            new TitleRule { Title = "Беглец", MainDeed = DeedType.Escape, RequiredCount = 7, RequiredImportance = 30 },
            new TitleRule { Title = "Несломленный", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 80 },
            new TitleRule { Title = "Тень", MainDeed = DeedType.SurviveMission, RequiredCount = 1, RequiredImportance = 60 },
            new TitleRule { Title = "Скиталец", MainDeed = DeedType.SurviveMission, RequiredCount = 20, RequiredImportance = 50 },

            // ──────────────────────────────────
            // Легендарные (особые условия)
            // ──────────────────────────────────
            new TitleRule { Title = "Некромант", MainDeed = DeedType.DigMostSouls, RequiredCount = 1, RequiredImportance = 70, RequiresSoulCollector = true },
            new TitleRule { Title = "Заклинатель Костей", MainDeed = DeedType.RecruitWarrior, RequiredCount = 5, RequiredImportance = 50, RequiresNearAltar = true },
            new TitleRule { Title = "Последний Рубеж", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 95, RequiresLastAlive = true },
            new TitleRule { Title = "Легенда", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 100, RequiresCoreMemory = true },
            new TitleRule { Title = "Страж Забытых Залов", MainDeed = DeedType.Kill, RequiredCount = 20, RequiredImportance = 80, RequiresNearAltar = true },
            new TitleRule { Title = "Исполнитель", MainDeed = DeedType.ExecuteEnemy, RequiredCount = 10, RequiredImportance = 40 },
        };
    }
}