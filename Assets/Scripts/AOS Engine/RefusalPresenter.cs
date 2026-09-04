// Assets/Scripts/AOS Engine/RefusalPresenter.cs
using System.Collections;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Как выглядит момент отказа.
    ///
    /// Три бесплатных приёма: значок укрупняется, воин доворачивает корпус
    /// к тому, что выбрал вместо приказа, и звучит бип его греха. Ни одной
    /// анимации — анимаций в проекте нет и не будет.
    ///
    /// Компонент необязательный: без него отказ всё равно происходит,
    /// просто тише.
    /// </summary>
    [RequireComponent(typeof(Warrior))]
    public class RefusalPresenter : MonoBehaviour
    {
        [SerializeField] private float _iconScale = 1.6f;
        [SerializeField] private float _turnSpeed = 12f;
        [SerializeField] private bool _speak = true;

        private Warrior _warrior;
        private Coroutine _running;

        void Awake() => _warrior = GetComponent<Warrior>();

        public void Play(Decision decision, DecisionContext context)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Routine(decision, context));
        }

        private IEnumerator Routine(Decision decision, DecisionContext context)
        {
            if (_speak)
            {
                var voice = GetComponent<Audio.VoiceGenerator>();
                if (voice != null) voice.Speak();
            }

            var icon = GetComponentInChildren<UI.DecisionIconUI>();
            Transform iconT = icon != null ? icon.transform : null;
            Vector3 original = iconT != null ? iconT.localScale : Vector3.one;
            if (iconT != null) iconT.localScale = original * _iconScale;

            Vector3 look = LookTarget(decision, context);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;

                if (look != Vector3.zero)
                {
                    Vector3 dir = look - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(dir), Time.deltaTime * _turnSpeed);
                }

                yield return null;
            }

            if (iconT != null) iconT.localScale = original;
            _running = null;
        }

        /// <summary>Куда смотреть: на то, что воин выбрал вместо приказа.</summary>
        private Vector3 LookTarget(Decision decision, DecisionContext context)
        {
            switch (decision.Action)
            {
                case ActionType.SaveAlly:
                    return context?.TargetWarrior != null
                        ? context.TargetWarrior.transform.position : Vector3.zero;

                case ActionType.Flee:
                    // Отвернуться от приказа.
                    return _warrior.Command.IsSet
                        ? transform.position - (_warrior.Command.Point - transform.position)
                        : Vector3.zero;

                default:
                    return Vector3.zero;
            }
        }
    }
}
