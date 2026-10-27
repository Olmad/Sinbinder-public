// Assets/Scripts/Gameplay/DiggerHabit.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Примета Марги Копателя: в лагере он то и дело уходит к краю и копает,
    /// и иногда что-то находит. Автор, 26 октября: «нужно, чтобы любой Игрок,
    /// даже самый слепой и тупой, понял — каждый уникален… Что Марга Копатель —
    /// постоянно находит закопанные сокровища» (28-ORDERS, «приметы воинов»).
    ///
    /// Прозвище объясняет себя без единого клика: сверху видно, что один из
    /// девятерых стоит на коленях у края поляны, рядом — лопата. Находка
    /// идёт в его карман (<see cref="Warrior.Pocket"/>) — тот самый карман,
    /// из-за которого он не слушается в набеге: примета и характер — одно.
    ///
    /// Когда — по часам и имени, без жребия: каждые <see cref="Period"/> секунд
    /// первые <see cref="DigFor"/> он копает в одной из трёх своих ямок (место
    /// тоже по имени). Только пока лагерь живёт и он свободен: приказ, разговор,
    /// набег — важнее. Где стоять, решает <see cref="CampLife.Where"/>, спрашивая
    /// здесь первым; как стоять — <see cref="WarriorAnimation.Digging"/>.
    /// </summary>
    [RequireComponent(typeof(Warrior))]
    public class DiggerHabit : MonoBehaviour
    {
        private const float Period = 70f;    // как часто уходит копать, секунд игры
        private const float DigFor = 24f;    // сколько из них — у ямки (дойти и копать)
        private const float Reach = 0.9f;    // ближе этого к ямке — копает
        private const float FindAfter = 8f;  // столько покопать, чтобы найти, секунд
        private const float Edge = 13.4f;    // ямки — у края поляны, за палатками (до 12,5)

        // Строки — черновик D; на сверку облаку (47-TEXTS, приметы).
        private static readonly string Found = Loc.N("{0} что-то выкопал — и сунул в карман.");
        private static readonly string Empty = Loc.N("{0} копает. Пока пусто.");

        private Warrior _warrior;
        private WarriorAnimation _body;
        private int _cycle = -1;
        private float _dugFor;
        private bool _told;

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
        }

        /// <summary>Куда идти копать сейчас; ложь — не время или не копатель.</summary>
        public static bool Point(Warrior w, out Vector3 at)
        {
            at = default;
            if (w == null || !w.TryGetComponent<DiggerHabit>(out var habit)) return false;
            return habit.Now(out at);
        }

        private bool Now(out Vector3 at)
        {
            at = default;
            if (_warrior == null || _warrior.IsDead || _warrior.HasCommand) return false;
            if (RaidEvent.Running) return false;

            float t = Time.time + Phase();
            if (t % Period > DigFor) return false;

            if (!CampLife.Centre(out var fire)) return false;

            // Три ямки по кругу у края поляны — по имени и номеру прохода.
            // Во дворе полигона ямка одна, своя (CampYard.Pit).
            int cycle = Mathf.FloorToInt(t / Period);
            float angle = (Phase() * 7f + (cycle % 3) * 118f) * Mathf.Deg2Rad;
            var yard = Object.FindFirstObjectByType<CampOpening>() == null ? CampYard.Instance : null;
            var pit = yard != null ? yard.Pit : null;
            at = pit != null ? pit.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.8f
                             : fire + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Edge;
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out var ground, 3f, UnityEngine.AI.NavMesh.AllAreas))
                at = ground.position;
            return true;
        }

        /// <summary>Сдвиг по часам — по имени: двое копателей не копали бы в ногу.</summary>
        private float Phase() => Wardrobe.Stamp(_warrior != null ? _warrior.DisplayName : "") % 40;

        void Update()
        {
            if (_body == null) _body = GetComponent<WarriorAnimation>();

            bool digging = CampLife.Enabled && CampLife.Idle(_warrior) && Now(out var at)
                && CampFocus.GroundDistance(transform.position, at) <= Reach;

            if (_body != null) _body.Digging = digging;

            int cycle = Mathf.FloorToInt((Time.time + Phase()) / Period);
            if (cycle != _cycle) { _cycle = cycle; _dugFor = 0f; _told = false; }
            if (!digging || _told) return;

            _dugFor += Time.deltaTime;
            if (_dugFor < FindAfter) return;

            // Находит через раз, по номеру прохода, — не каждый раз и не жребием.
            _told = true;
            bool lucky = cycle % 2 == 0;
            if (lucky) _warrior.Pocket(1 + Wardrobe.Stamp(_warrior.DisplayName + cycle) % 3);

            string line = Loc.F(lucky ? Found : Empty, Loc.Name(_warrior.DisplayName));
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
            if (lucky) UI.SpeechBubbles.Say(_warrior, Loc.T("Ага…"));
            Debug.Log($"[ПРИМЕТА] {_warrior.DisplayName} копает: {(lucky ? "нашёл" : "пусто")}");
        }

        void OnDisable()
        {
            if (_body != null) _body.Digging = false;
        }
    }
}
