// Assets/Scripts/UI/CommandPanel.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sinbinder.Gameplay;

using Sinbinder.Core;
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
    /// С 1 октября — правая зона нижней полосы по макету лагеря
    /// (docs/42-INTERFACE.md §3): кнопками только «Строй» (H) и «Оборона» (G),
    /// мышиные приказы и прочие клавиши (P, C, I, V) — строкой подсказки,
    /// установка отряда — одной кнопкой со списком. Прогноз — только при
    /// наведении: для отданного приказа его уже говорят голоса на полосе.
    /// Кнопки зовут то же, что клавиши (<see cref="SelectionManager.Stance"/>).
    ///
    /// Ставит себя сама и живёт между сценами, как <see cref="SelectionManager"/>.
    /// </summary>
    public class CommandPanel : MonoBehaviour
    {
        private static CommandPanel _instance;

        // Цвета макета лагеря (docs/42-INTERFACE.md §1, п. 8): тёмная кожа,
        // текст цвета кости. Наведённое и нажатое — заливкой, не рамкой;
        // недоступное — приглушено (§1, п. 6).
        private static readonly Color Plain = new(0.227f, 0.173f, 0.133f, 1f);
        private static readonly Color Lit = new(0.35f, 0.27f, 0.20f, 1f);
        private static readonly Color Dim = new(0.13f, 0.10f, 0.08f, 1f);
        private static readonly Color Leather = new(0.169f, 0.129f, 0.102f, 1f);
        private static readonly Color Muted = new(0.70f, 0.65f, 0.55f, 1f);

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

        // Прогноз (docs/35-CRITIQUE.md п. 5): кто пойдёт на этот приказ,
        // кто вряд ли — и почему. Считается раз в полсекунды, не каждый
        // кадр: на каждого сомневающегося — пересчёт «от противного».
        private static BehaviourResolverHolder _resolver;
        private Slot _forecastFor;
        private float _forecastAt;
        private string _forecast = "";
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
            if (RectTransformUtility.RectangleContainsScreenPoint(_instance._grid, screen, null)) return true;

            // Раскрытый список установок висит над зоной — и он тоже не земля.
            var list = _instance._stanceList;
            return list != null && list.gameObject.activeSelf
                && RectTransformUtility.RectangleContainsScreenPoint(list, screen, null);
        }

        private bool Shown => _group != null && _group.alpha > 0.5f;

        void Start()
        {
            Build();
            Loc.Changed += Rebuild;
        }

        void OnDestroy()
        {
            Loc.Changed -= Rebuild;
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Язык сменили в меню паузы. Панель живёт всю игру, и её слова
        /// собраны один раз — без этого «Идти» и «Атака» остались бы
        /// на прежнем языке до конца игры.
        /// </summary>
        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _slots.Clear();
            _hover = null;
            _forecastFor = null;
            _forecast = "";
            Build();
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
            if (!show)
            {
                _tip.text = "";
                if (_stanceList != null) _stanceList.gameObject.SetActive(false);
                return;
            }

            bool heroOnly = picked == SelectionManager.Picked.HeroOnly;

            // Один Греховод: приказывать некому — кнопки приглушены и сказано
            // почему (разбор макета, круг 5: «приказ — кому?»).
            _header.text = heroOnly ? Loc.T("Приказы — сначала выберите воинов") : Loc.T("Приказы");
            _stanceLine.text = Loc.F("Установка: {0}  ▾", SquadOrders.Name(SquadOrders.Current));

            foreach (var s in _slots)
            {
                bool usable = !heroOnly || s.HeroToo;
                s.Button.interactable = usable;
                s.Face.color = !usable ? Dim : _hover == s ? Lit : Plain;
            }

            _tip.text = _stanceList.gameObject.activeSelf ? "" : Tip(manager, heroOnly);
        }

        private string Tip(SelectionManager manager, bool heroOnly)
        {
            if (manager.Aiming == CommandKind.Attack)
                return Loc.T("Укажите врага — или место: пойдут и будут бить всех по дороге. ПКМ — передумать.");
            if (manager.Aiming == CommandKind.Patrol) return Loc.T("Укажите, докуда ходить. ПКМ — передумать.");
            if (manager.Aiming != CommandKind.None) return Loc.T("Укажите место. ПКМ — передумать.");
            // Без наведения — ничего: прогноз отданного приказа уже говорят
            // голоса на полосе (42-INTERFACE §3, «Прогноз»).
            if (_hover == null) return "";

            string tip = $"{_hover.Word} · {_hover.Key}\n";
            if (_hover.Act != null) return tip + _hover.Tip;   // экран, а не приказ
            if (heroOnly) return tip + Loc.T("Греховод слушается всегда.");

            tip += _hover.Tip;
            if (Voice.Enabled) tip += Loc.T("\nПриказ слышен тем лучше, чем ближе Греховод.");

            string forecast = Forecast(manager, _hover);
            if (!string.IsNullOrEmpty(forecast)) tip += "\n" + forecast;
            return tip;
        }

        /// <summary>
        /// Кто из выделенных пойдёт на приказ этой кнопки и кто вряд ли,
        /// с причиной «от противного» (<see cref="AOS.Counterfactual"/>).
        /// Кнопка перестаёт быть договором «нажал — исполнили» и становится
        /// разведкой: отказ виден до того, как случился. Вторая ступень
        /// ясности, выключатель «причина». Прогноз, а не обещание: у места,
        /// куда пошлют, может лежать своё.
        /// </summary>
        private string Forecast(SelectionManager manager, Slot slot)
        {
            if (!AOS.Counterfactual.Enabled || slot.Act != null || slot.Kind == CommandKind.None) return "";
            if (!Core.Transparency.Shows(Core.Clarity.Tooltips)) return "";

            if (slot == _forecastFor && Time.unscaledTime - _forecastAt < 0.5f) return _forecast;
            _forecastFor = slot;
            _forecastAt = Time.unscaledTime;
            _forecast = Predict(manager, slot.Kind);
            return _forecast;
        }

        /// <summary>
        /// Прогноз на приказ этого вида для нынешнего выделения — без
        /// наведения и без счётчика. Открыт автопрогону: навести мышь он
        /// не может, а прогноз обязан быть проверен в игре.
        /// </summary>
        public static string Predict(CommandKind kind)
        {
            var manager = SelectionManager.Instance;
            return manager == null ? "" : Predict(manager, kind);
        }

        private static string Predict(SelectionManager manager, CommandKind kind)
        {
            _resolver ??= new BehaviourResolverHolder();
            var willing = new List<string>();
            var doubtful = new List<string>();
            var deaf = new List<string>();
            int shown = 0;

            foreach (var unit in manager.GetSelectedUnits())
            {
                if (unit == null) continue;
                var w = unit.GetComponentInParent<Warrior>();
                if (w == null || w is SinbinderPlayer || w.IsDead || w.Team != Team.Player) continue;

                // Не услышит — приказа ему не будет вовсе, и гадать, пойдёт
                // ли он, незачем. Прогон 25 сентября: Вейн в пятнадцати
                // метрах числился в «скорее пойдут», а приказа не услышал.
                float muffle = Voice.MuffleFor(w);
                if (!Voice.Heard(muffle)) { deaf.Add(w.DisplayName); continue; }

                var ctx = AOS.CombatDecisionContext.Imagine(w, kind, muffle);
                if (_resolver.Value.WouldObey(w, ctx)) { willing.Add(w.DisplayName); continue; }

                if (shown++ >= 3) { doubtful.Add(w.DisplayName); continue; }
                string why = _resolver.Why(w, ctx);
                doubtful.Add(string.IsNullOrEmpty(why) ? Loc.Name(w.DisplayName) : $"{Loc.Name(w.DisplayName)}: {why}");
            }

            var lines = new List<string>();
            if (willing.Count > 0) lines.Add(Loc.T("Скорее пойдут: ") + string.Join(", ", willing) + ".");
            if (doubtful.Count > 0) lines.Add(Loc.T("Вряд ли — ") + string.Join("; ", doubtful) + ".");
            if (deaf.Count > 0) lines.Add(Loc.T("Не услышат: ") + string.Join(", ", deaf) + Loc.T(". Греховод далеко."));
            return string.Join("\n", lines);
        }

        /// <summary>Один резолвер на панель и причина отказа тем же правилом, что в бою.</summary>
        private sealed class BehaviourResolverHolder
        {
            public readonly AOS.BehaviourResolver Value = new AOS.BehaviourResolver();

            public string Why(Warrior w, AOS.DecisionContext ctx)
            {
                var d = new AOS.Decision { RefusedCommand = true };
                Value.Weigh(w, ctx, ref d);
                if (d.Decisive == AOS.Counterfactual.Factor.None && string.IsNullOrEmpty(d.DecisiveVoice)) return "";
                return AOS.PhraseGenerator.Reason(w, ctx, d);
            }
        }

        // ──────────────────────────────────
        // Сборка
        // ──────────────────────────────────

        // Место на нижней полосе (docs/42-INTERFACE.md §3, зона «отряд
        // и приказы»): справа, точки холста 1920×1080. Полоса — 280 в высоту
        // (сборщик сцен, ConsoleHeight); зона стоит внутри неё.
        private static readonly Vector2 ZoneSize = new(420f, 236f);
        private static readonly Vector2 ZoneAt = new(-24f, 22f);

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

            _grid = Rect("Приказы", canvasGo.transform, new Vector2(1f, 0f), ZoneAt, ZoneSize);

            _header = Label(_grid, UiStyle.Title, "", 20, TextAnchor.UpperLeft);
            _header.color = Muted;
            Place(_header.rectTransform, 0f, 0f, ZoneSize.x, 24f);

            // Кнопками — только то, что мышью не сделать (разбор макета,
            // круг 3): «Идти», «Бить», «Отход» — правая кнопка мыши,
            // и строкой подсказки ниже.
            Add(font, 0, Loc.T("Строй"), "H", CommandKind.Hold,
                Loc.T("Терпеливый стоит. Гневный рвётся."));
            Add(font, 1, Loc.T("Оборона"), "G", CommandKind.Defend,
                Loc.T("Стоять и защищаться. Кто рвётся в драку, стоять не любит."));

            var mouse = Label(_grid, font, Loc.T("ПКМ — идти · по врагу — бить · Shift+ПКМ — отход"),
                              16, TextAnchor.MiddleLeft);
            mouse.color = Muted;
            Place(mouse.rectTransform, 0f, 98f, ZoneSize.x, 22f);

            var keys = Label(_grid, font, Loc.T("P — патруль · C — отмена · I — вещи · V — вид"),
                             16, TextAnchor.MiddleLeft);
            keys.color = Muted;
            Place(keys.rectTransform, 0f, 122f, ZoneSize.x, 22f);

            BuildStance(font);

            var tipRect = Rect("Подсказка", canvasGo.transform, new Vector2(1f, 0f),
                               new Vector2(ZoneAt.x, ZoneAt.y + 280f), new Vector2(520f, 110f));
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

        // ---------- установка отряда: одна кнопка и список ----------

        private Text _header;
        private Text _stanceLine;
        private RectTransform _stanceList;

        /// <summary>
        /// Установка отряда — одной кнопкой со списком (разбор макета, круг 3:
        /// шесть кнопок на действие, которое меняют раз в бой). В списке
        /// у каждой — её описание: по одним названиям «Без указаний»
        /// и «Держаться приказа» не различить. Клавиши 1–6 по-прежнему
        /// у <see cref="SquadStrategyUI"/>.
        /// </summary>
        private void BuildStance(Font font)
        {
            var button = Rect("Установка", _grid, new Vector2(0f, 1f), Vector2.zero, new Vector2(ZoneSize.x, 48f));
            button.pivot = new Vector2(0f, 1f);
            button.anchoredPosition = new Vector2(0f, -(ZoneSize.y - 48f));

            var face = button.gameObject.AddComponent<Image>();
            face.color = Leather;
            face.sprite = UiStyle.Pill;            // установка — овалом, как в макете
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 1.3f;
            var b = button.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            b.onClick.AddListener(() => _stanceList.gameObject.SetActive(!_stanceList.gameObject.activeSelf));

            Key(button, font, "1–6", 12f);
            _stanceLine = Label(button, font, "", 18, TextAnchor.MiddleLeft);
            _stanceLine.rectTransform.offsetMin = new Vector2(64f, 0f);
            _stanceLine.rectTransform.offsetMax = new Vector2(-12f, 0f);

            // Список раскрывается вверх, над зоной: снизу экрана места нет.
            var choices = SquadOrders.InDemo;
            const float row = 58f;
            _stanceList = Rect("Список установок", _grid, new Vector2(0f, 1f), Vector2.zero,
                               new Vector2(ZoneSize.x, choices.Length * row));
            _stanceList.pivot = new Vector2(0f, 0f);
            _stanceList.anchoredPosition = new Vector2(0f, -(ZoneSize.y - 48f) + 4f);
            _stanceList.gameObject.AddComponent<Image>().color = new Color(0.114f, 0.086f, 0.071f, 0.98f);

            for (int i = 0; i < choices.Length; i++)
            {
                var strategy = choices[i];
                var line = Rect(SquadOrders.Name(strategy), _stanceList, new Vector2(0f, 1f), Vector2.zero,
                                new Vector2(ZoneSize.x, row - 4f));
                line.pivot = new Vector2(0f, 1f);
                line.anchoredPosition = new Vector2(0f, -i * row);

                var plate = line.gameObject.AddComponent<Image>();
                plate.color = Leather;
                plate.sprite = UiStyle.Pill;
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = 2.2f;
                var press = line.gameObject.AddComponent<Button>();
                press.targetGraphic = plate;
                press.onClick.AddListener(() => { SquadOrders.Set(strategy); _stanceList.gameObject.SetActive(false); });

                Key(line, font, (i + 1).ToString(), 12f);
                var name = Label(line, UiStyle.BodyBold, SquadOrders.Name(strategy), 18, TextAnchor.UpperLeft);
                name.rectTransform.offsetMin = new Vector2(52f, 0f);
                name.rectTransform.offsetMax = new Vector2(-10f, -6f);
                var about = Label(line, font, SquadOrders.Describe(strategy), 14, TextAnchor.LowerLeft);
                about.color = Muted;
                about.rectTransform.offsetMin = new Vector2(52f, 6f);
                about.rectTransform.offsetMax = new Vector2(-10f, 0f);
            }

            _stanceList.gameObject.SetActive(false);
        }

        /// <summary>Плашка клавиши: светлая кость, тёмная буква (макет: клавиши не мелкие и не бледные).</summary>
        private static void Key(RectTransform parent, Font font, string key, float left)
        {
            var plate = Rect("Клавиша", parent, new Vector2(0f, 0.5f), new Vector2(left, 0f),
                             new Vector2(key.Length > 1 ? 40f : 24f, 22f));
            plate.pivot = new Vector2(0f, 0.5f);
            plate.anchoredPosition = new Vector2(left, 0f);
            plate.gameObject.AddComponent<Image>().color = new Color(0.54f, 0.51f, 0.47f, 1f);
            var t = Label(plate, font, key, 14, TextAnchor.MiddleCenter);
            t.font = UiStyle.BodyBold;
            t.color = new Color(0.08f, 0.06f, 0.05f);
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private void Add(Font font, int col, string word, string key, CommandKind kind, string tip)
        {
            var slot = new Slot { Word = word, Key = key, Kind = kind, Aims = false, HeroToo = false, Tip = tip };

            const float w = 200f, h = 56f, gap = 20f;
            var cell = Rect(word, _grid, new Vector2(0f, 1f), Vector2.zero, new Vector2(w, h));
            cell.pivot = new Vector2(0f, 1f);
            cell.anchoredPosition = new Vector2(col * (w + gap), -32f);

            slot.Face = cell.gameObject.AddComponent<Image>();
            slot.Face.color = Plain;

            slot.Button = cell.gameObject.AddComponent<Button>();
            slot.Button.targetGraphic = slot.Face;
            slot.Button.onClick.AddListener(() => Press(slot));

            Key(cell, font, key, 14f);
            var label = Label(cell, UiStyle.BodyBold, word, 20, TextAnchor.MiddleLeft);
            label.rectTransform.offsetMin = new Vector2(48f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);

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
            // Шрифт макета (UiStyle) — тот же, что на полосе.
            if (UiStyle.Body != null) return UiStyle.Body;

            var any = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            if (any != null && any.font != null) return any.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
