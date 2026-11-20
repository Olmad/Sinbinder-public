// Assets/Scripts/AOS Engine/AutoAttackAOS.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Такт решения: раз в секунду воин смотрит на мир и выбирает,
    /// что делать. Здесь же живёт микропауза отказа.
    /// </summary>
    [RequireComponent(typeof(AOSWarriorWrapper))]
    public class AutoAttackAOS : MonoBehaviour
    {
        [SerializeField] private float _decisionInterval = 1f;

        [Header("Момент отказа")]
        [Tooltip("Задержка между решением ослушаться и его исполнением. "
               + "В эти доли секунды игрок успевает заметить значок. "
               + "Ноль — выключить.")]
        [SerializeField] private float _refusalPause = 0.4f;

        private AOSWarriorWrapper _wrapper;
        private RefusalPresenter _presenter;
        private Gameplay.Damageable _self;
        private float _lastDecisionTime;
        private bool _pausing;
        private bool _hasPresentedRefusal;
        private uint _lastPresentedRefusalOrder;

        void Awake()
        {
            _wrapper = GetComponent<AOSWarriorWrapper>();
            _presenter = GetComponent<RefusalPresenter>();
            _self = GetComponent<Gameplay.Damageable>();

            // Свой такт у каждого: все думали в один и тот же кадр раз в секунду,
            // и на поле в 400 этот кадр стоил секунду и больше (замер 27.10). Сдвиг —
            // по имени объекта, без жребия: те же воины думают в те же доли секунды.
            int stamp = 0;
            foreach (char c in gameObject.name) stamp = (stamp * 31 + c) & 0x7fffffff;
            _lastDecisionTime = Time.time - (stamp % 1000) / 1000f * _decisionInterval;
        }

        void OnDisable()
        {
            if (!_pausing) return;
            StopAllCoroutines();
            _pausing = false;
            _lastDecisionTime = Time.time - _decisionInterval;
        }

        void Update()
        {
            // Павший не решает. Decide у мёртвого и так отвечает «ничего»,
            // но ниже читается <b>прошлое</b> решение: кто умер, отказываясь
            // от приказа, тот раз в секунду снова проигрывал свой отказ —
            // реплика над трупом (автор: «мёртвые воины говорят»).
            if (_self != null && _self.IsDead) return;

            if (_pausing || Time.deltaTime <= 0f) return;
            if (Time.time - _lastDecisionTime < _decisionInterval) return;
            _lastDecisionTime = Time.time;

            using (Core.Cost.Of("decide")) _wrapper.Decide();
            var context = _wrapper.LastContext;

            // Отказ исполняется не сразу. Без этой задержки значок
            // намерения появляется и тонет в бою: игрок физически
            // не успевает связать приказ, поступок и причину.
            uint decidedForOrder = _wrapper.LastDecisionCommandRevision;
            if (_wrapper.HasCurrentCommandDecision && _wrapper.LastDecisionDetail.RefusedCommand &&
                _refusalPause > 0f &&
                OrderIntegrity.ShouldPresentRefusal(
                    _hasPresentedRefusal, _lastPresentedRefusalOrder, decidedForOrder))
            {
                _hasPresentedRefusal = true;
                _lastPresentedRefusalOrder = decidedForOrder;
                StartCoroutine(ExecuteAfterBeat(context));
            }
            else
                ExecuteOrRetry(context);
        }

        private IEnumerator ExecuteAfterBeat(DecisionContext context)
        {
            _pausing = true;

            if (_presenter != null)
                _presenter.Play(_wrapper.LastDecisionDetail, context);

            yield return new WaitForSeconds(_refusalPause);

            _pausing = false;
            ExecuteOrRetry(context);
        }

        private void ExecuteOrRetry(DecisionContext context)
        {
            using var cost = Core.Cost.Of("execute");
            if (_wrapper.TryExecuteDecision(context)) return;

            // После смены приказа или уговора спросить душу в ближайший игровой кадр.
            _lastDecisionTime = Time.time - _decisionInterval;
        }
    }
}
