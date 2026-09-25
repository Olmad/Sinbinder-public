// Assets/Scripts/AOS Engine/ICampModule.cs
using System.Collections.Generic;

namespace Sinbinder.AOS
{
    /// <summary>Места лагеря, между которыми выбирает душа без приказа (docs/32-CAMP.md).</summary>
    public enum CampSpot
    {
        /// <summary>Костёр: тепло, компания, котёл.</summary>
        Fire,
        /// <summary>Сундук с трофеями: добро.</summary>
        Chest,
        /// <summary>Стол с шаром: там решают, кто старший.</summary>
        Table,
        /// <summary>Дозор у северного края, лицом туда, откуда придут.</summary>
        Watch,
        /// <summary>Край света от костра: уединение.</summary>
        Apart,
        /// <summary>Палатки на холме: покой.</summary>
        Tents,
        /// <summary>Рядом с Греховодом: служба.</summary>
        Sinbinder,
    }

    /// <summary>
    /// Необязательное расширение личностного модуля: куда тянет душу
    /// в лагере, когда делать нечего (docs/32-CAMP.md, «жизнь в лагере»).
    ///
    /// Устроено как <see cref="IMissionModule"/>: место выбирается не
    /// таблицей сбоку, а голосованием тех же модулей, что решают в бою.
    /// Кому нечего сказать о месте (страху, памяти), тот интерфейс
    /// не реализует и молчит.
    ///
    /// Добродетель — та же шкала со знаком минус: щедрость тянет к огню,
    /// усердие — в дозор. Без жребия: одна душа — одно место.
    /// </summary>
    public interface ICampModule
    {
        float EvaluateSpot(Soul soul, CampSpot spot);
    }

    /// <summary>
    /// Выбор места: сумма голосов, побеждает громкий. Один код на игру
    /// (<c>Gameplay.CampLife</c>) и на стенд — иначе стенд мерил бы
    /// не то, что делает игра.
    /// </summary>
    public static class CampChoice
    {
        /// <summary>
        /// Огонь чуть тянет всех: у костра тепло. Без этой малости душа,
        /// которой ни одно место не нужно, выбирала бы первое по списку
        /// случайно для глаза, а не по делу.
        /// </summary>
        public const float Warmth = 2f;

        public static CampSpot Choose(IEnumerable<ICampModule> voices, Soul soul,
                                      System.Func<CampSpot, float> tired = null)
        {
            CampSpot best = CampSpot.Fire;
            float bestScore = float.NegativeInfinity;

            // Порядок перечисления — порядок при равенстве: без жребия.
            foreach (CampSpot spot in System.Enum.GetValues(typeof(CampSpot)))
            {
                float score = spot == CampSpot.Fire ? Warmth : 0f;
                foreach (var v in voices) score += v.EvaluateSpot(soul, spot);
                if (tired != null) score -= tired(spot);

                if (score > bestScore) { bestScore = score; best = spot; }
            }
            return best;
        }

        // ── Ритм (автор, 25 сентября: «если гордые всё время будут стоять
        // на одном месте, жадные — возле сундуков, а унылые — в палатках,
        // то где жизнь?»). Место приедается: чем дольше стоишь, тем слабее
        // тянет, — и душа идёт ко второму по сердцу месту, а потом
        // возвращается. Только что покинутое сразу назад не тянет. Без
        // жребия: те же минуты — тот же путь. ──

        /// <summary>
        /// Куда душа пойдёт сейчас: стоит на <paramref name="here"/> уже
        /// <paramref name="minutesHere"/> минут, ушла с <paramref name="left"/>
        /// <paramref name="minutesSinceLeft"/> минут назад. Один шаг на игру
        /// (<c>Gameplay.CampLife</c>) и на стенд.
        /// </summary>
        public static CampSpot Next(IEnumerable<ICampModule> voices, Soul soul, CampSpot here,
                                    float minutesHere, CampSpot? left, float minutesSinceLeft)
            => Choose(voices, soul, s => Tired(soul, s, here, minutesHere, left, minutesSinceLeft));

        /// <summary>
        /// Сколько минут место не приедается вовсе: пришёл — обживается.
        /// Без этого слабо тянутые души (кому все места почти равны)
        /// перебегали каждые полминуты — суета, а не жизнь (стенд).
        /// </summary>
        public const float Settle = 1.5f;

        /// <summary>На сколько за минуту на месте слабеет его тяга.</summary>
        public const float Boredom = 1.2f;

        /// <summary>Больше этого место не приедается: своё остаётся своим.</summary>
        public const float BoredomCap = 9f;

        /// <summary>Только что ушёл — назад тянет на столько меньше.</summary>
        public const float Rest = 4f;

        /// <summary>За сколько минут покинутое место отпускает.</summary>
        public const float RestMinutes = 2.5f;

        /// <summary>
        /// Насколько место сейчас не тянет: приелось (<paramref name="here"/>,
        /// простоял <paramref name="minutesHere"/>) или только что покинуто
        /// (<paramref name="left"/>, <paramref name="minutesSinceLeft"/> назад).
        /// Унылый обживается дольше и скучает медленнее — ему много не надо,
        /// в этом его суть; усердный (уныние со знаком минус) — наоборот.
        /// </summary>
        public static float Tired(Soul soul, CampSpot spot, CampSpot here, float minutesHere,
                                  CampSpot? left, float minutesSinceLeft)
        {
            if (spot == here)
            {
                float sloth = soul != null ? soul.Get(Core.SinType.Sloth) / 100f : 0f;
                float settle = sloth > 0f ? Settle * (1f + 5f * System.Math.Min(1f, sloth))
                                          : Settle / (1f + System.Math.Min(1f, -sloth));
                float pace = sloth > 0f ? 1f - System.Math.Min(0.8f, sloth) : 1f + System.Math.Min(1f, -sloth);
                float bored = System.Math.Max(0f, minutesHere - settle) * Boredom * pace;
                return System.Math.Min(BoredomCap, bored);
            }
            if (left.HasValue && spot == left.Value)
                return Rest * System.Math.Max(0f, 1f - minutesSinceLeft / RestMinutes);
            return 0f;
        }
    }
}
