// Assets/AOS Engine/PhraseGenerator.cs
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Вторая и третья ступени прозрачности: решение словами.
    ///
    /// Первая ступень — значок над головой — говорит ЧТО. Здесь говорится
    /// ПОЧЕМУ. Правило одно и жёсткое: ни одной цифры. Игрок не должен
    /// увидеть ни очков, ни весов, ни процентов — только факты о воине
    /// и о том, что было вокруг.
    ///
    /// Ничего случайного: одинаковое решение всегда описывается одними
    /// и теми же словами. Иначе игрок не сможет учиться на объяснениях.
    /// </summary>
    public static class PhraseGenerator
    {
        /// <summary>
        /// Подсказка при наведении, настоящее время. Одно-два предложения.
        /// </summary>
        public static string Explain(Warrior warrior, DecisionContext context, Decision decision)
        {
            if (warrior == null) return "";
            string name = warrior.DisplayName;

            if (decision.Hesitated)
                return $"{name} колеблется. {Noun(decision.TopContender)} и {Noun(decision.RunnerUp)} "
                     + "тянут его почти поровну.";

            string what = Verb(decision.Action, context);
            string why = Reason(warrior, context, decision);

            if (decision.RefusedCommand)
                return $"Приказ был. {name} {what} — {why}.";

            return string.IsNullOrEmpty(why)
                ? $"{name} {what}."
                : $"{name} {what}: {why}.";
        }

        /// <summary>
        /// Строка журнала, прошедшее время. Появляется в момент решения,
        /// а не после боя: объяснение, приехавшее через десять минут,
        /// объяснением уже не является.
        /// </summary>
        public static string LogLine(Warrior warrior, DecisionContext context, Decision decision)
        {
            if (warrior == null) return "";
            string name = warrior.DisplayName;

            if (decision.Hesitated)
                return $"{name} не сдвинулся с места — не смог выбрать.";

            string why = Reason(warrior, context, decision);
            string what = VerbPast(decision.Action, context);

            if (decision.RefusedCommand)
                return string.IsNullOrEmpty(why)
                    ? $"{name} не выполнил приказ. Вместо этого {what}."
                    : $"{name} не выполнил приказ: {why}. Вместо этого {what}.";

            return string.IsNullOrEmpty(why) ? $"{name} {what}." : $"{name} {what}: {why}.";
        }

        // ---------- причина ----------

        private static string Reason(Warrior warrior, DecisionContext context, Decision decision)
        {
            switch (decision.TopModule)
            {
                case "Greed":
                    if (context.UnpaidMissions > 2) return "ему не платили третью вылазку подряд";
                    if (context.UnpaidMissions > 0) return "ему до сих пор не заплатили";
                    if (context.NearbyLoot > 0) return "добыча лежала слишком близко";
                    return "он думает о своей доле";

                case "Wrath":
                    return "он не умеет стоять, когда есть кого ударить";

                case "Fear":
                    if (context.Surrounded) return "его обступили со всех сторон";
                    if (context.MaxHP > 0f && context.CurrentHP < context.MaxHP * 0.3f)
                        return "на нём нет живого места";
                    if (context.NearbyEnemies >= 3) return "их слишком много";
                    return "ему страшно";

                case "Pride":
                    if (decision.Action == ActionType.Flee) return "он скорее ляжет, чем побежит";
                    if (context.TargetBackExposed) return "он не бьёт в спину";
                    if (context.Fatigue > 0.3f && decision.Action != ActionType.Idle)
                        return "он не признаёт, что устал";
                    if (decision.RefusedCommand) return "он не привык, чтобы им распоряжались";
                    if (context.LastAlive) return "он остался один и не собирается уходить";
                    return "он не может позволить себе выглядеть слабым";

                case "Envy":
                    if (context.RelationshipWithCommander < 40f)
                        return "он не считает командира выше себя";
                    return "он не хочет, чтобы это досталось кому-то другому";

                case "Lust":
                    if (context.BrotherNearby) return "он не бросит своего";
                    return "он видит то, чего хочет, и больше ничего не слышит";

                case "Gluttony":
                    return "он тащит всё, до чего дотянется";

                case "Sloth":
                    if (context.IsExhausted) return "он выдохся и больше не может";
                    if (context.Fatigue > 0.4f) return "силы у него на исходе";
                    return "у него не осталось воли";

                case "Patience":
                    return "он ждёт удобной минуты";

                case "Loyalty":
                    if (context.RelationshipWithCommander > 70f) return "он верит командиру";
                    return "приказ есть приказ";

                case "Engagement":
                    return "уйти отсюда — значит подставить спину";

                case "Morality":
                    if (warrior.Soul != null && warrior.Soul.Moral == MoralType.Pious)
                        return "иначе он не может";
                    if (warrior.Soul != null && warrior.Soul.Moral == MoralType.Vicious)
                        return "чужая беда его не касается";
                    return "он поступает как привык";

                case "Memory":
                    return "он помнит, чем это кончилось в прошлый раз";

                case "Virtue":
                    return "он не привык проходить мимо";

                default:
                    return "";
            }
        }

        // ---------- действие ----------

        private static string Verb(ActionType action, DecisionContext context)
        {
            switch (action)
            {
                case ActionType.Attack: return "идёт в драку";
                case ActionType.SaveAlly:
                    return context.TargetWarrior != null
                        ? $"бросается к {context.TargetWarrior.DisplayName}"
                        : "бросается к раненому";
                case ActionType.Loot: return "идёт за добычей";
                case ActionType.Flee: return "отходит";
                case ActionType.Idle: return "стоит на месте";
                case ActionType.ObeyCommand: return "делает, как велено";
                default: return "действует по-своему";
            }
        }

        private static string VerbPast(ActionType action, DecisionContext context)
        {
            switch (action)
            {
                case ActionType.Attack: return "пошёл в драку";
                case ActionType.SaveAlly:
                    return context.TargetWarrior != null
                        ? $"бросился к {context.TargetWarrior.DisplayName}"
                        : "бросился к раненому";
                case ActionType.Loot: return "пошёл за добычей";
                case ActionType.Flee: return "отступил";
                case ActionType.Idle: return "остался на месте";
                case ActionType.ObeyCommand: return "сделал, как велено";
                default: return "поступил по-своему";
            }
        }

        /// <summary>Существительное для описания колебания.</summary>
        private static string Noun(ActionType action)
        {
            switch (action)
            {
                case ActionType.Attack: return "Драка";
                case ActionType.SaveAlly: return "Раненый товарищ";
                case ActionType.Loot: return "Добыча";
                case ActionType.Flee: return "Отход";
                case ActionType.Idle: return "Покой";
                case ActionType.ObeyCommand: return "Приказ";
                default: return "Что-то ещё";
            }
        }
    }
}
