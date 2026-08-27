#if UNITY_EDITOR
// Файл зависит от UnityEditor. Без этой обёртки сборка плеера падает с CS0246,
// потому что скрипт лежит не в папке Editor.
// Assets/_Project/Scripts/Editor/PerkDatabaseInitializer.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Audio;

namespace Sinbinder.Utilets
{
    public static class PerkDatabaseInitializer
    {
        private const string DATABASE_PATH = "Assets/_Project/Scripts/Core/PerkDatabase.asset";

        [MenuItem("Sinbinder/Initialize Perk Database")]
        public static void Initialize()
        {
            // Загружаем или создаём базу данных
            PerkDatabase database = AssetDatabase.LoadAssetAtPath<PerkDatabase>(DATABASE_PATH);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<PerkDatabase>();
                AssetDatabase.CreateAsset(database, DATABASE_PATH);
            }

            // Очищаем старые записи (опционально)
            database.AllPerks.Clear();

            // Заполняем 100 перков
            database.AllPerks = CreateAllPerks();

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PerkDatabase] Инициализировано {database.AllPerks.Count} перков.");
        }

        private static List<SoulPerk> CreateAllPerks()
{
    var perks = new List<SoulPerk>();

    // ──────────────────────────────────────────────
    // 1. Певец смерти
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Певец смерти", PerkType.Secret,
        "Голос этого воина прекрасен и губителен. Раз в 3 хода он поёт, накладывая Страх на врагов или Печаль на союзников.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        new VoiceModifier { OverrideVoiceType = true, VoiceType = VoiceGenerator.VoiceType.Sine, PitchMultiplier = 1.3f, DurationMultiplier = 1.5f },
        "Глас Рока"));

    // ──────────────────────────────────────────────
    // 2. Немой
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Немой", PerkType.Secret,
        "Не может говорить, но его присутствие успокаивает союзников (+10% к восстановлению морали).",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        new VoiceModifier { OverrideVoiceType = true, VoiceType = VoiceGenerator.VoiceType.Sine, PitchMultiplier = 0.5f, DurationMultiplier = 2f },
        "Безмолвный Страж"));

    // ──────────────────────────────────────────────
    // 3. Хранитель руин
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Хранитель руин", PerkType.Secret,
        "Клялся защищать древние места. В локациях типа 'Склеп' все характеристики удваиваются.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "InRuins", UnlockedAction = ActionType.Attack, ScoreModifier = 100f },
            new PerkEffect { Condition = "InRuins", UnlockedAction = ActionType.Flee, ScoreModifier = -50f }
        }, null, "Страж Забытых Залов"));

    // ──────────────────────────────────────────────
    // 4. Двойная жизнь
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Двойная жизнь", PerkType.Secret,
        "Был аристократом и разбойником. Можно переключать режимы вне боя.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "AristocratMode", UnlockedAction = ActionType.Loot, ScoreModifier = -30f },
            new PerkEffect { Condition = "BanditMode", UnlockedAction = ActionType.Loot, ScoreModifier = 30f }
        }, null, "Двуликий"));

    // ──────────────────────────────────────────────
    // 5. Клятва верности
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Клятва верности", PerkType.Debtor,
        "Дал нерушимую клятву служить командиру. Никогда не предаст и не откажется выполнять приказы.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "CommanderInDanger", UnlockedAction = ActionType.SaveAlly, ScoreModifier = 100f },
            new PerkEffect { Condition = "CommanderOrders", UnlockedAction = ActionType.ObeyCommand, ScoreModifier = 200f }
        }, null, "Верный До Гроба"));

    // ──────────────────────────────────────────────
    // 6. Побеждённый чемпион (с эволюцией)
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Побеждённый чемпион", PerkType.Rival,
        "Когда-то был величайшим, но проиграл. Ищет достойного противника, чтобы вернуть титул. При победе над сильным врагом эволюционирует в перк «Чемпион».",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyStronger", UnlockedAction = ActionType.Attack, ScoreModifier = 80f },
            new PerkEffect { Condition = "WinDuel", UnlockedAction = null, ScoreModifier = 0 }
        }, null, "Павший Чемпион"));

    // ──────────────────────────────────────────────
    // 7. Мать-волчица
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Мать-волчица", PerkType.Family,
        "При жизни была матерью, которая защищала своего ребёнка ценой собственной жизни. Ищет дочь по имени Элис.",
        "Элис",
        new List<PerkEffect> {
            new PerkEffect { Condition = "NearChild", UnlockedAction = ActionType.SaveAlly, ScoreModifier = 100f },
            new PerkEffect { Condition = "ChildFound", UnlockedAction = ActionType.Attack, ScoreModifier = -50f },
            new PerkEffect { Condition = "ChildNotFound", UnlockedAction = ActionType.Attack, ScoreModifier = 20f }
        }, null, "Защитница"));

    // ──────────────────────────────────────────────
    // 8. Учитель
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Учитель", PerkType.Mentor,
        "При жизни учил бою Кирана. Если ученик в отряде, бросается на его защиту.",
        "Киран",
        new List<PerkEffect> {
            new PerkEffect { Condition = "AllyIsStudent", UnlockedAction = ActionType.SaveAlly, ScoreModifier = 80f }
        }, null, "Наставник"));

    // ──────────────────────────────────────────────
    // 9. Бывший Охотник (ненавидит)
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Бывший Охотник (ненавидит)", PerkType.FormerFaction,
        "Провалил инициацию и теперь вымещает злобу на бывших товарищах.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsHunter", UnlockedAction = ActionType.Attack, ScoreModifier = 60f }
        }, null, "Охотник на Охотников"));

    // ──────────────────────────────────────────────
    // 10. Призрак родных мест
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Призрак родных мест", PerkType.Secret,
        "Погиб вдали от дома. При возвращении в место смерти получает призрачную ярость.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "AtDeathPlace", UnlockedAction = ActionType.Attack, ScoreModifier = 40f },
            new PerkEffect { Condition = "AtDeathPlace", UnlockedAction = ActionType.Flee, ScoreModifier = -50f }
        }, null, "Странник"));

    // ──────────────────────────────────────────────
    // 11. Долг перед павшим
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Долг перед павшим", PerkType.Debtor,
        "Единственный выживший из своего отряда. Поклялся защищать командира, чтобы искупить вину.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "CommanderInDanger", UnlockedAction = ActionType.SaveAlly, ScoreModifier = 120f }
        }, null, "Искупитель"));

    // ──────────────────────────────────────────────
    // 12. Лесной отшельник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Лесной отшельник", PerkType.Secret,
        "Много лет прожил в чаще. В лесу все его характеристики повышаются на 20%.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "InForest", UnlockedAction = ActionType.Attack, ScoreModifier = 30f },
            new PerkEffect { Condition = "InForest", UnlockedAction = ActionType.Flee, ScoreModifier = -30f }
        }, null, "Хозяин Леса"));

    // ──────────────────────────────────────────────
    // 13. Проклятый амулет
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Проклятый амулет", PerkType.Secret,
        "Носит амулет, который приносит несчастья. Раз в 10 ходов получает случайный дебафф.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Проклятый"));

    // ──────────────────────────────────────────────
    // 14. Брат по оружию
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Брат по оружию", PerkType.Family,
        "Его брат служит в том же войске. Если они в одном отряде, оба получают способность «Братский удар».",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "BrotherNearby", UnlockedAction = ActionType.Attack, ScoreModifier = 30f }
        }, null, "Братство"));

    // ──────────────────────────────────────────────
    // 15. Бывший надзиратель
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Бывший надзиратель", PerkType.FormerFaction,
        "Работал в тюрьме Магистра. При встрече с заключёнными или бывшими коллегами впадает в ярость.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsPrisoner", UnlockedAction = ActionType.Attack, ScoreModifier = 40f },
            new PerkEffect { Condition = "EnemyIsMagister", UnlockedAction = ActionType.Attack, ScoreModifier = 20f }
        }, null, "Тюремщик"));

    // ──────────────────────────────────────────────
    // 16. Спасённый маг
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Спасённый маг", PerkType.Debtor,
        "Маг спас ему жизнь от проклятия. Получает +30% к защите от магии, но боится темноты.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "InDarkness", UnlockedAction = ActionType.Flee, ScoreModifier = 50f }
        }, null, "Чародей"));

    // ──────────────────────────────────────────────
    // 17. Чемпион (эволюция «Побеждённого чемпиона»)
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Чемпион", PerkType.Secret,
        "Вернул свой титул в честном бою. Получает уважение союзников и постоянный бафф к атаке.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "Always", UnlockedAction = ActionType.Attack, ScoreModifier = 20f }
        }, null, "Чемпион"));

    // ──────────────────────────────────────────────
    // 18. Отступник-Магистр
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Отступник-Магистр", PerkType.FormerFaction,
        "Сбежал из Ордена Магистров, прихватив секретные знания. Магистры объявили на него охоту.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsMagister", UnlockedAction = ActionType.Attack, ScoreModifier = 50f }
        }, null, "Еретик"));

    // ──────────────────────────────────────────────
    // 19. Вечный странник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Вечный странник", PerkType.Secret,
        "Не может оставаться на одном месте. Скорость передвижения повышена на 15%, но защита в гарнизоне снижена.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "InGarrison", UnlockedAction = ActionType.Flee, ScoreModifier = 30f }
        }, null, "Скиталец"));

    // ──────────────────────────────────────────────
    // 20. Палач
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Палач", PerkType.Secret,
        "Приводил приговоры в исполнение. Может добить раненого врага, восстанавливая 5% HP, но союзники его боятся.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Исполнитель"));

        // ──────────────────────────────────────────────
    // 21. Проклятый странник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Проклятый странник", PerkType.Secret,
        "Носит древнее проклятие, которое усиливает его в бою, но медленно истощает здоровье.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "Always", UnlockedAction = ActionType.Attack, ScoreModifier = 15f }
        }, null, "Проклятый"));

    // ──────────────────────────────────────────────
    // 22. Гладиатор
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Гладиатор", PerkType.Secret,
        "Сражался на арене. Получает бонус к атаке, если бой длится дольше 3 ходов.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "LongBattle", UnlockedAction = ActionType.Attack, ScoreModifier = 25f }
        }, null, "Аренатор"));

    // ──────────────────────────────────────────────
    // 23. Мститель
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Мститель", PerkType.Rival,
        "Потерял семью из-за бандитов. При встрече с бандитами впадает в неконтролируемую ярость.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsBandit", UnlockedAction = ActionType.Attack, ScoreModifier = 100f },
            new PerkEffect { Condition = "EnemyIsBandit", UnlockedAction = ActionType.Flee, ScoreModifier = -100f }
        }, null, "Мститель"));

    // ──────────────────────────────────────────────
    // 24. Отравленный
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Отравленный", PerkType.Secret,
        "Был отравлен и выжил, получив сопротивляемость к ядам, но постоянную слабость.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "Always", UnlockedAction = ActionType.Attack, ScoreModifier = -5f }
        }, null, "Выживший"));

    // ──────────────────────────────────────────────
    // 25. Дрессировщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Дрессировщик", PerkType.Mentor,
        "При жизни дрессировал волков. Может приручать диких зверей в бою.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsBeast", UnlockedAction = ActionType.Attack, ScoreModifier = -50f },
            new PerkEffect { Condition = "EnemyIsBeast", UnlockedAction = ActionType.SaveAlly, ScoreModifier = 50f }
        }, null, "Повелитель Зверей"));

    // ──────────────────────────────────────────────
    // 26. Контрабандист
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Контрабандист", PerkType.Secret,
        "Знает тайные тропы и умеет находить редкие товары. Шанс найти дополнительный предмет при сборе добычи.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Теневой Торговец"));

    // ──────────────────────────────────────────────
    // 27. Священник-расстрига
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Священник-расстрига", PerkType.FormerFaction,
        "Был священником, но разуверился. Его молитвы могут как исцелить, так и навредить.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 }
        }, null, "Отступник"));

    // ──────────────────────────────────────────────
    // 28. Ныряльщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Ныряльщик", PerkType.Secret,
        "Провёл жизнь в море. В водных локациях его скорость и атака повышены.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "NearWater", UnlockedAction = ActionType.Attack, ScoreModifier = 20f },
            new PerkEffect { Condition = "NearWater", UnlockedAction = ActionType.Flee, ScoreModifier = 20f }
        }, null, "Морской Волк"));

    // ──────────────────────────────────────────────
    // 29. Фальшивомонетчик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Фальшивомонетчик", PerkType.Secret,
        "Умеет подделывать монеты. Периодически создаёт фальшивое золото, которое может обмануть торговцев.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Чеканщик"));

    // ──────────────────────────────────────────────
    // 30. Сомнамбула
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Сомнамбула", PerkType.Secret,
        "Ходит во сне. Ночью может случайно покинуть лагерь, но если его разбудить во время битвы, впадает в берсерк.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "NightTime", UnlockedAction = ActionType.Attack, ScoreModifier = 40f } },
        null, "Лунатик"));

    // ──────────────────────────────────────────────
    // 31. Алхимик-неудачник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Алхимик-неудачник", PerkType.Secret,
        "Взорвал свою лабораторию. В бою может случайно создать взрыв, наносящий урон всем вокруг.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Взрывоопасный"));

    // ──────────────────────────────────────────────
    // 32. Двойник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Двойник", PerkType.Secret,
        "Был похож на известного преступника. Иногда враги принимают его за того самого и пытаются убить в первую очередь.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Ложная Тень"));

    // ──────────────────────────────────────────────
    // 33. Заклинатель змей
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Заклинатель змей", PerkType.Mentor,
        "Умеет управлять змеями. При встрече со змеями может переманить их на свою сторону.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsSnake", UnlockedAction = ActionType.Attack, ScoreModifier = -100f }
        }, null, "Змеиный Язык"));

    // ──────────────────────────────────────────────
    // 34. Должник короны
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Должник короны", PerkType.Debtor,
        "Должен служить королю. Если в отряде есть персонаж с королевским титулом, его приказы выполняются беспрекословно.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "RoyalCommander", UnlockedAction = ActionType.ObeyCommand, ScoreModifier = 300f }
        }, null, "Королевский Слуга"));

    // ──────────────────────────────────────────────
    // 35. Охотник за головами
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Охотник за головами", PerkType.Rival,
        "Ищет конкретного человека. При встрече с целью получает невероятную мощь.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "TargetFound", UnlockedAction = ActionType.Attack, ScoreModifier = 200f }
        }, null, "Охотник"));

    // ──────────────────────────────────────────────
    // 36. Некромант-неудачник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Некромант-неудачник", PerkType.Secret,
        "Пытался поднять армию мёртвых, но воскресил только хомячка. Теперь его атаки иногда призывают хомяка-зомби.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Хомячий Лорд"));

    // ──────────────────────────────────────────────
    // 37. Слепой мудрец
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Слепой мудрец", PerkType.Secret,
        "Лишён зрения, но чувствует ложь. Не может атаковать, но его советы повышают защиту всего отряда.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "Always", UnlockedAction = ActionType.Attack, ScoreModifier = -100f },
            new PerkEffect { Condition = "Always", UnlockedAction = ActionType.SaveAlly, ScoreModifier = 30f }
        }, null, "Провидец"));

    // ──────────────────────────────────────────────
    // 38. Вечный optimist
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Вечный оптимист", PerkType.Secret,
        "Даже в смерти видит хорошее. Его присутствие повышает мораль союзников, но сам он получает больше урона.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "Always", UnlockedAction = ActionType.Attack, ScoreModifier = -10f }
        }, null, "Солнечный Луч"));

    // ──────────────────────────────────────────────
    // 39. Монах-молчальник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Монах-молчальник", PerkType.Secret,
        "Дал обет молчания. Его удары беззвучны и не привлекают внимания врагов.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "Always", UnlockedAction = ActionType.Attack, ScoreModifier = 10f }
        }, null, "Тихий Убийца"));

    // ──────────────────────────────────────────────
    // 40. Повар-отравитель
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Повар-отравитель", PerkType.Secret,
        "Был придворным поваром и знал толк в ядах. Его атаки имеют шанс отравить врага.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Шепчущий Яд"));

    // ──────────────────────────────────────────────
    // 41. Картограф
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Картограф", PerkType.Secret,
        "Составлял карты подземелий. Знает расположение ловушек и на 20% реже в них попадается.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Чертёжник"));

    // ──────────────────────────────────────────────
    // 42. Шут
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Шут", PerkType.Secret,
        "Веселил королей. Может рассмешить врага, временно снижая его атаку, но иногда шутка проваливается.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Королевский Шут"));

    // ──────────────────────────────────────────────
    // 43. Кукольник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Кукольник", PerkType.Secret,
        "Управлял марионетками. Может вселиться в мёртвое тело врага и управлять им 1 ход.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Марионеточник"));

    // ──────────────────────────────────────────────
    // 44. Метеоролог
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Метеоролог", PerkType.Secret,
        "Предсказывал погоду. Чувствует изменение климата и получает баффы в зависимости от окружения.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Предсказатель"));

    // ──────────────────────────────────────────────
    // 45. Сборщик налогов
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Сборщик налогов", PerkType.Secret,
        "Собирал подати. Его атаки имеют шанс украсть золото у врага.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Налоговик"));

    // ──────────────────────────────────────────────
    // 46. Вор-карманник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Вор-карманник", PerkType.Secret,
        "Мастерски обчищал карманы. Может украсть предмет у врага перед боем.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Карманник"));

    // ──────────────────────────────────────────────
    // 47. Танцор
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Танцор", PerkType.Secret,
        "Танцевал на сцене. Его уклонение повышено, а атаки выглядят как танец.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "Always", UnlockedAction = ActionType.Flee, ScoreModifier = 20f }
        }, null, "Танцующий"));

    // ──────────────────────────────────────────────
    // 48. Фонарщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Фонарщик", PerkType.Secret,
        "Зажигал уличные фонари. В тёмных локациях излучает свет, ослабляя врагов-нежить.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "InDarkness", UnlockedAction = ActionType.Attack, ScoreModifier = 20f }
        }, null, "Светоносный"));

    // ──────────────────────────────────────────────
    // 49. Кузнец-оружейник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Кузнец-оружейник", PerkType.Secret,
        "Ковал оружие. Его атаки имеют шанс сломать оружие врага.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Оружейник"));

    // ──────────────────────────────────────────────
    // 50. Последний выживший
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Последний выживший", PerkType.Secret,
        "Единственный, кто выжил после битвы. Когда он остаётся один против врагов, его сила удваивается.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "LastAlive", UnlockedAction = ActionType.Attack, ScoreModifier = 200f }
        }, null, "Одинокий Волк"));

        // ──────────────────────────────────────────────
    // 51. Скорбящий отец
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Скорбящий отец", PerkType.Family,
        "Потерял сына в бою и теперь винит себя. При получении урона союзником впадает в ярость.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "AllyDamaged", UnlockedAction = ActionType.Attack, ScoreModifier = 40f }
        }, null, "Отец"));

    // ──────────────────────────────────────────────
    // 52. Травница
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Травница", PerkType.Secret,
        "Знала лечебные травы. Может исцелять союзников вне боя, используя найденные растения.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Знахарка"));

    // ──────────────────────────────────────────────
    // 53. Конюх
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Конюх", PerkType.Secret,
        "Ухаживал за лошадьми. При передвижении верхом скорость отряда увеличивается.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Всадник"));

    // ──────────────────────────────────────────────
    // 54. Писарь
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Писарь", PerkType.Secret,
        "Вёл летописи. Может расшифровывать древние тексты, открывая секретные локации.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Летописец"));

    // ──────────────────────────────────────────────
    // 55. Звонарь
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Звонарь", PerkType.Secret,
        "Звонил в церковные колокола. Его крики в бою имеют шанс оглушить врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Колокол"));

    // ──────────────────────────────────────────────
    // 56. Винодел
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Винодел", PerkType.Secret,
        "Делал лучшее вино в долине. Может опьянить врага, снижая его точность.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Винный Маг"));

    // ──────────────────────────────────────────────
    // 57. Каменщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Каменщик", PerkType.Secret,
        "Строил замки. В обороне его защита повышается на 30%.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "InDefense", UnlockedAction = ActionType.Flee, ScoreModifier = -50f }
        }, null, "Строитель"));

    // ──────────────────────────────────────────────
    // 58. Рыбак
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Рыбак", PerkType.Secret,
        "Ловил рыбу сетями. Может опутать врага, временно лишив его возможности двигаться.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Рыболов"));

    // ──────────────────────────────────────────────
    // 59. Мельник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Мельник", PerkType.Secret,
        "Молол зерно. Его атаки поднимают облако пыли, ослепляющее врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Пыльный"));

    // ──────────────────────────────────────────────
    // 60. Пастух
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Пастух", PerkType.Secret,
        "Пас овец. Может приручать диких животных, как и Дрессировщик, но только травоядных.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsHerbivore", UnlockedAction = ActionType.Attack, ScoreModifier = -50f }
        }, null, "Пастырь"));

    // ──────────────────────────────────────────────
    // 61. Стеклодув
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Стеклодув", PerkType.Secret,
        "Выдувал стекло. Его атаки имеют шанс создать острую стеклянную крошку, наносящую дополнительный урон.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Стеклянный"));

    // ──────────────────────────────────────────────
    // 62. Гончар
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Гончар", PerkType.Secret,
        "Лепил горшки. Может создавать глиняные ловушки, замедляющие врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Горшечник"));

    // ──────────────────────────────────────────────
    // 63. Кожевник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Кожевник", PerkType.Secret,
        "Выделывал кожу. Его броня прочнее обычной, но он уязвим к огню.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Кожевенник"));

    // ──────────────────────────────────────────────
    // 64. Плотник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Плотник", PerkType.Secret,
        "Строил дома. Может быстро возводить баррикады, давая союзникам укрытие.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Строитель"));

    // ──────────────────────────────────────────────
    // 65. Пекарь
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Пекарь", PerkType.Secret,
        "Пёк хлеб. Может бросить горячую буханку, отвлекая врага.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Хлебопёк"));

    // ──────────────────────────────────────────────
    // 66. Свечник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Свечник", PerkType.Secret,
        "Делал свечи. В тёмных локациях его свет усиливает атаку союзников.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "InDarkness", UnlockedAction = ActionType.Attack, ScoreModifier = 15f }
        }, null, "Осветитель"));

    // ──────────────────────────────────────────────
    // 67. Красильщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Красильщик", PerkType.Secret,
        "Красил ткани. Может создавать дымовую завесу, скрывая отряд.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Красильный"));

    // ──────────────────────────────────────────────
    // 68. Парфюмер
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Парфюмер", PerkType.Secret,
        "Создавал духи. Его ароматы могут как привлечь, так и отпугнуть врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Ароматик"));

    // ──────────────────────────────────────────────
    // 69. Ювелир
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Ювелир", PerkType.Secret,
        "Обрабатывал драгоценности. Находит на 20% больше золота при сборе добычи.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Драгоценный"));

    // ──────────────────────────────────────────────
    // 70. Часовщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Часовщик", PerkType.Secret,
        "Чинил часы. Его чувство времени позволяет отряду действовать на 5% быстрее.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Хронометрист"));

    // ──────────────────────────────────────────────
    // 71. Архивариус
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Архивариус", PerkType.Secret,
        "Хранил древние знания. Может идентифицировать магические предметы.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Хранитель Знаний"));

    // ──────────────────────────────────────────────
    // 72. Гробовщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Гробовщик", PerkType.Secret,
        "Хоронил мёртвых. Нежить атакует его с меньшей вероятностью.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsUndead", UnlockedAction = ActionType.Attack, ScoreModifier = -30f }
        }, null, "Могильщик"));

    // ──────────────────────────────────────────────
    // 73. Заключённый
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Заключённый", PerkType.FormerFaction,
        "Сбежал из тюрьмы. Знает, как снимать оковы и открывать замки.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Беглец"));

    // ──────────────────────────────────────────────
    // 74. Глашатай
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Глашатай", PerkType.Secret,
        "Объявлял королевские указы. Его голос вдохновляет союзников, повышая их атаку на 10%.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Зычный"));

    // ──────────────────────────────────────────────
    // 75. Фокусник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Фокусник", PerkType.Secret,
        "Показывал фокусы. Может создать иллюзию, отвлекая врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Иллюзионист"));

    // ──────────────────────────────────────────────
    // 76. Акробат
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Акробат", PerkType.Secret,
        "Выступал в цирке. Его уклонение повышено на 15%.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Гимнаст"));

    // ──────────────────────────────────────────────
    // 77. Силач
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Силач", PerkType.Secret,
        "Поднимал тяжести. Его атаки имеют шанс оглушить врага.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Тяжеловес"));

    // ──────────────────────────────────────────────
    // 78. Бегун
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Бегун", PerkType.Secret,
        "Участвовал в марафонах. Скорость передвижения повышена на 10%.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Спринтер"));

    // ──────────────────────────────────────────────
    // 79. Пловец
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Пловец", PerkType.Secret,
        "Переплывал реки. В водных локациях не получает штрафов к скорости.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "NearWater", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Пловец"));

    // ──────────────────────────────────────────────
    // 80. Скалолаз
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Скалолаз", PerkType.Secret,
        "Покорял горы. Может забираться на стены, открывая новые пути.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Альпинист"));

        // ──────────────────────────────────────────────
    // 81. Бывший раб
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Бывший раб", PerkType.Debtor,
        "Освободился из рабства и теперь ценит свободу превыше всего. Не может быть порабощён магией.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Освобождённый"));

    // ──────────────────────────────────────────────
    // 82. Смотритель маяка
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Смотритель маяка", PerkType.Secret,
        "Годами жил на одиноком маяке. Его зрение позволяет замечать врагов на большем расстоянии.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Смотритель"));

    // ──────────────────────────────────────────────
    // 83. Шахтёр
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Шахтёр", PerkType.Secret,
        "Добывал руду в глубоких шахтах. В подземельях находит больше полезных ископаемых.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "InCave", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Шахтёр"));

    // ──────────────────────────────────────────────
    // 84. Дровосек
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Дровосек", PerkType.Secret,
        "Рубил лес. Его атаки по врагам из дерева или растений наносят удвоенный урон.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsPlant", UnlockedAction = ActionType.Attack, ScoreModifier = 50f }
        }, null, "Лесоруб"));

    // ──────────────────────────────────────────────
    // 85. Пчеловод
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Пчеловод", PerkType.Secret,
        "Разводил пчёл. Может выпустить рой, который отвлечёт врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Пасечник"));

    // ──────────────────────────────────────────────
    // 86. Виноградарь
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Виноградарь", PerkType.Secret,
        "Выращивал виноград. Его присутствие повышает восстановление здоровья союзников.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Виноградарь"));

    // ──────────────────────────────────────────────
    // 87. Садовник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Садовник", PerkType.Secret,
        "Ухаживал за садом. Может выращивать временные ловушки-растения на поле боя.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Садовник"));

    // ──────────────────────────────────────────────
    // 88. Гончар
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Гончар", PerkType.Secret,
        "Лепил горшки. Может создавать глиняные ловушки, замедляющие врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Горшечник"));

    // ──────────────────────────────────────────────
    // 89. Кузнец
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Кузнец", PerkType.Secret,
        "Ковал оружие. Его атаки имеют шанс сломать оружие врага.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Оружейник"));

    // ──────────────────────────────────────────────
    // 90. Плотник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Плотник", PerkType.Secret,
        "Строил дома. Может быстро возводить баррикады, давая союзникам укрытие.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Строитель"));

    // ──────────────────────────────────────────────
    // 91. Мельник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Мельник", PerkType.Secret,
        "Молол зерно. Его атаки поднимают облако пыли, ослепляющее врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Пыльный"));

    // ──────────────────────────────────────────────
    // 92. Пекарь
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Пекарь", PerkType.Secret,
        "Пёк хлеб. Может бросить горячую буханку, отвлекая врага.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Хлебопёк"));

    // ──────────────────────────────────────────────
    // 93. Свечник
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Свечник", PerkType.Secret,
        "Делал свечи. В тёмных локациях его свет усиливает атаку союзников.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "InDarkness", UnlockedAction = ActionType.Attack, ScoreModifier = 15f }
        }, null, "Осветитель"));

    // ──────────────────────────────────────────────
    // 94. Красильщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Красильщик", PerkType.Secret,
        "Красил ткани. Может создавать дымовую завесу, скрывая отряд.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Красильный"));

    // ──────────────────────────────────────────────
    // 95. Парфюмер
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Парфюмер", PerkType.Secret,
        "Создавал духи. Его ароматы могут как привлечь, так и отпугнуть врагов.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Ароматик"));

    // ──────────────────────────────────────────────
    // 96. Ювелир
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Ювелир", PerkType.Secret,
        "Обрабатывал драгоценности. Находит на 20% больше золота при сборе добычи.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Драгоценный"));

    // ──────────────────────────────────────────────
    // 97. Часовщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Часовщик", PerkType.Secret,
        "Чинил часы. Его чувство времени позволяет отряду действовать на 5% быстрее.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Хронометрист"));

    // ──────────────────────────────────────────────
    // 98. Архивариус
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Архивариус", PerkType.Secret,
        "Хранил древние знания. Может идентифицировать магические предметы.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Хранитель Знаний"));

    // ──────────────────────────────────────────────
    // 99. Гробовщик
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Гробовщик", PerkType.Secret,
        "Хоронил мёртвых. Нежить атакует его с меньшей вероятностью.",
        null,
        new List<PerkEffect> {
            new PerkEffect { Condition = "EnemyIsUndead", UnlockedAction = ActionType.Attack, ScoreModifier = -30f }
        }, null, "Могильщик"));

    // ──────────────────────────────────────────────
    // 100. Заключённый
    // ──────────────────────────────────────────────
    perks.Add(CreatePerk("Заключённый", PerkType.FormerFaction,
        "Сбежал из тюрьмы. Знает, как снимать оковы и открывать замки.",
        null,
        new List<PerkEffect> { new PerkEffect { Condition = "Always", UnlockedAction = null, ScoreModifier = 0 } },
        null, "Беглец"));
        
            
    return perks;
}

// Вспомогательный метод с титулом
private static SoulPerk CreatePerk(string name, PerkType type, string desc, string relatedName, List<PerkEffect> effects, VoiceModifier voiceMod, string title = null)
{
    var perk = ScriptableObject.CreateInstance<SoulPerk>();
    perk.PerkName = name;
    perk.Type = type;
    perk.Description = desc;
    perk.RelatedCharacterName = relatedName;
    perk.Effects = effects ?? new List<PerkEffect>();
    if (voiceMod != null) perk.VoiceModifier = voiceMod;
    if (!string.IsNullOrEmpty(title)) perk.Title = title; // ← нужно поле в SoulPerk
    return perk;
}
    }
}
#endif
