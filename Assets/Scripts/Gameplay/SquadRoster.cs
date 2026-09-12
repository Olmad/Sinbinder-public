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
    /// Опознаём по имени, а не по Id: идентификатор у души новый при каждой
    /// сборке, имя — то же самое во всех восьми долях.
    /// </summary>
    public static class SquadRoster
    {
        public struct Member
        {
            public string Name;
            public SinType Sin;
            public MoralType Moral;
            public Gender Gender;
            public float Intensity;
            public float Loyalty;
            public int UnpaidMissions;

            /// <summary>
            /// Носит ли перк «Брат по оружию». Живёт в реестре, а не
            /// только в составе лагеря: реестр переживает смену сцен,
            /// и братья обязаны остаться братьями в разгроме.
            /// </summary>
            public bool Brother;

            /// <summary>Ремесло души. Живёт в реестре: оно её свойство,
            /// а не свойство сцены, и переживает переход вместе с ней.</summary>
            public Core.Trade Trade;

            /// <summary>Слава, пришедшая с ним. Не заработанный титул.</summary>
            public bool Legend;

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
        /// Отправить отряд на доле 2. Уходит выбранный старший и рядовые
        /// при нём — опытные остаются в лагере, иначе к доле 4 защищать
        /// его будет некому, а телохранитель не уходит никогда.
        ///
        /// Порядок набора — по списку, без Random: тот же выбор игрока
        /// обязан уводить тех же людей.
        /// </summary>
        public static void SendAway(string commanderName, int size)
            => SendAway(commanderName, size, keepExperienced: true);

        /// <summary>
        /// То же, но с выбором: держать ли опытных дома.
        ///
        /// В прологе они остаются — иначе к доле 4 защищать лагерь
        /// некому. В склепе такого правила нет: игрок сам решает, кем
        /// рискнуть, и запрет «опытные не ходят» отнял бы у него ровно
        /// то решение, ради которого зона и построена.
        /// </summary>
        public static void SendAway(string commanderName, int size, bool keepExperienced)
        {
            int taken = 0;

            for (int i = 0; i < _members.Count && taken < size; i++)
            {
                var m = _members[i];
                if (m.Name != commanderName) continue;

                m.IsAway = true;
                _members[i] = m;
                taken++;
            }

            for (int i = 0; i < _members.Count && taken < size; i++)
            {
                var m = _members[i];
                if (m.IsAway) continue;
                if (!string.IsNullOrEmpty(m.Unavailable)) continue;   // телохранитель
                if (keepExperienced && Leadership.IsExperienced(m.Leadership)) continue;

                m.IsAway = true;
                _members[i] = m;
                taken++;
            }

            Debug.Log($"[ОТРЯД] С {commanderName} ушли {taken}. "
                    + $"В лагере остались {_members.Count - taken}.");
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
            _members.AddRange(members);
        }

        /// <summary>
        /// Запомнить выбор игрока. Командир остаётся один: остальные
        /// кандидаты возвращаются в строй рядовыми.
        /// </summary>
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

        /// <summary>
        /// Снять состояние с живых воинов сцены: кто дожил и с чем.
        /// Кого в списке нет — тот не дожил, и в следующей сцене
        /// его не будет.
        /// </summary>
        public static void Remember(IEnumerable<Warrior> warriors)
        {
            var survivors = new List<Member>();

            // Ушедших в сцене нет и быть не может. Не перенести их руками —
            // значит потерять отряд, который обязан вернуться в эпилоге,
            // и вместе с ним весь смысл военного совета.
            foreach (var m in _members) if (m.IsAway) survivors.Add(m);

            foreach (var w in warriors)
            {
                if (w == null || w.IsDead || w.Soul == null) continue;

                survivors.Add(new Member
                {
                    Name = w.DisplayName,
                    Sin = w.Soul.Sin,
                    Moral = w.Soul.Moral,
                    Intensity = w.Soul.Get(w.Soul.Sin),
                    Loyalty = w.Loyalty,
                    UnpaidMissions = w.UnpaidMissions,
                    IsCandidate = WasCandidate(w.DisplayName),
                    IsCommander = w.IsCommander,

                    // Навык и запрет живут только здесь: у Warrior их нет,
                    // и снять их со сцены невозможно. Не перенести — значит
                    // молча обнулить при первой же смене доли, и к совету
                    // все пришли бы рядовыми.
                    Leadership = PreviousLeadership(w.DisplayName),
                    Unavailable = PreviousUnavailable(w.DisplayName),
                    IsAway = false
                });
            }

            // Сцену закрыли до сборки отряда. Ушедшие не в счёт: они живы
            // и без сцены, но одни они отрядом не считаются.
            bool anyoneHere = false;
            foreach (var m in survivors) if (!m.IsAway) { anyoneHere = true; break; }
            if (!anyoneHere) return;

            _members.Clear();
            _members.AddRange(survivors);
            Debug.Log($"[ОТРЯД] Дальше идут {survivors.Count}.");
        }

        private static bool WasCandidate(string name)
        {
            foreach (var m in _members) if (m.Name == name) return m.IsCandidate;
            return false;
        }

        private static float PreviousLeadership(string name)
        {
            foreach (var m in _members) if (m.Name == name) return m.Leadership;
            return 0f;
        }

        private static string PreviousUnavailable(string name)
        {
            foreach (var m in _members) if (m.Name == name) return m.Unavailable;
            return "";
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
