// Assets/Scripts/Gameplay/WeaponPickup.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Оружие на стойке у входа в шатёр Греховода. Автор, 21 октября, п. 23:
    /// «хотелось бы иметь возможность самому доставать меч на кнопку. Вероятно,
    /// нужно меч положить рядом с катаной при входе, и игроку нужно будет поднять
    /// его на F, чтобы использовать — сделать как задачу. И нужна возможность
    /// взять катану — так интереснее».
    ///
    /// F рядом — оружие со стойки в руку Греховода: в ножнах на поясе, наголо —
    /// клавишей B (<see cref="WarriorAnimation"/>, <see cref="Armament"/>).
    /// Взял другое — прежнее возвращается на стойку. Строку F показывает
    /// <see cref="UI.GearPanel"/>, как у сундука и вещей.
    /// </summary>
    public class WeaponPickup : MonoBehaviour
    {
        [SerializeField] private string _kind = "Sword";

        private static readonly List<WeaponPickup> Live = new();
        private static WeaponPickup _held;
        private static readonly List<GameObject> _worn = new();

        /// <summary>Что у Греховода сейчас взято со стойки: «Sword», «Katana» или пусто.</summary>
        public static string Taken { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Live.Clear(); _held = null; _worn.Clear(); Taken = ""; }

        /// <summary>Новая игра: со стойки ничего не взято (Live — уже новой сцены, не трогаем).</summary>
        public static void Forget() { _held = null; _worn.Clear(); Taken = ""; }

        /// <summary>Задать из сборщика сцены.</summary>
        public void Configure(string kind) => _kind = kind;

        void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        void OnDisable() => Live.Remove(this);

        public string Prompt => _kind == "Katana" ? Loc.T("F — взять катану") : Loc.T("F — взять меч");

        private const float Reach = 1.8f;

        /// <summary>Оружие на стойке в пределах руки; ближайшее. Нет — null.</summary>
        public static WeaponPickup Nearest()
        {
            if (!SinbinderPlayer.Exists) return null;
            WeaponPickup best = null;
            float bestD = Reach;
            foreach (var p in Live)
            {
                if (p == null) continue;
                float d = CampFocus.GroundDistance(SinbinderPlayer.Where, p.transform.position);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>Взять со стойки. Прежнее взятое — обратно на стойку.</summary>
        public void Take()
        {
            var hero = SinbinderPlayer.Instance;
            if (hero == null) return;

            foreach (var go in _worn) if (go != null) Destroy(go);
            _worn.Clear();
            if (_held != null && _held != this) _held.gameObject.SetActive(true);

            string sheath = _kind == "Katana" ? "KatanaSaya" : "Scabbard";
            var inHand = Wardrobe.Wear(hero.gameObject, _kind);
            var stowed = Wardrobe.Wear(hero.gameObject, _kind + "Stowed");
            var case_ = Wardrobe.Wear(hero.gameObject, sheath);
            foreach (var go in new[] { inHand, stowed, case_ }) if (go != null) _worn.Add(go);

            // В первом лице тело выключено (RTS_Camera.ShowHero) — один раз,
            // при переключении. Надетое позже висело бы в воздухе само по себе:
            // видимость — как у тела.
            var body = hero.GetComponentInChildren<SkinnedMeshRenderer>();
            bool visible = body == null || body.enabled;
            foreach (var go in _worn)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;

            if (inHand != null)
            {
                var arms = hero.GetComponentInChildren<Armament>();
                if (arms == null) arms = hero.gameObject.AddComponent<Armament>();
                arms.Bind(inHand, stowed, drawn: false);
            }

            _held = this;
            Taken = _kind;
            gameObject.SetActive(false);

            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(_kind == "Katana"
                ? Loc.T("Катана на поясе. B — наголо или в ножны.")
                : Loc.T("Меч на поясе. B — наголо или в ножны."));
        }
    }
}
