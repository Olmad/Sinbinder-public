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
        private string _answer = "";

        private GameObject _root;
        private Text _title;
        private Text _gold;
        private Text _reply;
        private Text _bagTitle;
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

        void Update()
        {
            if (!_open)
            {
                if (Input.GetKeyDown(_key)) TryOpen();
                else if (Input.GetKeyDown(_talkKey)) TryTalk();
                return;
            }

            // Воин мог пасть или исчезнуть, пока экран был открыт.
            if (_warrior == null || _warrior.IsDead) { Close(); return; }

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

            var w = Chosen();
            if (w == null)
            {
                Say("Выделите воина: снаряжение смотрят у того, кто его несёт.");
                return;
            }

            if (PlayerInventory.Instance == null)
            {
                Say("Мешка Греховода в этой сцене нет.");
                return;
            }

            OpenFor(w, "", near: false);
        }

        /// <summary>
        /// Разговор от первого лица: Греховод смотрит на воина рядом и жмёт F.
        /// Воин отвечает на «как ты?» по своей душе (<see cref="Dialogue.TalkLines"/>),
        /// а ниже — тот же обмен вещами. Сверху разговора нет: туда приходят
        /// за вещами, а сюда — ногами, и за это здесь больше слов.
        /// </summary>
        private void TryTalk()
        {
            var view = FindFirstObjectByType<RTS_Camera>();
            if (view == null || !view.FirstPersonNow) return;

            var pause = Core.GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return;
            if (PlayerInventory.Instance == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out var hit, _talkReach + 2f)) return;

            var w = hit.collider.GetComponentInParent<Warrior>();
            if (w == null || w is SinbinderPlayer || w.IsDead || w.Team != Team.Player) return;
            if (SinbinderPlayer.Exists
                && CampFocus.GroundDistance(SinbinderPlayer.Where, w.transform.position) > _talkReach) return;

            OpenFor(w, $"{w.DisplayName}: «{Dialogue.TalkLines.HowAreYou(w)}»", near: true);
        }

        private void OpenFor(Warrior w, string first, bool near)
        {
            if (_root == null) Build();

            _warrior = w;
            _near = near;
            _answer = first;
            _open = true;
            _root.SetActive(true);
            Core.GamePauseController.Instance?.Pause();
            Redraw();
        }

        private void Close()
        {
            _open = false;
            if (_root != null) _root.SetActive(false);
            Core.GamePauseController.Instance?.Resume();
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
            var store = PlayerInventory.Instance;
            string name = _warrior.DisplayName;

            _title.text = $"Снаряжение: {name}";
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

            if (store != null)
            {
                int shown = 0;
                foreach (var item in new List<InventoryItem>(store.GetAllItems()))
                {
                    if (item == null || item.Type == ItemType.Gold) continue;

                    bool takes = SquadGear.WillTake(_warrior, item, out string word);
                    Row(_store, item.Name, Line(item, takes ? word : $"не возьмёт: {word}"),
                        _near ? () => HandOver(item) : (System.Action)null);
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

        /// <summary>Вторая строка вещи: что она даёт и как её примут — в роде воина.</summary>
        private string Line(InventoryItem item, string how)
        {
            string effect = SquadGear.Effect(item);
            string text = string.IsNullOrEmpty(effect) ? how : $"{effect} · {how}";
            return Core.Grammar.For(_warrior.Gender, text);
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

            var left = Label(panel, "На воине", 20, TextAnchor.UpperLeft,
                             new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(28f, -100f), new Vector2(-12f, -70f));
            left.text = "На воине";
            left.color = new Color(0.80f, 0.72f, 0.46f);

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

        private void Row(RectTransform column, string title, string line, System.Action click)
        {
            var rt = Box("Вещь", column, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero,
                         new Vector2(0f, 58f), new Color(0.12f, 0.105f, 0.095f, 1f));

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

        private static Font UIFont()
        {
            var any = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            if (any != null && any.font != null) return any.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
