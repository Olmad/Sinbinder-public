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
