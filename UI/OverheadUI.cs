// Assets/_Project/Scripts/UI/OverheadUI.cs
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// Корень надголовного интерфейса воина: полоска здоровья и значок намерения.
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
    }
}
