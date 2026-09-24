// Assets/Scripts/Dialogue/TalkLines.cs
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
                    case SinType.Greed:    return P("Жив. Была бы доля — был бы и весел.",
                                                    "Жива. Была бы доля — была бы и весела.");
                    case SinType.Pride:    return "Не хуже прочих. Лучше, если честно.";
                    case SinType.Wrath:    return "Скучно. Когда дадут кого-нибудь ударить?";
                    case SinType.Envy:     return "Как все. Только у других почему-то лучше.";
                    case SinType.Lust:     return "Хорошо, пока есть на кого посмотреть.";
                    case SinType.Gluttony: return P("Сыт был утром. Это было давно.",
                                                    "Сыта была утром. Это было давно.");
                    default:               return P("Устал. Можно я постою?",
                                                    "Устала. Можно я постою?");
                }
            }

            switch (sin)
            {
                case SinType.Greed:    return "Хорошо. Если кому-то нужнее — отдам своё.";
                case SinType.Pride:    return "Как скажете, так и есть.";
                case SinType.Wrath:    return "Спокойно. Подождём — увидим.";
                case SinType.Envy:     return P("Хорошо. Рад, что мы вместе.", "Хорошо. Рада, что мы вместе.");
                case SinType.Lust:     return "Держусь. Мысли в порядке.";
                case SinType.Gluttony: return P("Сыт малым. Остальное — отряду.", "Сыта малым. Остальное — отряду.");
                default:               return P("Готов. Скажите, что делать.", "Готова. Скажите, что делать.");
            }
        }
    }
}
