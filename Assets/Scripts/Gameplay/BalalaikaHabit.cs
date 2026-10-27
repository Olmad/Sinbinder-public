// Assets/Scripts/Gameplay/BalalaikaHabit.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Косой Ждан и балалайка (решение 27.10: «балалайка у Ждана, мимо нот — да»; автор, 26.10:
    /// «балалайка не используема — ни игроком, ни воинами»). В свой час, когда свободен, Ждан
    /// идёт к балалайке у огня, садится и берёт её на колени; час прошёл — кладёт на место.
    ///
    /// Звука пока нет: мелодию мимо нот автор слушает до того, как она встанет в игру
    /// (28-ORDERS, решение 18). Когда будет — играть её отсюда, пока он сидит.
    ///
    /// Часы — по имени, без жребия, как у копателя (<see cref="DiggerHabit"/>).
    /// Ставит себя сам: раз в две секунды ищет Ждана без привычки.
    /// </summary>
    public class BalalaikaHabit : MonoBehaviour
    {
        private const float Period = 110f;   // раз в столько секунд
        private const float PlayFor = 35f;   // столько сидит с балалайкой
        private const float Reach = 1.0f;

        private Warrior _warrior;
        private WarriorAnimation _body;
        private Transform _lute;
        private Vector3 _lutePos;
        private Quaternion _luteRot;
        private Transform _luteParent;
        private bool _holding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<Installer>() != null) return;
            var go = new GameObject("Балалайка Ждана");
            DontDestroyOnLoad(go);
            go.AddComponent<Installer>();
        }

        private class Installer : MonoBehaviour
        {
            private float _next;

            void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 2f;
                foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                    if (w.DisplayName != null && w.DisplayName.Contains("Ждан") && w.GetComponent<BalalaikaHabit>() == null)
                        w.gameObject.AddComponent<BalalaikaHabit>();
            }
        }

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
            _body = GetComponent<WarriorAnimation>();
        }

        /// <summary>Куда Ждану идти сейчас; ложь — не его час или балалайки нет.</summary>
        public static bool Point(Warrior w, out Vector3 at)
        {
            at = default;
            if (w == null || !w.TryGetComponent<BalalaikaHabit>(out var habit)) return false;
            return habit.Now(out at);
        }

        private bool Now(out Vector3 at)
        {
            at = default;
            if (_warrior == null || _warrior.IsDead || _warrior.HasCommand || RaidEvent.Running) return false;
            if (_lute == null)
            {
                var found = GameObject.Find("Balalaika");
                if (found == null) return false;
                _lute = found.transform;
                _lutePos = _lute.position; _luteRot = _lute.rotation; _luteParent = _lute.parent;
            }
            float t = Time.time + Wardrobe.Stamp(_warrior.DisplayName) % 50;
            if (t % Period > PlayFor) return false;
            at = _lutePos + (CampFocus.GroundDistance(_lutePos, Vector3.zero) > 0.1f ? -_lutePos.normalized : Vector3.forward) * 0.6f;
            return true;
        }

        void Update()
        {
            bool time = Now(out var at);
            bool there = time && CampFocus.GroundDistance(transform.position, at) <= Reach;

            if (there && !_holding) Take();
            else if (!there && _holding) Put();
        }

        private void Take()
        {
            _holding = true;
            if (_body != null) _body.Seated = true;
            if (_lute == null) return;
            // На колени: поперёк, грифом влево — как держат.
            _lute.SetParent(transform, true);
            _lute.localPosition = new Vector3(0.05f, 0.55f, 0.32f);
            _lute.localRotation = Quaternion.Euler(-20f, -70f, 0f) * Quaternion.Inverse(transform.rotation) * _luteRot;
        }

        private void Put()
        {
            _holding = false;
            if (_body != null) _body.Seated = false;
            if (_lute == null) return;
            _lute.SetParent(_luteParent, true);
            _lute.SetPositionAndRotation(_lutePos, _luteRot);
        }

        void OnDisable()
        {
            if (_holding) Put();
        }
    }
}
