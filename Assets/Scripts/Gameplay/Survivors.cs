// Assets/Scripts/Gameplay/Survivors.cs
// Перевод: текст через Loc
using System;
using System.Collections.Generic;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>Кто пережил встречу с отрядом на вылазке. Идёт в сохранение.</summary>
    [Serializable]
    public sealed class SavedSurvivor
    {
        public string Name;
        /// <summary>Где ждёт: Id карты личной вылазки.</summary>
        public string Place;
        public int Sin;
        public bool Undead;
        /// <summary>Доля здоровья, с которой ушёл.</summary>
        public float Health = 1f;
        public int Meetings;
        /// <summary>Кого из отряда убил — через «, », в порядке смертей.</summary>
        public string Killed = "";
        public bool Fled;
        /// <summary>
        /// Почему он здесь: «bound» — покойник места, «caravan» — охрана обоза, «order» — дозор Ордена,
        /// «guide» — пришёл с дозором, «robbed» — охрана разграбленного обоза, «hunt» — ищет Греховода сам.
        /// </summary>
        public string Reason = "";
        /// <summary>
        /// Постоянная личность (Warrior.Id). С ней память AOS о нём — у наших и его о наших —
        /// снова находит адресата при следующей встрече, а не указывает на исчезнувший номер.
        /// </summary>
        public string Id = "";
        /// <summary>Чин (Trade): у стражей Ордена — как у охотников пролога; у прочих — нет (0).</summary>
        public int Trade;
        /// <summary>
        /// Где его видели наши в последний раз. Меню экспедиций называет врага только там, где
        /// его встретили (автор, 23.11: «знать о противниках экспедиции только после того, как
        /// узнали их»): ушедший с места или пришедший туда без нас — неизвестен, пока не встретят.
        /// </summary>
        public string SeenAt = "";
        /// <summary>Личные счёты: не «отряд вообще», а кто именно что сделал (автор, 23.11).</summary>
        public List<SavedGrudge> Grudges = new List<SavedGrudge>();
    }

    /// <summary>
    /// Счёт между выжившим и одним из наших. Ours=false — он помнит нашего Who, убившего
    /// его товарища Victim; Ours=true — наш Who видел, как он убил нашего Victim.
    /// </summary>
    [Serializable]
    public sealed class SavedGrudge
    {
        public string Who;
        public string Victim;
        public bool Ours;
    }

    /// <summary>
    /// Время мира в походах: каждый ушедший отряд и каждое возвращение Греховода —
    /// один шаг. Своих часов у мира нет (23.11), а «через пару минут» и «через три
    /// похода» должны давать разное: автор — «логично ли, что часовню разграбили,
    /// а она восстановилась через пару минут?». Идёт в сохранение.
    /// </summary>
    public static class Sorties
    {
        public static int Count { get; private set; }
        public static void Tick() => Count++;
        public static void Restore(int count) => Count = Math.Max(0, count);
        public static void Forget() => Count = 0;
    }

    /// <summary>С чем возвращаются на место, где уже были.</summary>
    public enum Return { First, SameNight, Later, Reoccupied }

    /// <summary>
    /// Выжившие помнят (автор, 23.11: «выжившие стражники сохранялись в памяти
    /// и могли оказаться в другой экспедиции. НО ЛОГИЧНО! С объяснением почему
    /// так… подражать Middle-earth: Shadow of War»). План 69 §7.
    ///
    /// Шаг 1 — тот же враг на том же месте и по названной причине:
    /// покойник привязан к месту, где его подняли, и кости у него не срастаются;
    /// охрана обоза ходит той же дорогой. Причину игра говорит словами при входе,
    /// а сам выживший — при встрече, по тому, что было. Переходы между вылазками
    /// (шаг 2) — только живым и только с поводом.
    ///
    /// Правило без Unity — проверяется стендом.
    /// </summary>
    public static class Survivors
    {
        private static readonly List<SavedSurvivor> _all = new List<SavedSurvivor>();

        public static IReadOnlyList<SavedSurvivor> All => _all;

        // Имена — мужские: узнавание и объяснения пока только в мужском роде,
        // а женщина, о которой сказано «он», — та же ошибка, что «ему» у Марги.
        private static readonly string[] _dead =
        {
            Loc.N("Мирон Звонарь"), Loc.N("Ефрем Сухорукий"), Loc.N("Антип Свечник"),
            Loc.N("Лука Безбородый"), Loc.N("Прохор Немытый"), Loc.N("Устин Вдовец"),
            Loc.N("Фома Колодезник"), Loc.N("Савва Кривошей"),
        };
        private static readonly string[] _living =
        {
            Loc.N("Бажен Рыжий"), Loc.N("Тихон Соляной"), Loc.N("Ерёма Щербатый"),
            Loc.N("Радим Копьё"), Loc.N("Неждан Хмурый"), Loc.N("Гордей Плечистый"),
            Loc.N("Малюта Быстрый"), Loc.N("Любим Кистень"),
        };

        // Дозор Ордена — охотники Ордена, как в прологе: германское имя и чин в душе — на экране
        // «Готфрид Инквизитор» (решение автора 16.10: у Ордена своя культура, чужая отряду).
        // «Брат Евсей» (23.11) был ошибкой: русское монашеское имя, да ещё рядом с нашим Братом Хальдом.
        // Имена — из списка охотников, которых пролог не выводит (там Ансельм, Бертрам, Ульрих и вторая
        // волна с Эберхарда): поднятый «Бертрам Следопыт» и страж «Бертрам Охотник» читались бы одним.
        private static readonly string[] _order =
        {
            Loc.N("Готфрид"), Loc.N("Кунрад"), Loc.N("Ортвин"), Loc.N("Вольфрам"),
            Loc.N("Лотар"), Loc.N("Фолькмар"), Loc.N("Зигфрид"),
        };

        // Чины дозора: первым — Инквизитор, «командир охотников и ловчих» (00-GDD / 45-REVIEW).
        private static readonly Trade[] _ranks = { Trade.Inquisitor, Trade.OrderHunter, Trade.Tracker, Trade.Catcher };

        /// <summary>Чин стража Ордена на этом месте дозора.</summary>
        public static Trade OrderRank(int index) => _ranks[(index < 0 ? 0 : index) % _ranks.Length];

        /// <summary>Как его называть в журнале: с чином, если он есть, — как на экране.</summary>
        public static string Shown(SavedSurvivor s) => Loc.Name(Naming.Full(s.Name, (Trade)s.Trade, null, null));

        /// <summary>Имя по тому, кто он: «bound», «caravan» или «order».</summary>
        public static string NameFor(string reason, ICollection<string> taken)
            => reason == "order" ? Pick(_order, taken) : NameFor(reason == "bound", taken);

        private static string Pick(string[] pool, ICollection<string> taken)
        {
            foreach (var n in pool)
                if ((taken == null || !taken.Contains(n)) && !_all.Exists(s => s.Name == n)) return n;
            return pool[(taken?.Count ?? 0) % pool.Length];
        }

        /// <summary>Кто ждёт на этом месте — в порядке, в каком их запомнили.</summary>
        public static List<SavedSurvivor> At(string place)
        {
            var list = new List<SavedSurvivor>();
            foreach (var s in _all) if (s.Place == place) list.Add(s);
            return list;
        }

        /// <summary>
        /// Имя новому стражу. Не повторяет ни живых в памяти, ни уже взятых
        /// на этой карте: два Мирона Звонаря прочитались бы как ошибка сборки.
        /// Порядок неизменен — тот же поход даёт те же имена.
        /// </summary>
        public static string NameFor(bool undead, ICollection<string> taken)
        {
            var pool = undead ? _dead : _living;
            foreach (var n in pool)
            {
                if (taken != null && taken.Contains(n)) continue;
                if (_all.Exists(s => s.Name == n)) continue;
                return n;
            }
            return pool[(taken?.Count ?? 0) % pool.Length];
        }

        /// <summary>
        /// Итог встречи на месте: живые записаны (кто уже был — дополнен),
        /// павших больше нет. Зовётся при выходе из вылазки.
        /// </summary>
        public static void Settle(string place, IList<SavedSurvivor> alive)
        {
            _all.RemoveAll(s => s.Place == place);
            if (alive == null) return;
            foreach (var s in alive)
                if (s != null && !string.IsNullOrEmpty(s.Name)) { s.Place = place; _all.Add(s); }
        }

        // ── Переходы (план 69 §7 п.2): только живые и только с поводом ──

        /// <summary>Место бродяг: охранник разграбленного обоза, которому больше некого охранять.</summary>
        public const string Road = "road";

        /// <summary>Обоз разграблен — выжившая охрана без места: уходит на дорогу.</summary>
        public static void Wander(IList<SavedSurvivor> robbed)
        {
            if (robbed == null) return;
            foreach (var s in robbed)
                if (s != null && !s.Undead && !string.IsNullOrEmpty(s.Name))
                { s.Place = Road; s.Reason = "robbed"; _all.Add(s); }
        }

        /// <summary>
        /// Дозор Ордена берёт с собой того, кто знает Греховода в лицо: охранника
        /// разграбленного обоза. Он переходит на новое место; нет такого — null.
        /// </summary>
        public static SavedSurvivor Guide(string place)
        {
            var s = _all.Find(x => x.Place == Road && x.Reason == "robbed");
            if (s == null) return null;
            s.Place = place; s.Reason = "guide";
            return s;
        }

        /// <summary>
        /// Бежавший живой возвращается не один — если прошло время (не той же ночью).
        /// Покойник места не покидает и никого не приводит. Привёл — долг снят.
        /// </summary>
        public static List<SavedSurvivor> BringFriends(IList<SavedSurvivor> at, Return back)
        {
            var list = new List<SavedSurvivor>();
            if (back != Return.Later || at == null) return list;
            foreach (var s in at) if (s != null && s.Fled && !s.Undead) { list.Add(s); s.Fled = false; }
            return list;
        }

        /// <summary>Место охотника: живой, убивший нашего, ищет Греховода сам — пока не встретит.</summary>
        public const string Hunt = "hunt";

        /// <summary>
        /// Убил воина — ищет Греховода сам (план 69 §7 п.2): живой, на чьём счету наш, уходит
        /// со своего места. Зовётся, когда весть доходит до склепа, и только о тех местах,
        /// откуда её принесли: Греховод — о вылазке, откуда вернулся; отряд — о месте своего боя.
        /// Покойник места не покидает никогда. Вернёт ушедших и откуда.
        /// </summary>
        public static List<(SavedSurvivor who, string left)> GoHunting(ICollection<string> from)
        {
            var list = new List<(SavedSurvivor, string)>();
            if (from == null) return list;
            foreach (var s in _all)
                if (!s.Undead && !string.IsNullOrEmpty(s.Killed) && s.Place != Hunt && from.Contains(s.Place))
                { list.Add((s, s.Place)); s.Place = Hunt; s.Reason = Hunt; }
            return list;
        }

        /// <summary>
        /// Греховод вошёл в вылазку: кто его ищет — здесь. Слух о нём уже был в склепе
        /// (<see cref="Rumour"/>), поэтому приход не случаен и назван.
        /// </summary>
        public static List<SavedSurvivor> JoinHunt(string place)
        {
            var list = new List<SavedSurvivor>();
            if (string.IsNullOrEmpty(place)) return list;
            foreach (var s in _all) if (s.Place == Hunt) { s.Place = place; list.Add(s); }
            return list;
        }

        /// <summary>Слух в склепе — до похода, где охотник появится.</summary>
        public static string Rumour(SavedSurvivor s)
            => Loc.F("Слух: {0} ищет Греховода сам — от его руки погибли ваши: {1}. Куда пойдёте вы, туда придёт и он.",
                     Shown(s), s.Killed);

        // ── Что известно о месте (меню экспедиций) — только встреченное ──

        /// <summary>Видели ли его наши на этом месте. Старые записи (без отметки) — там, где стоят.</summary>
        public static bool SeenHere(SavedSurvivor s, string place)
            => s != null && s.Place == place && (s.SeenAt == place
               || string.IsNullOrEmpty(s.SeenAt) && s.Meetings > 0 && s.Reason != Hunt && s.Reason != "guide");

        /// <summary>Кого наши видели здесь — в порядке, в каком запомнили.</summary>
        public static List<SavedSurvivor> KnownAt(string place)
        {
            var list = new List<SavedSurvivor>();
            foreach (var s in _all) if (SeenHere(s, place)) list.Add(s);
            return list;
        }

        /// <summary>Строка меню о встреченном: кто он и что на нём. Без чисел — их игрок не видит.</summary>
        public static string Known(SavedSurvivor s)
        {
            string who = Shown(s);
            if (!string.IsNullOrEmpty(s.Killed)) return Loc.F("{0} — от его руки пали: {1}.", who, s.Killed);
            if (s.Reason == "guide") return Loc.F("{0} — охранял обоз, теперь служит Ордену.", who);
            if (s.Fled) return Loc.F("{0} — бежал от отряда.", who);
            if (s.Undead && s.Health < 0.999f) return Loc.F("{0} — кости с прошлой встречи не срослись.", who);
            if (s.Reason == "order") return Loc.F("{0} — из дозора Ордена.", who);
            return Loc.F("{0} — видел Греховода и остался жив.", who);
        }

        /// <summary>Кто ищет Греховода сам — слух уже был в склепе, значит, известно.</summary>
        public static List<SavedSurvivor> Hunters()
        {
            var list = new List<SavedSurvivor>();
            foreach (var s in _all) if (s.Place == Hunt) list.Add(s);
            return list;
        }

        /// <summary>Записать счёт один раз: та же пара и та же смерть дважды не считаются.</summary>
        public static void Grudge(SavedSurvivor s, string who, string victim, bool ours)
        {
            // victim пуст — счёт за рану: «ранил», не «убил».
            if (s == null || string.IsNullOrEmpty(who)) return;
            victim ??= "";
            if (s.Grudges == null) s.Grudges = new List<SavedGrudge>();
            if (s.Grudges.Exists(g => g.Who == who && g.Victim == victim && g.Ours == ours)) return;
            s.Grudges.Add(new SavedGrudge { Who = who, Victim = victim, Ours = ours });
        }

        /// <summary>
        /// Оклик при встрече — от того, кто помнит, тому, кого помнит. Имя павшего —
        /// в именительном и без рода: «Вейн Тихий лежит из-за тебя» верно и для Марги.
        /// </summary>
        public static string CallOut(string target, string victim)
            => Loc.F("{0}! {1} лежит из-за тебя.", Loc.Name(target), Loc.Name(victim));

        /// <summary>
        /// Бой отряда без Греховода (MissionBoard) с выжившими этого места — в память, по
        /// записи AOS: кто кого убил и ранил. Павшие стражи забыты, уцелевшие запомнены
        /// (встреч +1). Отряд мал, бой один — свидетели все, кто в нём был. Вернёт, сколько
        /// выживших пало.
        /// </summary>
        public static int AfterAutoBattle(string place, IList<string> foes, IList<string> party, AOS.BattleRecord record,
                                          IDictionary<string, float> health = null)
        {
            if (string.IsNullOrEmpty(place) || foes == null || party == null || record == null || record.Events == null) return 0;
            var here = At(place);
            var met = new Dictionary<string, SavedSurvivor>();
            foreach (var s in here) if (foes.Contains(s.Name)) met[s.Name] = s;
            if (met.Count == 0) return 0;
            var fallen = new HashSet<string>();
            foreach (var ev in record.Events)
            {
                if (ev == null || string.IsNullOrEmpty(ev.ActorName) || string.IsNullOrEmpty(ev.TargetName)) continue;
                bool foeActs = met.TryGetValue(ev.ActorName, out var actor) && party.Contains(ev.TargetName);
                bool oursActs = party.Contains(ev.ActorName) && met.TryGetValue(ev.TargetName, out _);
                if (ev.TargetKilled && foeActs)
                {
                    actor.Killed = string.IsNullOrEmpty(actor.Killed) ? Loc.Name(ev.TargetName) : actor.Killed + ", " + Loc.Name(ev.TargetName);
                    foreach (var p in party) Grudge(actor, p, ev.TargetName, true);   // и сам павший
                }
                else if (ev.TargetKilled && oursActs)
                {
                    fallen.Add(ev.TargetName);
                    foreach (var kv in met)
                        if (kv.Key != ev.TargetName && !fallen.Contains(kv.Key)) Grudge(kv.Value, ev.ActorName, ev.TargetName, false);
                }
                else if (ev.DamageDealt > 0f && foeActs) Grudge(actor, ev.TargetName, "", true);
                else if (ev.DamageDealt > 0f && oursActs) Grudge(met[ev.TargetName], ev.ActorName, "", false);
            }
            var alive = new List<SavedSurvivor>();
            foreach (var s in here)
            {
                if (fallen.Contains(s.Name)) continue;
                if (met.ContainsKey(s.Name))
                {
                    s.Meetings++;
                    s.SeenAt = place;   // отряд видел его здесь — и рассказал
                    if (health != null && health.TryGetValue(s.Name, out float h)) s.Health = Math.Max(0f, Math.Min(1f, h));
                }
                alive.Add(s);
            }
            Settle(place, alive);
            return fallen.Count;
        }

        /// <summary>Поднятый узнал того, кто его убил.</summary>
        public static string Murderer(string target)
            => Loc.F("{0}! Ты убил меня. Я вернулся.", Loc.Name(target));

        /// <summary>Оклик раненого тому, кто его ранил.</summary>
        public static string Scar(string target)
            => Loc.F("{0}! Я помню твой удар.", Loc.Name(target));

        /// <summary>Строка журнала: бежавший вернулся с подмогой.</summary>
        public static string Friends(SavedSurvivor s)
            => Loc.F("{0} бежал от вас в прошлый раз — и вернулся не один.", Shown(s));

        /// <summary>Через сколько походов разграбленную часовню займут снова.</summary>
        public const int ReoccupyAfter = 3;

        /// <summary>
        /// Место не обновляется само. Вернулся, пока в мире не прошло ни одного похода, —
        /// та же ночь: раны у всех те же. Зачищенное пустует; разграбленную часовню
        /// через <see cref="ReoccupyAfter"/> похода займёт дозор Ордена — слух дошёл.
        /// Зачищенное, но не разграбленное — пустует: беречь там нечего.
        /// </summary>
        public static Return OnReturn(int leftAt, int clearedAt, bool plundered, int enemiesLeft, int now)
        {
            if (leftAt < 0) return Return.First;
            if (enemiesLeft == 0 && plundered && clearedAt >= 0 && now - clearedAt >= ReoccupyAfter)
                return Return.Reoccupied;
            return now <= leftAt ? Return.SameNight : Return.Later;
        }

        /// <summary>Строка журнала: вернулись той же ночью.</summary>
        public static string SameNight() => Loc.T("Вы вернулись той же ночью: стража не успела ни уйти, ни залечить раны.");

        /// <summary>Строка журнала: часовню заняли снова.</summary>
        public static string Reoccupied() => Loc.T("Слух о разграбленной часовне дошёл до Ордена: у алтаря теперь их дозор.");

        /// <summary>Почему он здесь — строка журнала при входе.</summary>
        public static string Why(SavedSurvivor s, float maxHp)
        {
            if (s.Reason == Hunt)
                return Loc.F("{0} пришёл не за добычей, а за вами: слух в склепе не врал.", Shown(s))
                       + " " + Deed(s, maxHp);
            if (s.Reason == "guide")
                return Loc.F("{0} пришёл с дозором Ордена: обоз, который он охранял, разграбили, и теперь он служит Ордену — он знает вас в лицо.", Shown(s));
            if (s.Reason == "order")
                return Loc.F("{0} — из дозора Ордена: часовню стерегут с тех пор, как её разграбили.", Shown(s))
                       + " " + Deed(s, maxHp);
            string why = s.Undead
                ? Loc.F("{0} ждёт здесь: покойник не может уйти от места, где его подняли.", Shown(s))
                : Loc.F("{0} снова в охране: обоз ходит той же дорогой и с теми же людьми.", Shown(s));
            return why + " " + Deed(s, maxHp);
        }

        private static string Deed(SavedSurvivor s, float maxHp)
        {
            if (!string.IsNullOrEmpty(s.Killed))
                // Имя — после двоеточия, в именительном: «убил Вейна» потребовало бы
                // склонять имена, а «пал/пала» — знать род павшего.
                return Loc.F("В прошлый раз от его руки погибли ваши: {0}.", s.Killed);
            if (s.Fled) return Loc.T("В прошлый раз он бежал от отряда.");
            if (s.Undead && s.Health < 0.999f)
                return Loc.F("Кости с прошлой встречи не срослись: {0:F0} из {1:F0}.", s.Health * maxHp, maxHp);
            return Loc.T("В прошлый раз он видел Греховода и остался жив.");
        }

        /// <summary>Что он говорит, узнав Греховода.</summary>
        public static string Greeting(SavedSurvivor s)
        {
            if (!string.IsNullOrEmpty(s.Killed))
            {
                string first = s.Killed.Split(new[] { ", " }, StringSplitOptions.None)[0];
                return s.Undead ? Loc.F("Опять ты. {0} так и лежит у часовни.", first)
                                : Loc.F("Помню тебя. Твоих я уже считаю: {0}.", first);
            }
            if (s.Reason == "guide") return Loc.T("Обоз ты разграбил. Теперь я служу Ордену — и тебя узнаю где угодно.");
            if (s.Fled) return Loc.T("Ты. В тот раз я ушёл. Больше не уйду.");
            if (s.Undead && s.Health < 0.999f) return Loc.T("Кости не срастаются, некромант. Я считаю каждую.");
            return Loc.T("Я тебя видел. Ты ушёл — я остался.");
        }

        public static List<SavedSurvivor> Snapshot()
        {
            var list = new List<SavedSurvivor>();
            foreach (var s in _all)
                list.Add(new SavedSurvivor { Name = s.Name, Place = s.Place, Sin = s.Sin, Undead = s.Undead,
                    Health = s.Health, Meetings = s.Meetings, Killed = s.Killed ?? "", Fled = s.Fled, Reason = s.Reason ?? "",
                    Id = s.Id ?? "", Trade = s.Trade, SeenAt = s.SeenAt ?? "",
                    Grudges = s.Grudges == null ? new List<SavedGrudge>()
                        : s.Grudges.ConvertAll(g => new SavedGrudge { Who = g.Who, Victim = g.Victim, Ours = g.Ours }) });
            return list;
        }

        public static void Restore(List<SavedSurvivor> saved)
        {
            _all.Clear();
            if (saved == null) return;
            foreach (var s in saved)
                if (s != null && !string.IsNullOrEmpty(s.Name) && !string.IsNullOrEmpty(s.Place)) _all.Add(s);
        }

        /// <summary>Новая игра — никто никого не помнит, и время с нуля.</summary>
        public static void Forget() { _all.Clear(); Sorties.Forget(); }
    }
}
