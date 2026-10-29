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
    /// Звук — пока сидит с балалайкой (автор, 27.10: «Ждан брынькает без звука»): свой напев мимо
    /// нот (Tools/audio/balalaika.py → Sounds/Balalaika), из его рук, по кругу; слышно метров на
    /// восемнадцать. Молчит, когда поднят шар (тишина ритуала), и в набеге.
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
            if (_warrior == null || _warrior.IsDead || _warrior.HasCommand || RaidEvent.Running || CrystalBall.Raised) return false;
            if (_lute == null)
            {
                var found = GameObject.Find("Balalaika");
                if (found == null) return false;
                _lute = found.transform;
                _lutePos = _lute.position; _luteRot = _lute.rotation; _luteParent = _lute.parent;
            }
            float t = Time.time + Shift + Wardrobe.Stamp(_warrior.DisplayName) % 50;
            bool hour = t % Period <= PlayFor;
            // Свой час — сперва к балалайке, взял — к бревну (автор, 28.10: «Косой Ждан играет, сидя на воздухе,
            // а должен брать балалайку и садиться на бревно»); час прошёл — отнести на место.
            if (!_holding) { if (!hour) return false; at = LuteSpot(); return true; }
            if (hour && _seat != null) { at = _stand; return true; }
            if (hour) return false;
            at = LuteSpot();
            return true;
        }

        private Vector3 LuteSpot()
            => _lutePos + (CampFocus.GroundDistance(_lutePos, Vector3.zero) > 0.1f ? -_lutePos.normalized : Vector3.forward) * 0.6f;

        /// <summary>Сдвиг часов привычки — прогон демо ставит час Ждана сразу (DemoWalkthrough).</summary>
        public static float Shift;

        /// <summary>Сидит ли с балалайкой на бревне (прогон проверяет и снимает).</summary>
        public static bool SeatedWith(Warrior w) => w != null && w.TryGetComponent<BalalaikaHabit>(out var h) && h._seated && h._seat != null;

        /// <summary>Час Ждана — сейчас: сдвинуть часы так, чтобы его время только началось.</summary>
        public static void HourNow(Warrior w)
        {
            if (w == null) return;
            float t = Time.time + Wardrobe.Stamp(w.DisplayName) % 50;
            Shift = Period - t % Period;
        }

        private LogSeat _seat;
        private Vector3 _stand, _sat;
        private Quaternion _facing;
        private bool _seated;

        void Update()
        {
            bool go = Now(out var at);
            if (_warrior == null) return;
            float t = Time.time + Shift + Wardrobe.Stamp(_warrior.DisplayName) % 50;
            bool hour = t % Period <= PlayFor && !_warrior.IsDead && !_warrior.HasCommand && !RaidEvent.Running && !CrystalBall.Raised;

            // Сидит — пока час; кончился или позвали — встать.
            if (_seated) { if (!hour) Stand(); else HoldSeat(); return; }

            bool there = go && CampFocus.GroundDistance(transform.position, at) <= Reach;
            if (!_holding && hour && there) Take();
            else if (_holding && hour && _seat != null && there) SitDown();
            else if (_holding && !hour && there) Put();
            else if (_holding && hour && _seat == null) { SitDown(); }   // брёвен нет — играет стоя
        }

        /// <summary>Сесть на бревно: те же ноги, что у Греховода (LogSeat.PoseFor), — тело на кору, не в воздух.</summary>
        private void SitDown()
        {
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (_seat != null)
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
                if (agent != null) { agent.updatePosition = false; agent.updateRotation = false; }
                transform.SetPositionAndRotation(_sat, _facing);
                if (_body != null) _body.Seated = true;
                _seated = true;
            }
            Strum(true);
            if (_lute != null)
            {
                _lute.localPosition = new Vector3(0.05f, 0.55f, 0.32f);
                _lute.localRotation = Quaternion.Euler(-20f, -70f, 0f) * Quaternion.Inverse(transform.rotation) * _luteRot;
            }
        }

        private void HoldSeat() { if (_seat != null) transform.SetPositionAndRotation(_sat, _facing); }

        private void Stand()
        {
            _seated = false;
            Strum(false);
            if (_body != null) _body.Seated = false;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            transform.position = _stand;
            if (agent != null)
            {
                agent.Warp(_stand);
                agent.updatePosition = true; agent.updateRotation = true;
                if (agent.isOnNavMesh) agent.isStopped = false;
            }
            _seat = null;
            // Балалайка — в руке, по пути назад.
            if (_lute != null) { _lute.localPosition = new Vector3(0.28f, 0.95f, 0.12f); _lute.localRotation = Quaternion.Euler(0f, 0f, 70f); }
        }

        private AudioSource _sound;

        /// <summary>Напев — из его рук: по кругу, пока сидит; дальше 18 м не слышно.</summary>
        private void Strum(bool on)
        {
            if (!on) { if (_sound != null) _sound.Stop(); return; }
            var clip = Resources.Load<AudioClip>("Sounds/Balalaika");
            if (clip == null) return;
            if (_sound == null)
            {
                _sound = gameObject.AddComponent<AudioSource>();
                _sound.playOnAwake = false;
                _sound.loop = true;
                _sound.spatialBlend = 1f;
                _sound.rolloffMode = AudioRolloffMode.Linear;
                _sound.minDistance = 2f;
                _sound.maxDistance = 18f;
                _sound.dopplerLevel = 0f;
                _sound.volume = 0.55f;
            }
            _sound.clip = clip;
            _sound.Play();
        }

        public static bool Playing(Warrior w)
            => w != null && w.TryGetComponent<BalalaikaHabit>(out var h) && h._sound != null && h._sound.isPlaying;

        /// <summary>Взять балалайку в руку и выбрать бревно поближе к огню (до 14 м).</summary>
        private void Take()
        {
            _holding = true;
            if (_lute != null)
            {
                _lute.SetParent(transform, true);
                _lute.localPosition = new Vector3(0.28f, 0.95f, 0.12f);
                _lute.localRotation = Quaternion.Euler(0f, 0f, 70f);
            }
            _seat = LogSeat.Nearest(transform.position, 14f);
            if (_seat != null && !_seat.PoseFor(transform, out _stand, out _sat, out _facing)) _seat = null;
        }

        private void Put()
        {
            if (_seated) Stand();
            _holding = false;
            Strum(false);
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
