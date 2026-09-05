// Assets/Scripts/Gameplay/Homecoming.cs
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Чем кончилась вылазка отряда, ушедшего на доле 2.
    ///
    /// Это закрытие демо: игрок выбрал старшего полчаса назад, прочитав
    /// пророчество, и здесь ему возвращают счёт (docs/09-PROLOGUE.md §4,
    /// сцена 8). Исход зависит от греха командира, а не от имени: канон
    /// имён не закрепляет, а грехи закрепляет.
    ///
    /// Правило намеренно без Unity — проверяется стендом.
    /// </summary>
    public static class Homecoming
    {
        /// <summary>Сколько вернулось из ушедших.</summary>
        public static int Returned(SinType sin, int sent)
        {
            if (sent <= 0) return 0;

            int back;
            switch (sin)
            {
                // Привал в неподходящий момент. Возвращается один.
                case SinType.Sloth:    back = 1; break;

                // Прорвался куда угодно и не заметил засады.
                case SinType.Wrath:    back = 2; break;

                // Гордыня не отступает, и это стоит людей.
                case SinType.Pride:    back = 2; break;

                // Берёг добычу — сберёг и людей. Лучший исход демо.
                case SinType.Greed:    back = sent - 1; break;

                // Делили добычу и передрались.
                case SinType.Envy:     back = sent - 2; break;

                // Отвлеклись, но обошлось.
                case SinType.Lust:     back = sent - 2; break;
                case SinType.Gluttony: back = sent - 2; break;

                default:               back = sent - 2; break;
            }

            if (back < 1) back = 1;
            if (back > sent) back = sent;
            return back;
        }

        /// <summary>
        /// Что предполагает Карган, когда шар наливается красным
        /// (docs/09-PROLOGUE.md §4, сцена 3).
        ///
        /// Догадка растёт из того же греха, что и позднейший
        /// <see cref="Story"/>: доля 3 не имеет права обещать одно,
        /// а эпилог возвращать другое. При этом Карган ошибается —
        /// гаснут не свои отряды, а те, до кого добрались Охотники, —
        /// и потому догадка звучит как догадка, а не как сводка.
        /// </summary>
        public static string Guess(SinType sin)
        {
            switch (sin)
            {
                case SinType.Sloth:
                    return "опять сделал привал";
                case SinType.Wrath:
                    return "полез вперёд, не разглядев на что";
                case SinType.Pride:
                    return "не стал отходить, когда стоило";
                case SinType.Greed:
                    return "свернул за чем-то блестящим";
                case SinType.Envy:
                    return "не поделил добычу со своими";
                case SinType.Lust:
                    return "нашёл, на что отвлечься";
                case SinType.Gluttony:
                    return "встал есть не вовремя";
                default:
                    return "сделал что-то по-своему";
            }
        }

        /// <summary>
        /// Чем он это объясняет. Одна фраза, без цифр: игрок и так видит,
        /// сколько их вошло в склеп.
        /// </summary>
        public static string Story(SinType sin)
        {
            switch (sin)
            {
                case SinType.Sloth:
                    return "Он сделал привал. Проснулся один.";
                case SinType.Wrath:
                    return "Он прорвался. Засаду заметить было уже некому.";
                case SinType.Pride:
                    return "Он не отступил. Отступить пришлось бы первым — "
                         + "а он привык быть последним.";
                case SinType.Greed:
                    return "Он берёг добычу так, что сберёг и людей.";
                case SinType.Envy:
                    return "Добычу делили дважды. Второй раз — уже без тех, "
                         + "кому она досталась в первый.";
                case SinType.Lust:
                    return "Их задержали. Он не говорит кто.";
                case SinType.Gluttony:
                    return "Припасы кончились раньше дороги.";
                default:
                    return "Он не рассказывает, и никто не переспрашивает.";
            }
        }
    }
}
