// Стенд балансировки. Собирает НАСТОЯЩИЕ модули Sinbinder и прогоняет
// через них сотни тысяч голосований, чтобы увидеть распределения,
// которых иначе не увидеть без сотни часов игры.
//
// Цикл голосования повторяет BehaviourResolver.DecideDetailed построчно:
// те же кандидаты, тот же потолок голоса, та же относительная
// уверенность, тот же порог колебания. Сами модули и AOSConfig взяты
// из проекта без единой правки — значит, выводы о весах относятся
// к его коду, а не к моей выдумке.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.AOS.Modules;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.Inventory;

static class Bench
{
    static List<IPersonalityModule> Modules() => new List<IPersonalityModule>
    {
        new GreedModule(), new PrideModule(), new WrathModule(), new EnvyModule(),
        new LustModule(), new GluttonyModule(), new SlothModule(), new PatienceModule(),
        new FearModule(), new LoyaltyModule(), new MoralityModule(), new MemoryModule(),
        new VirtueModule()
    };

    // ---------- случайные, но правдоподобные души и положения ----------

    static SoulData MakeSoul(Random r, string name)
    {
        var spectra = new float[7];
        int dominant = r.Next(7);
        for (int i = 0; i < 7; i++)
            spectra[i] = i == dominant
                ? (float)(40 + r.NextDouble() * 55) * (r.NextDouble() < 0.25 ? -1 : 1)
                : (float)((r.NextDouble() * 50) - 25);

        var moral = (MoralType)r.Next(3);
        return new SoulData(name, moral, 1 + r.Next(3), spectra);
    }

    static DecisionContext MakeContext(Random r, Warrior w, bool forceCommand)
    {
        float hpRatio = (float)(0.15 + r.NextDouble() * 0.85);
        int enemies = r.NextDouble() < 0.20 ? 0 : r.Next(1, 6);
        int allies  = r.Next(0, 5);

        var c = new DecisionContext
        {
            MaxHP = 30f,
            CurrentHP = 30f * hpRatio,
            NearbyEnemies = enemies,
            NearbyAllies = allies,
            AllyInDanger = allies > 0 && r.NextDouble() < 0.30,
            NearbyLoot = r.NextDouble() < 0.35 ? r.Next(1, 4) : 0,
            DangerLevel = Mathf.Clamp01((1f - hpRatio) * 0.5f + Mathf.Min(enemies, 4) / 8f),
            UnpaidMissions = r.NextDouble() < 0.30 ? r.Next(1, 5) : 0,
            RelationshipWithCommander = (float)(r.NextDouble() * 100),
            // Усталость копится с нуля и редко доходит до предела,
            // поэтому не равномерно, а со скосом к малым значениям:
            // равномерное распределение переоценивало Уныние.
            Fatigue = (float)(r.NextDouble() * r.NextDouble()),
            RecentMemories = new List<MemoryRecord>()
        };
        c.IsExhausted = c.Fatigue > 0.7f;

        int eng = enemies == 0 ? 0 : r.Next(0, Math.Min(enemies, 3) + 1);
        c.EngagedWith = eng;
        c.IsEngaged = eng > 0;
        c.Surrounded = eng >= 2;
        c.TargetBackExposed = enemies > 0 && r.NextDouble() < 0.25;
        c.BrotherNearby = allies > 0 && r.NextDouble() < 0.15;
        c.LastAlive = allies == 0 && enemies > 0;

        if (forceCommand)
        {
            c.HasCommand = true;
            string[] kinds = { "Move", "Attack", "Hold", "Defend" };
            c.CommandType = kinds[r.Next(kinds.Length)];
        }

        c.CarriedItems = new List<InventoryItem>();
        if (r.NextDouble() < 0.20)
            c.CarriedItems.Add(new InventoryItem("предмет", "", ItemType.Equipment, 1,
                (SinType)r.Next(7), (float)(20 + r.NextDouble() * 60)));

        return c;
    }

    // ---------- голосование, один в один с резолвером ----------

    struct Outcome
    {
        public ActionType Action; public string Module;
        public float Gap, Confidence; public bool Hesitated, Refused;
    }

    static Dictionary<ActionType, float> Candidates(DecisionContext c)
    {
        var s = new Dictionary<ActionType, float> { { ActionType.Idle, 0f } };
        if (c.NearbyEnemies > 0) { s[ActionType.Attack] = 0f; s[ActionType.Flee] = 0f; }
        if (c.NearbyLoot > 0) s[ActionType.Loot] = 0f;
        if (c.AllyInDanger) s[ActionType.SaveAlly] = 0f;
        if (c.HasCommand) s[ActionType.ObeyCommand] = 0f;
        return s;
    }

    static Outcome Vote(List<IPersonalityModule> modules, Warrior w, DecisionContext c,
                        AOSConfig cfg, SquadStrategy strategy)
    {
        var scores = Candidates(c);
        var soul = Soul.FromWarrior(w);
        var loudest = new Dictionary<ActionType, (string m, float v)>();

        foreach (var module in modules)
            foreach (var action in scores.Keys.ToList())
            {
                float voice = module.Evaluate(soul, c, action);
                if (cfg.MaxVoice > 0f) voice = Mathf.Clamp(voice, -cfg.MaxVoice, cfg.MaxVoice);
                scores[action] += voice;
                if (!loudest.TryGetValue(action, out var cur) || voice > cur.v)
                    loudest[action] = (module.ModuleID, voice);
            }

        TemptationResolver.Apply(scores, c, cfg.TemptationScale);

        if (cfg.StrategyScale > 0f)
            foreach (var mod in StrategyDatabase.GetModifiers(strategy))
                if (scores.ContainsKey(mod.Action))
                    scores[mod.Action] += mod.Bonus * cfg.StrategyScale;

        var sorted = scores.OrderByDescending(kv => kv.Value).ToList();
        var best = sorted[0];
        bool alone = sorted.Count < 2;
        float gap = alone ? 0f : best.Value - sorted[1].Value;
        float loudness = Mathf.Max(
            Mathf.Max(Mathf.Abs(best.Value), alone ? 0f : Mathf.Abs(sorted[1].Value)), 1f);
        float confidence = alone ? 1f : gap / loudness;

        var o = new Outcome
        {
            Action = best.Key,
            Module = loudest.TryGetValue(best.Key, out var t) ? t.m : "",
            Gap = gap, Confidence = confidence,
            Hesitated = confidence < cfg.HesitationShare
        };
        if (o.Hesitated) { o.Action = ActionType.Idle; o.Module = ""; }

        bool obeyed = c.SatisfiedBy(o.Action);
        o.Refused = c.HasCommand && !obeyed && !o.Hesitated;
        return o;
    }

    // ---------- прогон ----------

    class Tally
    {
        public int N, WithCmd, Refused, Hesitated, RefusedButAligned;
        public Dictionary<ActionType, int> Actions = new();
        public Dictionary<string, int> Voices = new();
        public List<float> Gaps = new();
        public float RefusalRate => WithCmd > 0 ? (float)Refused / WithCmd : 0f;
        public float HesitationRate => N > 0 ? (float)Hesitated / N : 0f;
    }

    static Tally Run(int n, AOSConfig cfg, SquadStrategy strategy, int seed,
                     double commandShare = 0.5)
    {
        var r = new Random(seed);
        var modules = Modules();
        var t = new Tally();

        for (int i = 0; i < n; i++)
        {
            var w = new Warrior { Soul = MakeSoul(r, "В" + i), Loyalty = (float)(r.NextDouble() * 100) };
            bool cmd = r.NextDouble() < commandShare;
            var c = MakeContext(r, w, cmd);
            w.UnpaidMissions = c.UnpaidMissions;

            var o = Vote(modules, w, c, cfg, strategy);

            t.N++;
            t.Actions.TryGetValue(o.Action, out int a); t.Actions[o.Action] = a + 1;
            if (!string.IsNullOrEmpty(o.Module))
            { t.Voices.TryGetValue(o.Module, out int v); t.Voices[o.Module] = v + 1; }
            if (o.Hesitated) t.Hesitated++;
            if (c.HasCommand)
            {
                t.WithCmd++;
                if (o.Refused)
                {
                    t.Refused++;
                    // Приказ «Attack», а воин пошёл в атаку по своей причине —
                    // он сделал ровно то, о чём просили. Такой же случай уже
                    // разобран для отхода (Flee при CommandIsFallBack).
                    bool aligned =
                        (c.CommandType == "Attack" && o.Action == ActionType.Attack) ||
                        (c.CommandType == "Hold"   && o.Action == ActionType.Idle) ||
                        (c.CommandType == "Defend" && (o.Action == ActionType.Idle
                                                    || o.Action == ActionType.Attack));
                    if (aligned) t.RefusedButAligned++;
                }
            }
            if (t.Gaps.Count < 200000) t.Gaps.Add(o.Gap);
        }
        return t;
    }

    static string Pct<T>(Dictionary<T, int> d, int total)
    {
        return string.Join(", ", d.OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key} {kv.Value * 100.0 / Math.Max(total, 1):F1}%"));
    }

    // ---------- честна ли панель пророчеств ----------
    //
    // TemperamentPredictor обещает игроку, как воин поступит в четырёх
    // положениях, и на этом обещании стоит третье требование к демо:
    // отказ можно было предотвратить, и игрок это видел. Если панель
    // ошибается, вся лестница прозрачности — ложь, а отказ читается
    // как подстава.
    //
    // Проверка: берём пророчество для положения (на тех же выдуманных
    // контекстах, что строит предсказатель), потом много раз ставим
    // воина в ПОХОЖЕЕ, но не тождественное положение — сохраняем
    // определяющий признак, остальное случайно — и смотрим, совпадает
    // ли поступок с обещанным.

    static DecisionContext Predictor(Warrior w, int kind)
    {
        var c = new DecisionContext
        {
            CurrentHP = w.MaxHP, MaxHP = w.MaxHP,
            NearbyEnemies = 2, NearbyAllies = 2, DangerLevel = 0.3f,
            UnpaidMissions = w.UnpaidMissions,
            RelationshipWithCommander = 50f,
            RecentMemories = new List<MemoryRecord>(),
            CarriedItems = new List<InventoryItem>()
        };
        if (kind == 1) c.AllyInDanger = true;
        if (kind == 2) c.NearbyLoot = 3;
        if (kind == 3) { c.HasCommand = true; c.CommandType = "Move"; c.IsEngaged = true; c.EngagedWith = 1; }
        return c;
    }

    static DecisionContext Similar(Random r, Warrior w, int kind)
    {
        var c = MakeContext(r, w, kind == 3);
        if (c.NearbyEnemies == 0) c.NearbyEnemies = 1 + r.Next(3);
        c.AllyInDanger = kind == 1;
        if (kind == 1 && c.NearbyAllies == 0) c.NearbyAllies = 1;
        c.NearbyLoot = kind == 2 ? 1 + r.Next(3) : 0;
        if (kind == 3) { c.HasCommand = true; c.CommandType = "Move"; c.IsEngaged = true; c.EngagedWith = Math.Max(1, c.EngagedWith); }
        return c;
    }

    static void Prophecy(AOSConfig cfg, int n)
    {
        Console.WriteLine("\n=== ЧЕСТНА ЛИ ПАНЕЛЬ ПРОРОЧЕСТВ ===");
        Console.WriteLine("совпало — воин поступил так, как обещала панель\n");

        string[] names = { "Пока бой ровен", "Когда падает друг",
                           "Когда рядом золото", "Когда велено отойти" };
        var modules = Modules();
        var r = new Random(101);

        // Калибровка: при какой уверенности обещание можно давать без
        // оговорок. Формулировка должна соответствовать точности, иначе
        // панель врёт даже когда движок прав.
        {
            var buckets = new (float lo, float hi, int hit, int total)[]
            {
                (0.00f, 0.10f, 0, 0), (0.10f, 0.25f, 0, 0), (0.25f, 0.50f, 0, 0),
                (0.50f, 0.80f, 0, 0), (0.80f, 1.20f, 0, 0), (1.20f, 99f, 0, 0)
            };
            var rr = new Random(202);
            for (int i = 0; i < n / 8; i++)
            {
                var w = new Warrior { Soul = MakeSoul(rr, "В"), Loyalty = (float)(rr.NextDouble() * 100) };
                int kind = rr.Next(4);
                var told = Vote(modules, w, Predictor(w, kind), cfg, SquadStrategy.Balanced);
                if (told.Hesitated) continue;
                var real = Vote(modules, w, Similar(rr, w, kind), cfg, SquadStrategy.Balanced);
                for (int b = 0; b < buckets.Length; b++)
                    if (told.Confidence >= buckets[b].lo && told.Confidence < buckets[b].hi)
                    {
                        buckets[b].total++;
                        if (real.Action == told.Action) buckets[b].hit++;
                        break;
                    }
            }
            Console.WriteLine("калибровка: уверенность → доля сбывшихся");
            foreach (var b in buckets)
                if (b.total > 50)
                    Console.WriteLine($"    {b.lo:F2}–{(b.hi > 90 ? 99 : b.hi),4:F2}  "
                        + $"{b.hit * 100.0 / b.total,5:F1}%   ({b.total} случаев)");
            Console.WriteLine();
        }

        Console.WriteLine($"{"положение",22} {"всего",9} {"уверенно",10} {"с оговоркой",12} {"колеблется",11}");
        for (int kind = 0; kind < 4; kind++)
        {
            int hit = 0, total = 0, vague = 0;
            int sureHit = 0, sureTotal = 0, softHit = 0, softTotal = 0;

            for (int i = 0; i < n / 40; i++)
            {
                var w = new Warrior { Soul = MakeSoul(r, "В"), Loyalty = (float)(r.NextDouble() * 100) };
                var told = Vote(modules, w, Predictor(w, kind), cfg, SquadStrategy.Balanced);
                if (told.Hesitated) { vague++; continue; }

                // Так предсказатель выбирает формулировку: при разрыве
                // втрое выше порога он обещает без оговорок, иначе
                // добавляет «скорее всего, но не наверняка».
                bool sure = told.Confidence > cfg.HesitationShare * 3f;

                for (int k = 0; k < 5; k++)
                {
                    var real = Vote(modules, w, Similar(r, w, kind), cfg, SquadStrategy.Balanced);
                    total++;
                    bool ok = real.Action == told.Action;
                    if (ok) hit++;
                    if (sure) { sureTotal++; if (ok) sureHit++; }
                    else      { softTotal++; if (ok) softHit++; }
                }
            }
            Console.WriteLine($"{names[kind],22} {hit * 100.0 / Math.Max(total, 1),8:F1}% "
                + $"{sureHit * 100.0 / Math.Max(sureTotal, 1),10:F1}% "
                + $"{softHit * 100.0 / Math.Max(softTotal, 1),12:F1}% "
                + $"{vague * 100.0 / Math.Max(n / 40, 1),11:F1}%");
        }
    }

    // ---------- автономный бой ----------
    //
    // AutoBattleResolver не вызывался ни разу за всю историю проекта.
    // Здесь его цикл повторён на настоящем AutoBattleContext, чтобы
    // проверить главное: собирается ли осмысленный бюллетень без сцены.
    // Прежний CombatDecisionContext возвращал мир без врагов, в нём
    // оставалось одно «стоять», и бой проходил в неподвижности.

    static void AutoBattle(AOSConfig cfg, int n)
    {
        Console.WriteLine("\n=== АВТОНОМНЫЙ БОЙ (никогда не запускался) ===");
        var modules = Modules();
        var r = new Random(303);

        int battles = Math.Max(200, n / 500);
        int draws = 0, squadWins = 0, enemyWins = 0;
        long turnSum = 0, actionSum = 0, idleSum = 0;

        for (int b = 0; b < battles; b++)
        {
            var squad = new List<Warrior>();
            var foes = new List<Warrior>();
            for (int i = 0; i < 4; i++)
            {
                squad.Add(new Warrior { Soul = MakeSoul(r, "С" + i), Attack = 5f + (float)r.NextDouble() * 4f,
                                        Loyalty = (float)(r.NextDouble() * 100), Team = Team.Player,
                                        IsCommander = i == 0, Relationships = new RelationshipSystem() });
                foes.Add(new Warrior { Soul = MakeSoul(r, "В" + i), Attack = 5f + (float)r.NextDouble() * 4f,
                                       Loyalty = (float)(r.NextDouble() * 100), Team = Team.Enemy,
                                       Relationships = new RelationshipSystem() });
            }

            int turns = 0;
            while (turns < 20 && squad.Exists(w => !w.IsDead) && foes.Exists(e => !e.IsDead))
            {
                foreach (var w in squad.Where(x => !x.IsDead).ToList())
                {
                    var ctx = AutoBattleContext.Create(w, squad, foes);
                    var o = Vote(modules, w, ctx, cfg, SquadStrategy.Balanced);
                    actionSum++; if (o.Action == ActionType.Idle) idleSum++;
                    if (o.Action == ActionType.Attack)
                    {
                        var t = foes.Where(x => !x.IsDead).OrderBy(x => x.HP).FirstOrDefault();
                        t?.TakeDamage(w.Attack);
                    }
                    else if (o.Action == ActionType.SaveAlly)
                    {
                        var a = squad.Where(x => !x.IsDead && x != w && x.HP < x.MaxHP * 0.5f)
                                     .OrderBy(x => x.HP).FirstOrDefault();
                        a?.Heal(5f);
                    }
                }
                foreach (var e in foes.Where(x => !x.IsDead).ToList())
                {
                    var ctx = AutoBattleContext.Create(e, foes, squad);
                    var o = Vote(modules, e, ctx, cfg, SquadStrategy.Balanced);
                    actionSum++; if (o.Action == ActionType.Idle) idleSum++;
                    if (o.Action == ActionType.Attack)
                    {
                        var t = squad.Where(x => !x.IsDead).OrderBy(x => x.HP).FirstOrDefault();
                        t?.TakeDamage(e.Attack);
                    }
                }
                turns++;
            }

            turnSum += turns;
            bool squadAlive = squad.Exists(w => !w.IsDead);
            bool foesAlive = foes.Exists(e => !e.IsDead);
            if (squadAlive && foesAlive) draws++;
            else if (squadAlive) squadWins++;
            else enemyWins++;
        }

        Console.WriteLine($"боёв: {battles}, средняя длина {turnSum / (double)battles:F1} ходов "
                        + $"(предел 20)");
        Console.WriteLine($"исходы: победа отряда {squadWins * 100.0 / battles:F0}%, "
                        + $"поражение {enemyWins * 100.0 / battles:F0}%, "
                        + $"ничья по истечении ходов {draws * 100.0 / battles:F0}%");
        Console.WriteLine($"бездействие: {idleSum * 100.0 / Math.Max(actionSum, 1):F1}% решений");
        Console.WriteLine(idleSum * 100.0 / Math.Max(actionSum, 1) > 80
            ? "  ВНИМАНИЕ: бой проходит в неподвижности — бюллетень пуст"
            : "  бой идёт: воины действуют");
    }

    static void Main(string[] args)
    {
        Debug.Mute = true;
        int n = args.Length > 0 ? int.Parse(args[0]) : 200000;
        var cfg = ScriptableObject.CreateInstance<AOSConfig>();

        // Модули читают конфиг через Resources.Load в своих конструкторах.
        // Регистрируем наш экземпляр, иначе каждый создаст запасной
        // и правки весов на стенде не дойдут до голосования.
        UnityEngine.Resources.Register(cfg);

        Console.WriteLine($"=== ОСНОВНОЙ ПРОГОН: {n:N0} голосований ===");
        Console.WriteLine($"MaxVoice={cfg.MaxVoice}  HesitationShare={cfg.HesitationShare}  "
                        + $"StrategyScale={cfg.StrategyScale}\n");

        var b = Run(n, cfg, SquadStrategy.Balanced, 1);
        Console.WriteLine($"Действия:  {Pct(b.Actions, b.N)}");
        Console.WriteLine($"Голоса:    {Pct(b.Voices, b.Voices.Values.Sum())}");
        Console.WriteLine($"Колебание: {b.HesitationRate * 100:F1}%");
        Console.WriteLine($"Отказы при приказе: {b.RefusalRate * 100:F1}%  "
                        + $"(решений с приказом: {b.WithCmd:N0})");
        Console.WriteLine($"  из них воин всё же сделал требуемое: "
                        + $"{b.RefusedButAligned * 100.0 / Math.Max(b.Refused, 1):F1}% "
                        + $"→ настоящих отказов {(b.Refused - b.RefusedButAligned) * 100.0 / Math.Max(b.WithCmd, 1):F1}%\n");

        Console.WriteLine("=== ПОТОЛОК ГОЛОСА: как он меняет картину ===");
        Console.WriteLine($"{"MaxVoice",9} {"отказы",8} {"колеб.",8} {"SaveAlly",9} {"Flee",7} {"Страх",7}");
        foreach (float mv in new float[] { 0, 40, 60, 80, 100, 120, 160, 250 })
        {
            cfg.MaxVoice = mv;
            var t = Run(n / 4, cfg, SquadStrategy.Balanced, 2);
            int save = t.Actions.TryGetValue(ActionType.SaveAlly, out var sv) ? sv : 0;
            int flee = t.Actions.TryGetValue(ActionType.Flee, out var fl) ? fl : 0;
            int fear = t.Voices.TryGetValue("Fear", out var fr) ? fr : 0;
            Console.WriteLine($"{(mv == 0 ? "нет" : mv.ToString("F0")),9} "
                + $"{t.RefusalRate * 100,7:F1}% {t.HesitationRate * 100,7:F1}% "
                + $"{save * 100.0 / t.N,8:F1}% {flee * 100.0 / t.N,6:F1}% "
                + $"{fear * 100.0 / Math.Max(t.Voices.Values.Sum(), 1),6:F1}%");
        }
        cfg.MaxVoice = 120f;

        Console.WriteLine("\n=== УСТАНОВКИ ОТРЯДА: работает ли рычаг ===");
        Console.WriteLine($"{"установка",20} {"отказы",8} {"SaveAlly",9} {"Attack",8} {"Loot",7} {"Flee",7}");
        foreach (var s in new[] { SquadStrategy.Balanced, SquadStrategy.Aggressive,
                                  SquadStrategy.Defensive, SquadStrategy.Cautious,
                                  SquadStrategy.LootFocused, SquadStrategy.Focused })
        {
            var t = Run(n / 4, cfg, s, 3);
            double P(ActionType a) => (t.Actions.TryGetValue(a, out var v) ? v : 0) * 100.0 / t.N;
            Console.WriteLine($"{SquadOrders.Name(s),20} {t.RefusalRate * 100,7:F1}% "
                + $"{P(ActionType.SaveAlly),8:F1}% {P(ActionType.Attack),7:F1}% "
                + $"{P(ActionType.Loot),6:F1}% {P(ActionType.Flee),6:F1}%");
        }

        Console.WriteLine("\n=== ПОДБОР ВЕСОВ: перебор по трём осям ===");
        Console.WriteLine("цель: отказы 20-30%, ни одно действие выше 35%, "
                        + "ни один голос выше 25%, колебание 5-12%\n");

        float fearHp0 = cfg.FearFleeLowHpBonus;
        float fearDanger0 = cfg.FearFleeDangerMultiplier;
        float fearSurr0 = cfg.FearFleeSurroundedBonus;
        float loyalty0 = cfg.LoyaltyObeySinMultiplier;
        float maxVoice0 = cfg.MaxVoice;

        var results = new List<(double score, float fear, float loyal, float mv, Tally t)>();

        foreach (float fearScale in new float[] { 1.0f, 0.7f, 0.5f, 0.35f, 0.25f })
        foreach (float loyal in new float[] { 0.5f, 1.0f, 1.5f, 2.0f, 3.0f })
        foreach (float mv in new float[] { 60f, 80f, 120f })
        {
            cfg.FearFleeLowHpBonus = fearHp0 * fearScale;
            cfg.FearFleeDangerMultiplier = fearDanger0 * fearScale;
            cfg.FearFleeSurroundedBonus = fearSurr0 * fearScale;
            cfg.LoyaltyObeySinMultiplier = loyal;
            cfg.MaxVoice = mv;

            var t = Run(n / 8, cfg, SquadStrategy.Balanced, 7);

            double refus = t.RefusalRate;
            double topAction = t.Actions.Values.Max() * 1.0 / t.N;
            double topVoice = t.Voices.Count == 0 ? 1 :
                              t.Voices.Values.Max() * 1.0 / t.Voices.Values.Sum();
            double hes = t.HesitationRate;

            // Штраф за отклонение от каждой цели. Отказы весят вдвое:
            // это центральное число игры.
            double score =
                2.0 * Math.Abs(refus - 0.25) / 0.25
                + Math.Max(0, topAction - 0.35) / 0.35
                + Math.Max(0, topVoice - 0.25) / 0.25
                + Math.Max(0, Math.Abs(hes - 0.085) - 0.035) / 0.085;

            results.Add((score, fearScale, loyal, mv, t));
        }

        Console.WriteLine($"{"Страх×",7} {"Верн.",6} {"Потол.",7} | {"отказы",7} {"колеб.",7} "
                        + $"{"верх.дейст.",12} {"верх.голос",11}");
        foreach (var r in results.OrderBy(x => x.score).Take(8))
        {
            var t = r.t;
            var topA = t.Actions.OrderByDescending(kv => kv.Value).First();
            var topV = t.Voices.OrderByDescending(kv => kv.Value).First();
            Console.WriteLine($"{r.fear,7:F2} {r.loyal,6:F1} {r.mv,7:F0} | "
                + $"{t.RefusalRate * 100,6:F1}% {t.HesitationRate * 100,6:F1}% "
                + $"{topA.Key + " " + (topA.Value * 100.0 / t.N).ToString("F0") + "%",12} "
                + $"{topV.Key + " " + (topV.Value * 100.0 / t.Voices.Values.Sum()).ToString("F0") + "%",11}");
        }

        var best = results.OrderBy(x => x.score).First();
        cfg.FearFleeLowHpBonus = fearHp0 * best.fear;
        cfg.FearFleeDangerMultiplier = fearDanger0 * best.fear;
        cfg.FearFleeSurroundedBonus = fearSurr0 * best.fear;
        cfg.LoyaltyObeySinMultiplier = best.loyal;
        cfg.MaxVoice = best.mv;

        Console.WriteLine($"\nЛУЧШЕЕ: Страх×{best.fear:F2} "
            + $"(бонусы {fearHp0 * best.fear:F0}/{fearDanger0 * best.fear:F0}/{fearSurr0 * best.fear:F0}), "
            + $"верность {best.loyal:F1}, потолок {best.mv:F0}");

        var final = Run(n, cfg, SquadStrategy.Balanced, 11);
        Console.WriteLine($"  действия: {Pct(final.Actions, final.N)}");
        Console.WriteLine($"  голоса:   {Pct(final.Voices, final.Voices.Values.Sum())}");
        Console.WriteLine($"  отказы {final.RefusalRate * 100:F1}%, "
                        + $"колебание {final.HesitationRate * 100:F1}%");

        Console.WriteLine("\n  установки на подобранных весах:");
        foreach (var st in new[] { SquadStrategy.Defensive, SquadStrategy.Focused,
                                   SquadStrategy.Aggressive, SquadStrategy.Cautious })
        {
            var t = Run(n / 8, cfg, st, 12);
            double P(ActionType a) => (t.Actions.TryGetValue(a, out var v) ? v : 0) * 100.0 / t.N;
            Console.WriteLine($"    {SquadOrders.Name(st),-20} отказы {t.RefusalRate * 100,5:F1}%  "
                + $"SaveAlly {P(ActionType.SaveAlly),4:F1}%  Attack {P(ActionType.Attack),4:F1}%  "
                + $"Flee {P(ActionType.Flee),4:F1}%");
        }

        Console.WriteLine("\n=== ИМЕНОВАННЫЕ ВАРИАНТЫ ===");
        Console.WriteLine($"{"вариант",26} {"отказы",8} {"колеб.",7} {"Flee",6} {"Obey",6} {"верх.голос",12}");
        foreach (var v in new (string name, float fear, float loyal, float mv)[] {
            ("применено сейчас",      0.35f, 2.5f,  80f),
            ("верность 3.0",          0.35f, 3.0f,  80f),
            ("верность 3.5",          0.35f, 3.5f,  80f),
            ("верность 4.0",          0.35f, 4.0f,  80f),
            ("верность 5.0",          0.35f, 5.0f,  80f),
            ("верность 4.0, Страх 0.25", 0.25f, 4.0f, 80f) })
        {
            cfg.FearFleeLowHpBonus = fearHp0 * v.fear;
            cfg.FearFleeDangerMultiplier = fearDanger0 * v.fear;
            cfg.FearFleeSurroundedBonus = fearSurr0 * v.fear;
            cfg.LoyaltyObeySinMultiplier = v.loyal;
            cfg.MaxVoice = v.mv;
            var t = Run(n / 3, cfg, SquadStrategy.Balanced, 21);
            double P(ActionType a) => (t.Actions.TryGetValue(a, out var x) ? x : 0) * 100.0 / t.N;
            var tv = t.Voices.OrderByDescending(kv => kv.Value).First();
            Console.WriteLine($"{v.name,26} {t.RefusalRate * 100,7:F1}% {t.HesitationRate * 100,6:F1}% "
                + $"{P(ActionType.Flee),5:F1}% {P(ActionType.ObeyCommand),5:F1}% "
                + $"{tv.Key + " " + (tv.Value * 100.0 / t.Voices.Values.Sum()).ToString("F0") + "%",12}");
        }

        Prophecy(cfg, n);
        AutoBattle(cfg, n);

        Console.WriteLine("\n=== ПОРОГ КОЛЕБАНИЯ ===");
        foreach (float hs in new float[] { 0.05f, 0.10f, 0.15f, 0.20f, 0.30f })
        {
            cfg.HesitationShare = hs;
            var t = Run(n / 8, cfg, SquadStrategy.Balanced, 4);
            Console.WriteLine($"  доля {hs:F2} → колебание {t.HesitationRate * 100:F1}%, "
                            + $"отказы {t.RefusalRate * 100:F1}%");
        }
    }
}
