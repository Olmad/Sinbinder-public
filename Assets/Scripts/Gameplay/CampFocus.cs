// Assets/Scripts/Gameplay/CampFocus.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Где в лагере находится игрок.
    ///
    /// У Греховода в демо нет фигуры, которой можно было бы столкнуться
    /// с триггером: его присутствие — это камера. Значит «подойти к столу»
    /// (docs/09-PROLOGUE.md §4, сцена 2) означает «навести взгляд на стол»,
    /// и это надо посчитать: куда смотрящий сверху вниз луч попадает
    /// в землю.
    ///
    /// Правило намеренно без Unity-объектов — только вектора, — и потому
    /// проверяется стендом. Ошибиться тут легко и незаметно: камера,
    /// смотрящая в горизонт, не попадает в землю нигде, а наивная формула
    /// вернёт на это ноль и решит, что игрок стоит в начале координат.
    /// Отсутствие точки — это событие, а не ноль.
    /// </summary>
    public static class CampFocus
    {
        /// <summary>
        /// Насколько близко надо подвести взгляд к столу совета.
        /// Живёт здесь, а не в шаре: по этому же числу сборщик сцен
        /// проверяет, не стоит ли стол прямо под открывающим кадром.
        /// Два числа разошлись бы молча.
        /// </summary>
        public const float TableReach = 3.5f;

        /// <summary>
        /// Куда смотрящий взгляд ложится на землю.
        /// false — не ложится нигде: камера смотрит в горизонт или выше,
        /// либо земля осталась позади неё.
        /// </summary>
        public static bool TryGroundPoint(Vector3 eye, Vector3 forward,
            float groundY, out Vector3 point)
        {
            point = eye;

            Vector3 dir = forward.normalized;

            // Наклон вниз — единственное, чем взгляд достаёт до земли.
            // Ровно горизонтальный не достаёт: расстояние до земли уходит
            // в бесконечность, а не в ноль.
            if (dir.y >= -1e-4f) return false;

            float height = eye.y - groundY;
            if (height <= 0f) return false;      // камера ниже земли — некуда падать

            point = eye + dir * (height / -dir.y);
            return true;
        }

        /// <summary>
        /// Расстояние по земле. Высота не считается: подойти к столу
        /// и подняться над ним — разные вещи, и вторая ничего не значит.
        /// </summary>
        public static float GroundDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Подошёл ли игрок к предмету настолько близко, чтобы предмет
        /// это заметил.
        /// </summary>
        public static bool Reached(Vector3 eye, Vector3 forward,
            Vector3 target, float radius)
        {
            if (radius <= 0f) return false;
            if (!TryGroundPoint(eye, forward, target.y, out var point)) return false;

            return GroundDistance(point, target) <= radius;
        }
    }
}
