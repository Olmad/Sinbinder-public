using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class SoulManager : MonoBehaviour
    {
        public static SoulManager Instance { get; private set; }

        [SerializeField] private float _fadeTime = 60f;
        [SerializeField] private float _harvestRadius = 3f;
        [SerializeField] private GameObject _soulIndicatorPrefab;

        private List<FadingSoul> _fadingSouls = new();
        private List<SoulIndicator> _indicators = new();

        public System.Action<FadingSoul> OnSoulHarvested;
        public System.Action<FadingSoul> OnSoulFaded;

        public int FadingCount => _fadingSouls.Count;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            for (int i = _fadingSouls.Count - 1; i >= 0; i--)
            {
                var soul = _fadingSouls[i];
                soul.RemainingTime -= Time.deltaTime;

                if (soul.RemainingTime <= 0f)
                {
                    Debug.Log($"[SOUL] Душа {soul.Warrior.DisplayName} угасла навсегда");
                    OnSoulFaded?.Invoke(soul);

                    RemoveIndicator(soul);
                    _fadingSouls.RemoveAt(i);
                }
            }
        }

        public void StartSoulFade(Warrior warrior, Vector3 position)
        {
            var fadingSoul = new FadingSoul
            {
                Warrior = warrior,
                Position = position,
                RemainingTime = _fadeTime,
                SoulQuality = Core.SoulQuality.Shock
            };

            _fadingSouls.Add(fadingSoul);

            if (_soulIndicatorPrefab != null)
            {
                var go = Instantiate(_soulIndicatorPrefab, position, Quaternion.identity);
                var indicator = go.GetComponent<SoulIndicator>();
                if (indicator != null)
                {
                    indicator.Initialize(fadingSoul);
                    _indicators.Add(indicator);
                }
            }

            Debug.Log($"[SOUL] Душа {warrior.DisplayName} покинула тело. Угаснет через {_fadeTime} сек.");
        }

        public FadingSoul TryHarvestSoul(Vector3 harvesterPosition)
        {
            FadingSoul closest = null;
            float minDist = _harvestRadius;

            foreach (var soul in _fadingSouls)
            {
                float dist = Vector3.Distance(harvesterPosition, soul.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = soul;
                }
            }

            if (closest != null)
            {
                _fadingSouls.Remove(closest);
                Debug.Log($"[SOUL] Душа {closest.Warrior.DisplayName} собрана! Осталось угасающих: {_fadingSouls.Count}");
                OnSoulHarvested?.Invoke(closest);
                return closest;
            }

            Debug.Log("[SOUL] Нет душ поблизости для сбора");
            return null;
        }

        public void RemoveIndicator(FadingSoul soul)
        {
            for (int i = _indicators.Count - 1; i >= 0; i--)
            {
                if (_indicators[i] != null && _indicators[i].gameObject != null)
                {
                    Destroy(_indicators[i].gameObject);
                    _indicators.RemoveAt(i);
                    break;
                }
            }
        }

        public List<FadingSoul> GetAllFadingSouls()
        {
            return _fadingSouls;
        }
    }

    [System.Serializable]
    public class FadingSoul
    {
        public Warrior Warrior;
        public Vector3 Position;
        public float RemainingTime;
        public Core.SoulQuality SoulQuality;
    }
}