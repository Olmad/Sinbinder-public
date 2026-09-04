// Assets/Scripts/Gameplay/CrystalBall.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Хрустальный шар на столе военного совета.
    ///
    /// Это предмет, а не пункт меню. По сценарию (docs/09-PROLOGUE.md §4)
    /// игрок подходит к нему на доле 2, чтобы отправить отряд, а на доле 3
    /// шар загорается красным — и игрок смотрит в него, чтобы увидеть,
    /// как гаснут его отряды. Тревога должна быть вещью в лагере,
    /// которую видно от палатки, а не строкой в сводке.
    ///
    /// Пульсация берёт Time и потому недетерминирована. Это допустимо:
    /// правило повторяемости охраняет решения движка, а свет на решения
    /// не влияет никак. Ни один модуль сюда не смотрит.
    /// </summary>
    public class CrystalBall : MonoBehaviour
    {
        [SerializeField] private Light _glow;

        [Tooltip("Пока всё спокойно: холодный, ровный.")]
        [SerializeField] private Color _calm = new Color(0.45f, 0.62f, 0.95f);

        [Tooltip("Тревога доли 3: отряды гибнут один за другим.")]
        [SerializeField] private Color _alarm = new Color(0.95f, 0.18f, 0.12f);

        [SerializeField] private float _calmIntensity = 1.4f;
        [SerializeField] private float _alarmIntensity = 4.5f;

        /// <summary>Насколько живо дышит свет. Ноль — не дышит вовсе.</summary>
        [SerializeField] private float _pulseDepth = 0.18f;
        [SerializeField] private float _pulseSpeed = 0.6f;

        public bool IsAlarmed { get; private set; }

        void Awake()
        {
            if (_glow == null) _glow = GetComponentInChildren<Light>();
            Apply();
        }

        /// <summary>Тревога: шар наливается красным. Доля 3.</summary>
        public void Alarm()
        {
            IsAlarmed = true;
            Apply();
            Debug.Log("[ШАР] Тревога: отряды гаснут.");
        }

        /// <summary>Вернуть спокойный свет.</summary>
        public void Calm()
        {
            IsAlarmed = false;
            Apply();
        }

        private void Apply()
        {
            if (_glow == null) return;
            _glow.color = IsAlarmed ? _alarm : _calm;
            _glow.intensity = IsAlarmed ? _alarmIntensity : _calmIntensity;
        }

        void Update()
        {
            if (_glow == null || _pulseDepth <= 0f) return;

            float baseline = IsAlarmed ? _alarmIntensity : _calmIntensity;
            float breath = Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f);

            // Тревожный шар дышит вдвое чаще: это единственная разница,
            // которую видно от палатки, не подходя к столу.
            if (IsAlarmed) breath = Mathf.Sin(Time.time * _pulseSpeed * 4f * Mathf.PI);

            _glow.intensity = baseline * (1f + breath * _pulseDepth);
        }
    }
}
