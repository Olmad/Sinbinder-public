using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Говорит телу то, что решила душа.
    ///
    /// Движок уже называет действие — <c>Flee</c>, <c>Attack</c>,
    /// <c>Loot</c>. Не хватало одного: сказать это аниматору.
    /// <c>DialogueAnimator</c> был написан месяцы назад и не сыграл
    /// ни разу, потому что <c>Animator</c>а в проекте не было ни одного —
    /// восьмой случай «написано, звена нет».
    ///
    /// <b>18 сентября аниматоры появились</b>: контроллеры пересобраны,
    /// <c>Talk</c> и <c>Attack</c> перестали быть покоем. Проводка,
    /// написанная заранее, тем и хороша — вспоминать её в тот вечер
    /// не пришлось.
    ///
    /// Тогда же <c>DialogueAnimator</c> снят совсем: он остался никем
    /// не повешенным, а повесить его рядом значило бы завести **второго
    /// хозяина одного `Animator`** — тот же вид беды, что утром того же
    /// дня развели у камеры. Хозяин тела один, и это здесь.
    ///
    /// Состояние меняем только когда оно сменилось: <c>Animator.Play</c>
    /// каждый кадр перезапускает анимацию с нуля, и воин дёргается
    /// на месте вместо того, чтобы идти.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class WarriorAnimation : MonoBehaviour
    {
        private Animator _animator;
        private Warrior _warrior;
        private Damageable _self;
        private AOSWarriorWrapper _mind;

        private string _now;
        private bool _fell;
        private bool _talking;

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
            _self = GetComponent<Damageable>();
            _mind = GetComponent<AOSWarriorWrapper>();
        }

        void Update()
        {
            // Аниматор ищем каждый раз, пока не нашли: модель может
            // появиться позже нас — воина собирают телом вперёд,
            // а бывает и наоборот.
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            HushIfSilent();
            Play(State());
        }

        private string State()
        {
            // Упал один раз и навсегда: мёртвый не идёт и не бьёт,
            // а перезапуск падения выглядит как судорога.
            if (_self != null && _self.IsDead) { _fell = true; return BodyMotion.Die; }
            if (_fell) return BodyMotion.Die;

            if (_talking) return BodyMotion.Talk;

            // Ноги важнее решения: воин, решивший «бить», но ещё идущий
            // к цели, на экране идёт. Решение станет ударом, когда он
            // дойдёт, и спорить с ногами здесь нельзя — игрок смотрит
            // на ноги, а не на бюллетень.
            if (Walking()) return BodyMotion.Walk;

            return _mind != null ? BodyMotion.For(_mind.LastDecision) : BodyMotion.Idle;
        }

        private bool Walking()
        {
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null || !agent.isOnNavMesh) return false;

            return agent.velocity.sqrMagnitude > 0.04f;
        }

        /// <summary>
        /// Говорит ли <b>этот</b> воин прямо сейчас.
        ///
        /// Здесь стояло «идёт ли разговор вообще»: условие спрашивало
        /// <c>InDialogue</c> дважды и **ни разу — кто говорящий**.
        /// На совете это значило, что рот шевелят все девятеро разом,
        /// а разговор в прологе — его хребет.
        ///
        /// Ставит флаг тот, кто заставляет говорить: <c>DialogueUI</c>
        /// гасит его у всех и зажигает у говорящего — эта проводка была
        /// верной с самого начала, только указывала на
        /// <c>DialogueAnimator</c>, которого никто не вешал и который
        /// дрался бы с нами за один <c>Animator</c>. Хозяин тела один,
        /// и это мы.
        /// </summary>
        public void Talk(bool on) => _talking = on;

        /// <summary>
        /// Страховка от застрявшего рта: разговор мог оборваться
        /// смертью говорящего или сменой сцены, и гасить флаг стало бы
        /// некому.
        /// </summary>
        private void HushIfSilent()
        {
            if (!_talking) return;

            var camera = Dialogue.DialogueCameraController.Instance;
            if (camera == null || !camera.InDialogue) _talking = false;
        }

        private void Play(string state)
        {
            if (state == _now) return;

            _now = state;
            _animator.Play(state);
        }
    }
}
