// Assets/AOS Engine/TemperamentPredictor.cs
using System.Collections.Generic;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>Одно предсказание: положение и то, как воин в нём поступит.</summary>
    public struct Prophecy
    {
        public string Situation;
        public string Outcome;

        /// <summary>Разрыв между первым и вторым. Мера уверенности.</summary>
        public float Gap;

        public override string ToString() => $"{Situation} — {Outcome}";
    }

    /// <summary>
    /// Панель предсказания темперамента.
    ///
    /// Прогоняем настоящие модули против выдуманных положений и печатаем,
    /// что победит. Ни одной новой формулы: движок становится собственным
    /// интерфейсом.
    ///
    /// Зачем это нужно, кроме удовольствия сборки. Строка «ломает строй
    /// ради добычи» — предупреждение, которое игрок прочитал и принял.
    /// Когда через десять минут воин действительно ломает строй, это уже
    /// не предательство движка, а сбывшееся пророчество. Отказ становится
    /// честным, а третье требование к демо — «отказ можно было
    /// предотвратить, и игрок это видит» — закрывается здесь, задолго
    /// до самого отказа.
    /// </summary>
    public static class TemperamentPredictor
    {
        private static BehaviourResolver _resolver;

        private static BehaviourResolver Resolver
        {
            get
            {
                if (_resolver == null) _resolver = new BehaviourResolver();
                return _resolver;
            }
        }

        public static List<Prophecy> Predict(Warrior warrior)
        {
            var result = new List<Prophecy>();
            if (warrior == null) return result;

            result.Add(Ask(warrior, "Пока бой ровен", Even(warrior)));
            result.Add(Ask(warrior, "Когда падает друг", AllyDown(warrior)));
            result.Add(Ask(warrior, "Когда рядом золото", LootNear(warrior)));
            result.Add(Ask(warrior, "Когда велено отойти", OrderedBack(warrior)));

            return result;
        }

        private static Prophecy Ask(Warrior warrior, string situation, DecisionContext context)
        {
            var decision = Resolver.DecideDetailed(warrior, context);
            return new Prophecy
            {
                Situation = situation,
                Outcome = Describe(decision, context),
                Gap = decision.Gap
            };
        }

        /// <summary>
        /// Уверенность словами, без чисел. Порог колебания берётся
        /// у резолвера, чтобы пророчество и движок не расходились.
        /// </summary>
        private static string Describe(Decision decision, DecisionContext context)
        {
            if (decision.Hesitated) return "заколеблется";

            string what = Future(decision.Action, context);
            if (decision.Gap > BehaviourResolver.HesitationGap * 3f) return what;
            return $"скорее всего, {what}, но не наверняка";
        }

        private static string Future(ActionType action, DecisionContext context)
        {
            switch (action)
            {
                case ActionType.Attack:      return "пойдёт в драку";
                case ActionType.SaveAlly:    return "бросится к раненому";
                case ActionType.Loot:        return "сломает строй ради добычи";
                case ActionType.Flee:        return "отойдёт";
                case ActionType.Idle:        return "останется на месте";
                case ActionType.ObeyCommand: return "сделает, как велено";
                default:                     return "поступит по-своему";
            }
        }

        // ---------- выдуманные положения ----------

        private static DecisionContext Base(Warrior warrior)
        {
            return new DecisionContext
            {
                CurrentHP = warrior.MaxHP,
                MaxHP = warrior.MaxHP,
                NearbyEnemies = 2,
                NearbyAllies = 2,
                DangerLevel = 0.3f,
                UnpaidMissions = warrior.UnpaidMissions,
                RelationshipWithCommander = 50f,
                RecentMemories = MemoryProcessor.Instance != null
                    ? MemoryProcessor.Instance.GetMemories(warrior)
                    : new List<MemoryRecord>()
            };
        }

        private static DecisionContext Even(Warrior warrior) => Base(warrior);

        private static DecisionContext AllyDown(Warrior warrior)
        {
            var c = Base(warrior);
            c.AllyInDanger = true;
            return c;
        }

        private static DecisionContext LootNear(Warrior warrior)
        {
            var c = Base(warrior);
            c.NearbyLoot = 3;
            return c;
        }

        private static DecisionContext OrderedBack(Warrior warrior)
        {
            var c = Base(warrior);
            c.HasCommand = true;
            c.CommandType = "Move";
            c.IsEngaged = true;
            c.EngagedWith = 1;
            return c;
        }

        /// <summary>Все четыре строки одним текстом — для панели сборки.</summary>
        public static string Describe(Warrior warrior)
        {
            var lines = Predict(warrior);
            var sb = new System.Text.StringBuilder();
            foreach (var p in lines) sb.AppendLine(p.ToString());
            return sb.ToString().TrimEnd();
        }
    }
}
