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
    /// Женщины отряда говорят только своими словами (правило проекта):
    /// у Лиски свои строки, и за неё не отвечает общий банк. Немой Гурт
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

            string first = Opening(a, b);
            string answer = Mute(b) ? Gesture(b) : Answer(b, a);
            if (Mute(a)) first = Gesture(a);
            return (first, answer);
        }

        /// <summary>
        /// Не говорит ни с кем. Признак — в имени, как он записан в отряде
        /// («Немой Гурт»): отдельного поля немоты в данных нет, а заводить
        /// его ради одного человека — вторая правда о нём же.
        /// </summary>
        public static bool Mute(Warrior w) => w != null && w.DisplayName.StartsWith("Немой");

        private static bool IsLiska(Warrior w) => w.DisplayName == "Лиска";

        private static string Gesture(Warrior w) => Loc.T("(молча кивает)");

        // ──────────────────────────────────
        // Первая строка
        // ──────────────────────────────────

        private static string Opening(Warrior a, Warrior b)
        {
            var sin = a.Soul.Sin;

            // 1. Что знает лагерь.
            if (sin == SinType.Greed && a.UnpaidMissions >= 3) return Loc.T("Третья вылазка без платы. Я запоминаю.");
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
                    default:            return Loc.T("Своё я уже посчитала.");
                }
            }

            // На то, что знает лагерь.
            if (asks == SinType.Greed && a.UnpaidMissions >= 2)
            {
                if (sin == SinType.Greed) return Loc.T("И мне не платят. Считай за двоих.");
                if (sin == SinType.Pride) return Loc.T("Плату просят, а не считают.");
                return Loc.T("Мне бы и без платы полежать.");
            }
            if (Remembers(a, "SinbinderTookFromMe"))
            {
                if (sin == SinType.Greed) return Loc.T("Моё он не заберёт.");
                if (sin == SinType.Pride) return Grammar.Pick(a.Gender, Loc.T("Значит, не заслужил держать."), Loc.T("Значит, не заслужила держать."));
                return Loc.T("Меньше нести.");
            }
            if (Brother(a) && Brother(b)) return Loc.T("А где ж мне ещё.");

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
