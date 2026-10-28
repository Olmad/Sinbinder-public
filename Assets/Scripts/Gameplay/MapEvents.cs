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
    /// новый расклад, в сети — одинаковый у всех; подряд роды не повторяются, пока не кончатся все. Событие
    /// разрешилось — через <see cref="Again"/> с на месте встаёт следующее.
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
        public const float Again = 180f;

        private int _spot, _spots = 1, _gen, _j;
        private ExpeditionBook.Entry _entry;
        private readonly List<Warrior> _guards = new();
        private readonly List<Warrior> _camp = new();
        private string _owner;           // дезертиры — чьи были
        private bool _done;
        private float _againAt = -1f;
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

        /// <summary>Род на карте не задаётся — его даёт перестановка; место помнит свой номер.</summary>
        public void Configure(string kind, int index) => _spot = index;

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
        void Start()
        {
            _spots = Mathf.Max(1, MapLoader.Current != null ? MapLoader.Current.Events.Count : 1);
            _home = gameObject.AddComponent<Residents>();
            _home.Configure("village", 6f, 0, 0);
            Open();
        }

        /// <summary>Встать следующему событию на этом месте.</summary>
        private void Open()
        {
            _j = _spot + _gen * _spots;
            _entry = ExpeditionBook.All[Pick(_j)];
            _done = false;
            _againAt = -1f;
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
            Debug.Log($"[СОБЫТИЯ] место {_spot}, событие {_j}: {_entry.Id} — стражи {_guards.Count}, у огня {_camp.Count}");
        }

        void Update()
        {
            if (_entry == null) return;
            if (_done)
            {
                if (_againAt > 0f && Time.time >= _againAt) Next();
                return;
            }
            if (TugMatch.Over) return;

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

        /// <summary>Сразу встать следующему (прогон проверяет, что место не пустеет).</summary>
        public void Next()
        {
            if (_things != null) Destroy(_things);
            _gen++;
            Open();
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
            _againAt = Time.time + Again;
            if (_mark != null) Destroy(_mark);
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
}
