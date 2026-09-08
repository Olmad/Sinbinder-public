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

        /// <summary>
        /// Собранная душа и то, в каком виде её застали.
        ///
        /// Качество приходится нести рядом, а не только внутри спектров:
        /// потери <see cref="Core.SoulDecay.Harvest"/> уже вшил в копию,
        /// но <b>выбор оболочки</b> спрашивает не «насколько потускнела»,
        /// а «сколько воли осталось» — и по потускневшим числам этого
        /// не восстановить. Душа с Гневом 40 могла быть свежей и слабой
        /// или сильной и истлевшей, и это два разных набора тел.
        /// </summary>
        public readonly struct Kept
        {
            public readonly Core.SoulData Soul;
            public readonly Core.SoulQuality Quality;

            public Kept(Core.SoulData soul, Core.SoulQuality quality)
            {
                Soul = soul;
                Quality = quality;
            }
        }

        private readonly List<Kept> _harvested = new();

        /// <summary>
        /// Собранные души, ждущие тела.
        ///
        /// Раньше собранная душа просто исчезала: TryHarvestSoul убирал её
        /// из списка угасающих, писал строчку в лог и возвращал — а держать
        /// её было негде. Жатва не давала ничего, и связывать было нечего.
        ///
        /// Кладём сюда уже с потерями: <see cref="Core.SoulDecay.Harvest"/>
        /// снимает копию по качеству, и промедление оседает в самой душе.
        /// </summary>
        public IReadOnlyList<Kept> Harvested => _harvested;

        /// <summary>Первая в очереди, не забирая. Нужна экрану выбора тела.</summary>
        public Kept PeekHarvested()
            => _harvested.Count == 0 ? default : _harvested[0];

        /// <summary>Забрать душу под связывание. Первая собранная уходит первой.</summary>
        public Kept TakeHarvested()
        {
            if (_harvested.Count == 0) return default;

            var kept = _harvested[0];
            _harvested.RemoveAt(0);
            return kept;
        }

        void Start()
        {
            // До сих пор StartSoulFade не звал никто вообще: души
            // не начинали угасать никогда, жать было нечего, и весь слой
            // существовал как труба без воды. Слушать смерти — работа
            // того, кто ведёт список угасающих, а не чья-то ещё.
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnAnyDeath += OnAnyDeath;
            else
                Debug.LogWarning("[ДУШИ] CombatManager в сцене нет: "
                               + "о смертях узнать неоткуда, жать будет нечего.");
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnAnyDeath -= OnAnyDeath;
        }

        private void OnAnyDeath(Damageable killed, GameObject killer)
        {
            if (killed == null || killed.Warrior == null) return;

            StartSoulFade(killed.Warrior, killed.transform.position);
        }

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

                // Качество ставилось один раз, в момент смерти, и больше
                // не менялось: собранная через минуту душа была та же,
                // что собранная сразу. Цена промедления, расписанная
                // в SoulDecay до последнего множителя, не наступала никогда.
                soul.SoulQuality = Core.SoulDecay.QualityAt(soul.RemainingTime, _fadeTime);

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

            // Префаба нет ни одного, и это не повод оставить душу невидимой:
            // огонёк — единственное, чем игрок узнаёт, что забирать
            // и откуда. Собираем сами, как и весь остальной мир демо.
            SoulIndicator indicator = null;

            if (_soulIndicatorPrefab != null)
            {
                var go = Instantiate(_soulIndicatorPrefab, position, Quaternion.identity);
                indicator = go.GetComponent<SoulIndicator>();
                if (indicator != null) indicator.Initialize(fadingSoul);
            }
            else
            {
                indicator = SoulIndicator.Create(fadingSoul);
            }

            if (indicator != null) _indicators.Add(indicator);

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

                // Копия с потерями по качеству. Оригинал остаётся у мертвеца:
                // мы забираем не его, а то, что от него осталось.
                var kept = Core.SoulDecay.Harvest(closest.Warrior.Soul, closest.SoulQuality);
                if (kept != null) _harvested.Add(new Kept(kept, closest.SoulQuality));
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