// Assets/Scripts/Dialogue/CampLines.cs
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

        private static string Gesture(Warrior w) => "(молча кивает)";

        // ──────────────────────────────────
        // Первая строка
        // ──────────────────────────────────

        private static string Opening(Warrior a, Warrior b)
        {
            var sin = a.Soul.Sin;

            // 1. Что знает лагерь.
            if (sin == SinType.Greed && a.UnpaidMissions >= 3) return "Третья вылазка без платы. Я запоминаю.";
            if (sin == SinType.Greed && a.UnpaidMissions == 2) return "Вторая вылазка без платы. Я считаю.";

            if (Remembers(a, "SinbinderTookFromMe")) return "Он забрал моё. Запомни, как это бывает.";
            if (Remembers(a, "SinbinderGaveMe"))
            {
                if (sin == SinType.Pride) return "Видел? Дали мне. Не тебе — мне.";
                if (sin == SinType.Greed) return "Моё теперь. Даже не смотри.";
                return "Дали вещь. Не просил, а несу.";
            }

            if (a.PocketGold > 0 && sin == SinType.Greed) return "Слышишь, звенит? Своё береги сам.";

            string commander = SquadRoster.CommanderName;
            if (!string.IsNullOrEmpty(commander))
            {
                if (a.IsCommander && sin == SinType.Pride) return "Теперь слушать меня. Меня.";
                if (a.IsCommander) return "Старший теперь я. Не радуйтесь раньше времени.";
                if (sin == SinType.Pride) return $"Старший — {Short(commander)}. Посмотрим, куда заведёт.";
            }

            // 2. Братья.
            if (Brother(a) && Brother(b)) return "Держись рядом.";

            // 3. Лиска — своими словами.
            if (IsLiska(a))
            {
                switch (b.Soul.Sin)
                {
                    case SinType.Pride: return "Гордость не греет. Кошель — греет.";
                    case SinType.Sloth: return "Спи. Я посторожу твоё. Со всей заботой.";
                    default:            return "Не смотри на мой пояс. Смотри на свой.";
                }
            }

            // 4. Грех против греха.
            switch (sin)
            {
                case SinType.Pride:
                    switch (b.Soul.Sin)
                    {
                        case SinType.Pride: return "Стоишь, будто тебя поставили старшим.";
                        case SinType.Greed: return "Опять у сундука? Бьются не монеты.";
                        case SinType.Sloth: return "Встань, когда рядом стоит воин.";
                    }
                    break;

                case SinType.Greed:
                    switch (b.Soul.Sin)
                    {
                        case SinType.Pride: return "Гордость в карман не положишь.";
                        case SinType.Greed: return "Сколько там у тебя?";
                        case SinType.Sloth: return "Вставай — у сундука есть место.";
                    }
                    break;

                case SinType.Sloth:
                    switch (b.Soul.Sin)
                    {
                        case SinType.Pride: return "Ты всегда так стоишь? Не устаёшь?";
                        case SinType.Greed: return "Сядь. Монеты не убегут.";
                        case SinType.Sloth: return "Разбуди, если что.";
                    }
                    break;
            }

            return "Тихо сегодня.";
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
                    case SinType.Pride: return "Гордись. А считать буду я.";
                    case SinType.Sloth: return "Лежи-лежи. Я посмотрю, что у тебя в мешке.";
                    default:            return "Своё я уже посчитала.";
                }
            }

            // На то, что знает лагерь.
            if (asks == SinType.Greed && a.UnpaidMissions >= 2)
            {
                if (sin == SinType.Greed) return "И мне не платят. Считай за двоих.";
                if (sin == SinType.Pride) return "Плату просят, а не считают.";
                return "Мне бы и без платы полежать.";
            }
            if (Remembers(a, "SinbinderTookFromMe"))
            {
                if (sin == SinType.Greed) return "Моё он не заберёт.";
                if (sin == SinType.Pride) return "Значит, не заслужил держать.";
                return "Меньше нести.";
            }
            if (Brother(a) && Brother(b)) return "А где ж мне ещё.";

            // Грех на грех.
            switch (sin)
            {
                case SinType.Pride:
                    if (asks == SinType.Pride) return "Будто? Подожди.";
                    if (asks == SinType.Greed) return "Зато её не отнимут.";
                    return "Устают те, кому не для чего стоять.";

                case SinType.Greed:
                    if (asks == SinType.Pride) return "Зато монеты не хвастают.";
                    if (asks == SinType.Greed) return "Столько, чтоб ты не спрашивал.";
                    return "Убегут — если сяду.";

                case SinType.Sloth:
                    if (asks == SinType.Pride) return "Стой, раз нравится. Я полежу.";
                    if (asks == SinType.Greed) return "Сундук не денется. И я тоже.";
                    return "Если что — сам проснусь. Может быть.";
            }

            return "Пока тихо.";
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

        /// <summary>«Вейн Тихий» → «Вейн»: у костра по имени, не по прозвищу.</summary>
        private static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int space = name.IndexOf(' ');
            // «Брат Хальд», «Немой Гурт» — прозвище впереди, имя последним.
            if (name.StartsWith("Брат ") || name.StartsWith("Немой ") || name.StartsWith("Толстый ")
                || name.StartsWith("Одноглазый ") || name.StartsWith("Косой "))
                return name.Substring(space + 1);
            return space > 0 ? name.Substring(0, space) : name;
        }
    }
}
