// Assets/Scripts/AOS Engine/TitleDatabase.cs
using System.Collections.Generic;

namespace Sinbinder.AOS
{
    public static class TitleDatabase
    {
        /// <summary>
        /// Титулы. У каждого <b>обе формы написаны руками</b>, даже когда
        /// они совпадают: «Тень» и «Тень» — это решение, что слово одно
        /// на оба пола, а пустое поле было бы просто недосмотром.
        /// Различить их иначе нельзя — из «Защитница» не выводится,
        /// что она женская, а из «Тень» что она общая.
        ///
        /// Стенд требует обе формы у каждого правила (ТИТУЛЫ: одно
        /// условие, две формы). Так добавивший новое имя обязан
        /// решить, как его носит женщина, — а не узнать об этом
        /// от игрока.
        /// </summary>
        public static List<TitleRule> Rules = new()
        {
            // ──────────────────────────────────
            // Боевые титулы (действие Kill)
            // ──────────────────────────────────
            new TitleRule { Title = "Убийца Охотников", Female = "Убийца Охотников", MainDeed = DeedType.Kill, RequiredCount = 7, RequiredImportance = 3.5f },
            new TitleRule { Title = "Гроза Охотников", Female = "Гроза Охотников", MainDeed = DeedType.Kill, RequiredCount = 15, RequiredImportance = 7.5f },
            new TitleRule { Title = "Мститель", Female = "Мстительница", MainDeed = DeedType.Kill, RequiredCount = 10, RequiredImportance = 5f },
            new TitleRule { Title = "Каратель", Female = "Карательница", MainDeed = DeedType.KillCommander, RequiredCount = 5, RequiredImportance = 2.5f },
            new TitleRule { Title = "Берсерк", Female = "Берсерк", MainDeed = DeedType.NeverRetreat, RequiredCount = 8, RequiredImportance = 4f },
            new TitleRule { Title = "Одинокий Волк", Female = "Одинокая Волчица", MainDeed = DeedType.LastStand, RequiredCount = 3, RequiredImportance = 3f },

            // ──────────────────────────────────
            // Защитные титулы (действие SaveAlly)
            // ──────────────────────────────────
            new TitleRule { Title = "Спаситель", Female = "Спасительница", MainDeed = DeedType.SaveAlly, RequiredCount = 7, RequiredImportance = 4.9f },
            new TitleRule { Title = "Хранитель", Female = "Хранительница", MainDeed = DeedType.SaveAlly, RequiredCount = 15, RequiredImportance = 10.5f },
            new TitleRule { Title = "Щит Отряда", Female = "Щит Отряда", MainDeed = DeedType.ProtectCommander, RequiredCount = 5, RequiredImportance = 3.5f },
            new TitleRule { Title = "Телохранитель", Female = "Телохранительница", MainDeed = DeedType.ProtectCommander, RequiredCount = 8, RequiredImportance = 5.6f },
            new TitleRule { Title = "Защитник", Female = "Защитница", MainDeed = DeedType.SaveAlly, RequiredCount = 12, RequiredImportance = 8.4f },
            new TitleRule { Title = "Наставник", Female = "Наставница", MainDeed = DeedType.SaveAlly, RequiredCount = 10, RequiredImportance = 7f },

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
            new TitleRule { Title = "Костекоп", Female = "Костекоп", MainDeed = DeedType.CollectMostLoot, RequiredCount = 30, RequiredImportance = 40f },
            new TitleRule { Title = "Золотоискатель", Female = "Золотоискательница", MainDeed = DeedType.CollectMostLoot, RequiredCount = 52, RequiredImportance = 70f },
            new TitleRule { Title = "Мародёр", Female = "Мародёр", MainDeed = DeedType.FindTreasure, RequiredCount = 15, RequiredImportance = 18f },
            new TitleRule { Title = "Скупой", Female = "Скупая", MainDeed = DeedType.CollectMostLoot, RequiredCount = 26, RequiredImportance = 35f },
            new TitleRule { Title = "Золотые Руки", Female = "Золотые Руки", MainDeed = DeedType.CollectMostLoot, RequiredCount = 37, RequiredImportance = 50f },

            // ──────────────────────────────────
            // Трусливые / Выживальщики
            // ──────────────────────────────────
            new TitleRule { Title = "Везунчик", Female = "Везунья", MainDeed = DeedType.SurviveMission, RequiredCount = 5, RequiredImportance = 1.5f },
            new TitleRule { Title = "Беглец", Female = "Беглянка", MainDeed = DeedType.Escape, RequiredCount = 7, RequiredImportance = 2.1f },
            new TitleRule { Title = "Несломленный", Female = "Несломленная", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f },
            new TitleRule { Title = "Тень", Female = "Тень", MainDeed = DeedType.SurviveMission, RequiredCount = 1, RequiredImportance = 0.3f },
            new TitleRule { Title = "Скиталец", Female = "Скиталица", MainDeed = DeedType.SurviveMission, RequiredCount = 20, RequiredImportance = 6f },

            // ──────────────────────────────────
            // Легендарные (особые условия)
            // ──────────────────────────────────
            new TitleRule { Title = "Некромант", Female = "Некромантка", MainDeed = DeedType.DigMostSouls, RequiredCount = 1, RequiredImportance = 1f, RequiresSoulCollector = true },
            new TitleRule { Title = "Заклинатель Костей", Female = "Заклинательница Костей", MainDeed = DeedType.RecruitWarrior, RequiredCount = 5, RequiredImportance = 5f, RequiresNearAltar = true },
            new TitleRule { Title = "Последний Рубеж", Female = "Последний Рубеж", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f, RequiresLastAlive = true },
            new TitleRule { Title = "Легенда", Female = "Легенда", MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f, RequiresCoreMemory = true },
            new TitleRule { Title = "Страж Забытых Залов", Female = "Страж Забытых Залов", MainDeed = DeedType.Kill, RequiredCount = 20, RequiredImportance = 10f, RequiresNearAltar = true },
            new TitleRule { Title = "Исполнитель", Female = "Исполнительница", MainDeed = DeedType.ExecuteEnemy, RequiredCount = 10, RequiredImportance = 5f },
        };
    }
}