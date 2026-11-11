// Assets/Scripts/Dialogue/CampLines.cs
// Перевод: текст через Loc
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Dialogue
{
    /// <summary>
    /// Что двое говорят друг другу у костра (docs/32-CAMP.md §6, шаг второй
    /// жизни в лагере). Автор, 25 сентября: «добавить переговоры,
    /// подходящие соответствующим грехам».
    ///
    /// <b>Сперва — то, что знает лагерь, потом — характер.</b> Реплика
    /// просто по греху — приправа: её читают, а потом перестают замечать.
    /// Реплика, знающая положение, — предупреждение: игрок слышит, кто
    /// откажет, раньше отказа. Поэтому слои по порядку:
    ///
    /// <list type="number">
    /// <item>положение говорящего: долг, отнятое или подаренное Греховодом,
    /// карман, кого поставили старшим;</item>
    /// <item>братья по оружию;</item>
    /// <item>грех против греха — три греха демо, девять пар;</item>
    /// <item>по умолчанию.</item>
    /// </list>
    ///
    /// У Лиски свои строки, и за неё не отвечает общий банк. В демо
    /// Лиска — мужчина (решение автора 11 сентября: «женщины будут только
    /// в основной игре», коммит 7ec62c1) — и строки её мужского рода. Немой Гурт
    /// не говорит ни с кем — так он записан в отряде, — и отвечает жестом.
    ///
    /// Без жребия: одна и та же пара в одном и том же положении говорит
    /// одно и то же. Чисел нет.
    /// </summary>
    public static class CampLines
    {
        /// <summary>Двое и что они скажут: первая строка — его, вторая — ответ.</summary>
        public static (string First, string Answer) Exchange(Warrior a, Warrior b)
        {
            if (a == null || b == null || a.Soul == null || b.Soul == null) return ("", "");

            // На кивок нельзя отвечать так, будто немой рассказал о долге или старшем.
            if (Mute(a)) return (Gesture(a), Mute(b) ? Gesture(b) : Loc.T("Понял."));

            string first = Opening(a, b);
            // Ответ — на то, что сказано, а не просто грех на грех (автор, 28.10: «Старший — Вейн. Посмотрим,
            // куда заведёт» — «Будто? Подожди.»: ответ был на другую фразу).
            var topic = TopicOf(a, b);
            string answer = Mute(b) ? Gesture(b) : (OnTopic(b, a, topic) ?? Answer(b, a));
            return (first, answer);
        }

        private enum Topic { Unpaid, Took, Gave, Gold, LeadSelf, LeadOther, Brothers, Liska, Sin, Quiet }

        /// <summary>О чём первая строка — тем же порядком, что <see cref="Opening"/>.</summary>
        private static Topic TopicOf(Warrior a, Warrior b)
        {
            var sin = a.Soul.Sin;
            if (sin == SinType.Greed && a.UnpaidMissions >= 2) return Topic.Unpaid;
            if (Remembers(a, "SinbinderTookFromMe")) return Topic.Took;
            if (Remembers(a, "SinbinderGaveMe")) return Topic.Gave;
            if (a.PocketGold > 0 && sin == SinType.Greed) return Topic.Gold;
            if (!string.IsNullOrEmpty(SquadRoster.CommanderName))
            {
                if (a.IsCommander) return Topic.LeadSelf;
                if (sin == SinType.Pride) return Topic.LeadOther;
            }
            if (Brother(a) && Brother(b)) return Topic.Brothers;
            if (IsLiska(a)) return Topic.Liska;
            bool pair = (sin == SinType.Pride || sin == SinType.Greed || sin == SinType.Sloth)
                        && (b.Soul.Sin == SinType.Pride || b.Soul.Sin == SinType.Greed || b.Soul.Sin == SinType.Sloth);
            return pair ? Topic.Sin : Topic.Quiet;
        }

        /// <summary>Ответ на тему, которой у <see cref="Answer"/> нет; null — пусть отвечает он.</summary>
        private static string OnTopic(Warrior b, Warrior a, Topic topic)
        {
            var sin = b.Soul.Sin;
            switch (topic)
            {
                case Topic.Unpaid:
                    if (sin == SinType.Greed) return b.UnpaidMissions > 0
                        ? Loc.T("И мне не платят. Считай за двоих.")
                        : Loc.T("Тебе должны — с владыки и спрашивай.");
                    if (sin == SinType.Pride) return Loc.T("Не считай вылазки. Потребуй своё.");
                    return Loc.T("Мне бы и без платы полежать.");
                case Topic.Took:
                    if (sin == SinType.Greed) return Loc.T("Моё он не заберёт.");
                    if (sin == SinType.Pride) return Loc.T("Твоё? Так забери обратно.");
                    return Loc.T("Меньше нести.");
                case Topic.Brothers:
                    return Loc.T("А где ж мне ещё.");
                case Topic.Gave:
                    if (sin == SinType.Pride) return Grammar.Pick(a.Gender, Loc.T("Дали — не значит заслужил."), Loc.T("Дали — не значит заслужила."));
                    if (sin == SinType.Greed) return Loc.T("Покажи хоть, что дали.");
                    return Loc.T("Носи. Мне бы не дали — и ладно.");
                case Topic.Gold:
                    if (sin == SinType.Pride) return Loc.T("Звон — не честь.");
                    if (sin == SinType.Greed) return b.PocketGold > 0
                        ? Loc.T("Звенит. И у меня звенит.") : Loc.T("У меня пока не звенит.");
                    return Loc.T("Звени потише, я сплю.");
                case Topic.LeadSelf:
                    if (sin == SinType.Pride) return Loc.T("Посмотрим, надолго ли.");
                    if (sin == SinType.Greed) return Loc.T("Старший — значит, и платит старший?");
                    return Loc.T("Слушаю. Лёжа.");
                case Topic.LeadOther:
                    if (b.IsCommander) return Loc.T("Я здесь. И всё слышу.");
                    if (sin == SinType.Pride) return Loc.T("Заведёт — так выведем сами.");
                    if (sin == SinType.Greed) return Loc.T("Лишь бы платил, а вести пусть ведёт.");
                    return Loc.T("Куда бы ни завёл — лишь бы недалеко.");
                case Topic.Liska:
                    if (sin == SinType.Pride) return Loc.T("Мне кошель не нужен. Мне враг нужен.");
                    if (sin == SinType.Greed) return Loc.T("Мой пояс — моя забота.");
                    return Loc.T("Бери, если донесёшь.");
                case Topic.Quiet:
                    return Loc.T("Пока тихо.");
                default:
                    return null;
            }
        }

        /// <summary>
        /// Не говорит ни с кем. Признак — в имени, как он записан в отряде
        /// («Немой Гурт»): отдельного поля немоты в данных нет, а заводить
        /// его ради одного человека — вторая правда о нём же.
        /// </summary>
        public static bool Mute(Warrior w) => w != null && w.DisplayName.StartsWith("Немой");

        private static bool IsLiska(Warrior w) => w.DisplayName == "Лиска";

        public static string Gesture(Warrior w) => Loc.T("(молча кивает)");

        // ──────────────────────────────────
        // Первая строка
        // ──────────────────────────────────

        private static string Opening(Warrior a, Warrior b)
        {
            var sin = a.Soul.Sin;

            // 1. Что знает лагерь.
            if (sin == SinType.Greed && a.UnpaidMissions >= 4) return Loc.T("Давно без платы. Я запоминаю.");
            if (sin == SinType.Greed && a.UnpaidMissions == 3) return Loc.T("Третья вылазка без платы. Я запоминаю.");
            if (sin == SinType.Greed && a.UnpaidMissions == 2) return Loc.T("Вторая вылазка без платы. Я считаю.");

            if (Remembers(a, "SinbinderTookFromMe")) return Loc.T("Он забрал моё. Запомни, как это бывает.");
            if (Remembers(a, "SinbinderGaveMe"))
            {
                if (sin == SinType.Pride) return Grammar.Pick(b.Gender, Loc.T("Видел? Дали мне. Не тебе — мне."), Loc.T("Видела? Дали мне. Не тебе — мне."));
                if (sin == SinType.Greed) return Loc.T("Моё теперь. Даже не смотри.");
                return Grammar.Pick(a.Gender, Loc.T("Дали вещь. Не просил, а несу."), Loc.T("Дали вещь. Не просила, а несу."));
            }

            if (a.PocketGold > 0 && sin == SinType.Greed)
                return Grammar.Pick(b.Gender, Loc.T("Слышишь, звенит? Своё береги сам."), Loc.T("Слышишь, звенит? Своё береги сама."));

            string commander = SquadRoster.CommanderName;
            if (!string.IsNullOrEmpty(commander))
            {
                if (a.IsCommander && sin == SinType.Pride) return Loc.T("Теперь слушать меня. Меня.");
                if (a.IsCommander) return Loc.T("Старший теперь я. Не радуйтесь раньше времени.");
                if (sin == SinType.Pride) return Loc.F("Старший — {0}. Посмотрим, куда заведёт.", Short(commander));
            }

            // 2. Братья.
            if (Brother(a) && Brother(b)) return Loc.T("Держись рядом.");

            // 3. Лиска — своими словами.
            if (IsLiska(a))
            {
                switch (b.Soul.Sin)
                {
                    case SinType.Pride: return Loc.T("Гордость не греет. Кошель — греет.");
                    case SinType.Sloth: return Loc.T("Спи. Я посторожу твоё. Со всей заботой.");
                    default:            return Loc.T("Не смотри на мой пояс. Смотри на свой.");
                }
            }

            // 4. Грех против греха.
            switch (sin)
            {
                case SinType.Pride:
                    switch (b.Soul.Sin)
                    {
                        case SinType.Pride: return Loc.T("Стоишь, будто тебя поставили старшим.");
                        case SinType.Greed: return Loc.T("Опять у сундука? Бьются не монеты.");
                        case SinType.Sloth: return Loc.T("Встань, когда рядом стоит воин.");
                    }
                    break;

                case SinType.Greed:
                    switch (b.Soul.Sin)
                    {
                        case SinType.Pride: return Loc.T("Гордость в карман не положишь.");
                        case SinType.Greed: return Loc.T("Сколько там у тебя?");
                        case SinType.Sloth: return Loc.T("Вставай — у сундука есть место.");
                    }
                    break;

                case SinType.Sloth:
                    switch (b.Soul.Sin)
                    {
                        case SinType.Pride: return Loc.T("Ты всегда так стоишь? Не устаёшь?");
                        case SinType.Greed: return Loc.T("Сядь. Монеты не убегут.");
                        case SinType.Sloth: return Loc.T("Разбуди, если что.");
                    }
                    break;
            }

            return Loc.T("Тихо сегодня.");
        }

        // ──────────────────────────────────
        // Ответ
        // ──────────────────────────────────

        private static string Answer(Warrior b, Warrior a)
        {
            var sin = b.Soul.Sin;
            var asks = a.Soul.Sin;

            // Лиска отвечает своими словами.
            if (IsLiska(b))
            {
                switch (asks)
                {
                    case SinType.Pride: return Loc.T("Гордись. А считать буду я.");
                    case SinType.Sloth: return Loc.T("Лежи-лежи. Я посмотрю, что у тебя в мешке.");
                    default:            return Loc.T("Своё я уже посчитал.");
                }
            }

            // Грех на грех.
            switch (sin)
            {
                case SinType.Pride:
                    if (asks == SinType.Pride) return Loc.T("Будто? Подожди.");
                    if (asks == SinType.Greed) return Loc.T("Зато её не отнимут.");
                    return Loc.T("Устают те, кому не для чего стоять.");

                case SinType.Greed:
                    if (asks == SinType.Pride) return Loc.T("Зато монеты не хвастают.");
                    if (asks == SinType.Greed) return Grammar.Pick(a.Gender, Loc.T("Столько, чтоб ты не спрашивал."), Loc.T("Столько, чтоб ты не спрашивала."));
                    return Loc.T("Убегут — если сяду.");

                case SinType.Sloth:
                    if (asks == SinType.Pride) return Loc.T("Стой, раз нравится. Я полежу.");
                    if (asks == SinType.Greed) return Loc.T("Сундук не денется. И я тоже.");
                    return Grammar.Pick(b.Gender, Loc.T("Если что — сам проснусь. Может быть."), Loc.T("Если что — сама проснусь. Может быть."));
            }

            return Loc.T("Пока тихо.");
        }

        // ──────────────────────────────────
        // Что знает лагерь
        // ──────────────────────────────────

        private static bool Brother(Warrior w)
        {
            var perks = w.Soul?.Memory?.NarrativePerks;
            return perks != null && perks.Exists(p => p.PerkName == "Брат по оружию");
        }

        /// <summary>Есть ли у него такое воспоминание о Греховоде.</summary>
        private static bool Remembers(Warrior w, string what)
        {
            var memory = AOS.MemoryProcessor.Instance;
            if (memory == null || !SinbinderPlayer.Exists) return false;

            string him = SinbinderPlayer.Instance.Id;
            foreach (var m in memory.GetMemories(w))
                if (m != null && m.TargetID == him && m.EventType == what) return true;
            return false;
        }

        /// <summary>
        /// «Вейн Тихий» → «Вейн»: у костра по имени, не по прозвищу. Имя —
        /// показанное (Loc.Name): в переводе и прозвища свои, и стоят они
        /// там же — «Brother Hald», «Vein the Quiet».
        /// </summary>
        private static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            name = Loc.Name(name);
            int space = name.IndexOf(' ');
            // «Брат Хальд», «Немой Гурт» — прозвище впереди, имя последним.
            foreach (var epithet in Loc.IsSource ? EpithetFirst : EpithetFirstEn)
                if (name.StartsWith(epithet)) return name.Substring(epithet.Length);
            return space > 0 ? name.Substring(0, space) : name;
        }

        private static readonly string[] EpithetFirst =
        {
            "Брат ", "Немой ", "Толстый ", "Одноглазый ", "Косой ",   // ключ: начало имени
        };

        private static readonly string[] EpithetFirstEn =
        {
            "Brother ", "Mute ", "Fat ", "One-Eyed ", "Squint ",
        };
    }
}
