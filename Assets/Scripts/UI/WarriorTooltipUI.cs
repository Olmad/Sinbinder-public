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
            // Вторая ступень прозрачности. Игрок, выбравший только значки,
            // причин не спрашивает — и подсказка ему не мешает.
            if (!Core.Transparency.Shows(Core.Clarity.Tooltips)) { Hide(); return; }

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

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

        private void Refresh(Warrior warrior)
        {
            var wrapper = warrior.GetComponent<AOSWarriorWrapper>();
            if (wrapper == null || wrapper.LastContext == null) { Hide(); return; }

            if (_text != null)
                _text.text = PhraseGenerator.Explain(warrior, wrapper.LastContext, wrapper.LastDecisionDetail);

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
