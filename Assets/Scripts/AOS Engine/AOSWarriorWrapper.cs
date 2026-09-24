// Assets/Scripts/AOS Engine/AOSWarriorWrapper.cs
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
            _goal = GetComponent<HunterGoal>();
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

            // Смена действия — до присваивания: объявлять надо начало
            // поступка, а не каждый тик, пока он длится. Бежать можно
            // полминуты, и «сбегает» полминуты подряд — это не событие,
            // а мигающая надпись.
            bool changed = decision.Action != LastDecision;

            LastContext = context;
            LastDecisionDetail = decision;
            LastDecision = decision.Action;

            if (decision.RefusedCommand)
                AOSEventHub.Instance?.OnCommandRefused(_warrior, decision, context);
            else if (changed)
                AOSEventHub.Instance?.OnSelfWilled(_warrior, decision, context);

            return LastDecision;
        }

        public void Execute(ActionType action)
        {
            if (_self != null && _self.IsDead) return;

            // Охотник без врага рядом идёт к своей цели, что бы ни выбрал голос
            // (HunterGoal): первая волна — к костру, вторая — по следу
            // Инквизитора. Рядом кто-то есть — решает голос, как у всех.
            if (Hunting(action, out var goal))
            {
                ShowDecisionIcon(ActionType.Attack);
                var legs = GetComponent<UnitMover>();
                if (legs != null) legs.CommandMove(goal);
                return;
            }

            // Жизнь в лагере (docs/32-CAMP.md): решил «стоять» — стоит там,
            // куда тянет душа. Голос решил что, лагерь — где.
            if (action == ActionType.Idle && CampLife.Where(_warrior, out var spot, out _))
            {
                ShowDecisionIcon(action);
                var legs = GetComponent<UnitMover>();
                if (legs != null) legs.CommandMove(spot);
                return;
            }

            ShowDecisionIcon(action);
            switch (action)
            {
                case ActionType.Attack: ExecuteAttack(); break;
                case ActionType.Flee: ExecuteFlee(); break;
                case ActionType.Loot: ExecuteLoot(); break;
                case ActionType.SaveAlly: ExecuteSaveAlly(); break;
                case ActionType.ObeyCommand: ExecuteCommand(); break;
                case ActionType.AcceptBribe:
                    bool wasOurs = _warrior.Team == Team.Player;
                    _warrior.Team = wasOurs ? Team.Enemy : Team.Player;

                    // Поля мало. CombatManager решает, кто кому враг,
                    // по спискам, а не по полю Team: GetEnemies спрашивает
                    // «в каком ты списке». До 17 сентября перебежчик менял
                    // только надпись — оставался в списке своих, продолжал
                    // бить своих новых друзей, а конец боя считал его нашим.
                    // UpdateTeam для этого и написан, и не звался ниоткуда
                    // (правило orphans, 11-MISSING.md §5).
                    var body = GetComponent<Damageable>();
                    if (body != null && CombatManager.Instance != null)
                        CombatManager.Instance.UpdateTeam(body, _warrior.Team);

                    Debug.Log($"[AOS] {_warrior.DisplayName} принял подкуп и перешёл на сторону противника!");

                    // Предательство надо объявить, иначе его никто
                    // не заметит. Обработчик написан целиком — злость
                    // командира, отряд рядом, запись в память с весом
                    // MemoryBetrayalStrengthMultiplier (-40, сильнейшее
                    // число в конфиге) — и до 14 сентября его не звал
                    // никто. Воин уходил к чужим, и всем было всё равно.
                    if (wasOurs) AOSEventHub.Instance?.OnBetrayal(_warrior, Commander());
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

                // Отход исполняется тем же движением, что и обычный переход:
                // разница между ними не в ногах, а в том, как приказ звучит
                // для характера и что считать его исполнением.
                case CommandKind.Move:
                case CommandKind.FallBack:
                    if (mover != null) mover.CommandMove(cmd.Point);
                    // Дошёл — приказ исчерпан.
                    if (Vector3.Distance(transform.position, cmd.Point) < 1.5f)
                        _warrior.ClearCommand();
                    break;

                // Патруль: туда и обратно, пока не снимут. Драка по дороге —
                // уже решение голоса, а не маршрут.
                case CommandKind.Patrol:
                    var leg = cmd.Back ? cmd.From : cmd.Point;
                    if (mover != null) mover.CommandMove(leg);
                    if (Vector3.Distance(transform.position, leg) < 1.5f) _warrior.TurnPatrol();
                    break;

                // Атака с ходу: идти в точку. Бить по дороге — дело голоса,
                // и удар приказ исполняет (DecisionContext.SatisfiedBy).
                case CommandKind.AttackMove:
                    if (mover != null) mover.CommandMove(cmd.Point);
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
            // Проверка стояла ПОСЛЕ обращения: autoAttack.AttackRange
            // читался у того, кого могло не быть. У прологовых воинов
            // его и не было — значит на каждом решении Attack, у каждого,
            // дважды в секунду, летело исключение.
            var autoAttack = GetComponent<AutoAttack>();
            if (autoAttack != null && dist <= autoAttack.AttackRange)
            {
                autoAttack.ForceAttack(target);
                // Если цель умерла от этой атаки – записываем деяние
                if (target.IsDead)
                {
                    OnKilledEnemy(target.Warrior);
                }
            }
        }

        private HunterGoal _goal;

        /// <summary>
        /// Идёт ли охотник к цели вместо выбранного. Только когда рядом
        /// (десять метров, <see cref="DecisionContext.NearbyEnemies"/>) драться
        /// не с кем, а голос выбрал стоять, грабить или бить кого-то вдали:
        /// тогда он не стоит, не бредёт к трупам и не гонится за дальним
        /// рядовым — он идёт туда, зачем пришёл. Бегство, спасение своего,
        /// умения — остаются голосу: они случаются, только когда рядом кто-то есть.
        /// </summary>
        private bool Hunting(ActionType action, out Vector3 where)
        {
            where = default;
            if (_goal == null) return false;
            if (LastContext != null && LastContext.NearbyEnemies > 0) return false;

            if (action != ActionType.Idle
             && action != ActionType.Loot
             && action != ActionType.Attack) return false;

            return _goal.TryGet(transform.position, out where);
        }

        /// <summary> Публичный метод для поиска лучшей вражеской цели. </summary>
        public Damageable FindBestTarget()
        {
            if (CombatManager.Instance == null) return null;
            var enemies = CombatManager.Instance.GetEnemies(gameObject);
            Damageable best = null;
            float bestScore = float.MinValue;
            foreach (var w in enemies)
            {
                if (w == null || w.IsDead) continue;
                float dist = Vector3.Distance(transform.position, w.transform.position);

                // Тот же радиус, в котором враг виден для голоса. Было 15:
                // решивший гнаться за тем, кто в двадцати метрах, не находил
                // цели и шёл грабить.
                if (dist < CombatDecisionContext.SightRadius)
                {
                    float score = -dist + (1f - w.HP / w.MaxHP) * 50f;
                    if (score > bestScore) { bestScore = score; best = w; }
                }
            }
            return best;
        }

        /// <summary>
        /// Бежал ли этот воин в текущем бою. Нужен «Берсерку»: титул
        /// стоит на NeverRetreat, а писать это деяние было некому —
        /// признак есть только у того, кто исполняет побег.
        /// </summary>
        public bool Fled { get; private set; }

        /// <summary>
        /// Ударил ли этот воин хоть раз в текущем бою. Нужен «Тени»:
        /// титул за то, что был в бою и в нём не участвовал. Ставит его
        /// <see cref="Damageable.TakeDamage"/> — единственное место, куда
        /// сходятся все удары: вблизи, умением, по приказу и без.
        /// </summary>
        public bool Struck { get; private set; }

        public void MarkStruck() => Struck = true;

        /// <summary>Бой кончился: и бегство, и удары считаются заново.</summary>
        public void ForgetBattle()
        {
            Fled = false;
            Struck = false;
        }

        private void ExecuteFlee()
        {
            Fled = true;

            var mover = GetComponent<UnitMover>();
            if (mover != null)
            {
                Vector3 fleeDir = (transform.position - GetAverageEnemyPosition()).normalized;
                mover.CommandMove(transform.position + fleeDir * 15f);
            }
        }


        /// <summary>
        /// Командир отряда, которого предали. Ищется по имени из
        /// <see cref="SquadRoster"/>: хранить ссылку нельзя — воин
        /// может погибнуть, а ссылка переживёт его и бросит
        /// MissingReferenceException (тот же случай, что у долгожителей
        /// на Managers, 13-DRIFT.md).
        ///
        /// Командира нет — это событие, а не ноль: предательство
        /// случилось, объявить его некому, и об этом надо сказать вслух.
        /// </summary>
        private Warrior Commander()
        {
            string name = SquadRoster.CommanderName;

            if (!string.IsNullOrEmpty(name))
                foreach (var w in Object.FindObjectsByType<Warrior>(
                             FindObjectsSortMode.InstanceID))
                    if (!w.IsDead && w.Team == Team.Player && w.DisplayName == name)
                        return w;

            Debug.LogWarning($"[AOS] {_warrior.DisplayName} ушёл к чужим, "
                           + "а старшего в отряде нет — предательство некому "
                           + "запомнить.");
            return null;
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

                    // «Здесь однажды взял». MemoryModule разбирает эту
                    // запись и тянет жадного обратно к месту поживы —
                    // но до 14 сентября строку FoundLoot не писал никто
                    // и нигде, и жадная половина памяти не наступала
                    // ни разу (11-MISSING.md §6).
                    MemoryProcessor.Instance?.CreateMemory(
                        _warrior, "FoundLoot", "", EmotionType.Joy,
                        Mathf.Clamp01(0.3f + gold * 0.02f));

                    Debug.Log($"[LOOT] {_warrior.DisplayName} собрал {gold} золота и {equip ?? "ничего"}");
                    Destroy(closest.gameObject);

                    // Запись деяния за сбор добычи
                    if (gold > 0)
                    {
                        _warrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.CollectMostLoot, Importance = gold / 10f });
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

                // Умение стоит сил, как и удар. До 17 сентября SpendForSkill
                // был написан и не звался ниоткуда: умения выходили даром,
                // и «силы у него на исходе» после десятка умений не наступало
                // никогда. А на усталость смотрят три модуля из тринадцати —
                // Гордыня, Лень и Гнев.
                GetComponent<Gameplay.Fatigue>()?.SpendForSkill();

                return;
            }

            // Умения нет или оно на откате — воин стоит, но не проваливается
            // обратно в Execute: это была бы рекурсия.
            ShowDecisionIcon(ActionType.Idle);
        }

        /// <summary>Что показывали в прошлый раз. Смотри ShowDecisionIcon.</summary>
        private ActionType _shownAction;
        private bool _shownEver;

        /// <summary>
        /// Первая ступень прозрачности: значок намерения <b>в момент
        /// решения</b> (00-GDD.md §7).
        ///
        /// Показывается на смену намерения, а не каждый такт. Решение
        /// принимается раз в секунду, а значок горел полторы — то есть
        /// новый показ приходил раньше, чем истекал старый, и значок
        /// не гас никогда. Вместо вспышки в момент решения над каждым
        /// висела постоянная лампа, и «знак атаки» стоял над тем, кто
        /// просто стоит.
        ///
        /// Молчание тоже говорит: пока намерение не менялось, показывать
        /// нечего. Что он решил и почему — вторая и третья ступени,
        /// подсказка и журнал.
        /// </summary>
        private void ShowDecisionIcon(ActionType action)
        {
            if (_shownEver && action == _shownAction) return;

            _shownAction = action;
            _shownEver = true;

            var overheadUI = GetComponentInChildren<UI.OverheadUI>();
            if (overheadUI?.DecisionIcon != null)
            {
                Sprite icon = GetIconForAction(action);
                // Четыре секунды, а не полторы: значок теперь вспыхивает
                // только на смену намерения, и мелькнувший на полтора
                // такта успевал бы не всякий взгляд. Гаснуть он всё
                // равно обязан — иначе это не намерение, а ярлык.
                if (icon != null) overheadUI.DecisionIcon.Show(icon, 4f);
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
            _warrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = 0.5f });
            // Специальные деяния для перков
            if (_warrior.Soul.Memory?.NarrativePerks != null)
            {
                foreach (var perk in _warrior.Soul.Memory.NarrativePerks)
                {
                    if (perk.PerkName == "Мститель" && enemy.DisplayName.Contains("Бандит"))
                        _warrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = 1.5f });
                    if (perk.PerkName == "Бывший Охотник (ненавидит)" && enemy.DisplayName.Contains("Охотник"))
                        _warrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = 2.0f });
                }
            }
            TitleManager.UpdateTitle(_warrior);
        }
    }
}