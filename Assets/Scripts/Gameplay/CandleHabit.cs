// Assets/Scripts/Gameplay/CandleHabit.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Примета Вейна Тихого — свеча в палатке. Облачная, 26 октября: «Вейна в палатке
    /// сверху не видно, нужен свет свечи сквозь полог» («Красота сейчас», п. 7); под
    /// именем — «Говорит шёпотом и читает при свече, пока лагерь спит» (Signature).
    ///
    /// Плотная ткань света не пропускает, поэтому полог его палатки тлеет сам:
    /// тёплое свечение изнутри своей копии материала (остальные палатки — нет)
    /// и неровное, как от живого огня; у входа — маленький тёплый свет на землю.
    /// Сверху видно: среди серых палаток одна светится. Гаснет, когда Вейн пал
    /// («В палатке Вейна догорает свеча. Задуть её некому» — догорает и гаснет).
    /// </summary>
    [RequireComponent(typeof(Warrior))]
    public class CandleHabit : MonoBehaviour
    {
        private static readonly Color Flame = new Color(1.00f, 0.62f, 0.28f);
        private const float Glow = 0.55f;          // свечение ткани в покое
        private const float BurnOut = 25f;         // сколько догорает после гибели, секунд

        private Warrior _warrior;
        private Material[] _cloth;
        private Light _light;
        private float _phase;
        private float _diedAt = -1f;

        /// <summary>Тот ли это — по прозвищу «Тихий» (Signature: «читает при свече»).</summary>
        public static bool Reader(Warrior w)
            => w != null && w.DisplayName != null && w.DisplayName.Contains("Тихий");

        void Start()
        {
            _warrior = GetComponent<Warrior>();
            _phase = Wardrobe.Stamp(_warrior.DisplayName) % 100 * 0.37f;

            var tent = CampLife.TentOf(_warrior);
            var inside = tent != null ? tent.GetComponent<TentInterior>() : null;
            if (inside == null) { enabled = false; return; }

            // Своя копия материала полога — только у этой палатки (.materials).
            var mats = new System.Collections.Generic.List<Material>();
            foreach (var r in tent.GetComponentsInChildren<Renderer>())
            {
                if (!inside.IsCover(r)) continue;
                var own = r.materials;
                foreach (var m in own)
                {
                    if (m == null || !m.HasProperty("_EmissionColor")) continue;
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mats.Add(m);
                }
                r.materials = own;
            }
            _cloth = mats.ToArray();

            var go = new GameObject("Свеча Вейна");
            go.transform.SetParent(tent, false);
            go.transform.position = inside.Doorstep(-0.6f) + Vector3.up * 0.5f;
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Flame;
            _light.range = 3.2f;
            _light.shadows = LightShadows.None;
            Debug.Log($"[ПРИМЕТА] свеча {_warrior.DisplayName}: палатка «{tent.name}», ткани {_cloth.Length}");
        }

        void Update()
        {
            if (_warrior == null) return;
            if (_warrior.IsDead && _diedAt < 0f) _diedAt = Time.time;

            // Огонь живой: две медленные волны и мелкая дрожь, по имени — не в такт с костром.
            float t = Time.time + _phase;
            float flicker = 0.82f + 0.10f * Mathf.Sin(t * 2.3f) + 0.06f * Mathf.Sin(t * 7.1f)
                          + 0.04f * Mathf.PerlinNoise(t * 3f, _phase);
            float life = _diedAt < 0f ? 1f : Mathf.Clamp01(1f - (Time.time - _diedAt) / BurnOut);

            var glow = Flame * (Glow * flicker * life);
            if (_cloth != null)
                foreach (var m in _cloth) if (m != null) m.SetColor("_EmissionColor", glow);
            if (_light != null) _light.intensity = 0.9f * flicker * life;
            if (life <= 0f) enabled = false;
        }
    }
}
