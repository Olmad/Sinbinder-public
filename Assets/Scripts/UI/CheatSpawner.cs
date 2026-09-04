// Assets/Scripts/UI/CheatSpawner.cs
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    public class CheatSpawner : MonoBehaviour
    {
        [SerializeField] private UnitFactory _factory;

        private Core.RelationshipSystem _relSystem;
        private bool _spawnAsEnemy = false;

        void Start()
        {
            // Инициализатор поля отработал бы до Awake процессора памяти
            // и навсегда захватил бы null, молча выключив отношения.
            _relSystem = new Core.RelationshipSystem(AOS.MemoryProcessor.Instance);

            if (_factory == null)
                _factory = FindFirstObjectByType<UnitFactory>();

            Debug.Log($"[CHEAT] Режим спавна: {(_spawnAsEnemy ? "ВРАГ" : "СОЮЗНИК")}. Нажми 0 для переключения. Нажми 5 для нанесения урона союзникам.");

            // Проверка иконок
            var test = Resources.Load<Sprite>("Icons/Attack");
            Debug.Log(test != null ? "[CHEAT] ICON FOUND" : "[CHEAT] ICON MISSING");
        }

        void Update()
        {
            // Переключение команды
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                _spawnAsEnemy = !_spawnAsEnemy;
                Debug.Log($"[CHEAT] Режим спавна переключён: {(_spawnAsEnemy ? "ВРАГ" : "СОЮЗНИК")}");
            }

            // Спавн воинов
            if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnAtMouse(SinType.Greed, MoralType.Vicious, ShellType.Skeleton, "Жадный");
            if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnAtMouse(SinType.Pride, MoralType.Pious, ShellType.Zombie, "Гордый");
            if (Input.GetKeyDown(KeyCode.Alpha3)) SpawnAtMouse(SinType.Wrath, MoralType.Vicious, ShellType.Golem, "Гневный");
            if (Input.GetKeyDown(KeyCode.Alpha4)) SpawnAtMouse(SinType.Sloth, MoralType.Neutral, ShellType.Ghost, "Унылый");

            // Нанесение урона всем союзникам
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                var allWarriors = FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID);
                foreach (var w in allWarriors)
                {
                    if (w.Team == Team.Player && !w.IsDead)
                    {
                        var damageable = w.GetComponent<Damageable>();
                        if (damageable != null)
                        {
                            damageable.TakeDamage(15, gameObject);
                            Debug.Log($"[CHEAT] Нанесено 15 урона {w.DisplayName}. Текущее HP: {damageable.HP}");
                        }
                    }
                }
            }
        }

        void SpawnAtMouse(SinType sin, MoralType moral, ShellType shell, string label)
        {
            if (_factory == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 spawnPoint = hit.point + Vector3.up * 1.5f;
                var soul = new SoulData(label, sin, moral, 1, 50f);
                Warrior warrior;

                if (_spawnAsEnemy)
                    warrior = _factory.SpawnEnemyUnit(soul, shell, _relSystem, spawnPoint, true);
                else
                    warrior = _factory.SpawnPlayerUnit(soul, shell, _relSystem, spawnPoint, true);

                if (warrior != null)
                {
                    if (warrior.GetComponent<AOS.AOSWarriorWrapper>() == null)
                        warrior.gameObject.AddComponent<AOS.AOSWarriorWrapper>();
                    if (warrior.GetComponent<AOS.AutoAttackAOS>() == null)
                        warrior.gameObject.AddComponent<AOS.AutoAttackAOS>();

                    var oldAttack = warrior.GetComponent<AutoAttack>();
                    if (oldAttack != null) oldAttack.enabled = false;
                }

                Debug.Log($"[CHEAT] Заспавнен {label} ({sin}, {moral}) как {(_spawnAsEnemy ? "ВРАГ" : "СОЮЗНИК")} в {spawnPoint}");
            }
        }
    }
}