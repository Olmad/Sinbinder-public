// Assets/Scripts/Gameplay/Scryer.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Инквизитор второй волны: раз в несколько секунд находит Греховода
    /// магией, и стая бежит туда (<see cref="SinbinderTrail"/>).
    ///
    /// Механика, а не декорация, и держится она на двух вещах, которые
    /// игрок обязан видеть:
    ///
    /// <list type="bullet">
    /// <item><b>кто колдует</b> — в миг заклинания Инквизитор вспыхивает,
    /// и тем же светом вспыхивает Греховод: связь видна глазом, без слов;</item>
    /// <item><b>что будет, если его убить</b> — след остывает: стая дойдёт
    /// до последнего найденного места, и там охота кончится.</item>
    /// </list>
    ///
    /// Часы, а не жребий: одинаковый вход — одинаковый выход. Первое
    /// заклинание — сразу, как волна вышла: иначе охотники первые секунды
    /// не знали бы, куда идти.
    /// </summary>
    public class Scryer : MonoBehaviour
    {
        [Tooltip("Как часто Инквизитор ищет Греховода, в секундах игры. Между "
               + "двумя заклинаниями можно успеть уйти с места, где нашли.")]
        [SerializeField] private float _every = 7f;

        private static readonly Color Glare = new Color(0.78f, 0.22f, 0.95f);
        private const float FlareSeconds = 0.9f;

        private static readonly List<Scryer> All = new();

        /// <summary>Жив ли хоть один, кто умеет искать.</summary>
        public static bool AnyAlive
        {
            get
            {
                foreach (var s in All)
                    if (s != null && s.Alive) return true;
                return false;
            }
        }

        private Damageable _self;
        private float _next;
        private bool _mourned;

        private bool Alive => _self != null && !_self.IsDead;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            _self = GetComponent<Damageable>();
            _next = Time.time;
        }

        void Update()
        {
            if (!Alive)
            {
                Mourn();
                return;
            }

            if (Time.time < _next) return;
            if (!SinbinderPlayer.Exists || SinbinderPlayer.Instance.IsDead) return;

            _next = Time.time + _every;
            Cast();
        }

        private void Cast()
        {
            var quarry = SinbinderPlayer.Instance.transform;
            SinbinderTrail.Mark(quarry.position);

            // Вспышка создаётся заново при каждом заклинании и загорается
            // включённой, даже в тумане: туман выключает свет врага, лишь
            // когда тот уходит из зрения, а новую вспышку до следующего
            // такта не трогает. Колдующий Инквизитор выдаёт себя сквозь
            // туман — это нарочно (слово автора, 24 сентября: «демаскирует
            // его в тумане — тоже интересная часть механики»). Решить, так
            // ли оставить, — после живого прогона.
            Flare(transform);
            Flare(quarry);

            if (SinbinderTrail.ToldFound) return;
            SinbinderTrail.ToldFound = true;

            Object.FindFirstObjectByType<UI.BattleLogUI>()
                  ?.Write("Инквизитор шепчет над ладонью — охотники повернули к Греховоду.");
        }

        /// <summary>
        /// Инквизитор пал. Если он был последним, кто умел искать, — след
        /// остыл, и игрок обязан это узнать: ради этого его и убивали.
        /// </summary>
        private void Mourn()
        {
            if (_mourned) return;
            _mourned = true;

            if (AnyAlive || !SinbinderTrail.Known || SinbinderTrail.ToldLost) return;
            SinbinderTrail.ToldLost = true;

            Object.FindFirstObjectByType<UI.BattleLogUI>()
                  ?.Write("Инквизитор пал — охотники потеряли след.");
        }

        /// <summary>
        /// Вспышка на миг заклинания. Свет гаснет сам и уничтожается по сроку,
        /// даже если Инквизитор погибнет посреди неё и гасить будет некому.
        /// </summary>
        private void Flare(Transform on)
        {
            var go = new GameObject("Взгляд Инквизитора");
            go.transform.SetParent(on, false);
            go.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Glare;
            light.range = 4.5f;
            light.intensity = 0f;

            Destroy(go, FlareSeconds + 0.1f);
            StartCoroutine(Fade(light));
        }

        private static IEnumerator Fade(Light light)
        {
            float t = 0f;
            while (t < FlareSeconds && light != null)
            {
                t += Time.deltaTime;
                float k = t / FlareSeconds;

                // Вспыхнуть быстро, гаснуть медленно: взгляд, а не мигание.
                light.intensity = 6f * (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f);
                yield return null;
            }
        }
    }
}
