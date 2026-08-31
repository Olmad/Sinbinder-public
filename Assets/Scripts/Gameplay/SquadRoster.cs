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
            public float Intensity;
            public float Loyalty;
            public int UnpaidMissions;

            /// <summary>Один из трёх, кого можно поставить командиром на доле 3.</summary>
            public bool IsCandidate;

            /// <summary>Кого игрок выбрал. До совета — ни один.</summary>
            public bool IsCommander;
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
                    IsCommander = w.IsCommander
                });
            }

            if (survivors.Count == 0) return;   // сцену закрыли до сборки отряда

            _members.Clear();
            _members.AddRange(survivors);
            Debug.Log($"[ОТРЯД] Дальше идут {survivors.Count}.");
        }

        private static bool WasCandidate(string name)
        {
            foreach (var m in _members) if (m.Name == name) return m.IsCandidate;
            return false;
        }

        /// <summary>Забыть всё — начать пролог заново.</summary>
        public static void Clear() => _members.Clear();
    }
}
