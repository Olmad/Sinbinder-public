// Assets/Scripts/Gameplay/Fatigue.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Запас сил. Первая механика пола.
    ///
    /// Всё, что воин делает, стоит сил; силы возвращаются только когда он
    /// стоит. Уставший бьёт слабее и промахивается чаще. Смысл не в цифрах:
    /// усталость превращает время в ресурс, и у боя появляется дуга вместо
    /// обмена ударами до нуля.
    ///
    /// И она же кормит характеры: уныние охотнее отдыхает, гордыня
    /// отказывается признать, что устала, гнев тратит запас безрассудно.
    /// Механика пола, которую читают модули, — это и есть правило пола.
    /// </summary>
    [RequireComponent(typeof(Warrior))]
    public class Fatigue : MonoBehaviour
    {
        [SerializeField] private float _max = 100f;
        [SerializeField] private float _current = 100f;

        [Header("Расход")]
        [SerializeField] private float _perAttack = 12f;
        [SerializeField] private float _perSkill = 25f;
        [SerializeField] private float _movePerSecond = 6f;

        [Header("Восстановление")]
        [Tooltip("В секунду, когда воин стоит на месте.")]
        [SerializeField] private float _restPerSecond = 9f;
        [Tooltip("В секунду, когда воин обороняется.")]
        [SerializeField] private float _defendPerSecond = 14f;

        [Header("Порог измождения")]
        [SerializeField] private float _exhaustedBelow = 0.3f;

        private UnitMover _mover;
        private Warrior _warrior;

        public float Max => _max;
        public float Current => _current;

        /// <summary>Сколько сил осталось, 0…1.</summary>
        public float Ratio => _max > 0f ? Mathf.Clamp01(_current / _max) : 0f;

        /// <summary>Насколько воин вымотан, 0…1. Это читают модули.</summary>
        public float Spent => 1f - Ratio;

        public bool IsExhausted => Ratio < _exhaustedBelow;

        /// <summary>Множитель урона и точности от усталости, 0.5…1.</summary>
        public float Effectiveness => Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0f, _exhaustedBelow, Ratio));

        void Awake()
        {
            _mover = GetComponent<UnitMover>();
            _warrior = GetComponent<Warrior>();
            _current = _max;
        }

        void Update()
        {
            if (_warrior != null && _warrior.IsDead) return;

            bool moving = _mover != null && _mover.IsMoving;
            if (moving)
            {
                _current -= _movePerSecond * Time.deltaTime;
            }
            else
            {
                bool defending = _warrior != null
                    && _warrior.Command.Kind == CommandKind.Defend;
                _current += (defending ? _defendPerSecond : _restPerSecond) * Time.deltaTime;
            }

            _current = Mathf.Clamp(_current, 0f, _max);
        }

        public void SpendForAttack() => Spend(_perAttack);
        public void SpendForSkill() => Spend(_perSkill);

        public void Spend(float amount)
        {
            _current = Mathf.Clamp(_current - Mathf.Abs(amount), 0f, _max);
        }

        public void Restore(float amount)
        {
            _current = Mathf.Clamp(_current + Mathf.Abs(amount), 0f, _max);
        }
    }
}
