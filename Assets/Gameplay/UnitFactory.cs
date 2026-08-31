// Assets/Gameplay/UnitFactory.cs
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
            ShellType shellType = classData.requiredShell != null ? classData.requiredShell.shellType : ShellType.Skeleton;
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
            ShellType shellType = classData.requiredShell != null ? classData.requiredShell.shellType : ShellType.Skeleton;
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

        private void CreateOverheadUI(GameObject parent, Damageable damageable)
        {
            // Пробуем взять из пула
            GameObject overheadGo = OverheadUIPool.Get();

            if (overheadGo == null)
            {
                // Создаём новый, если пул пуст
                overheadGo = new GameObject("OverheadUI");
                overheadGo.transform.SetParent(parent.transform, false);
                overheadGo.transform.localPosition = new Vector3(0, 2.5f, 0);
                overheadGo.layer = LayerMask.NameToLayer("UI");

                var canvas = overheadGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;

                var canvasRect = overheadGo.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(120, 30);
                canvasRect.localScale = new Vector3(0.02f, 0.02f, 0.02f);

                // --- Slider (HealthBar) ---
                var sliderGo = new GameObject("HealthSlider");
                sliderGo.transform.SetParent(overheadGo.transform, false);
                var sliderRect = sliderGo.AddComponent<RectTransform>();
                sliderRect.anchorMin = new Vector2(0, 1);
                sliderRect.anchorMax = new Vector2(1, 1);
                sliderRect.pivot = new Vector2(0.5f, 1);
                sliderRect.sizeDelta = new Vector2(0, 20);
                sliderRect.anchoredPosition = new Vector2(0, -2);

                var slider = sliderGo.AddComponent<Slider>();
                slider.interactable = false;
                slider.minValue = 0f;
                slider.maxValue = damageable.MaxHP;
                slider.value = damageable.HP;

                // Background
                var bgGo = new GameObject("Background");
                bgGo.transform.SetParent(sliderGo.transform, false);
                var bgRect = bgGo.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                var bgImage = bgGo.AddComponent<Image>();
                bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

                // Fill Area
                var fillAreaGo = new GameObject("Fill Area");
                fillAreaGo.transform.SetParent(sliderGo.transform, false);
                var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
                fillAreaRect.anchorMin = new Vector2(0, 0);
                fillAreaRect.anchorMax = new Vector2(1, 1);
                fillAreaRect.offsetMin = Vector2.zero;
                fillAreaRect.offsetMax = Vector2.zero;

                // Fill
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(fillAreaGo.transform, false);
                var fillRect = fillGo.AddComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                var fillImage = fillGo.AddComponent<Image>();
                fillImage.color = Color.green;

                slider.fillRect = fillRect;
                slider.targetGraphic = fillImage;

                var healthBarUI = sliderGo.AddComponent<HealthBarUI>();
                healthBarUI.ManualInit(damageable, slider, fillImage, overheadGo); // ← передаём ссылку на overheadGo

                // --- Decision Icon ---
                var iconGo = new GameObject("DecisionIcon");
                iconGo.transform.SetParent(overheadGo.transform, false);
                var iconRect = iconGo.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0);
                iconRect.anchorMax = new Vector2(0.5f, 0);
                iconRect.pivot = new Vector2(0.5f, 0);
                iconRect.sizeDelta = new Vector2(48, 48);
                iconRect.anchoredPosition = new Vector2(0, -20);

                var iconImage = iconGo.AddComponent<Image>();
                var decisionIconUI = iconGo.AddComponent<DecisionIconUI>();
                decisionIconUI.SetIconImage(iconImage);

                var overheadUI = overheadGo.AddComponent<OverheadUI>();
                overheadUI.HealthBar = healthBarUI;
                overheadUI.DecisionIcon = decisionIconUI;
            }
            else
            {
                // Переиспользуем из пула
                overheadGo.transform.SetParent(parent.transform, false);
                overheadGo.transform.localPosition = new Vector3(0, 2.5f, 0);
                var overheadUI = overheadGo.GetComponent<OverheadUI>();
                if (overheadUI?.HealthBar != null)
                    overheadUI.HealthBar.ManualInit(damageable, overheadUI.HealthBar.Slider, overheadUI.HealthBar.FillImage, overheadGo);
            }
        }
    }
}