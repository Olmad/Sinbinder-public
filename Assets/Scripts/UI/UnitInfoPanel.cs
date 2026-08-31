using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    public class UnitInfoPanel : MonoBehaviour
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _sinText;
        [SerializeField] private Text _intensityText;
        [SerializeField] private Text _moralText;
        [SerializeField] private Text _loyaltyText;

        void Start()
        {
            if (Gameplay.SelectionManager.Instance != null)
                Gameplay.SelectionManager.Instance.OnSelectionChanged += UpdateInfo;
        }

        void OnDestroy()
        {
            if (Gameplay.SelectionManager.Instance != null)
                Gameplay.SelectionManager.Instance.OnSelectionChanged -= UpdateInfo;
        }

        private void UpdateInfo(System.Collections.Generic.List<Gameplay.SelectionComponent> selected)
        {
            if (selected.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            var warrior = selected[0].Warrior;
            if (warrior == null) return;

            if (_nameText != null)
                _nameText.text = warrior.DisplayName;

            if (_sinText != null)
                _sinText.text = $"Грех: {warrior.Soul.GetSinName()}";

            if (_intensityText != null)
                _intensityText.text = $"Характер: {warrior.Virtue.GetDescription()}";

            if (_moralText != null)
                _moralText.text = $"Мораль: {warrior.Soul.GetMoralName()}";

            if (_loyaltyText != null)
            {
                string loyaltyDesc;
                if (warrior.Loyalty > 70f) loyaltyDesc = "Предан вам";
                else if (warrior.Loyalty < 30f) loyaltyDesc = "Готов предать";
                else loyaltyDesc = "Нейтральна";
                _loyaltyText.text = $"Верность: {loyaltyDesc}";
            }
        }
    }
}