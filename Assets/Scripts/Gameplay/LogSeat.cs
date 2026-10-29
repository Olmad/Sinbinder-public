// Assets/Scripts/Gameplay/LogSeat.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Бревно у огня — на него садятся (автор, 26.10: «интересно посидеть возле костра на бревне»;
    /// 27.10: «на брёвна возле костра по-прежнему нельзя сесть»). F, глядя на бревно, — Греховод
    /// садится на ближнюю к нему половину лицом к огню, поза — та же, что у игроков за картами
    /// (<see cref="WarriorAnimation.Seated"/>, таз на верх бревна). Встаёт от шага (WASD глазами),
    /// от приказа мышью и от F ещё раз — и оказывается перед бревном, на навмеше.
    ///
    /// Ставит себя сам на каждое бревно сцены (модель «LogBench», props.py): брёвна кладёт сборщик
    /// сцен, и метить каждое руками значило бы забыть одно.
    /// </summary>
    public class LogSeat : MonoBehaviour
    {
        /// <summary>С какого расстояния по земле дотянуться до бревна, м.</summary>
        private const float Reach = 2.0f;

        /// <summary>Таз сидящего — на 41 % ниже стоячего (клип SitIdle; так же у CardTable).</summary>
        private const float SeatedHips = 0.591f;

        /// <summary>Огонь не дальше стольких метров — садятся лицом к нему.</summary>
        private const float FireNear = 7f;

        private static readonly List<LogSeat> Live = new();

        /// <summary>Бревно, на котором Греховод сидит сейчас. Нет — null.</summary>
        public static LogSeat Taken { get; private set; }
        public static bool Sitting => Taken != null;

        private Renderer _renderer;
        private Vector3 _stand;
        private Vector3 _sat;
        private Quaternion _facing;
        private int _satFrame;
        private bool _updatedPosition, _updatedRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            Live.Clear();
            Taken = null;
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void First() => Install();

        private static void OnScene(Scene scene, LoadSceneMode mode) => Install();

        private static void Install()
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var go = mf.gameObject;
                if (go.name.StartsWith("LogBench") && go.GetComponent<LogSeat>() == null) go.AddComponent<LogSeat>();
            }
        }

        void Awake() => _renderer = GetComponent<Renderer>();
        void OnEnable() => Live.Add(this);

        void OnDisable()
        {
            Live.Remove(this);
            if (Taken == this) Rise();
        }

        /// <summary>Брёвна, до которых достаёт рука; сидя — никакие: сначала встать.</summary>
        public static IEnumerable<LogSeat> InReach()
        {
            if (!SinbinderPlayer.Exists || Sitting) yield break;
            foreach (var s in Live)
                if (s != null && CampFocus.GroundDistance(SinbinderPlayer.Where, s.transform.position) <= Reach)
                    yield return s;
        }

        /// <summary>Середина бревна — туда смотрят, чтобы сесть.</summary>
        public Vector3 Centre => _renderer != null ? _renderer.bounds.center : transform.position + Vector3.up * 0.25f;

        public string Prompt => Loc.T("F — сесть на бревно");

        /// <summary>Брёвна у точки от ближнего к дальнему (не дальше <paramref name="max"/>) — Ждан ищет свободное.</summary>
        public static List<LogSeat> Near(Vector3 from, float max)
        {
            var got = new List<LogSeat>();
            foreach (var s in Live)
                if (s != null && s != Taken && CampFocus.GroundDistance(from, s.transform.position) <= max) got.Add(s);
            got.Sort((a, b) => CampFocus.GroundDistance(from, a.transform.position).CompareTo(CampFocus.GroundDistance(from, b.transform.position)));
            return got;
        }

        /// <summary>Ближнее бревно к точке (не дальше <paramref name="max"/>); нет — null. Зовёт Ждан с балалайкой.</summary>
        public static LogSeat Nearest(Vector3 from, float max)
        {
            LogSeat best = null;
            float d = max;
            foreach (var s in Live)
            {
                if (s == null || s == Taken) continue;
                float k = CampFocus.GroundDistance(from, s.transform.position);
                if (k < d) { d = k; best = s; }
            }
            return best;
        }

        /// <summary>
        /// Как сесть на это бревно тому, кто подходит, — та же мерка, что у Греховода (<see cref="Sit"/>): ближняя
        /// половина бревна, лицом к огню, таз — над корой. stand — где встать перед бревном.
        /// </summary>
        public bool PoseFor(Transform who, out Vector3 stand, out Vector3 sat, out Quaternion facing)
        {
            stand = sat = default; facing = Quaternion.identity;
            if (who == null || _renderer == null) return false;
            var axis = transform.right; axis.y = 0f;
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
            axis.Normalize();
            var mf = GetComponent<MeshFilter>();
            float length = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.x * Mathf.Abs(transform.lossyScale.x) : 1.9f;
            var middle = Centre; middle.y = transform.position.y;
            float along = Mathf.Clamp(Vector3.Dot(who.position - middle, axis), -length * 0.25f, length * 0.25f);
            var seat = middle + axis * along;
            var toward = who.position - seat;
            var fire = NearestFire(seat);
            if (fire.HasValue) toward = fire.Value - seat;
            var across = Vector3.Cross(Vector3.up, axis);
            var forward = Vector3.Dot(toward, across) >= 0f ? across : -across;
            float hip = 0.95f;
            var hips = Find(who, "Hips");
            if (hips != null) hip = Mathf.Max(0.3f, hips.position.y - who.position.y);
            sat = seat + forward * 0.12f;
            sat.y = _renderer.bounds.max.y + 0.02f - hip * SeatedHips;
            facing = Quaternion.LookRotation(forward);
            stand = seat + forward * 0.6f;
            stand.y = transform.position.y;
            return true;
        }

        public void Sit()
        {
            var hero = SinbinderPlayer.Instance;
            if (hero == null || hero.IsDead || Sitting || _renderer == null) return;

            // Длинная ось бревна — X модели; сесть на ту половину, что ближе к Греховоду.
            var axis = transform.right; axis.y = 0f;
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
            axis.Normalize();
            var mf = GetComponent<MeshFilter>();
            float length = mf != null && mf.sharedMesh != null
                ? mf.sharedMesh.bounds.size.x * Mathf.Abs(transform.lossyScale.x) : 1.9f;
            var middle = Centre; middle.y = transform.position.y;
            float along = Mathf.Clamp(Vector3.Dot(hero.transform.position - middle, axis), -length * 0.25f, length * 0.25f);
            var seat = middle + axis * along;

            // Лицом к огню, если он рядом; нет — к той стороне, откуда подошёл.
            var toward = hero.transform.position - seat;
            var fire = NearestFire(seat);
            if (fire.HasValue) toward = fire.Value - seat;
            var across = Vector3.Cross(Vector3.up, axis);
            var forward = Vector3.Dot(toward, across) >= 0f ? across : -across;

            float hip = 0.95f;
            var hips = Find(hero.transform, "Hips");
            if (hips != null) hip = Mathf.Max(0.3f, hips.position.y - hero.transform.position.y);

            _sat = seat + forward * 0.12f;
            _sat.y = _renderer.bounds.max.y + 0.02f - hip * SeatedHips;
            _facing = Quaternion.LookRotation(forward);
            _stand = seat + forward * 0.6f;
            _stand.y = transform.position.y;

            var agent = hero.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
                _updatedPosition = agent.updatePosition;
                _updatedRotation = agent.updateRotation;
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
            hero.transform.SetPositionAndRotation(_sat, _facing);
            if (hero.TryGetComponent<WarriorAnimation>(out var body)) body.Seated = true;

            var view = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            if (view != null && view.FirstPersonNow) view.Face(_sat + forward * 3f + Vector3.up * 0.6f);

            Taken = this;
            _satFrame = Time.frameCount;
            Debug.Log($"[БРЕВНО] Греховод сел: {name}");
        }

        /// <summary>Встать — перед бревном, на навмеше. Не сидит — ничего.</summary>
        public static void Rise()
        {
            var seat = Taken;
            if (seat == null) return;
            Taken = null;

            var hero = SinbinderPlayer.Instance;
            if (hero == null) return;
            if (hero.TryGetComponent<WarriorAnimation>(out var body)) body.Seated = false;

            var at = seat._stand;
            if (NavMesh.SamplePosition(at, out var hit, 1.5f, NavMesh.AllAreas)) at = hit.position;
            hero.transform.position = at;

            var agent = hero.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.Warp(at);
                agent.updatePosition = seat._updatedPosition;
                agent.updateRotation = seat._updatedRotation;
                if (agent.isOnNavMesh) agent.isStopped = false;
            }
            Debug.Log("[БРЕВНО] Греховод встал");
        }

        void Update()
        {
            if (Taken != this) return;
            var hero = SinbinderPlayer.Instance;
            if (hero == null || hero.IsDead) { Rise(); return; }

            // Приказ мышью — встать и идти: агент проложил путь, пока тело сидело.
            var agent = hero.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh && (agent.hasPath || agent.pathPending))
            {
                Rise();
                return;
            }

            // Тело держим на бревне: от первого лица курс задаёт голова (RTS_Camera), сверху — бревно.
            var view = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            hero.transform.position = _sat;
            if (view == null || !view.FirstPersonNow) hero.transform.rotation = _facing;
        }

        /// <summary>Нажали F в тот же кадр, что сели, — это то же нажатие, а не «встать».</summary>
        public static bool JustSat => Taken != null && Taken._satFrame == Time.frameCount;

        private static Vector3? NearestFire(Vector3 from)
        {
            Vector3? best = null;
            float bestD = FireNear;
            foreach (var f in Object.FindObjectsByType<FireFlicker>(FindObjectsSortMode.None))
            {
                var at = f.transform.parent != null ? f.transform.parent.position : f.transform.position;
                float d = CampFocus.GroundDistance(from, at);
                if (d < bestD) { bestD = d; best = at; }
            }
            return best;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var hit = Find(c, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
