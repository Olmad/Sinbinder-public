// Deterministic, context-aware village replies. Pure C# to exercise in .NET CI.
// Index order follows SinType: Greed, Pride, Wrath, Envy, Lust, Gluttony, Sloth.
using Sinbinder.Core;

namespace Sinbinder.Dialogue
{
    public enum VillageLordTone { Respected, Distrusted, Invited }

    public static class VillageDialogueRules
    {
        // Rows: how the village regards the player Lord. Columns: responder's sin.
        private static readonly string[][] LordReplies =
        {
            new[] {
                Loc.N("Пока держит слово, зря ругать его не стану."),
                Loc.N("Не худший. Но за меня решать не надо."),
                Loc.N("Посмотрим, каков он в настоящей драке."),
                Loc.N("К нему идут охотнее, чем ко мне."),
                Loc.N("С ним хоть поговорить можно."),
                Loc.N("Главное, чтоб отряд без ужина не оставил."),
                Loc.N("Пока не гонит зря, пусть командует."),
            },
            new[] {
                Loc.N("С меня ему не взять ни гроша."),
                Loc.N("Пусть попробует мной помыкать."),
                Loc.N("Я бы ему дорогу перегородил."),
                Loc.N("Другим он улыбается. Нам — нет."),
                Loc.N("Не нравится мне, как он смотрит на людей."),
                Loc.N("При нём и крошку поберегу."),
                Loc.N("Лишь бы мимо прошёл."),
            },
            new[] {
                Loc.N("Звал — пусть сначала заплатит."),
                Loc.N("Я сам решу, за кем идти."),
                Loc.N("В бой зовёт или просто за собой таскать?"),
                Loc.N("Тебя звал, а меня — нет?"),
                Loc.N("Звал? Хоть по-человечески?"),
                Loc.N("У него, говорят, котёл полный."),
                Loc.N("Пусть зовёт. Я тут посижу."),
            }
        };

        // Rows: sin of the *speaker who asked*. Columns: sin of the one answering.
        // The responder must react to what was said, not announce a random vice.
        private static readonly string[][] SinReplies =
        {
            new[] {
                Loc.N("Свой счёт веди. До моего не доберёшься."),
                Loc.N("Тебе бы чужие кошели считать."),
                Loc.N("Припрятал топор. Хочешь проверить?"),
                Loc.N("Меньше твоего. Как всегда."),
                Loc.N("На двоих хватит. Может, поделюсь."),
                Loc.N("Горбушка хлеба. До вечера не доживёт."),
                Loc.N("Не считал. Спать не мешай."),
            },
            new[] {
                Loc.N("Достоинство на хлеб не выменяешь."),
                Loc.N("А я, значит, для вида здесь стою?"),
                Loc.N("Поговорим, когда дело до драки дойдёт."),
                Loc.N("Тебя вечно первым считают. С чего бы?"),
                Loc.N("Уж больно много о себе говоришь."),
                Loc.N("А поесть от этого что?"),
                Loc.N("Ты стой. Я посижу."),
            },
            new[] {
                Loc.N("Только своё не ломай. За чужое платят."),
                Loc.N("Научись сначала держать себя в руках."),
                Loc.N("Если начнётся, зови меня."),
                Loc.N("Конечно. Тебе всё с рук сойдёт."),
                Loc.N("Лучше с кем-нибудь поговори."),
                Loc.N("Сначала поешь. Может, остынешь."),
                Loc.N("Ты сначала встань с лавки."),
            },
            new[] {
                Loc.N("Видал. Если продаёт, узнаю цену."),
                Loc.N("А своим ты когда-нибудь доволен?"),
                Loc.N("Мне до чужого дела нет."),
                Loc.N("У него новое, у меня старое. И ладно."),
                Loc.N("Я смотрел не на обновку."),
                Loc.N("У соседа хлеб свежий? Тогда видал."),
                Loc.N("Видал. И пусть носит."),
            },
            new[] {
                Loc.N("Тепло пока бесплатно."),
                Loc.N("Не со всяким я сяду рядом."),
                Loc.N("Садись. Пока тихо, можно."),
                Loc.N("К тебе все идут, ко мне никто."),
                Loc.N("Подсаживайся ближе."),
                Loc.N("Тепло-то тепло, а ужин где?"),
                Loc.N("Пока не зовут, посижу."),
            },
            new[] {
                Loc.N("На чужой ужин не рассчитывай."),
                Loc.N("Ты ведь только что ел."),
                Loc.N("Не ной. Лучше дров подкинь."),
                Loc.N("Ты ешь чаще нас."),
                Loc.N("Разделим хлеб, коли найдётся."),
                Loc.N("У меня горбушка осталась. Пополам?"),
                Loc.N("Мне бы тоже, да вставать лень."),
            },
            new[] {
                Loc.N("Пока ты сидишь, за сундуком кто смотрит?"),
                Loc.N("Кто-то же должен встать, когда беда придёт."),
                Loc.N("Посидишь, когда врага выгоним."),
                Loc.N("Тебе легко. За тебя другие работают."),
                Loc.N("Если место есть, останусь."),
                Loc.N("А еды принесут?"),
                Loc.N("И не собирался вставать."),
            }
        };

        public static string LordAnswer(SinType responder, VillageLordTone tone) =>
            Loc.T(LordReplies[(int)tone][(int)responder]);

        public static string SinAnswer(SinType speaker, SinType responder) =>
            Loc.T(SinReplies[(int)speaker][(int)responder]);

        // Negative or overflowed identity stamps must still select a valid topic.
        public static int TopicIndex(int stamp) => (int)((uint)stamp % 4u);
    }
}
