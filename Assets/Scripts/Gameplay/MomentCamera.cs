using System.Collections;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Dialogue;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Наезд на воина, который что-то решил сам.
    ///
    /// Автор заметил убегающего воина <b>краем глаза</b> — и это диагноз
    /// всей подаче: движок производит ровно те моменты, ради которых игра
    /// затевалась, а показать их некому. Отказ от приказа объявлен давно;
    /// поступок без приказа не объявлялся ничем.
    ///
    /// <b>Скупость здесь важнее полноты.</b> Камера, уезжающая на каждое
    /// своеволие, отнимает у игрока бой: решение принимается каждый тик,
    /// и в девятером их сотни за схватку. Поэтому тратится она только
    /// на <see cref="Notice.Scene"/> и только на <b>первый</b> случай
    /// каждого рода за бой — тем же правилом живёт тишина на первом
    /// отказе, и по той же причине: повторённое перестаёт быть событием.
    ///
    /// Слово при этом пишется всегда — журналом, без наезда. Игрок
    /// не пропустит и второй побег, просто ему не станут ради этого
    /// останавливать игру.
    ///
    /// Кадр берётся у <see cref="DialogueCameraController"/>: он уже
    /// умеет наезд, качку и возврат, и заводить второй тем же способом
    /// значило бы развести два кадра при первой же правке.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MomentCamera : MonoBehaviour
    {
        [Tooltip("Сколько держать план, прежде чем вернуть камеру.")]
        [SerializeField] private float _hold = 1.1f;

        [Tooltip("Сколько ждать между двумя наездами, даже если оба "
               + "первые в своём роде. Два подряд читаются как рывок.")]
        [SerializeField] private float _cooldown = 8f;

        [Tooltip("Снимать не советую: наезд на каждый побег превращает "
               + "бой в нарезку и перестаёт что-либо значить.")]
        [SerializeField] private bool _onlyFirstOfKind = true;

        private readonly System.Collections.Generic.HashSet<ActionType> _spent = new();
        private float _nextAllowed;
        private bool _running;

        void Start()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnSelfWill += OnSelfWill;
        }

        void OnDestroy()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnSelfWill -= OnSelfWill;
        }

        /// <summary>Новый бой — новые первые разы.</summary>
        public void Forget()
        {
            _spent.Clear();
            _nextAllowed = 0f;
        }

        private void OnSelfWill(Warrior warrior, Decision decision, DecisionContext context)
        {
            if (warrior == null || warrior.IsDead) return;
            if (Moment.Worth(decision, context) != Notice.Scene) return;

            if (_running || Time.time < _nextAllowed) return;
            if (_onlyFirstOfKind && !_spent.Add(decision.Action)) return;

            var camera = DialogueCameraController.Instance;
            if (camera == null || camera.InDialogue) return;

            StartCoroutine(Show(warrior));
        }

        private IEnumerator Show(Warrior warrior)
        {
            _running = true;
            _nextAllowed = Time.time + _cooldown;

            var camera = DialogueCameraController.Instance;
            camera.SaveCameraPosition();

            // Цель держим как Transform, а не как воина: он может лечь
            // посреди собственного плана, и обращение к уничтоженному
            // воину уронило бы наезд. Тот же урок, что стоил разговору
            // обрыва на полуфразе.
            var target = warrior.transform;

            yield return camera.FocusOn(target);
            yield return new WaitForSeconds(_hold);

            camera.StopSway();
            yield return camera.RestoreCamera();

            _running = false;
        }
    }
}
