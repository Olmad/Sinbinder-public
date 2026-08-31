// Assets/_Project/Scripts/AOS Engine/MissionResolver.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Точка входа для автономных миссий: отряд ушёл, игрок не вмешивается,
    /// исход определяет личность командира.
    /// </summary>
    public class MissionResolver : MonoBehaviour
    {
        public static MissionResolver Instance { get; private set; }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public MissionOutcome Resolve(Warrior commander, Quest quest)
        {
            // Вся логика живёт в Quest.ResolveCommander: контекст, голосование
            // модулей и применение исхода. Здесь только вызов — иначе мы дублируем
            // конвейер и лезем в protected-члены квеста.
            return quest.ResolveCommander(commander);
        }
    }
}
