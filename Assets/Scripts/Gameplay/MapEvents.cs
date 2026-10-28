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
    }

    /// <summary>
    /// Событие-экспедиция на месте карты «Перетягивания» — по записи <see cref="ExpeditionBook"/> (тридцать
    /// родов). Какой род где — перестановка по номеру партии (<see cref="Pick"/>): без жребия, каждая партия —
    /// новый расклад, в сети — одинаковый у всех; подряд роды не повторяются, пока не кончатся все. События
    /// живут на карте одновременно, у каждого свой срок (<see cref="FightLife"/>, <see cref="ParleyLife"/>);
    /// где и сколько — <see cref="EventDirector"/>.
    ///
    /// Боевое: стража пала — добыча тому Греховоду, чьи люди бились; развилку решают пошедшие (верные этого
    /// Греховода ближе 20 м) голосом своего греха. Мирное: Греховод у места выбирает сам — 1 или 2.
    /// Награды — в рамках записи, выбор внутри рамок — от номера партии.
    /// </summary>
    public class MapEvent : MonoBehaviour
    {
        private static readonly List<MapEvent> Live = new();
        private const float Witness = 20f;
        private const float Talk = 6f;

        /// <summary>Сколько событие ждёт: обоз проходит, торговец уходит, пленника казнят.</summary>
        public const float FightLife = 300f;
        public const float ParleyLife = 240f;

        private int _j;
        private float _endsAt;
        private EventDirector _director;
        private ExpeditionBook.Entry _entry;
        private readonly List<Warrior> _guards = new();
        private readonly List<Warrior> _camp = new();
        private string _owner;           // дезертиры — чьи были
        private bool _done;
        private GameObject _mark, _things;
        private Residents _home;
        private bool _near;

        public bool Done => _done;
        public string Kind => _entry != null ? _entry.Id : "";
        public ExpeditionBook.Entry Entry => _entry;
        public bool Parley => _entry != null && _entry.Parley;
        public IReadOnlyList<Warrior> Guards => _guards;
        public static IReadOnlyList<MapEvent> All => Live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        /// <summary>Номер события в партии: от него — род (перестановка) и награды в рамках.</summary>
        public void Configure(EventDirector director, int j) { _director = director; _j = j; }

        public int Number => _j;

        /// <summary>
        /// Какой род у j-го события партии: шаг перестановки взаимно прост с числом родов, сдвиг и шаг — от
        /// номера партии. Так за 30 событий выйдут все 30 родов, каждый раз в другом порядке.
        /// </summary>
        public static int Pick(int j)
        {
            int n = ExpeditionBook.All.Length;
            int m = Mathf.Abs(MapLoader.Match);
            int step = 1 + (m * 7) % Mathf.Max(1, n - 1);
            while (Gcd(step, n) != 1) step++;
            return ((m * 13) % n + j * step) % n;
        }

        private static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }

        /// <summary>Число в рамках [min, max] — от номера партии и номера события.</summary>
        private int Within(int min, int max, int salt) => max <= min ? min : min + Mathf.Abs(MapLoader.Match * 31 + _j * 17 + salt) % (max - min + 1);

        // После выпечки навмеша (Start): стража встаёт на навмеш.
        void Start() => Ensure();

        /// <summary>Встать событию (Start; прогон зовёт сразу, не дожидаясь кадра). Повторный вызов — ничего.</summary>
        public void Ensure()
        {
            if (_entry != null) return;
            _home = gameObject.AddComponent<Residents>();
            _home.Configure("village", 6f, 0, 0);
            Open();
        }

        private void Open()
        {
            _entry = ExpeditionBook.All[Pick(_j)];
            _done = false;
            _endsAt = Time.time + (_entry.Parley ? ParleyLife : FightLife);
            _guards.Clear();
            _camp.Clear();
            _owner = null;
            name = Loc.F("Событие — {0}", Loc.T(_entry.Title));
            _things = new GameObject("Вещи события");
            _things.transform.SetParent(transform, false);

            for (int i = 0; i < _entry.Guards; i++)
            {
                float a = (i * 360f / _entry.Guards + _j * 37f) * Mathf.Deg2Rad;
                var at = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4f;
                var w = HunterWaves.Instance != null ? HunterWaves.Instance.Guard(at, transform.position, _entry.GuardShell) : null;
                if (w != null) _guards.Add(w);
            }
            if (_entry.Camp > 0)
            {
                var rivals = RivalLord.All;
                _owner = rivals.Count > 0 && rivals[_j % rivals.Count] != null ? rivals[_j % rivals.Count].Lord : null;
                for (int i = 0; i < _entry.Camp; i++)
                {
                    float a = (i * 120f + _j * 53f) * Mathf.Deg2Rad;
                    var w = _home.Newcomer(MapLoader.Match * 1000 + _j * 10 + i, transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.5f);
                    if (w == null) continue;
                    _camp.Add(w);
                    if (_owner != null) Allegiance.Change(w.Id, w.Loyalty, _owner, 55f - Allegiance.Of(w.Id, w.Loyalty, _owner));
                }
            }
            Props();
            Beacon();
            Debug.Log($"[СОБЫТИЯ] событие {_j} у ({transform.position.x:0}, {transform.position.z:0}): {_entry.Id} — стражи {_guards.Count}, у огня {_camp.Count}");
        }

        void Update()
        {
            if (_entry == null || _done || TugMatch.Over) return;

            // Время уходит: знак тускнеет (без цифр), вышло — событие ушло без тех, кто не успел.
            if (_mark != null && _mark.TryGetComponent<Light>(out var light))
                light.intensity = 0.8f + 2.2f * Mathf.Clamp01((_endsAt - Time.time) / (_entry.Parley ? ParleyLife : FightLife));
            if (Time.time >= _endsAt) { Expire(); return; }

            if (_entry.Parley)
            {
                _near = SinbinderPlayer.Exists && CampFocus.GroundDistance(SinbinderPlayer.Where, transform.position) <= Talk;
                if (_near && Input.GetKeyDown(KeyCode.Alpha1)) Choose(0, Lords.Player);
                else if (_near && Input.GetKeyDown(KeyCode.Alpha2)) Choose(1, Lords.Player);
                else
                    foreach (var rl in RivalLord.All)
                        if (rl != null && CampFocus.GroundDistance(rl.transform.position, transform.position) <= Talk)
                        { Choose(Favor.PurseOf(rl.Lord) >= _entry.A.Cost ? 0 : 1, rl.Lord); break; }
                return;
            }
            if (_guards.Count == 0) return;
            foreach (var g in _guards) if (g != null && !g.IsDead) return;
            Resolve();
        }

        /// <summary>Время вышло: стража уходит, вещи увозят; люди у огня остаются ничьими на карте.</summary>
        public void Expire()
        {
            if (_done) return;
            Log(Loc.F("{0}: не успели — ушло.", Loc.T(_entry.Title)));
            Debug.Log($"[СОБЫТИЯ] {_entry.Id}: ушло по времени");
            foreach (var g in _guards) if (g != null && !g.IsDead) Destroy(g.gameObject);
            Finish();
        }

        /// <summary>Убрать место: люди события (пришедшие, дезертиры) остаются на карте, место — пустеет.</summary>
        private void Close()
        {
            var keep = transform.parent;
            foreach (var w in _home.Folk) if (w != null) w.transform.SetParent(keep, true);
            Destroy(gameObject);
        }

        /// <summary>Стража пала: чьи люди бились — того и добыча; развилку решают пошедшие голосом греха.</summary>
        public void Resolve()
        {
            if (_done || _entry == null) return;
            string lord = Winner();
            if (lord == null)
            {
                Finish();
                Log(Loc.F("{0}: стража перебита, но никто не взял своё.", Loc.T(_entry.Title)));
                return;
            }
            var went = Went(lord, Witness);
            int a = 0, b = 0;
            foreach (var w in went)
            {
                if (w.Soul == null) continue;
                if (System.Array.IndexOf(_entry.A.Favour, w.Soul.Sin) >= 0) a++;
                else if (System.Array.IndexOf(_entry.B.Favour, w.Soul.Sin) >= 0) b++;
            }
            var pick = a >= b ? _entry.A : _entry.B;
            string got = Apply(lord, pick, went);
            Log(Loc.F("{0}: взял {1}. Пошедших {2}: «{3}» — {4}, «{5}» — {6}. {7}", Loc.T(_entry.Title), lord, went.Count,
                      Loc.T(_entry.A.Label), a, Loc.T(_entry.B.Label), b, got));
            Debug.Log($"[СОБЫТИЯ] {_entry.Id}: взял {lord}, пошедших {went.Count}, голоса {a}:{b}, выбрано {(pick == _entry.A ? "A" : "B")}");
        }

        /// <summary>Мирное: Греховод у места выбирает сам. Не хватило монет — событие ждёт.</summary>
        public bool Choose(int which, string lord)
        {
            if (_done || _entry == null || lord == null) return false;
            var pick = which == 0 ? _entry.A : _entry.B;
            if (pick.Cost > 0 && !Favor.Spend(lord, pick.Cost))
            {
                if (lord == Lords.Player) Log(Loc.F("{0}: не хватает монет.", Loc.T(_entry.Title)));
                return false;
            }
            string got = Apply(lord, pick, Went(lord, Witness));
            Log(Loc.F("{0}: {1} — «{2}». {3}", Loc.T(_entry.Title), lord, Loc.T(pick.Label), got));
            Debug.Log($"[СОБЫТИЯ] {_entry.Id}: {lord} выбрал {(which == 0 ? "A" : "B")}");
            return true;
        }

        /// <summary>Последствия развилки. Возвращает, что досталось, — словами для журнала.</summary>
        private string Apply(string lord, ExpeditionBook.Option o, List<Warrior> went)
        {
            Finish();
            var said = new List<string>();
            bool me = lord == Lords.Player;

            int coins = Within(o.CoinsMin, o.CoinsMax, 1);
            if (o.Gamble) coins = (MapLoader.Match * 7 + _j * 3) % 3 != 0 ? o.CoinsMax : 0;
            if (o.Ransom && _owner != null)
            {
                int paid = Mathf.Min(coins, Favor.PurseOf(_owner));
                Favor.Spend(_owner, paid);
                coins = paid;
            }
            if (coins > 0) { Favor.AddCoins(lord, coins); said.Add(Loc.F("монет {0}", coins)); }
            else if (o.Gamble) said.Add(Loc.T("кости легли против"));

            if (o.Trophy.Length > 0)
            {
                var k = o.Trophy[Within(0, o.Trophy.Length - 1, 2)];
                said.Add(Trophies.Give(lord, k));
            }
            if (me)
            {
                for (int i = 0; i < o.Jars; i++) if (Satchel.AddJar()) said.Add(Loc.T("банка для душ"));
                Remains.Add(Remains.Stuff.Stone, o.Stone);
                Remains.Add(Remains.Stuff.Bones, o.Bones);
                Remains.Add(Remains.Stuff.Flesh, o.Flesh);
                if (o.Stone > 0) said.Add(Loc.F("камня {0}", o.Stone));
                if (o.Bones > 0) said.Add(Loc.F("костей {0}", o.Bones));
                if (o.Flesh > 0) said.Add(Loc.F("плоти {0}", o.Flesh));
                int souls = 0;
                for (int i = 0; i < o.Souls; i++)
                {
                    int jar = Satchel.FreeJar();
                    if (jar < 0) break;
                    var k = WarriorGenerator.Make(MapLoader.Match * 977 + _j * 7 + i, SoulOrigin.Sortie, new List<string>());
                    var soul = new SoulData(k.Name, k.Sin, k.Moral, 1 + k.Number % 3, k.Strength, k.Seed);
                    if (Satchel.Fill(jar, soul, SoulQuality.Acceptance)) souls++;
                }
                if (souls > 0) said.Add(Loc.F("душ в банках {0}", souls));
            }

            for (int i = 0; i < o.Recruits; i++)
            {
                var w = _home.Newcomer(MapLoader.Match * 1000 + _j * 10 + 5 + i, transform.position + Vector3.right * (1.5f + i));
                if (w == null) continue;
                Allegiance.Change(w.Id, w.Loyalty, lord, o.RecruitTrust - Allegiance.Of(w.Id, w.Loyalty, lord));
                said.Add(Loc.F("пришёл {0}", Loc.Name(w.DisplayName)));
            }
            if (o.Deserters)
                foreach (var w in _camp)
                {
                    if (w == null || w.IsDead) continue;
                    Allegiance.Change(w.Id, w.Loyalty, lord, 62f - Allegiance.Of(w.Id, w.Loyalty, lord));
                    if (_owner != null) Allegiance.Change(w.Id, w.Loyalty, _owner, -30f);
                    said.Add(Loc.F("перешёл {0}", Loc.Name(w.DisplayName)));
                }
            if (o.Ransom && _owner != null)
                foreach (var w in _camp) if (w != null && !w.IsDead) Allegiance.Change(w.Id, w.Loyalty, _owner, 10f);

            string line = Loc.T(o.Line);
            bool spoke = false;
            foreach (var w in went)
            {
                if (w == null || w.Soul == null) continue;
                float d = o.Followers;
                bool pleased = System.Array.IndexOf(o.PleasedSins, w.Soul.Sin) >= 0;
                bool vexed = System.Array.IndexOf(o.VexedSins, w.Soul.Sin) >= 0;
                if (pleased) d += o.Pleased;
                if (vexed) d -= o.Vexed;
                if (w.Soul.Moral == MoralType.Pious) d += o.Pious;
                bool say = !spoke && !string.IsNullOrEmpty(o.Line) && (pleased || vexed || (o.PleasedSins.Length == 0 && o.VexedSins.Length == 0));
                if (say) spoke = true;
                if (Mathf.Abs(d) > 0.01f || say) Favor.Nudge(w, lord, d, say ? line : null);
            }

            if (Mathf.Abs(o.Residents) > 0.01f || Mathf.Abs(o.Pious) > 0.01f)
            {
                var home = NearestVillage();
                if (home != null)
                    for (int i = 0; i < home.Folk.Count; i++)
                    {
                        var r = home.Folk[i];
                        if (r == null || r.IsDead || r.Soul == null) continue;
                        float d = o.SplitResidents ? (i % 2 == 0 ? o.Residents : -o.Residents) : o.Residents;
                        if (r.Soul.Moral == MoralType.Pious) d += o.Pious;
                        if (Mathf.Abs(d) > 0.01f) Favor.Nudge(r, lord, d, null);
                    }
                if (o.Residents > 0.01f && !o.SplitResidents) said.Add(Loc.T("жители запомнили добро"));
                else if (o.Residents < -0.01f) said.Add(Loc.T("жители запомнили и это"));
                else if (o.SplitResidents) said.Add(Loc.T("одни запомнили добро, другие — обиду"));
            }
            if (o.DelayHunters > 0f && HunterWaves.Instance != null)
            {
                HunterWaves.Instance.Delay(o.DelayHunters);
                said.Add(Loc.T("охотники задержатся"));
            }
            return said.Count > 0 ? string.Join(", ", said) + "." : "";
        }

        private void Finish()
        {
            _done = true;
            if (_mark != null) Destroy(_mark);
            _director?.Ended(this);
            Invoke(nameof(Close), 8f);
        }

        private static void Log(string text) => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(text);

        private List<Warrior> Went(string lord, float radius)
        {
            var went = new List<Warrior>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !w.IsDead && w.Team == Team.Neutral && Allegiance.Dearest(w.Id) == lord && !_camp.Contains(w)
                    && CampFocus.GroundDistance(w.transform.position, transform.position) <= radius) went.Add(w);
            return went;
        }

        /// <summary>Чьи люди бились: у кого больше верных рядом (Греховод сам — за одного, верный — за двоих).</summary>
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

        private Residents NearestVillage()
        {
            Residents best = null;
            float d = float.MaxValue;
            foreach (var r in Object.FindObjectsByType<Residents>(FindObjectsSortMode.None))
            {
                if (r == _home || r.GetComponent<MapEvent>() != null) continue;
                float k = CampFocus.GroundDistance(r.transform.position, transform.position);
                if (k < d) { d = k; best = r; }
            }
            return best;
        }

        // ---------------------------------------------------------- вид

        /// <summary>Знак события — свет над местом: его видно издали, через туман (FogOfWar.Reveal).</summary>
        private void Beacon()
        {
            _mark = new GameObject("Знак события");
            _mark.transform.SetParent(transform, false);
            _mark.transform.localPosition = Vector3.up * 6f;
            var light = _mark.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 14f;
            light.intensity = 3f;
            light.color = _entry.Parley ? new Color(0.55f, 0.75f, 1f) : new Color(1f, 0.70f, 0.35f);
            FogOfWar.Reveal(transform.position, 8f);
        }

        /// <summary>Вещи места по записи: ящики, бочки, камни, факел — то, за чем идут.</summary>
        private void Props()
        {
            for (int i = 0; i < _entry.Props.Length; i++)
            {
                var p = MapLoader.Prefab(_entry.Props[i]);
                if (p == null) continue;
                var go = Instantiate(p, _things.transform);
                var at = new Vector3((i - (_entry.Props.Length - 1) * 0.5f) * 1.1f, 0f, (i % 2) * 0.4f);
                go.transform.SetPositionAndRotation(transform.position + Quaternion.Euler(0f, _j * 29f, 0f) * at,
                                                    Quaternion.Euler(0f, _j * 29f + i * 41f, 0f) * p.transform.rotation);
                go.transform.localScale = p.transform.localScale;
            }
        }

        /// <summary>Мирное событие рядом: что это и чем кончится каждая развилка.</summary>
        void OnGUI()
        {
            if (!_near || _done || _entry == null || !_entry.Parley || TugMatch.Over) return;
            var box = new Rect(Screen.width * 0.5f - 300f, Screen.height - 230f, 600f, 140f);
            GUILayout.BeginArea(box, GUI.skin.box);
            GUILayout.Label(Loc.T(_entry.Title), new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold });
            GUILayout.Label(Loc.T(_entry.Story), new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label(Option(1, _entry.A));
            GUILayout.Label(Option(2, _entry.B));
            GUILayout.EndArea();
        }

        private static string Option(int key, ExpeditionBook.Option o)
            => o.Cost > 0 ? Loc.F("{0} — {1} (монет: {2})", key, Loc.T(o.Label), o.Cost) : Loc.F("{0} — {1}", key, Loc.T(o.Label));
    }

    /// <summary>
    /// Ведёт события экспедиций на карте (автор, 28.10: «странно, что меняется целая карта, чтобы появилось
    /// новое событие… они должны параллельно существовать»). Одновременно — по два на игрока. Места: те, что
    /// поставил автор карты, и по формуле — у подлокаций и между склепами (спорная земля). Кончилось событие —
    /// через <see cref="Pause"/> с новое встаёт на другом свободном месте. Без жребия: порядок мест и родов —
    /// от номера партии.
    /// </summary>
    public class EventDirector : MonoBehaviour
    {
        public const float Pause = 60f;
        private const float FromCrypt = 30f, Apart = 16f;

        private readonly List<Vector3> _places = new();
        private readonly List<float> _due = new();
        private int _target = 2, _next, _cursor;

        public static EventDirector Instance { get; private set; }
        public IReadOnlyList<Vector3> Places => _places;
        public int Target => _target;
        /// <summary>Сколько новых событий ждут своей паузы.</summary>
        public int Waiting => _due.Count;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Configure(MapData map)
        {
            _places.Clear();
            var crypts = new List<Vector3>();
            foreach (var c in map.Crypts) crypts.Add(new Vector3(c.X, 0f, c.Z));
            float edge = map.Size * 0.5f - 12f;

            var wish = new List<Vector3>();
            foreach (var s in map.Events) wish.Add(new Vector3(s.X, 0f, s.Z));
            for (int i = 0; i < crypts.Count; i++)
                for (int k = i + 1; k < crypts.Count; k++) wish.Add((crypts[i] + crypts[k]) * 0.5f);
            for (int i = 0; i < map.Locations.Count; i++)
            {
                var l = map.Locations[i];
                var at = new Vector3(l.X, 0f, l.Z);
                for (int k = 0; k < 2; k++)
                {
                    float a = (i * 73f + k * 180f + 45f) * Mathf.Deg2Rad;
                    wish.Add(at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (l.Radius + 14f));
                }
            }
            foreach (var p in wish)
            {
                if (Mathf.Abs(p.x) > edge || Mathf.Abs(p.z) > edge) continue;
                bool ok = true;
                foreach (var c in crypts) if (CampFocus.GroundDistance(p, c) < FromCrypt) ok = false;
                foreach (var q in _places) if (CampFocus.GroundDistance(p, q) < Apart) ok = false;
                if (ok) _places.Add(p);
            }
            _target = Mathf.Min(_places.Count, Mathf.Max(2, map.Players * 2));
            _cursor = Mathf.Abs(MapLoader.Match * 5) % Mathf.Max(1, _places.Count);
            for (int i = 0; i < _target; i++) _due.Add(0f);
            Debug.Log($"[СОБЫТИЯ] мест {_places.Count}, разом {_target}");
        }

        void Update()
        {
            if (TugMatch.Over) return;
            for (int i = _due.Count - 1; i >= 0; i--)
                if (Time.time >= _due[i]) { _due.RemoveAt(i); Spawn(); }
        }

        /// <summary>Событие кончилось — через паузу встанет новое.</summary>
        public void Ended(MapEvent e) => _due.Add(Time.time + Pause);

        /// <summary>Поставить следующее событие на ближайшее по кругу свободное место; номер — следующий.</summary>
        public MapEvent Spawn() => Spawn(_next);

        /// <summary>Поставить событие с этим номером (прогон берёт нужный род).</summary>
        public MapEvent Spawn(int j)
        {
            if (_places.Count == 0) return null;
            for (int tries = 0; tries < _places.Count; tries++)
            {
                var p = _places[_cursor];
                _cursor = (_cursor + 3) % _places.Count;
                if (Taken(p)) continue;
                var go = new GameObject("Событие");
                go.transform.SetParent(transform, false);
                go.transform.position = p;
                var e = go.AddComponent<MapEvent>();
                e.Configure(this, j);
                _next = Mathf.Max(_next, j + 1);
                return e;
            }
            return null;
        }

        private static bool Taken(Vector3 p)
        {
            foreach (var e in MapEvent.All)
                if (e != null && !e.Done && CampFocus.GroundDistance(e.transform.position, p) < 1f) return true;
            return false;
        }

        /// <summary>Сейчас живых (не решённых) событий.</summary>
        public static int Alive()
        {
            int n = 0;
            foreach (var e in MapEvent.All) if (e != null && !e.Done) n++;
            return n;
        }
    }
}
