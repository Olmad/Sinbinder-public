// Assets/Scripts/Gameplay/Engagement.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Зацепление. Вторая механика пола и самая важная во всей игре.
    ///
    /// Два бойца на дистанции удара сцеплены. Выйти из зацепления можно,
    /// но противник бьёт вслед бесплатно. Двое и больше на одном —
    /// окружение, защита падает за каждого лишнего.
    ///
    /// Почему это важнее, чем кажется: до неё подчиниться не стоило
    /// ничего, и голосование за ObeyCommand было тривиальным — не
    /// подчиниться было незачем. Когда приказ «отойди» стоит бесплатного
    /// удара, отказ становится иногда правильным, и AOS превращается
    /// из генератора капризов в генератор аргументов. Отказ, который
    /// игрок задним числом признаёт разумным, — единственный вид отказа,
    /// который не бесит.
    /// </summary>
    [RequireComponent(typeof(Warrior))]
    public class Engagement : MonoBehaviour
    {
        [SerializeField] private float _range = 2.5f;
        [Tooltip("Доля обычного урона, которую наносит удар вслед.")]
        [SerializeField] private float _freeStrikeMultiplier = 0.75f;
        [Tooltip("Штраф к защите за каждого противника сверх первого.")]
        [SerializeField] private float _defencePenaltyPerExtra = 0.15f;

        private Warrior _warrior;
        private readonly List<Damageable> _engaged = new();
        private readonly List<Damageable> _previous = new();

        /// <summary>Со сколькими противниками воин сцеплен прямо сейчас.</summary>
        public int Count => _engaged.Count;

        public bool IsEngaged => _engaged.Count > 0;
        public bool IsSurrounded => _engaged.Count >= 2;

        /// <summary>Множитель входящего урона от окружения.</summary>
        public float IncomingMultiplier =>
            1f + Mathf.Max(0, _engaged.Count - 1) * _defencePenaltyPerExtra;

        void Awake() => _warrior = GetComponent<Warrior>();

        /// <summary>Как часто пересматривать, с кем сцеплен, секунды.</summary>
        private const float Every = 0.1f;

        private float _nextLook;

        void Update()
        {
            if (_warrior == null || _warrior.IsDead) { _engaged.Clear(); return; }
            if (CombatManager.Instance == null) return;

            // Десять раз в секунду, а не каждый кадр: каждый сверялся со всеми врагами,
            // и на поле в 400 это было 80 тысяч расстояний за кадр — 60 мс (проба 27.10).
            // Шаг отхода за десятую секунды меньше дистанции удара: удар вслед не теряется.
            if (Time.time < _nextLook) return;
            _nextLook = Time.time + Every;

            _previous.Clear();
            _previous.AddRange(_engaged);
            _engaged.Clear();

            var here = transform.position;
            float reach = _range * _range;
            foreach (var dmg in CombatManager.Instance.GetEnemies(gameObject))
            {
                if (dmg == null || dmg.IsDead) continue;
                if ((dmg.transform.position - here).sqrMagnitude <= reach)
                    _engaged.Add(dmg);
            }

            // Кто был сцеплен и больше не сцеплен — тот получил право
            // ударить вслед. Уходить из ближнего боя дорого.
            foreach (var was in _previous)
            {
                if (was == null || was.IsDead) continue;
                if (_engaged.Contains(was)) continue;
                FreeStrikeFrom(was);
            }
        }

        private void FreeStrikeFrom(Damageable enemy)
        {
            var self = GetComponent<Damageable>();
            if (self == null || self.IsDead) return;

            var attack = enemy.GetComponent<AutoAttack>();
            if (attack == null) return;

            float damage = attack.AttackDamage * _freeStrikeMultiplier;
            self.TakeDamage(damage, enemy.gameObject);

            if (Core.Transparency.Shows(Core.Clarity.Trace))
                Debug.Log($"[БОЙ] {_warrior.DisplayName} вышел из ближнего боя "
                    + $"и получил удар вслед от {enemy.name}.");
        }
    }
}
