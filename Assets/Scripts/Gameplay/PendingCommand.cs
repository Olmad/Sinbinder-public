// Assets/Scripts/Gameplay/PendingCommand.cs
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
        FallBack,

        /// <summary>
        /// Патруль: ходить между тем местом, где стоял, и указанной точкой,
        /// пока приказ не снимут (docs/33-COMMANDS.md, шаг второй). Бросить
        /// маршрут ради драки — уже не исполнение.
        /// </summary>
        Patrol,

        /// <summary>
        /// Атака с ходу: идти в точку и бить всех по дороге — как A-щелчок
        /// по земле в Warcraft 3. Драка по пути приказ исполняет, а не нарушает.
        /// </summary>
        AttackMove,
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

        /// <summary>
        /// Насколько приказ был приглушён в миг выдачи (<see cref="Voice"/>):
        /// 0 — в полную силу. Хранится приглушённость, а не громкость,
        /// нарочно: приказ, собранный где-то без неё, по умолчанию слышен
        /// полностью, а не молчит.
        /// </summary>
        public float Muffle;

        /// <summary>Патруль: откуда вышел — второй конец маршрута.</summary>
        public Vector3 From;

        /// <summary>Патруль: идёт ли сейчас обратно, к <see cref="From"/>.</summary>
        public bool Back;

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
            => Kind == CommandKind.FallBack || Kind == CommandKind.Move || Kind == CommandKind.Patrol;
    }
}
