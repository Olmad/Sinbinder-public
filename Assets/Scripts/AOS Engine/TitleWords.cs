// Assets/Scripts/AOS Engine/TitleWords.cs
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Что говорят на церемонии титула.
    ///
    /// Замысел автора от 15 сентября: получающий стоит, отряд вокруг
    /// кричит его имя с титулом, а он отвечает — и в ответе слышен
    /// характер. Образец его же словами:
    ///
    /// <i>«Гроза Охотников? А мне нравится. Отныне я — Гертон Гроза
    /// Охотников!»</i>
    ///
    /// <b>Жребия здесь нет и быть не может.</b> Три ответа — не
    /// <c>Random</c>, а функция от воина: доминирующий грех решает,
    /// каким голосом он принимает имя. Два одинаковых прохода дают
    /// одинаковую церемонию, как и всё остальное в этой игре.
    ///
    /// Грех выбран мерилом не для красоты: титул — это приговор
    /// окружающих (<c>14-HANDOFF.md</c> §28.4), и принимает его каждый
    /// по-своему. Гордый ждал, жадный прикидывает выгоду, ленивый
    /// тяготится.
    /// </summary>
    public static class TitleWords
    {
        /// <summary>
        /// Крик отряда. Коротко: это возглас, а не реплика.
        /// Легендарному кричат иначе — такое имя слышат дальше отряда.
        /// </summary>
        public static string Shout(Warrior warrior, string title, bool legendary)
        {
            if (warrior == null) return title;

            return legendary
                ? $"{title.ToUpperInvariant()}! {warrior.DisplayName.ToUpperInvariant()}!"
                : $"{title}! {warrior.DisplayName}!";
        }

        /// <summary>
        /// Ответ получившего. Три голоса по греху, и четвёртый —
        /// для легендарного: там уже не до характера.
        /// </summary>
        public static string Answer(Warrior warrior, string title, bool legendary)
        {
            if (warrior == null || warrior.Soul == null)
                return $"Теперь я — {title}.";

            string name = warrior.DisplayName;

            if (legendary)
                return $"Меня будут помнить дольше, чем я жил. {title} {name}.";

            switch (warrior.Soul.Sin)
            {
                // Ждал и дождался. Принимает как должное.
                case SinType.Pride:
                    return $"{title}? Наконец-то вслух. Отныне я — {name} {title}!";

                // Считает, во что это ему обойдётся и что принесёт.
                case SinType.Greed:
                    return $"{title}... За такое имя и платят иначе. Запомните: {name} {title}!";

                // Имя как разрешение бить сильнее.
                case SinType.Wrath:
                    return $"{title}! Пусть знают, кого встретили. Я — {name} {title}!";

                // Сравнивает себя с теми, кому это не досталось.
                case SinType.Envy:
                    return $"{title}. А ведь многие ждали дольше. Я — {name} {title}.";

                // Хотел, чтобы смотрели, — и смотрят.
                case SinType.Lust:
                    return $"{title}? Мне нравится, как это звучит. Я — {name} {title}.";

                // Всё меряет тем, сколько взял.
                case SinType.Gluttony:
                    return $"{title}. Заслужено до последней крохи. Я — {name} {title}!";

                // Имя — это ещё и обязанность, а она тяготит.
                case SinType.Sloth:
                    return $"{title}... Теперь с меня и спрос другой. Ладно. {name} {title}.";

                default:
                    return $"{title}. Отныне я — {name} {title}.";
            }
        }
    }
}
