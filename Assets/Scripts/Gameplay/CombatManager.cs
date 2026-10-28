// Перевод: текст через Loc
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
        private readonly List<Damageable> _neutralUnits = new();
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

        /// <summary>Ничьи («Перетягивание», 54-TUG.md): своих и чужих у них нет — враги по отношениям.</summary>
        public void RegisterNeutralUnit(Damageable unit) { _neutralUnits.Add(unit); OnUnitsChanged?.Invoke(); }

        public void UnregisterUnit(Damageable unit)
        {
            _playerUnits.Remove(unit);
            _enemyUnits.Remove(unit);
            _neutralUnits.Remove(unit);
            OnUnitsChanged?.Invoke();
        }

        public void UpdateTeam(Damageable unit, Team newTeam)
        {
            UnregisterUnit(unit);
            if (newTeam == Team.Player) _playerUnits.Add(unit);
            else if (newTeam == Team.Neutral) _neutralUnits.Add(unit);
            else _enemyUnits.Add(unit);
            OnUnitsChanged?.Invoke();
        }

        /// <summary>
        /// Враги спросившего. Пока ничьих нет (демо) — как всегда: два списка друг против друга.
        /// С ничьими (54-TUG.md): охотник бьёт всех; ничей — кого сам считает врагом
        /// (<see cref="Hostility"/>); Греховод и его люди — охотников и тех ничьих, кто враждует с ними.
        /// Ответ ничьему держится полсекунды (<see cref="Hostility.EnemiesOf"/>): спрашивают его десятки раз за такт.
        /// </summary>
        public List<Damageable> GetEnemies(GameObject asker)
        {
            var askerDmg = asker.GetComponent<Damageable>();
            if (askerDmg == null) return new List<Damageable>();
            if (_neutralUnits.Count == 0) return _playerUnits.Contains(askerDmg) ? _enemyUnits : _playerUnits;

            if (_enemyUnits.Contains(askerDmg))
            {
                var all = new List<Damageable>(_playerUnits.Count + _neutralUnits.Count);
                all.AddRange(_playerUnits);
                all.AddRange(_neutralUnits);
                return all;
            }
            return Hostility.EnemiesOf(askerDmg, _playerUnits, _enemyUnits, _neutralUnits);
        }

        /// <summary>
        /// Свои — с точки зрения спросившего. <see cref="GetAliveAllies"/>
        /// отвечает «свои игрока» кому угодно, и охотник, спросивший его,
        /// считал своими наш отряд (14-HANDOFF §62). Ничьему свои — те, кто верней всех
        /// тому же Греховоду; никому не верен — один.
        /// </summary>
        public List<Damageable> GetAllies(GameObject asker)
        {
            var askerDmg = asker.GetComponent<Damageable>();
            if (askerDmg == null) return new List<Damageable>();
            if (_neutralUnits.Contains(askerDmg)) return Hostility.FriendsOf(askerDmg, _neutralUnits);
            return _playerUnits.Contains(askerDmg) ? _playerUnits : _enemyUnits;
        }

        public int NeutralCount { get { _neutralUnits.RemoveAll(u => u == null || u.IsDead); return _neutralUnits.Count; } }

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
            using var cost = Core.Cost.Of("death");
            UnregisterUnit(killed);
            // «Перетягивание»: спас, победа под началом Греховода (Favor); в демо — ничего.
            if (Lords.Active && killer != null)
            {
                Favor.Killed(killed.Warrior, killer.GetComponent<Warrior>());
                Hostility.NoteKill(killed.Warrior, killer.GetComponent<Warrior>());
            }

            // Пал Греховод («Перетягивание», автор 28.10): ни тела для жатвы, ни души — только кошель
            // на земле, и через минуту он встаёт у трона (TugMatch).
            if (Lords.Active && killed.Warrior != null && Lords.Of(killed.Warrior) != null)
            {
                TugMatch.LordDown(killed.Warrior);
                return;
            }
            if (killed.Warrior != null)
                CreateLootOnField(killed);
            if (OnAnyDeath != null)
                foreach (var d in OnAnyDeath.GetInvocationList())
                    using (Core.Cost.Of(Core.Cost.On ? "death>" + d.Method.DeclaringType?.Name : null))
                        ((System.Action<Damageable, GameObject>)d)(killed, killer);
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
            harvestableBody.Foe = killed.Warrior.Team != Team.Player;
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

                // Цепь добычи (§41): грех решает, кто что понесёт.
                LootChain.Share(_playerUnits, _bodiesOnField);
            }
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
            foreach (var d in _neutralUnits) if (d.Warrior != null) all.Add(d.Warrior);
            return all;
        }
    }
}