using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Говорит телу то, что решила душа.
    ///
    /// Движок уже называет действие — <c>Flee</c>, <c>Attack</c>,
    /// <c>Loot</c>. Не хватало одного: сказать это аниматору.
    /// <see cref="Dialogue.DialogueAnimator"/> был написан месяцы назад
    /// и не сыграл ни разу, потому что <c>Animator</c>а в проекте нет
    /// ни одного — восьмой случай «написано, звена нет».
    ///
    /// Компонент висит на воине и молчит, пока аниматора нет: он
    /// появится вместе с первой моделью, и в тот же день всё заработает
    /// само. Проводка, написанная заранее, тем и хороша — её не надо
    /// вспоминать в вечер, когда модель наконец легла.
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

            Play(State());
        }

        private string State()
        {
            // Упал один раз и навсегда: мёртвый не идёт и не бьёт,
            // а перезапуск падения выглядит как судорога.
            if (_self != null && _self.IsDead) { _fell = true; return BodyMotion.Die; }
            if (_fell) return BodyMotion.Die;

            if (Dialogue.DialogueCameraController.Instance != null
                && Dialogue.DialogueCameraController.Instance.InDialogue
                && Talking()) return BodyMotion.Talk;

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

        private bool Talking()
        {
            var speaker = Dialogue.DialogueCameraController.Instance;
            return speaker != null && _warrior != null && speaker.InDialogue;
        }

        private void Play(string state)
        {
            if (state == _now) return;

            _now = state;
            _animator.Play(state);
        }
    }
}
