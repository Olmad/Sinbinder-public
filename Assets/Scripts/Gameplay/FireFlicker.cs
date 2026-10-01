// Assets/Scripts/Gameplay/FireFlicker.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Живой огонь: свет костра, жаровни, фонаря дышит и чуть пляшет
    /// (41-SHOWCASE п. 2 — «тёплый костёр»).
    ///
    /// До 1 октября огонь в игре светил ровно, как лампа. Ночь Knightcore
    /// держится на встрече тёплого огня с холодом (00-GDD.md §9), а ровный
    /// огонь читается электрическим. Яркость и место света плывут по шуму
    /// Перлина — плавно, без жребия: одинаковое время — одинаковый огонь;
    /// фаза у каждого своя, от места, где он стоит, — два костра не мигают
    /// в такт.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FireFlicker : MonoBehaviour
    {
        [Tooltip("На сколько гуляет яркость, доля от исходной.")]
        [SerializeField] private float _depth = 0.22f;

        [Tooltip("Как быстро, вдохов в секунду.")]
        [SerializeField] private float _speed = 2.6f;

        [Tooltip("На сколько пляшет точка света, метры: тени шевелятся.")]
        [SerializeField] private float _sway = 0.06f;

        private Light _light;
        private float _base;
        private Vector3 _home;
        private float _phase;

        /// <summary>Настроить из сборщика: костёр — сильнее, фонарь — едва.</summary>
        public void Tune(float depth, float speed, float sway)
        {
            _depth = depth;
            _speed = speed;
            _sway = sway;
        }

        void Start()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _home = transform.localPosition;

            var p = transform.position;
            _phase = Mathf.Repeat(p.x * 3.7f + p.z * 5.3f, 100f);
        }

        void Update()
        {
            float t = Time.time * _speed + _phase;

            // Два слоя шума: медленное дыхание и быстрые языки поверх.
            float slow = Mathf.PerlinNoise(t * 0.35f, _phase) - 0.5f;
            float fast = Mathf.PerlinNoise(t * 1.9f, _phase + 17f) - 0.5f;
            _light.intensity = _base * (1f + _depth * (slow * 1.4f + fast * 0.8f));

            if (_sway > 0f)
                transform.localPosition = _home + new Vector3(
                    (Mathf.PerlinNoise(t, _phase + 31f) - 0.5f) * _sway,
                    (Mathf.PerlinNoise(t, _phase + 47f) - 0.5f) * _sway * 0.6f,
                    (Mathf.PerlinNoise(t, _phase + 59f) - 0.5f) * _sway);
        }
    }
}
