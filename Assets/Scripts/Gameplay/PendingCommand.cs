// Assets/_Project/Scripts/Gameplay/PendingCommand.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>Что именно велел игрок.</summary>
    public enum CommandKind
    {
        None,
        Move,
        Attack,
        Hold,
        Defend,

        /// <summary>
        /// Отход: «уходи отсюда», а не «иди туда».
        ///
        /// Отдельный вид нужен потому, что отход — единственный приказ,
        /// который воин может исполнить, не подчиняясь. Побежавший от
        /// врага сделал ровно то, о чём просили, — своим способом и не
        /// в ту точку. Пока отход был обычным Move, движок записывал
        /// такого в ослушавшиеся и бил его же памятью об отказе.
        ///
        /// Дописано в конец намеренно: значения этого перечисления лежат
        /// числами в сохранённых сценах, и вставка в середину сдвинула бы
        /// все приказы после себя.
        /// </summary>
        FallBack
    }

    /// <summary>
    /// Приказ игрока — не команда, а предложение.
    ///
    /// Здесь он только записывается. Исполнится ли — решает голосование:
    /// ObeyCommand участвует в нём наравне с дракой, добычей и бегством
    /// и может проиграть. В этом вся игра.
    /// </summary>
    [System.Serializable]
    public struct PendingCommand
    {
        public CommandKind Kind;
        public Vector3 Point;
        public GameObject Target;

        /// <summary>Время выдачи. Свежий приказ звучит громче старого.</summary>
        public float IssuedAt;

        public bool IsSet => Kind != CommandKind.None;

        /// <summary>Строка для DecisionContext.CommandType.</summary>
        public string TypeName => Kind == CommandKind.None ? "" : Kind.ToString();

        /// <summary>
        /// Уводит ли приказ из боя. Читается при сборке контекста, чтобы
        /// модули личности не сравнивали строки: модуль не должен знать
        /// ни про Warrior, ни про приказы игрока — он переводит характер
        /// в очки, и всё.
        /// </summary>
        public bool LeadsAwayFromFight
            => Kind == CommandKind.FallBack || Kind == CommandKind.Move;
    }
}
