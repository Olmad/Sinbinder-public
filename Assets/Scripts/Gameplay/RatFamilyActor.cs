// Sinbinder v306. Individual animals; not Warrior and not a combat target.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RatFamilyActor : MonoBehaviour
    {
        public enum Nature { Mother, Bold, Curious, Playful, Shy, Sleepy }

        private static readonly List<RatFamilyActor> Live = new List<RatFamilyActor>();
        private static readonly string[] Names = { "Мама-крыса", "Пискун", "Пятнышко", "Прыг", "Мох", "Соня" };

        // Подсказки целиком, а не «с » + имя: имя в творительном падеже,
        // и строка цела для перевода (Resources/Lang/en.txt).
        private static readonly string[] Prompts =
        {
            "F — присесть к маме-крысе",
            "F — присесть к Пискуну",
            "F — присесть к Пятнышку",
            "F — присесть к Прыгу",
            "F — присесть к Моху",
            "F — присесть к Соне"
        };

        // Рассказ о Греховоде — в третьем лице, как весь журнал.
        private static readonly string[] Lines =
        {
            "Мама-крыса обнюхивает руку Греховода и оглядывается: все ли малыши рядом.",
            "Пискун забирается Греховоду на сапог — и тут же спрыгивает.",
            "Пятнышко трогает пальцы Греховода носом, будто впервые видит человека.",
            "Прыг крутится на месте и ловит собственный хвост.",
            "Мох прячется за маму и выглядывает одним глазом.",
            "Соня зевает. На сегодня с неё хватит приключений."
        };

        public int Index { get; private set; }
        public Nature Personality => (Nature)Index;
        public bool IsMother => Index == 0;
        public string Prompt => Loc.T(Prompts[Index]);
        public Vector3 Focus => transform.position + Vector3.up * (IsMother ? 0.08f : 0.05f);
        public bool OnMesh => _agent != null && _agent.enabled && _agent.isOnNavMesh;
        public bool Moving => OnMesh && !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.02f;
        public float DistanceFrom(RatFamilyActor other) => other == null ? float.PositiveInfinity
            : Vector3.Distance(transform.position, other.transform.position);

        private NavMeshAgent _agent;
        private Transform _visual;
        private RatTail _tail;
        private float _nextPause;
        private float _pausedUntil;
        private float _nextFollow;
        private float _happiness;
        private float _phase;
        private float _baseHeight;
        private AudioSource _voice;
        private static AudioClip _squeak;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { Live.Clear(); _squeak = null; }

        private void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        private void OnDisable() { Live.Remove(this); }

        public static IEnumerable<RatFamilyActor> InReach()
        {
            if (!SinbinderPlayer.Exists || RaidEvent.Running) yield break;
            if (OpeningReveal.Lying || OpeningReveal.Dreaming) yield break;
            var pause = GamePauseController.Instance;
            if (pause != null && pause.IsPaused) yield break;
            for (int i = 0; i < Live.Count; i++)
            {
                var rat = Live[i];
                if (rat != null && rat.isActiveAndEnabled &&
                    CampFocus.GroundDistance(rat.transform.position, SinbinderPlayer.Where) <= 2.25f)
                    yield return rat;
            }
        }

        public static RatFamilyActor Create(int index, Vector3 at, Transform parent)
        {
            if (index < 0 || index > 5) return null;
            var gameObject = new GameObject(Names[index]);
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.position = at;
            var actor = gameObject.AddComponent<RatFamilyActor>();
            actor.Initialize(index);
            return actor;
        }

        private void Initialize(int index)
        {
            Index = index;
            _phase = index * 1.37f;
            _baseHeight = 0f;
            _visual = RatFamilyVisual.Build(transform, index, out _tail);
            // Коллайдера нет: F находит крысу по взгляду (Gaze), а триггер
            // ловил бы лучи мыши, приказы по земле и проверки камеры разговора.
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 1f;
            _voice.volume = 0.24f;
            _voice.minDistance = 0.5f;
            _voice.maxDistance = 7f;
            _nextPause = Time.time + (index == 5 ? 4.5f : 7f + index * 1.8f);

            if (NavMesh.SamplePosition(transform.position, out var onGround, 2.5f, NavMesh.AllAreas))
                transform.position = onGround.position;
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.radius = index == 0 ? 0.09f : 0.06f;
            _agent.height = index == 0 ? 0.12f : 0.08f;
            _agent.speed = index == 0 ? 0.84f : (index == 5 ? 0.66f : 1.06f + (index == 1 ? 0.16f : 0f));
            _agent.angularSpeed = 480f;
            _agent.acceleration = 4.0f;
            _agent.stoppingDistance = index == 0 ? 0.19f : 0.16f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.autoBraking = true;
            if (!_agent.isOnNavMesh)
                Debug.LogWarning("[КРЫСЫ] Нет NavMesh под " + Names[index] + ". Она останется на месте, не пройдёт сквозь палатки.");
        }

        public bool MoveTo(Vector3 world)
        {
            if (!OnMesh || !NavMesh.SamplePosition(world, out var found, 2f, NavMesh.AllAreas)) return false;
            // Не посылать зверька к несвязанному острову сетки.
            var path = new NavMeshPath();
            if (!_agent.CalculatePath(found.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            _agent.isStopped = false;
            return _agent.SetDestination(found.position);
        }

        public void Wait(bool waiting)
        {
            if (OnMesh) _agent.isStopped = waiting;
        }

        public void Follow(RatFamilyActor parent)
        {
            if (parent == null || !OnMesh) return;
            if (Time.time < _pausedUntil) { Wait(true); return; }
            if (Time.time < _nextFollow) return;
            _nextFollow = Time.time + 0.31f + Index * 0.019f;

            // Каждый малыш следует за мамой по-своему. Никакой телепортации.
            float side = Index % 2 == 0 ? 1f : -1f;
            float behind = 0.38f + Index * 0.16f;
            float spread = Index == 4 ? 0.15f : 0.25f;
            if (Index == 1) behind = 0.20f; // Смелый постоянно норовит вперёд.
            if (Index == 3) spread += 0.25f * Mathf.Sin(Time.time * 2.2f);
            Vector3 target = parent.transform.position - parent.transform.forward * behind
                           + parent.transform.right * side * spread;
            if (Vector3.Distance(transform.position, target) < 0.20f) { Wait(true); return; }
            MoveTo(target);
        }

        private void Update()
        {
            if (_visual == null) return;
            bool moving = Moving;
            float t = Time.time;
            float gait = moving ? Mathf.Sin(t * (Index == 0 ? 13f : 18f) + _phase) : 0f;
            // Подскок — в долях роста: крыса 10 см, а не 30.
            _visual.localPosition = Vector3.up * (_baseHeight + Mathf.Abs(gait) * (IsMother ? 0.006f : 0.004f));
            _visual.localRotation = Quaternion.Euler(0f, 0f, (moving ? gait * 1.7f : Mathf.Sin(t * 2f + _phase) * 0.8f));
            if (_tail != null) _tail.Lively = moving;
            // Игровые небольшие остановки: любопытство, игра, сонливость.
            if (IsMother || !OnMesh) return;
            if (t >= _nextPause)
            {
                float pause = Index == 5 ? 2.4f : (Index == 2 ? 0.8f : Index == 3 ? 0.45f : 0.2f);
                _pausedUntil = t + pause;
                _nextPause = t + (Index == 5 ? 8.0f : 10f + Index);
            }
            if (t < _pausedUntil) Wait(true);
        }

        public void Interact()
        {
            if (!isActiveAndEnabled || !SinbinderPlayer.Exists) return;
            Herald.Line(Loc.T(Lines[Index]));
            _happiness = Mathf.Min(3f, _happiness + 1f);
            _pausedUntil = Mathf.Max(_pausedUntil, Time.time + 0.5f);
            if (_visual != null) _visual.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            if (_voice != null)
            {
                if (_squeak == null) _squeak = GenerateSqueak();
                _voice.pitch = 0.82f + 0.09f * Index;
                _voice.PlayOneShot(_squeak, 0.45f);
            }
        }

        private static AudioClip GenerateSqueak()
        {
            const int rate = 22050;
            const int samples = 3308;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Sin(Mathf.PI * i / (samples - 1));
                float tone = 710f + 260f * t / (samples / (float)rate);
                data[i] = 0.15f * envelope * envelope * Mathf.Sin(2f * Mathf.PI * tone * t);
            }
            var clip = AudioClip.Create("Крысиный писк (процедурный)", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
