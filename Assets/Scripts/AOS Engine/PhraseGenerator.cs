// Assets/Scripts/AOS Engine/PhraseGenerator.cs
// Перевод: текст через Loc
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
        /// Громкость, ниже которой отказ объясняется расстоянием. У самой
        /// ближней границы «издали» причина ещё не в нём.
        /// </summary>
        private const float DistantOrder = 0.75f;

        /// <summary>
        /// Подсказка при наведении, настоящее время. Одно-два предложения.
        /// </summary>
        public static string Explain(Warrior warrior, DecisionContext context, Decision decision)
        {
            if (warrior == null) return "";
            string name = Loc.Name(warrior.DisplayName);

            if (decision.Hesitated)
            {
                // Колебание при приказе, взвешенное «от противного»: названа
                // причина, без которой воин бы послушался, — её игрок и чинит.
                // Голос бывает и без слов (смирение ниже) — тогда как без причины.
                string cause = Caused(decision) ? Reason(warrior, context, decision) : "";
                if (!string.IsNullOrEmpty(cause))
                    return Loc.F("{0} колеблется: {1}.", name, cause);

                // Когда кандидат один, BehaviourResolver кладёт его же
                // и во второе поле (alone ? best : sorted[1]). Фраза
                // выходила «Покой и Покой тянут его почти поровну» —
                // игрок видит бессмыслицу вместо объяснения.
                // Фраза целиком, а не склейкой из кусков: переводчику нужен
                // весь смысл, а «его» у неё — «её» (Grammar.For).
                if (decision.RunnerUp == decision.TopContender)
                    return Core.Grammar.For(warrior.Gender,
                        Loc.F("{0} медлит. {1} тянет его, но не настолько, чтобы решиться.",
                              name, Noun(decision.TopContender)));

                return Core.Grammar.For(warrior.Gender,
                    Loc.F("{0} колеблется. {1} и {2} тянут его почти поровну.",
                          name, Noun(decision.TopContender), Noun(decision.RunnerUp)));
            }

            string what = Verb(decision.Action, context);
            string why = Reason(warrior, context, decision);

            if (decision.RefusedCommand)
                return string.IsNullOrEmpty(why)
                    ? Loc.F("Приказ был. {0} {1}.", name, what)
                    : Loc.F("Приказ был. {0} {1} — {2}.", name, what, why);

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
            string name = Loc.Name(warrior.DisplayName);

            if (decision.Hesitated)
            {
                string stuck = Core.Grammar.Pick(warrior.Gender,
                    Loc.F("{0} не сдвинулся с места", name), Loc.F("{0} не сдвинулась с места", name));
                string cause = Caused(decision) ? Reason(warrior, context, decision) : "";
                if (!string.IsNullOrEmpty(cause)) return $"{stuck}: {cause}.";
                return Core.Grammar.Pick(warrior.Gender,
                    Loc.F("{0} не сдвинулся с места — не смог выбрать.", name),
                    Loc.F("{0} не сдвинулась с места — не смогла выбрать.", name));
            }

            string why = Reason(warrior, context, decision);
            string what = VerbPast(decision.Action, context, warrior.Gender);

            if (decision.RefusedCommand)
            {
                string failed = Core.Grammar.Pick(warrior.Gender,
                    Loc.F("{0} не выполнил приказ", name), Loc.F("{0} не выполнила приказ", name));
                return string.IsNullOrEmpty(why)
                    ? Loc.F("{0}. Вместо этого {1}.", failed, what)
                    : Loc.F("{0}: {1}. Вместо этого {2}.", failed, why, what);
            }

            return string.IsNullOrEmpty(why) ? $"{name} {what}." : $"{name} {what}: {why}.";
        }

        // ---------- причина ----------

        /// <summary>Взвешено «от противного» и причина нашлась — причиной или голосом.</summary>
        private static bool Caused(Decision decision)
            => decision.Weighed
            && (decision.Decisive != Counterfactual.Factor.None || !string.IsNullOrEmpty(decision.DecisiveVoice));

        /// <summary>
        /// Голая причина, без имени и без действия: «их слишком много».
        ///
        /// Публично ради подписи момента — она ставит причину второй
        /// строкой под словом, и имя ей не нужно: камера уже стоит
        /// на том, о ком речь.
        /// </summary>
        public static string Reason(Warrior warrior, DecisionContext context, Decision decision)
        {
            // Причина пишется в мужском роде, а род наводится один раз
            // на выходе. Местоимения — закрытый набор, менять их
            // механически надёжно; глаголы, которые несут род, написаны
            // ниже парами. См. Core/Grammar.cs.
            var gender = warrior != null ? warrior.Gender : Core.Gender.Male;
            return Core.Grammar.For(gender, Because(warrior, context, decision, gender));
        }

        private static readonly string Pocket = Loc.N("ему есть что терять — карман не пустой");

        private static string Because(Warrior warrior, DecisionContext context,
                                      Decision decision, Core.Gender gender)
        {
            // Положения может не быть: причину спрашивают и там, где бой
            // уже кончился. Тогда причины нет — и это ответ, а не сбой.
            if (context == null) return "";

            string P(string he, string she) => Core.Grammar.Pick(gender, he, she);

            // Объяснение «от противного» (Counterfactual, docs/35-CRITIQUE.md
            // §3): названа причина, без которой приказ был бы исполнен.
            // Взвешено, но ни одна поодиночке не решает — решил характер:
            // ниже, голосом души, без особых правил о дали и кармане и без
            // строк о положении — пересчёт их уже проверил, и они не решили.
            bool weighed = decision.Weighed;
            if (weighed && decision.Decisive != Counterfactual.Factor.None)
            {
                // Даль звучит словами греха — гордеца, лентяя, — только если
                // шкала на стороне греха. Главный грех — шкала дальше всех
                // от нуля, знак ему не важен; а смиренного окрик издали
                // не задевает, и усердный глухим не прикидывается.
                var own = warrior != null ? warrior.Soul : null;
                var sin = own != null && own.Get(own.Sin) > 0f ? own.Sin : (SinType?)null;
                string main = Counterfactual.Phrase(decision.Decisive, context, sin, gender);
                return decision.DecisiveAlso == Counterfactual.Factor.None
                    ? main
                    : Loc.F("{0}, и {1}", main, Counterfactual.Phrase(decision.DecisiveAlso, context, sin, gender));
            }

            // Голос Греховода: отказ приказу, пришедшему издали. Причина —
            // та, которую игрок может исправить ногами (docs/31-VOICE.md).
            //
            // Расстоянием объясняем только там, где победил голос, который
            // громкость и читает: гордыня и уныние. Бросившийся к раненому
            // бросился бы и в упор — стенд показал это сразу (одна доля
            // на любой громкости), и «приказ пришёл издали» было бы враньём.
            if (!weighed && decision.RefusedCommand && context.CommandVolume < DistantOrder)
            {
                // Смиренный слушает и далёкого: его Гордыня громче всех
                // за то, что он сделал вместо приказа, — это ниже, её словами.
                if (decision.TopModule == "Pride" && !Meek(warrior))
                    return Loc.T("приказ крикнули издали, а он не из тех, кого зовут криком");
                if (decision.TopModule == "Sloth" && !Virtuous(warrior, SinType.Sloth))
                    return P(Loc.T("он сделал вид, что не расслышал"),
                             Loc.T("она сделала вид, что не расслышала"));
            }

            // Личный карман (docs/34-GEAR.md §9.3): жадный с непустым
            // карманом не пошёл в драку и остался стоять. Громче всех за
            // «стоять» тут обычно усталость, но решил карман: без него
            // жадный отказывает «бей» бегством, а не стоянием, — стенд
            // (КАРМАН) показывает, что стоячие отказы приходят с карманом.
            if (!weighed && decision.RefusedCommand && context.CommandIntoFight && context.PocketGold > 0
                && decision.Action == ActionType.Idle
                && warrior != null && warrior.Soul != null && warrior.Soul.Sin == SinType.Greed)
                return Loc.T(Pocket);

            // Второй круг «от противного»: решил голос души — его слова.
            string voice = weighed && !string.IsNullOrEmpty(decision.DecisiveVoice)
                ? decision.DecisiveVoice : decision.TopModule;

            // Добродетель — та же шкала со знаком минус, и голос греха у неё
            // голосует наоборот (стенд, ДОБРОДЕТЕЛЬ): щедрый бросается
            // к раненому, а журнал писал «он думает о своей доле». Слова
            // каждой половины — ниже, у Гордыни — Humility.
            var did = decision.Hesitated ? decision.TopContender : decision.Action;

            switch (voice)
            {
                case "Greed":
                    if (Virtuous(warrior, SinType.Greed)) return Generosity(did, context);
                    // У долга теперь есть ступени, и у каждой свой голос.
                    // Пока Жадность отказывалась только на третьей невыплате,
                    // хватало двух строк; теперь воин может отказать и на
                    // второй, и объяснение обязано это различать — иначе
                    // игрок услышит «третью» там, где задолжали две.
                    if (weighed) return Loc.T("он думает о своей доле");
                    if (context.UnpaidMissions > 3) return Loc.T("ему не платили вылазку за вылазкой");
                    if (context.UnpaidMissions == 3) return Loc.T("ему не платили третью вылазку подряд");
                    if (context.UnpaidMissions == 2) return Loc.T("ему не платили вторую вылазку подряд");
                    if (context.UnpaidMissions > 0) return Loc.T("ему до сих пор не заплатили");
                    if (context.NearbyLoot > 0) return Loc.T("добыча лежала слишком близко");
                    if (context.PocketGold > 0) return Loc.T(Pocket);
                    return Loc.T("он думает о своей доле");

                case "Wrath":
                    if (Virtuous(warrior, SinType.Wrath)) return Meekness(did);
                    return Loc.T("он не умеет стоять, когда есть кого ударить");

                case "Fear":
                    if (weighed) return context.NearbyEnemies >= 3 ? Loc.T("их слишком много") : Loc.T("ему страшно");
                    if (context.Surrounded) return Loc.T("его обступили со всех сторон");
                    if (context.MaxHP > 0f && context.CurrentHP < context.MaxHP * 0.3f)
                        return Loc.T("на нём нет живого места");
                    if (context.NearbyEnemies >= 3) return Loc.T("их слишком много");
                    return Loc.T("ему страшно");

                case "Pride":
                    // Смирение — та же шкала со знаком минус, и голос у неё
                    // обратный: гордыня держит в бою, смирение отпускает
                    // (PrideModule). За отход, за раненого, за послушание
                    // Гордыня громче всех бывает только у смиренного, и слова
                    // гордеца врали за него наоборот: Марга-зомби (тело
                    // отнимает Гордыню) отходил, а журнал писал «он скорее
                    // ляжет, чем побежит. Вместо этого отступил» — стенд,
                    // ГОРДЫНЯ И СМИРЕНИЕ.
                    if (Meek(warrior))
                        return Humility(decision.Hesitated ? decision.TopContender : decision.Action);

                    // Гордец за отход не голосует. Назван он, когда решило
                    // «от противного»: без гордыни приказ был бы исполнен —
                    // задел приказ, а побежал он по другой причине.
                    if (decision.Action == ActionType.Flee)
                        return decision.RefusedCommand
                            ? P(Loc.T("он не привык к чужим приказам"), Loc.T("она не привыкла к чужим приказам"))
                            : "";

                    // Велели уйти из схватки — вот где эти слова правда:
                    // унижает гордеца приказ отойти, когда враг уже в шаге.
                    if (decision.RefusedCommand && context.IsEngaged && context.CommandLeavesFight)
                        return Loc.T("он скорее ляжет, чем побежит");

                    if (context.TargetBackExposed) return Loc.T("он не бьёт в спину");
                    if (context.Fatigue > 0.3f && decision.Action != ActionType.Idle)
                        return Loc.T("он не признаёт, что устал");
                    // «Им» здесь было творительным от «он», а у этого
                    // слова два разных хозяина, и по строке их не различить.
                    // Фраза переписана так, чтобы его не было.
                    if (decision.RefusedCommand)
                        return P(Loc.T("он не привык к чужим приказам"),
                                 Loc.T("она не привыкла к чужим приказам"));
                    if (context.LastAlive)
                        return P(Loc.T("он остался один и не собирается уходить"),
                                 Loc.T("она осталась одна и не собирается уходить"));
                    return Loc.T("он не может позволить себе выглядеть слабым");

                case "Envy":
                    // Доброжелательность голосует против того же, за что
                    // зависть, — и ни за что сверх того: причины нет.
                    if (Virtuous(warrior, SinType.Envy)) return "";
                    if (context.RelationshipWithCommander < 40f)
                        return Loc.T("он не считает командира выше себя");
                    return Loc.T("он не хочет, чтобы это досталось кому-то другому");

                case "Lust":
                    if (context.BrotherNearby) return Loc.T("он не бросит своего");
                    if (Virtuous(warrior, SinType.Lust))
                        return did == ActionType.ObeyCommand ? Loc.T("чужое добро его не тянет") : "";
                    return Loc.T("он видит то, чего хочет, и больше ничего не слышит");

                case "Gluttony":
                    if (Virtuous(warrior, SinType.Gluttony))
                        return did == ActionType.Attack ? Loc.T("лишнего ему не нужно — только дело") : "";
                    return Loc.T("он тащит всё, до чего дотянется");

                case "Sloth":
                    if (Virtuous(warrior, SinType.Sloth)) return Diligence(did, context, P);
                    if (weighed) return Loc.T("у него не осталось воли");
                    if (context.IsExhausted)
                        return P(Loc.T("он выдохся и больше не может"),
                                 Loc.T("она выдохлась и больше не может"));
                    if (context.Fatigue > 0.4f) return Loc.T("силы у него на исходе");
                    return Loc.T("у него не осталось воли");

                case "Patience":
                    return Loc.T("он ждёт удобной минуты");

                case "Loyalty":
                    if (context.RelationshipWithCommander > 70f) return Loc.T("он верит командиру");
                    return Loc.T("приказ есть приказ");

                case "Engagement":
                    return Loc.T("уйти отсюда — значит подставить спину");

                case "Morality":
                    if (warrior.Soul != null && warrior.Soul.Moral == MoralType.Pious)
                        return Loc.T("иначе он не может");
                    if (warrior.Soul != null && warrior.Soul.Moral == MoralType.Vicious)
                        return Loc.T("чужая беда его не касается");
                    return Loc.T("он поступает как привык");

                case "Memory":
                    return Loc.T("он помнит, чем это кончилось в прошлый раз");

                case "Virtue":
                    return Loc.T("он не привык проходить мимо");

                default:
                    return "";
            }
        }

        /// <summary>
        /// Шкала греха у него со знаком минус — добродетель: щедрость,
        /// кротость, доброжелательность, сдержанность, умеренность, усердие.
        /// Голос греха за такую душу голосует наоборот, и слова порока за неё
        /// врут. Гордыня со смирением — отдельно (<see cref="Meek"/>).
        /// </summary>
        private static bool Virtuous(Warrior warrior, SinType sin)
            => warrior != null && warrior.Soul != null && warrior.Soul.Get(sin) < 0f;

        /// <summary>
        /// Голос Жадности у щедрого (GreedModule): за спасение своего и за
        /// драку, не считая цены. Долг он всё равно слышит — меньше жадного,
        /// но слышит, — и добыча рядом тянет любого: это слова о положении,
        /// они правдивы при любом знаке.
        /// </summary>
        private static string Generosity(ActionType action, DecisionContext context)
        {
            if (context.UnpaidMissions > 3) return Loc.T("ему не платили вылазку за вылазкой");
            if (context.UnpaidMissions == 3) return Loc.T("ему не платили третью вылазку подряд");
            if (context.UnpaidMissions == 2) return Loc.T("ему не платили вторую вылазку подряд");
            if (context.UnpaidMissions > 0) return Loc.T("ему до сих пор не заплатили");

            switch (action)
            {
                case ActionType.Loot:     return context.NearbyLoot > 0 ? Loc.T("добыча лежала слишком близко") : "";
                case ActionType.SaveAlly: return Loc.T("ему для своих ничего не жалко");
                case ActionType.Attack:   return Loc.T("он не считает, чего ему это будет стоить");
                default:                  return "";
            }
        }

        /// <summary>Голос Гнева у кроткого (WrathModule): драки он не ищет.</summary>
        private static string Meekness(ActionType action)
        {
            switch (action)
            {
                case ActionType.Flee: return Loc.T("драться ему не по сердцу");
                case ActionType.Idle: return Loc.T("он не ищет драки");
                default:              return "";
            }
        }

        /// <summary>
        /// Голос Уныния у усердного (SlothModule). За работу — за удар,
        /// за приказ, за своего — он голосует сам. А стоять и отходить
        /// его толкает не лень, а положение: усталость и опасность Уныние
        /// читает при любом знаке. «Не осталось воли» у усердного — неправда.
        /// </summary>
        private static string Diligence(ActionType action, DecisionContext context,
                                        System.Func<string, string, string> P)
        {
            switch (action)
            {
                case ActionType.Attack:      return P(Loc.T("он не привык сидеть без дела"), Loc.T("она не привыкла сидеть без дела"));
                case ActionType.ObeyCommand: return Loc.T("работа его не пугает");
                case ActionType.SaveAlly:    return Loc.T("ему не лень помочь своим");
                case ActionType.Idle:
                    if (context.IsExhausted)
                        return P(Loc.T("он выдохся и больше не может"), Loc.T("она выдохлась и больше не может"));
                    return context.Fatigue > 0.2f ? Loc.T("силы у него на исходе") : "";
                case ActionType.Flee:
                    return context.DangerLevel > 0.6f
                        || (context.MaxHP > 0f && context.CurrentHP < context.MaxHP * 0.4f)
                        ? Loc.T("здесь уже не выстоять") : "";
                default: return "";
            }
        }

        /// <summary>
        /// Гордыня у него со знаком минус — смирение, и голос Гордыни
        /// за него голосует наоборот.
        /// </summary>
        private static bool Meek(Warrior warrior)
            => warrior != null && warrior.Soul != null && warrior.Soul.Get(SinType.Pride) < 0f;

        /// <summary>
        /// Голос Гордыни у смиренного: гордость не держит его ни в бою,
        /// ни над раненым, ни над приказом. Остального смирение не толкает —
        /// там причины нет, и это ответ, а не сбой.
        /// </summary>
        private static string Humility(ActionType action)
        {
            switch (action)
            {
                case ActionType.Flee:        return Loc.T("уйти из боя ему не стыдно");
                case ActionType.SaveAlly:    return Loc.T("чужая жизнь для него не дешевле своей");
                case ActionType.ObeyCommand: return Loc.T("подчиниться ему не зазорно");
                default:                     return "";
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
                return Loc.F("бросается к {0}", Core.Grammar.Dative(context.TargetWarrior.DisplayName, context.TargetWarrior.Gender));

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
                    return context != null && context.HasCommand ? Loc.T("Отходит") : Loc.T("Сбегает");

                case ActionType.SaveAlly:     return Loc.T("Спасает");
                case ActionType.Loot:         return Loc.T("Грабит");
                case ActionType.AcceptBribe:  return Loc.T("Предаёт");
                case ActionType.BribeEnemy:   return Loc.T("Торгуется");
                case ActionType.Devour:       return Loc.T("Жрёт");
                case ActionType.Berserk:      return Loc.T("Звереет");
                case ActionType.LastStand:    return Loc.T("Насмерть");
                case ActionType.DuelChallenge: return Loc.T("Вызывает");
                case ActionType.Sacrifice:    return Loc.T("Закрывает собой");
                case ActionType.StealWeapon:  return Loc.T("Ворует");
                case ActionType.Charm:        return Loc.T("Морочит");
                case ActionType.EternalSleep: return Loc.T("Спит");

                // Рядовое подписи не получает: оно и не объявляется.
                default: return null;
            }
        }

        public static string Doing(ActionType action)
        {
            switch (action)
            {
                // Базовые
                case ActionType.Attack: return Loc.T("идёт в драку");
                case ActionType.SaveAlly: return Loc.T("бросается к раненому");
                case ActionType.Loot: return Loc.T("идёт за добычей");
                case ActionType.Flee: return Loc.T("отходит");
                case ActionType.Idle: return Loc.T("стоит на месте");
                case ActionType.ObeyCommand: return Loc.T("делает, как велено");

                // Гнев
                case ActionType.Berserk: return Loc.T("впадает в бешенство");
                case ActionType.PowerStrike: return Loc.T("бьёт со всей силы");

                // Терпение
                case ActionType.IronStance: return Loc.T("встаёт железной стойкой");
                case ActionType.CounterAttack: return Loc.T("ждёт удара, чтобы ответить");
                case ActionType.SecondWind: return Loc.T("переводит дыхание");
                case ActionType.Unshakable: return Loc.T("стоит несдвигаемо");

                // Уныние
                case ActionType.Yawn: return Loc.T("зевает");
                case ActionType.LazyHeal: return Loc.T("лениво зализывает раны");
                case ActionType.AuraOfApathy: return Loc.T("заражает всех безразличием");
                case ActionType.EternalSleep: return Loc.T("засыпает намертво");

                // Усердие
                case ActionType.WorkSurge: return Loc.T("работает за троих");
                case ActionType.WorkInspiration: return Loc.T("подгоняет остальных");
                case ActionType.Tireless: return Loc.T("не знает усталости");

                // Похоть
                case ActionType.Charm: return Loc.T("очаровывает");
                case ActionType.KissOfDeath: return Loc.T("целует насмерть");
                case ActionType.Seduce: return Loc.T("переманивает на свою сторону");
                case ActionType.FatalPassion: return Loc.T("сгорает от страсти");

                // Чревоугодие
                case ActionType.Devour: return Loc.T("пожирает");
                case ActionType.Vomit: return Loc.T("извергает съеденное");
                case ActionType.InsatiableHunger: return Loc.T("не может насытиться");

                // Подкуп и предательство. Дописано по следу замера
                // (Tools/bench → МОМЕНТЫ): эти шесть объявляются как
                // поступки, а слов у них не было — заглушка «действует
                // по-своему» накрывала в том числе переход к врагу,
                // самое громкое, что вообще умеет движок.
                case ActionType.BribeEnemy: return Loc.T("торгуется с чужим");
                case ActionType.AcceptBribe: return Loc.T("уходит к чужим");

                // Гордыня
                case ActionType.DuelChallenge: return Loc.T("зовёт на поединок");
                case ActionType.LastStand: return Loc.T("встаёт насмерть");
                case ActionType.HeroicPose: return Loc.T("становится в позу");
                case ActionType.Inspiration: return Loc.T("поднимает остальных");

                // Смирение
                case ActionType.Sacrifice: return Loc.T("закрывает собой");

                // Зависть
                case ActionType.StealWeapon: return Loc.T("тянет чужое оружие");

                default: return Loc.T("действует по-своему");
            }
        }

        /// <summary>
        /// Что он сделал, в прошедшем: «сбежал», «пошёл за добычей».
        ///
        /// Публично ради пересказа вылазки, которую игрок не видел.
        /// Положения там нет — передаётся null, и это правильный ответ:
        /// приказов на вылазке не отдают, значит отход был побегом.
        /// </summary>
        public static string Did(ActionType action, DecisionContext context,
                                 Core.Gender gender = Core.Gender.Male)
            => VerbPast(action, context, gender);

        private static string VerbPast(ActionType action, DecisionContext context,
                                       Core.Gender gender)
        {
            // Обе формы на одной строке. Правило по окончанию тут
            // не годится: «пошёл» даёт «пошла», а «лёг» — «легла»,
            // и почти работающее правило выдаёт «пошёла» молча.
            string P(string he, string she) => Core.Grammar.Pick(gender, he, she);

            switch (action)
            {
                case ActionType.Attack: return P(Loc.T("пошёл в драку"), Loc.T("пошла в драку"));

                // Положения может не быть вовсе: пересказ вылазки знает
                // действие, но не знает, кого спасали, — бой уже кончился.
                // Раньше эта строка падала на null, и падала бы только
                // там, куда ни один прогон до сих пор не заходил.
                case ActionType.SaveAlly:
                    return context != null && context.TargetWarrior != null
                        ? P(Loc.F("бросился к {0}", Core.Grammar.Dative(context.TargetWarrior.DisplayName, context.TargetWarrior.Gender)),
                            Loc.F("бросилась к {0}", Core.Grammar.Dative(context.TargetWarrior.DisplayName, context.TargetWarrior.Gender)))
                        : P(Loc.T("бросился к раненому"), Loc.T("бросилась к раненому"));

                case ActionType.Loot: return P(Loc.T("пошёл за добычей"), Loc.T("пошла за добычей"));

                // Отход и побег — разные вещи, и разница ровно в том,
                // просили его об этом или нет. Отступить по приказу —
                // манёвр; развернуться и уйти, когда никто не велел, —
                // побег, и называть их одним словом значит прятать
                // от игрока именно то, ради чего здесь движок решений.
                case ActionType.Flee:
                    return context != null && context.HasCommand
                         ? P(Loc.T("отступил"), Loc.T("отступила"))
                         : P(Loc.T("сбежал"), Loc.T("сбежала"));

                case ActionType.Idle: return P(Loc.T("остался на месте"), Loc.T("осталась на месте"));
                case ActionType.ObeyCommand: return P(Loc.T("сделал, как велено"), Loc.T("сделала, как велено"));

                // Дописано по следу замера (Tools/bench → МОМЕНТЫ):
                // эти поступки объявляются, а слов у них не было.
                case ActionType.BribeEnemy: return P(Loc.T("торговался с чужим"), Loc.T("торговалась с чужим"));
                case ActionType.AcceptBribe: return P(Loc.T("ушёл к чужим"), Loc.T("ушла к чужим"));
                case ActionType.DuelChallenge: return P(Loc.T("позвал на поединок"), Loc.T("позвала на поединок"));
                case ActionType.LastStand: return P(Loc.T("встал насмерть"), Loc.T("встала насмерть"));
                case ActionType.Sacrifice: return P(Loc.T("закрыл собой"), Loc.T("закрыла собой"));
                case ActionType.StealWeapon: return P(Loc.T("потянул чужое оружие"), Loc.T("потянула чужое оружие"));
                case ActionType.Berserk: return P(Loc.T("впал в бешенство"), Loc.T("впала в бешенство"));
                case ActionType.Devour: return P(Loc.T("сожрал"), Loc.T("сожрала"));
                case ActionType.Charm: return P(Loc.T("очаровал"), Loc.T("очаровала"));
                case ActionType.EternalSleep: return P(Loc.T("уснул намертво"), Loc.T("уснула намертво"));

                default: return P(Loc.T("поступил по-своему"), Loc.T("поступила по-своему"));
            }
        }

        /// <summary>Существительное для описания колебания.</summary>
        private static string Noun(ActionType action)
        {
            switch (action)
            {
                case ActionType.Attack: return Loc.T("Драка");
                case ActionType.SaveAlly: return Loc.T("Раненый товарищ");
                case ActionType.Loot: return Loc.T("Добыча");
                case ActionType.Flee: return Loc.T("Отход");
                case ActionType.Idle: return Loc.T("Покой");
                case ActionType.ObeyCommand: return Loc.T("Приказ");
                default: return Loc.T("Что-то ещё");
            }
        }
    }
}
