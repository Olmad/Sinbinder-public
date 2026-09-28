// Assets/Scripts/Gameplay/SoulJarGlow.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Душа в банке горит своими грехами.
    ///
    /// Автор, 27 сентября: «сделай нашу банку по подобию и добавь вариант
    /// с душой — её цвет зависит от грехов». Банка — <c>Props/SoulJar</c>
    /// и <c>Props/SoulJarFull</c> (<c>Tools/blender/props.py</c>); у полной
    /// внутри огонёк: ядро — материал «Soul», язык пламени и искры —
    /// «Soul Dim». Этот компонент их красит:
    ///
    /// * <b>ядро</b> — спектром, дальше всех ушедшим от нуля, — тем же,
    ///   что <see cref="SoulData.Sin"/> и глаза воина (<see cref="SinEyes"/>);
    /// * <b>пламя и искры</b> — вторым за ним. Одна краска на всю душу
    ///   говорила бы «грех», а у души их семь: второй голос — то, что
    ///   отличает жадного гордеца от жадного лентяя;
    /// * <b>добродетель</b> (спектр ниже нуля) — тот же цвет, выбеленный:
    ///   чистый свет, а не новая краска. Добродетель — отрицательная
    ///   половина шкалы греха (CLAUDE.md), а не восьмой цвет;
    /// * <b>сила</b> — яркостью и светом вокруг. Цифр нет, как нигде.
    ///
    /// Свет дышит медленно, и фаза у каждой банки своя — от места, где
    /// она стоит, а не от жребия: полка из девяти банок, мерцающих в такт,
    /// читается гирляндой.
    /// </summary>
    public class SoulJarGlow : MonoBehaviour
    {
        [Tooltip("Громче всех звучащий спектр: цвет ядра.")]
        [SerializeField] private SinType _loudest = SinType.Greed;

        [Tooltip("Второй за ним: цвет пламени и искр.")]
        [SerializeField] private SinType _next = SinType.Pride;

        [Tooltip("Насколько громко звучит душа: яркость и свет вокруг.")]
        [SerializeField, Range(0f, 1f)] private float _force = 0.6f;

        [Tooltip("Громкий спектр — добродетель: свет бледнее, чище.")]
        [SerializeField] private bool _virtue;

        /// <summary>Имена материалов — из props.py. «Soul Dim» проверяется первым: он тоже начинается с «Soul».</summary>
        private const string Wisp = "Soul Dim";
        private const string Core = "Soul";
        private const string LampName = "Свет души";

        private Material[] _cores = new Material[0];
        private Material[] _wisps = new Material[0];
        private Color _coreGlow, _wispGlow;
        private Light _lamp;
        private float _lampBase;
        private float _phase;
        private bool _painted;

        /// <summary>
        /// Два самых громких спектра души, сила первого и знак.
        /// Чистая функция: одна и та же душа — одна и та же банка.
        /// </summary>
        public static void Read(SoulData soul, out SinType loudest, out SinType next,
                                out float force, out bool virtue)
        {
            loudest = soul != null ? soul.Sin : SinType.Greed;
            next = loudest;
            float best = -1f;

            if (soul != null)
            {
                for (int i = 0; i < SoulData.SpectrumCount; i++)
                {
                    var sin = (SinType)i;
                    if (sin == loudest) continue;

                    float loud = Mathf.Abs(soul.Get(sin));
                    if (loud > best) { best = loud; next = sin; }
                }
            }

            // Второй молчит — пламя того же цвета, что ядро: одноголосая душа.
            if (best <= 0f) next = loudest;

            float value = soul != null ? soul.Get(loudest) : 0f;
            force = Mathf.Clamp01(Mathf.Abs(value) / 100f);
            virtue = value < 0f;
        }

        /// <summary>Зажечь по душе. Зовут те, кто ставит банку с настоящей душой.</summary>
        public void Show(SoulData soul)
        {
            Read(soul, out _loudest, out _next, out _force, out _virtue);
            Paint();
        }

        /// <summary>
        /// Задать грехи прямо — для банки без настоящей души: в палатке
        /// Греховода стоит «последняя, кого он не донёс». Работает и в
        /// редакторе — сборщик сцены видит банку уже горящей, а поля
        /// сохраняются вместе со сценой.
        /// </summary>
        public void Set(SinType loudest, SinType next, float force, bool virtue = false)
        {
            _loudest = loudest;
            _next = next;
            _force = Mathf.Clamp01(force);
            _virtue = virtue;
            Paint();
        }

        /// <summary>
        /// Свои материалы этой банки. В игре — <c>.materials</c>: общий
        /// материал покрасил бы разом все банки в грех последней (как
        /// у <see cref="SinEyes"/>). В редакторе <c>.materials</c> копирует
        /// материалы молча и жалуется на утечку — там копии делаются явно.
        /// </summary>
        private static Material[] Own(Renderer r)
        {
            if (Application.isPlaying) return r.materials;

            var shared = r.sharedMaterials;
            var copies = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                copies[i] = shared[i] == null ? null : new Material(shared[i]) { name = shared[i].name };
            }
            return copies;
        }

        private static void Keep(Renderer r, Material[] mats)
        {
            if (Application.isPlaying) r.materials = mats;
            else r.sharedMaterials = mats;
        }

        void Start()
        {
            if (!_painted) Paint();
        }

        /// <summary>Цвет спектра в банке: добродетель — выбеленным.</summary>
        public static Color Tone(SinType sin, bool virtue)
        {
            var c = SinPalette.Of(sin);
            return virtue ? Color.Lerp(c, new Color(0.95f, 0.95f, 0.90f), 0.45f) : c;
        }

        private void Paint()
        {
            _painted = true;

            var core = Tone(_loudest, _virtue);
            // Второй спектр той же души — его знак не храним: добродетель
            // решает ядро, пламя — только голос.
            var wisp = SinPalette.Of(_next);

            // Ярче единицы, но не намного: порог свечения в профиле
            // «Взгляд» — 1,05, и то, что сильно выше, выбеливается,
            // а цвет греха и есть смысл банки.
            _coreGlow = core * (0.70f + 0.50f * _force);
            _wispGlow = wisp * (0.65f + 0.40f * _force);

            var cores = new System.Collections.Generic.List<Material>();
            var wisps = new System.Collections.Generic.List<Material>();

            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var mats = Own(r);
                bool touched = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;

                    string n = mats[i].name;
                    bool isWisp = n.StartsWith(Wisp);
                    bool isCore = !isWisp && n.StartsWith(Core);
                    if (!isWisp && !isCore) continue;

                    var tone = isWisp ? wisp : core;
                    mats[i].color = tone;
                    if (mats[i].HasProperty("_EmissionColor"))
                    {
                        mats[i].EnableKeyword("_EMISSION");
                        mats[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        mats[i].SetColor("_EmissionColor", isWisp ? _wispGlow : _coreGlow);
                    }

                    (isWisp ? wisps : cores).Add(mats[i]);
                    touched = true;
                }

                if (touched) Keep(r, mats);
            }

            _cores = cores.ToArray();
            _wisps = wisps.ToArray();

            // Свет вокруг — маленький и без теней, как огонь греха у воина:
            // банок на полке девять, и тени от девяти огоньков стоили бы
            // дороже всей полки.
            // Свет, сохранённый со сценой, — тот же: второй не заводим.
            if (_lamp == null)
            {
                var old = transform.Find(LampName);
                if (old != null) _lamp = old.GetComponent<Light>();
            }

            if (_cores.Length > 0 && _lamp == null)
            {
                var go = new GameObject(LampName);
                go.transform.SetParent(transform, false);
                go.transform.position = CoreCentre();

                _lamp = go.AddComponent<Light>();
                _lamp.type = LightType.Point;
                _lamp.shadows = LightShadows.None;
            }

            if (_lamp != null)
            {
                _lamp.color = core;
                _lamp.range = 0.7f + 0.5f * _force;
                _lampBase = 0.25f + 0.55f * _force;
                _lamp.intensity = _lampBase;
            }

            // Фаза — от места: полка не мерцает в такт.
            var p = transform.position;
            _phase = Mathf.Repeat(p.x * 1.7f + p.z * 2.3f, Mathf.PI * 2f);
        }

        /// <summary>Середина огонька: ядро в модели на трети роста банки.</summary>
        private Vector3 CoreCentre()
        {
            var r = GetComponentInChildren<Renderer>();
            if (r == null) return transform.position;
            var b = r.bounds;
            return new Vector3(b.center.x, b.min.y + b.size.y * 0.36f, b.center.z);
        }

        void Update()
        {
            if (_cores.Length == 0 && _wisps.Length == 0) return;

            // Дышит, а не мигает: полтора-два вдоха в секунду было бы
            // тревогой, один вдох в три секунды — жизнью.
            float breath = 1f + 0.16f * Mathf.Sin(Time.time * 2.1f + _phase);

            foreach (var m in _cores)
                if (m != null) m.SetColor("_EmissionColor", _coreGlow * breath);
            foreach (var m in _wisps)
                if (m != null) m.SetColor("_EmissionColor", _wispGlow * (2f - breath));

            if (_lamp != null) _lamp.intensity = _lampBase * breath;
        }
    }
}
