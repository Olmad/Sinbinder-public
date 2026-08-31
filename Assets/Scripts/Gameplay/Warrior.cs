// Assets/_Project/Scripts/Gameplay/Warrior.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    public enum Team
    {
        Player,
        Enemy
    }

    public class Warrior : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private Core.SoulData _soul;
        [SerializeField] private Core.ShellType _shell;
        [SerializeField] private Core.ShellData _shellData;
        [SerializeField] private float _maxHP;
        [SerializeField] private float _hp;
        [SerializeField] private float _attack;
        [SerializeField] private float _defense;
        [SerializeField] private bool _isCommander;
        [SerializeField] private Team _team;

        private Core.VirtueSystem _virtue;
        private float _loyalty = 50f;
        private int _unpaidMissions = 0;
        private Core.RelationshipSystem _relationships;
        private bool _isDead = false;
        private HashSet<string> _spokenWithThisBattle = new();

        /// <summary>
        /// Что воин несёт на себе. Восьмой рычаг игрока и, по 08-FLOOR §3.3,
        /// лучший из восьми: он медленный, косвенный и обратимый. Греховод
        /// не переписывает душу — он вкладывает в руку золочёный клинок
        /// и смотрит, что будет.
        ///
        /// Список принадлежит воину, а не мешку Греховода: раньше контекст
        /// брал предметы из общего инвентаря, и «дать клинок жадному»
        /// означало соблазнить им весь отряд разом.
        /// </summary>
        private readonly List<Inventory.InventoryItem> _carried = new();

        public string Id => _id;
        public string DisplayName => _soul.Name;
        public Core.SoulData Soul => _soul;
        public Core.ShellType Shell => _shell;

        /// <summary>Оболочка целиком, если воин собран из неё. Может быть null.</summary>
        public Core.ShellData ShellData => _shellData;
        public Core.VirtueSystem Virtue => _virtue;
        public float HP => _hp;
        public float MaxHP => _maxHP;
        public float Attack { get => _attack; set => _attack = value; }
        public float Defense { get => _defense; set => _defense = value; }
        public Core.RelationshipSystem Relationships => _relationships;
        public float Loyalty => _loyalty;
        public int UnpaidMissions { get => _unpaidMissions; set => _unpaidMissions = value; }
        public bool IsDead => _isDead;
        public bool IsCommander => _isCommander;
        public Team Team { get => _team; set => _team = value; }
        public ReputationData Reputation = new();
        public int Salary => _soul.Level * 10;

        // ---------- приказ игрока ----------

        [SerializeField] private PendingCommand _command;

        /// <summary>
        /// Последний приказ игрока. Записан, но не обязательно исполнен:
        /// исполнение решает голосование, а не игрок.
        /// </summary>
        public PendingCommand Command => _command;

        public bool HasCommand => _command.IsSet;

        public void IssueCommand(CommandKind kind, Vector3 point, GameObject target = null)
        {
            _command = new PendingCommand
            {
                Kind = kind,
                Point = point,
                Target = target,
                IssuedAt = Time.time
            };
        }

        public void ClearCommand() => _command = default;

        /// <summary>
        /// Связывание души с полноценной оболочкой.
        ///
        /// Оболочка перестаёт быть косметикой: она задаёт тело и тянет
        /// душу на себя. Смещение спектров оседает здесь, один раз,
        /// и оно необратимо — вынув душу из волка, получите не ту,
        /// кого вкладывали.
        /// </summary>
        public void Initialize(Core.SoulData soul, Core.ShellData shell, Core.RelationshipSystem relSystem,
            bool isCommander = false, Team team = Team.Player)
        {
            Core.ShellBinder.Bind(soul, shell);
            InitializeCore(soul, shell != null ? shell.type : Core.ShellType.Skeleton,
                relSystem, isCommander, team);
            ApplyShellBody(shell);
        }

        /// <summary>
        /// Связывание по типу оболочки. Тело для типа ищется в библиотеке:
        /// без этого все спавнеры проходили мимо ассетов, и оболочка
        /// оставалась косметикой, хотя <see cref="ShellData"/> уже читали
        /// и экран сборки, и <see cref="Core.ShellBinder"/>.
        /// </summary>
        public void Initialize(Core.SoulData soul, Core.ShellType shell, Core.RelationshipSystem relSystem, bool isCommander = false, Team team = Team.Player)
        {
            var data = Core.ShellLibrary.Get(shell);

            Core.ShellBinder.Bind(soul, data);
            InitializeCore(soul, shell, relSystem, isCommander, team);
            ApplyShellBody(data);
        }

        /// <summary>
        /// Тело оболочки поверх базовых характеристик. Ассета нет —
        /// остаются значения по умолчанию из <see cref="InitializeCore"/>.
        /// </summary>
        private void ApplyShellBody(Core.ShellData shell)
        {
            _shellData = shell;
            if (shell == null) return;

            _maxHP = shell.EffectiveHP + _soul.Level * 10f;
            _hp = _maxHP;
            _defense = shell.baseDefense + _soul.Level;

            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && shell.movementSpeed > 0f)
                agent.speed = shell.movementSpeed;
        }

        private void InitializeCore(Core.SoulData soul, Core.ShellType shell, Core.RelationshipSystem relSystem, bool isCommander, Team team)
        {
            _id = System.Guid.NewGuid().ToString();
            _soul = soul;
            _shell = shell;
            _virtue = new Core.VirtueSystem(soul);
            _relationships = relSystem;
            _isCommander = isCommander;
            _team = team;

            _maxHP = 20f + soul.Level * 10f;
            _hp = _maxHP;
            _attack = 3f + soul.Level * 2f;
            _defense = 1f + soul.Level;

            // Применяем пассивные перки, влияющие на скорость передвижения
            if (_soul.HasMemory && _soul.Memory.NarrativePerks != null)
            {
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    if (_soul.Memory.NarrativePerks.Exists(p => p.PerkName == "Часовщик"))
                    {
                        agent.speed *= 1.05f;
                        agent.angularSpeed *= 1.05f;
                    }
                    if (_soul.Memory.NarrativePerks.Exists(p => p.PerkName == "Бегун"))
                    {
                        agent.speed *= 1.10f;
                    }
                }
            }

            Debug.Log($"[SINBINDER] Воин создан: {soul.Name} | {soul.GetSinName()} | {soul.GetMoralName()} | Командир: {_isCommander} | Команда: {_team}");
        }

        public bool HasSpokenWith(string otherId) => _spokenWithThisBattle.Contains(otherId);
        public void MarkSpokenWith(string otherId) => _spokenWithThisBattle.Add(otherId);
        public void ResetBattleDialogue() => _spokenWithThisBattle.Clear();

        public string GetPersonality()
        {
            string desc = $"=== {DisplayName} ===\n";
            desc += $"Оболочка: {_shell}\n";
            desc += $"Грех: {_soul.GetSinName()}\n";
            desc += $"Характер: {_virtue.GetDescription()}\n";
            desc += $"Мораль: {_soul.GetMoralName()}\n";
            desc += $"Роль: {(_isCommander ? "Командир" : "Рядовой")}\n";
            desc += $"Команда: {_team}\n";

            if (_loyalty > 70f) desc += "Верность: Предан вам\n";
            else if (_loyalty < 30f) desc += "Верность: Готов предать\n";
            else desc += "Верность: Нейтральна\n";

            if (_unpaidMissions > 0) desc += $"Не получал плату: {_unpaidMissions} миссий\n";
            if (_soul.HasMemory) desc += $"Память: Дремлет...\n";

            return desc;
        }

        public void TakeDamage(float damage)
        {
            if (_isDead) return;
            float actual = Mathf.Max(1f, damage - _defense);
            _hp -= actual;
            if (_hp <= 0f) { _hp = 0f; _isDead = true; Debug.Log($"[SINBINDER] {DisplayName} пал в бою!"); }
        }

        /// <summary>
        /// Восстановление здоровья — зеркало <see cref="TakeDamage"/>.
        ///
        /// Мёртвого не лечит: смерть терминальна, и лечение не должно
        /// становиться скрытым воскрешением. Защита на исцеление не влияет —
        /// она гасит удар, а не помощь. Значение берётся по модулю, чтобы
        /// отрицательное число не превратило лечение в урон в обход брони.
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead) return;
            _hp = Mathf.Min(_maxHP, _hp + Mathf.Abs(amount));
        }

        /// <summary>
        /// Плата за вылазку. Верность здесь зажимается в те же границы,
        /// что и в <see cref="ChangeLoyalty"/>: она уходит прямым слагаемым
        /// в голос Верности за приказ, и уйдя ниже нуля, начинала бы
        /// голосовать против послушания — тем сильнее, чем дольше не платят.
        /// Долг и без того наказывается штрафом Жадности.
        /// </summary>
        public void PaySalary(float amount)
        {
            if (amount >= _soul.Level * 10f) { _loyalty = Mathf.Min(100f, _loyalty + 5f); _unpaidMissions = 0; }
            else { _loyalty = Mathf.Max(0f, _loyalty - 20f); _unpaidMissions++; }
        }

        public void ChangeLoyalty(float amount) => _loyalty = Mathf.Clamp(_loyalty + amount, 0f, 100f);

        /// <summary>
        /// Назначить или снять командирство. Нужно военному совету доли 3:
        /// в лагере командирами помечены трое кандидатов, а после выбора
        /// игрока командир обязан остаться один — иначе <c>GetCommander</c>
        /// возьмёт первого попавшегося, и верность будет считаться
        /// к тому, кого игрок не выбирал.
        /// </summary>
        public void SetCommander(bool value) => _isCommander = value;

        // ---------- снаряжение ----------

        public IReadOnlyList<Inventory.InventoryItem> Carried => _carried;

        /// <summary>Вложить предмет в руки. Один и тот же — только раз.</summary>
        public bool Give(Inventory.InventoryItem item)
        {
            if (item == null) return false;
            foreach (var carried in _carried)
                if (carried.Name == item.Name) return false;

            _carried.Add(item);
            return true;
        }

        /// <summary>Забрать всё. Искушение обратимо — в этом его смысл.</summary>
        public void TakeAll() => _carried.Clear();
    }
}