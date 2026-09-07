// Assets/Scripts/Gameplay/UnitFactory.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Sinbinder.UI;
using Sinbinder.Core;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    public class UnitFactory : MonoBehaviour
    {
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private GameObject _enemyPrefab;

        // ──────────────────────────────────────────────
        // Старые методы (оставлены для совместимости)
        // ──────────────────────────────────────────────

        public Warrior SpawnPlayerUnit(SoulData soul, ShellType shell, RelationshipSystem relSystem, Vector3 position, bool isCommander = false)
        {
            var go = Instantiate(_unitPrefab, position, Quaternion.identity);
            SetupUnit(go, soul, shell, relSystem, Team.Player, isCommander);
            return go.GetComponent<Warrior>();
        }

        public Warrior SpawnEnemyUnit(SoulData soul, ShellType shell, RelationshipSystem relSystem, Vector3 position, bool isCommander = false)
        {
            var prefab = _enemyPrefab != null ? _enemyPrefab : _unitPrefab;
            var go = Instantiate(prefab, position, Quaternion.identity);
            SetupUnit(go, soul, shell, relSystem, Team.Enemy, isCommander);
            return go.GetComponent<Warrior>();
        }

        // ──────────────────────────────────────────────
        // Новые методы (используют ClassData / ShellData)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Создаёт союзного воина на основе класса (ClassData).
        /// Класс уже содержит ссылку на оболочку (ShellData).
        /// </summary>
        public Warrior SpawnPlayerUnit(SoulData soul, ClassData classData, Vector3 position, bool isCommander = false)
        {
            var go = Instantiate(_unitPrefab, position, Quaternion.identity);
            // Извлекаем ShellType из ClassData (временный мост)
            ShellType shellType = classData.requiredShell != null ? classData.requiredShell.type : ShellType.Skeleton;
            SetupUnit(go, soul, shellType, null, Team.Player, isCommander);

            // Применяем параметры класса поверх базовых
            var warrior = go.GetComponent<Warrior>();
            ApplyClassData(warrior, classData);
            return warrior;
        }

        /// <summary>
        /// Создаёт вражеского воина на основе класса.
        /// </summary>
        public Warrior SpawnEnemyUnit(SoulData soul, ClassData classData, Vector3 position, bool isCommander = false)
        {
            var prefab = _enemyPrefab != null ? _enemyPrefab : _unitPrefab;
            var go = Instantiate(prefab, position, Quaternion.identity);
            ShellType shellType = classData.requiredShell != null ? classData.requiredShell.type : ShellType.Skeleton;
            SetupUnit(go, soul, shellType, null, Team.Enemy, isCommander);

            var warrior = go.GetComponent<Warrior>();
            ApplyClassData(warrior, classData);
            return warrior;
        }

        // ──────────────────────────────────────────────
        // Внутренние методы
        // ──────────────────────────────────────────────

        /// <summary>
        /// Применяет бонусы класса к уже созданному воину.
        /// </summary>
        private void ApplyClassData(Warrior warrior, ClassData classData)
        {
            if (classData == null) return;

            // Модификаторы атаки и защиты
            warrior.Attack += classData.attackModifier;
            warrior.Defense += classData.defenseModifier;

            // Выдаём навыки класса (если у воина есть SkillManager)
            var skillManager = warrior.GetComponent<SkillManager>();
            if (skillManager != null && classData.classSkills != null)
            {
                foreach (var skill in classData.classSkills)
                    skillManager.AddSkill(skill);
            }

            Debug.Log($"[FACTORY] {warrior.DisplayName} получил класс {classData.className}");
        }

        private void SetupUnit(GameObject go, SoulData soul, ShellType shell, RelationshipSystem relSystem, Team team, bool isCommander)
        {
            var warrior = go.GetComponent<Warrior>();
            if (warrior == null) warrior = go.AddComponent<Warrior>();

            var damageable = go.GetComponent<Damageable>();
            if (damageable == null) damageable = go.AddComponent<Damageable>();

            var autoAttack = go.GetComponent<AutoAttack>();
            if (autoAttack == null) autoAttack = go.AddComponent<AutoAttack>();

            var mover = go.GetComponent<UnitMover>();
            if (mover == null) mover = go.AddComponent<UnitMover>();

            var selection = go.GetComponent<SelectionComponent>();
            if (selection == null) selection = go.AddComponent<SelectionComponent>();

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            warrior.Initialize(soul, shell, relSystem, isCommander, team);
            damageable.Initialize(20f + soul.Level * 10f, 1f + soul.Level);
            autoAttack.Initialize(3f + soul.Level * 2f, 2f, 1f);

            CreateOverheadUI(go, damageable);

            if (team == Team.Enemy)
            {
                if (CombatManager.Instance != null)
                    CombatManager.Instance.RegisterEnemyUnit(damageable);
                go.name = $"[ВРАГ] {soul.Name}";
            }
            else
            {
                if (CombatManager.Instance != null)
                    CombatManager.Instance.RegisterPlayerUnit(damageable);
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.RegisterUnit(selection);
                go.name = $"[СОЮЗ] {soul.Name}";
            }
        }

        /// <summary>
        /// Сборка вынесена в <see cref="UI.OverheadBuilder"/>: тем же
        /// надголовным интерфейсом пользуются спавнеры пролога, а две
        /// сборки одного и того же разошлись бы при первой правке.
        /// </summary>
        private void CreateOverheadUI(GameObject parent, Damageable damageable)
            => UI.OverheadBuilder.Attach(parent, damageable);
    }
}