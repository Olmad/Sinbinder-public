// Assets/Audio/VoiceGenerator.cs
using UnityEngine;
using Sinbinder.AOS;
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

        public void Speak()
        {
            if (_audioSource == null) return;

            EmotionType emotion = EmotionType.Calm;
            VoiceType voiceType = _defaultVoice;
            float pitch = _basePitch;
            float variation = _pitchVariation;
            float duration = _duration;

            if (_useEmotions && _warrior != null && EmotionSystem.Instance != null)
            {
                emotion = EmotionSystem.Instance.GetDominantEmotion(_warrior);
            }

            switch (emotion)
            {
                case EmotionType.Anger:
                    voiceType = VoiceType.Sawtooth;
                    pitch = _basePitch * (1 + _angerPitchShift);
                    variation *= 0.5f;
                    duration *= 0.8f;
                    break;
                case EmotionType.Joy:
                    voiceType = VoiceType.Square;
                    pitch = _basePitch * (1 + _joyPitchShift);
                    variation *= 1.5f;
                    duration *= 0.9f;
                    break;
                case EmotionType.Sadness:
                    voiceType = VoiceType.Triangle;
                    pitch = _basePitch * (1 + _sadnessPitchShift);
                    variation *= 0.3f;
                    duration *= 1.3f;
                    break;
                case EmotionType.Fear:
                    voiceType = VoiceType.Sine;
                    pitch = _basePitch * (1 + _fearPitchShift);
                    variation *= 2.0f;
                    duration *= 0.7f;
                    break;
                case EmotionType.Hope:
                    voiceType = VoiceType.Sine;
                    pitch = _basePitch * 1.2f;
                    variation *= 0.8f;
                    duration *= 1.1f;
                    break;
            }

            float finalPitch = pitch * (1f + Random.Range(-variation, variation));
            AudioClip clip = GenerateClip(voiceType, finalPitch, duration);
            _audioSource.PlayOneShot(clip);
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