// Assets/AOS Engine/AOSWarriorWrapper.cs
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

        /// <summary>Последнее решение вместе с причиной. Читают подсказка и журнал.</summary>
        public Decision LastDecisionDetail { get; private set; }

        /// <summary>Контекст последнего решения. Нужен генератору фраз.</summary>
        public DecisionContext LastContext { get; private set; }

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
            _self = GetComponent<Damageable>();
            _resolver = new BehaviourResolver();
        }

        public ActionType Decide()
        {
            if (_self != null && _self.IsDead) return ActionType.Idle;

            // Приказ уезжает в контекст. Без второго аргумента HasCommand
            // всегда оставался false, LoyaltyModule возвращал ноль,
            // и ObeyCommand не мог победить ни при каких условиях.
            var cmd = _warrior.Command;
            var context = CombatDecisionContext.Create(_warrior, cmd.TypeName);

            var decision = _resolver.DecideDetailed(_warrior, context);

            LastContext = context;
            LastDecisionDetail = decision;
            LastDecision = decision.Action;

            if (decision.RefusedCommand)
                AOSEventHub.Instance?.OnCommandRefused(_warrior, decision, context);

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
                case ActionType.ObeyCommand: ExecuteCommand(); break;
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

        /// <summary>
        /// Исполнение того, что велел игрок. Ветки не было вовсе:
        /// ObeyCommand проваливался в default, оттуда в TryExecuteSkill,
        /// не находил подходящего умения и вырождался в Idle.
        /// То есть подчинение исполнялось как «стоять на месте».
        /// </summary>
        private void ExecuteCommand()
        {
            var cmd = _warrior.Command;
            if (!cmd.IsSet) { Execute(ActionType.Idle); return; }

            var mover = GetComponent<UnitMover>();

            switch (cmd.Kind)
            {
                case CommandKind.Attack:
                    if (cmd.Target != null)
                    {
                        var dmg = cmd.Target.GetComponent<Damageable>();
                        if (dmg != null && !dmg.IsDead)
                        {
                            if (mover != null) mover.CommandMove(cmd.Target.transform.position);

                            var autoAttack = GetComponent<AutoAttack>();
                            if (autoAttack != null
                                && Vector3.Distance(transform.position, cmd.Target.transform.position) <= autoAttack.AttackRange)
                            {
                                autoAttack.ForceAttack(dmg);
                                if (dmg.IsDead) OnKilledEnemy(dmg.Warrior);
                            }
                            return;
                        }
                    }
                    // Цель мертва или исчезла: приказ выполнен, снимаем.
                    _warrior.ClearCommand();
                    break;

                case CommandKind.Move:
                    if (mover != null) mover.CommandMove(cmd.Point);
                    // Дошёл — приказ исчерпан.
                    if (Vector3.Distance(transform.position, cmd.Point) < 1.5f)
                        _warrior.ClearCommand();
                    break;

                case CommandKind.Hold:
                    if (mover != null) mover.Stop();
                    break;

                case CommandKind.Defend:
                    if (mover != null) mover.Stop();
                    // Оборона: бить только того, кто подошёл сам.
                    var near = FindBestTarget();
                    var aa = GetComponent<AutoAttack>();
                    if (near != null && aa != null
                        && Vector3.Distance(transform.position, near.transform.position) <= aa.AttackRange)
                    {
                        aa.ForceAttack(near);
                        if (near.IsDead) OnKilledEnemy(near.Warrior);
                    }
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

        /// <summary>
        /// Исполнение умения. Раньше здесь была лестница из шести
        /// GetComponent с перечислением конкретных классов: добавить набор
        /// умений значило не забыть дописать сюда ещё одну ветку.
        /// Теперь спрашиваются все наборы, какие на воине есть.
        /// </summary>
        private void TryExecuteSkill(ActionType action)
        {
            foreach (var set in GetComponents<ISkillSet>())
            {
                if (set == null || !set.CanUseSkill(action)) continue;
                set.ExecuteSkill(action);
                return;
            }

            // Умения нет или оно на откате — воин стоит, но не проваливается
            // обратно в Execute: это была бы рекурсия.
            ShowDecisionIcon(ActionType.Idle);
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