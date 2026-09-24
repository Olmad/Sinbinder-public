// Assets/Scripts/UI/GearPanel.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;
using Sinbinder.Inventory;

namespace Sinbinder.UI
{
    /// <summary>
    /// Снаряжение воина и мешок Греховода (docs/34-GEAR.md). Слева — пять
    /// мест воина (оружие, щит или второе оружие, шлем, броня, пояс),
    /// справа — мешок, который Греховод несёт сам. Вещь переносится
    /// щелчком, а воин отвечает как душа (<see cref="SquadGear"/>):
    /// может не взять и может не отдать.
    ///
    /// <b>Передают из рук в руки</b> (решение автора, 24 сентября): обмен —
    /// только в разговоре вблизи (F от первого лица). Клавиша I сверху —
    /// посмотреть издали, щелчки там не работают. Как с голосом
    /// (docs/31-VOICE.md): хочешь дать — подойди.
    ///
    /// <b>Сундук лагеря</b> — тот же экран, только слева сундук, а не воин
    /// (§9.4, выключатель «склад»): Греховод берёт, сколько унесёт, и что
    /// осталось в сундуке — осталось в лагере.
    ///
    /// У каждой вещи сразу написано, возьмёт ли её воин или отдаст ли. Это
    /// не подсказка ради удобства, а то, ради чего экран есть: игрок учит
    /// отряд, глядя, кто чему рад. «Искушай», а не «экипируй».
    ///
    /// Чисел нет: вместо «удар +2» — «бьёт тяжелее», золото — «кошель».
    /// Пока экран открыт, мир на паузе.
    ///
    /// Ставит себя сам и живёт между сценами.
    /// </summary>
    public class GearPanel : MonoBehaviour
    {
        [SerializeField] private KeyCode _key = KeyCode.I;

        [Tooltip("От первого лица: смотреть на воина и нажать — разговор, и в нём "
               + "тот же обмен. Та же клавиша, что «взаимодействовать».")]
        [SerializeField] private KeyCode _talkKey = KeyCode.F;

        [Tooltip("С какого расстояния можно заговорить, в метрах.")]
        [SerializeField] private float _talkReach = 3.5f;

        private static GearPanel _instance;

        /// <summary>Открыт ли экран. Другие клавиши на это время не слушают.</summary>
        public static bool Open => _instance != null && _instance._open;

        private bool _open;

        /// <summary>Открыт вблизи, в разговоре: можно передавать. Иначе только смотреть.</summary>
        private bool _near;
        private Warrior _warrior;

        /// <summary>Открыт у сундука: слева сундук, а не воин.</summary>
        private TrophyChest _chest;

        /// <summary>Открыт без воина: мешок Греховода сам по себе, только смотреть.</summary>
        private bool _bagOnly;

        // Подсказка у предмета: «F — поговорить», «F — сундук». Без неё
        // о разговоре и о сундуке-складе игрок не узнает никогда.
        private Text _prompt;
        private float _nextLook;

        // Отметка своей паузы (GamePauseController.Stamp). Закрываясь, экран
        // снимает паузу, только если поверх никто не взял свою — церемония
        // титула, разговор: снять чужую значило бы пустить бой под ней.
        private int _stamp;
        private string _answer = "";

        private GameObject _root;
        private Text _title;
        private Text _gold;
        private Text _reply;
        private Text _bagTitle;
        private Text _leftTitle;
        private Text _hint;
        private RectTransform _hands;
        private RectTransform _store;
        private Font _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("Снаряжение");
            _instance = go.AddComponent<GearPanel>();
            DontDestroyOnLoad(go);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // Консоль выключает экран на время ввода (InputHush) — подсказка
        // не должна висеть над ней старой.
        void OnDisable()
        {
            if (_prompt != null) _prompt.text = "";
        }

        /// <summary>Закрыть экран, если открыт. Автопрогону: клавиш у него нет.</summary>
        public static void Dismiss()
        {
            if (_instance != null && _instance._open) _instance.Close();
        }

        /// <summary>Кнопка «Вещи» на панели приказов: то же, что клавиша I.</summary>
        public static void Toggle()
        {
            if (_instance == null) return;
            if (_instance._open) _instance.Close();
            else _instance.TryOpen();
        }

        void Update()
        {
            Prompt();

            if (!_open)
            {
                if (Input.GetKeyDown(_key)) TryOpen();
                else if (Input.GetKeyDown(_talkKey) && !CommanderCouncilUI.AtTable && !TryTalk()) TryChest();
                return;
            }

            // Воин мог пасть или исчезнуть, пока экран был открыт.
            if (_chest == null && !_bagOnly && (_warrior == null || _warrior.IsDead)) { Close(); return; }

            if (Input.GetKeyDown(_key) || Input.GetKeyDown(_talkKey) || Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // ──────────────────────────────────
        // Открыть и закрыть
        // ──────────────────────────────────

        private void TryOpen()
        {
            // Мир уже стоит — руки у другой панели (совет, плата, меню).
            var pause = Core.GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return;

            if (PlayerInventory.Instance == null)
            {
                Say("Мешка Греховода в этой сцене нет.");
                return;
            }

            // Воин не выделен (или выделен один Греховод) — свой мешок.
            var w = Chosen();
            if (w == null) { OpenBag(); return; }

            OpenFor(w, "", near: false);
        }

        /// <summary>
        /// Разговор от первого лица: Греховод смотрит на воина рядом и жмёт F.
        /// Воин отвечает на «как ты?» по своей душе (<see cref="Dialogue.TalkLines"/>),
        /// а ниже — тот же обмен вещами. Сверху разговора нет: туда приходят
        /// за вещами, а сюда — ногами, и за это здесь больше слов.
        /// </summary>
        private bool TryTalk()
        {
            var pause = Core.GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return false;
            if (PlayerInventory.Instance == null) return false;

            var w = LookedAt();
            if (w == null) return false;

            string said = Dialogue.TalkLines.HowAreYou(w);
            string memory = Dialogue.TalkLines.Remembers(w);
            if (!string.IsNullOrEmpty(memory)) said += " " + memory;
            OpenFor(w, $"{w.DisplayName}: «{said}»", near: true);
            return true;
        }

        /// <summary>Свой воин, на которого Греховод смотрит вблизи от первого лица. Нет — null.</summary>
        private Warrior LookedAt()
        {
            var view = FindFirstObjectByType<RTS_Camera>();
            if (view == null || !view.FirstPersonNow) return null;

            var cam = Camera.main;
            if (cam == null) return null;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out var hit, _talkReach + 2f)) return null;

            var w = hit.collider.GetComponentInParent<Warrior>();
            if (w == null || w is SinbinderPlayer || w.IsDead || w.Team != Team.Player) return null;
            if (SinbinderPlayer.Exists
                && CampFocus.GroundDistance(SinbinderPlayer.Where, w.transform.position) > _talkReach) return null;
            return w;
        }

        /// <summary>
        /// Подсказка у предмета: что сделает F, пока экран закрыт. Раз
        /// в пятую долю секунды — луч и поиск сундука не нужны каждый кадр.
        /// </summary>
        private void Prompt()
        {
            if (_prompt == null) BuildPrompt();
            if (_open) { _prompt.text = ""; return; }
            if (Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + 0.2f;

            var pause = Core.GamePauseController.Instance;
            if ((pause != null && pause.IsPaused) || PlayerInventory.Instance == null
                || CommanderCouncilUI.AtTable) { _prompt.text = ""; return; }

            var w = LookedAt();
            if (w != null) { _prompt.text = $"F — поговорить: {w.DisplayName}"; return; }

            _prompt.text = TrophyChest.Reachable() != null ? "F — сундук Марги" : "";
        }

        /// <summary>Мешок Греховода сам по себе: что несёт он и как отдать это воину.</summary>
        private void OpenBag()
        {
            if (_root == null) Build();

            _warrior = null;
            _chest = null;
            _bagOnly = true;
            _near = false;
            _answer = "";
            _open = true;
            _root.SetActive(true);
            Hold();
            Redraw();
        }

        /// <summary>
        /// F у открытого сундука — снова к нему (§9.4). В любом виде, сверху
        /// и от первого лица: сундук не собеседник, к нему только подходят.
        /// </summary>
        private void TryChest()
        {
            var pause = Core.GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return;

            var chest = TrophyChest.Reachable();
            if (chest != null) OpenChest(chest);
        }

        /// <summary>Открыть сундук лагеря: слева он, справа мешок Греховода.</summary>
        public static void OpenChest(TrophyChest chest)
        {
            if (_instance == null || chest == null || PlayerInventory.Instance == null) return;
            if (_instance._open) return;

            var panel = _instance;
            if (panel._root == null) panel.Build();

            panel._chest = chest;
            panel._bagOnly = false;
            panel._warrior = null;
            panel._near = true;
            panel._answer = "";
            panel._open = true;
            panel._root.SetActive(true);
            panel.Hold();
            panel.Redraw();
        }

        private void OpenFor(Warrior w, string first, bool near)
        {
            if (_root == null) Build();

            _warrior = w;
            _chest = null;
            _bagOnly = false;
            _near = near;
            _answer = first;
            _open = true;
            _root.SetActive(true);
            Hold();
            Redraw();
        }

        private void Close()
        {
            _open = false;
            _chest = null;
            _bagOnly = false;
            if (_root != null) _root.SetActive(false);

            var pause = Core.GamePauseController.Instance;
            if (pause != null && pause.Stamp == _stamp) pause.Resume();
        }

        /// <summary>Поставить свою паузу и запомнить её отметку.</summary>
        private void Hold()
        {
            var pause = Core.GamePauseController.Instance;
            if (pause == null) return;
            pause.Pause();
            _stamp = pause.Stamp;
        }

        /// <summary>Первый выделенный свой живой воин — не Греховод: у того свои руки.</summary>
        private static Warrior Chosen()
        {
            var manager = SelectionManager.Instance;
            if (manager == null) return null;

            foreach (var unit in manager.GetSelectedUnits())
            {
                if (unit == null) continue;
                var w = unit.GetComponentInParent<Warrior>();
                if (w != null && !(w is SinbinderPlayer) && !w.IsDead && w.Team == Team.Player)
                    return w;
            }
            return null;
        }

        // ──────────────────────────────────
        // Содержимое
        // ──────────────────────────────────

        private void Redraw()
        {
            if (_chest != null) { RedrawChest(); return; }
            if (_bagOnly) { RedrawBag(); return; }

            var store = PlayerInventory.Instance;
            string name = _warrior.DisplayName;

            _title.text = $"Снаряжение: {name}";
            _leftTitle.text = "На воине";
            Clear(_hands);
            Clear(_store);

            foreach (var slot in SquadGear.Slots)
            {
                var item = _warrior.Worn(slot);
                string place = SquadGear.SlotWord(slot);
                if (item == null) { Row(_hands, $"{place}: — Пусто —", "", null); continue; }

                bool gives = SquadGear.WillGive(_warrior, item, out string word);
                Row(_hands, $"{place}: {item.Name}", Line(item, gives ? "отдаст" : $"не отдаст: {word}"),
                    _near ? () => TakeBack(item) : (System.Action)null);
            }

            // Личный карман (docs/34-GEAR.md §9.3): своё золото воина.
            // Строка — только когда в нём что-то есть.
            if (_warrior.PocketGold > 0)
            {
                bool gives = SquadGear.WillGivePocket(_warrior, out string word);
                Row(_hands, $"Карман: {SquadGear.GoldWord(_warrior.PocketGold)}",
                    Core.Grammar.For(_warrior.Gender, gives ? "своё золото · отдаст, если попросить"
                                                            : $"своё золото · не отдаст: {word}"),
                    _near ? AskPocket : (System.Action)null);
            }

            if (store != null)
            {
                var bag = Bag(store);
                float height = RowHeight(_store, bag.Count);
                int shown = 0;
                foreach (var item in bag)
                {
                    bool takes = SquadGear.WillTake(_warrior, item, out string word);
                    string than = SquadGear.Compare(_warrior, item);
                    if (!string.IsNullOrEmpty(than)) word = $"{than} · {word}";
                    Row(_store, item.Name, Line(item, takes ? word : $"не возьмёт: {word}"),
                        _near ? () => HandOver(item) : (System.Action)null, height);
                    shown++;
                }

                if (shown == 0) Row(_store, "— Пусто —", "", null);
                _gold.text = $"Кошель Греховода: {SquadGear.GoldWord(store.Gold)}";
            }

            _bagTitle.text = _near ? "Мешок Греховода — щелчок: отдать"
                                   : "Мешок Греховода";
            _hint.text = _near
                ? "Щелчок по вещи на воине — забрать в мешок. Занятое место — замена. I, F или Esc — закрыть."
                : "Издали только смотрят. Отдать и забрать — подойдя: F от первого лица. I или Esc — закрыть.";

            _reply.text = _answer;
        }

        /// <summary>
        /// Сундук и мешок (§9.4). Слева — что лежит в сундуке, справа — мешок;
        /// щелчок перекладывает. Всё, что останется слева, останется в лагере.
        /// </summary>
        private void RedrawChest()
        {
            var store = PlayerInventory.Instance;

            _title.text = "Сундук Марги";
            _leftTitle.text = "В сундуке — щелчок: взять в мешок";
            _bagTitle.text = "Мешок Греховода — щелчок: положить в сундук";
            Clear(_hands);
            Clear(_store);

            var inside = new List<InventoryItem>(_chest.Contents);
            float left = RowHeight(_hands, inside.Count);
            foreach (var item in inside)
            {
                string how = item.Type == ItemType.Gold ? "в кошель" : "в мешок";
                Row(_hands, item.Name, Plain(item, how), () => FromChest(item), left);
            }
            if (inside.Count == 0) Row(_hands, "— Пусто —", "", null);

            if (store != null)
            {
                var bag = Bag(store);
                float right = RowHeight(_store, bag.Count);
                foreach (var item in bag)
                    Row(_store, item.Name, Plain(item, "в сундук"), () => ToChest(item), right);
                if (bag.Count == 0) Row(_store, "— Пусто —", "", null);
                _gold.text = $"Кошель Греховода: {SquadGear.GoldWord(store.Gold)}";
            }

            _hint.text = "Что останется в сундуке, останется в лагере: придётся бежать — достанется охотникам. F или Esc — закрыть.";
            _reply.text = _answer;
        }

        /// <summary>
        /// Мешок сам по себе: слева — что в нём, справа — как этим
        /// распорядиться. Щелчков нет: отдают из рук в руки, а не отсюда.
        /// </summary>
        private void RedrawBag()
        {
            var store = PlayerInventory.Instance;

            _title.text = "Мешок Греховода";
            _leftTitle.text = "В мешке";
            _bagTitle.text = "Как отдать";
            Clear(_hands);
            Clear(_store);

            var bag = store != null ? Bag(store) : new List<InventoryItem>();
            float height = RowHeight(_hands, bag.Count);
            foreach (var item in bag) Row(_hands, item.Name, Plain(item, item.Description), null, height);
            if (bag.Count == 0) Row(_hands, "— Пусто —", "", null);

            Row(_store, "Воину", "подойти к нему и F от первого лица", null);
            Row(_store, "Посмотреть, что на воине", "выделить его и I — или «Вещи»", null);
            if (TrophyChest.Store) Row(_store, "В сундук лагеря", "подойти к сундуку и F", null);

            if (store != null) _gold.text = $"Кошель Греховода: {SquadGear.GoldWord(store.Gold)}";
            _hint.text = "I или Esc — закрыть.";
            _reply.text = "";
        }

        private void FromChest(InventoryItem item)
        {
            bool ok = _chest.Take(item, PlayerInventory.Instance, out string word);
            _answer = ok ? $"{item.Name} — {word}." : $"Не взять: {word}.";
            Redraw();
        }

        private void ToChest(InventoryItem item)
        {
            bool ok = _chest.Put(item, PlayerInventory.Instance, out string word);
            _answer = ok ? $"{item.Name} — {word}." : $"Не положить: {word}.";
            Redraw();
        }

        /// <summary>Вещи мешка без золота: золото — строкой кошеля.</summary>
        private static List<InventoryItem> Bag(PlayerInventory store)
        {
            var bag = new List<InventoryItem>();
            foreach (var item in store.GetAllItems())
                if (item != null && item.Type != ItemType.Gold) bag.Add(item);
            return bag;
        }

        /// <summary>
        /// Высота строки, чтобы столбец вместил все: мешок держит восемь вещей,
        /// а по старой высоте в столбец влезало шесть — остальные уходили
        /// за край панели.
        /// </summary>
        private static float RowHeight(RectTransform column, int rows)
        {
            if (rows <= 0) return 58f;
            float room = column.rect.height > 1f ? column.rect.height : 382f;
            return Mathf.Clamp((room - 6f * (rows - 1)) / rows, 40f, 58f);
        }

        /// <summary>Вторая строка вещи: что она даёт и как её примут — в роде воина.</summary>
        private string Line(InventoryItem item, string how)
            => Core.Grammar.For(_warrior.Gender, Plain(item, how));

        private static string Plain(InventoryItem item, string how)
        {
            string effect = SquadGear.Effect(item);
            return string.IsNullOrEmpty(effect) ? how : $"{effect} · {how}";
        }

        private void HandOver(InventoryItem item)
        {
            bool ok = SquadGear.Hand(_warrior, item, PlayerInventory.Instance, out string word);
            Answer(ok ? $"{_warrior.DisplayName} {word}: {item.Name.ToLowerInvariant()}."
                      : $"{_warrior.DisplayName} {word}.");
        }

        private void TakeBack(InventoryItem item)
        {
            bool ok = SquadGear.Take(_warrior, item, PlayerInventory.Instance, out string word);
            Answer(ok ? $"{_warrior.DisplayName} {word}: {item.Name.ToLowerInvariant()}."
                      : $"{_warrior.DisplayName} {word}.");
        }

        private void AskPocket()
        {
            bool ok = SquadGear.AskPocket(_warrior, PlayerInventory.Instance, out string word);
            Answer(ok ? $"{_warrior.DisplayName} отдаёт своё золото в кошель Греховода."
                      : $"{_warrior.DisplayName} {word}.");
        }

        private void Answer(string line)
        {
            _answer = Core.Grammar.For(_warrior.Gender, line);
            Say(_answer);
            Redraw();
        }

        private static void Say(string line)
            => FindFirstObjectByType<BattleLogUI>()?.Write(line);

        // ──────────────────────────────────
        // Сборка
        // ──────────────────────────────────

        private void Build()
        {
            _font = UIFont();

            _root = new GameObject("Холст снаряжения", typeof(Canvas), typeof(CanvasScaler),
                                   typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var shade = Box("Тень", _root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                            new Color(0f, 0f, 0f, 0.55f));
            shade.offsetMin = shade.offsetMax = Vector2.zero;

            var panel = Box("Панель", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(1000f, 600f), new Color(0.07f, 0.06f, 0.055f, 0.97f));

            _title = Label(panel, "Заголовок", 30, TextAnchor.UpperLeft,
                           new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -64f), new Vector2(-28f, -18f));

            _leftTitle = Label(panel, "Слева", 20, TextAnchor.UpperLeft,
                               new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(28f, -100f), new Vector2(-12f, -70f));
            _leftTitle.color = new Color(0.80f, 0.72f, 0.46f);

            _bagTitle = Label(panel, "Мешок", 20, TextAnchor.UpperLeft,
                              new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(12f, -100f), new Vector2(-28f, -70f));
            _bagTitle.color = new Color(0.80f, 0.72f, 0.46f);

            _hands = Column(panel, "Руки", new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(28f, 110f), new Vector2(-12f, -108f));
            _store = Column(panel, "Мешок Греховода", new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(12f, 110f), new Vector2(-28f, -108f));

            _gold = Label(panel, "Кошель", 20, TextAnchor.LowerRight,
                          new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(12f, 68f), new Vector2(-28f, 100f));

            _reply = Label(panel, "Ответ", 21, TextAnchor.LowerLeft,
                           new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 34f), new Vector2(-28f, 66f));
            _reply.color = new Color(0.94f, 0.86f, 0.62f);

            _hint = Label(panel, "Клавиши", 16, TextAnchor.LowerLeft,
                          new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 10f), new Vector2(-28f, 32f));
            _hint.color = new Color(0.55f, 0.52f, 0.48f);

            _root.SetActive(false);
        }

        private RectTransform Column(RectTransform parent, string name, Vector2 min, Vector2 max,
                                     Vector2 offMin, Vector2 offMax)
        {
            var rt = Box(name, parent, min, max, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;

            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            return rt;
        }

        private void Row(RectTransform column, string title, string line, System.Action click,
                         float height = 58f)
        {
            var rt = Box("Вещь", column, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero,
                         new Vector2(0f, height), new Color(0.12f, 0.105f, 0.095f, 1f));

            if (click != null)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = rt.GetComponent<Image>();
                button.onClick.AddListener(() => click());
            }

            var name = Label(rt, "Имя", 20, TextAnchor.UpperLeft,
                             Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-10f, -6f));
            name.text = title;

            var small = Label(rt, "Строка", 15, TextAnchor.LowerLeft,
                              Vector2.zero, Vector2.one, new Vector2(12f, 5f), new Vector2(-10f, -4f));
            small.text = line;
            small.color = new Color(0.66f, 0.62f, 0.56f);
        }

        private static void Clear(RectTransform column)
        {
            // Выключить сразу: Destroy доедет до конца кадра, а раскладка
            // успела бы на кадр поставить новые строки рядом со старыми.
            for (int i = column.childCount - 1; i >= 0; i--)
            {
                var child = column.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max,
                                         Vector2 at, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        private Text Label(RectTransform parent, string name, int size, TextAnchor align,
                           Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;

            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = new Color(0.94f, 0.92f, 0.86f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Подсказка у предмета — внизу посередине, поверх игры, мимо щелчков.</summary>
        private void BuildPrompt()
        {
            var canvasGo = new GameObject("Подсказка F", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var go = new GameObject("Строка", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasGo.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 250f);
            rt.sizeDelta = new Vector2(700f, 40f);

            _prompt = go.GetComponent<Text>();
            _prompt.font = UIFont();
            _prompt.fontSize = 24;
            _prompt.alignment = TextAnchor.MiddleCenter;
            _prompt.color = new Color(0.94f, 0.86f, 0.62f);
            _prompt.raycastTarget = false;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        }

        private static Font UIFont()
        {
            var any = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            if (any != null && any.font != null) return any.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
