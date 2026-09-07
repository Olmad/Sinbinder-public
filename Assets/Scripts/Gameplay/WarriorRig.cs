// Assets/Scripts/Gameplay/WarriorRig.cs
using UnityEngine;
using UnityEngine.AI;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Общая оснастка воина: всё, без чего он не воин, а декорация.
    ///
    /// Заведена после того, как выяснилось худшее из возможного: в демо
    /// <b>никто не мог сдвинуться с места</b>. Цепочка движения —
    /// <c>AOSWarriorWrapper.Execute</c> зовёт <see cref="UnitMover"/>,
    /// тот требует <see cref="NavMeshAgent"/>, тому нужен испечённый
    /// навмеш, — и собиралась она только в <c>UnitFactory</c>, которым
    /// пролог не пользуется. Спавнеры лепили воинов руками и звеньев
    /// не ставили.
    ///
    /// Последствия были не косметические. Охотники выходили в двенадцати
    /// метрах при дальности удара в два: бой не начинался вовсе. А на
    /// сцене 5 побег — это «довести отряд до края карты», и доводить
    /// было некого: круг оставался пуст, отсчёт не начинался, и демо
    /// вставало намертво.
    ///
    /// Поэтому оснастка теперь одна на всех, кто делает воинов. Три
    /// спавнера, разошедшиеся с фабрикой, — это ровно то, что здесь
    /// однажды уже случилось.
    /// </summary>
    public static class WarriorRig
    {
        /// <summary>
        /// Повесить всё общее. Возвращает <see cref="Damageable"/> —
        /// он нужен вызывающему для надголовного интерфейса.
        /// </summary>
        public static Damageable Attach(GameObject go, float speed = 3.5f)
        {
            if (go == null) return null;

            var damageable = Require<Damageable>(go);

            // Ноги. Радиус меньше стандартного: воины стоят кругом у костра
            // в трёх с половиной метрах, и на полуметровых агентах они
            // выталкивают друг друга из строя ещё до первого приказа.
            var agent = Require<NavMeshAgent>(go);
            agent.speed = speed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.radius = 0.35f;
            agent.height = 1.6f;
            agent.stoppingDistance = 1.2f;

            Require<UnitMover>(go);

            // Пол боя и цена приказа (docs/11-MISSING.md §2.3).
            Require<Fatigue>(go);
            Require<Engagement>(go);
            Require<AOS.RefusalPresenter>(go);

            // Первая ступень прозрачности.
            UI.OverheadBuilder.Attach(go, damageable);

            return damageable;
        }

        private static T Require<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
