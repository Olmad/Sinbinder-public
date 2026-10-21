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

        void Awake()
        {
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            _voice.ignoreListenerPause = true;
            _voice.priority = 0;

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

            var wet = Reverb(dry);
            var clip = AudioClip.Create("Death", n, 1, Rate, false);
            clip.SetData(wet, 0);
            return clip;
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
