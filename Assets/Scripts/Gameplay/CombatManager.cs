// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        private readonly List<Damageable> _playerUnits = new();
        private readonly List<Damageable> _enemyUnits = new();
        private readonly List<Damageable> _neutralUnits = new();
        private readonly List<HarvestableBody> _bodiesOnField = new();
        private readonly BattleEndGate _battleEndGate = new BattleEndGate();

        public System.Action<Damageable, GameObject> OnAnyDeath;
        public System.Action OnUnitsChanged;

        public List<HarvestableBody> BodiesOnField => _bodiesOnField;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Instance = null;
        }

        private void OnSceneUnloaded(Scene scene) => _battleEndGate.Reset();

        public void RegisterPlayerUnit(Damageable unit) => Register(unit, _playerUnits);
        public void RegisterEnemyUnit(Damageable unit) => Register(unit, _enemyUnits);
        public void RegisterNeutralUnit(Damageable unit) => Register(unit, _neutralUnits);

        private void Register(Damageable unit, List<Damageable> destination)
        {
            if (unit == null || unit.IsDead) return;
            if (destination.Contains(unit)
                && (destination == _playerUnits || !_playerUnits.Contains(unit))
                && (destination == _enemyUnits || !_enemyUnits.Contains(unit))
                && (destination == _neutralUnits || !_neutralUnits.Contains(unit))) return;
            RemoveFromTeams(unit);
            destination.Add(unit);
            ObserveBattleRoster();
            OnUnitsChanged?.Invoke();
        }

        private bool RemoveFromTeams(Damageable unit)
        {
            return _playerUnits.RemoveAll(u => ReferenceEquals(u, unit))
                 + _enemyUnits.RemoveAll(u => ReferenceEquals(u, unit))
                 + _neutralUnits.RemoveAll(u => ReferenceEquals(u, unit)) > 0;
        }

        public void UnregisterUnit(Damageable unit)
        {
            if (ReferenceEquals(unit, null) || !RemoveFromTeams(unit)) return;
            ObserveBattleRoster();
            OnUnitsChanged?.Invoke();
        }

        private static bool IsAlive(Damageable unit) => unit != null && !unit.IsDead;

        private void ObserveBattleRoster() =>
            _battleEndGate.RosterChanged(_playerUnits.Exists(IsAlive), _enemyUnits.Exists(IsAlive));

        public void UpdateTeam(Damageable unit, Team newTeam)
        {
            if (newTeam == Team.Player) RegisterPlayerUnit(unit);
            else if (newTeam == Team.Neutral) RegisterNeutralUnit(unit);
            else RegisterEnemyUnit(unit);
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

        internal void OnUnitKilled(Damageable killed, GameObject killer)
        {
            if (killed == null || !killed.TryReportDeath()) return;
            using var cost = Core.Cost.Of("death");
            var victim = killed.Warrior;
            bool lordDown = Lords.Active && victim != null && Lords.Of(victim) != null;
            bool playerUnit = _playerUnits.Contains(killed);
            bool enemyUnit = _enemyUnits.Contains(killed);
            bool registered = RemoveFromTeams(killed);
            if (lordDown)
                ObserveBattleRoster();
            else if (playerUnit)
                _battleEndGate.RecordDeath(true, _playerUnits.Exists(IsAlive), victim?.Id);
            else if (enemyUnit)
                _battleEndGate.RecordDeath(false, _enemyUnits.Exists(IsAlive), null);
            if (registered) OnUnitsChanged?.Invoke();

            // «Перетягивание»: спас, победа под началом Греховода (Favor); в демо — ничего.
            if (Lords.Active && killer != null)
            {
                var attacker = killer.GetComponent<Warrior>();
                Favor.Killed(victim, attacker);
                Hostility.NoteKill(victim, attacker);
            }

            // Пал Греховод («Перетягивание», автор 28.10): ни тела для жатвы, ни души — только кошель
            // на земле, и через минуту он встаёт у трона (TugMatch).
            if (lordDown)
            {
                TugMatch.LordDown(victim);
                AOS.AOSEventHub.Instance?.OnUnitDied(killed, killer, lordDown: true);
                return;
            }
            if (victim != null)
            {
                // Resolve card escrow before the fallen wallet becomes loot.
                CardTable.Instance?.CancelForDeath(victim);
                var bodyLoot = CreateLootOnField(killed);
                // Direct death path: independent of watcher Update timing.
                // The bag takes tangible belongings. If LootChain is off,
                // it also receives the body's generated treasure exactly
                // once so enemies do not leave untouchable invisible loot.
                FallenBag.CreateFor(killed, bodyLoot);
            }
            AOS.AOSEventHub.Instance?.OnUnitDied(killed, killer);
            if (OnAnyDeath != null)
                foreach (var d in OnAnyDeath.GetInvocationList())
                    using (Core.Cost.Of(Core.Cost.On ? "death>" + d.Method.DeclaringType?.Name : null))
                        ((System.Action<Damageable, GameObject>)d)(killed, killer);
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
        private HarvestableBody CreateLootOnField(Damageable killed)
        {
            var pos = killed.transform.position;

            var bodyObj = new GameObject($"Body_{killed.Warrior.DisplayName}");
            bodyObj.transform.position = pos;
            var harvestableBody = bodyObj.AddComponent<HarvestableBody>();
            harvestableBody.Initialize(killed.Warrior.Shell, killed.Warrior.Soul);
            harvestableBody.Foe = killed.Warrior.Team != Team.Player;
            _bodiesOnField.Add(harvestableBody);
            return harvestableBody;
        }

        // Resolve after attacks and death callbacks so both sides can fall in the same frame.
        void LateUpdate()
        {
            if (!_battleEndGate.TryComplete(out var result)) return;
            // In Tug, a lord can rise again and only the throne determines match defeat.
            if (Lords.Active && result.Outcome != BattleOutcome.Victory) return;

            Debug.Log($"[COMBAT] {result.Outcome}: врагов пало {result.EnemiesKilled}, своих {result.AlliesLost}.");
            AOS.AOSEventHub.Instance?.OnBattleEnd(result);

            // Цепь добычи (§41): грех решает, кто что понесёт.
            if (result.Outcome == BattleOutcome.Victory) LootChain.Share(_playerUnits, _bodiesOnField);
        }

        public int GetAlivePlayerCount() => CountAlive(_playerUnits);
        public int GetAliveEnemyCount() => CountAlive(_enemyUnits);

        private static int CountAlive(List<Damageable> units)
        {
            int count = 0;
            foreach (var unit in units)
                if (IsAlive(unit)) count++;
            return count;
        }

        public List<Damageable> GetAliveAllies() => new List<Damageable>(_playerUnits);
        public List<Damageable> GetAliveEnemies() => new List<Damageable>(_enemyUnits);
        public List<Warrior> GetAllWarriors()
        {
            List<Warrior> all = new();
            foreach (var d in _playerUnits) if (d != null && d.Warrior != null) all.Add(d.Warrior);
            foreach (var d in _enemyUnits) if (d != null && d.Warrior != null) all.Add(d.Warrior);
            foreach (var d in _neutralUnits) if (d != null && d.Warrior != null) all.Add(d.Warrior);
            return all;
        }
    }
}
