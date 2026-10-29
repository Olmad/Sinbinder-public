// Assets/Scripts/Gameplay/BalalaikaHabit.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.AI;

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
        // Шире, чем «остановиться» у ног (WarriorRig: 1,2 м) с запасом: при 1,0 Ждан вставал у бревна в 1,2 м
        // и ждал, что дойдёт, — не садился никогда (проход демо 29.10, «ЗАСТРЯЛО: Ждан сел…»).
        private const float Reach = 1.6f;

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

        /// <summary>
        /// Сесть играть сейчас — его час с этого мига, балалайка в руках, сам — на бревне (съёмка трейлера: ждать,
        /// пока дойдёт, ролик не может — в дубле 1 он шёл с балалайкой в руке, когда до него дошла камера).
        /// </summary>
        public static void SitNow(Warrior w)
        {
            if (w == null || !w.TryGetComponent<BalalaikaHabit>(out var h)) return;
            HourNow(w);
            if (!h.Now(out _) && h._lute == null) return;
            if (h._seated) return;
            if (!h._holding || h._seat == null) h.Take();
            if (h._seat == null) return;
            var legs = h.GetComponent<NavMeshAgent>();
            if (legs != null && legs.enabled && legs.isOnNavMesh) legs.Warp(h._stand);
            h.transform.position = h._stand;
            h.SitDown();
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
            else if (_holding && hour && _seat == null)
            {
                // Места не нашлось — искать снова раз в две секунды (проход 29.10: однажды все места показались
                // занятыми, и Ждан так и играл стоя). Брёвен у огня нет вовсе — играет стоя.
                if (Time.time >= _retry) { _retry = Time.time + 2f; Take(); }
                if (_seat == null && LogSeat.Near(transform.position, 14f).Count == 0) SitDown();
            }
        }

        private float _retry;
        private float _crowded;

        /// <summary>Сесть на бревно: те же ноги, что у Греховода (LogSeat.PoseFor), — тело на кору, не в воздух.</summary>
        private void SitDown()
        {
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (_seat != null)
            {
                // Сидящий — не помеха идущим: ноги из толпы вон, пока сидит (проход демо 29.10: Хорь стоял,
                // упершись в Ждана на бревне у прохода).
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
                if (agent != null) agent.enabled = false;
                // Сидящего обходят: ноги выключены, но препятствие — на месте (без него в него садились и шли).
                // TryGetComponent, а не «??»: пустой компонент в Unity — не null для «??» (поток ошибок 29.10).
                if (!TryGetComponent<NavMeshObstacle>(out var block)) block = gameObject.AddComponent<NavMeshObstacle>();
                block.shape = NavMeshObstacleShape.Capsule; block.radius = 0.35f; block.height = 1.2f; block.carving = false;
                block.enabled = true;
                transform.SetPositionAndRotation(_sat, _facing);
                if (_body != null) { _body.Seated = true; _body.Strumming = true; }
                _seated = true;
            }
            Strum(true);
            InHands();
        }

        private void HoldSeat()
        {
            if (_seat != null) transform.SetPositionAndRotation(_sat, _facing);
            InHands();
            // Встал кто-то вплотную и стоит (у костра своё место — и оно бывает тем же, проход демо 29.10: Ждан и
            // Вейн в одной точке) — встать и искать другое бревно. Прошёл мимо — не повод: полторы секунды подряд.
            if (_seat == null || Time.frameCount % 15 != 0) return;
            // Вплотную — ближе 0,4 м: сосед на своём месте у костра в метре — не повод вставать.
            if (Room(_sat, true) >= 0.4f) { _crowded = 0f; return; }
            if (_crowded == 0f) { _crowded = Time.time; return; }
            if (Time.time - _crowded < 1.5f) return;
            _crowded = 0f;
            Debug.Log($"[ЖДАН] место занято — пересаживается с «{_seat.name}»");
            Stand();
            Take();
        }

        private Transform _left, _right;
        private Quaternion _luteBasis;
        private bool _basisReady;

        /// <summary>
        /// Балалайка — в руках, а не на бревне (кадр 29.10: лежала перед ним, руки висели): корпус у правой кисти,
        /// гриф — к левой, дека — от груди. Оси инструмента берутся из его модели: длинная — гриф (в ту сторону,
        /// куда смещена рамка от корпуса), тонкая — дека. Каждый кадр — руки ходят клипом Strum.
        /// </summary>
        private void InHands()
        {
            if (_lute == null) return;
            if (_left == null || _right == null)
                foreach (var t in GetComponentsInChildren<Transform>())
                {
                    if (t.name == "LeftHand") _left = t;
                    else if (t.name == "RightHand") _right = t;
                }
            if (_left == null || _right == null) return;
            if (!_basisReady)
            {
                var mf = _lute.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return;
                var b = mf.sharedMesh.bounds;
                var e = b.extents;
                Vector3 neck = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
                Vector3 deck = e.x <= e.y && e.x <= e.z ? Vector3.right : e.y <= e.z ? Vector3.up : Vector3.forward;
                if (Vector3.Dot(b.center, neck) < 0f) neck = -neck;
                _luteBasis = Quaternion.Inverse(Quaternion.LookRotation(neck, deck));
                _basisReady = true;
            }
            var along = _left.position - _right.position;
            if (along.sqrMagnitude < 1e-4f) return;
            _lute.rotation = Quaternion.LookRotation(along.normalized, transform.forward) * _luteBasis;
            _lute.position = _right.position + transform.forward * 0.06f;
        }

        private void Stand()
        {
            _seated = false;
            Strum(false);
            if (_body != null) { _body.Seated = false; _body.Strumming = false; }
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (TryGetComponent<NavMeshObstacle>(out var block)) block.enabled = false;
            transform.position = _stand;
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(_stand);
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
            // Бревно — ближнее свободное (проход демо 29.10: сел в ту же точку, где сидел Вейн Тихий): место на
            // нём не занято другим; встать перед ним — на землю, не на палатку (там же: шёл по палатке на 0,4 м).
            // Свободное место — где до соседа не меньше 0,9 м. Такого нет (проход 29.10: в час Ждана все уже
            // стоят у брёвен на своих местах у костра — и каждая половина бревна «занята») — самое просторное,
            // если там хоть полметра: сидеть тесно лучше, чем играть стоя.
            _seat = null;
            float best = 0.5f;
            int tried = 0;
            foreach (var log in LogSeat.Near(transform.position, 14f))
            {
                // Ближняя половина бревна, потом дальняя — точка, отражённая через середину бревна.
                var middle = log.Centre; middle.y = log.transform.position.y;
                foreach (var from in new[] { transform.position, middle * 2f - transform.position })
                {
                    if (!log.PoseFor(transform, from, out var stand, out var sat, out var facing)) continue;
                    tried++;
                    float room = Room(sat, false);
                    if (room <= best) continue;
                    float ground = log.transform.position.y;
                    if (Ground(stand, ground, out var here)) stand = here;
                    else if (Ground(middle * 2f - stand, ground, out var there)) stand = there;
                    else continue;
                    _seat = log; _stand = stand; _sat = sat; _facing = facing;
                    best = room;
                    if (room >= 0.9f) break;
                }
                if (best >= 0.9f) break;
            }
            Debug.Log(_seat != null ? $"[ЖДАН] берёт балалайку — к бревну «{_seat.name}», до соседа {best:F1} м"
                                    : $"[ЖДАН] берёт балалайку — места нет (мест у брёвен {tried})");
        }

        /// <summary>До ближнего живого от места, м (не дальше 9). still — только стоящие (идущий мимо — не занял).</summary>
        private float Room(Vector3 sat, bool still)
        {
            float near = 9f;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w == _warrior || w.IsDead) continue;
                if (still && w.TryGetComponent<NavMeshAgent>(out var a) && a.enabled && a.velocity.sqrMagnitude > 0.04f) continue;
                near = Mathf.Min(near, CampFocus.GroundDistance(w.transform.position, sat));
            }
            return near;
        }

        private static bool Ground(Vector3 at, float ground, out Vector3 hit)
        {
            hit = at;
            if (!UnityEngine.AI.NavMesh.SamplePosition(at, out var h, 0.6f, UnityEngine.AI.NavMesh.AllAreas)) return false;
            if (Mathf.Abs(h.position.y - ground) > 0.15f) return false;
            hit = h.position;
            return true;
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
            if (!_holding) return;
            // Ждана выключают целиком (сцена, набег) — переносить балалайку нельзя, пока он гаснет
            // (проход демо 29.10: «Cannot set the parent… while deactivating»): она уйдёт вместе с ним.
            if (!gameObject.activeInHierarchy) { Strum(false); return; }
            Put();
        }
    }
}
