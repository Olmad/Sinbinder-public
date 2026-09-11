// Assets/Scripts/AOS Engine/PhraseGenerator.cs
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
            {
                // Когда кандидат один, BehaviourResolver кладёт его же
                // и во второе поле (alone ? best : sorted[1]). Фраза
                // выходила «Покой и Покой тянут его почти поровну» —
                // игрок видит бессмыслицу вместо объяснения.
                if (decision.RunnerUp == decision.TopContender)
                    return $"{name} медлит. {Noun(decision.TopContender)} тянет его, "
                         + "но не настолько, чтобы решиться.";

                return $"{name} колеблется. {Noun(decision.TopContender)} и "
                     + $"{Noun(decision.RunnerUp)} тянут его почти поровну.";
            }

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
                    // У долга теперь есть ступени, и у каждой свой голос.
                    // Пока Жадность отказывалась только на третьей невыплате,
                    // хватало двух строк; теперь воин может отказать и на
                    // второй, и объяснение обязано это различать — иначе
                    // игрок услышит «третью» там, где задолжали две.
                    if (context.UnpaidMissions > 3) return "ему не платили вылазку за вылазкой";
                    if (context.UnpaidMissions == 3) return "ему не платили третью вылазку подряд";
                    if (context.UnpaidMissions == 2) return "ему не платили вторую вылазку подряд";
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
            // Единственный случай, которому нужны обстоятельства: у кого
            // именно он в ногах. Всё прочее — общий словарь ниже, и держать
            // его надо в одном месте: два списка слов для одних и тех же
            // действий разъезжаются молча.
            if (action == ActionType.SaveAlly && context.TargetWarrior != null)
                return $"бросается к {context.TargetWarrior.DisplayName}";

            return Doing(action);
        }

        /// <summary>
        /// Что он делает — одним глаголом, без обстоятельств.
        ///
        /// Умения названы поимённо, все двадцать, что есть в игре
        /// (<see cref="SkillCatalog"/>). До этого их не называл никто:
        /// воин, выбравший Мощный Удар, показывался игроку как
        /// «действует по-своему» — то есть самое характерное, что он
        /// делает, было единственным, чего нельзя было прочесть.
        /// </summary>
        /// <summary>
        /// Одно-два слова: подпись к моменту, которую читают на бегу.
        ///
        /// Три регистра одних и тех же действий живут в одном файле
        /// намеренно. <see cref="Short"/> — подпись на экране, её читают
        /// краем глаза за полсекунды. <see cref="Doing"/> — что он делает
        /// сейчас, для подсказки при наведении. <see cref="LogLine"/> —
        /// прошедшее время и причина, для журнала. Разнести их по разным
        /// файлам значило бы завести три списка слов для одних действий,
        /// и они разъехались бы на первой же правке.
        ///
        /// Отход и побег различаются и здесь: разница в том, просили
        /// его об этом или нет.
        /// </summary>
        public static string Short(ActionType action, DecisionContext context)
        {
            switch (action)
            {
                case ActionType.Flee:
                    return context != null && context.HasCommand ? "Отходит" : "Сбегает";

                case ActionType.SaveAlly:     return "Спасает";
                case ActionType.Loot:         return "Грабит";
                case ActionType.AcceptBribe:  return "Предаёт";
                case ActionType.BribeEnemy:   return "Торгуется";
                case ActionType.Devour:       return "Жрёт";
                case ActionType.Berserk:      return "Звереет";
                case ActionType.LastStand:    return "Насмерть";
                case ActionType.DuelChallenge: return "Вызывает";
                case ActionType.Sacrifice:    return "Закрывает собой";
                case ActionType.StealWeapon:  return "Ворует";
                case ActionType.Charm:        return "Морочит";
                case ActionType.EternalSleep: return "Спит";

                // Рядовое подписи не получает: оно и не объявляется.
                default: return null;
            }
        }

        public static string Doing(ActionType action)
        {
            switch (action)
            {
                // Базовые
                case ActionType.Attack: return "идёт в драку";
                case ActionType.SaveAlly: return "бросается к раненому";
                case ActionType.Loot: return "идёт за добычей";
                case ActionType.Flee: return "отходит";
                case ActionType.Idle: return "стоит на месте";
                case ActionType.ObeyCommand: return "делает, как велено";

                // Гнев
                case ActionType.Berserk: return "впадает в бешенство";
                case ActionType.PowerStrike: return "бьёт со всей силы";

                // Терпение
                case ActionType.IronStance: return "встаёт железной стойкой";
                case ActionType.CounterAttack: return "ждёт удара, чтобы ответить";
                case ActionType.SecondWind: return "переводит дыхание";
                case ActionType.Unshakable: return "стоит несдвигаемо";

                // Уныние
                case ActionType.Yawn: return "зевает";
                case ActionType.LazyHeal: return "лениво зализывает раны";
                case ActionType.AuraOfApathy: return "заражает всех безразличием";
                case ActionType.EternalSleep: return "засыпает намертво";

                // Усердие
                case ActionType.WorkSurge: return "работает за троих";
                case ActionType.WorkInspiration: return "подгоняет остальных";
                case ActionType.Tireless: return "не знает усталости";

                // Похоть
                case ActionType.Charm: return "очаровывает";
                case ActionType.KissOfDeath: return "целует насмерть";
                case ActionType.Seduce: return "переманивает на свою сторону";
                case ActionType.FatalPassion: return "сгорает от страсти";

                // Чревоугодие
                case ActionType.Devour: return "пожирает";
                case ActionType.Vomit: return "извергает съеденное";
                case ActionType.InsatiableHunger: return "не может насытиться";

                // Подкуп и предательство. Дописано по следу замера
                // (Tools/bench → МОМЕНТЫ): эти шесть объявляются как
                // поступки, а слов у них не было — заглушка «действует
                // по-своему» накрывала в том числе переход к врагу,
                // самое громкое, что вообще умеет движок.
                case ActionType.BribeEnemy: return "торгуется с чужим";
                case ActionType.AcceptBribe: return "уходит к чужим";

                // Гордыня
                case ActionType.DuelChallenge: return "зовёт на поединок";
                case ActionType.LastStand: return "встаёт насмерть";
                case ActionType.HeroicPose: return "становится в позу";
                case ActionType.Inspiration: return "поднимает остальных";

                // Смирение
                case ActionType.Sacrifice: return "закрывает собой";

                // Зависть
                case ActionType.StealWeapon: return "тянет чужое оружие";

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

                // Отход и побег — разные вещи, и разница ровно в том,
                // просили его об этом или нет. Отступить по приказу —
                // манёвр; развернуться и уйти, когда никто не велел, —
                // побег, и называть их одним словом значит прятать
                // от игрока именно то, ради чего здесь движок решений.
                case ActionType.Flee:
                    return context != null && context.HasCommand
                         ? "отступил" : "сбежал";

                case ActionType.Idle: return "остался на месте";
                case ActionType.ObeyCommand: return "сделал, как велено";

                // Дописано по следу замера (Tools/bench → МОМЕНТЫ):
                // эти поступки объявляются, а слов у них не было.
                case ActionType.BribeEnemy: return "торговался с чужим";
                case ActionType.AcceptBribe: return "ушёл к чужим";
                case ActionType.DuelChallenge: return "позвал на поединок";
                case ActionType.LastStand: return "встал насмерть";
                case ActionType.Sacrifice: return "закрыл собой";
                case ActionType.StealWeapon: return "потянул чужое оружие";
                case ActionType.Berserk: return "впал в бешенство";
                case ActionType.Devour: return "сожрал";
                case ActionType.Charm: return "очаровал";
                case ActionType.EternalSleep: return "уснул намертво";

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
