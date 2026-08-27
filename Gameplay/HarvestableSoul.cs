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

            var soul = new SoulData(_soul.Name, _soul.Sin, _soul.Moral, _soul.Level, _soul.SinIntensity, _soul.Memory);

            Debug.Log($"[HARVEST] Душа собрана: {soul.Name} (качество: {harvestedQuality})");

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