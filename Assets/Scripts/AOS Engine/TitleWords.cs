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
            MoralType moral = warrior.Soul.Moral;

            if (legendary) return Legend(moral, name, title);

            switch (warrior.Soul.Sin)
            {
                // Ждал признания. Порочный считает, что мало; праведного
                // собственная гордость смущает.
                case SinType.Pride: return Pick(moral,
                    $"Наконец-то. Хотя такому, как я, и этого мало.",
                    $"{title}? Наконец-то вслух. Отныне я — {name} {title}!",
                    $"Я не просил этого имени. Но носить буду достойно.");

                // Меряет ценой. Праведный вдруг понимает, что не всё продаётся.
                case SinType.Greed: return Pick(moral,
                    $"За такое имя платят больше. Запомните и это: {name} {title}.",
                    $"{title}... За такое имя и платят иначе. Запомните: {name} {title}!",
                    $"Имя дороже золота. Жаль, понял это поздно.");

                // Имя как разрешение. Праведный рад делу, не имени.
                case SinType.Wrath: return Pick(moral,
                    $"{title}! Пусть знают, кого встретили!",
                    $"{title}! Теперь идите за мной — я впереди. {name} {title}!",
                    $"Я не рад этому имени. Но заслужил его честно.");

                // Сравнивает. Порочный — со злорадством, праведный — со стыдом.
                case SinType.Envy: return Pick(moral,
                    $"Наконец-то не им, а мне. {name} {title}.",
                    $"{title}. А ведь многие ждали дольше. Я — {name} {title}.",
                    $"Многие достойнее. Постараюсь не подвести.");

                // Хотел, чтобы смотрели. Образец автора — в середине.
                case SinType.Lust: return Pick(moral,
                    $"Смотрите. Все смотрите: {name} {title}!",
                    $"{title}? Мне нравится, как это звучит. Я — {name} {title}.",
                    $"Приятно. Слишком приятно — и это меня тревожит.");

                // Считает взятым. Праведному довольно и этого.
                case SinType.Gluttony: return Pick(moral,
                    $"Заслужено до последней крохи. И ещё возьму.",
                    $"{title}. Заслужено до последней крохи. Я — {name} {title}!",
                    $"Хватит с меня и этого. Больше не прошу.");

                // Имя — это обязанность. Праведный всё же встаёт.
                case SinType.Sloth: return Pick(moral,
                    $"Теперь и спрос другой. Могли бы и не кричать.",
                    $"{title}... Теперь с меня и спрос другой. Ладно. {name} {title}.",
                    $"Имя дали — придётся соответствовать. Встаю.");

                default:
                    return $"{title}. Отныне я — {name} {title}.";
            }
        }

        /// <summary>
        /// Легендарное имя — тоже по морали: порочный хочет, чтобы
        /// боялись, праведный жалеет, что запомнят имя, а не дело.
        /// </summary>
        private static string Legend(MoralType moral, string name, string title)
            => Pick(moral,
                $"Меня будут помнить. Пусть боятся. {name} {title}.",
                $"Меня будут помнить дольше, чем я жил. {title} {name}.",
                $"Помнить будут имя. Я бы хотел — дело.");

        /// <summary>
        /// Выбор по морали. Это и есть <b>вторая ось вместо жребия</b>:
        /// грех говорит, что его тянет, мораль — как он себя за это судит,
        /// и в игре это разные вещи с самого начала. Семь грехов на три
        /// морали дают двадцать одну реплику, и ни одна не выпадает
        /// случайно.
        ///
        /// Осей, если понадобится больше, здесь же ещё две: ремесло души
        /// и то, который это титул по счёту. Каждая умножает набор,
        /// не трогая ни строки логики.
        /// </summary>
        private static string Pick(MoralType moral, string vicious,
                                   string neutral, string pious)
        {
            switch (moral)
            {
                case MoralType.Vicious: return vicious;
                case MoralType.Pious:   return pious;
                default:                return neutral;
            }
        }
    }
}
