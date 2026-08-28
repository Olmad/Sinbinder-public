using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public class HarvestableSoul : MonoBehaviour
    {
        [SerializeField] private SoulData _soul;
        [SerializeField] private SoulQuality _quality;
        [SerializeField] private float _deathTime;

        private bool _harvested = false;

        public SoulData Soul => _soul;
        public SoulQuality Quality => _quality;
        public bool IsHarvested => _harvested;

        void Start()
        {
            _deathTime = Time.time;
            _quality = SoulQuality.Shock;
        }

        void Update()
        {
            if (_harvested) return;

            float elapsed = Time.time - _deathTime;

            if (elapsed > 600f)
                _quality = SoulQuality.Dissolved;
            else if (elapsed > 300f)
                _quality = SoulQuality.Fading;
            else if (elapsed > 60f)
                _quality = SoulQuality.Acceptance;
        }

        public SoulData Harvest(SoulQuality harvestedQuality)
        {
            if (_harvested) return null;
            _harvested = true;

            // Копия с потерями по качеству. До этого качество считалось
            // по времени и ни на что не влияло: душа была одна и та же
            // хоть через минуту, хоть через час, и смерть воина не стоила
            // ничего. Теперь стоит — и у боя появляется вторая ставка
            // помимо победы: успеть.
            var soul = SoulDecay.Harvest(_soul, harvestedQuality);

            Debug.Log($"[HARVEST] Душа собрана: {soul.Name} — {SoulDecay.Describe(harvestedQuality)}");

            Destroy(gameObject, 0.1f);
            return soul;
        }

        public void ForceDissolve()
        {
            if (_harvested) return;
            _harvested = true;
            Debug.Log($"[SOUL] Душа {_soul.Name} растворилась в Некроэфире");
            Destroy(gameObject);
        }
    }
}