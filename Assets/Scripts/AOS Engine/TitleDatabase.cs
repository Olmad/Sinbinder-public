// Assets/Scripts/AOS Engine/TitleDatabase.cs
// Перевод: текст через Loc
using System.Collections.Generic;

using Sinbinder.Core;
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
            new TitleRule { Title = Loc.N("Убийца Охотников"), Female = Loc.N("Убийца Охотников"), MainDeed = DeedType.Kill, RequiredCount = 7, RequiredImportance = 3.5f },
            new TitleRule { Title = Loc.N("Гроза Охотников"), Female = Loc.N("Гроза Охотников"), MainDeed = DeedType.Kill, RequiredCount = 15, RequiredImportance = 7.5f },
            new TitleRule { Title = Loc.N("Мститель"), Female = Loc.N("Мстительница"), MainDeed = DeedType.Kill, RequiredCount = 10, RequiredImportance = 5f },
            new TitleRule { Title = Loc.N("Каратель"), Female = Loc.N("Карательница"), MainDeed = DeedType.KillCommander, RequiredCount = 5, RequiredImportance = 2.5f },
            new TitleRule { Title = Loc.N("Берсерк"), Female = Loc.N("Берсерк"), MainDeed = DeedType.NeverRetreat, RequiredCount = 8, RequiredImportance = 4f },
            new TitleRule { Title = Loc.N("Одинокий Волк"), Female = Loc.N("Одинокая Волчица"), MainDeed = DeedType.LastStand, RequiredCount = 3, RequiredImportance = 3f },

            // ──────────────────────────────────
            // Защитные титулы (действие SaveAlly)
            // ──────────────────────────────────
            new TitleRule { Title = Loc.N("Спаситель"), Female = Loc.N("Спасительница"), MainDeed = DeedType.SaveAlly, RequiredCount = 7, RequiredImportance = 4.9f },
            new TitleRule { Title = Loc.N("Хранитель"), Female = Loc.N("Хранительница"), MainDeed = DeedType.SaveAlly, RequiredCount = 15, RequiredImportance = 10.5f },
            new TitleRule { Title = Loc.N("Щит Отряда"), Female = Loc.N("Щит Отряда"), MainDeed = DeedType.ProtectCommander, RequiredCount = 5, RequiredImportance = 3.5f },
            new TitleRule { Title = Loc.N("Телохранитель"), Female = Loc.N("Телохранительница"), MainDeed = DeedType.ProtectCommander, RequiredCount = 8, RequiredImportance = 5.6f },
            new TitleRule { Title = Loc.N("Защитник"), Female = Loc.N("Защитница"), MainDeed = DeedType.SaveAlly, RequiredCount = 12, RequiredImportance = 8.4f },
            new TitleRule { Title = Loc.N("Наставник"), Female = Loc.N("Наставница"), MainDeed = DeedType.SaveAlly, RequiredCount = 10, RequiredImportance = 7f },

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
            new TitleRule { Title = Loc.N("Костекоп"), Female = Loc.N("Костекоп"), MainDeed = DeedType.CollectMostLoot, RequiredCount = 30, RequiredImportance = 40f },
            new TitleRule { Title = Loc.N("Золотоискатель"), Female = Loc.N("Золотоискательница"), MainDeed = DeedType.CollectMostLoot, RequiredCount = 52, RequiredImportance = 70f },
            new TitleRule { Title = Loc.N("Мародёр"), Female = Loc.N("Мародёр"), MainDeed = DeedType.FindTreasure, RequiredCount = 15, RequiredImportance = 18f },
            new TitleRule { Title = Loc.N("Скупой"), Female = Loc.N("Скупая"), MainDeed = DeedType.CollectMostLoot, RequiredCount = 26, RequiredImportance = 35f },
            new TitleRule { Title = Loc.N("Золотые Руки"), Female = Loc.N("Золотые Руки"), MainDeed = DeedType.CollectMostLoot, RequiredCount = 37, RequiredImportance = 50f },

            // ──────────────────────────────────
            // Трусливые / Выживальщики
            // ──────────────────────────────────
            new TitleRule { Title = Loc.N("Везунчик"), Female = Loc.N("Везунья"), MainDeed = DeedType.SurviveMission, RequiredCount = 5, RequiredImportance = 1.5f },
            new TitleRule { Title = Loc.N("Беглец"), Female = Loc.N("Беглянка"), MainDeed = DeedType.Escape, RequiredCount = 7, RequiredImportance = 2.1f },
            new TitleRule { Title = Loc.N("Несломленный"), Female = Loc.N("Несломленная"), MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f },
            // «Тень» — тот, кого в бою не видели. До 24 сентября она стояла
            // на SurviveMission со счётом 1, а конец боя пишет это деяние
            // каждому уцелевшему: после первой же волны «Тенью» становился
            // весь отряд, Греховод в том числе, и четыре церемонии подряд
            // объявляли одно и то же слово (27-TRAILER §9). Автор: «все
            // получают титул Тень… мне кажется, этот титул сломан».
            //
            // Теперь — два боя, простоянных без единого удара. Два, а не
            // один: в короткой стычке можно не успеть дойти до врага,
            // и это ещё не характер. Уныние же стоит в стороне раз за разом,
            // и имя приходит тому, кто его заработал, — голосом, а не жребием.
            new TitleRule { Title = Loc.N("Тень"), Female = Loc.N("Тень"), MainDeed = DeedType.StayedOut, RequiredCount = 2, RequiredImportance = 0.6f },
            new TitleRule { Title = Loc.N("Скиталец"), Female = Loc.N("Скиталица"), MainDeed = DeedType.SurviveMission, RequiredCount = 20, RequiredImportance = 6f },

            // ──────────────────────────────────
            // Легендарные (особые условия)
            // ──────────────────────────────────
            new TitleRule { Title = Loc.N("Некромант"), Female = Loc.N("Некромантка"), MainDeed = DeedType.DigMostSouls, RequiredCount = 1, RequiredImportance = 1f, RequiresSoulCollector = true },
            new TitleRule { Title = Loc.N("Заклинатель Костей"), Female = Loc.N("Заклинательница Костей"), MainDeed = DeedType.RecruitWarrior, RequiredCount = 5, RequiredImportance = 5f, RequiresNearAltar = true },
            new TitleRule { Title = Loc.N("Последний Рубеж"), Female = Loc.N("Последний Рубеж"), MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f, RequiresLastAlive = true },
            new TitleRule { Title = Loc.N("Легенда"), Female = Loc.N("Легенда"), MainDeed = DeedType.LastStand, RequiredCount = 1, RequiredImportance = 1f, RequiresCoreMemory = true },
            new TitleRule { Title = Loc.N("Страж Забытых Залов"), Female = Loc.N("Страж Забытых Залов"), MainDeed = DeedType.Kill, RequiredCount = 20, RequiredImportance = 10f, RequiresNearAltar = true },
            new TitleRule { Title = Loc.N("Исполнитель"), Female = Loc.N("Исполнительница"), MainDeed = DeedType.ExecuteEnemy, RequiredCount = 10, RequiredImportance = 5f },
        };
    }
}