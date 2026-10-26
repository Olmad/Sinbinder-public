// Assets/Scripts/Gameplay/AskableSkill.cs
namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Навык, который можно попросить. Автор, 26 октября: навыки — «в полусвободном
    /// формате» (воин пускает сам, игрок может попросить) и «в интерфейсе — в панели
    /// управления справа» (<see cref="UI.CommandPanel"/>). Просьба — не команда:
    /// она идёт к воину приказом и голосуется, как любой приказ.
    /// Первый такой навык — <see cref="CrowStep"/> Каргана.
    /// </summary>
    public interface IAskableSkill
    {
        /// <summary>Название на кнопке.</summary>
        string Word { get; }

        /// <summary>Что делает — строкой подсказки.</summary>
        string Tip { get; }

        /// <summary>Готов ли — откат прошёл и силы есть. Без цифр: «готов» или нет.</summary>
        bool Ready { get; }

        /// <summary>
        /// Уместен ли сейчас; ложь — с причиной словами («Я и так рядом»): тогда
        /// просьба не уходит, а воин отвечает, почему незачем.
        /// </summary>
        bool Fits(out string why);

        /// <summary>Попросить. Решит душа.</summary>
        void Ask();
    }
}
