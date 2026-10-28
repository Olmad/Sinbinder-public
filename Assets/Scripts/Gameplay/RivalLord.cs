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

        /// <summary>
        /// Стиль (54-TUG, «Стили ИИ»): по греху самого Греховода-ИИ — грех от номера партии и места, без жребия.
        /// Гнев — натиск; уныние, гордыня — оборона; чревоугодие, зависть — некромант; похоть — переманщик;
        /// жадность — искатель. Сильный подмешивает второй стиль (<see cref="Prefers"/>).
        /// </summary>
        public enum Style { Onslaught, Defence, Necromancer, Lurer, Seeker }
        public SinType Sin { get; private set; }
        public Style Way { get; private set; }
        public Style Second { get; private set; }

        public static Style WayOf(SinType sin) => sin switch
        {
            SinType.Wrath => Style.Onslaught,
            SinType.Sloth or SinType.Pride => Style.Defence,
            SinType.Gluttony or SinType.Envy => Style.Necromancer,
            SinType.Lust => Style.Lurer,
            _ => Style.Seeker,
        };

        public bool Prefers(Style s) => Way == s || (Level == MapSession.Seat.AiStrong && Second == s);
        private bool _rumoured;

        private Warrior _body;
        private UnitMover _legs;
        private float _next;
        private bool _muted;

        // Экспедиция: кто в отряде, куда ушёл, когда (Expedition).
        private readonly HashSet<Warrior> _away = new();
        private MapEvent _out;
        private float _outSince;
        private const float ExpeditionRange = 110f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public static IReadOnlyList<RivalLord> All => Live;

        /// <summary>Все Греховоды-ИИ ждут (обучение, пока не дошло до урока о чужом троне).</summary>
        public static bool Hold { get; set; }

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
            Sin = (SinType)(Mathf.Abs(MapLoader.Match * 3 + side * 5) % 7);
            Way = WayOf(Sin);
            Second = (Style)(((int)Way + 1 + side) % 5);
            if (Second == Way) Second = (Style)(((int)Way + 1) % 5);

            // Замер «стиль против стиля» (MatchRun): «-tugStyle Lurer» — стиль задан, второй — следующий по кругу.
            var args = System.Environment.GetCommandLineArgs();
            for (int a = 0; a + 1 < args.Length; a++)
                if (args[a] == "-tugStyle" && System.Enum.TryParse<Style>(args[a + 1], out var forced))
                { Way = forced; Second = (Style)(((int)forced + 1) % 5); }
        }

        private float Beat => Level switch { MapSession.Seat.AiStrong => 3.5f, MapSession.Seat.AiMedium => 6f, _ => 10f };
        // На поле на двоих — тридцать жителей: девять верных одному было не собрать (длинная партия 28.10).
        private int Need => Level switch { MapSession.Seat.AiStrong => 6, MapSession.Seat.AiMedium => 5, _ => 3 }
                            - (Prefers(Style.Onslaught) ? 1 : 0) + (Way == Style.Defence ? 2 : 0);
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

            // Обучение держит ИИ, пока не дошло до своего урока (TugTutorial).
            if (TugMatch.Over || Hold || Time.time < _next) return;
            _next = Time.time + Beat;

            // Слух о стиле: жители говорят, чем занят Греховод, — игроку есть чем готовить ответ.
            if (!_rumoured && Time.time > 90f) { _rumoured = true; Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Rumour()); }

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

            // Нападать — когда верных хватает, или с четвёртой минуты — если набралась хотя бы половина:
            // ждать полного отряда, пока верные остывают от скуки (Idleness), — проиграть время.
            bool ready = followers.Count >= Need || (Time.time > LateStart && followers.Count >= (Need + 1) / 2);
            if (!Attacking && ready) Attacking = true;
            else if (Attacking && followers.Count < (Need + 1) / 2 - 1) Attacking = false;

            if (Attacking)
            {
                _away.Clear();
                Attack(match, followers);
                // Средний и сильный, пока отряд на приступе, зовут подкрепление (замер 29.10: приступ впятером
                // ломал дверь, но трона не добивал — люди гибли, а новых никто не звал).
                if (Level != MapSession.Seat.AiWeak) Recruit(followers);
                return;
            }

            // Экспедиция (MapEvent): ближнее живое событие — туда часть верных; решит на месте их лидер.
            Expedition(followers);

            // Строить — как игрок (BuildSite): по порядку костёр, ворота, вышки; на стройку — свободных верных,
            // на готовую вышку — унылого.
            var site = Build(followers);

            // Прочие верные без приказа — в караул перед своим склепом: при деле не остывают, и склеп не пуст.
            var gate = match != null && Side < match.Crypts.Count ? match.Crypts[Side] : null;
            int builders = 0;
            for (int i = 0; i < followers.Count; i++)
            {
                var f = followers[i];
                if (f.HasCommand || gate == null || TowerPost.Guards(f) || _away.Contains(f)) continue;
                if (site != null && builders < 3) { builders++; Order(f, CommandKind.Hold, site.transform.position + site.transform.right * (builders - 2) * 1.2f, null); continue; }
                var post = gate.position + gate.forward * 9f + gate.right * ((i % 5 - 2) * 1.6f) + gate.forward * (i / 5 * 1.6f);
                Order(f, CommandKind.Hold, post, null);
            }
            // Некромант сперва — тела и души рядом; нечего нести — зовёт живых, как все.
            if (Prefers(Style.Necromancer) && Necro(gate)) return;
            Recruit(followers);
        }

        // ---------------------------------------------------------- некромант

        private readonly Dictionary<Remains.Stuff, int> _stock = new();
        private readonly List<SoulData> _souls = new();
        private ShellType? _carry;
        private float _speed = -1f;
        private RelationshipSystem _relations;
        private const float BodyRange = 40f;

        /// <summary>
        /// Некромант — как игрок на алтаре (Remains, BindingDevice), только своим счётом: собрать душу павшего
        /// (SoulManager), взвалить тело (шаг ×0.6), донести до своего склепа — в материал; душа и материал есть —
        /// поднять: голем — по рецепту из экспедиции, зомби — плоть и кости, скелет — кости, призрак — одна душа.
        /// Поднятый ничей, «готов слушать» поднявшего. Банок — три, как у игрока, плюс добытые.
        /// Ложь — делать нечего, пусть зовёт живых.
        /// </summary>
        private bool Necro(Transform gate)
        {
            if (gate == null || _body == null) return false;
            var home = gate.position;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

            // Душа под ногами — в банку.
            int jars = Satchel.StartingJars + (Trophies.Has(Lord, Trophies.Kind.Jar) ? 1 : 0);
            var souls = SoulManager.Instance;
            if (souls != null && _souls.Count < jars && souls.TryHarvestSoul(transform.position) is { } fading)
            {
                souls.RemoveIndicator(fading);
                var kept = souls.TakeHarvested(souls.Harvested.Count - 1);
                if (kept.Soul != null) { _souls.Add(kept.Soul); SoulHarvester.Freeze(this); return true; }
            }

            // Несёт — домой; дошёл — тело в материал.
            if (_carry.HasValue)
            {
                if (CampFocus.GroundDistance(transform.position, home) > 8f) { _legs?.CommandMove(home); return true; }
                foreach (var s in Remains.From(_carry.Value)) { _stock.TryGetValue(s, out var n); _stock[s] = n + 1; }
                _carry = null;
                if (agent != null && _speed > 0f) agent.speed = _speed;
                return true;
            }

            // Дома, есть душа — поднять лучшее, на что хватит материала.
            if (_souls.Count > 0 && CampFocus.GroundDistance(transform.position, home) <= 12f)
            {
                var shell = Have(Remains.Stuff.Stone) && Trophies.Has(Lord, Trophies.Kind.GolemRecipe) ? ShellType.Golem
                          : Have(Remains.Stuff.Flesh) && Have(Remains.Stuff.Bones) ? ShellType.Zombie
                          : Have(Remains.Stuff.Bones) ? ShellType.Skeleton : ShellType.Ghost;
                var soul = _souls[0];
                _relations ??= new RelationshipSystem(AOS.MemoryProcessor.Instance);
                var risen = Raising.Rise(soul, shell, home + gate.forward * 3f, gate.rotation, _relations);
                if (risen != null)
                {
                    _souls.RemoveAt(0);
                    foreach (var s in Remains.Needs(shell)) _stock[s] = Mathf.Max(0, (_stock.TryGetValue(s, out var n) ? n : 0) - 1);
                    float trust = Hostility.KillerOf(soul.Name) == _body.DisplayName ? 40f : 62f;
                    Allegiance.Change(risen.Id, risen.Loyalty, Lord, trust - Allegiance.Of(risen.Id, risen.Loyalty, Lord));
                    Debug.Log($"[ИИ] {Lord}: поднял {risen.DisplayName} ({shell})");
                    return true;
                }
            }

            // Ближнее тело — взвалить; душа рядом с ним — по пути.
            HarvestableBody best = null;
            float near = BodyRange;
            foreach (var b in Object.FindObjectsByType<HarvestableBody>(FindObjectsSortMode.None))
            {
                if (b == null || b.IsCollected) continue;
                float d = CampFocus.GroundDistance(transform.position, b.transform.position);
                if (d < near) { near = d; best = b; }
            }
            if (best == null) return _souls.Count > 0 && CampFocus.GroundDistance(transform.position, home) > 12f && Go(home);
            if (near > 2.2f) { _legs?.CommandMove(best.transform.position); return true; }

            _carry = best.Shell;
            best.MarkCollected();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && w.IsDead && CampFocus.GroundDistance(w.transform.position, best.transform.position) < 3f)
                { Object.Destroy(w.gameObject); break; }
            Object.Destroy(best.gameObject);
            if (agent != null) { _speed = agent.speed; agent.speed *= Remains.Burden; }
            Debug.Log($"[ИИ] {Lord}: взвалил тело ({_carry})");
            return true;
        }

        private bool Have(Remains.Stuff s) => _stock.TryGetValue(s, out var n) && n > 0;

        private bool Go(Vector3 at) { _legs?.CommandMove(at); return true; }

        // ---------------------------------------------------------- экспедиции

        public string Rumour() => Way switch
        {
            Style.Onslaught => Loc.F("Жители шепчутся: {0} точит топоры — скоро пойдёт на соседей.", Lord),
            Style.Defence => Loc.F("Жители шепчутся: {0} строит стены и никуда не спешит.", Lord),
            Style.Necromancer => Loc.F("Жители шепчутся: {0} собирает мёртвых.", Lord),
            Style.Lurer => Loc.F("Жители шепчутся: {0} щедр — сманивает чужих людей.", Lord),
            _ => Loc.F("Жители шепчутся: люди {0} рыщут по дорогам за добычей.", Lord),
        };

        /// <summary>
        /// Отряд в экспедицию — как игрок, ПКМ у знака: верных не меньше трёх — уходит половина (двое–четверо),
        /// к ближнему живому событию, мирное — если хватит монет на первую развилку или вторая даром. Отряд
        /// вернулся (событие кончилось, две минуты прошло) — снова в караул.
        /// </summary>
        public void Expedition(List<Warrior> followers)
        {
            _away.RemoveWhere(w => w == null || w.IsDead);
            if (_out != null && (_out.Done || Time.time - _outSince > 120f)) { _out = null; _away.Clear(); }
            // Стиль: искатель ходит и вдвоём, дальше и большим отрядом; оборона — лишь с избытком людей;
            // натиск — пока на приступ не хватает.
            bool seeker = Prefers(Style.Seeker);
            if (_out != null || followers.Count < (seeker ? 2 : 3)) return;
            if (Way == Style.Defence && !seeker && followers.Count < Need) return;
            if (Way == Style.Onslaught && followers.Count >= Need) return;

            // Сколько можно увести, не оголяя вышки; на бой — хотя бы на одного больше живой стражи
            // (замер 29.10: уходили по двое против трёх-четырёх и гибли — верные таяли без единого приступа).
            int free = 0;
            foreach (var f in followers) if (f != null && !f.IsDead && !TowerPost.Guards(f)) free++;
            int cap = Mathf.Min(free, seeker ? 6 : 5);

            MapEvent best = null;
            int bestNeed = 2;
            float near = float.MaxValue, range = seeker ? 170f : ExpeditionRange;
            foreach (var e in MapEvent.All)
            {
                if (e == null || e.Done || e.Entry == null) continue;
                if (e.Parley && e.Entry.A.Cost > Favor.PurseOf(Lord) && e.Entry.B.Cost > Favor.PurseOf(Lord)) continue;
                int guards = 0;
                foreach (var g in e.Guards) if (g != null && !g.IsDead) guards++;
                int need = Mathf.Max(2, guards + 1);
                if (need > cap) continue;
                float d = CampFocus.GroundDistance(transform.position, e.transform.position);
                if (d > range) continue;
                // Некромант — к бою (тела), переманщик — туда, где есть люди.
                if (Prefers(Style.Necromancer) && !e.Parley) d *= 0.6f;
                if (Prefers(Style.Lurer) && (e.Entry.A.Recruits > 0 || e.Entry.B.Recruits > 0 || e.Entry.A.Deserters)) d *= 0.5f;
                if (d < near) { near = d; best = e; bestNeed = need; }
            }
            if (best == null) return;

            int take = seeker ? Mathf.Clamp(followers.Count * 2 / 3, 2, 5) : Mathf.Clamp(followers.Count / 2, 2, 4);
            take = Mathf.Min(Mathf.Max(take, bestNeed), cap);
            var squad = new List<Warrior>();
            foreach (var f in followers)
            {
                if (squad.Count >= take) break;
                if (f == null || f.IsDead || TowerPost.Guards(f)) continue;
                squad.Add(f);
            }
            if (squad.Count < 2) return;
            foreach (var f in squad) { Order(f, CommandKind.Move, best.transform.position, null); _away.Add(f); }
            best.Send(Lord, squad);
            _out = best;
            _outSince = Time.time;
            Debug.Log($"[ИИ] {Lord}: отряд {squad.Count} — в экспедицию «{best.Kind}»");
        }

        /// <summary>
        /// Стройка ИИ: заложенное и недостроенное — вернуть его (туда идут строители); нет — заложить следующее,
        /// если хватает монет и верных хотя бы двое. Готовая вышка без дозорного — унылого туда.
        /// </summary>
        private BuildSite Build(List<Warrior> followers)
        {
            BuildSite next = null;
            // Оборона строит всё, сперва ворота и вышки; натиск — только костёр (зов своих); прочие — по порядку.
            var order = Way == Style.Defence ? new[] { BuildSite.Kind.Gate, BuildSite.Kind.Tower, BuildSite.Kind.Fire }
                      : Way == Style.Onslaught && !Prefers(Style.Defence) ? new[] { BuildSite.Kind.Fire }
                      : new[] { BuildSite.Kind.Fire, BuildSite.Kind.Gate, BuildSite.Kind.Tower };
            foreach (var kind in order)
            {
                foreach (var s in BuildSite.All)
                {
                    if (s == null || s.Side != Side || s.What != kind) continue;
                    if (s.Laid && !s.Done) return s;
                    if (s.Done && s.What == BuildSite.Kind.Tower)
                    {
                        var post = s.GetComponent<TowerPost>();
                        if (post != null && post.Guard == null)
                            foreach (var f in followers)
                                if (f.Soul != null && f.Soul.Sin == SinType.Sloth && !f.HasCommand && !TowerPost.Guards(f))
                                { Order(f, CommandKind.Move, s.transform.position, null); break; }
                    }
                    if (!s.Laid && next == null) next = s;
                }
            }
            if (next == null || followers.Count < (Way == Style.Defence ? 1 : 2) || Favor.PurseOf(Lord) < next.Cost) return null;
            next.Lay(Lord);
            return next;
        }

        /// <summary>С какой секунды ИИ идёт и с половиной отряда: натиск — раньше, оборона — позже.</summary>
        private float LateStart => Way == Style.Onslaught ? 160f : Way == Style.Defence ? 360f : 240f;

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

        /// <summary>Сколько раз звал каждого и кому уже дарил: упрямых не уговаривает вечно (длинная партия 28.10).</summary>
        private readonly Dictionary<string, int> _asked = new();
        private readonly HashSet<string> _gifted = new();
        private int Stubborn => Prefers(Style.Lurer) ? 6 : 4;

        /// <summary>Стоит ли звать: не верен ещё, не упрям — меньше четырёх зовов или уже знаком (слушался).</summary>
        private bool Worth(Warrior w, List<Warrior> followers)
            => w != null && !w.IsDead && !followers.Contains(w) && w != _body
               && (!_asked.TryGetValue(w.Id, out var n) || n < Stubborn || Allegiance.Of(w.Id, w.Loyalty, Lord) > Allegiance.Of(w.Id, w.Loyalty, Lords.Player));

        private void Recruit(List<Warrior> followers)
        {
            // Ближний очаг, где ещё есть кого звать.
            Residents best = null;
            float bestD = float.MaxValue;
            foreach (var r in Object.FindObjectsByType<Residents>(FindObjectsSortMode.None))
            {
                bool someone = false;
                foreach (var w in r.Folk) if (Worth(w, followers)) { someone = true; break; }
                if (!someone) continue;
                float d = CampFocus.GroundDistance(transform.position, r.transform.position);
                if (d < bestD) { bestD = d; best = r; }
            }
            if (best == null) return;

            if (bestD > 10f) { _legs?.CommandMove(best.transform.position); return; }

            // У очага: по трое за раз — сперва знакомых (слушались — слушаются охотней), потом прочих.
            var queue = new List<Warrior>();
            foreach (var w in best.Folk)
                if (Worth(w, followers) && CampFocus.GroundDistance(transform.position, w.transform.position) <= Voice) queue.Add(w);
            queue.Sort((a, b) => Allegiance.Of(b.Id, b.Loyalty, Lord).CompareTo(Allegiance.Of(a.Id, a.Loyalty, Lord)));

            int asked = 0;
            foreach (var w in queue)
            {
                if (Gifts(w.Soul) && _gifted.Add(w.Id)) Favor.Give(w, Lord);
                _asked.TryGetValue(w.Id, out var n);
                _asked[w.Id] = n + 1;
                // Унылому — не идти, а стоять: «всё безразлично» он скажет любому зову, а пост ему по душе.
                if (w.Soul != null && w.Soul.Sin == SinType.Sloth) Order(w, CommandKind.Hold, w.transform.position, null);
                else Order(w, CommandKind.Move, transform.position + (w.transform.position - transform.position).normalized * 2.5f, null);
                if (++asked >= (Prefers(Style.Lurer) ? 5 : 3)) break;
            }
        }

        /// <summary>Кому дарит: слабый — жадным, средний — ещё завистливым и похотливым, сильный — всем, кроме гордых.</summary>
        private bool Gifts(SoulData soul)
        {
            if (soul == null) return false;
            if (soul.Sin == SinType.Greed) return true;
            if (Prefers(Style.Lurer)) return soul.Sin != SinType.Pride;   // переманщик дарит всем, кроме гордых
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

            // Сам — домой, к трону; средний и сильный — зовут подкрепление (Update).
            if (Level != MapSession.Seat.AiWeak) return;
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
