// Assets/_Project/Scripts/AOS Engine/ISkillSet.cs
using System.Collections.Generic;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Набор умений, которые воин может предложить голосованию.
    ///
    /// В ActionType шестьдесят два значения, а в голосовании до сих пор
    /// участвовали шесть: словарь кандидатов был задан списком. Пятьдесят
    /// шесть умений существовали в коде и были недостижимы.
    ///
    /// Теперь кандидаты собираются с самого воина: какие компоненты умений
    /// на нём висят и какие из них сейчас не на откате — те действия и
    /// попадают в голосование.
    /// </summary>
    public interface ISkillSet
    {
        /// <summary>Все действия, которые предлагает этот набор.</summary>
        IReadOnlyList<ActionType> SkillActions { get; }

        /// <summary>Готово ли умение прямо сейчас (откат, условия).</summary>
        bool CanUseSkill(ActionType action);

        void ExecuteSkill(ActionType action);
    }
}
