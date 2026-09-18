// Assets/Scripts/Audio/VoiceGenerator.cs
using UnityEngine;
using System.Collections.Generic;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class VoiceGenerator : MonoBehaviour
    {
        [Header("Базовые настройки")]
        [SerializeField] private VoiceType _defaultVoice = VoiceType.Square;
        [SerializeField] private float _basePitch = 440f;
        [SerializeField] private float _pitchVariation = 0.3f;
        [SerializeField] private float _duration = 0.06f;
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

        /// <summary>Готовые клипы: тембр, высота в герцах, длина в мсек.</summary>
        private static readonly Dictionary<(VoiceType, int, int), AudioClip> _clips = new();

        public enum VoiceType
        {
            Sine,     // Мягкий, для духов
            Square,   // Резкий, для скелетов (как у Санса)
            Triangle, // Глухой, для зомби
            Sawtooth  // Агрессивный, для гневных
        }

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0.7f;
            _warrior = GetComponent<Warrior>();
        }

        /// <summary>
        /// Бип. <paramref name="letter"/> — буква, которую печатают:
        /// от неё берётся дрожание высоты, и потому одна и та же реплика
        /// звучит одинаково при каждом прочтении. Без буквы — ровный тон,
        /// таким говорит отказ.
        /// </summary>
        public void Speak(char letter = '\0')
        {
            if (_audioSource == null) return;

            EmotionType emotion = EmotionType.Calm;
            VoiceType voiceType = Timbre();
            float pitch = _basePitch * Height();
            float variation = _pitchVariation;
            float duration = _duration;

            if (_useEmotions && _warrior != null && EmotionSystem.Instance != null)
            {
                emotion = EmotionSystem.Instance.GetDominantEmotion(_warrior);
            }

            switch (emotion)
            {
                case EmotionType.Anger:
                    // Единственная эмоция, которой позволено перебить
                    // тембр оболочки: «пила для гневных» записана в GDD
                    // прямо, наравне с оболочками.
                    voiceType = VoiceType.Sawtooth;
                    pitch = _basePitch * (1 + _angerPitchShift);
                    variation *= 0.5f;
                    duration *= 0.8f;
                    break;
                case EmotionType.Joy:
                    pitch = _basePitch * (1 + _joyPitchShift);
                    variation *= 1.5f;
                    duration *= 0.9f;
                    break;
                case EmotionType.Sadness:
                    pitch = _basePitch * (1 + _sadnessPitchShift);
                    variation *= 0.3f;
                    duration *= 1.3f;
                    break;
                case EmotionType.Fear:
                    pitch = _basePitch * (1 + _fearPitchShift);
                    variation *= 2.0f;
                    duration *= 0.7f;
                    break;
                case EmotionType.Hope:
                    // Через поле, а не литералом: 1.2 здесь и есть
                    // (1 + _hopePitchShift), только вписанное числом —
                    // оттого ручка надежды была единственной, которая
                    // ничего не крутила, а компилятор считал поле мёртвым.
                    pitch = _basePitch * (1 + _hopePitchShift);
                    variation *= 0.8f;
                    duration *= 1.1f;
                    break;
            }

            float finalPitch = pitch * (1f + variation * Wobble(letter));
            AudioClip clip = Clip(voiceType, finalPitch, duration);
            _audioSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Тембр, каким он задуман: <b>по оболочке, а поверх — по греху</b>
        /// (<c>00-GDD.md</c> §9). «Синус для духов, меандр для скелетов,
        /// треугольник для зомби, пила для гневных».
        ///
        /// До 18 сентября тембр брался только у эмоции, а спокойный воин
        /// получал <c>_defaultVoice</c> — то есть меандр у всех подряд,
        /// от призрака до голема. Оболочки в голосе не было слышно вовсе.
        ///
        /// Голем и живые в GDD не названы: голему дан треугольник, потому
        /// что он камень и глух, живым — меандр как середина. Это мой
        /// выбор, а не запись из документа.
        /// </summary>
        private VoiceType Timbre()
        {
            if (_warrior == null || _warrior.Soul == null) return _defaultVoice;

            // Грех громче тела: гневный рычит пилой в любой оболочке.
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

        /// <summary>
        /// Насколько голос выше или ниже основы. Камень гудит низко,
        /// бесплотный звенит высоко — это слышно раньше, чем игрок
        /// успевает посмотреть, кто говорит.
        /// </summary>
        private float Height()
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
        /// Разброс высоты от самой буквы, а не от жребия.
        ///
        /// Здесь стоял <c>Random.Range</c>, и это нарушало главное
        /// правило проекта: одинаковый вход обязан давать одинаковый
        /// выход. Одна и та же реплика звучала каждый раз иначе.
        ///
        /// Теперь дрожание выводится из кода буквы — и заодно выходит
        /// ближе к образцу: в Undertale высота пляшет именно по тексту,
        /// отчего у реплики появляется своя мелодия, одна и та же
        /// при каждом прочтении.
        ///
        /// Ноль (вызов без буквы, как у отказа) — ровный тон.
        /// </summary>
        private static float Wobble(char letter)
        {
            if (letter == '\0') return 0f;

            return ((letter * 37) % 23) / 11f - 1f;
        }

        /// <summary>
        /// Готовый клип для этого тембра и высоты.
        ///
        /// Кэш заведён вместе с подключением голоса: бип звучит
        /// на **каждой букве**, тридцать с лишним раз в секунду, и без
        /// кэша игра рожала бы столько же коротких клипов — по десять
        /// килобайт каждый. Собирать мусор посреди реплики значит
        /// дёрнуть кадр там, где на него и смотрят.
        ///
        /// Высота округляется до герца: на слух это не различимо,
        /// а разных ключей становится десятки вместо тысяч.
        /// </summary>
        private AudioClip Clip(VoiceType type, float frequency, float dur)
        {
            var key = (type, Mathf.RoundToInt(frequency), Mathf.RoundToInt(dur * 1000f));

            if (_clips.TryGetValue(key, out var ready) && ready != null) return ready;

            var made = GenerateClip(type, key.Item2, dur);
            _clips[key] = made;
            return made;
        }

        private AudioClip GenerateClip(VoiceType type, float frequency, float dur)
        {
            int samples = Mathf.CeilToInt(_sampleRate * dur);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / _sampleRate;
                float value = 0f;

                switch (type)
                {
                    case VoiceType.Sine:
                        value = Mathf.Sin(2 * Mathf.PI * frequency * t);
                        break;
                    case VoiceType.Square:
                        value = Mathf.Sin(2 * Mathf.PI * frequency * t) >= 0 ? 1f : -1f;
                        break;
                    case VoiceType.Triangle:
                        value = 1f - 4f * Mathf.Abs(Mathf.Round(t * frequency - 0.25f) - (t * frequency - 0.25f));
                        break;
                    case VoiceType.Sawtooth:
                        value = 2f * (t * frequency - Mathf.Floor(t * frequency + 0.5f));
                        break;
                }

                float envelope = Mathf.Min(1f, (float)(samples - i) / (samples * 0.1f));
                data[i] = value * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Voice", samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}