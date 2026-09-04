// Assets/Scripts/UI/HealthBarUI.cs
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    public class HealthBarUI : MonoBehaviour
    {
        private Slider _slider;
        private Image _fillImage;
        private Damageable _damageable;
        private bool _isDead;
        private GameObject _overheadRoot; // ссылка на корень OverheadUI для возврата в пул

        public Slider Slider => _slider;
        public Image FillImage => _fillImage;

        public void ManualInit(Damageable damageable, Slider slider, Image fillImage, GameObject overheadRoot = null)
        {
            _damageable = damageable;
            _slider = slider;
            _fillImage = fillImage;
            _overheadRoot = overheadRoot;
            _isDead = false;
            gameObject.SetActive(true);

            if (_slider != null)
            {
                _slider.minValue = 0f;
                _slider.maxValue = damageable.MaxHP;
                _slider.value = damageable.HP;
            }
            UpdateColor();
        }

        void Update()
        {
            if (_damageable == null || _slider == null) return;

            if (!_isDead && _damageable.IsDead)
            {
                _isDead = true;
                // Возвращаем в пул
                if (_overheadRoot != null)
                    OverheadUIPool.Return(_overheadRoot);
                else
                    gameObject.SetActive(false);
                return;
            }

            if (_isDead) return;

            _slider.value = _damageable.HP;
            UpdateColor();
        }

        private void UpdateColor()
        {
            if (_fillImage == null || _slider == null) return;
            float pct = _slider.value / _slider.maxValue;
            if (pct > 0.6f) _fillImage.color = Color.green;
            else if (pct > 0.3f) _fillImage.color = Color.yellow;
            else _fillImage.color = Color.red;
        }

        void OnDestroy()
        {
            // На всякий случай возвращаем в пул при уничтожении
            if (_overheadRoot != null)
                OverheadUIPool.Return(_overheadRoot);
        }
    }
}