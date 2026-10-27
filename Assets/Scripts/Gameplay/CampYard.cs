// Assets/Scripts/Gameplay/CampYard.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Двор уникальности на полигоне (53-GENERATOR §9, автор, 27.10: «место проверки
    /// уникальности. С интерактивными объектами»): костёр, сундук, стол, карты, палатки,
    /// дозор и край — те же места, что <see cref="CampLife"/> знает в лагере. Здесь
    /// созданные (<see cref="Crypt.WarriorForge"/>) живут, как в лагере: каждый идёт
    /// туда, куда тянет душа, — и видно, похожи двое или разные.
    ///
    /// Живут им только впущенные (<see cref="Admit"/>): девять подопытных полигона
    /// стоят там, куда их ставят рычаги, иначе опыт уходил бы к костру.
    ///
    /// Сам двор ничего не решает — только говорит <see cref="CampLife"/>, где что.
    /// </summary>
    public class CampYard : MonoBehaviour
    {
        [SerializeField] private Transform _chest;
        [SerializeField] private Transform _table;
        [SerializeField] private Transform _watch;
        [SerializeField] private Transform _apart;
        [SerializeField] private Transform _pit;

        private static readonly HashSet<Warrior> Residents = new();

        public static CampYard Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Instance = null; Residents.Clear(); }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Residents.Clear();
        }

        /// <summary>Впустить во двор: созданный живёт им, как в лагере.</summary>
        public static void Admit(Warrior w)
        {
            if (w != null) Residents.Add(w);
        }

        /// <summary>Живёт ли он двором.</summary>
        public static bool Lives(Warrior w) => Instance != null && w != null && Residents.Contains(w);

        public Vector3 Fire => transform.position;

        /// <summary>Место двора по названию. Ложь — такого места во дворе нет.</summary>
        public bool Point(AOS.CampSpot spot, out Vector3 at)
        {
            var t = spot switch
            {
                AOS.CampSpot.Chest => _chest,
                AOS.CampSpot.Table => _table,
                AOS.CampSpot.Watch => _watch,
                AOS.CampSpot.Apart => _apart,
                _ => null,
            };
            at = t != null ? t.position : default;
            return t != null;
        }

        /// <summary>Ямка копателя (<see cref="DiggerHabit"/>). Нет — null.</summary>
        public Transform Pit => _pit;
    }
}
