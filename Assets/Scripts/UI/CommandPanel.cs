// Assets/Scripts/UI/CommandPanel.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Панель приказов как в Warcraft 3 (docs/33-COMMANDS.md, шаг первый):
    /// сетка кнопок справа внизу, буква клавиши в углу, подсказка при
    /// наведении — и в подсказке сказано, <b>как приказ звучит для
    /// характера</b>. Там приказ исполняется всегда, здесь он — голос.
    ///
    /// Автор, 24 сентября: «было бы хорошо сделать панель управления как
    /// в WarCraft 3». Сам он не нашёл, как листать суму, — с приказами было
    /// то же самое: клавиши знал только `УПРАВЛЕНИЕ.md`.
    ///
    /// Кнопки зовут то же, что клавиши (<see cref="SelectionManager.Stance"/>,
    /// <see cref="SelectionManager.Aim"/>). Восьмая — «Вещи» (I): не приказ,
    /// а экран снаряжения (docs/34-GEAR.md); без наведения подсказка
    /// говорит, что на выделенном воине. Шаг второй — патруль (P) и атака
    /// с ходу («Атака» по земле) — новые голоса в движке, через модули.
    ///
    /// Ставит себя сама и живёт между сценами, как <see cref="SelectionManager"/>.
    /// </summary>
    public class CommandPanel : MonoBehaviour
    {
        private static CommandPanel _instance;

        private const float Cell = 76f;
        private const float Gap = 6f;

        private static readonly Color Plain = new(0.10f, 0.09f, 0.09f, 0.88f);
        private static readonly Color Lit = new(0.62f, 0.54f, 0.30f, 0.95f);
        private static readonly Color Dim = new(0.10f, 0.09f, 0.09f, 0.40f);

        private sealed class Slot
        {
            public string Word, Key, Tip;
            public CommandKind Kind;
            public bool Aims;
            public bool HeroToo;
            public System.Action Act;   // не приказ, а экран: «Вещи»
            public Button Button;
            public Image Face;
        }

        private readonly List<Slot> _slots = new();
        private RectTransform _grid;
        private CanvasGroup _group;
        private Text _tip;
        private Slot _hover;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("Панель приказов");
            _instance = go.AddComponent<CommandPanel>();
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// Лежит ли точка экрана на панели. Спрашивает выделение: щелчок
        /// по кнопке не должен заодно снимать выделение или слать отряд
        /// в землю за панелью.
        /// </summary>
        public static bool Covers(Vector2 screen)
        {
            if (_instance == null || _instance._grid == null || !_instance.Shown) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_instance._grid, screen, null);
        }

        private bool Shown => _group != null && _group.alpha > 0.5f;

        void Start()
        {
            Build();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (_group == null) return;

            var manager = SelectionManager.Instance;
            var pause = Core.GamePauseController.Instance;

            var picked = manager != null && manager.enabled
                ? manager.Selection : SelectionManager.Picked.Nobody;
            bool show = picked != SelectionManager.Picked.Nobody
                     && (pause == null || !pause.IsPaused);

            _group.alpha = show ? 1f : 0f;
            _group.interactable = show;
            _group.blocksRaycasts = show;
            if (!show) { _tip.text = ""; return; }

            bool heroOnly = picked == SelectionManager.Picked.HeroOnly;

            foreach (var s in _slots)
            {
                bool usable = !heroOnly || s.HeroToo;
                s.Button.interactable = usable;
                s.Face.color = !usable ? Dim
                             : s.Aims && manager.Aiming == s.Kind ? Lit
                             : Plain;
            }

            _tip.text = Tip(manager, heroOnly);
        }

        private string Tip(SelectionManager manager, bool heroOnly)
        {
            if (manager.Aiming == CommandKind.Attack)
                return "Укажите врага — или место: пойдут и будут бить всех по дороге. ПКМ — передумать.";
            if (manager.Aiming == CommandKind.Patrol) return "Укажите, докуда ходить. ПКМ — передумать.";
            if (manager.Aiming != CommandKind.None) return "Укажите место. ПКМ — передумать.";
            if (_hover == null) return Worn(manager);

            string tip = $"{_hover.Word} · {_hover.Key}\n";
            if (_hover.Act != null) return tip + _hover.Tip;   // экран, а не приказ
            if (heroOnly) return tip + "Греховод слушается всегда.";

            tip += _hover.Tip;
            if (Voice.Enabled) tip += "\nПриказ слышен тем лучше, чем ближе Греховод.";
            return tip;
        }

        /// <summary>
        /// Без наведения — что на выделенном воине (docs/34-GEAR.md): надетое
        /// и карман словами. Только для одного своего: у отряда сводка
        /// была бы стеной текста.
        /// </summary>
        private static string Worn(SelectionManager manager)
        {
            Warrior only = null;
            foreach (var unit in manager.GetSelectedUnits())
            {
                if (unit == null) continue;
                var w = unit.GetComponentInParent<Warrior>();
                if (w == null || w is SinbinderPlayer || w.IsDead || w.Team != Team.Player) continue;
                if (only != null) return "";
                only = w;
            }
            return only == null ? "" : $"{SquadGear.Summary(only)} I — вещи.";
        }

        // ──────────────────────────────────
        // Сборка
        // ──────────────────────────────────

        private void Build()
        {
            var font = UIFont();

            var canvasGo = new GameObject("Холст приказов", typeof(Canvas), typeof(CanvasScaler),
                                          typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _group = canvasGo.GetComponent<CanvasGroup>();

            // Справа внизу, над сумой и её строкой клавиш.
            _grid = Rect("Сетка", canvasGo.transform, new Vector2(1f, 0f),
                         new Vector2(-40f, 150f), new Vector2(4 * Cell + 3 * Gap, 2 * Cell + Gap));

            Add(font, 0, 0, "Идти", "M или ПКМ", CommandKind.Move, aims: true, heroToo: true,
                "Большинство пойдёт. Кто держит своё — сундук, раненого, врага рядом, — поспорит.");
            Add(font, 1, 0, "Атака", "T или ПКМ по врагу", CommandKind.Attack, aims: true, heroToo: true,
                "По врагу — бить его. По земле — идти туда и бить всех по дороге. "
              + "Гневный рад. Трус и раненый — нет.");
            Add(font, 2, 0, "Отход", "X или Shift + ПКМ", CommandKind.FallBack, aims: true, heroToo: false,
                "Трус исполнит охотно и по-своему — побежит. Гордец отходить не любит.");
            Add(font, 3, 0, "Патруль", "P", CommandKind.Patrol, aims: true, heroToo: false,
                "Ходить отсюда туда и обратно, пока не снимут. Унылому скучно, "
              + "усердный идёт охотно. Гневный бросит маршрут, увидев врага.");
            Add(font, 0, 1, "Держать", "H", CommandKind.Hold, aims: false, heroToo: false,
                "Терпеливый стоит. Гневный рвётся.");
            Add(font, 1, 1, "Оборона", "G", CommandKind.Defend, aims: false, heroToo: false,
                "Стоять и защищаться. Кто рвётся в драку, стоять не любит.");
            Add(font, 2, 1, "Отмена", "C", CommandKind.None, aims: false, heroToo: true,
                "Снятый приказ — не приказ. Дальше решают сами.");
            Add(font, 3, 1, "Вещи", "I", CommandKind.None, aims: false, heroToo: true,
                "Что на воине и что в мешке Греховода. Отдать и забрать — подойдя к воину: F.",
                GearPanel.Toggle);

            var tipRect = Rect("Подсказка", canvasGo.transform, new Vector2(1f, 0f),
                               new Vector2(-40f, 150f + 2 * Cell + Gap + 10f), new Vector2(520f, 96f));
            _tip = tipRect.gameObject.AddComponent<Text>();
            _tip.font = font;
            _tip.fontSize = 19;
            _tip.alignment = TextAnchor.LowerRight;
            _tip.color = new Color(0.92f, 0.89f, 0.82f);
            _tip.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tip.verticalOverflow = VerticalWrapMode.Overflow;
            _tip.raycastTarget = false;
            tipRect.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            _group.alpha = 0f;
        }

        private void Add(Font font, int col, int row, string word, string key, CommandKind kind,
                         bool aims, bool heroToo, string tip, System.Action act = null)
        {
            var slot = new Slot { Word = word, Key = key, Kind = kind, Aims = aims, HeroToo = heroToo, Tip = tip, Act = act };

            // Ряды сверху вниз: верхний ряд — приказы с точкой.
            var cell = Rect(word, _grid, new Vector2(0f, 1f),
                            new Vector2(col * (Cell + Gap), -row * (Cell + Gap)), new Vector2(Cell, Cell));
            cell.pivot = new Vector2(0f, 1f);
            cell.anchoredPosition = new Vector2(col * (Cell + Gap), -row * (Cell + Gap));

            slot.Face = cell.gameObject.AddComponent<Image>();
            slot.Face.color = Plain;

            slot.Button = cell.gameObject.AddComponent<Button>();
            slot.Button.targetGraphic = slot.Face;
            slot.Button.onClick.AddListener(() => Press(slot));

            var label = Label(cell, font, word, 17, TextAnchor.MiddleCenter);
            label.rectTransform.offsetMin = new Vector2(2f, 4f);
            label.rectTransform.offsetMax = new Vector2(-2f, -4f);

            var letter = Label(cell, font, key.Substring(0, 1), 14, TextAnchor.UpperLeft);
            letter.color = new Color(0.80f, 0.72f, 0.46f);
            letter.rectTransform.offsetMin = new Vector2(6f, 4f);
            letter.rectTransform.offsetMax = new Vector2(-4f, -4f);

            var trigger = cell.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => _hover = slot);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (_hover == slot) _hover = null; });
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);

            _slots.Add(slot);
        }

        private static void Press(Slot slot)
        {
            var manager = SelectionManager.Instance;
            if (manager == null) return;

            if (slot.Act != null) { slot.Act(); return; }
            if (slot.Aims) manager.Aim(slot.Kind);
            else manager.Stance(slot.Kind);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor,
                                          Vector2 at, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text Label(RectTransform parent, Font font, string text, int size, TextAnchor align)
        {
            var go = new GameObject("Слово", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;

            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.text = text;
            t.alignment = align;
            t.color = new Color(0.94f, 0.92f, 0.86f);
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
