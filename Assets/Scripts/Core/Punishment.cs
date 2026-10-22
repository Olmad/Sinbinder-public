// Assets/Scripts/Core/Punishment.cs
// Перевод: строк нет
namespace Sinbinder.Core
{
    /// <summary>
    /// Наказание — рычаг на грех (`docs/48-PUNISHMENT.md`).
    ///
    /// Автор, 16 октября: «ГГ наказал Маргу за ситуацию у моста… Это
    /// наказание — попытка снизить Жадность, чтобы он больше
    /// сосредотачивался на задаче. И хочется чтобы это была механика».
    ///
    /// <b>Наказание отнимает то, чего хочет грех</b>; у Жадности — плату.
    /// Пока оно длится, каждая ночь без платы (плата у алтаря после
    /// вылазки) делает две вещи:
    /// <list type="bullet">
    /// <item><b>исправление</b> — спектр греха сдвигается к добродетели;</item>
    /// <item><b>обида</b> — падает верность Греховоду.</item>
    /// </list>
    /// После <see cref="Limit"/> ночей исправление кончается, обида растёт
    /// быстрее — наказание «затянулось» (автор: «да» на две ночи). Ночи
    /// считаются за деяние, а не за долг: снял наказание и наказал снова
    /// за тот же мост — для души это всё та же затянувшаяся кара
    /// (<see cref="MemorySeed.Atoned"/>).
    ///
    /// <b>Заслуженное наказание обижает меньше невыплаты</b>: душа знает,
    /// за что. Стенд (22 октября): с обидой, равной невыплате, наказанный
    /// после уплаты слушался хуже, чем ненаказанный, — у всех трёх моралей;
    /// рычаг, который всегда хуже бездействия, игрок выучит как «не трогай».
    ///
    /// <b>Мораль решает, как его принимают</b>: праведный принимает, что
    /// заслужил, — исправляется сильнее и обижается меньше; порочный считает
    /// несправедливым. <b>Сила греха решает, сколько обиды</b>: чем громче
    /// грех, тем больнее отнимают то, чего он хочет.
    ///
    /// <b>Наказывают за деяние, которое помнит душа</b> (§3), а не за
    /// характер: без деяния невыплата — просто невыплата, обида без
    /// исправления. Пока деяние одно — мост Марги.
    ///
    /// Ни один модуль не знает слова «наказание»: исправление — это спектр,
    /// обида — верность, и голосуют те же Жадность и Верность.
    ///
    /// Правило без Unity — числа выверены стендом (`12-BALANCE.md`,
    /// «Наказание»).
    /// </summary>
    public static class Punishment
    {
        /// <summary>Сколько ночей наказание исправляет. Дальше — только обида.</summary>
        public const int Limit = 2;

        /// <summary>
        /// Ключ памяти Марги: у моста отряд дрался, а он копал монеты.
        /// Объект памяти, игрок его не видит (<c>PrologueCampSpawner</c>).
        /// </summary>
        public const string BridgeDeed = "мост";   // ключ, не строка экрана

        /// <summary>
        /// Сдвиг спектра за ночь у нейтрального — к добродетели. Стенд:
        /// у добычи нейтральный перестаёт отказываться, когда Жадность падает
        /// с 65 до 35, — две ночи по пятнадцать; меньший сдвиг не слышен.
        /// </summary>
        public const float Correction = 15f;

        /// <summary>Сколько верности стоит одна невыплата — и без наказания тоже.</summary>
        public const float Unpaid = 20f;

        /// <summary>Доля обиды от невыплаты, пока наказание заслуженно и в пределе.</summary>
        public const float Deserved = 0.1f;

        /// <summary>Во сколько раз обида больше невыплаты, когда наказание затянулось.</summary>
        public const float Overdrawn = 1.5f;

        /// <summary>
        /// За какой грех душа заслужила наказание — по деянию, которое она
        /// помнит. Нет деяния — null: наказывать не за что.
        /// </summary>
        public static SinType? DeedSin(SoulData soul)
        {
            if (soul == null || !soul.HasMemory) return null;
            return soul.Memory.Object == BridgeDeed ? SinType.Greed : (SinType?)null;
        }

        /// <summary>Как мораль принимает наказание: множитель исправления.</summary>
        public static float Accepts(MoralType moral)
        {
            switch (moral)
            {
                case MoralType.Pious:   return 1.6f;
                case MoralType.Vicious: return 0.4f;
                default:                return 1f;
            }
        }

        /// <summary>Как мораль переносит наказание: множитель обиды.</summary>
        public static float Resents(MoralType moral)
        {
            switch (moral)
            {
                case MoralType.Pious:   return 0.5f;
                case MoralType.Vicious: return 1.25f;
                default:                return 1f;
            }
        }

        /// <summary>
        /// Одна ночь наказания: у Жадности — ночь без платы
        /// (<paramref name="punishedFor"/> — грех, чьё желание отняли). Сдвигает спектр (не дальше нуля: наказание
        /// исправляет, но святых не делает), считает ночь отбытой и говорит,
        /// сколько верности она стоила. Сколько исправлено — в
        /// <paramref name="corrected"/>. Деяния нет — обычная невыплата.
        /// </summary>
        public static float Night(SoulData soul, SinType punishedFor, out float corrected)
        {
            corrected = 0f;

            // Отнимают то, чего хочет грех деяния, — или это не наказание
            // за него, а просто невыплата: плата Гневу не кара.
            var sin = DeedSin(soul);
            if (sin == null || sin.Value != punishedFor) return Unpaid;

            float value = soul.Get(sin.Value);
            float loud = 0.75f + System.Math.Abs(value) / 200f;   // 0,75…1,25
            float hurt = Unpaid * Resents(soul.Moral) * loud;

            var deed = soul.Memory;
            if (deed.Atoned >= Limit) return hurt * Overdrawn;

            deed.Atoned++;
            corrected = System.Math.Min(System.Math.Max(value, 0f), Correction * Accepts(soul.Moral));
            soul.Change(sin.Value, -corrected);
            return hurt * Deserved;
        }

        /// <summary>Затянулось ли: ночей за деяние отбыто сполна.</summary>
        public static bool Overdue(SoulData soul)
            => DeedSin(soul) != null && soul.Memory.Atoned >= Limit;
    }
}
