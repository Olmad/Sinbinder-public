// Assets/Scripts/Gameplay/RivalLord.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Греховод-ИИ «Перетягивания» (54-TUG.md, шаг 3: «ИИ — такой же Греховод, ему тоже могут
    /// отказать»). Тот же голос, что у игрока: приказы ничьим с его именем (<see cref="Lords"/>),
    /// слушаются его по верности, располагает их тем же (<see cref="Favor"/>). Без жребия — по формуле.
    ///
    /// Два замысла:
    /// * <b>Собрать</b> — идёт к ближнему очагу, дарит и зовёт за собой: послушание растит верность.
    /// * <b>Напасть</b> — когда верных хватает: ведёт их на склеп ближней живой стороны — дверь,
    ///   потом трон — и указывает на её Греховода; сам — домой.
    /// И всегда — <b>защита</b>: склеп бьют — верные бьют того, кто бьёт.
    ///
    /// Слабый, средний, сильный — как часто думает, сколько верных ему надо, кому дарит.
    /// Пал — сторона выбыла (<see cref="TugMatch"/>).
    /// </summary>
    public class RivalLord : MonoBehaviour
    {
        private static readonly List<RivalLord> Live = new();

        public string Lord { get; private set; }
        public int Side { get; private set; }
        public MapSession.Seat Level { get; private set; }
        public int Orders { get; private set; }
        public bool Attacking { get; private set; }

        private Warrior _body;
        private UnitMover _legs;
        private float _next;
        private bool _muted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public static IReadOnlyList<RivalLord> All => Live;

        /// <summary>Чей голос у этого тела; не Греховод-ИИ — null.</summary>
        public static string LordOf(Warrior w)
        {
            foreach (var r in Live) if (r != null && r._body == w) return r.Lord;
            return null;
        }

        public void Configure(string lord, int side, MapSession.Seat level)
        {
            Lord = lord;
            Side = side;
            Level = level;
            _body = GetComponent<Warrior>();
            _legs = GetComponent<UnitMover>();
            _next = Time.time + 2f + side;   // не все разом
        }

        private float Beat => Level switch { MapSession.Seat.AiStrong => 3.5f, MapSession.Seat.AiMedium => 6f, _ => 10f };
        // На поле на двоих — тридцать жителей: девять верных одному было не собрать (длинная партия 28.10).
        private int Need => Level switch { MapSession.Seat.AiStrong => 6, MapSession.Seat.AiMedium => 5, _ => 3 };
        private const float Voice = 16f;

        void Update()
        {
            if (_body == null) return;
            // Решает не душа, а ум Греховода: голосование обычного воина (если движок его выдал) —
            // выключить, один раз. Снять нельзя: за него держится удар (AutoAttackAOS).
            if (!_muted && TryGetComponent<AOS.AOSWarriorWrapper>(out var brain)) { brain.enabled = false; _muted = true; }
            // Пал — лежит до восстания у трона (TugMatch.Rise ставит новое тело).
            if (_body.IsDead) return;

            // Кошель павшего у ног — поднять, проходя (тот же, что игрок поднимает по F).
            var purse = LordPurse.Near(transform.position);
            if (purse != null) purse.Take(Lord);

            if (TugMatch.Over || Time.time < _next) return;
            _next = Time.time + Beat;

            var match = TugMatch.Instance;
            (CryptDoor door, Sturdy throne) mine = match != null ? match.PartsOf(Side) : default;
            var followers = Followers();

            // Своя дверь — открыта: Греховоду надо выходить и возвращаться.
            if (mine.door != null && !mine.door.Open && !Attacking) mine.door.Toggle();

            // Защита: склеп бьют — верные бьют того, кто бьёт.
            var leaf = mine.door != null ? mine.door.Leaf.GetComponent<Sturdy>() : null;
            var hit = Recent(leaf) ?? Recent(mine.throne);
            if (hit != null)
            {
                Hostility.Mark(Lord, hit);
                foreach (var f in followers) Order(f, CommandKind.Attack, hit.transform.position, hit.gameObject);
                return;
            }

            if (!Attacking && followers.Count >= Need) Attacking = true;
            else if (Attacking && followers.Count < Need / 2) Attacking = false;

            if (Attacking) Attack(match, followers);
            else Recruit(followers);
        }

        private static Warrior Recent(Sturdy s)
            => s != null && s.LastBy != null && !s.LastBy.IsDead && Time.time - s.LastHitAt < 6f ? s.LastBy : null;

        /// <summary>Верные ему: верней всех ему и хотя бы на <see cref="Hostility.Follows"/>.</summary>
        private List<Warrior> Followers()
        {
            var list = new List<Warrior>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !w.IsDead && w.Team == Team.Neutral && w != _body
                    && Allegiance.Dearest(w.Id) == Lord && Allegiance.Of(w.Id, w.Loyalty, Lord) >= Hostility.Follows)
                    list.Add(w);
            return list;
        }

        // ---------------------------------------------------------- собрать

        private void Recruit(List<Warrior> followers)
        {
            // Ближний очаг, где ещё есть не его люди.
            Residents best = null;
            float bestD = float.MaxValue;
            foreach (var r in Object.FindObjectsByType<Residents>(FindObjectsSortMode.None))
            {
                bool someone = false;
                foreach (var w in r.Folk) if (w != null && !w.IsDead && !followers.Contains(w)) { someone = true; break; }
                if (!someone) continue;
                float d = CampFocus.GroundDistance(transform.position, r.transform.position);
                if (d < bestD) { bestD = d; best = r; }
            }
            if (best == null) return;

            if (bestD > 10f) { _legs?.CommandMove(best.transform.position); return; }

            // У очага: по трое за раз — кого подарком, кого зовом за собой.
            int asked = 0;
            foreach (var w in best.Folk)
            {
                if (w == null || w.IsDead || followers.Contains(w)) continue;
                if (CampFocus.GroundDistance(transform.position, w.transform.position) > Voice) continue;
                if (Gifts(w.Soul)) Favor.Give(w, Lord);
                Order(w, CommandKind.Move, transform.position + (w.transform.position - transform.position).normalized * 2.5f, null);
                if (++asked >= 3) break;
            }
        }

        /// <summary>Кому дарит: слабый — жадным, средний — ещё завистливым и похотливым, сильный — всем, кроме гордых.</summary>
        private bool Gifts(SoulData soul)
        {
            if (soul == null) return false;
            if (soul.Sin == SinType.Greed) return true;
            if (Level == MapSession.Seat.AiWeak) return false;
            if (soul.Sin == SinType.Envy || soul.Sin == SinType.Lust) return true;
            return Level == MapSession.Seat.AiStrong && soul.Sin != SinType.Pride;
        }

        // ---------------------------------------------------------- напасть

        private void Attack(TugMatch match, List<Warrior> followers)
        {
            if (match == null) return;
            int foe = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < match.Crypts.Count; i++)
            {
                if (i == Side || match.Out(i) || match.Crypts[i] == null) continue;
                float d = CampFocus.GroundDistance(transform.position, match.Crypts[i].position);
                if (d < bestD) { bestD = d; foe = i; }
            }
            if (foe < 0) return;

            var (door, throne) = match.PartsOf(foe);
            var target = door != null && !door.Open ? door.Leaf.gameObject : throne != null ? throne.gameObject : null;
            if (target == null) return;

            // На Греховода той стороны указал — верные бьют его и без приказа.
            if (foe == match.PlayerSide && SinbinderPlayer.Exists) Hostility.Mark(Lord, SinbinderPlayer.Instance);
            foreach (var r in Live) if (r != null && r.Side == foe && r._body != null) Hostility.Mark(Lord, r._body);

            foreach (var f in followers)
                if (!(f.HasCommand && f.Command.Target == target)) Order(f, CommandKind.Attack, target.transform.position, target);

            // Сам — домой, к трону.
            var home = match.PartsOf(Side).throne;
            if (home != null && CampFocus.GroundDistance(transform.position, home.transform.position) > 4f)
                _legs?.CommandMove(home.transform.position + home.transform.forward * 2f);
        }

        private void Order(Warrior w, CommandKind kind, Vector3 point, GameObject target)
        {
            w.IssueCommand(kind, point, target, 0f, Lord);
            Orders++;
        }
    }
}
