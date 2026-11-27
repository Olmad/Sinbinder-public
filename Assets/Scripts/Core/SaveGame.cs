// Перевод: текст через Loc
using System.Collections.Generic;

namespace Sinbinder.Core
{
    /// <summary>Состояние собранной души.</summary>
    [System.Serializable]
    public class SavedSoul
    {
        public string Id;
        public string PersonId;
        // Authored kin group, independent of custody and personal identity.
        public string Kin;
        public string Name;
        public int Moral;
        public int Level;
        public float[] Spectra;
        public int Quality;
        public bool HasMemorySeed;
        public MemorySeed Memory;
        // Experienced events carried by an unbound soul, not the living roster ledger.
        public List<Sinbinder.AOS.SavedMemory> PastMemories;

        /// <summary>
        /// Пол, ремесло и заслуженное при жизни имя.
        ///
        /// Добавлены 18 сентября: полка теряла всё три. Душа, записанная
        /// и прочитанная обратно, вставала мужчиной без ремесла и без
        /// имени — а <b>отряд пол сохранял всегда</b> (<c>SavedMember</c>),
        /// и это расхождение и выдало недосмотр.
        ///
        /// Цена была не косметической: женщина на полке переставала быть
        /// женщиной (правило автора о том, что женские лица уникальны),
        /// охотник — охотником, и первая фраза поднятого теряла и руки,
        /// и имя.
        ///
        /// Версию снимка **не поднимаем нарочно**: у старых записей этих
        /// полей нет, и читаются они нулями — мужчина, ремесла нет, имени
        /// нет. Ровно то, что старые записи и означали. Поднять версию
        /// значило бы отнять у игрока его сохранения ради полей, которые
        /// и так вырождаются верно.
        /// </summary>
        public int Gender;
        public int Trade;
        public string Title;
    }

    /// <summary>Запись отряда, как она ложится в файл.</summary>
    [System.Serializable]
    public class SavedMember
    {
        public string Name;
        public string WarriorId;
        public bool JoinedAfterRaid;
        // Читается из записей прежней ветки. Новые записи хранят эмоции в AosEmotions.
        public List<Sinbinder.AOS.SavedEmotion> Emotions;
        public int Sin;
        public int Moral;
        public int Gender;
        public float Intensity;
        // Explicit presence distinguishes old saves from a captured all-zero vector.
        public bool HasSoulSpectra;
        public float[] SoulSpectra;
        public int SoulLevel;
        // Absent in legacy saves; do not infer identity or kin from the name.
        public string SoulId;
        public string SoulPersonId;
        public string SoulKin;
        public float Loyalty;
        // New saves preserve a living warrior's injuries. Legacy zero = full.
        public float HealthFraction;
        public int UnpaidMissions;
        public float Leadership;
        public bool IsCommander;
        public bool IsCandidate;
        public string Unavailable;

        // Надетое и карман (docs/34-GEAR.md §9.4). Вещь — [Serializable],
        // JsonUtility пишет её список как есть.
        public List<Inventory.InventoryItem> Gear = new List<Inventory.InventoryItem>();
        public bool HasEquipmentLayout;
        public List<Inventory.EquippedItem> Equipment;
        public int Pocket;

        /// <summary>
        /// Ушёл на вылазку с советом. Добавлено 25 сентября: без флага
        /// «С начала доли» в разгроме и любая запись после совета
        /// возвращали ушедших в лагерь — старшего не было, Карган снова
        /// звал к совету, эпилог терял его итог (14-HANDOFF §105.7).
        /// Версию снимка не поднимаем — по той же причине, что у полки:
        /// старые записи читаются «не уходил», и это ровно то, что в них
        /// и лежало.
        /// </summary>
        public bool IsAway;

        /// <summary>
        /// Ремесло, братство и слава. Добавлены 25 сентября: в составе они
        /// живут с 24-го (<c>SquadRoster.Remember</c> несёт их между долями),
        /// а в запись не попадали — «С начала доли» и загрузка возвращали
        /// братьев чужими, лучника без колчана, Каргана без славы
        /// (14-HANDOFF §105.13). Версию не поднимаем: старые записи
        /// читаются «без ремесла, не брат, без славы».
        /// </summary>
        public int Trade;
        public bool Brother;
        public bool Legend;

        /// <summary>
        /// История сгенерированного (как умер и как попал к нам, docs/53-GENERATOR.md §11).
        /// До 23.11 в запись не шла: после загрузки выкованный воин вспоминал чужую жизнь —
        /// «по составу лагеря». Флаг — потому что JsonUtility не пишет null у класса.
        /// </summary>
        public bool HasSeed;
        public MemorySeed Seed;
        // Legacy records omit this field. Captured absence must not acquire a camp biography.
        public bool SeedCaptured;

        /// <summary>
        /// Подвиги, уважение и страх (ReputationData) — путь к званию. До 23.11 жили только
        /// на теле: смена доли и загрузка обнуляли счёт подвигов, звание сохранялось.
        /// </summary>
        public List<AOS.DeedRecord> Deeds = new List<AOS.DeedRecord>();
        public float Respect;
        public float Fear;

        /// <summary>
        /// Тело (<see cref="ShellType"/> числом). С 25 сентября отряд — не одни
        /// скелеты (§107). Старые записи читаются нулём — скелетом, каким
        /// тогда был весь отряд.
        /// </summary>
        public int Shell;

        /// <summary>
        /// Заслуженное имя словом («Костекоп»). С 16 октября: в составе его
        /// не было, и титул терялся при каждой смене доли. Старые записи
        /// читаются пустым — «имени нет», что в них и лежало.
        /// </summary>
        public string Title;
    }

    /// <summary>
    /// Снимок игры.
    ///
    /// Сохраняется <b>состояние склепа между вылазками</b>: отряд, полка
    /// душ, казна, улучшения. Положение тел и текущий ход боя
    /// остаются в сцене.
    ///
    /// Плоские поля и массивы: <c>JsonUtility</c> не умеет ни словари,
    /// ни свойства, ни вложенные обобщения. Узнаётся это обычно на пустом
    /// файле сохранения, который записался без единой ошибки.
    /// </summary>
    [System.Serializable]
    public class SavedFallenBag
    {
        public string Scene;
        public UnityEngine.Vector3 Position;
        public string Owner;
        public string OwnerId;
        public List<Inventory.InventoryItem> Items = new List<Inventory.InventoryItem>();
        public Inventory.InventoryItem Memento;
        public int Gold;
        public bool Decided;
        public bool Away;
        public float RemainingAway = 60f;
    }

    [System.Serializable]
    public class SaveGame
    {
        /// <summary>
        /// Номер уклада. Файл, записанный старой игрой, обязан быть
        /// опознан как старый, а не прочитан наполовину: половина
        /// состояния хуже, чем ничего, потому что выглядит целой.
        /// </summary>
        public int Version = Current;

        public const int Current = 1;

        public string Scene;

        public List<SavedMember> Squad = new();
        public List<SavedSoul> Shelf = new();
        public SavedSoulCustody SoulCustody;

        public int Gold;

        /// <summary>
        /// Мешок Греховода (docs/34-GEAR.md §9.1). До 24 сентября не писался:
        /// загрузка из другой сцены теряла всё, что Греховод нёс.
        /// </summary>
        public List<Inventory.InventoryItem> Bag = new List<Inventory.InventoryItem>();

        /// <summary>
        /// Physical uncollected corpse bags on each map, including contents
        /// and time remaining if the player is currently on another map.
        /// Old saves without this field are treated as having no bags.
        /// </summary>
        public List<SavedFallenBag> FallenBags = new List<SavedFallenBag>();

        // Progress on two personally visitable expedition maps.
        // Absent on old save files: these locations start unvisited.
        public List<Sinbinder.Gameplay.SavedPersonalExpedition> PersonalExpeditions =
            new List<Sinbinder.Gameplay.SavedPersonalExpedition>();
        public bool CryptSandboxOpened;

        // Выжившие стражи личных вылазок (Survivors, план 69 §7). В старых сохранениях нет — никто не помнит.
        public List<Sinbinder.Gameplay.SavedSurvivor> Survivors = new List<Sinbinder.Gameplay.SavedSurvivor>();
        // Время мира в походах (Sorties). В старых сохранениях 0.
        public int Sorties;

        // Память AOS отряда и выживших стражей (MemoryProcessor): из неё считаются отношения.
        // До 23.11 не писалась. В старых сохранениях нет — никто ничего не помнит, как и было.
        public List<Sinbinder.AOS.SavedMemory> AosMemories = new List<Sinbinder.AOS.SavedMemory>();
        public List<Sinbinder.AOS.SavedEmotion> AosEmotions = new List<Sinbinder.AOS.SavedEmotion>();
        // An explicit empty modern snapshot must not revive legacy member emotions.
        public bool HasAosEmotions;

        // Здоровье Греховода. 0 — старая запись: цел (ExpeditionHealthRules.RestoreFraction).
        public float HeroHealth;

        // Older crypt saves record expeditions only after entering the sandbox.
        internal bool OpensCryptSandbox => Scene == Sinbinder.Crypt.CryptMap.SceneName &&
            (CryptSandboxOpened || AutoExpeditions?.Count > 0 || ExpeditionReplays?.Count > 0 ||
             PersonalExpeditions?.Count > 0 || !string.IsNullOrEmpty(AutoExpeditionReport));

        // Delayed automatic operations, including assigned warriors and travel stage.
        public List<Sinbinder.Crypt.SavedAutoExpedition> AutoExpeditions =
            new List<Sinbinder.Crypt.SavedAutoExpedition>();
        // Last known expedition event, including events while player was away
        // from the crypt. Absent in earlier saves = no report.
        public string AutoExpeditionReport;
        // Actual expedition experiences, keyed by persistent roster identity.
        public List<Sinbinder.Gameplay.ExpeditionMemoryEntry> ExpeditionMemories =
            new List<Sinbinder.Gameplay.ExpeditionMemoryEntry>();
        public List<Sinbinder.Gameplay.ExpeditionReplayData> ExpeditionReplays =
            new List<Sinbinder.Gameplay.ExpeditionReplayData>();

        /// <summary>
        /// Открыт ли сундук Марги и что в нём осталось. Без этого загрузка
        /// в лагерь из другой сцены открывала сундук заново — и его горсть
        /// монет ложилась в кошель второй раз.
        /// </summary>
        public bool ChestLooted;
        public List<Inventory.InventoryItem> Chest = new List<Inventory.InventoryItem>();

        public bool[] Installed;
        public bool[] Brought;

        /// <summary>
        /// Какие уроки стоп-кадром уже были (docs/36-LESSONS.md): урок —
        /// один раз за игру, и загрузка не должна показывать его снова.
        /// Имена, а не номера: запись переживёт новый урок в середине списка.
        /// </summary>
        public List<string> Lessons = new List<string>();

        /// <summary>
        /// Был ли режим обязательств. Хранится в самом файле, а не
        /// в настройках: иначе снимок, сделанный в ответственной игре,
        /// открывался бы в обычной — и весь смысл режима пропадал бы
        /// вместе с одной галочкой в меню.
        /// </summary>
        public bool Commitment;

        /// <summary>
        /// Чем эта запись была. Строится при снимке и хранится в файле:
        /// иначе список сохранений пришлось бы собирать, открывая каждое,
        /// а открывать чужой уклад мы как раз отказываемся.
        ///
        /// Без цифр, как и всё, что видит игрок: «Склеп · девять воинов ·
        /// долгов нет». Номер гнезда игроку тоже цифра — гнёзда
        /// называются тем, что в них лежит.
        /// </summary>
        public string Label;
    }
}
