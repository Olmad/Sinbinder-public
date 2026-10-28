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
        private Warrior _shown;

        /// <summary>
        /// Над кем подсказка сейчас. Урок отказа (<see cref="Lesson"/>) ждёт,
        /// пока её прочтут над отказавшим.
        /// </summary>
        public static Warrior Hovered { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Hovered = null;

        void Awake()
        {
            _cam = Camera.main;
            Hide();
        }

        void OnDisable() => Hide();

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // Трон и дверь склепа (автор, 27.10: «нельзя посмотреть трон на количество здоровья,
            // то же самое с дверью склепа»): прочность — при любой ступени прозрачности: это вещь,
            // а не чьи-то мысли.
            var sturdy = SturdyUnderCursor();
            if (sturdy != null) { ShowSturdy(sturdy); return; }

            // Вторая ступень прозрачности. Игрок, выбравший только значки,
            // причин не спрашивает — и подсказка ему не мешает.
            if (!Core.Transparency.Shows(Core.Clarity.Tooltips)) { Hide(); return; }

            var warrior = WarriorUnderCursor();
            if (warrior == null) { Hide(); return; }

            if (warrior != _shown) { _shown = warrior; Refresh(warrior); }
            Hovered = _shown;

            if (_panel != null)
                _panel.position = (Vector2)Input.mousePosition + _offset;
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
            if (_frame != null) _frame.color = sturdy.Mine ? new Color(0.85f, 0.70f, 0.35f) : new Color(0.55f, 0.55f, 0.58f);
            if (_panel == null) return;
            _panel.gameObject.SetActive(true);
            _panel.position = (Vector2)Input.mousePosition + _offset;
        }

        private void Refresh(Warrior warrior)
        {
            var wrapper = warrior.GetComponent<AOSWarriorWrapper>();
            if (wrapper == null || wrapper.LastContext == null) { Hide(); return; }

            if (_text != null)
            {
                _text.text = PhraseGenerator.Explain(warrior, wrapper.LastContext, wrapper.LastDecisionDetail);
                // Ничей («Перетягивание») — первым делом, как он к тебе: словом, без числа (Favor).
                if (warrior.Team == Team.Neutral && Lords.Active)
                    _text.text = Favor.Attitude(warrior) + "\n" + _text.text;
            }

            if (_frame != null)
                _frame.color = SinColor(warrior.Soul.Sin);

            if (_panel != null) _panel.gameObject.SetActive(true);
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
