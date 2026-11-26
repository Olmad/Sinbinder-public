// Assets/Scripts/AOS Engine/Traits.cs
// Перевод: имена, «что делает», «почему» и реплики — через Loc
using System;
using System.Collections.Generic;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    /// <summary>Что о душе знает выбор особенностей.</summary>
    public struct TraitSoul
    {
        public SinType Sin, Second;
        /// <summary>Главное в душе — добродетель (спектр ниже нуля), а не порок.</summary>
        public bool Virtue;
        public MoralType Moral;
        public Trade Trade;
        public ShellType Shell;

        public static TraitSoul Of(SoulData soul, ShellType shell)
        {
            var t = new TraitSoul { Shell = shell };
            if (soul == null) return t;
            t.Sin = soul.Sin;
            t.Virtue = soul.Get(t.Sin) < 0f;
            t.Moral = soul.Moral;
            t.Trade = soul.Trade;
            float best = float.MinValue;
            for (int i = 0; i < SoulData.SpectrumCount; i++)
            {
                if (i == (int)t.Sin) continue;
                float v = Math.Abs(soul.Get((SinType)i));
                if (v > best) { best = v; t.Second = (SinType)i; }
            }
            return t;
        }

        public static TraitSoul Of(Sketch k) => new TraitSoul
            { Sin = k.Sin, Second = k.Second, Virtue = false, Moral = k.Moral, Trade = k.Trade, Shell = k.Shell };

        public bool Is(SinType sin) => Sin == sin && !Virtue;
        public bool Has(SinType sin) => Is(sin) || (Second == sin && !Virtue);
        public bool Good => Moral == MoralType.Pious || Virtue;
        public bool Bad => Moral == MoralType.Vicious;
    }

    /// <summary>Одна прибавка: когда, к какому действию, сколько.</summary>
    public readonly struct TraitVote
    {
        public readonly Func<DecisionContext, bool> When;
        public readonly ActionType Action;
        public readonly float Bonus;
        public TraitVote(Func<DecisionContext, bool> when, ActionType action, float bonus)
        { When = when; Action = action; Bonus = bonus; }
    }

    /// <summary>
    /// Особенность — правило, которое ломает общее правило в бою (53-GENERATOR §10, автор 27.10),
    /// с рычагом движка и крючком истории. Хранится в душе тем же местом, что сюжетный перк
    /// (<see cref="MemorySeed.NarrativePerks"/>), по имени: так переживает и сохранение, и смерть.
    /// </summary>
    public sealed class Trait
    {
        /// <summary>Имя — оно же ключ в сохранении. Мужская форма.</summary>
        public string Name;
        /// <summary>Женская форма имени; null — та же.</summary>
        public string NameShe;
        /// <summary>Что делает — ровно то, что делает рычаг, без рода (род наводит Grammar.For на местоимениях).</summary>
        public string What;
        /// <summary>Почему он такой — строка истории.</summary>
        public string Why;
        /// <summary>Реплика, когда особенность решила отказ (RefusalPresenter): от первого лица, без рода.</summary>
        public string Says;
        /// <summary>Совет игроку, когда особенность решила отказ (AOSDecisionAdvice): какой рычаг есть. Null — общий.</summary>
        public string Advice;
        public Func<TraitSoul, bool> Fits;
        public string[] NotWith = Array.Empty<string>();
        public TraitVote[] Votes;
    }

    /// <summary>
    /// Библиотека особенностей и их раздача. Автор, 26.11: генератор выдавал «пустых» воинов — с одним
    /// грехом, — «что абсолютно противоречит AOS и идее игры». Примета давала прозвище и строки, но
    /// решения не меняла, а когда прозвища кончались («Перетягивание»: жителей на карте больше, чем
    /// примет), не было и строк. Теперь у каждого — одна-две особенности, и каждая меняет решение
    /// (проверка — Tools/bench, «ОСОБЕННОСТИ»). Повторяться между людьми им можно: это правило, а не имя.
    ///
    /// Сила — как ещё один голос (±20…±80, не больше потолка голоса MaxVoice): особенность сдвигает
    /// решение, но не отменяет душу. Подобрана по стенду (26.11): в своём положении, где она вообще может
    /// что-то решить, каждая меняет поступок в 29–71 % случаев — заметно, но остальное решает душа.
    ///
    /// Объяснение честное: если отказ держится на особенности, «от противного» её и называет
    /// (BehaviourResolver.Weigh → <see cref="PerkResolver.DecisiveTrait"/>), а не грех, который в тот раз не решал.
    ///
    /// Перки из старого списка ста, у которых нашёлся рычаг, вошли сюда
    /// (разбор — D:\Claude\AOS\перки-разбор-26.11.md). ЧЕРНОВИК ЧИСЕЛ И СТРОК — на чтение автору.
    /// </summary>
    public static class Traits
    {
        /// <summary>Сколько секунд «союзника только что ранили» (<see cref="DecisionContext.AllyDamagedRecently"/>).</summary>
        public const float AllyHurtWindow = 3f;

        /// <summary>Метка места (LocationManager.HasTag); в стенде — пусто.</summary>
        public static Func<string, bool> PlaceHas;

        private static bool Hurt(DecisionContext c, float share) => c.MaxHP > 0f && c.CurrentHP < c.MaxHP * share;
        private static bool Fight(DecisionContext c) => c.NearbyEnemies > 0;
        private static bool Holding(DecisionContext c) =>
            c.HasCommand && (c.CommandType == "Hold" || c.CommandType == "Defend");
        private static bool Crypt(DecisionContext c) => PlaceHas != null && PlaceHas("Crypt") && c.NearbyEnemies > 0;

        public static readonly Trait[] All =
        {
            new() { Name = Loc.N("Кровник Ордена"), NameShe = Loc.N("Кровница Ордена"),
                    What = Loc.N("Увидит людей Ордена — бьёт их, даже когда велено отходить."),
                    Why = Loc.N("Орден сжёг его дом. Он помнит, кто держал факел."),
                    Says = Loc.N("Не уйду, пока они живы!"),
                    Advice = Loc.N("Уведите от людей Ордена — при них не отступит."),
                    Fits = s => (s.Is(SinType.Wrath) || s.Is(SinType.Pride)) && !Trades.OfOrder(s.Trade),
                    NotWith = new[] { "Боится Ордена" },
                    Votes = new[] { new TraitVote(c => c.EnemyIsHunter, ActionType.Attack, 80f),
                                    new TraitVote(c => c.EnemyIsHunter && c.CommandLeavesFight, ActionType.ObeyCommand, -60f) } },

            new() { Name = Loc.N("Боится Ордена"),
                    What = Loc.N("Люди Ордена пугают его и после смерти: при них думает, как уйти."),
                    Why = Loc.N("Видел костры Ордена — и тех, кого на них жгли."),
                    Says = Loc.N("Это Орден… Туда не пойду."),
                    Advice = Loc.N("Уберите людей Ордена с пути или пошлите другого."),
                    Fits = s => (s.Has(SinType.Sloth) || s.Has(SinType.Gluttony)) && !Trades.OfOrder(s.Trade),
                    NotWith = new[] { "Кровник Ордена" },
                    Votes = new[] { new TraitVote(c => c.EnemyIsHunter, ActionType.Flee, 45f) } },

            new() { Name = Loc.N("Только в спину"),
                    What = Loc.N("Бьёт, когда враг подставил спину; в лоб идёт неохотно."),
                    Why = Loc.N("В лоб ходят дураки — так он говорил при жизни."),
                    Says = Loc.N("В лоб не полезу."),
                    Advice = Loc.N("Зайдите врагу в спину или пошлите другого."),
                    Fits = s => (s.Has(SinType.Envy) || s.Is(SinType.Greed)) && !s.Good,
                    NotWith = new[] { "Не бьёт в спину" },
                    Votes = new[] { new TraitVote(c => c.TargetBackExposed, ActionType.Attack, 60f),
                                    new TraitVote(c => Fight(c) && !c.TargetBackExposed, ActionType.Attack, -30f) } },

            new() { Name = Loc.N("Не бьёт в спину"),
                    What = Loc.N("Врага со спины не тронет — ждёт, пока тот повернётся."),
                    Why = Loc.N("Так не бьют. Так его учили, и он это помнит."),
                    Says = Loc.N("Со спины не бью."),
                    Advice = Loc.N("Дождитесь, пока враг повернётся, или пошлите другого."),
                    Fits = s => s.Good || (s.Is(SinType.Pride) && !s.Bad),
                    NotWith = new[] { "Только в спину" },
                    Votes = new[] { new TraitVote(c => c.TargetBackExposed, ActionType.Attack, -40f) } },

            new() { Name = Loc.N("Глуховат"), NameShe = Loc.N("Глуховата"),
                    What = Loc.N("Тихий приказ издали не слышит; вблизи — слышит всегда."),
                    Why = Loc.N("Оглушён обвалом ещё при жизни."),
                    Says = Loc.N("А? Что? Не слышу!"),
                    Advice = Loc.N("Подойдите ближе — вблизи слышно всегда."),
                    Fits = s => s.Shell == ShellType.Skeleton || s.Trade == Trade.Quarryman || s.Trade == Trade.Carter,
                    NotWith = new[] { "Не терпит окрика" },
                    Votes = new[] { new TraitVote(c => c.HasCommand && c.CommandVolume < 0.6f, ActionType.ObeyCommand, -40f) } },

            // До 26.11 (вечер) — «приказ в упор, во весь голос»: но громкость 1 — вся ближняя половина
            // зоны голоса (Voice.Near), а при выключенном голосе — везде; мирный приказ не исполнялся
            // никогда, и рычага не было. Теперь окрик — тот же приказ снова через пару секунд (щелчки подряд).
            new() { Name = Loc.N("Не терпит окрика"),
                    What = Loc.N("Когда приказ твердят ему раз за разом, злится — назло не сделает."),
                    Why = Loc.N("Был господином и не привык, чтобы ему повторяли."),
                    Says = Loc.N("С первого раза понятно."),
                    Advice = Loc.N("Не торопите — выждите пару секунд и повторите."),
                    Fits = s => s.Has(SinType.Pride) && !s.Good,
                    NotWith = new[] { "Глуховат", "Клятва", "Свободный" },
                    Votes = new[] { new TraitVote(c => c.HasCommand && c.CommandNagged, ActionType.ObeyCommand, -25f) } },

            new() { Name = Loc.N("Клятва"),
                    What = Loc.N("Связан клятвой служить: приказ для него весит больше, чем для других."),
                    Why = Loc.N("Клялся ещё живым и держит слово и после смерти."),
                    Says = Loc.N("Слово дано."),
                    Fits = s => s.Good && !s.Is(SinType.Pride),
                    NotWith = new[] { "Свободный", "Не терпит окрика" },
                    Votes = new[] { new TraitVote(c => c.HasCommand, ActionType.ObeyCommand, 60f) } },

            // До 26.11 (вечер) — приказ −20 всегда: в спокойном положении голоса дают по 2–3 очка, и
            // мирный приказ не исполнялся никогда (сон: брат на печать не встал). «Вполуха» — с первого раза.
            new() { Name = Loc.N("Свободный"), NameShe = Loc.N("Свободная"),
                    What = Loc.N("Неволю помнит: с первого раза приказ пропускает мимо ушей."),
                    Why = Loc.N("Свобода ему дороже жизни. Жизни уже нет — свобода осталась."),
                    Says = Loc.N("Я больше ничей."),
                    Advice = Loc.N("Повторите приказ — со второго раза дойдёт."),
                    Fits = s => (s.Has(SinType.Envy) || s.Has(SinType.Pride)) && !s.Good,
                    NotWith = new[] { "Клятва", "Не терпит окрика" },
                    Votes = new[] { new TraitVote(c => c.HasCommand && !c.CommandRepeated, ActionType.ObeyCommand, -30f) } },

            new() { Name = Loc.N("Не чувствует боли"),
                    What = Loc.N("Раны не гонят его из боя — он не знает, что умирает."),
                    Why = Loc.N("Мёртвое тело не болит."),
                    Says = Loc.N("Мне не больно."),
                    Fits = s => s.Shell == ShellType.Zombie,
                    NotWith = new[] { "Бережёт шкуру" },
                    Votes = new[] { new TraitVote(c => Hurt(c, 0.4f), ActionType.Flee, -80f) } },

            new() { Name = Loc.N("Бережёт шкуру"),
                    What = Loc.N("При первой ране думает, как уйти."),
                    Why = Loc.N("Тело — всё, что у него было, и он его бережёт."),
                    Says = Loc.N("Шкура у меня одна."),
                    Fits = s => s.Has(SinType.Gluttony) || s.Is(SinType.Sloth),
                    NotWith = new[] { "Не чувствует боли", "Последний стоит" },
                    Votes = new[] { new TraitVote(c => Hurt(c, 0.5f) && Fight(c), ActionType.Flee, 30f) } },

            new() { Name = Loc.N("Последний стоит"), NameShe = Loc.N("Последняя стоит"),
                    What = Loc.N("Один против всех — не бежит, а дерётся."),
                    Why = Loc.N("Однажды уже остался один из всего отряда — и не простил себе, что выжил."),
                    Says = Loc.N("Не побегу."),
                    Fits = s => s.Has(SinType.Wrath) || s.Is(SinType.Pride) || (s.Virtue && s.Sin == SinType.Wrath),
                    NotWith = new[] { "Бережёт шкуру" },
                    Votes = new[] { new TraitVote(c => c.LastAlive && Fight(c), ActionType.Attack, 75f),
                                    new TraitVote(c => c.LastAlive && Fight(c), ActionType.Flee, -45f) } },

            new() { Name = Loc.N("Мстит за своих"),
                    What = Loc.N("Ранили своего рядом — бросается на врага."),
                    Why = Loc.N("Не уберёг сына в бою и с тех пор не прощает тех, кто бьёт своих."),
                    Says = Loc.N("За своих!"),
                    Fits = s => s.Has(SinType.Lust) || s.Is(SinType.Wrath),
                    Votes = new[] { new TraitVote(c => c.AllyDamagedRecently && Fight(c), ActionType.Attack, 80f) } },

            new() { Name = Loc.N("Не бросит своих"),
                    What = Loc.N("Свой в беде — бежит к нему, даже против приказа."),
                    Why = Loc.N("Его самого когда-то бросили умирать."),
                    Says = Loc.N("Своих не брошу!"),
                    Advice = Loc.N("Сначала помогите раненому."),
                    Fits = s => s.Has(SinType.Lust) || s.Good,
                    Votes = new[] { new TraitVote(c => c.AllyInDanger, ActionType.SaveAlly, 40f),
                                    new TraitVote(c => c.AllyInDanger && c.CommandLeavesFight, ActionType.ObeyCommand, -20f) } },

            new() { Name = Loc.N("Падок на добро"), NameShe = Loc.N("Падка на добро"),
                    What = Loc.N("Видит, где лежит ценное, и тянется туда."),
                    Why = Loc.N("Был контрабандистом: чужое добро — его ремесло."),
                    Says = Loc.N("Тут есть чем поживиться."),
                    Fits = s => s.Has(SinType.Greed),
                    NotWith = new[] { "Бессребреник" },
                    Votes = new[] { new TraitVote(c => c.NearbyLoot > 0, ActionType.Loot, 45f) } },

            new() { Name = Loc.N("Бессребреник"), NameShe = Loc.N("Бессребреница"),
                    What = Loc.N("С мёртвых не берёт."),
                    Why = Loc.N("Видел, что бывает с теми, кто берёт с мёртвых."),
                    Says = Loc.N("С мёртвых не беру."),
                    Fits = s => (s.Good || (s.Virtue && s.Sin == SinType.Greed)) && !s.Has(SinType.Greed),
                    NotWith = new[] { "Падок на добро" },
                    Votes = new[] { new TraitVote(c => c.NearbyLoot > 0, ActionType.Loot, -40f) } },

            new() { Name = Loc.N("Мёртвых не боится"),
                    What = Loc.N("Против нежити не отступает."),
                    Why = Loc.N("Всю жизнь хоронил мёртвых. Теперь они встают — что с того."),
                    Says = Loc.N("Мёртвых не боюсь."),
                    Fits = s => s.Trade == Trade.Novice || s.Trade == Trade.Quarryman || s.Trade == Trade.Peasant || s.Good,
                    Votes = new[] { new TraitVote(c => c.EnemyIsUndead, ActionType.Flee, -60f) } },

            new() { Name = Loc.N("Держит место"),
                    What = Loc.N("Велено стоять — стоит, даже когда страшно."),
                    Why = Loc.N("Клал стены. Стена не бегает."),
                    Says = Loc.N("Велено стоять — стою."),
                    Fits = s => s.Is(SinType.Sloth) || s.Trade == Trade.Quarryman || (s.Virtue && s.Sin == SinType.Wrath),
                    Votes = new[] { new TraitVote(Holding, ActionType.Flee, -45f),
                                    new TraitVote(Holding, ActionType.ObeyCommand, 30f) } },

            new() { Name = Loc.N("Хладнокровный"), NameShe = Loc.N("Хладнокровная"),
                    What = Loc.N("В окружении не мечется — дерётся, где стоит."),
                    Why = Loc.N("Терпел и не такое."),
                    Says = Loc.N("Спокойно."),
                    Fits = s => s.Good || s.Is(SinType.Pride),
                    Votes = new[] { new TraitVote(c => c.Surrounded, ActionType.Flee, -60f) } },

            new() { Name = Loc.N("Выдыхается"),
                    What = Loc.N("К середине боя устаёт и встаёт передохнуть."),
                    Why = Loc.N("Всю жизнь берёг силы — и после смерти бережёт."),
                    Says = Loc.N("Дай отдышаться…"),
                    Advice = Loc.N("Дайте передышку."),
                    Fits = s => s.Has(SinType.Sloth) || s.Has(SinType.Gluttony),
                    NotWith = new[] { "Неутомимый" },
                    Votes = new[] { new TraitVote(c => c.Fatigue > 0.5f, ActionType.Idle, 60f),
                                    new TraitVote(c => c.Fatigue > 0.5f, ActionType.Attack, -30f) } },

            new() { Name = Loc.N("Неутомимый"), NameShe = Loc.N("Неутомимая"),
                    What = Loc.N("Усталость его не останавливает."),
                    Why = Loc.N("Работал от зари до зари — и не помнит, как это: отдыхать."),
                    Says = Loc.N("Отдохну потом."),
                    Fits = s => !s.Has(SinType.Sloth) && (s.Trade == Trade.Peasant || s.Trade == Trade.Carter
                                || s.Trade == Trade.Quarryman || (s.Virtue && s.Sin == SinType.Sloth)),
                    NotWith = new[] { "Выдыхается" },
                    Votes = new[] { new TraitVote(c => c.Fatigue > 0.5f, ActionType.Idle, -45f) } },

            new() { Name = Loc.N("Хранитель склепа"), NameShe = Loc.N("Хранительница склепа"),
                    What = Loc.N("В склепе — как дома: не отступит."),
                    Why = Loc.N("При жизни стерёг склеп. Теперь лежит в нём сам — и стережёт."),
                    Says = Loc.N("Это мой склеп."),
                    Fits = s => s.Trade == Trade.Novice || s.Trade == Trade.Mage || s.Is(SinType.Pride),
                    Votes = new[] { new TraitVote(Crypt, ActionType.Attack, 45f),
                                    new TraitVote(Crypt, ActionType.Flee, -45f) } },

            new() { Name = Loc.N("Первым в бой"), NameShe = Loc.N("Первой в бой"),
                    What = Loc.N("Рвётся вперёд, пока другие ждут."),
                    Why = Loc.N("Бился на арене: кто ударил первым, тот и жив."),
                    Says = Loc.N("Вперёд!"),
                    Fits = s => s.Has(SinType.Pride) || s.Has(SinType.Wrath),
                    Votes = new[] { new TraitVote(c => Fight(c) && !c.IsEngaged, ActionType.Attack, 75f) } },
        };

        private static readonly Dictionary<string, Trait> _byName = Index();

        private static Dictionary<string, Trait> Index()
        {
            var d = new Dictionary<string, Trait>(StringComparer.Ordinal);
            foreach (var t in All) d[t.Name] = t;
            return d;
        }

        public static Trait Find(string name) =>
            !string.IsNullOrEmpty(name) && _byName.TryGetValue(name, out var t) ? t : null;

        /// <summary>Подходящие душе особенности.</summary>
        public static List<Trait> Fitting(TraitSoul s)
        {
            var list = new List<Trait>();
            foreach (var t in All) if (t.Fits(s)) list.Add(t);
            return list;
        }

        /// <summary>
        /// Одна-две особенности по номеру (без жребия: тот же номер — тот же человек). Пустым не бывает:
        /// если по душе не подошло ничего, берётся по телу — у каждого тела своя.
        /// </summary>
        public static List<string> Pick(TraitSoul s, int number)
        {
            var fits = Fitting(s);
            if (fits.Count == 0)
                fits.Add(Find(s.Shell == ShellType.Zombie ? "Не чувствует боли" : s.Shell == ShellType.Skeleton ? "Глуховат" : "Хладнокровный"));

            uint h = 2166136261u;
            unchecked { foreach (char ch in number.ToString()) { h ^= ch; h *= 16777619u; } h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; }

            var first = fits[(int)(h % (uint)fits.Count)];
            var picked = new List<string> { first.Name };

            // Вторая — у половины, и не та, что спорит с первой.
            if (((h >> 8) & 1) == 0)
            {
                var rest = fits.FindAll(t => t != first && Array.IndexOf(first.NotWith, t.Name) < 0
                                                        && Array.IndexOf(t.NotWith, first.Name) < 0);
                if (rest.Count > 0) picked.Add(rest[(int)((h >> 16) % (uint)rest.Count)].Name);
            }
            return picked;
        }

        /// <summary>Есть ли у души хоть одна особенность из библиотеки.</summary>
        public static bool Any(MemorySeed memory) => Of(memory).Count > 0;

        /// <summary>Имена особенностей души — ключи, как в сохранении.</summary>
        public static List<string> Of(MemorySeed memory)
        {
            var names = new List<string>();
            if (memory?.NarrativePerks == null) return names;
            foreach (var p in memory.NarrativePerks) if (Find(p.PerkName) != null) names.Add(p.PerkName);
            return names;
        }

        /// <summary>Записать особенности в память души (туда же, где сюжетные перки).</summary>
        public static void Write(MemorySeed memory, IEnumerable<string> names)
        {
            if (memory == null || names == null) return;
            memory.NarrativePerks ??= new List<NarrativePerk>();
            foreach (var n in names)
                if (!memory.NarrativePerks.Exists(p => p.PerkName == n))
                    memory.NarrativePerks.Add(new NarrativePerk { PerkName = n, IsFound = true });
        }

        /// <summary>
        /// Прибавки особенностей души к очкам кандидатов. <paramref name="muted"/> — особенность,
        /// которая в этом подсчёте молчит (пересчёт «от противного»); null — говорят все.
        /// </summary>
        public static void Apply(Dictionary<ActionType, float> scores, List<NarrativePerk> perks, DecisionContext c,
                                 string muted = null)
        {
            if (scores == null || perks == null || c == null) return;
            foreach (var p in perks)
            {
                if (muted != null && p.PerkName == muted) continue;
                var t = Find(p.PerkName);
                if (t == null) continue;
                foreach (var v in t.Votes)
                    if (scores.ContainsKey(v.Action) && v.When(c)) scores[v.Action] += v.Bonus;
            }
        }

        /// <summary>
        /// Третий круг «от противного»: первая особенность, без которой приказ был бы исполнен.
        /// Null — ни одна поодиночке не решает.
        /// </summary>
        public static string Decisive(MemorySeed memory, Func<string, bool> obeysWithout)
        {
            if (obeysWithout == null) return null;
            foreach (var name in Of(memory))
                if (obeysWithout(name)) return name;
            return null;
        }

        /// <summary>Имя для глаз — в роде души.</summary>
        public static string Title(string name, Gender gender)
        {
            var t = Find(name);
            if (t == null) return Loc.T(name);
            return Loc.T(gender == Gender.Female && !string.IsNullOrEmpty(t.NameShe) ? t.NameShe : t.Name);
        }

        /// <summary>Причина отказа словами — «что делает», со строчной и без точки; род — у вызывающего.</summary>
        public static string Reason(string name)
        {
            var t = Find(name);
            return t == null ? "" : Lower(Loc.T(t.What).TrimEnd('.'));
        }

        /// <summary>Совет игроку при отказе по особенности; null — у особенности своего рычага нет.</summary>
        public static string AdviceFor(string name)
        {
            var t = Find(name);
            return t == null || string.IsNullOrEmpty(t.Advice) ? null : Loc.T(t.Advice);
        }

        /// <summary>Реплика отказа от первого лица (RefusalPresenter).</summary>
        public static string Says(string name)
        {
            var t = Find(name);
            return t == null || string.IsNullOrEmpty(t.Says) ? null : Loc.T(t.Says);
        }

        /// <summary>Строка для карточки души: «Глуховат — тихий приказ издали не слышит…».</summary>
        public static string Describe(MemorySeed memory, Gender gender = Gender.Male)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var name in Of(memory))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(Title(name, gender)).Append(" — ")
                  .Append(Grammar.For(gender, Lower(Loc.T(Find(name).What))));
            }
            return sb.ToString();
        }

        private static string Lower(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
