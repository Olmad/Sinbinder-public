// Перевод: текст через Loc
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Что тело делает, когда душа решила.
    ///
    /// <b>Это договор с художником, а не список анимаций.</b> Движок уже
    /// говорит «Flee», «Attack», «Loot» — здесь сказано, во что это
    /// превращается на экране. Написано до того, как появилась первая
    /// модель, нарочно: тогда модель, когда она появится, подходит
    /// или не подходит сразу, а не после девятой.
    ///
    /// <b>Пять состояний. Не пятьдесят.</b> Столько нужно, чтобы подпись
    /// «Сбегает» перестала быть подписью к ничему: игрок обязан увидеть
    /// побег, а не прочитать о нём. Остальное — потом и только если
    /// понадобится; в искусстве нет ни замера, ни правила «мёртвый код
    /// хуже отсутствующего», и рамку приходится держать руками.
    ///
    /// Полсотни действий движка сводятся к пяти нарочно: <c>Devour</c>
    /// и <c>Berserk</c> различаются подписью и причиной, а на экране оба
    /// — удар. Заводить им свою анимацию значит заводить сорок анимаций,
    /// которых никто не отличит в бою.
    ///
    /// Чистый класс — проверяется стендом.
    /// </summary>
    public static class BodyMotion
    {
        /// <summary>Стоит. Всё, что не движение и не удар.</summary>
        public const string Idle = "Idle";

        /// <summary>Идёт. Половина времени на экране.</summary>
        public const string Walk = "Walk";

        /// <summary>Бьёт. Любое действие, в котором он кого-то трогает.</summary>
        public const string Attack = "Attack";

        /// <summary>Бежит прочь. То, ради чего всё затевалось.</summary>
        public const string Flee = "Flee";

        /// <summary>Падает. Один раз и навсегда.</summary>
        public const string Die = "Die";

        /// <summary>Говорит. Не решение движка — состояние разговора.</summary>
        public const string Talk = "Talk";

        /// <summary>
        /// Достаёт оружие из ножен. Как и <see cref="Talk"/>, не решение
        /// движка, а облик: в бою оружие наголо, в покое — в ножнах
        /// (<see cref="Armament"/>, <see cref="WarriorAnimation"/>). Клип —
        /// набор Mixamo «Sword and Shield», перенесённый на нашу арматуру
        /// (<c>Tools/blender/retarget.py</c>).
        /// </summary>
        public const string Draw = "Draw";

        /// <summary>Убирает оружие в ножны.</summary>
        public const string Sheathe = "Sheathe";

        /// <summary>
        /// Сидит — за картами (<see cref="CardTable"/>). Автор, 21 октября:
        /// «я так ни разу и не увидел, чтобы воины сидели за картами».
        /// Клип — «Sitting_Idle_Loop» из Quaternius Universal Animation Library
        /// (CC0), перенесённый <c>retarget.py --rig rigify</c>.
        /// </summary>
        public const string Sit = "Sit";

        /// <summary>
        /// Копает на коленях — примета Марги Копателя (<see cref="DiggerHabit"/>,
        /// 26 октября). Клип — «Fixing_Kneeling» той же библиотеки Quaternius.
        /// </summary>
        public const string Dig = "Dig";

        /// <summary>
        /// Вызов — удары мечом в щит и руки в стороны (арьергард Каргана, 26.10;
        /// <see cref="WarriorAnimation.Challenge"/>). Свой клип <c>bodies.py</c>, не зациклен.
        /// </summary>
        public const string Challenge = "Challenge";

        /// <summary>Все состояния, которые обязан знать аниматор.</summary>
        public static string[] All()
            => new[] { Idle, Walk, Attack, Flee, Die, Talk, Draw, Sheathe, Sit, Dig, Challenge };

        /// <summary>
        /// Во что превращается решение.
        ///
        /// Уход за добычей и бросок к раненому — это ходьба: воин идёт
        /// туда, куда решил. Отличает их не походка, а подпись над
        /// головой, и различать их походкой было бы враньём — со спины
        /// они выглядят одинаково, потому что одинаково и есть.
        /// </summary>
        public static string For(ActionType action)
        {
            switch (action)
            {
                case ActionType.Flee:
                    return Flee;

                // Идёт куда-то по своей воле или по приказу.
                case ActionType.Loot:
                case ActionType.SaveAlly:
                case ActionType.ObeyCommand:
                case ActionType.AcceptBribe:
                case ActionType.BribeEnemy:
                    return Walk;

                case ActionType.Idle:
                    return Idle;

                // Всё остальное — удар. Умения различаются словами
                // и последствиями, а не тем, как это выглядит.
                default:
                    return Attack;
            }
        }
    }
}
