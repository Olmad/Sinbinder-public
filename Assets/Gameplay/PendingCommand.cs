// Assets/Gameplay/PendingCommand.cs
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
        Defend
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
    }
}
