// Assets/Scripts/UI/StaminaHintUI.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

using Sinbinder.Core;
namespace Sinbinder.UI
{
    /// <summary>
    /// Подсказка о силах — один раз, когда свой воин впервые выдохся.
    ///
    /// Карта обучения (docs/36-LESSONS.md §7, 26 октября; автор: «нужно
    /// УСИЛЕННО СЕСТЬ ЗА УРОКИ, ОБЪЯСНЕНИЯ»): полоса сил есть
    /// (<see cref="StaminaBarUI"/>), слово «выдохся» — в причинах отказа,
    /// а что полоса — это силы, что уставший бьёт вполсилы и что силы
    /// возвращаются, только пока воин стоит, — не сказано нигде.
    ///
    /// Строкой, а не уроком стоп-кадром: уроки — глаголы (`36-LESSONS.md` §2),
    /// а силы — правило, которое достаточно назвать в миг, когда оно впервые
    /// сработало. Раньше — пустой звук, позже — игрок уже проиграл из-за него.
    /// Ждёт, пока идёт урок, — две плашки разом читаются списком требований.
    /// </summary>
    public class StaminaHintUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _text;

        public static readonly string Line =
            Loc.N("{0} выдохся: уставший бьёт вполсилы. Полоса под здоровьем — силы; "
          + "они возвращаются, только пока он стоит.");

        public static readonly string LineFemale =
            Loc.N("{0} выдохлась: уставшая бьёт вполсилы. Полоса под здоровьем — силы; "
          + "они возвращаются, только пока она стоит.");

        [Tooltip("Сколько держать подсказку на экране.")]
        [SerializeField] private float _holdSeconds = 9f;

        /// <summary>Показывали ли уже. Один раз за игру.</summary>
        public static bool Shown { get; private set; }

        /// <summary>Кто выдохся, когда подсказка встала. Для прогона.</summary>
        public static string Who { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Shown = false; Who = null; }

        /// <summary>Забыть подсказку. Новая игра.</summary>
        public static void Forget() { Shown = false; Who = null; }

        private float _hideAt = -1f;
        private float _nextLook;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        void Update()
        {
            if (_hideAt > 0f)
            {
                // Реальное время: дочитывается и на паузе.
                if (Time.unscaledTime >= _hideAt) Hide();
                return;
            }

            if (Shown) { enabled = false; return; }
            if (Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + 0.5f;

            if (Lesson.Holding) return;

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer) continue;
                if (!w.TryGetComponent<Fatigue>(out var f) || !f.IsExhausted) continue;
                Show(w);
                return;
            }
        }

        private void Show(Warrior w)
        {
            Shown = true;
            Who = w.DisplayName;
            _hideAt = Time.unscaledTime + _holdSeconds;

            // Пара форм — через Grammar, как у причин отказа.
            string line = w.Soul != null ? Grammar.Pick(w.Soul.Gender, Line, LineFemale) : Line;
            if (_text != null) _text.text = Loc.F(line, Loc.Name(w.DisplayName));
            if (_panel != null) _panel.SetActive(true);
            Debug.Log($"[ПОДСКАЗКА] силы: {w.DisplayName} выдохся — строка о силах показана.");
        }

        private void Hide()
        {
            _hideAt = -1f;
            if (_panel != null) _panel.SetActive(false);
            enabled = false;
        }
    }
}
