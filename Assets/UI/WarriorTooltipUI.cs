// Assets/UI/WarriorTooltipUI.cs
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

        void Awake()
        {
            _cam = Camera.main;
            Hide();
        }

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            var warrior = WarriorUnderCursor();
            if (warrior == null) { Hide(); return; }

            if (warrior != _shown) { _shown = warrior; Refresh(warrior); }

            if (_panel != null)
                _panel.position = (Vector2)Input.mousePosition + _offset;
        }

        private Warrior WarriorUnderCursor()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, _unitLayer)) return null;

            var warrior = hit.collider.GetComponentInParent<Warrior>();
            return (warrior != null && !warrior.IsDead) ? warrior : null;
        }

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
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Цвет доминирующего греха. Игрок не осознаёт связь, но набирает
        /// её за десяток подсказок и потом читает рамку быстрее текста.
        /// </summary>
        public static Color SinColor(Core.SinType sin)
        {
            switch (sin)
            {
                case Core.SinType.Greed:    return new Color(0.85f, 0.68f, 0.24f); // старое золото
                case Core.SinType.Pride:    return new Color(0.62f, 0.45f, 0.78f); // фиолетовый
                case Core.SinType.Wrath:    return new Color(0.78f, 0.25f, 0.20f); // тёмно-красный
                case Core.SinType.Envy:     return new Color(0.36f, 0.62f, 0.42f); // болотный
                case Core.SinType.Lust:     return new Color(0.80f, 0.40f, 0.55f); // тусклый розовый
                case Core.SinType.Gluttony: return new Color(0.72f, 0.52f, 0.30f); // ржавый
                case Core.SinType.Sloth:    return new Color(0.48f, 0.50f, 0.54f); // серый
                default:                    return new Color(0.80f, 0.79f, 0.76f); // пепел
            }
        }
    }
}
