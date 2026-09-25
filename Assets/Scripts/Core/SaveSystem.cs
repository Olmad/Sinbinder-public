using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;

namespace Sinbinder.Core
{
    /// <summary>
    /// Снять состояние игры и вернуть его обратно.
    ///
    /// Состояние живёт в пяти местах и ни одно из них себя не хранит:
    /// отряд (<see cref="SquadRoster"/>), полка душ
    /// (<see cref="SoulManager"/>), казна
    /// (<see cref="Inventory.PlayerInventory"/>), улучшения склепа
    /// (<see cref="CryptUpgrades"/>) и открытая сцена. Собирать это
    /// по кускам из пяти вызывающих значило бы завести пятую и шестую
    /// правду о том, что такое «игра» — поэтому сборка одна и она здесь.
    ///
    /// <b>Чего снимок не содержит.</b> Середины боя. Бой длится минуту,
    /// решает его движок, и скрытого состояния у движка нет: те же души
    /// в том же положении дадут тот же исход. Переигрывать бой незачем —
    /// а вот переигрывать вылазку очень хочется, и ровно поэтому есть
    /// <see cref="Commitment"/>.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>
        /// Гнёзда свободной игры. Их немного и они постоянные: список,
        /// который растёт без края, — это архив, а не сохранение.
        /// </summary>
        public static readonly string[] Slots = { "первое", "второе", "третье", "быстрое" };

        /// <summary>Быстрое гнездо: в него пишет F5.</summary>
        public const string Quick = "быстрое";

        /// <summary>
        /// Единственное гнездо ответственной игры.
        ///
        /// Отдельное от гнёзд свободной намеренно: иначе одна партия
        /// затирала бы записи другой, а вернуться к свободной игре
        /// после ответственной — законно.
        /// </summary>
        public const string Bound = "с обязательством";

        private static string Folder =>
            Path.Combine(Application.persistentDataPath, "saves");

        /// <summary>Путь к гнезду по имени.</summary>
        public static string PathOf(string slot)
        {
            // Имя гнезда приходит из нашего же списка, но путь собирается
            // из строки — и однажды кто-нибудь передаст сюда чужую.
            string safe = string.IsNullOrEmpty(slot) ? Quick : slot;
            foreach (char bad in Path.GetInvalidFileNameChars())
                safe = safe.Replace(bad, '_');

            return Path.Combine(Folder, safe + ".sinbin");
        }

        /// <summary>
        /// Куда пишет F5 сейчас. В ответственной игре — в её единственное
        /// гнездо: там «быстрое сохранение» и «сохранение» одно и то же.
        /// </summary>
        public static string QuickPath =>
            PathOf(Commitment.On ? Bound : Quick);

        /// <summary>Гнёзда, доступные сейчас. В ответственной игре — одно.</summary>
        public static string[] Available()
        {
            return Commitment.On ? new[] { Bound } : Slots;
        }

        // ──────────────────────────────────
        // Снимок
        // ──────────────────────────────────

        public static SaveGame Snapshot()
        {
            var save = new SaveGame
            {
                Scene = Here,
                Gold = Inventory.PlayerInventory.Instance != null
                     ? Inventory.PlayerInventory.Instance.Gold : 0,
                Installed = CryptUpgrades.InstalledAll(),
                Brought = CryptUpgrades.BroughtAll(),
                Commitment = Commitment.On,
            };

            save.Label = Label(save);

            if (Inventory.PlayerInventory.Instance != null)
                save.Bag = new List<Inventory.InventoryItem>(Inventory.PlayerInventory.Instance.GetAllItems());
            save.ChestLooted = TrophyChest.Looted;
            save.Chest = TrophyChest.Remaining();

            foreach (var m in SquadRoster.Members)
            {
                save.Squad.Add(new SavedMember
                {
                    Name = m.Name,
                    Sin = (int)m.Sin,
                    Moral = (int)m.Moral,
                    Gender = (int)m.Gender,
                    Intensity = m.Intensity,
                    Loyalty = m.Loyalty,
                    UnpaidMissions = m.UnpaidMissions,
                    Leadership = m.Leadership,
                    IsCommander = m.IsCommander,
                    IsCandidate = m.IsCandidate,
                    Unavailable = m.Unavailable,
                    Gear = m.Gear != null ? new List<Inventory.InventoryItem>(m.Gear)
                                          : new List<Inventory.InventoryItem>(),
                    Pocket = m.Pocket,
                    IsAway = m.IsAway,
                    Trade = (int)m.Trade,
                    Brother = m.Brother,
                    Legend = m.Legend,
                    Shell = (int)m.Shell,
                });
            }

            var souls = SoulManager.Instance;
            if (souls != null)
            {
                foreach (var kept in souls.Harvested)
                {
                    if (kept.Soul == null) continue;

                    save.Shelf.Add(Record(kept.Soul, kept.Quality));
                }
            }

            return save;
        }

        // ──────────────────────────────────
        // Возврат
        // ──────────────────────────────────

        // ──────────────────────────────────
        // Вернуться туда, где записался
        // ──────────────────────────────────

        /// <summary>
        /// Пришли ли мы в эту сцену загрузкой, а не ходом истории.
        ///
        /// Живёт ровно одну смену сцены: <c>sceneLoaded</c> звучит после
        /// <c>Awake</c> новой сцены и до её <c>Start</c>, так что директор
        /// в своём <c>Awake</c> флаг видит, а следующая сцена — уже нет.
        /// Иначе флаг пережил бы загрузку, и настоящая новая игра после
        /// конца демо не сбросила бы отряд.
        /// </summary>
        public static bool Arriving { get; private set; }

        /// <summary>
        /// Какая доля идёт, если она не совпадает со сценой. Набег —
        /// событие лагеря (<see cref="RaidEvent"/>): сцена — лагерь, а доля —
        /// набег, и запись, сделанная посреди него, обязана вернуть в набег,
        /// а не к совету. Сбрасывается сменой сцены.
        /// </summary>
        public static string StagedScene { get; set; }

        /// <summary>
        /// Где игрок сейчас — в долях, а не в сценах: посреди разгрома это
        /// «набег», хотя открыт лагерь. По этому месту пишется запись и по
        /// нему же решается, грузить ли сцену при загрузке.
        /// </summary>
        public static string Here =>
            string.IsNullOrEmpty(StagedScene)
                ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                : StagedScene;

        /// <summary>
        /// Доля, которую надо развернуть в сцене, открытой загрузкой. Живёт
        /// от <see cref="ReturnTo"/> до <c>sceneLoaded</c>.
        /// </summary>
        private static string _unfold;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Listen()
        {
            Arriving = false;
            StagedScene = null;
            _unfold = null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Arrived;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += Arrived;
        }

        private static void Arrived(UnityEngine.SceneManagement.Scene scene,
                                    UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Arriving = false;
            StagedScene = null;

            // Начало доли — её отметка: сюда вернёт «С начала доли».
            // Сцена — пришедшая, а не «открытая»: в sceneLoaded Unity могла
            // ещё не сделать её открытой.
            MarkCheckpoint(scene.name);

            // Запись посреди разгрома: лагерь открыт — разгром разворачивается
            // в нём сейчас же, до Start сцены. Шар, совет и открытие лагеря
            // в своих Start видят, что идёт разгром, и молчат.
            string part = _unfold;
            _unfold = null;
            if (part == RaidEvent.SceneName) RaidEvent.Resume(scene);
        }

        // ──────────────────────────────────
        // Начало доли
        // ──────────────────────────────────

        /// <summary>
        /// Состояние в начале нынешней доли — в памяти, не в файле. Смерть
        /// Греховода или гибель отряда возвращают сюда кнопкой «С начала
        /// доли» (решение автора, 24 сентября): для демо на фестивале полный
        /// перезапуск пролога — это закрытое окно (docs/35-CRITIQUE.md п. 12).
        /// В игре с обязательством отметки не предлагают: переиграть смерть
        /// нельзя, в этом уговор.
        /// </summary>
        public static SaveGame Checkpoint { get; private set; }

        /// <summary>
        /// Отметить начало доли. Зовут приход в сцену и начало разгрома
        /// (<see cref="RaidEvent"/>). <paramref name="part"/> — доля; пусто —
        /// нынешняя (<see cref="Here"/>).
        /// </summary>
        public static void MarkCheckpoint(string part = null)
        {
            var save = Snapshot();
            if (!string.IsNullOrEmpty(StagedScene)) save.Scene = StagedScene;
            else if (!string.IsNullOrEmpty(part)) save.Scene = part;
            Checkpoint = save;
        }

        /// <summary>Можно ли сейчас вернуться к началу доли.</summary>
        public static bool CanRestartPart => Checkpoint != null && !Commitment.On;

        /// <summary>
        /// Вернуться к началу доли: отметка — и сцена заново, даже если это
        /// та же доля (иначе мёртвые остались бы мёртвыми, а охотники — на поле).
        /// </summary>
        public static bool RestartPart()
        {
            if (!CanRestartPart) return false;
            GamePauseController.Instance?.Unhalt();
            return ReturnTo(Checkpoint, reload: true);
        }

        /// <summary>
        /// Восстановить запись <b>и вернуться туда, где записался</b>.
        ///
        /// До 24 сентября загрузка возвращала состояние, но не место:
        /// <see cref="Restore"/> ставил отряд, золото и полку, а сцену
        /// из записи не читал никто. Самый вероятный путь игрока был
        /// худшим: запустил сборку, нажал «Продолжить» — и получил
        /// состояние склепа, стоя в лагере.
        ///
        /// <b>Порядок — сперва состояние, потом сцена.</b> Всё, что
        /// восстанавливается, смену сцены переживает (три одиночки
        /// держатся через <c>DontDestroyOnLoad</c>, три статические),
        /// и спавнеры новой сцены читают уже верный состав.
        ///
        /// <b>Ловушка, ради которой заведён <see cref="Arriving"/>.</b>
        /// Директор лагеря в своём <c>Awake</c> забывает отряд — это
        /// сброс новой игры. Загрузи лагерь без флага, и он молча
        /// выбросит только что восстановленную запись.
        ///
        /// <b>Загрузка в той же доле остаётся прежней</b> — состояние
        /// на месте, без перезагрузки. Перезагружать сцену значило бы
        /// заново проиграть её доли, а это уже решение, а не починка
        /// (разбор — 14-HANDOFF §56).
        /// </summary>
        public static bool ReturnTo(SaveGame save, bool reload = false)
        {
            if (!Restore(save)) return false;

            // Сравниваем доли, а не сцены. Посреди разгрома открыт лагерь,
            // и запись «лагерь до совета» по сцене совпала бы с ним: состав
            // вернулся бы, а охотники остались бы на поле. «С начала доли»
            // (reload) грузит сцену и в той же доле — начать её заново.
            if (string.IsNullOrEmpty(save.Scene)) return true;
            if (save.Scene == Here && !reload) return true;

            // Доля «набег» своей сцены не имеет: её открывает лагерь,
            // а разгром разворачивается в нём по приходу (Arrived).
            string scene = RaidEvent.HostOf(save.Scene);

            // Сцены нет в сборке — не падаем, а говорим. Состояние
            // вернули, место вернуть не можем, и игрок должен это знать.
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"[ЗАПИСЬ] Сцены «{scene}» нет в сборке: "
                               + "состояние возвращено, место — нет.");
                return true;
            }

            Arriving = true;
            _unfold = scene != save.Scene ? save.Scene : null;
            GamePauseController.Instance?.Resume();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            return true;
        }

        /// <summary>
        /// Вернуть состояние. Возвращает <c>false</c>, если снимок
        /// не от этой игры: молча прочитать половину хуже, чем не
        /// прочитать ничего, — половина выглядит целой.
        /// </summary>
        public static bool Restore(SaveGame save)
        {
            if (save == null) return false;

            if (save.Version != SaveGame.Current)
            {
                Debug.LogWarning($"[СОХРАНЕНИЕ] Файл другого уклада "
                               + $"({save.Version}, ждали {SaveGame.Current}). Не читаю.");
                return false;
            }

            var members = new List<SquadRoster.Member>();
            foreach (var m in save.Squad)
            {
                members.Add(new SquadRoster.Member
                {
                    Name = m.Name,
                    Sin = (SinType)m.Sin,
                    Moral = (MoralType)m.Moral,
                    Gender = (Gender)m.Gender,
                    Intensity = m.Intensity,
                    Loyalty = m.Loyalty,
                    UnpaidMissions = m.UnpaidMissions,
                    Leadership = m.Leadership,
                    IsCommander = m.IsCommander,
                    IsCandidate = m.IsCandidate,
                    Unavailable = m.Unavailable,
                    Gear = m.Gear != null ? new List<Inventory.InventoryItem>(m.Gear) : null,
                    Pocket = m.Pocket,
                    IsAway = m.IsAway,
                    Trade = (Trade)m.Trade,
                    Brother = m.Brother,
                    Legend = m.Legend,
                    Shell = (ShellType)m.Shell,
                });
            }

            SquadRoster.Set(members);
            CryptUpgrades.Restore(save.Installed, save.Brought);
            Commitment.Set(save.Commitment);

            Inventory.PlayerInventory.Instance?.SetGold(save.Gold);
            Inventory.PlayerInventory.Instance?.ReplaceItems(save.Bag);
            TrophyChest.Restore(save.ChestLooted, save.Chest);

            var souls = SoulManager.Instance;
            if (souls != null)
            {
                souls.ClearShelf();

                foreach (var s in save.Shelf)
                {
                    souls.PutBack(new SoulManager.Kept(Soul(s), (SoulQuality)s.Quality));
                }
            }

            return true;
        }

        // ──────────────────────────────────
        // Душа на полке: туда и обратно
        // ──────────────────────────────────

        /// <summary>
        /// Душа в запись. Вынесено отдельно вместе с починкой 18 сентября,
        /// чтобы самопроверка звала **тот же код**, что и игра: проверка,
        /// переписывающая перекладку своими руками, не проверяет ничего.
        ///
        /// Пол, ремесло и заслуженное имя терялись здесь целиком, а отряд
        /// (<see cref="SavedMember"/>) пол сохранял всегда — это
        /// расхождение и выдало недосмотр.
        /// </summary>
        public static SavedSoul Record(SoulData soul, SoulQuality quality)
        {
            if (soul == null) return null;

            return new SavedSoul
            {
                Name = soul.Name,
                Moral = (int)soul.Moral,
                Level = soul.Level,
                Spectra = soul.CopySpectra(),
                Quality = (int)quality,
                Gender = (int)soul.Gender,
                Trade = (int)soul.Trade,
                Title = soul.EarnedTitle,
            };
        }

        /// <summary>
        /// Запись обратно в душу. У старых записей новых полей нет,
        /// и читаются они нулями: мужчина, ремесла нет, имени нет —
        /// ровно то, что старая запись и означала.
        /// </summary>
        public static SoulData Soul(SavedSoul s)
        {
            if (s == null) return null;

            var soul = new SoulData(s.Name, (MoralType)s.Moral, s.Level,
                                    s.Spectra, null, (Gender)s.Gender);
            soul.SetTrade((Trade)s.Trade);
            soul.Remember(s.Title);
            return soul;
        }

        // ──────────────────────────────────
        // Файл
        // ──────────────────────────────────

        /// <summary>
        /// Чем назвать запись. Словами и по тому, что в ней лежит:
        /// «Склеп · девять воинов · трое в долгу».
        /// </summary>
        private static string Label(SaveGame save)
        {
            string where = Where(save.Scene);

            // Счёт словами берём у Leadership: он уже говорит об этом же
            // отряде теми же словами, и второй словарь чисел разошёлся
            // бы с ним на первой правке — там об этом прямо написано.
            string who = Leadership.Collective(save.Squad.Count);

            int owed = 0;
            foreach (var m in save.Squad) if (m.UnpaidMissions > 0) owed++;

            string debt = owed == 0
                ? "долгов нет"
                : $"{Leadership.Collective(owed)} в долгу";

            return $"{where} · {who} · {debt}";
        }

        /// <summary>
        /// Где игрок записался — <b>словами, а не именем файла</b>.
        ///
        /// Здесь стояло имя сцены как есть, и в списке сохранений игрок
        /// читал «Prologue_Camp · девятеро · долгов нет». Замысел был
        /// другой и записан прямо в <c>SaveGame.cs</c> над полем метки:
        /// «Склеп · девять воинов · трое в долгу». Игра, где игрок
        /// не видит чисел, показывала ему snake_case из проводника.
        ///
        /// Сцена, которой здесь нет, названа вслух один раз: молчаливое
        /// «Где-то» на новой сцене выглядело бы как забытая запись,
        /// а не как забытая строчка в этом списке.
        /// </summary>
        private static string Where(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return "Где-то";

            switch (scene)
            {
                case "Prologue_Camp":  return "Лагерь";
                case "Prologue_Raid":  return "Набег";
                case "Crypt_Entrance": return "Склеп";
                case "Crypt_Test":     return "Полигон";
            }

            if (!_toldAboutPlace)
            {
                _toldAboutPlace = true;
                Debug.LogWarning($"[ЗАПИСЬ] Сцена «{scene}» без человеческого "
                               + "имени — в списке сохранений она будет "
                               + "«Где-то». Добавьте её в SaveSystem.Where.");
            }

            return "Где-то";
        }

        private static bool _toldAboutPlace;

        public static bool Write(SaveGame save, string path)
        {
            if (save == null) return false;

            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(path, JsonUtility.ToJson(save, true));
                return true;
            }
            catch (IOException e)
            {
                // Отсутствие права записи — событие, а не ноль: молча
                // «сохранить» и не сохранить хуже любой ошибки.
                Debug.LogWarning($"[СОХРАНЕНИЕ] Не записалось: {e.Message}");
                return false;
            }
        }

        public static SaveGame Read(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                return JsonUtility.FromJson<SaveGame>(File.ReadAllText(path));
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[СОХРАНЕНИЕ] Не прочиталось: {e.Message}");
                return null;
            }
        }

        public static bool Exists(string path) => File.Exists(path);

        /// <summary>
        /// Стереть запись ответственной игры. Зовёт конец игры: Греховод пал,
        /// и в игре с обязательством переиграть это нельзя — ни быстрой
        /// загрузкой, ни «Продолжить» при следующем запуске (запись к тому
        /// мигу была сделана до смерти). В свободной игре записи не трогаем:
        /// грузиться когда угодно — её уговор.
        /// </summary>
        public static void EraseBound()
        {
            string path = PathOf(Bound);
            if (!File.Exists(path)) return;

            try
            {
                File.Delete(path);
                Debug.Log("[СОХРАНЕНИЕ] Греховод пал: запись игры с обязательством стёрта.");
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[СОХРАНЕНИЕ] Запись с обязательством не стёрлась: {e.Message}");
            }
        }

        /// <summary>
        /// Записать самой, без нажатия. Зовётся в названные моменты —
        /// пока такой момент один: отряд вернулся с вылазки.
        ///
        /// В свободной игре тоже пишет: точка, к которой игрок вернётся,
        /// если закроет игру, нужна в обоих режимах. Разница между
        /// режимами не в том, кто пишет, а в том, можно ли грузить.
        /// </summary>
        public static bool AutoSave()
        {
            bool ok = Write(Snapshot(), QuickPath);

            if (ok) Debug.Log("[СОХРАНЕНИЕ] Записано само.");
            return ok;
        }
    }
}
