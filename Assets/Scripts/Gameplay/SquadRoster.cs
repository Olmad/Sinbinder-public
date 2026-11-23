// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Отряд между сценами.
    ///
    /// Демо требует «возвращение выжившего отряда, состав которого зависит
    /// от выбора командира в начале» (00-GDD.md §8). До этого списка каждая
    /// сцена собирала все восемь фигур заново: и выбор командира на доле 3,
    /// и потери в бою умирали вместе со сценой, а обещание §8 было
    /// невыполнимо в принципе.
    ///
    /// Здесь лежит только то, чего нельзя пересчитать: кто дожил, с какой
    /// верностью, сколько ему не доплатили и кого игрок поставил командиром.
    /// Отношения сюда не попадают намеренно — они складываются из общей
    /// памяти и считаются заново, а отдельное поле связи создало бы вторую
    /// правду о персонаже.
    ///
    /// Вылазки выбирают бойцов по имени. Снаряжение следует за
    /// идентификатором воина при смене сцены и загрузке.
    /// </summary>
    public static class SquadRoster
    {
        public struct Member
        {
            public string Name;
            public string WarriorId;
            public bool JoinedAfterRaid;
            public SinType Sin;
            public MoralType Moral;
            public Gender Gender;
            public float Intensity;
            public float Loyalty;
            // 0 is old save / no recorded wounds; interpreted as full health.
            public float HealthFraction;
            public int UnpaidMissions;

            /// <summary>
            /// Что на нём надето (docs/34-GEAR.md §9.4). Живёт здесь, а не
            /// только на теле: тело остаётся в сцене, а вещи на сбежавших
            /// обязаны уйти вместе с ними (решение автора, 24 сентября).
            /// До этого всё надетое пропадало при каждой смене доли.
            /// </summary>
            public List<Inventory.InventoryItem> Gear;
            public List<Inventory.EquippedItem> Equipment;

            /// <summary>Золото в личном кармане (§9.3). Уходит вместе с ним.</summary>
            public int Pocket;

            /// <summary>
            /// Носит ли перк «Брат по оружию». Живёт в реестре, а не
            /// только в составе лагеря: реестр переживает смену сцен,
            /// и братья обязаны остаться братьями в разгроме.
            /// </summary>
            public bool Brother;

            /// <summary>Ремесло души. Живёт в реестре: оно её свойство,
            /// а не свойство сцены, и переживает переход вместе с ней.</summary>
            public Core.Trade Trade;

            /// <summary>
            /// Память, пришедшая с ним: история сгенерированного (docs/53-GENERATOR.md §11 —
            /// как умер и как душа попала к нам). Пусто — память по составу лагеря.
            /// </summary>
            public Core.MemorySeed Seed;

            /// <summary>Слава, пришедшая с ним. Не заработанный титул.</summary>
            public bool Legend;

            /// <summary>
            /// Заработанный титул словом («Костекоп»; пусто — нет). Живёт
            /// здесь с 16 октября: титул лежит на теле (<c>ReputationData</c>),
            /// тело остаётся в сцене, и заслуживший имя в набеге входил в склеп
            /// без него. Вешает обратно <c>TitleManager.Bestow</c>.
            /// </summary>
            public string Title;

            /// <summary>
            /// Имя на экране — по тому же старшинству, что у воина в сцене
            /// (<see cref="Naming"/>): титул, иначе ремесло к имени из одного
            /// слова. Для окон, где воина в сцене нет: карта вылазок, эпилог.
            /// <see cref="Name"/> — голое имя, по нему ищут.
            /// </summary>
            public string ShownName => Naming.Full(Name, Trade, "",
                string.IsNullOrEmpty(Title) ? "" : Title + " " + Name);

            /// <summary>
            /// Тело: скелет, зомби, призрак. Живёт в составе, а не в сцене:
            /// зомби у костра обязан войти в склеп зомби и вернуться с вылазки
            /// в своём теле. По умолчанию — скелет, каким отряд был до 25 сентября.
            /// </summary>
            public ShellType Shell;

            /// <summary>Один из трёх, кого можно поставить командиром на доле 3.</summary>
            public bool IsCandidate;

            /// <summary>Кого игрок выбрал. До совета — ни один.</summary>
            public bool IsCommander;

            /// <summary>
            /// Навык командования. Определяет только размер отряда,
            /// который этот воин уведёт (<see cref="Leadership"/>).
            /// Ноль — рядовой: вести может, но возьмёт троих.
            /// </summary>
            public float Leadership;

            /// <summary>
            /// Ушёл с отрядом на доле 2. Такого нет ни в одной сцене
            /// пролога, и он обязан пережить их все: вернётся он только
            /// в эпилоге (docs/09-PROLOGUE.md §4, сцена 8).
            /// </summary>
            public bool IsAway;

            /// <summary>
            /// Почему его нельзя поставить старшим. Пусто — можно.
            /// Причина показывается игроку прямо в строке совета, чтобы
            /// он не жал кнопку, которая соврёт (docs/09-PROLOGUE.md §6).
            /// </summary>
            public string Unavailable;
        }

        private static readonly List<Member> _members = new();

        public static IReadOnlyList<Member> Members => _members;
        public static bool HasSquad => _members.Count > 0;

        /// <summary>Имя выбранного командира или пусто, если совет ещё не был.</summary>
        public static string CommanderName
        {
            get
            {
                foreach (var m in _members) if (m.IsCommander) return m.Name;
                return "";
            }
        }

        /// <summary>Кто ушёл на вылазку и не показывается до эпилога.</summary>
        public static IEnumerable<Member> Away
        {
            get
            {
                foreach (var m in _members) if (m.IsAway) yield return m;
            }
        }

        /// <summary>
        /// Может ли он уйти на вылазку прямо сейчас: не телохранитель
        /// и не в пути с другим отрядом.
        /// </summary>
        public static bool CanGo(Member m)
            => string.IsNullOrEmpty(m.Unavailable) && !m.IsAway;

        /// <summary>
        /// Кого старший возьмёт, если игрок не выберет сам, — без старшего,
        /// на <paramref name="size"/> минус одно место. Это прежнее правило
        /// набора, и до 16 октября оно было единственным: игрок выбирал
        /// старшего, а спутников ему подбирал список. Теперь это подсказка:
        /// окно выбора открывается с ней, и не тронувший ничего получает
        /// ровно то, что получал раньше.
        ///
        /// Порядок — по списку, без Random. <paramref name="keepExperienced"/> —
        /// в прологе опытные по подсказке остаются в лагере: так он не пустеет
        /// к набегу. Игрок может взять их сам.
        /// </summary>
        public static List<string> Suggest(string commanderName, int size, bool keepExperienced)
        {
            var party = new List<string>();
            foreach (var m in _members)
            {
                if (party.Count >= size - 1) break;
                if (m.Name == commanderName || !CanGo(m) || !HasUniqueName(m.Name)) continue;
                if (keepExperienced && Leadership.IsExperienced(m.Leadership)) continue;
                party.Add(m.Name);
            }
            return party;
        }

        /// <summary>
        /// Отправить отряд по подсказке (<see cref="Suggest"/>): в прологе
        /// опытные остаются. Для тех, кто посылает без окна выбора, —
        /// самопроверки и сцен без игрока.
        /// </summary>
        public static void SendAway(string commanderName, int size)
            => SendAway(commanderName, Suggest(commanderName, size, keepExperienced: true));

        /// <summary>То же, с выбором, держать ли опытных дома.</summary>
        public static void SendAway(string commanderName, int size, bool keepExperienced)
            => SendAway(commanderName, Suggest(commanderName, size, keepExperienced));

        /// <summary>
        /// Отправить старшего и тех, кого выбрал игрок (решение автора, 16 октября:
        /// «по плану можно выбирать командира и его отряд из списка всех воинов»;
        /// <c>00-GDD.md</c> §5: «сбор отряда → выбор командира → вылазка»).
        /// Кто не может уйти (<see cref="CanGo"/>), не уходит, даже если назван.
        /// </summary>
        public static void SendAway(string commanderName, ICollection<string> party)
        {
            int taken = 0;
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                bool named = m.Name == commanderName || (party != null && party.Contains(m.Name));
                // Старшего уже проверили, когда давали выбрать; спутник,
                // названный по ошибке (телохранитель, ушедший), — остаётся.
                if (!named || m.IsAway) continue;
                if (m.Name != commanderName && !CanGo(m)) continue;

                m.IsAway = true;
                _members[i] = m;
                taken++;
            }

            Debug.Log($"[ОТРЯД] С {commanderName} ушли {taken}. "
                    + $"Остались {_members.Count - taken}.");
        }

        /// <summary>
        /// Отряд вернулся. Кого нет в списке выживших — тот не вернулся,
        /// и его в составе больше нет.
        ///
        /// <b>И каждому вернувшемуся прибавляется невыплата.</b> Это
        /// не мелочь учёта, а тот самый рычаг: долг — единственная
        /// причина отказа, которую игрок может убрать заранее
        /// (<c>12-BALANCE.md</c>, «Долг»). Пока вылазка ничего не стоила,
        /// рычага не было; теперь после каждой ему должны, и заплатить
        /// или нет — решение.
        /// </summary>
        /// <summary>
        /// Вернуть одну вылазку, а не всех ушедших: из склепа уходят, пока
        /// отряд, посланный из лагеря, ещё в пути. Прежний <see cref="ComeBack(IReadOnlyList{string})"/>
        /// снимал всех ушедших разом — и тех, кто на эту вылазку не ходил,
        /// не найдя их среди вернувшихся, вычёркивал из отряда как павших.
        /// </summary>
        public static void ComeBack(IReadOnlyList<string> party, IReadOnlyList<string> survivors)
            => ComeBack(party, survivors, clearCommander: false);

        internal static void ComeBack(IReadOnlyList<string> party, IReadOnlyList<string> survivors,
            bool clearCommander)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                var m = _members[i];
                if (!m.IsAway || !Contains(party, m.Name)) continue;

                if (!Contains(survivors, m.Name)) { _members.RemoveAt(i); continue; }

                m.IsAway = false;
                if (clearCommander) m.IsCommander = false;
                m.UnpaidMissions++;
                _members[i] = m;
            }
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            if (names == null) return false;
            for (int j = 0; j < names.Count; j++)
                if (names[j] == name) return true;
            return false;
        }

        public static void ComeBack(IReadOnlyList<string> survivors)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                var m = _members[i];
                if (!m.IsAway) continue;

                bool alive = false;
                if (survivors != null)
                    for (int j = 0; j < survivors.Count; j++)
                        if (survivors[j] == m.Name) { alive = true; break; }

                if (!alive) { _members.RemoveAt(i); continue; }

                m.IsAway = false;
                m.IsCommander = false;
                m.UnpaidMissions++;
                _members[i] = m;
            }
        }

        /// <summary>
        /// Сдвинуть верность одного по имени.
        ///
        /// Нужно тому, кто судит о поступке: сам роcтер не знает,
        /// что случилось на дороге, и знать не должен. Он хранит людей,
        /// а не мораль.
        /// </summary>
        public static void ShiftLoyalty(string name, float delta)
        {
            if (string.IsNullOrEmpty(name) || Mathf.Approximately(delta, 0f)) return;

            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Name != name) continue;

                var m = _members[i];
                m.Loyalty = Mathf.Clamp(m.Loyalty + delta, 0f, 100f);
                _members[i] = m;
                return;
            }
        }

        /// <summary>
        /// The AOS simulator has already determined who survived and how
        /// wounded they are. Apply ONLY that named survivor's condition;
        /// never re-roll damage when the party reaches home.
        /// </summary>
        public static void ApplyExpeditionHealth(string name, float fraction)
        {
            if(string.IsNullOrEmpty(name)) return;
            float safe=ExpeditionHealthRules.RestoreFraction(fraction);
            for(int i=0;i<_members.Count;i++)
            {
                var member=_members[i];
                if(member.Name!=name || !member.IsAway) continue;
                member.HealthFraction=safe;
                _members[i]=member;
                return;
            }
        }

        /// <summary>
        /// Actual crypt repair after an expedition, not a save-file heal.
        /// This is only available to present (not away) wounded warriors.
        /// The caller charges the player's inventory and synchronizes any
        /// currently spawned scene body before saving.
        /// </summary>
        public static bool RepairReturnedWarrior(string name)
        {
            if(string.IsNullOrEmpty(name)) return false;
            for(int i=0;i<_members.Count;i++)
            {
                var member=_members[i];
                if(member.Name!=name || member.IsAway ||
                   ExpeditionHealthRules.TreatmentCost(member.HealthFraction)==0)
                    continue;
                member.HealthFraction=1f;
                _members[i]=member;
                return true;
            }
            return false;
        }

        /// <summary>Заплатить всем: долги обнуляются, верность растёт.</summary>
        public static void PayEveryone()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (m.UnpaidMissions <= 0) continue;

                m.UnpaidMissions = 0;
                m.Loyalty = Mathf.Min(100f, m.Loyalty + 5f);
                _members[i] = m;
            }
        }

        public static void Set(IEnumerable<Member> members)
        {
            _members.Clear();
            var ids = new HashSet<string>();
            foreach (var member in members)
            {
                var m = member;
                if (string.IsNullOrEmpty(m.WarriorId) || !ids.Add(m.WarriorId))
                {
                    m.WarriorId = System.Guid.NewGuid().ToString();
                    ids.Add(m.WarriorId);
                }
                _members.Add(m);
            }
        }

        /// <summary>
        /// Запомнить выбор игрока. Командир остаётся один: остальные
        /// кандидаты возвращаются в строй рядовыми.
        /// </summary>
        /// <summary>
        /// Immediate transfer of a warrior's pocket into the roster used
        /// by SaveSystem.Snapshot. Without this, card stakes could vanish
        /// on save/reload because only the old pocket value was written.
        /// Away members are deliberately never overwritten.
        /// </summary>
        /// <summary>
        /// Return escrow after a scene has destroyed the warrior object.
        /// Only the persistent roster can receive those coins safely.
        /// </summary>
        public static void CreditPocket(string name, int amount)
        {
            if (string.IsNullOrEmpty(name) || amount <= 0) return;
            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (member.Name != name) continue;
                member.Pocket = checked(member.Pocket + amount);
                _members[i] = member;
                break;
            }
        }

        public static void SyncPocket(Warrior warrior)
        {
            if (warrior == null || warrior.Soul == null) return;
            int index = PresentMemberIndex(warrior);
            if (index < 0) return;

            var member = _members[index];
            member.Pocket = warrior.PocketGold;
            _members[index] = member;
        }

        internal static IEnumerable<Member> SaveSnapshot(IEnumerable<Warrior> liveWarriors, bool captureCondition)
        {
            var owners = new Dictionary<int, Warrior>();
            var ambiguous = new HashSet<int>();
            foreach (var warrior in liveWarriors)
            {
                if (!IsEquipmentOwner(warrior) || !warrior.gameObject.activeInHierarchy || warrior.IsDead) continue;
                int index = PresentMemberIndex(warrior);
                if (index >= 0 && !owners.TryAdd(index, warrior) && owners[index] != warrior)
                    ambiguous.Add(index);
            }

            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (!ambiguous.Contains(i) && owners.TryGetValue(i, out var warrior))
                {
                    member = CaptureEquipment(warrior, member);
                    if (captureCondition) member = CaptureCondition(warrior, member);
                }
                yield return member;
            }
        }

        internal static void SyncEquipment(Warrior warrior)
        {
            if (!IsEquipmentOwner(warrior)) return;
            int index = PresentMemberIndex(warrior);
            if (index >= 0) _members[index] = CaptureEquipment(warrior, _members[index]);
        }

        private static bool IsEquipmentOwner(Warrior warrior)
            => warrior != null && warrior.Soul != null && warrior.Team == Team.Player && !(warrior is SinbinderPlayer);

        private static int PresentMemberIndex(Warrior warrior)
        {
            int index = MemberIndex(warrior);
            return index >= 0 && !_members[index].IsAway ? index : -1;
        }

        private static int MemberIndex(Warrior warrior)
        {
            int named = -1;
            bool ambiguous = false;
            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (!string.IsNullOrEmpty(member.WarriorId) && member.WarriorId == warrior.Id)
                    return i;
                if (member.Name != warrior.DisplayName) continue;
                if (named >= 0) ambiguous = true;
                named = i;
            }
            // На старых записях имени хватает, только если оно единственное.
            if (!ambiguous && named >= 0 && string.IsNullOrEmpty(_members[named].WarriorId))
                return named;
            return -1;
        }

        private static Member CaptureEquipment(Warrior warrior, Member member)
        {
            member.Equipment = warrior.CaptureEquipment();
            member.Gear = new List<Inventory.InventoryItem>(warrior.Carried);
            return member;
        }

        public static void ChooseCommander(string name)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                m.IsCommander = m.Name == name;
                _members[i] = m;
            }

            Debug.Log($"[ОТРЯД] Командиром выбран {name}.");
        }

        /// <summary>Вписать поднятого в склепе воина сразу после связывания.</summary>
        internal static void Recruit(Warrior warrior)
        {
            foreach (var member in _members)
                if (member.WarriorId == warrior.Id) return;

            var recruit = Capture(warrior, NewMember(warrior));
            recruit.JoinedAfterRaid = true;
            _members.Add(recruit);
        }

        internal static bool OwedRaidPay(Warrior warrior)
        {
            if (!HasSquad) return true;
            foreach (var member in _members)
                if (member.WarriorId == warrior.Id) return !member.JoinedAfterRaid;
            return TryGet(warrior.DisplayName, out var namedMember) && !namedMember.JoinedAfterRaid;
        }

        /// <summary>
        /// Снять состояние с живых воинов сцены: кто дожил и с чем.
        /// Кого в списке нет — тот не дожил, и в следующей сцене
        /// его не будет.
        /// </summary>
        public static void Remember(IEnumerable<Warrior> warriors)
            => CaptureSceneRoster(warriors, preserveWhenEmpty: true);

        /// <summary>Обновить состав с живой сцены, даже если в ней никого не осталось.</summary>
        public static void RefreshFromScene(IEnumerable<Warrior> warriors)
            => CaptureSceneRoster(warriors, preserveWhenEmpty: false);

        private static void CaptureSceneRoster(IEnumerable<Warrior> warriors, bool preserveWhenEmpty)
        {
            var survivors = new List<Member>();

            // Ушедших в сцене нет и быть не может. Не перенести их руками —
            // значит потерять отряд, который обязан вернуться в эпилоге,
            // и вместе с ним весь смысл военного совета.
            foreach (var m in _members) if (m.IsAway) survivors.Add(m);
            int awayCount = survivors.Count;

            foreach (var w in warriors)
            {
                if (w == null || w.IsDead || w.Soul == null) continue;

                // An automatic expedition reserves real warriors in the
                // roster before the crypt scene has despawned them. Never
                // re-import their lingering GameObjects as available units:
                // doing so would create two entries for one soul and permit
                // the same soldier to join overlapping missions.
                var previous = Previous(w);
                if (previous.IsAway) continue;

                // Начинаем с прежней записи, а не с пустой: пол, ремесло,
                // братство, слава, навык и запрет живут только здесь, и снять
                // их со сцены нельзя. До 24 сентября переносились лишь навык
                // и запрет — а пол, ремесло, братство и слава обнулялись при
                // каждой смене доли: уникальная женщина входила в склеп
                // мужчиной, братья переставали быть братьями.
                survivors.Add(Capture(w, previous));
            }

            // Сцену закрыли до сборки отряда. Ушедшие не в счёт: они живы
            // и без сцены, но одни они отрядом не считаются.
            if (preserveWhenEmpty && survivors.Count == awayCount) return;

            _members.Clear();
            _members.AddRange(survivors);
            Debug.Log($"[ОТРЯД] Дальше идут {survivors.Count}.");
        }

        private static Member Capture(Warrior w, Member member)
        {
            member.Name = w.DisplayName;
            member.WarriorId = w.Id;
            member.Sin = w.Soul.Sin;
            member.Moral = w.Soul.Moral;
            member.Intensity = w.Soul.Get(w.Soul.Sin);
            member.IsAway = false;
            member.Shell = w.Shell;
            member.Title = AOS.TitleManager.TitleOf(w);
            return CaptureEquipment(w, CaptureCondition(w, member));
        }

        private static Member CaptureCondition(Warrior warrior, Member member)
        {
            member.Loyalty = warrior.Loyalty;
            member.HealthFraction = ExpeditionHealthRules.Capture(warrior.HP, warrior.MaxHP);
            member.UnpaidMissions = warrior.UnpaidMissions;
            member.IsCommander = warrior.IsCommander;
            member.Pocket = warrior.PocketGold;
            return member;
        }

        /// <summary>
        /// Прежняя запись о воине. Нет её — он пришёл не из состава
        /// (поднятый в склепе): пол и ремесло берём у его души, остальное
        /// пусто.
        /// </summary>
        private static Member Previous(Warrior w)
        {
            int index = MemberIndex(w);
            return index >= 0 ? _members[index] : NewMember(w);
        }

        private static Member NewMember(Warrior w)
            => new Member
            {
                Gender = w.Gender,
                Trade = w.Soul.Trade,
                Unavailable = "",
            };

        public static bool HasUniqueName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            bool found = false;
            foreach (var member in _members)
            {
                if (member.Name != name) continue;
                if (found) return false;
                found = true;
            }
            return found;
        }

        /// <summary>Найти воина в составе по имени.</summary>
        public static bool TryGet(string name, out Member member)
        {
            foreach (var m in _members)
                if (m.Name == name) { member = m; return true; }

            member = default;
            return false;
        }

        /// <summary>Забыть всё — начать пролог заново.</summary>
        public static void Clear() => _members.Clear();
    }
}
