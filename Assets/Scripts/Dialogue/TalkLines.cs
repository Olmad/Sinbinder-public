// Assets/Scripts/Dialogue/TalkLines.cs
// Перевод: текст через Loc
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Dialogue
{
    /// <summary>
    /// Что воин отвечает Греховоду на «как ты?» — первая строка разговора
    /// от первого лица (F, <see cref="UI.GearPanel"/>). Автор, 24 сентября:
    /// «взаимодействовать с воином от первого лица — открывая диалог,
    /// и в нём можно посмотреть снаряжение и инвентарь».
    ///
    /// Строка по той стороне шкалы, что сильнее всего (<see cref="SoulData.Sin"/>):
    /// порок отвечает пороком, добродетель — добродетелью. Щедрый не скажет
    /// «была бы доля». Семь шкал — четырнадцать ответов, без жребия: одна
    /// душа всегда отвечает одно и то же, и по ответу её узнают.
    /// </summary>
    public static class TalkLines
    {
        /// <summary>
        /// Что воин помнит о Греховоде — самое сильное из свежего: отнятое
        /// или подаренное (<see cref="SquadGear"/> пишет это в память).
        /// Отношения вычисляются из памяти, но игрок видел их только через
        /// итог голосования; здесь память говорит сама (docs/35-CRITIQUE.md
        /// п. 10). Пусто — помнить нечего.
        /// </summary>
        public static string Remembers(Warrior w)
        {
            var memory = AOS.MemoryProcessor.Instance;
            if (memory == null || w == null || !SinbinderPlayer.Exists) return "";

            string him = SinbinderPlayer.Instance.Id;
            AOS.MemoryRecord best = null;
            foreach (var m in memory.GetMemories(w))
            {
                if (m == null || m.TargetID != him) continue;
                if (m.EventType != "SinbinderTookFromMe" && m.EventType != "SinbinderGaveMe") continue;
                if (best == null || System.Math.Abs(m.Strength) > System.Math.Abs(best.Strength)) best = m;
            }
            if (best == null) return "";

            return best.EventType == "SinbinderTookFromMe"
                ? Loc.T("Ты забрал моё. Я помню.")
                : Grammar.Pick(w.Gender, Loc.T("Ты дал мне — я не забыл."), Loc.T("Ты дал мне — я не забыла."));
        }

        /// <summary>
        /// Первая строка разговора — пока игрок выбирает, что сказать (меню
        /// разговора, решение автора 30 сентября: «как в Skyrim или Fallout 4»).
        /// Должник начинает с долга: о чём он думает, то и говорит первым.
        /// </summary>
        public static string Greet(Warrior w)
        {
            if (w.UnpaidMissions >= 2) return Loc.T("Владыка. Если о монетах — я слушаю.");

            switch (w.Soul.Sin)
            {
                case SinType.Pride:    return Loc.T("Слушаю.");
                case SinType.Wrath:    return Loc.T("Что? Кого бить?");
                case SinType.Sloth:    return Loc.T("М? Я тут.");
                default:               return Loc.T("Владыка?");
            }
        }

        /// <summary>
        /// «Как ты? Как ко мне?» — как он и как он к Греховоду. Отношение не
        /// хранится, а вычисляется (правило проекта): из верности, долга
        /// и того, что он помнит. Словами, без чисел.
        /// </summary>
        public static string Attitude(Warrior w)
        {
            string toMe;
            if (w.UnpaidMissions >= 4) toMe = Loc.T("Давно без платы, владыка. Сами считайте, как я к вам.");
            else if (w.UnpaidMissions == 3) toMe = Loc.T("Третью вылазку без платы, владыка. Сами считайте, как я к вам.");
            else if (w.UnpaidMissions == 2) toMe = Loc.T("Две вылазки без платы. Я пока молчу.");
            else if (w.UnpaidMissions == 1) toMe = Loc.T("За прошлую вылазку не заплачено. Помню.");
            else if (w.Loyalty >= 80f) toMe = Loc.T("За вами — куда скажете.");
            else if (w.Loyalty >= 50f) toMe = Loc.T("Служу. Пока дело идёт — служу.");
            else toMe = Loc.T("Служу, пока есть за что.");

            string said = HowAreYou(w) + " " + toMe;
            string memory = Remembers(w);
            return string.IsNullOrEmpty(memory) ? said : said + " " + memory;
        }

        /// <summary>
        /// Что говорит воин, когда Греховод отдал долг из рук в руки. По греху:
        /// жадный считает, гордый делает вид, что не ради денег.
        /// </summary>
        public static string Paid(Warrior w, int owed = 1)
        {
            var soul = w.Soul;
            bool virtue = soul.Get(soul.Sin) < 0f;
            string P(string he, string she) => Grammar.Pick(w.Gender, he, she);

            // Одна плата снимает долг за несколько вылазок: Греховод удерживал
            // плату в наказание, и отдать одну — снять наказание (решение
            // автора, 30 сентября). Должник говорит это сам — иначе «третью
            // вылазку без платы» закрывала бы молча одна монета.
            if (owed >= 2)
            {
                if (!virtue && soul.Sin == SinType.Greed)
                {
                    if (owed == 2) return Loc.T("Одна за две? …Ладно. Наказание кончилось — и то хлеб.");
                    if (owed == 3) return Loc.T("Одна за три? …Ладно. Наказание кончилось — и то хлеб.");
                    return Loc.T("Одна за все? …Ладно. Наказание кончилось — и то хлеб.");
                }
                return Loc.T("Наказание кончилось. Этого довольно, владыка.");
            }

            if (virtue)
                return soul.Sin == SinType.Greed
                    ? Loc.T("Спасибо. Если кому-то нужнее — скажите, отдам.")
                    : Loc.T("Спасибо, владыка. Я запомню.");

            switch (soul.Sin)
            {
                case SinType.Greed:    return P(Loc.T("Пересчитал. Сходится. Теперь мы в расчёте."),
                                                Loc.T("Пересчитала. Сходится. Теперь мы в расчёте."));
                case SinType.Pride:    return Loc.T("Служу не за деньги. Но раз положено — возьму.");
                case SinType.Wrath:    return Loc.T("Монеты. Лучше бы дали кого ударить. Но спасибо.");
                case SinType.Envy:     return Loc.T("Мне? Раньше других? …Спасибо.");
                case SinType.Lust:     return Loc.T("Потрачу на приятное. Спасибо, владыка.");
                case SinType.Gluttony: return Loc.T("Вечером будет мясо. Спасибо, владыка.");
                default:               return Loc.T("Спасибо. Можно я их просто подержу?");
            }
        }

        public static string HowAreYou(Warrior w)
        {
            var soul = w.Soul;
            var sin = soul.Sin;
            bool virtue = soul.Get(sin) < 0f;

            string P(string he, string she) => Grammar.Pick(w.Gender, he, she);

            if (!virtue)
            {
                switch (sin)
                {
                    case SinType.Greed:    return P(Loc.T("Жив. Была бы доля — был бы и весел."),
                                                    Loc.T("Жива. Была бы доля — была бы и весела."));
                    case SinType.Pride:    return Loc.T("Не хуже прочих. Лучше, если честно.");
                    case SinType.Wrath:    return Loc.T("Скучно. Когда дадут кого-нибудь ударить?");
                    case SinType.Envy:     return Loc.T("Как все. Только у других почему-то лучше.");
                    case SinType.Lust:     return Loc.T("Хорошо, пока есть на кого посмотреть.");
                    case SinType.Gluttony: return P(Loc.T("Сыт был утром. Это было давно."),
                                                    Loc.T("Сыта была утром. Это было давно."));
                    default:               return P(Loc.T("Устал. Можно я постою?"),
                                                    Loc.T("Устала. Можно я постою?"));
                }
            }

            switch (sin)
            {
                case SinType.Greed:    return Loc.T("Хорошо. Если кому-то нужнее — отдам своё.");
                case SinType.Pride:    return Loc.T("Как скажете, так и есть.");
                case SinType.Wrath:    return Loc.T("Спокойно. Подождём — увидим.");
                case SinType.Envy:     return P(Loc.T("Хорошо. Рад, что мы вместе."), Loc.T("Хорошо. Рада, что мы вместе."));
                case SinType.Lust:     return Loc.T("Держусь. Мысли в порядке.");
                case SinType.Gluttony: return P(Loc.T("Сыт малым. Остальное — отряду."), Loc.T("Сыта малым. Остальное — отряду."));
                default:               return P(Loc.T("Готов. Скажите, что делать."), Loc.T("Готова. Скажите, что делать."));
            }
        }
    }
}
