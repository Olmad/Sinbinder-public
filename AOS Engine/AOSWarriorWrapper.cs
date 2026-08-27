// Assets/_Project/Scripts/AOS/AOSWarriorWrapper.cs
using UnityEngine;
using System.Collections.Generic;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    [RequireComponent(typeof(Warrior))]
    public class AOSWarriorWrapper : MonoBehaviour
    {
        private Warrior _warrior;
        private BehaviourResolver _resolver;
        private Damageable _self;

        // Кэш спрайтов для иконок решений
        private static Dictionary<string, Sprite> _iconCache = new();

        public ActionType LastDecision { get; private set; }

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
            _self = GetComponent<Damageable>();
            _resolver = new BehaviourResolver();
        }

        public ActionType Decide()
        {
            if (_self != null && _self.IsDead) return ActionType.Idle;
            var context = CombatDecisionContext.Create(_warrior);
            LastDecision = _resolver.Decide(_warrior, context);
            return LastDecision;
        }

        public void Execute(ActionType action)
        {
            if (_self != null && _self.IsDead) return;
            ShowDecisionIcon(action);
            switch (action)
            {
                case ActionType.Attack: ExecuteAttack(); break;
                case ActionType.Flee: ExecuteFlee(); break;
                case ActionType.Loot: ExecuteLoot(); break;
                case ActionType.SaveAlly: ExecuteSaveAlly(); break;
                case ActionType.AcceptBribe:
                    if (_warrior.Team == Team.Player)
                        _warrior.Team = Team.Enemy;
                    else
                        _warrior.Team = Team.Player;
                    Debug.Log($"[AOS] {_warrior.DisplayName} принял подкуп и перешёл на сторону противника!");
                    break;
                default:
                    TryExecuteSkill(action);
                    break;
            }
        }

        private void ExecuteAttack()
        {
            var target = FindBestTarget();
            if (target == null || target.IsDead) { ExecuteLoot(); return; }
            var mover = GetComponent<UnitMover>();
            if (mover != null) mover.CommandMove(target.transform.position);
            float dist = Vector3.Distance(transform.position, target.transform.position);
            var autoAttack = GetComponent<AutoAttack>();
            if (dist <= autoAttack.AttackRange && autoAttack != null)
            {
                autoAttack.ForceAttack(target);
                // Если цель умерла от этой атаки – записываем деяние
                if (target.IsDead)
                {
                    OnKilledEnemy(target.Warrior);
                }
            }
        }

        /// <summary> Публичный метод для поиска лучшей вражеской цели. </summary>
        public Damageable FindBestTarget()
        {
            if (CombatManager.Instance == null) return null;
            var enemies = CombatManager.Instance.GetEnemies(gameObject);
            Warrior best = null;
            float bestScore = float.MinValue;
            foreach (var w in enemies)
            {
                if (w == null || w.IsDead) continue;
                float dist = Vector3.Distance(transform.position, w.transform.position);
                if (dist < 15f)
                {
                    float score = -dist + (1f - w.HP / w.MaxHP) * 50f;
                    if (score > bestScore) { bestScore = score; best = w; }
                }
            }
            return best?.GetComponent<Damageable>();
        }

        private void ExecuteFlee()
        {
            var mover = GetComponent<UnitMover>();
            if (mover != null)
            {
                Vector3 fleeDir = (transform.position - GetAverageEnemyPosition()).normalized;
                mover.CommandMove(transform.position + fleeDir * 15f);
            }
        }

        private void ExecuteLoot()
        {
            if (CombatManager.Instance == null) return;
            var bodies = CombatManager.Instance.BodiesOnField;
            HarvestableBody closest = null;
            float minDist = float.MaxValue;
            foreach (var body in bodies)
            {
                if (body == null || body.IsCollected) continue;
                float dist = Vector3.Distance(transform.position, body.transform.position);
                if (dist < minDist) { minDist = dist; closest = body; }
            }
            if (closest != null)
            {
                float dist = Vector3.Distance(transform.position, closest.transform.position);
                if (dist < 2f)
                {
                    int gold = closest.CollectGold();
                    string equip = closest.CollectEquipment();
                    closest.MarkCollected();
                    Debug.Log($"[LOOT] {_warrior.DisplayName} собрал {gold} золота и {equip ?? "ничего"}");
                    Destroy(closest.gameObject);

                    // Запись деяния за сбор добычи
                    if (gold > 0)
                    {
                        _warrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.CollectMostLoot, Importance = gold / 10f, Time = System.DateTime.Now });
                        TitleManager.UpdateTitle(_warrior);
                    }
                }
                else
                {
                    var mover = GetComponent<UnitMover>();
                    if (mover != null) mover.CommandMove(closest.transform.position);
                }
            }
        }

        private void ExecuteSaveAlly()
        {
            if (CombatManager.Instance == null) return;
            var allies = CombatManager.Instance.GetAliveAllies();
            Warrior best = null;
            float bestDist = float.MaxValue;
            foreach (var dmg in allies)
            {
                if (dmg == null || dmg.IsDead || dmg.Warrior == null || dmg.Warrior == _warrior) continue;
                var w = dmg.Warrior;
                if (w.Team != _warrior.Team) continue;
                if (w.HP < w.MaxHP * 0.5f)
                {
                    float dist = Vector3.Distance(transform.position, w.transform.position);
                    if (dist < bestDist) { bestDist = dist; best = w; }
                }
            }
            if (best != null)
            {
                var mover = GetComponent<UnitMover>();
                if (mover != null) mover.CommandMove(best.transform.position);
                AOSEventHub.Instance?.OnAllySaved(_warrior, best);
            }
        }

        private void TryExecuteSkill(ActionType action)
        {
            if (GetComponent<WrathSkills>()?.CanUseSkill(action) == true) GetComponent<WrathSkills>().ExecuteSkill(action);
            else if (GetComponent<PatienceSkills>()?.CanUseSkill(action) == true) GetComponent<PatienceSkills>().ExecuteSkill(action);
            else if (GetComponent<LustSkills>()?.CanUseSkill(action) == true) GetComponent<LustSkills>().ExecuteSkill(action);
            else if (GetComponent<GluttonySkills>()?.CanUseSkill(action) == true) GetComponent<GluttonySkills>().ExecuteSkill(action);
            else if (GetComponent<SlothSkills>()?.CanUseSkill(action) == true) GetComponent<SlothSkills>().ExecuteSkill(action);
            else if (GetComponent<DiligenceSkills>()?.CanUseSkill(action) == true) GetComponent<DiligenceSkills>().ExecuteSkill(action);
            else Execute(ActionType.Idle);
        }

        private void ShowDecisionIcon(ActionType action)
        {
            var overheadUI = GetComponentInChildren<UI.OverheadUI>();
            if (overheadUI?.DecisionIcon != null)
            {
                Sprite icon = GetIconForAction(action);
                if (icon != null) overheadUI.DecisionIcon.Show(icon, 1.5f);
            }
        }

        private Sprite GetIconForAction(ActionType action)
        {
            string path = action switch
            {
                ActionType.Attack => "Icons/Attack",
                ActionType.SaveAlly => "Icons/SaveAlly",
                ActionType.Loot => "Icons/Loot",
                ActionType.Flee => "Icons/Flee",
                _ => null
            };

            if (string.IsNullOrEmpty(path)) return null;

            if (!_iconCache.TryGetValue(path, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                _iconCache[path] = sprite;
            }

            return sprite;
        }

        private Vector3 GetAverageEnemyPosition()
        {
            if (CombatManager.Instance == null) return transform.position + Vector3.back * 10f;
            var enemies = CombatManager.Instance.GetEnemies(gameObject);
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var w in enemies)
            {
                if (w == null || w.IsDead) continue;
                sum += w.transform.position;
                count++;
            }
            return count > 0 ? sum / count : transform.position + Vector3.back * 10f;
        }

        // Запись деяния при убийстве врага
        public void OnKilledEnemy(Warrior enemy)
        {
            if (enemy == null) return;
            _warrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = 0.5f, Time = System.DateTime.Now });
            // Специальные деяния для перков
            if (_warrior.Soul.Memory?.NarrativePerks != null)
            {
                foreach (var perk in _warrior.Soul.Memory.NarrativePerks)
                {
                    if (perk.PerkName == "Мститель" && enemy.DisplayName.Contains("Бандит"))
                        _warrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = 1.5f, Time = System.DateTime.Now });
                    if (perk.PerkName == "Бывший Охотник (ненавидит)" && enemy.DisplayName.Contains("Охотник"))
                        _warrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = 2.0f, Time = System.DateTime.Now });
                }
            }
            TitleManager.UpdateTitle(_warrior);
        }
    }
}