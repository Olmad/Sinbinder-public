// Assets/Scripts/UI/WarriorTooltipUI.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Вторая ступень прозрачности: наведи мышь — узнай почему.
    ///
    /// Значок над головой говорит ЧТО. Здесь говорится ПОЧЕМУ, словами
    /// и без единой цифры. Ни очков, ни весов, ни процентов: только факты
    /// о воине и о том, что было вокруг.
    ///
    /// Настройка в сцене: повесить на Canvas, задать _panel (RectTransform
    /// панели), _text и _frame (Image рамки — красится в цвет греха).
    /// </summary>
    public class WarriorTooltipUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _text;
        [SerializeField] private Image _frame;
        [SerializeField] private LayerMask _unitLayer = ~0;
        [SerializeField] private Vector2 _offset = new Vector2(18f, -18f);

        private Camera _cam;
        private Outline _edge;
        private Warrior _shown;
        private float _nextRefresh;

        // Строка весов — на решение, а не на кадр: голоса считаются заново, только когда воин решил снова.
        private DecisionContext _scalesFor;
        private Warrior _scalesWarrior;
        private bool _scalesWhisperCompelled;
        private string _scales;

        /// <summary>
        /// Над кем подсказка сейчас. Урок отказа (<see cref="Lesson"/>) ждёт,
        /// пока её прочтут над отказавшим.
        /// </summary>
        public static Warrior Hovered { get; private set; }

        /// <summary>
        /// Прогон лагеря (пакетный режим, мыши нет): «навести» подсказку на этого воина,
        /// чтобы снять её кадром. В игре всегда пусто.
        /// </summary>
        public static Warrior Pinned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Hovered = null; Pinned = null; }

        /// <summary>Где «мышь»: у прогона — у груди воина, в игре — сама мышь.</summary>
        private Vector2 Pointer()
        {
            if (Pinned == null || Pinned.IsDead || _cam == null) return Input.mousePosition;
            Vector3 p = _cam.WorldToScreenPoint(Pinned.transform.position + Vector3.up * 1.2f);
            // A pinned warrior behind the active camera has no meaningful
            // on-screen anchor. Avoid pinning the note to a mirrored projection.
            if (p.z <= 0f) return Input.mousePosition;
            return new Vector2(p.x, p.y);
        }

        void Awake()
        {
            _cam = Camera.main;
            Hide();
        }

        void OnDisable() => Hide();

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) { Hide(); return; }

            // Трон и дверь склепа (автор, 27.10: «нельзя посмотреть трон на количество здоровья,
            // то же самое с дверью склепа»): прочность — при любой ступени прозрачности: это вещь,
            // а не чьи-то мысли.
            var sturdy = SturdyUnderCursor();
            if (sturdy != null) { ShowSturdy(sturdy); return; }

            // Вторая ступень прозрачности. Игрок, выбравший только значки,
            // причин не спрашивает — и подсказка ему не мешает.
            bool tutorialNeedsAttitude = TugTutorial.Instance != null && TugTutorial.Instance.Step == 1;
            if (!Core.Transparency.Shows(Core.Clarity.Tooltips) && !tutorialNeedsAttitude)
            {
                Hide();
                return;
            }

            var warrior = Pinned != null && !Pinned.IsDead ? Pinned : WarriorUnderCursor();
            if (warrior == null) { Hide(); return; }

            // При подарке или приказе отношение меняется, даже если мышь не сдвинулась.
            // Не перерисовываем сложное объяснение каждый кадр: достаточно 3 раз в секунду.
            if (warrior != _shown || Time.unscaledTime >= _nextRefresh)
            {
                _shown = warrior;
                Refresh(warrior);
                _nextRefresh = Time.unscaledTime + 0.35f;
            }
            // Подсказку можно считать прочитанной лишь когда панель действительно видна.
            Hovered = _panel != null && _panel.gameObject.activeInHierarchy ? _shown : null;

            PlaceNearPointer();
        }

        /// <summary>
        /// The hover note used to disappear beyond the screen edge near the
        /// right and bottom borders. Clamp the same overlay-space position,
        /// respecting the prefab's pivot and actual canvas scaling.
        /// </summary>
        private void PlaceNearPointer(bool actualMouse = false)
        {
            if (_panel == null) return;
            // Object tooltips must follow the hovered object, not a pinned warrior.
            Vector2 pointer = actualMouse ? (Vector2)Input.mousePosition : Pointer();
            Vector2 at = pointer + _offset;
            Vector2 size = _panel.rect.size;
            Vector3 scale = _panel.lossyScale;
            float width = size.x * Mathf.Abs(scale.x);
            float height = size.y * Mathf.Abs(scale.y);
            const float margin = 12f;
            float left = width * _panel.pivot.x;
            float right = width * (1f - _panel.pivot.x);
            float bottom = height * _panel.pivot.y;
            float top = height * (1f - _panel.pivot.y);

            // When there is room on the opposite side, flip instead of
            // clamping the tooltip directly over the hovered unit/cursor.
            // Keep the original offsets and panel pivot in screen pixels.
            if (at.x + right + margin > Screen.width &&
                pointer.x - Mathf.Abs(_offset.x) - width - margin >= 0f)
                at.x = pointer.x - Mathf.Abs(_offset.x) - right;
            if (at.y - bottom - margin < 0f &&
                pointer.y + Mathf.Abs(_offset.y) + height + margin <= Screen.height)
                at.y = pointer.y + Mathf.Abs(_offset.y) + bottom;

            at.x = Mathf.Clamp(at.x, left + margin,
                Mathf.Max(left + margin, Screen.width - right - margin));
            at.y = Mathf.Clamp(at.y, bottom + margin,
                Mathf.Max(bottom + margin, Screen.height - top - margin));
            _panel.position = new Vector3(at.x, at.y, _panel.position.z);
        }

        private Warrior WarriorUnderCursor()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, _unitLayer)) return null;

            var warrior = hit.collider.GetComponentInParent<Warrior>();
            if (warrior == null || warrior.IsDead) return null;

            // Описание — тому, кто рядом с Греховодом (автор, 26.10: «ограничить дальность
            // показа описания воинов при наведении мыши»): о чём думает воин за полкарты,
            // Греховоду не слышно.
            if (Gameplay.SinbinderPlayer.Exists && !(warrior is Gameplay.SinbinderPlayer)
                && Gameplay.CampFocus.GroundDistance(Gameplay.SinbinderPlayer.Where, warrior.transform.position) > Reach)
                return null;
            return warrior;
        }

        /// <summary>Дальше этого от Греховода описание воина не показывается, метров.</summary>
        private const float Reach = 10f;

        private Sturdy SturdyUnderCursor()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 400f, _unitLayer)) return null;
            return hit.collider.GetComponentInParent<Sturdy>();
        }

        /// <summary>Подпись вещи склепа: имя, чья и прочность; рамка — золотом своя, серым чужая.</summary>
        private void ShowSturdy(Sturdy sturdy)
        {
            _shown = null;
            Hovered = null;
            if (_text != null) _text.text = sturdy.Describe();
            Tint(sturdy.Mine ? new Color(0.85f, 0.70f, 0.35f) : new Color(0.55f, 0.55f, 0.58f));
            if (_panel == null) return;
            _panel.gameObject.SetActive(true);
            PlaceNearPointer(actualMouse: true);
        }

        private void Refresh(Warrior warrior)
        {
            var wrapper = warrior.GetComponent<AOSWarriorWrapper>();
            // New command, old AOS context: don't attribute the old verdict to it.
            bool waitingForCommand = warrior.HasCommand &&
                (wrapper == null || !wrapper.HasCurrentCommandDecision);
            bool hasDecision = wrapper != null && wrapper.LastContext != null;
            bool isNeutralInMatch = warrior.Team == Team.Neutral && Lords.Active;
            // У только появившегося ничьего ещё нет решения ИИ, но отношение к нему уже есть.
            // Без этой ветки подсказка не появлялась и второй урок мог стать непроходимым.
            if (!hasDecision && !isNeutralInMatch && !waitingForCommand) { Hide(); return; }

            if (_text != null)
            {
                // Explain what the thirteen voices genuinely voted for.
                // If the warrior was persuaded, tell the player separately
                // that the external whisper changed what actually happened.
                var actualVote = hasDecision && wrapper.LastWhisperCompelled
                    ? wrapper.NaturalDecisionDetail : hasDecision ? wrapper.LastDecisionDetail : default;
                _text.text = waitingForCommand
                    ? Core.Loc.T("Новый приказ отдан. Ожидаем решения воина.")
                    : hasDecision
                        ? PhraseGenerator.Explain(warrior, wrapper.LastContext, actualVote)
                        : string.Empty;
                // Весы — языком суда из сна: кто за приказ, кто против, цветами голосов (63-TEACHING, п. 4).
                if (hasDecision && !waitingForCommand)
                {
                    // A context alone is not a complete visual cache key: hovering
                    // another warrior (or compelling this verdict) must not reuse
                    // the previous warrior's displayed voice balance.
                    if (_scalesFor != wrapper.LastContext ||
                        _scalesWarrior != warrior ||
                        _scalesWhisperCompelled != wrapper.LastWhisperCompelled)
                    {
                        _scalesFor = wrapper.LastContext;
                        _scalesWarrior = warrior;
                        _scalesWhisperCompelled = wrapper.LastWhisperCompelled;
                        _scales = SoulCourt.ScalesLine(warrior, actualVote, wrapper.LastContext);
                    }
                    if (!string.IsNullOrEmpty(_scales)) _text.text += "\n" + _scales;
                    if (wrapper.LastWhisperCompelled)
                        _text.text += "\n" + Core.Loc.T(
                            "Поддался шёпоту: исполняет текущий приказ вопреки голосованию души.");

                    // После отказа игроку нужен следующий осмысленный шаг,
                    // а не ещё один процент послушания. Весь текст исходит
                    // из Counterfactual и положения, не гарантирует результата.
                    if (warrior.HasCommand &&
                        (wrapper.LastDecisionDetail.RefusedCommand || wrapper.LastDecisionDetail.Hesitated))
                    {
                        string advice = AOSDecisionAdvice.Next(wrapper.LastDecisionDetail, wrapper.LastContext);
                        if (!string.IsNullOrEmpty(advice))
                            _text.text += "\n" + Core.Loc.T("Можно попробовать: ") + advice;
                    }
                    else if (warrior.HasCommand &&
                             AOSDecisionAdvice.Fulfilled(wrapper.LastDecisionDetail, wrapper.LastContext) &&
                             wrapper.LastDecisionDetail.Action != ActionType.ObeyCommand)
                    {
                        _text.text += "\n" + Core.Loc.T("Приказ выполнен по собственной воле.");
                    }
                }
                // После суда тот же язык работает на поле боя: герой может
                // увидеть, какое искушение принёс конкретный предмет, не
                // возвращаясь в полный суд и не открывая панель снаряжения.
                int shownTemptations = 0;
                foreach (var item in warrior.Carried)
                {
                    if (item == null || Mathf.Approximately(item.TemptationValue, 0f)) continue;
                    string effect = SquadGear.Effect(item);
                    _text.text += "\n" + Core.Loc.F("{0}: {1}", Core.Loc.Name(item.Name), effect);
                    if (++shownTemptations >= 2) break;
                }
                if (isNeutralInMatch)
                    _text.text = Favor.Attitude(warrior) +
                        (hasDecision || waitingForCommand ? "\n" + _text.text : string.Empty);
                // Титулованный или легенда — дар словами (Gift).
                if (Lords.Active && warrior.TryGetComponent<Gift>(out var gift))
                {
                    var giftLine = Core.Loc.F("Дар: {0}.", gift.What);
                    // В уроке подсказка отношения должна оставаться первой строкой.
                    _text.text = isNeutralInMatch ? _text.text + "\n" + giftLine : giftLine + "\n" + _text.text;
                }
            }

            // Never carry the previous warrior's sin colour into a tooltip
            // for a warrior whose soul has not been revealed or assigned.
            Tint(warrior.Soul != null
                ? SinColor(warrior.Soul.Sin)
                : new Color(0.55f, 0.55f, 0.58f));

            if (_panel != null) _panel.gameObject.SetActive(true);
        }

        /// <summary>
        /// Фон — тёмный, цвет — рамкой. До 06.11 цвет греха красил весь фон подсказки:
        /// на жёлтом Уныния белый текст и цветные имена голосов в строке весов не читались
        /// (кадр лагеря 06.11). Теперь фон лишь чуть тронут этим цветом.
        /// </summary>
        private void Tint(Color edge)
        {
            if (_frame == null) return;
            _frame.color = new Color(Mathf.Lerp(0.06f, edge.r, 0.12f), Mathf.Lerp(0.06f, edge.g, 0.12f),
                                     Mathf.Lerp(0.07f, edge.b, 0.12f), 0.92f);
            if (_edge == null)
            {
                _edge = _frame.GetComponent<Outline>();
                if (_edge == null) _edge = _frame.gameObject.AddComponent<Outline>();
                _edge.effectDistance = new Vector2(2f, -2f);
                _edge.useGraphicAlpha = false;
            }
            _edge.effectColor = new Color(edge.r, edge.g, edge.b, 1f);
        }

        private void Hide()
        {
            _shown = null;
            Hovered = null;
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Цвет доминирующего греха. Таблица — одна на игру
        /// (<see cref="Core.SinPalette"/>): с 15 сентября тот же цвет
        /// горит у воина в глазах, и разъехаться им нельзя.
        /// </summary>
        public static Color SinColor(Core.SinType sin) => Core.SinPalette.Of(sin);
    }
}
