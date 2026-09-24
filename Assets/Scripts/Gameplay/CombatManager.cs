using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        private List<Damageable> _playerUnits = new();
        private List<Damageable> _enemyUnits = new();
        private List<HarvestableBody> _bodiesOnField = new();

        public System.Action<Damageable, GameObject> OnAnyDeath;
        public System.Action OnUnitsChanged;

        public List<HarvestableBody> BodiesOnField => _bodiesOnField;

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);
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

        /// <summary>
        /// Свои — с точки зрения спросившего. <see cref="GetAliveAllies"/>
        /// отвечает «свои игрока» кому угодно, и охотник, спросивший его,
        /// считал своими наш отряд (14-HANDOFF §62).
        /// </summary>
        public List<Damageable> GetAllies(GameObject asker)
        {
            var askerDmg = asker.GetComponent<Damageable>();
            if (askerDmg == null) return new List<Damageable>();
            return _playerUnits.Contains(askerDmg) ? _playerUnits : _enemyUnits;
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

        /// <summary>
        /// Что остаётся на поле от убитого. <b>Только тело.</b>
        ///
        /// Здесь же заводилась вторая душа — <c>HarvestableSoul</c>,
        /// на каждой смерти, невидимая и с незаполненным полем
        /// <c>_soul</c>: компонент вешали, а <c>Initialize</c> у него
        /// не звали никогда. Снята 18 сентября, разбор — 14-HANDOFF §43.
        ///
        /// Настоящая душа заводится строкой ниже по стеку:
        /// <c>OnAnyDeath</c> → <see cref="SoulManager.StartSoulFade"/>,
        /// и она же единственная, у которой есть огонёк, срок и цена
        /// промедления.
        /// </summary>
        private void CreateLootOnField(Damageable killed)
        {
            var pos = killed.transform.position;

            var bodyObj = new GameObject($"Body_{killed.Warrior.DisplayName}");
            bodyObj.transform.position = pos;
            var harvestableBody = bodyObj.AddComponent<HarvestableBody>();
            harvestableBody.Initialize(killed.Warrior.Shell, killed.Warrior.Soul);
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

        /// <summary>
        /// Раздать добычу отряду: грех решает, кто что понесёт
        /// (<see cref="LootCarrySystem.DistributeLoot"/>).
        ///
        /// <b>Пока не вызывается ниоткуда</b> — разбор цепи и то, чего
        /// ей не хватает, в 14-HANDOFF §41.
        ///
        /// Здесь же стояла жатва душ прямо на поле, и только воинами
        /// с грехом Гордыня или Уныние. Снята вместе со второй системой
        /// душ (§43): души в этой игре жнёт Греховод, а не отряд,
        /// и жнёт он их через <see cref="SoulManager"/>.
        /// </summary>
        public CarriedLoot CollectLootWithSquad(List<Warrior> squad)
            => LootCarrySystem.DistributeLoot(squad, _bodiesOnField);

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