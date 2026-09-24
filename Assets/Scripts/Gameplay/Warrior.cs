// Assets/Scripts/Gameplay/Warrior.cs
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

        /// <summary>
        /// Те же вещи по местам (docs/34-GEAR.md): одно место — одна вещь.
        /// Номер — <see cref="Inventory.GearSlot"/>. Список выше — они же
        /// подряд, для движка: искушению всё равно, где вещь висит.
        /// </summary>
        private readonly Inventory.InventoryItem[] _worn = new Inventory.InventoryItem[6];

        public string Id => _id;
        public string DisplayName => _soul.Name;

        /// <summary>
        /// Имя, каким его видит игрок: с ремеслом после него,
        /// а заработанный титул — впереди («Гертон Крестьянин»
        /// → «Костекоп Гертон»). Разбор приёма — Core/Naming.
        ///
        /// DisplayName остаётся голым именем нарочно: по нему
        /// ищут в составе отряда, сверяют с ушедшими и пишут
        /// в журнал. Украшенное имя сломало бы все эти сверки
        /// в тот же день, когда кто-нибудь заработает титул.
        /// </summary>
        public string ShownName => Core.Naming.Full(
            _soul.Name, _soul.Trade, Reputation?.CurrentName);
        public Core.SoulData Soul => _soul;
        public Core.ShellType Shell => _shell;

        /// <summary>
        /// Пол — у души, а не у оболочки. Скелет не мужчина и не женщина;
        /// мужчина или женщина — тот, кто в нём сидит.
        /// </summary>
        public Core.Gender Gender => Soul != null ? Soul.Gender : Core.Gender.Male;

        /// <summary>Оболочка целиком, если воин собран из неё. Может быть null.</summary>
        public Core.ShellData ShellData => _shellData;
        public Core.VirtueSystem Virtue => _virtue;
        /// <summary>
        /// Здоровье — у тела, когда тело есть.
        ///
        /// До 24 сентября у воина было <b>две полосы здоровья</b>: своя
        /// и у <see cref="Damageable"/>. Бой бьёт по второй (её же рисует
        /// полоска над головой), а душа читала первую, которую бой
        /// не трогал никогда. Для движка решений убитый оставался жив,
        /// а раненый — цел: мёртвые разговаривали и получали титулы,
        /// мёртвым Греховодом можно было управлять (слово автора),
        /// а Страх «бежать, когда меньше сорока процентов», спасение
        /// раненого товарища и лечение умениями в настоящем бою
        /// не срабатывали ни разу. Правило проекта — одна правда
        /// о персонаже, а не две, которые разойдутся. Разошлись.
        ///
        /// Свои поля остаются для воина без тела — вылазки, которые
        /// считаются без сцены (<c>AutoBattleResolver</c>).
        /// </summary>
        public float HP => Body is Damageable b ? b.HP : _hp;
        public float MaxHP => Body is Damageable b ? b.MaxHP : _maxHP;
        /// <summary>
        /// Удар в бою: от оболочки плюс от вещей в руках (решение автора,
        /// 24 сентября). Запись меняет основу — ту, что от оболочки; вещи
        /// прибавляются сверху, пока их несут.
        /// </summary>
        public float Attack
        {
            get
            {
                float sum = _attack;
                for (int s = 0; s < _worn.Length; s++)
                {
                    var i = _worn[s];
                    if (i == null) continue;
                    sum += s == (int)Inventory.GearSlot.Offhand ? i.AttackBonus * CombatMath.OffhandShare : i.AttackBonus;
                }
                return sum;
            }
            set => _attack = value;
        }

        /// <summary>Защита в бою: от оболочки плюс от надетого. См. <see cref="CombatMath"/>.</summary>
        public float Defense
        {
            get { float sum = _defense; foreach (var i in _worn) if (i != null) sum += i.DefenseBonus; return sum; }
            set => _defense = value;
        }
        public Core.RelationshipSystem Relationships => _relationships;
        public float Loyalty => _loyalty;
        public int UnpaidMissions { get => _unpaidMissions; set => _unpaidMissions = value; }
        public bool IsDead => _isDead || (Body is Damageable b && b.IsDead);

        /// <summary>
        /// Сколько держит тело этой оболочки, с износом. Ноль — оболочка
        /// не загрузилась. Отсюда тело берёт свой запас, появляясь
        /// (<see cref="Damageable"/>): до 24 сентября оно брало 30
        /// по умолчанию у всех, и оболочка до боя не доходила никогда.
        /// </summary>
        public float ShellHP => _shellData != null ? _shellData.EffectiveHP : 0f;

        /// <summary>
        /// Тело в мире. Его вешают после души (<see cref="WarriorRig"/>
        /// идёт вторым), поэтому ищем, пока не найдём, и пустоту
        /// не запоминаем — иначе спросивший раньше времени навсегда
        /// оставил бы воина без тела. Найденное держим и после
        /// уничтожения: здоровье — простое поле, прочесть его можно.
        /// </summary>
        private Damageable Body
        {
            get
            {
                if (ReferenceEquals(_body, null) && this != null)
                {
                    var found = GetComponent<Damageable>();
                    if (found != null) _body = found;
                }
                return _body;
            }
        }

        private Damageable _body;
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

        /// <param name="muffle">Насколько приглушён голос Греховода
        /// (<see cref="Voice.MuffleFor"/>). Сценарные приказы — провожатый,
        /// прогон демо — звучат в полную силу: это не голос игрока.</param>
        public void IssueCommand(CommandKind kind, Vector3 point, GameObject target = null,
                                 float muffle = 0f)
        {
            _command = new PendingCommand
            {
                Kind = kind,
                Point = point,
                Target = target,
                IssuedAt = Time.time,
                Muffle = Mathf.Clamp01(muffle),
            };
        }

        public void ClearCommand() => _command = default;

        /// <summary>
        /// Патруль: ходить отсюда до <paramref name="to"/> и обратно, пока
        /// не снимут. Второй конец маршрута — там, где воин стоял в миг приказа.
        /// </summary>
        public void IssuePatrol(Vector3 to, float muffle = 0f)
        {
            IssueCommand(CommandKind.Patrol, to, null, muffle);
            _command.From = transform.position;
        }

        /// <summary>Дошёл до конца маршрута — развернуться.</summary>
        public void TurnPatrol() => _command.Back = !_command.Back;

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

            // Только оболочка. Десять за уровень ушли 24 сентября: уровней
            // в игре нет (слово автора), а прибавка жила лишь здесь,
            // в полосе, которую бой не читал.
            _maxHP = shell.EffectiveHP;
            _hp = _maxHP;

            // Удар и защита — тоже только оболочка (решение автора,
            // 24 сентября); вещи в руках прибавляются в Attack и Defense.
            _attack = shell.baseAttack;
            _defense = shell.baseDefense;

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

            // Без оболочки — как было в бою у всех: удар 5, защиты нет.
            // Оболочка перепишет оба числа (ApplyShellBody).
            _attack = 5f;
            _defense = 0f;

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
            if (IsDead) return;

            // Есть тело — бьём по телу: смерть обязана пройти тем же путём,
            // что и в бою (CombatManager, труп, душа), а не остаться строкой
            // в поле, которого никто не видит. Защиту тогда гасит тело —
            // одно место на весь бой; иначе удар гасился бы дважды.
            if (Body is Damageable b)
            {
                b.TakeDamage(CombatMath.Enabled ? damage : Mathf.Max(1f, damage - _defense), null);
                return;
            }

            float actual = CombatMath.Enabled
                ? CombatMath.Absorb(damage, Defense)
                : Mathf.Max(1f, damage - _defense);

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
            if (IsDead) return;
            if (Body is Damageable b) { b.Heal(amount); return; }
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

        /// <summary>Что надето на этом месте. Пусто — null.</summary>
        public Inventory.InventoryItem Worn(Inventory.GearSlot slot) => _worn[(int)slot];

        /// <summary>
        /// Свободное место, куда встанет вещь. Оружие — в руку, а если рука
        /// занята, во вторую. None — всё, куда она встаёт, занято: тогда
        /// это уже замена, и решает её не воин, а <see cref="SquadGear"/>.
        /// </summary>
        public Inventory.GearSlot FreePlaceFor(Inventory.InventoryItem item)
        {
            if (item == null) return Inventory.GearSlot.None;

            var own = item.Slot;
            if (own == Inventory.GearSlot.None) return own;
            if (Worn(own) == null) return own;
            if (own == Inventory.GearSlot.Weapon && Worn(Inventory.GearSlot.Offhand) == null)
                return Inventory.GearSlot.Offhand;
            return Inventory.GearSlot.None;
        }

        /// <summary>Надеть на свободное место. Ложь — места нет или вещь уже на нём.</summary>
        public bool Give(Inventory.InventoryItem item) => Wear(item, FreePlaceFor(item));

        /// <summary>Надеть на это место, если оно свободно и вещь на него встаёт.</summary>
        public bool Wear(Inventory.InventoryItem item, Inventory.GearSlot slot)
        {
            if (item == null || !item.Fits(slot) || Worn(slot) != null) return false;
            if (_carried.Contains(item)) return false;

            _worn[(int)slot] = item;
            Arrange();
            return true;
        }

        /// <summary>Снять вещь. Отдаёт ли — решает не здесь (SquadGear).</summary>
        public bool Drop(Inventory.InventoryItem item)
        {
            if (item == null) return false;
            for (int s = 0; s < _worn.Length; s++)
            {
                if (_worn[s] != item) continue;
                _worn[s] = null;
                Arrange();
                return true;
            }
            return false;
        }

        /// <summary>Забрать всё. Искушение обратимо — в этом его смысл.</summary>
        public void TakeAll()
        {
            System.Array.Clear(_worn, 0, _worn.Length);
            _carried.Clear();
        }

        /// <summary>
        /// Лучшее оружие — в главной руке. Опустела главная — вторая
        /// перекладывает своё в неё; щит так и остаётся щитом.
        /// </summary>
        private void Arrange()
        {
            int main = (int)Inventory.GearSlot.Weapon, off = (int)Inventory.GearSlot.Offhand;
            var second = _worn[off];
            if (second != null && second.Slot == Inventory.GearSlot.Weapon
                && (_worn[main] == null || second.AttackBonus > _worn[main].AttackBonus))
            {
                _worn[off] = _worn[main];
                _worn[main] = second;
            }

            _carried.Clear();
            foreach (var i in _worn) if (i != null) _carried.Add(i);
        }
    }
}