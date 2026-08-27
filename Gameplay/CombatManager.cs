using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [SerializeField] private PlayerInventory _inventory;

        private List<Damageable> _playerUnits = new();
        private List<Damageable> _enemyUnits = new();
        private List<HarvestableSoul> _soulsOnField = new();
        private List<HarvestableBody> _bodiesOnField = new();

        public System.Action<Damageable, GameObject> OnAnyDeath;
        public System.Action OnUnitsChanged;

        public List<HarvestableSoul> SoulsOnField => _soulsOnField;
        public List<HarvestableBody> BodiesOnField => _bodiesOnField;

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);
        }

        void Start()
        {
            if (_inventory == null) _inventory = FindObjectOfType<PlayerInventory>();
        }

        void Update()
        {
            for (int i = _soulsOnField.Count - 1; i >= 0; i--)
            {
                if (_soulsOnField[i] == null || _soulsOnField[i].IsHarvested)
                    _soulsOnField.RemoveAt(i);
                else if (_soulsOnField[i].Quality == SoulQuality.Dissolved)
                {
                    _soulsOnField[i].ForceDissolve();
                    _soulsOnField.RemoveAt(i);
                }
            }
        }

        public void RegisterPlayerUnit(Damageable unit) { _playerUnits.Add(unit); OnUnitsChanged?.Invoke(); }
        public void RegisterEnemyUnit(Damageable unit) { _enemyUnits.Add(unit); OnUnitsChanged?.Invoke(); }

        public void UnregisterUnit(Damageable unit)
        {
            _playerUnits.Remove(unit);
            _enemyUnits.Remove(unit);
            OnUnitsChanged?.Invoke();
        }

        public void UpdateTeam(Damageable unit, Team newTeam)
        {
            UnregisterUnit(unit);
            if (newTeam == Team.Player) _playerUnits.Add(unit);
            else _enemyUnits.Add(unit);
            OnUnitsChanged?.Invoke();
        }

        public List<Damageable> GetEnemies(GameObject asker)
        {
            var askerDmg = asker.GetComponent<Damageable>();
            if (askerDmg == null) return new List<Damageable>();
            return _playerUnits.Contains(askerDmg) ? _enemyUnits : _playerUnits;
        }

        /// <summary> Возвращает ближайшего живого врага в радиусе. </summary>
        public Damageable GetClosestEnemy(Vector3 position, float radius, GameObject asker)
        {
            var enemies = GetEnemies(asker);
            Damageable closest = null;
            float minDist = radius;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                float dist = Vector3.Distance(position, enemy.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = enemy;
                }
            }
            return closest;
        }

        public void OnUnitKilled(Damageable killed, GameObject killer)
        {
            UnregisterUnit(killed);
            if (killed.Warrior != null)
                CreateLootOnField(killed);
            OnAnyDeath?.Invoke(killed, killer);
            CheckBattleEnd();
        }

        private void CreateLootOnField(Damageable killed)
        {
            var pos = killed.transform.position;
            var soulObj = new GameObject($"Soul_{killed.Warrior.DisplayName}");
            soulObj.transform.position = pos + Vector3.up * 0.5f;
            var harvestableSoul = soulObj.AddComponent<HarvestableSoul>();
            _soulsOnField.Add(harvestableSoul);

            var bodyObj = new GameObject($"Body_{killed.Warrior.DisplayName}");
            bodyObj.transform.position = pos;
            var harvestableBody = bodyObj.AddComponent<HarvestableBody>();
            harvestableBody.Initialize(killed.Warrior.Shell, Random.Range(5, 20), Random.value < 0.3f, "Сломанный меч");
            _bodiesOnField.Add(harvestableBody);
        }

        private void CheckBattleEnd()
        {
            if (_enemyUnits.Count == 0 || !_enemyUnits.Exists(e => e != null && !e.IsDead))
            {
                int enemiesKilled = _enemyUnits.Count;
                int alliesLost = 0;
                foreach (var unit in _playerUnits)
                    if (unit == null || unit.IsDead) alliesLost++;
                Debug.Log("[COMBAT] Бой окончен! Лут остался на поле боя.");
                AOS.AOSEventHub.Instance?.OnBattleEnd(true, enemiesKilled, alliesLost);
            }
        }

        public CarriedLoot CollectLootWithSquad(List<Warrior> squad)
        {
            var loot = LootCarrySystem.DistributeLoot(squad, _bodiesOnField);
            foreach (var warrior in squad)
            {
                if (warrior.IsDead) continue;
                bool canHarvest = (warrior.Soul.Sin == SinType.Pride) || (warrior.Soul.Sin == SinType.Sloth);
                if (!canHarvest) continue;
                foreach (var soul in _soulsOnField)
                {
                    if (soul == null || soul.IsHarvested) continue;
                    var harvested = soul.Harvest(soul.Quality);
                    if (harvested != null && _inventory != null)
                        _inventory.AddItem(new InventoryItem(harvested.Name, harvested.GetFullDescription(), ItemType.Soul));
                }
            }
            return loot;
        }

        public int GetAlivePlayerCount() { _playerUnits.RemoveAll(u => u == null || u.IsDead); return _playerUnits.Count; }
        public int GetAliveEnemyCount() { _enemyUnits.RemoveAll(u => u == null || u.IsDead); return _enemyUnits.Count; }
        public List<Damageable> GetAliveAllies() => new List<Damageable>(_playerUnits);
        public List<Damageable> GetAliveEnemies() => new List<Damageable>(_enemyUnits);
        public List<Warrior> GetAllWarriors()
        {
            List<Warrior> all = new();
            foreach (var d in _playerUnits) if (d.Warrior != null) all.Add(d.Warrior);
            foreach (var d in _enemyUnits) if (d.Warrior != null) all.Add(d.Warrior);
            return all;
        }
    }
}