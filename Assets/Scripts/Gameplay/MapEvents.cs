// Assets/Scripts/Gameplay/MapEvents.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Что Греховод добыл в экспедициях «Перетягивания» (54-TUG.md; автор, 28.10: «схемы строений — за
    /// экспедиции», «голему нужен рецепт, который можно получить только в экспедициях», «банки — в
    /// подходящих местах и в экспедициях»). У каждого Греховода — своё.
    /// </summary>
    public static class Trophies
    {
        public enum Kind { Treasury, Forge, Pillory, GolemRecipe, Jar, Coins }

        private static readonly Dictionary<string, HashSet<Kind>> Got = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Got.Clear();

        public static void Forget() => Got.Clear();

        public static bool Has(string lord, Kind k) => lord != null && Got.TryGetValue(lord, out var s) && s.Contains(k);

        /// <summary>Отдать трофей: банка — сразу в сумку (только игроку), монеты — в кошель, остальное — знание.</summary>
        public static string Give(string lord, Kind k)
        {
            if (string.IsNullOrEmpty(lord)) return "";
            if (!Got.TryGetValue(lord, out var s)) Got[lord] = s = new HashSet<Kind>();
            s.Add(k);
            switch (k)
            {
                case Kind.Jar: if (lord == Lords.Player) Satchel.AddJar(); return Loc.T("банка для душ");
                case Kind.Coins: Favor.AddCoins(lord, 8); return Loc.T("восемь монет");
                case Kind.GolemRecipe: return Loc.T("рецепт голема");
                case Kind.Treasury: return Loc.T("схема казны");
                case Kind.Forge: return Loc.T("схема кузни");
                default: return Loc.T("схема позорного столба");
            }
        }

        /// <summary>
        /// Что лежит в обозе номер n — по кругу, без жребия; круг сдвинут номером партии (MapLoader.Match):
        /// партия от партии — другие грузы, а в сетевой партии у всех одинаковые.
        /// </summary>
        public static Kind Cargo(int n) => ((n + MapLoader.Match) % 6) switch
        {
            0 => Kind.GolemRecipe,
            1 => Kind.Jar,
            2 => Kind.Treasury,
            3 => Kind.Forge,
            4 => Kind.Pillory,
            _ => Kind.Coins,
        };
    }

    /// <summary>
    /// Событие-экспедиция на карте «Перетягивания» (54-TUG.md: «события: вдвое больше, чем игроков, —
    /// экспедиции на месте; развилки решаются голосованием пошедших, как вылазки»). Два рода:
    ///
    /// * <b>Обоз</b> Ордена — охрана и груз (трофей: рецепт голема, банка, схема…). Охрану перебили — груз
    ///   тому Греховоду, чьи люди бились, и развилка: <i>разделить добычу между пошедшими</i> или <i>всё —
    ///   Греховоду</i>.
    /// * <b>Деревня в беде</b> — охотники у очага подлокации. Отбили — развилка: <i>остаться и помочь</i>
    ///   (жители — к этому Греховоду) или <i>взять припасы и уйти</i> (монеты, но жители запомнят).
    ///
    /// Развилку решают пошедшие (верные этого Греховода ближе 20 м) — каждый по своему греху, без жребия:
    /// жадные — делить, гордые — «всё владыке», унылые воздерживаются… Чем кончилось — вслух и в журнал.
    /// </summary>
    public class MapEvent : MonoBehaviour
    {
        private static readonly List<MapEvent> Live = new();
        private const float Witness = 20f;

        private string _kind = "caravan";
        private int _index;
        private readonly List<Warrior> _guards = new();
        private bool _done;
        private GameObject _mark;

        public bool Done => _done;
        public string Kind => _kind;
        public IReadOnlyList<Warrior> Guards => _guards;
        public static IReadOnlyList<MapEvent> All => Live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public void Configure(string kind, int index)
        {
            _kind = kind == "village" ? "village" : "caravan";
            _index = index;
        }

        // После выпечки навмеша (Start): охрана встаёт на навмеш.
        void Start()
        {
            Beacon();
            int count = _kind == "caravan" ? 3 : 3 + _index % 2;
            for (int i = 0; i < count; i++)
            {
                float a = (i * 120f + _index * 37f) * Mathf.Deg2Rad;
                var at = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (_kind == "caravan" ? 3f : 6f);
                var w = HunterWaves.Instance != null ? HunterWaves.Instance.Guard(at, transform.position) : null;
                if (w != null) _guards.Add(w);
            }
            if (_kind == "caravan") Cart();
            Debug.Log($"[СОБЫТИЯ] {name}: охраны {_guards.Count}");
        }

        void Update()
        {
            if (_done || TugMatch.Over || _guards.Count == 0) return;
            foreach (var g in _guards) if (g != null && !g.IsDead) return;
            Resolve();
        }

        /// <summary>Охрана пала: чьи люди бились — того и добыча; развилка — голосом пошедших.</summary>
        public void Resolve()
        {
            if (_done) return;
            _done = true;
            if (_mark != null) Destroy(_mark);

            string lord = Winner();
            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            if (lord == null) { log?.Write(Loc.T("Охрана перебита, но никто не взял своё.")); return; }

            var went = new List<Warrior>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !w.IsDead && w.Team == Team.Neutral && Allegiance.Dearest(w.Id) == lord
                    && CampFocus.GroundDistance(w.transform.position, transform.position) <= Witness) went.Add(w);

            // Голоса: за первое (делить / остаться) — и за второе (владыке / уйти с припасами).
            int first = 0, second = 0;
            foreach (var w in went)
            {
                if (w.Soul == null) continue;
                switch (w.Soul.Sin)
                {
                    case SinType.Greed: case SinType.Envy: case SinType.Lust: case SinType.Gluttony:
                        if (_kind == "caravan") first++; else second++; break;
                    case SinType.Pride: case SinType.Wrath:
                        if (_kind == "caravan") second++; else first++; break;
                    default: break;   // унылые воздерживаются
                }
            }
            bool shareOrStay = first >= second;

            if (_kind == "caravan")
            {
                string cargo = Trophies.Give(lord, Trophies.Cargo(_index));
                log?.Write(Loc.F("Обоз взят: {0} — {1}. Пошедших {2}: {3} за то, чтобы разделить, {4} — всё владыке.",
                                 lord, cargo, went.Count, first, second));
                foreach (var w in went)
                {
                    bool greedy = w.Soul != null && (w.Soul.Sin == SinType.Greed || w.Soul.Sin == SinType.Envy);
                    if (shareOrStay) Favor.Nudge(w, lord, greedy ? 12f : 6f, greedy ? Loc.T("Вот это по-честному, владыка.") : null);
                    else { Favor.Nudge(w, lord, greedy ? -6f : 2f, greedy ? Loc.T("Всё — ему. Как всегда.") : null); }
                }
                if (!shareOrStay) Favor.AddCoins(lord, 10);
                else Favor.AddCoins(lord, 2);
            }
            else
            {
                var home = NearestHome();
                if (shareOrStay)
                {
                    log?.Write(Loc.F("Деревню отбили. Остались помочь — жители запомнили {0}.", lord));
                    if (home != null) foreach (var r in home.Folk) if (r != null && !r.IsDead) Favor.Nudge(r, lord, 10f, null);
                    foreach (var w in went) Favor.Nudge(w, lord, -2f, null);
                }
                else
                {
                    log?.Write(Loc.F("Деревню отбили и забрали припасы. Жители запомнили и это — {0}.", lord));
                    Favor.AddCoins(lord, 6);
                    if (home != null) foreach (var r in home.Folk) if (r != null && !r.IsDead) Favor.Nudge(r, lord, -10f, null);
                    foreach (var w in went)
                        if (w.Soul != null && w.Soul.Sin == SinType.Greed) Favor.Nudge(w, lord, 4f, Loc.T("Припасы — это дело."));
                }
            }
            Debug.Log($"[СОБЫТИЯ] {name}: взял {lord}, пошедших {went.Count}, голоса {first}:{second}");
        }

        /// <summary>Чьи люди бились: у кого больше верных рядом; никого — Греховод, что стоит ближе 20 м.</summary>
        private string Winner()
        {
            var count = new Dictionary<string, int>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || CampFocus.GroundDistance(w.transform.position, transform.position) > Witness) continue;
                string lord = Lords.Of(w) ?? Allegiance.Dearest(w.Id);
                if (lord == null) continue;
                count.TryGetValue(lord, out var n);
                count[lord] = n + (Lords.Of(w) != null ? 1 : 2);
            }
            string best = null;
            int top = 0;
            foreach (var kv in count) if (kv.Value > top) { top = kv.Value; best = kv.Key; }
            return best;
        }

        private Residents NearestHome()
        {
            Residents best = null;
            float d = float.MaxValue;
            foreach (var r in Object.FindObjectsByType<Residents>(FindObjectsSortMode.None))
            {
                float k = CampFocus.GroundDistance(r.transform.position, transform.position);
                if (k < d) { d = k; best = r; }
            }
            return best;
        }

        // ---------------------------------------------------------- вид

        /// <summary>Знак события — столб огня над местом: его видно издали, через туман (FogOfWar.Reveal).</summary>
        private void Beacon()
        {
            _mark = new GameObject("Знак события");
            _mark.transform.SetParent(transform, false);
            _mark.transform.localPosition = Vector3.up * 6f;
            var light = _mark.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 14f;
            light.intensity = 3f;
            light.color = _kind == "caravan" ? new Color(1f, 0.78f, 0.35f) : new Color(0.95f, 0.35f, 0.25f);
            FogOfWar.Reveal(transform.position, 8f);
        }

        /// <summary>Телега обоза: ящики и бочка — груз, за которым идут.</summary>
        private void Cart()
        {
            foreach (var (prop, at) in new[] { ("Crate", new Vector3(0f, 0f, 0f)), ("Crate", new Vector3(0.9f, 0f, 0.2f)), ("Barrel", new Vector3(-0.8f, 0f, -0.3f)) })
            {
                var p = MapLoader.Prefab(prop);
                if (p == null) continue;
                var go = Instantiate(p, transform);
                go.transform.SetPositionAndRotation(transform.position + at, Quaternion.Euler(0f, _index * 29f, 0f) * p.transform.rotation);
                go.transform.localScale = p.transform.localScale;
            }
        }
    }
}
