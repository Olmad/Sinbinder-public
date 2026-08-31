// Assets/_Project/Scripts/AOS/AutoAttackAOS.cs
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
        private float _lastDecisionTime;
        private bool _pausing;

        void Awake()
        {
            _wrapper = GetComponent<AOSWarriorWrapper>();
            _presenter = GetComponent<RefusalPresenter>();
        }

        void Update()
        {
            if (_pausing) return;
            if (Time.time - _lastDecisionTime < _decisionInterval) return;
            _lastDecisionTime = Time.time;

            var action = _wrapper.Decide();

            // Отказ исполняется не сразу. Без этой задержки значок
            // намерения появляется и тонет в бою: игрок физически
            // не успевает связать приказ, поступок и причину.
            if (_wrapper.LastDecisionDetail.RefusedCommand && _refusalPause > 0f)
                StartCoroutine(ExecuteAfterBeat(action));
            else
                _wrapper.Execute(action);
        }

        private IEnumerator ExecuteAfterBeat(ActionType action)
        {
            _pausing = true;

            if (_presenter != null)
                _presenter.Play(_wrapper.LastDecisionDetail, _wrapper.LastContext);

            yield return new WaitForSeconds(_refusalPause);

            _pausing = false;
            _wrapper.Execute(action);
        }
    }
}
