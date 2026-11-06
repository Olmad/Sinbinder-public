// Assets/Scripts/Audio/VoiceGenerator.cs
// Перевод: текст через Loc
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Audio
{
    /// <summary>
    /// Голос воина — бормотание слогами (автор, 06.11: «нужно переработать голоса
    /// вообще — они раздражают»).
    ///
    /// До 06.11 на каждую букву пищал меандр 440 Гц, 60 мс, без мягкого начала,
    /// 26 раз в секунду: резкий верх до 11 кГц и щелчок на каждом бипе. Теперь на
    /// каждую гласную строки — слог: звонкий тон (гармоники с завалом) через три
    /// форманты той самой гласной (а, о, у, э, и, ы), мягкое начало и спад; перед
    /// слогом — толчок или шорох согласной. Фраза к концу ниже, вопрос — вверх,
    /// ударный слог (середина слова) — выше. Низко: мужчина около 118 Гц, женщина
    /// около 205. Без жребия: одна строка — один звук при каждом прочтении.
    /// Прототип и образцы «было/стало» — D:\Claude\Голоса (murmur.py).
    ///
    /// Тембр — по оболочке, а поверх по греху (00-GDD §9): живой — ровный, скелет —
    /// пустой (чётные гармоники тише), зомби — глухой, голем — низкий гул, призрак —
    /// с придыханием, гневный — яркий и резкий.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VoiceGenerator : MonoBehaviour
    {
        [Header("Базовые настройки")]
        [SerializeField] private VoiceType _defaultVoice = VoiceType.Square;
        /// <summary>Высота: 440 — обычная. Голос строится от 118 Гц, поле — множитель (поле / 440).</summary>
        [SerializeField] private float _basePitch = 440f;
        /// <summary>Дрожание высоты по буквам: 0,3 — обычное (±2 %).</summary>
        [SerializeField] private float _pitchVariation = 0.3f;
        /// <summary>Длина: 0,06 — обычная; поле / 0,06 — множитель длины слога.</summary>
        [SerializeField] private float _duration = 0.06f;
        /// <summary>Частота записи; слог строится на половине (голосу выше 4 кГц нечего делать).</summary>
        [SerializeField] private int _sampleRate = 44100;

        [Header("Модификаторы эмоций")]
        [SerializeField] private bool _useEmotions = true;
        [SerializeField] private float _angerPitchShift = -0.3f;
        [SerializeField] private float _joyPitchShift = 0.4f;
        [SerializeField] private float _sadnessPitchShift = -0.1f;
        [SerializeField] private float _fearPitchShift = 0.5f;
        [SerializeField] private float _hopePitchShift = 0.2f;

        private AudioSource _audioSource;
        private Warrior _warrior;

        /// <summary>
        /// Глубина голоса: ниже тон, чуть ниже форманты (тело больше), медленнее.
        /// Судьи суда — 0,62–1,18 (SoulCourt.Throat). До 06.11 суд опускал высоту
        /// самим источником (AudioSource.pitch) — так тянулся и сам звук.
        /// </summary>
        public float Depth { get; set; } = 1f;

        /// <summary>Тембр голоса без души — у судей суда свой у каждого (SoulCourt.Cast).</summary>
        public VoiceType Kind { get => _defaultVoice; set => _defaultVoice = value; }

        /// <summary>Темп: больше — медленнее (Уныние тянет, Страх частит).</summary>
        public float Tempo { get; set; } = 1f;

        /// <summary>Основной тон мужского голоса, Гц.</summary>
        private const float Male = 118f;

        /// <summary>Женский — выше: поверх ×1,2 в <see cref="Personal"/> — 118 × 1,2 × 1,45 ≈ 205 Гц.</summary>
        private const float Female = 1.45f;

        public enum VoiceType
        {
            Sine,     // С придыханием — для духов
            Square,   // Ровный (у скелета — пустой)
            Triangle, // Глухой — зомби; у голема — низкий гул
            Sawtooth  // Яркий и резкий — для гневных
        }

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0.7f;
            _warrior = GetComponent<Warrior>();
        }

        // ──────────────────────────────────
        // Что говорит
        // ──────────────────────────────────

        /// <summary>
        /// Голос под печатаемую строку (окно разговора) — по букве: гласная — слог
        /// (не чаще 10 в секунду), согласная — молча, но готовит начало следующего
        /// слога. От буквы — дрожание высоты, и реплика звучит одинаково при каждом
        /// прочтении. Без буквы — недовольное «хм» отказа.
        /// </summary>
        public void Speak(char letter = '\0')
        {
            if (_audioSource == null || Dialogue.CampLines.Mute(_warrior)) return;
            var mood = Mood();

            if (letter == '\0')
            {
                float hm = Base(mood) * 0.95f;
                Play(mood, 2, Hiss, hm, hm * 0.86f, 0.17f * mood.Pace);
                return;
            }

            char c = char.ToLowerInvariant(letter);
            int vowel = Vowel(c);
            if (vowel < 0)
            {
                if (".!?…".IndexOf(c) >= 0) _since = 0;
                int onset = Onset(c);
                if (onset != None && (onset != Soft || _onset == None)) _onset = onset;
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastSyllable < 0.1f) { _onset = None; return; }
            _lastSyllable = now;

            float p = Base(mood) * (1.06f - 0.025f * Mathf.Min(_since, 6)) * (1f + 0.02f * mood.Jitter * Wobble(c));
            _since++;
            Play(mood, vowel, _onset, p, p * 0.96f, 0.10f * mood.Pace);
            _onset = None;
        }

        private int _onset = None;
        private int _since;
        private float _lastSyllable = -1f;

        /// <summary>Громкость текущего бормотания: болтовня лагеря тише сцены.</summary>
        private float _loud = 1f;

        /// <summary>Больше слогов на одну реплику не надо: дальше — уже шум, а не речь.</summary>
        private const int MostSyllables = 40;

        private Coroutine _babble;

        /// <summary>
        /// Проговорить строку, которая показана целиком: на нижней полосе
        /// наезда (<see cref="Gameplay.Herald"/>) и в облачке над головой
        /// (<see cref="UI.SpeechBubbles"/>). Слог на гласную, пауза между
        /// словами, длиннее — на запятой и конце фразы; не дольше
        /// <paramref name="seconds"/>. Реальное время: на наезде мир стоит.
        ///
        /// Новая строка того же голоса обрывает прежнюю: говорить разом
        /// могут двое, но не один дважды.
        /// </summary>
        public void Babble(string words, float seconds, float loud = 1f)
        {
            if (string.IsNullOrEmpty(words) || seconds <= 0f || !isActiveAndEnabled
                || Dialogue.CampLines.Mute(_warrior)) return;
            _loud = Mathf.Clamp01(loud);
            if (_babble != null) StopCoroutine(_babble);
            _babble = StartCoroutine(BabbleRoutine(words, seconds));
        }

        private IEnumerator BabbleRoutine(string words, float seconds)
        {
            var mood = Mood();
            // Щелчок по полосе — строка уже целиком (или следующая): голос молкнет.
            int click = UI.Letterbox.Clicks;
            bool framed = UI.Letterbox.Instance != null && UI.Letterbox.Instance.Shown;

            foreach (var step in Plan(words, mood, seconds))
            {
                if (framed && UI.Letterbox.Clicks != click) break;
                Play(mood, step.Vowel, step.Onset, step.From, step.To, step.Length);
                yield return new WaitForSecondsRealtime(step.Wait);
            }

            _babble = null;
            _loud = 1f;   // разговор вблизи (DialogueUI, Speak) — в полную силу
        }

        private struct Step
        {
            public int Vowel, Onset;
            public float From, To, Length, Wait;
        }

        /// <summary>
        /// Как строка будет сказана: слог за слогом — гласная, начало, высота от и до,
        /// длина и пауза после. Фраза — до конца предложения: к концу ниже, вопрос —
        /// вверх, ударный слог выше, перед знаком слог длиннее.
        ///
        /// Строка длиннее <paramref name="seconds"/> — голос частит (до 0,7 обычной
        /// длины слога), чтобы фраза дошла до конца: образцы 06.11 обрывались на «Я —
        /// Гордыня…». Не влезла и так — последний слог сходит вниз, как конец фразы.
        /// </summary>
        private List<Step> Plan(string words, Tone mood, float seconds)
        {
            var all = new List<Step>();
            float tone = Base(mood);
            var line = Parse(words);
            int a = 0;
            while (a < line.Count && all.Count < MostSyllables)
            {
                int b = a;
                while (b < line.Count - 1 && line[b].Pause < Full) b++;
                int kind = line[b].Pause;

                for (int i = a; i <= b && all.Count < MostSyllables; i++)
                {
                    var s = line[i];
                    float k = (i - a) / (float)Mathf.Max(1, b - a);
                    float p = tone * (1.08f - 0.18f * k);
                    if (kind == Exclaim) p *= 1.08f;
                    if (s.Stress) p *= kind == Exclaim ? 1.18f : 1.10f;
                    p *= 1f + 0.02f * mood.Jitter * Wobble(s.Letter);
                    float end = kind == Question && i >= b - 1 ? p * 1.22f : p * 0.96f;
                    float dur = 0.112f * mood.Pace * (s.Stress ? 1.25f : 1f) * (s.Pause >= Comma ? 1.4f : 1f);
                    all.Add(new Step { Vowel = s.Vowel, Onset = s.Onset, From = p, To = end, Length = dur, Wait = dur + Gap(s.Pause) });
                }
                a = b + 1;
            }

            float natural = 0f;
            foreach (var s in all) natural += s.Wait;
            float squeeze = natural > seconds && natural > 0f ? Mathf.Max(0.7f, seconds / natural) : 1f;

            var steps = new List<Step>(all.Count);
            float spent = 0f;
            foreach (var s in all)
            {
                if (spent >= seconds) break;
                var t = s;
                t.Length = Mathf.Max(0.06f, s.Length * squeeze);
                t.Wait = s.Wait * squeeze;
                steps.Add(t);
                spent += t.Wait;
            }
            if (steps.Count > 0 && steps.Count < all.Count)
            {
                var last = steps[steps.Count - 1];
                last.To = last.From * 0.9f;
                steps[steps.Count - 1] = last;
            }
            return steps;
        }

        /// <summary>
        /// Строка целиком, как её скажет этот голос, — отсчёты и частота (образцы
        /// в редакторе: Editor/VoiceSamples). Те же слоги, что звучат в игре.
        /// </summary>
        public float[] Render(string words, float seconds, out int rate)
        {
            var mood = Mood();
            rate = Rate;
            var steps = Plan(words, mood, seconds);
            float total = 0.4f;
            foreach (var s in steps) total += s.Wait;
            var mix = new float[Mathf.CeilToInt(total * rate) + rate];
            bool odd = Odd(mood.Type);
            int fk = Mathf.RoundToInt(Formant() * 50f);
            float at = 0f;
            foreach (var s in steps)
            {
                int q0 = Quarter(s.From), q1 = Quarter(s.To);
                int cs = Mathf.Clamp(Mathf.RoundToInt(s.Length * 100f), 4, 60);
                var one = Samples(Paint(mood.Type, odd), s.Vowel, s.Onset, Hz(q0), Hz(q1), cs / 100f, fk / 50f,
                                  q0 * 31 + s.Vowel * 7 + s.Onset * 3 + cs);
                int start = Mathf.RoundToInt(at * rate);
                for (int i = 0; i < one.Length && start + i < mix.Length; i++) mix[start + i] += one[i] * 0.7f;   // громкость источника
                at += s.Wait;
            }
            return mix;
        }

        // ──────────────────────────────────
        // Строка → слоги
        // ──────────────────────────────────

        private const int None = 0, Burst = 1, Hiss = 2, Soft = 3;
        private const int Inside = 0, WordEnd = 1, Comma = 2, Full = 3, Question = 4, Exclaim = 5;

        private struct Syllable
        {
            public int Vowel, Onset, Pause;
            public bool Stress;
            public char Letter;
        }

        /// <summary>Пауза после слога: внутри слова — щель, между словами — дольше, у знака — вдох.</summary>
        private static float Gap(int pause)
        {
            switch (pause)
            {
                case Inside:  return 0.03f;
                case WordEnd: return 0.075f;
                case Comma:   return 0.18f;
                default:      return 0.32f;
            }
        }

        /// <summary>
        /// Слоги строки: по гласной, с началом от согласной перед ней и паузой после.
        /// Ударная — середина слова: без словаря, но одинаково всякий раз.
        /// </summary>
        private static List<Syllable> Parse(string text)
        {
            var all = new List<Syllable>();
            int word = 0, onset = None;

            void Close()
            {
                int count = all.Count - word;
                if (count <= 0) return;
                var last = all[all.Count - 1];
                last.Pause = Mathf.Max(last.Pause, WordEnd);
                all[all.Count - 1] = last;
                var mid = all[word + count / 2];
                mid.Stress = true;
                all[word + count / 2] = mid;
                word = all.Count;
            }

            foreach (char raw in text)
            {
                char ch = char.ToLowerInvariant(raw);
                int vowel = Vowel(ch);
                if (vowel >= 0)
                {
                    all.Add(new Syllable { Vowel = vowel, Onset = onset, Pause = Inside, Letter = ch });
                    onset = None;
                    continue;
                }
                int o = Onset(ch);
                if (o != None) { if (o != Soft || onset == None) onset = o; continue; }
                if (char.IsLetterOrDigit(ch)) continue;   // ь, ъ, цифры — слово продолжается

                Close();
                onset = None;
                int mark = Mark(ch);
                if (mark > Inside && all.Count > 0)
                {
                    var last = all[all.Count - 1];
                    last.Pause = Mathf.Max(last.Pause, mark);
                    all[all.Count - 1] = last;
                }
            }
            Close();
            if (all.Count > 0 && all[all.Count - 1].Pause < Full)
            {
                var last = all[all.Count - 1];
                last.Pause = Full;
                all[all.Count - 1] = last;
            }
            return all;
        }

        private static int Mark(char c)
        {
            if (c == '?') return Question;
            if (c == '!') return Exclaim;
            if (c == '.' || c == '…') return Full;
            if (",;:—–".IndexOf(c) >= 0) return Comma;
            return Inside;
        }

        /// <summary>Гласная: 0 а, 1 о, 2 у, 3 э, 4 и, 5 ы; не гласная — −1.</summary>
        private static int Vowel(char c)
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

        private static int Onset(char c)
        {
            if ("пбтдкгцpbtdkgcq".IndexOf(c) >= 0) return Burst;
            if ("сшщфхзжчsfhzxj".IndexOf(c) >= 0) return Hiss;
            if ("млнрвйmlnrvw".IndexOf(c) >= 0) return Soft;
            return None;
        }

        // ──────────────────────────────────
        // Чей голос
        // ──────────────────────────────────

        private struct Tone
        {
            public VoiceType Type;
            public float Pitch, Jitter, Pace;
        }

        /// <summary>Голос на эту реплику: тембр, высота и темп — от души, тела и чувства.</summary>
        private Tone Mood()
        {
            var tone = new Tone
            {
                Type = Timbre(),
                Pitch = 1f,
                Jitter = _pitchVariation / 0.3f,
                Pace = Pace() * (_duration / 0.06f) * Mathf.Lerp(1f, 1f / Mathf.Max(0.3f, Depth), 0.4f) * Tempo,
            };
            if (!_useEmotions || _warrior == null || EmotionSystem.Instance == null) return tone;

            switch (EmotionSystem.Instance.GetDominantEmotion(_warrior))
            {
                case EmotionType.Anger:
                    // Единственная эмоция, которой позволено перебить тембр оболочки:
                    // «пила для гневных» записана в GDD прямо, наравне с оболочками.
                    tone.Type = VoiceType.Sawtooth;
                    tone.Pitch = 1f + _angerPitchShift * 0.4f;
                    tone.Jitter *= 0.5f;
                    tone.Pace *= 0.85f;
                    break;
                case EmotionType.Joy:
                    tone.Pitch = 1f + _joyPitchShift * 0.3f;
                    tone.Jitter *= 1.5f;
                    tone.Pace *= 0.92f;
                    break;
                case EmotionType.Sadness:
                    tone.Pitch = 1f + _sadnessPitchShift * 0.6f;
                    tone.Jitter *= 0.5f;
                    tone.Pace *= 1.2f;
                    break;
                case EmotionType.Fear:
                    tone.Pitch = 1f + _fearPitchShift * 0.3f;
                    tone.Jitter *= 2f;
                    tone.Pace *= 0.8f;
                    break;
                case EmotionType.Hope:
                    tone.Pitch = 1f + _hopePitchShift * 0.3f;
                    tone.Pace *= 1.05f;
                    break;
            }
            return tone;
        }

        /// <summary>Основной тон, Гц: мужчина 118, женщина ×1,74; тело, душа, варево Хальда, глубина.</summary>
        private float Base(Tone mood)
            => Male * (_basePitch / 440f) * Mathf.Pow(Shelled(), 0.6f) * Personal() * (Woman() ? Female : 1f)
             * Brew.Pitch * Depth * mood.Pitch;   // варево Хальда: голоса выше

        /// <summary>Форманты: у женщин выше, у призрака тоньше, у голема и зомби ниже, у глубокого — ниже.</summary>
        private float Formant()
        {
            float f = Woman() ? 1.15f : 1f;
            if (_warrior != null)
                switch (_warrior.Shell)
                {
                    case ShellType.Ghost:  f *= 1.08f; break;
                    case ShellType.Golem:  f *= 0.90f; break;
                    case ShellType.Zombie: f *= 0.94f; break;
                }
            return f * Mathf.Lerp(1f, Depth, 0.35f);
        }

        /// <summary>
        /// Тембр, каким он задуман: <b>по оболочке, а поверх — по греху</b>
        /// (<c>00-GDD.md</c> §9). Призрак — с придыханием, скелет — пустой,
        /// зомби и голем — глухие, гневный — резкий.
        ///
        /// До 18 сентября тембр брался только у эмоции, а спокойный воин
        /// получал <c>_defaultVoice</c> — один голос у всех подряд,
        /// от призрака до голема. Оболочки в голосе не было слышно вовсе.
        /// </summary>
        private VoiceType Timbre()
        {
            if (_warrior == null || _warrior.Soul == null) return _defaultVoice;

            // Грех громче тела: гневный рычит в любой оболочке.
            if (_warrior.Soul.Sin == SinType.Wrath) return VoiceType.Sawtooth;

            switch (_warrior.Shell)
            {
                case ShellType.Ghost:    return VoiceType.Sine;
                case ShellType.Skeleton: return VoiceType.Square;
                case ShellType.Zombie:   return VoiceType.Triangle;
                case ShellType.Golem:    return VoiceType.Triangle;
                default:                 return VoiceType.Square;
            }
        }

        /// <summary>Скелет среди ровных и голем среди глухих звучат по-своему.</summary>
        private bool Odd(VoiceType type)
            => _warrior != null && (type == VoiceType.Square ? _warrior.Shell == ShellType.Skeleton
                                  : type == VoiceType.Triangle && _warrior.Shell == ShellType.Golem);

        private struct Colour
        {
            public float Tilt, Odd, Breath, Bright, Loud;
        }

        /// <summary>
        /// Краска тембра: завал гармоник (больше — мягче), чётные против нечётных
        /// (пустой звук скелета), придыхание, яркость верхних формант, громкость.
        /// </summary>
        private static Colour Paint(VoiceType type, bool odd)
        {
            switch (type)
            {
                case VoiceType.Sawtooth: return new Colour { Tilt = 0.75f, Odd = 1f, Breath = 0.03f, Bright = 1.4f, Loud = 0.55f };
                case VoiceType.Sine:     return new Colour { Tilt = 1.6f, Odd = 1f, Breath = 0.07f, Bright = 1f, Loud = 0.45f };
                case VoiceType.Triangle:
                    return odd ? new Colour { Tilt = 0.85f, Odd = 1f, Breath = 0.02f, Bright = 0.9f, Loud = 0.52f }    // голем
                               : new Colour { Tilt = 1.3f, Odd = 1f, Breath = 0.03f, Bright = 0.8f, Loud = 0.48f };   // зомби
                default:
                    return odd ? new Colour { Tilt = 1.0f, Odd = 1.35f, Breath = 0.03f, Bright = 1f, Loud = 0.5f }     // скелет
                               : new Colour { Tilt = 1.1f, Odd = 1f, Breath = 0.02f, Bright = 1f, Loud = 0.5f };      // живой
            }
        }

        /// <summary>
        /// Свой голос у каждого (автор, 28.10: «при разговоре на второй фразе одинаковые голоса»): высота —
        /// от души по формуле, ±15 %, у женщин выше на пятую часть. Без жребия: тот же человек звучит одинаково.
        /// </summary>
        private float Personal()
        {
            if (_warrior == null || _warrior.Soul == null) return 1f;
            float k = 0.85f + (Wardrobe.Stamp(_warrior.Soul.Name) % 31) / 100f;
            if (Elder()) k *= 0.88f;
            return Woman() ? k * 1.2f : k;
        }

        private bool Woman() => _warrior != null && Grammar.Pick(_warrior.Gender, "m", "f") == "f";

        /// <summary>
        /// Старый — ниже и медленнее (КТ1 п. 11: «голоса различаются по телу и возрасту»). Возраста в душе нет;
        /// старость названа в имени — «Карган Старый Ворон». Без этого Карган и Вейн, два скелета, звучали одним
        /// голосом: высота 1,08 и 1,04, темп 1,19 и 1,18 (расчёт 31.10).
        /// </summary>
        private bool Elder() => _warrior != null && _warrior.Soul != null && _warrior.Soul.Name != null
                                && _warrior.Soul.Name.Contains("Стар");

        /// <summary>Свой темп: одни тянут слоги, другие частят (±20 %).</summary>
        private float Pace()
        {
            if (_warrior == null || _warrior.Soul == null) return 1f;
            float pace = 0.8f + (Wardrobe.Stamp(_warrior.Soul.Name) / 31 % 41) / 100f;
            return Elder() ? pace * 0.85f : pace;
        }

        /// <summary>Насколько тело выше или ниже основы: камень гудит низко, бесплотный звенит выше (в тоне — в степени 0,6).</summary>
        private float Shelled()
        {
            if (_warrior == null) return 1f;

            switch (_warrior.Shell)
            {
                case ShellType.Golem:  return 0.55f;
                case ShellType.Zombie: return 0.8f;
                case ShellType.Ghost:  return 1.35f;
                default:               return 1f;
            }
        }

        /// <summary>
        /// Разброс высоты от самой буквы, а не от жребия: одинаковый вход —
        /// одинаковый выход. У реплики появляется своя мелодия, одна и та же
        /// при каждом прочтении.
        /// </summary>
        private static float Wobble(char letter)
        {
            if (letter == '\0') return 0f;

            return ((letter * 37) % 23) / 11f - 1f;
        }

        // ──────────────────────────────────
        // Как звучит слог
        // ──────────────────────────────────

        /// <summary>Форманты гласных (мужские), Гц: а, о, у, э, и, ы.</summary>
        private static readonly float[,] Formants =
        {
            { 750f, 1250f, 2600f },
            { 500f,  880f, 2500f },
            { 330f,  760f, 2400f },
            { 540f, 1780f, 2550f },
            { 290f, 2200f, 2900f },
            { 340f, 1480f, 2450f },
        };

        /// <summary>
        /// Готовые слоги: тембр, гласная, начало, высота в четвертях тона (от и до),
        /// длина в сотых, форманты. Слог звучит на каждой гласной — без кэша игра
        /// рожала бы клип за клипом посреди реплики. Больше 900 — кэш сбрасывается
        /// (около 12 МБ): голосов в игре десятки, а не сотни.
        /// </summary>
        private static readonly Dictionary<(int, int, int, int, int, int, int), AudioClip> _clips = new();

        private const int MostClips = 900;

        private void Play(Tone mood, int vowel, int onset, float from, float to, float dur)
        {
            if (_audioSource == null) return;
            bool odd = Odd(mood.Type);
            int q0 = Quarter(from), q1 = Quarter(to);
            int cs = Mathf.Clamp(Mathf.RoundToInt(dur * 100f), 4, 60);
            int fk = Mathf.RoundToInt(Formant() * 50f);
            var key = ((int)mood.Type * 2 + (odd ? 1 : 0), vowel, onset, q0, q1, cs, fk);

            if (!_clips.TryGetValue(key, out var clip) || clip == null)
            {
                if (_clips.Count >= MostClips)
                {
                    foreach (var old in _clips.Values) if (old != null) Destroy(old);
                    _clips.Clear();
                }
                clip = Make(Paint(mood.Type, odd), vowel, onset, Hz(q0), Hz(q1), cs / 100f, fk / 50f,
                            q0 * 31 + vowel * 7 + onset * 3 + cs);
                _clips[key] = clip;
            }
            _audioSource.PlayOneShot(clip, _loud);
        }

        private static int Quarter(float hz) => Mathf.RoundToInt(24f * Mathf.Log(Mathf.Max(20f, hz) / 55f, 2f));
        private static float Hz(int quarter) => 55f * Mathf.Pow(2f, quarter / 24f);

        private static float Resonance(float f, float centre, float width)
        {
            float x = (f - centre) / (width * 0.5f);
            return 1f / (1f + x * x);
        }

        private static float Rise(float x) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(x));

        /// <summary>
        /// Слог: толчок или шорох согласной, затем гласная — гармоники основного тона
        /// (сумма по Чебышёву: один синус и косинус на отсчёт), каждая со своим весом
        /// от формант; лёгкое живое дрожание 5,5 Гц; мягкое начало, спад и хвост;
        /// верх приглушён около 2,8 кГц.
        /// </summary>
        private int Rate => Mathf.Clamp(_sampleRate / 2, 11025, 24000);

        private AudioClip Make(Colour colour, int vowel, int onset, float f0a, float f0b, float dur, float formant, int seed)
        {
            var data = Samples(colour, vowel, onset, f0a, f0b, dur, formant, seed);
            var clip = AudioClip.Create("Слог", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private float[] Samples(Colour colour, int vowel, int onset, float f0a, float f0b, float dur, float formant, int seed)
        {
            int rate = Rate;
            int n = Mathf.Max(1, Mathf.RoundToInt(dur * rate));
            int pre = onset == Burst ? Mathf.RoundToInt(0.012f * rate) : onset == Hiss ? Mathf.RoundToInt(0.032f * rate) : 0;
            var data = new float[pre + n];
            var rng = new System.Random(seed);

            float f1 = Formants[vowel, 0] * formant, f2 = Formants[vowel, 1] * formant, f3 = Formants[vowel, 2] * formant;
            float mean = 0.5f * (f0a + f0b);
            int harmonics = Mathf.Clamp(Mathf.FloorToInt(3800f / Mathf.Max(f0a, f0b)), 1, 64);
            var gain = new double[harmonics + 1];
            for (int k = 1; k <= harmonics; k++)
            {
                float f = k * mean;
                float g = (1f / Mathf.Pow(k, colour.Tilt))
                        * (Resonance(f, f1, 110f) + 0.55f * colour.Bright * Resonance(f, f2, 150f)
                           + 0.22f * colour.Bright * Resonance(f, f3, 260f) + 0.015f);
                g *= k % 2 == 0 ? 2f - colour.Odd : colour.Odd;
                gain[k] = g;
            }

            // Гласная.
            double phase = 0.0;
            float shake = seed % 7;
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = Mathf.Lerp(f0a, f0b, t / dur) * (1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5.5f * t + shake));
                phase += 2.0 * System.Math.PI * f / rate;
                double s1 = System.Math.Sin(phase), c2 = 2.0 * System.Math.Cos(phase);
                double prev = 0.0, cur = s1, sum = gain[1] * s1;
                for (int k = 2; k <= harmonics; k++)
                {
                    double next = c2 * cur - prev;
                    prev = cur;
                    cur = next;
                    sum += gain[k] * next;
                }
                data[pre + i] = (float)sum;
                peak = Mathf.Max(peak, Mathf.Abs((float)sum));
            }

            // Придыхание. Шум сглажен дважды: с одним сглаживанием он шуршал поверх
            // гласной (автор, 06.11: «временами слышно шорканье»).
            if (colour.Breath > 0f && peak > 0f)
            {
                float smooth = 0f, softer = 0f;
                for (int i = 0; i < n; i++)
                {
                    float w = (float)(rng.NextDouble() * 2.0 - 1.0) * 1.7f;
                    smooth += (w - smooth) * 0.3f;
                    softer += (smooth - softer) * 0.3f;
                    data[pre + i] += colour.Breath * peak * softer * 1.6f;
                }
            }

            // Огибающая: мягкое начало (у м, н, л, р — мягче), спад к концу, хвост.
            float attack = onset == Soft ? 0.035f : 0.018f;
            float most = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Rise(t / attack) * Rise((dur - t) / 0.04f) * Mathf.Exp(-t / (dur * 0.9f));
                data[pre + i] *= env;
                most = Mathf.Max(most, Mathf.Abs(data[pre + i]));
            }
            float scale = most > 0f ? colour.Loud / most : 0f;
            for (int i = 0; i < n; i++) data[pre + i] *= scale;

            // Согласная перед гласной — намёком, а не шумом. Автор, 06.11: «временами
            // слышно шорканье»: шип 45 мс почти в треть гласной и щелчок с полного
            // размаха шуршали на каждом «с», «ш», «х», «т». Теперь шип короче и в
            // несколько раз тише, нарастает и гаснет плавно (синус в квадрате);
            // взрыв — тихий толчок с нарастанием в полторы миллисекунды.
            float last = 0f, soft = 0f;
            for (int i = 0; i < pre; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0) * 1.7f;
                float edge = w - last;   // шум без низа: шорох, а не гул
                last = w;
                soft += (edge - soft) * 0.5f;   // и без самого верха: не скрежет
                float bell = Mathf.Sin(Mathf.PI * i / pre);
                data[i] = onset == Burst
                    ? soft * Mathf.Clamp01(i / (0.0015f * rate)) * Mathf.Exp(-i / (0.003f * rate)) * colour.Loud * 0.08f
                    : soft * bell * bell * colour.Loud * 0.04f;
            }

            // Верх приглушить: голос — бормотание за стеной, а не писк.
            float alpha = 1f - Mathf.Exp(-2f * Mathf.PI * 2800f / rate);
            float acc = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                acc += alpha * (data[i] - acc);
                data[i] = acc;
            }
            return data;
        }
    }
}
