// Assets/Scripts/Gameplay/CourtCaller.cs
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Суд Души — по делу. Автор, 06.11: «эту НЕВЕРОЯТНО ВЕЛИКОЛЕПНУЮ сцену нужно
    /// показывать чаще, но по делу».
    ///
    /// Слушает отказы приказу (<see cref="AOSEventHub.OnRefusal"/>) в лагере и в поле
    /// и созывает суд, когда отказ того стоит (<see cref="SoulCourt.Importance"/>):
    /// на кону жизнь, отказал старший, или суд чему-то учит. Не чаще раза в
    /// <see cref="Cooldown"/> секунд, не поверх другой сцены, окна или урока. Во сне
    /// суд зовёт сам сон (DreamTutorialDirector). Настройка «Показывать Суд Души» —
    /// <see cref="Preferences.Court"/>.
    /// </summary>
    public class CourtCaller : MonoBehaviour
    {
        /// <summary>С этой важности отказ стоит суда.</summary>
        public const float Threshold = 0.5f;

        /// <summary>Между двумя судами — не меньше, секунд: суд — событие, а не помеха.</summary>
        public const float Cooldown = 180f;

        /// <summary>Чаще этого важность не считаем: в большом бою отказов десятки в секунду.</summary>
        private const float Throttle = 0.25f;

        /// <summary>Почему созван последний суд — для прогона и журнала.</summary>
        public static string Last { get; private set; } = "";

        private static CourtCaller _instance;
        private AOSEventHub _hub;
        private float _nextAt, _nextLook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { _instance = null; Last = ""; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Суд по делу");
            _instance = go.AddComponent<CourtCaller>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            var hub = AOSEventHub.Instance;
            if (hub == _hub) return;
            if (_hub != null) _hub.OnRefusal -= Refused;
            _hub = hub;
            if (_hub != null) _hub.OnRefusal += Refused;
        }

        void OnDestroy()
        {
            if (_hub != null) _hub.OnRefusal -= Refused;
        }

        private void Refused(Warrior w, Decision d, DecisionContext c)
        {
            if (!Preferences.Court || SoulCourt.Sitting || DreamTutorialDirector.Instance != null) return;
            if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer) return;
            float now = Time.unscaledTime;
            if (now < _nextAt || now < _nextLook || Busy()) return;
            _nextLook = now + Throttle;

            float weight = SoulCourt.Importance(w, d, c, out string why);
            if (weight < Threshold) return;

            _nextAt = now + Cooldown;
            Last = $"{w.DisplayName}: важность {weight:F2} ({why})";
            Debug.Log("[СУД ПО ДЕЛУ] " + Last);
            StartCoroutine(SoulCourt.Convene(w, d, c.Copy()));
        }

        /// <summary>Экран занят другим: сцена, окно, урок, шар, заставка — суд подождёт следующего повода.</summary>
        private static bool Busy()
        {
            var pause = GamePauseController.Instance;
            var frame = Dialogue.DialogueCameraController.Instance;
            var box = UI.Letterbox.Instance;
            return (pause != null && pause.IsPaused)
                || (frame != null && frame.InDialogue)
                || (box != null && box.Shown)
                || UI.Modal.Any || Lesson.Holding || CrystalBall.Gazing || Herald.Busy
                || OpeningReveal.Busy || UI.PrologueTitleUI.Showing;
        }
    }
}
