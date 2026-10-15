// Assets/Scripts/UI/OverheadUI.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// Корень надголовного интерфейса воина: полоска здоровья, полоса сил,
    /// знак окружения и значок намерения.
    /// Собирается в UnitFactory, переиспользуется через OverheadUIPool.
    /// </summary>
    public class OverheadUI : MonoBehaviour
    {
        [SerializeField] private HealthBarUI _healthBar;
        [SerializeField] private DecisionIconUI _decisionIcon;

        public HealthBarUI HealthBar
        {
            get => _healthBar;
            set => _healthBar = value;
        }

        public DecisionIconUI DecisionIcon
        {
            get => _decisionIcon;
            set => _decisionIcon = value;
        }

        /// <summary>Полоса сил под здоровьем (<see cref="StaminaBarUI"/>).</summary>
        public StaminaBarUI Stamina { get; set; }

        /// <summary>Знак «окружён» слева от здоровья (<see cref="SurroundedMarkUI"/>).</summary>
        public SurroundedMarkUI Surrounded { get; set; }
    }
}
