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

        public static CampSpot Choose(IEnumerable<ICampModule> voices, Soul soul)
        {
            CampSpot best = CampSpot.Fire;
            float bestScore = float.NegativeInfinity;

            // Порядок перечисления — порядок при равенстве: без жребия.
            foreach (CampSpot spot in System.Enum.GetValues(typeof(CampSpot)))
            {
                float score = spot == CampSpot.Fire ? Warmth : 0f;
                foreach (var v in voices) score += v.EvaluateSpot(soul, spot);

                if (score > bestScore) { bestScore = score; best = spot; }
            }
            return best;
        }
    }
}
