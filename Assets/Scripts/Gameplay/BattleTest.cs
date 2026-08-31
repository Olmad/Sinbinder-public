using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class BattleTest : MonoBehaviour
    {
        [SerializeField] private UnitFactory _factory;

        private Core.RelationshipSystem _relSystem;

        void Awake()
        {
            Invoke(nameof(SpawnUnits), 0.2f);
        }

        void SpawnUnits()
        {
            // Отношения вычисляются из общей памяти, поэтому системе нужен
            // MemoryProcessor. Его синглтон готов только после всех Awake —
            // отсюда создание здесь, а не в Awake.
            _relSystem = new Core.RelationshipSystem(AOS.MemoryProcessor.Instance);

            if (_factory == null)
            {
                Debug.LogError("[BATTLETEST] UnitFactory не назначен!");
                return;
            }

            // ГГ — командир
            var heroSoul = new Core.SoulData("Греховод", Core.SinType.Pride, Core.MoralType.Neutral, 3, sinIntensity: 20f);
            var hero = _factory.SpawnPlayerUnit(heroSoul, Core.ShellType.Ghost, _relSystem, new Vector3(0, 0, 0), true);

            if (hero != null)
            {
                var harvester = hero.GetComponent<SoulHarvester>();
                if (harvester == null)
                    harvester = hero.gameObject.AddComponent<SoulHarvester>();
            }

            // Солдат — командир
            var soldierSoul = new Core.SoulData("Солдат-Зомби", Core.SinType.Wrath, Core.MoralType.Vicious, 1, sinIntensity: 60f);
            _factory.SpawnPlayerUnit(soldierSoul, Core.ShellType.Zombie, _relSystem, new Vector3(2, 0, 0), true);

            // Скелет — НЕ командир (рядовой)
            var greedSoul = new Core.SoulData("Жадный Скелет", Core.SinType.Greed, Core.MoralType.Vicious, 2, sinIntensity: -30f);
            _factory.SpawnPlayerUnit(greedSoul, Core.ShellType.Skeleton, _relSystem, new Vector3(-2, 0, 0), false);

            // Враги — командиры
            for (int i = 0; i < 2; i++)
            {
                var enemySoul = new Core.SoulData($"Враг {i + 1}", Core.SinType.Wrath, Core.MoralType.Vicious, 1, sinIntensity: 80f);
                _factory.SpawnEnemyUnit(enemySoul, Core.ShellType.Golem, _relSystem, new Vector3(5 + i * 2, 0, 2), true);
            }
        }
    }
}