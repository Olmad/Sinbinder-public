// Assets/Scripts/Audio/DeathVoice.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Audio
{
    /// <summary>
    /// Голос Смерти — для пешки-пасхалки. Автор, 21 октября: «голос у пешки
    /// должен быть как у самой СМЕРТИ. Такой, который будет пробирать до
    /// дрожи»; записи не будет — «синтезируй, там посмотрим».
    ///
    /// Не бип на букву, как у воинов (<see cref="VoiceGenerator"/>), а одна
    /// дорожка на строку: низкий гул (около 42 Гц, пила под мягким срезом
    /// и суб на октаву ниже), медленно дышащий; на каждой гласной —
    /// шёпот, полоса шума около 700 Гц; поверх — эхо склепа (три гребёнки
    /// и рассеиватель), хвост в две с лишним секунды. Время букв — то же,
    /// что у печати полосы (<see cref="UI.Letterbox"/>), но медленнее.
    ///
    /// Пока говорит — мир молчит: все прочие источники звука уходят почти
    /// в ноль, остаётся редкий глухой удар сердца; огни рядом меркнут.
    /// Всё возвращается, когда она замолчит. Без жребия: та же строка —
    /// тот же звук.
    /// </summary>
    public class DeathVoice : MonoBehaviour
    {
        /// <summary>Букв в секунду: Смерть не торопится.</summary>
        public const float Letters = 18f;

        private const int Rate = 22050;
        private const float Tail = 2.4f;
        private const float Gain = 0.55f;

        private AudioSource _voice;
        private AudioSource _echo;      // хвост прошлой строки Бога Смерти, пока звучит новая
        private AudioSource _heart;
        private Coroutine _beat;
        private readonly List<(AudioSource Src, float Was)> _hushed = new();
        private readonly List<(Light Lamp, float Was, FireFlicker Flicker)> _dimmed = new();

        private static DeathVoice _instance;

        /// <summary>Начать: тишина вокруг, огни меркнут у <paramref name="where"/>, сердце.</summary>
        public static void Begin(Vector3 where)
        {
            if (_instance == null)
            {
                var go = new GameObject("DeathVoice");
                _instance = go.AddComponent<DeathVoice>();
            }
            _instance.Open(where);
        }

        /// <summary>Сказать строку. Возвращает, сколько она звучит до хвоста.</summary>
        public static float Say(string line)
        {
            if (_instance == null || string.IsNullOrEmpty(line)) return 0f;
            var clip = Make(line, out float spoken);
            _instance._voice.Stop();
            _instance._voice.clip = clip;
            _instance._voice.Play();
            return spoken;
        }

        /// <summary>Замолчать: звук и огни возвращаются.</summary>
        public static void End()
        {
            if (_instance != null) _instance.Close();
        }

        // ───────────────────────────────────────── Бог Смерти на суде души

        private Coroutine _hush;

        /// <summary>
        /// Бог Смерти ведёт суд души (автор, 06.11: «Вместо Голоса Суда — сделай
        /// Голос Бога Смерти»). Мир не замолкает и сердце не бьётся: в зале суда и так
        /// тихо, а Смерть говорит много строк подряд. Голос — <see cref="Speech"/>.
        /// Возвращает, сколько звучит строка до хвоста.
        /// </summary>
        public static float Utter(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0f;
            if (_instance == null) _instance = new GameObject("DeathVoice").AddComponent<DeathVoice>();
            var me = _instance;
            int ticket = ++me._ticket;
            // Строка строится 0,1–0,2 с (замер 07.11): в главном потоке кадр дёрнулся бы на каждой
            // строке Смерти. Строим фоном; полоса тем временем уже печатает.
            var job = System.Threading.Tasks.Task.Run(() => Court(line, out float _));
            me.StartCoroutine(me.Speak(job, ticket));
            return Clock(line);
        }

        private int _ticket;

        private IEnumerator Speak(System.Threading.Tasks.Task<float[]> job, int ticket)
        {
            float began = Time.unscaledTime;
            while (!job.IsCompleted) yield return null;
            if (job.IsFaulted) { Debug.LogWarning("[СУД] голос Смерти не построился: " + job.Exception?.GetBaseException().Message); yield break; }
            if (ticket != _ticket) yield break;   // суд кончился или уже звучит новая строка

            var data = job.Result;
            var clip = AudioClip.Create("Бог Смерти", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            if (_hush != null) StopCoroutine(_hush);
            _hush = null;

            // Эхо прошлой строки не рвётся щелчком: оно уходит на второй источник
            // и гаснет за полсекунды, пока новая строка уже звучит.
            if (_voice.isPlaying)
            {
                (_voice, _echo) = (_echo, _voice);
                _hush = StartCoroutine(Hush(_echo, 0.5f));
            }
            _voice.Stop();
            _voice.volume = 1f;
            _voice.clip = clip;
            _voice.Play();
            // Начало строки — 0,35 с тишины: опоздание съедает её, а не слова, и голос идёт с полосой.
            _voice.timeSamples = Mathf.Clamp((int)((Time.unscaledTime - began) * Rate), 0, (int)(0.3f * Rate));
        }

        /// <summary>Суд кончился: строка гаснет за миг, мир возвращать не нужно — его не трогали.</summary>
        public static void Quiet()
        {
            if (_instance == null) return;
            _instance._ticket++;   // недостроенная строка уже не зазвучит
            if (_instance._voice.isPlaying) _instance.StartCoroutine(_instance.Hush(_instance._voice, 0.8f));
            if (_instance._echo.isPlaying) _instance.StartCoroutine(_instance.Hush(_instance._echo, 0.8f));
        }

        private IEnumerator Hush(AudioSource source, float seconds)
        {
            float from = source.volume;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(from, 0f, t / seconds);
                yield return null;
            }
            source.Stop();
            source.volume = 1f;
        }

        /// <summary>
        /// Строка Бога Смерти на суде — отсчётами (образцы: Editor/VoiceSamples).
        ///
        /// Голос пешки (<see cref="Track"/>) — гул и вспышки шёпота: на одной фразе
        /// пробирает, на десяти строках суда — гудение (автор, 06.11: «над голосом
        /// Смерти надо ещё поработать»). Первая попытка речи — слоги на 65 Гц с резким
        /// началом и спадом — звучала ударами, «хлопушкой».
        ///
        /// Здесь — связная речь без ударов. Голос тянется через всю фразу, гласные
        /// перетекают друг в друга: форманты скользят от гласной к гласной, а не
        /// включаются слогом. Согласные — провал громкости (глухие глубже, сонорные
        /// мельче; м, н, л, в красят голос сами), сглаженный: начало не быстрее
        /// ~25 мс. Основной тон 84 Гц — октава над гулом пешки (42), к концу фразы ниже;
        /// под ним суб-октава — тот самый гул Смерти, но идущий за голосом, по фразе,
        /// а не по слогам, как и нижние гармоники: низ не качается и не бьёт. Поверх —
        /// шёпот теми же формантами, второй, бесплотный голос, — громче самого голоса
        /// (вариант «Б», автор, 07.11); эхо склепа — как у пешки.
        /// Прототип и образцы — D:\Claude\Голоса\death.py.
        /// </summary>
        public static float[] Speech(string line, out float spoken) => Court(line, out spoken);

        void Awake()
        {
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            _voice.ignoreListenerPause = true;
            _voice.priority = 0;

            _echo = gameObject.AddComponent<AudioSource>();
            _echo.playOnAwake = false;
            _echo.spatialBlend = 0f;
            _echo.ignoreListenerPause = true;
            _echo.priority = 0;

            _heart = gameObject.AddComponent<AudioSource>();
            _heart.playOnAwake = false;
            _heart.spatialBlend = 0f;
            _heart.ignoreListenerPause = true;
            _heart.clip = Heartbeat();
        }

        private void Open(Vector3 where)
        {
            _hushed.Clear();
            foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (s == null || s == _voice || s == _heart || !s.isPlaying) continue;
                _hushed.Add((s, s.volume));
            }

            _dimmed.Clear();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || l.type == LightType.Directional) continue;
                if ((l.transform.position - where).sqrMagnitude > 36f) continue;
                var flicker = l.GetComponent<FireFlicker>();
                if (flicker != null) flicker.enabled = false;
                _dimmed.Add((l, l.intensity, flicker));
            }

            Music.Silenced = true;
            StopAllCoroutines();
            StartCoroutine(Fade(0f, 1.2f));
            _beat = StartCoroutine(Beat());
        }

        // Сцену закрыли посреди голоса смерти («Начать сначала» в эпилоге): Close не придёт,
        // и музыка молчала бы всю следующую игру (автор, 26.10: «голоса… ломаются»).
        void OnDestroy() => Music.Silenced = false;

        private void Close()
        {
            if (_beat != null) StopCoroutine(_beat);
            _beat = null;
            Music.Silenced = false;
            StartCoroutine(Restore(1.5f));
        }

        private IEnumerator Fade(float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                foreach (var (src, was) in _hushed) if (src != null) src.volume = Mathf.Lerp(was, was * 0.04f, k);
                foreach (var (lamp, was, _) in _dimmed) if (lamp != null) lamp.intensity = Mathf.Lerp(was, was * 0.33f, k);
                yield return null;
            }
        }

        private IEnumerator Restore(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                foreach (var (src, was) in _hushed) if (src != null) src.volume = Mathf.Lerp(was * 0.04f, was, k);
                foreach (var (lamp, was, _) in _dimmed) if (lamp != null) lamp.intensity = Mathf.Lerp(was * 0.33f, was, k);
                yield return null;
            }
            foreach (var (src, was) in _hushed) if (src != null) src.volume = was;
            foreach (var (lamp, was, flicker) in _dimmed)
            {
                if (lamp != null) lamp.intensity = was;
                if (flicker != null) flicker.enabled = true;
            }
            _hushed.Clear();
            _dimmed.Clear();
        }

        private IEnumerator Beat()
        {
            while (true)
            {
                _heart.PlayOneShot(_heart.clip, 0.5f);
                yield return new WaitForSecondsRealtime(1.25f);
            }
        }

        // ───────────────────────────────────────── синтез

        /// <summary>Глухой двойной удар: «тук-тук», 55 и 48 Гц, быстрый спад.</summary>
        private static AudioClip Heartbeat()
        {
            int n = (int)(Rate * 0.6f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float a = t < 0.25f ? Mathf.Exp(-t * 22f) * Mathf.Sin(2f * Mathf.PI * 55f * t) : 0f;
                float t2 = t - 0.22f;
                float b = t2 > 0f ? 0.7f * Mathf.Exp(-t2 * 20f) * Mathf.Sin(2f * Mathf.PI * 48f * t2) : 0f;
                d[i] = (a + b) * 0.9f;
            }
            var clip = AudioClip.Create("Heart", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        private const string Vowels = "аеёиоуыэюяaeiouy";

        /// <summary>Строка — в одну дорожку. <paramref name="spoken"/> — секунды до хвоста.</summary>
        private static AudioClip Make(string line, out float spoken)
        {
            var wet = Track(line, out spoken);
            var clip = AudioClip.Create("Death", wet.Length, 1, Rate, false);
            clip.SetData(wet, 0);
            return clip;
        }

        /// <summary>Строка — отсчётами: гул, шёпот на гласных, эхо склепа.</summary>
        private static float[] Track(string line, out float spoken)
        {
            // Время букв — как у полосы, но в темпе Смерти.
            var hits = new List<float>();
            float at = 0.35f, step = 1f / Letters;
            foreach (char raw in line)
            {
                char c = char.ToLowerInvariant(raw);
                if (Vowels.IndexOf(c) >= 0) hits.Add(at);
                at += step;
                if (".!?…".IndexOf(c) >= 0) at += 0.5f;
                else if (",;:—".IndexOf(c) >= 0) at += 0.22f;
            }
            spoken = at;

            int n = (int)(Rate * (at + Tail));
            var dry = new float[n];
            uint seed = 2166136261u;
            foreach (char c in line) seed = (seed ^ c) * 16777619u;

            float lp = 0f, bp1 = 0f, bp2 = 0f, phase = 0f, sub = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;

                // Гул: пила около 42 Гц, дрожит медленно; суб на октаву ниже.
                float f = 42f * (1f + 0.035f * Mathf.Sin(2f * Mathf.PI * 0.31f * t));
                phase += f / Rate; if (phase > 1f) phase -= 1f;
                sub += f * 0.5f / Rate; if (sub > 1f) sub -= 1f;
                float saw = 2f * phase - 1f;
                lp += 0.035f * (saw - lp);                 // мягкий срез: гул, а не жужжание
                float body = lp * 0.9f + 0.6f * Mathf.Sin(2f * Mathf.PI * sub);
                float swell = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((at - t + 0.4f) / 0.8f);
                float breath = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.18f * t);

                // Шёпот: шум, полоса около 700 Гц (два одно-полюсных фильтра),
                // вспышки на гласных.
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed >> 9) / 4194304f) - 1f;
                bp1 += 0.18f * (noise - bp1);
                bp2 += 0.05f * (bp1 - bp2);
                float hiss = bp1 - bp2;
                float whisper = 0f;
                foreach (float h in hits)
                {
                    float d = t - h;
                    if (d < -0.02f || d > 0.5f) continue;
                    whisper += d < 0f ? 0f : Mathf.Exp(-d * 6f) * Mathf.Clamp01(d / 0.03f);
                }

                dry[i] = (body * 0.55f * breath * swell + hiss * 1.6f * Mathf.Min(1f, whisper)) * Gain;
            }

            return Reverb(dry);
        }

        // ───────────────────────────────────────── голос Бога Смерти на суде

        /// <summary>Основной тон, Гц: октава над гулом пешки.</summary>
        private const float CourtPitch = 84f;

        /// <summary>Форманты гласных (мужские, как у воинов), Гц: а, о, у, э, и, ы.</summary>
        private static readonly float[,] VowelFormants =
        {
            { 750f, 1250f, 2600f }, { 500f, 880f, 2500f }, { 330f, 760f, 2400f },
            { 540f, 1780f, 2550f }, { 290f, 2200f, 2900f }, { 340f, 1480f, 2450f },
        };

        private const string Unvoiced = "птксшщфхцчptkcsfhxq", VoicedObs = "бдгзжbdgzj", Hissing = "сшщфхцчзжsfhzxcj";
        private const string Sonors = "мнлврйmnlvwr", Stops = ".!?…", Commas = ",;:—";

        /// <summary>Сонорная красит голос сама: м и н — носом, л и в — темнее, р — посередине.</summary>
        private static Vector3 SonorFormants(char c)
        {
            switch (c)
            {
                case 'м': case 'm': return new Vector3(260f, 1000f, 2300f);
                case 'н': case 'n': return new Vector3(260f, 1350f, 2400f);
                case 'л': case 'l': return new Vector3(360f, 1000f, 2500f);
                case 'в': case 'v': case 'w': return new Vector3(300f, 900f, 2300f);
                case 'й': return new Vector3(280f, 2100f, 2800f);
                default: return new Vector3(450f, 1300f, 2500f);   // р
            }
        }

        private static int VowelIndex(char c)
        {
            switch (c)
            {
                case 'а': case 'я': case 'a': return 0;
                case 'о': case 'ё': case 'o': return 1;
                case 'у': case 'ю': case 'u': return 2;
                case 'э': case 'е': case 'e': return 3;
                case 'и': case 'i': return 4;
                case 'ы': case 'y': return 5;
                default: return -1;
            }
        }

        private struct Point
        {
            public float Time, Level, Hiss;
            public bool Coloured;
            public Vector3 Formants;
        }

        /// <summary>Сглаживание вперёд и назад (одно-полюсное): без сдвига во времени и без ступеней.</summary>
        private static void Smooth(float[] x, float tau)
        {
            if (x.Length == 0) return;
            float a = 1f - Mathf.Exp(-1f / (tau * Rate));
            float acc = x[0];
            for (int i = 0; i < x.Length; i++) { acc += a * (x[i] - acc); x[i] = acc; }
            acc = x[x.Length - 1];
            for (int i = x.Length - 1; i >= 0; i--) { acc += a * (x[i] - acc); x[i] = acc; }
        }

        private static float Resonance(float f, float centre, float width)
        {
            float x = (f - centre) / (width * 0.5f);
            return 1f / (1f + x * x);
        }

        private static float Max(float[] x)
        {
            float most = 1e-9f;
            foreach (float v in x) most = Mathf.Max(most, Mathf.Abs(v));
            return most;
        }

        /// <summary>Сколько звучит строка до хвоста — по тем же часам, что и <see cref="Court"/>.</summary>
        private static float Clock(string line)
        {
            float at = 0.35f;
            foreach (char raw in line)
            {
                char c = char.ToLowerInvariant(raw);
                at += 1f / Letters;
                if (Stops.IndexOf(c) >= 0) at += 0.5f;
                else if (Commas.IndexOf(c) >= 0) at += 0.22f;
            }
            return at;
        }

        private static float[] Court(string line, out float spoken)
        {
            // 1. Буквы — во времени: те же часы, что у печати полосы (Letters в секунду, вдох на знаке).
            var pts = new List<Point>();
            float at = 0.35f, step = 1f / Letters;
            foreach (char raw in line)
            {
                char c = char.ToLowerInvariant(raw);
                float mid = at + step * 0.5f;
                int v = VowelIndex(c);
                if (v >= 0)
                    pts.Add(new Point { Time = mid, Level = 1f, Coloured = true,
                                        Formants = new Vector3(VowelFormants[v, 0], VowelFormants[v, 1], VowelFormants[v, 2]) });
                else if (Unvoiced.IndexOf(c) >= 0)
                    pts.Add(new Point { Time = mid, Level = 0.06f, Hiss = Hissing.IndexOf(c) >= 0 ? 1f : 0f });
                else if (VoicedObs.IndexOf(c) >= 0)
                    pts.Add(new Point { Time = mid, Level = 0.25f, Hiss = Hissing.IndexOf(c) >= 0 ? 0.6f : 0f });
                else if (Sonors.IndexOf(c) >= 0)
                    pts.Add(new Point { Time = mid, Level = 0.5f, Coloured = true, Formants = SonorFormants(c) });
                else if (c == ' ')
                    pts.Add(new Point { Time = mid, Level = 0.4f });
                else if (Stops.IndexOf(c) >= 0 || Commas.IndexOf(c) >= 0 || c == '–')
                    pts.Add(new Point { Time = mid, Level = 0f });
                at += step;
                if (Stops.IndexOf(c) >= 0) at += 0.5f;
                else if (Commas.IndexOf(c) >= 0) at += 0.22f;
            }
            spoken = at;

            int n = (int)(Rate * (at + Tail));
            var amp = new float[n];
            var hiss = new float[n];
            for (int k = 0; k < pts.Count; k++)
            {
                int lo = Mathf.Max(0, (int)((pts[k].Time - 0.5f * step) * Rate));
                int hi = k + 1 < pts.Count ? (int)((pts[k + 1].Time - 0.5f * step) * Rate)
                                           : (int)((pts[k].Time + 0.5f * step) * Rate);
                hi = Mathf.Min(n, Mathf.Max(hi, lo + 1));
                for (int i = lo; i < hi; i++) { amp[i] = pts[k].Level; hiss[i] = pts[k].Hiss; }
            }
            Smooth(amp, 0.012f);
            Smooth(hiss, 0.012f);

            // 2. Форманты: опоры на гласных и сонорных, между ними — плавный переход.
            var f1 = new float[n]; var f2 = new float[n]; var f3 = new float[n];
            var marks = pts.FindAll(p => p.Coloured);
            if (marks.Count == 0) marks.Add(new Point { Time = 0f, Formants = new Vector3(750f, 1250f, 2600f) });
            for (int i = 0, m = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                while (m + 1 < marks.Count && marks[m + 1].Time <= t) m++;
                Vector3 f;
                if (t <= marks[0].Time) f = marks[0].Formants;
                else if (m + 1 >= marks.Count) f = marks[m].Formants;
                else f = Vector3.Lerp(marks[m].Formants, marks[m + 1].Formants,
                                      (t - marks[m].Time) / Mathf.Max(1e-4f, marks[m + 1].Time - marks[m].Time));
                f1[i] = f.x; f2[i] = f.y; f3[i] = f.z;
            }
            Smooth(f1, 0.025f); Smooth(f2, 0.025f); Smooth(f3, 0.025f);

            // 3. Высота: фраза (до знака) сходит от 1,06 к 0,86 основы, вопрос в конце — вверх.
            var f0 = new float[n];
            for (int i = 0; i < n; i++) f0[i] = CourtPitch * 0.86f;
            float start = 0f;
            var phrases = new List<Vector2>();
            foreach (var p in pts) if (p.Level == 0f) { phrases.Add(new Vector2(start, p.Time)); start = p.Time; }
            phrases.Add(new Vector2(start, spoken));
            foreach (var ph in phrases)
            {
                int lo = Mathf.Max(0, (int)(ph.x * Rate)), hi = Mathf.Min(n, (int)(ph.y * Rate));
                float len = Mathf.Max(0.2f, ph.y - ph.x);
                for (int i = lo; i < hi; i++) f0[i] = CourtPitch * (1.06f - 0.20f * ((float)i / Rate - ph.x) / len);
            }
            if (line.TrimEnd().EndsWith("?"))
                for (int i = Mathf.Max(0, (int)((spoken - 0.5f) * Rate)); i < Mathf.Min(n, (int)(spoken * Rate)); i++)
                    f0[i] *= 1f + 0.25f * ((float)i / Rate - (spoken - 0.5f)) / 0.5f;
            Smooth(f0, 0.08f);

            // 4. Голос: гармоники основного тона с весом от формант (сумма по Чебышёву: один синус
            // и косинус на отсчёт). Веса — раз в 32 отсчёта, между ними — по прямой.
            const float Formant = 0.88f, Tilt = 1.15f, Sub = 0.35f, Top = 3800f;
            const int Block = 32;
            float least = float.MaxValue;
            for (int i = 0; i < n; i++) least = Mathf.Min(least, f0[i]);
            int harmonics = Mathf.Clamp(Mathf.FloorToInt(Top / Mathf.Max(40f, least)), 1, 96);
            var tilt = new float[harmonics + 1];
            for (int k = 1; k <= harmonics; k++) tilt[k] = Mathf.Pow(k, -Tilt);
            // Своя огибающая у низа — только у гармоник ниже 200 Гц: при 72–111 Гц это первые две.
            int lows = Mathf.Clamp(Mathf.FloorToInt(199f / Mathf.Max(40f, least)), 0, harmonics);
            var gh = new float[harmonics + 1]; var gl = new float[harmonics + 1];
            var nh = new float[harmonics + 1]; var nl = new float[harmonics + 1];
            var dh = new float[harmonics + 1]; var dl = new float[harmonics + 1];
            void Weigh(int i, float[] high, float[] low)
            {
                i = Mathf.Min(i, n - 1);
                for (int k = 1; k <= harmonics; k++)
                {
                    float fk = k * f0[i];
                    float g = tilt[k] * (Resonance(fk, f1[i] * Formant, 120f) + 0.55f * Resonance(fk, f2[i] * Formant, 170f)
                                         + 0.22f * Resonance(fk, f3[i] * Formant, 280f) + 0.012f)
                              * Mathf.Clamp01((Top - fk) / 400f);           // верх гаснет плавно, а не обрывом
                    float w = k <= lows ? Mathf.Clamp01((200f - fk) / 80f) : 0f;   // нижние гармоники — отдельно
                    low[k] = g * w;
                    high[k] = g * (1f - w);
                }
            }
            var upper = new float[n]; var under = new float[n]; var deep = new float[n];
            double phase = 0.0;
            Weigh(0, gh, gl);
            for (int b = 0; b < n; b += Block)
            {
                Weigh(b + Block, nh, nl);
                for (int k = 1; k <= harmonics; k++) { dh[k] = (nh[k] - gh[k]) / Block; dl[k] = (nl[k] - gl[k]) / Block; }
                int end = Mathf.Min(n, b + Block);
                for (int i = b; i < end; i++)
                {
                    phase += 2.0 * System.Math.PI * f0[i] / Rate;
                    float s1 = (float)System.Math.Sin(phase), c2 = 2f * (float)System.Math.Cos(phase);
                    float prev = 0f, cur = s1, hiSum = gh[1] * s1, loSum = gl[1] * s1;
                    gh[1] += dh[1]; gl[1] += dl[1];
                    for (int k = 2; k <= harmonics; k++)
                    {
                        float next = c2 * cur - prev;
                        prev = cur; cur = next;
                        hiSum += gh[k] * next;
                        gh[k] += dh[k];
                        if (k <= lows) { loSum += gl[k] * next; gl[k] += dl[k]; }
                    }
                    upper[i] = hiSum;
                    under[i] = loSum;
                    // суб-октава: гул Смерти, идущий за голосом
                    deep[i] = (float)(System.Math.Sin(phase * 0.5) + 0.25 * System.Math.Sin(phase + 0.3));
                }
                (gh, nh) = (nh, gh);
                (gl, nl) = (nl, gl);
            }
            float most = 1e-9f;
            for (int i = 0; i < n; i++) most = Mathf.Max(most, Mathf.Abs(upper[i] + under[i]));
            var phraseAmp = (float[])amp.Clone(); Smooth(phraseAmp, 0.05f);
            var slow = (float[])amp.Clone(); Smooth(slow, 0.15f);
            float slowTop = Max(slow);

            // 5. Шёпот: шум через три резонатора, идущих за формантами; шип согласных — тихо и без верха.
            uint seed = 2166136261u;
            foreach (char c in line) seed = (seed ^ c) * 16777619u;
            var white = new float[n];
            for (int i = 0; i < n; i++) { seed = seed * 1664525u + 1013904223u; white[i] = ((seed >> 9) / 4194304f) - 1f; }
            var air = new float[n];
            float[] widths = { 220f, 260f, 360f }, levels = { 1f, 0.6f, 0.3f };
            for (int j = 0; j < 3; j++)
            {
                float[] fr = j == 0 ? f1 : j == 1 ? f2 : f3;
                float r = Mathf.Exp(-Mathf.PI * widths[j] / Rate);
                float y1 = 0f, y2 = 0f, c1 = 0f, cc = -r * r, gain = (1f - r) * levels[j];
                for (int i = 0; i < n; i++)
                {
                    if (i % Block == 0) c1 = 2f * r * Mathf.Cos(2f * Mathf.PI * fr[i] * 0.95f / Rate);
                    float y = gain * white[i] + c1 * y1 + cc * y2;
                    y2 = y1; y1 = y;
                    air[i] += y;
                }
            }
            float airTop = Max(air);
            var hush = new float[n];
            for (int i = 0; i < n; i++) hush[i] = white[i] - (i > 0 ? white[i - 1] : 0f);
            Smooth(hush, 0.00012f);
            float hushTop = Max(hush);

            // 6. Вместе; верх мягче (за стеной склепа), эхо склепа.
            // Вариант «Б»: шёпот громче, голос тише — больше призрака (автор, 07.11: «Поставь Б»).
            const float Whisper = 1.0f, Body = 0.6f;
            var dry = new float[n];
            float lp = 0f, alpha = 1f - Mathf.Exp(-2f * Mathf.PI * 3000f / Rate);
            for (int i = 0; i < n; i++)
            {
                float voice = Body * (upper[i] / most * amp[i] + under[i] / most * phraseAmp[i] + Sub * deep[i] * slow[i] / slowTop);
                float ghost = Whisper * air[i] / airTop * amp[i] + 0.10f * hush[i] / hushTop * hiss[i];
                lp += alpha * (voice + ghost - lp);
                dry[i] = lp;
            }
            float peak = Max(dry);
            for (int i = 0; i < n; i++) dry[i] *= 0.5f / peak;
            var wet = Reverb(dry);
            float wetTop = Max(wet);
            for (int i = 0; i < n; i++) wet[i] *= 0.64f / wetTop;
            return wet;
        }

        /// <summary>Эхо склепа: три гребёнки с затуханием и рассеиватель.</summary>
        private static float[] Reverb(float[] x)
        {
            int[] delays = { (int)(Rate * 0.113f), (int)(Rate * 0.171f), (int)(Rate * 0.289f) };
            const float fb = 0.62f;
            var y = new float[x.Length];
            foreach (int dl in delays)
            {
                var buf = new float[x.Length];
                for (int i = 0; i < x.Length; i++)
                {
                    float back = i >= dl ? buf[i - dl] : 0f;
                    buf[i] = x[i] + back * fb;
                    y[i] += buf[i] / delays.Length;
                }
            }
            int ap = (int)(Rate * 0.047f);
            var z = new float[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                float prev = i >= ap ? z[i - ap] : 0f;
                float inPrev = i >= ap ? y[i - ap] : 0f;
                z[i] = -0.6f * y[i] + inPrev + 0.6f * prev;
            }
            for (int i = 0; i < x.Length; i++) z[i] = Mathf.Clamp(x[i] * 0.55f + z[i] * 0.7f, -1f, 1f);
            return z;
        }
    }
}
