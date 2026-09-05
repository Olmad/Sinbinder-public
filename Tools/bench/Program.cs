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

    // ---------- слухи ----------
    // RumourManager тоже не вызывался ни разу. Проверяем главное:
    // выходит ли что-нибудь наружу и накапливается ли молва.

    static void Rumours()
    {
        Console.WriteLine("\n=== СЛУХИ (никогда не запускались) ===");
        RumourManager.Clear();

        var r = new Random(404);
        var squad = new List<Warrior>();
        for (int i = 0; i < 5; i++)
            squad.Add(new Warrior { Soul = MakeSoul(r, "Воин" + i), Team = Team.Player,
                                    Relationships = new RelationshipSystem() });
        var enemy = new Warrior { Soul = MakeSoul(r, "Чужой"), Team = Team.Enemy,
                                  Relationships = new RelationshipSystem() };

        var hero = squad[0];

        // Один слушатель слышит о герое трижды — молва должна копиться,
        // а текст обновляться вместе с деянием.
        RumourManager.Spread(squad[1], hero, DeedType.SaveAlly, 30f);
        // What отдаёт живой объект, а не снимок — значение надо забрать сразу.
        float progress1 = RumourManager.What(squad[1], hero).Progress;
        string text1 = RumourManager.What(squad[1], hero).Text;

        RumourManager.Spread(squad[1], hero, DeedType.LastStand, 30f);
        RumourManager.Spread(squad[1], hero, DeedType.LastStand, 30f);
        var after3 = RumourManager.What(squad[1], hero);

        Console.WriteLine($"  копится:  после первого {progress1:F0}, "
                        + $"после трёх {after3.Progress:F0}");
        Console.WriteLine($"  текст:    было «{text1}», стало «{after3.Text}»");
        Console.WriteLine($"  верит:    {RumourManager.Believes(squad[1], hero)} "
                        + $"(порог {RumourManager.ConfirmAt:F0})");

        // Остальные тоже слышат — считаем, сколько верят.
        for (int i = 2; i < squad.Count; i++)
            RumourManager.Spread(squad[i], hero, DeedType.LastStand, 120f);
        Console.WriteLine($"  верящих в отряде: {RumourManager.BelieverCount(hero)} из {squad.Count - 1}");

        // Чужому не рассказывают.
        RumourManager.Spread(enemy, hero, DeedType.Kill, 200f);
        Console.WriteLine($"  чужой услышал: {(RumourManager.What(enemy, hero) == null ? "нет" : "ДА — ошибка")}");

        // Сам о себе не сплетничает.
        RumourManager.Spread(hero, hero, DeedType.Kill, 200f);
        Console.WriteLine($"  сам о себе:    {(RumourManager.What(hero, hero) == null ? "нет" : "ДА — ошибка")}");

        // Пустые аргументы не роняют.
        RumourManager.Spread(null, hero, DeedType.Kill, 10f);
        RumourManager.Spread(squad[1], null, DeedType.Kill, 10f);
        Console.WriteLine("  пустые аргументы: не уронили");

        RumourManager.Clear();
        Console.WriteLine($"  после Clear: слухов о герое {RumourManager.About(hero).Count}");
    }

    // ---------- мирные миссии ----------
    //
    // FalseGodQuest содержит таблицу «грех × мораль → поступок» на
    // пятнадцать строк — авторский замысел того, как командир решает
    // судьбу деревни. ResolveCommander её не читает: он зовёт
    // голосование, у которого до сегодня не было ни одного голосующего.
    //
    // Проверка прямая: воспроизводит ли голосование таблицу.

    static void Missions(AOSConfig cfg)
    {
        Console.WriteLine("\n=== МИРНЫЕ МИССИИ: сходится ли с таблицей квеста ===");

        var table = new (SinType sin, MoralType moral, MissionAction want)[]
        {
            (SinType.Wrath, MoralType.Vicious, MissionAction.KillEveryone),
            (SinType.Wrath, MoralType.Neutral, MissionAction.KillTraveler),
            (SinType.Wrath, MoralType.Pious,   MissionAction.KillTraveler),
            (SinType.Pride, MoralType.Vicious, MissionAction.DestroyAltar),
            (SinType.Pride, MoralType.Neutral, MissionAction.SanctifyAltar),
            (SinType.Pride, MoralType.Pious,   MissionAction.SanctifyAltar),
            (SinType.Greed, MoralType.Vicious, MissionAction.TaxVillage),
            (SinType.Greed, MoralType.Neutral, MissionAction.TaxVillage),
            (SinType.Greed, MoralType.Pious,   MissionAction.TaxVillage),
            (SinType.Sloth, MoralType.Vicious, MissionAction.IgnoreVillage),
            (SinType.Sloth, MoralType.Neutral, MissionAction.IgnoreVillage),
            (SinType.Sloth, MoralType.Pious,   MissionAction.IgnoreVillage),
            (SinType.Envy,  MoralType.Vicious, MissionAction.EnslaveVillage),
            (SinType.Envy,  MoralType.Neutral, MissionAction.EnslaveVillage),
            (SinType.Envy,  MoralType.Pious,   MissionAction.HelpVillage),
        };

        var available = new List<MissionAction>
        {
            MissionAction.KillEveryone, MissionAction.KillTraveler,
            MissionAction.DestroyAltar, MissionAction.SanctifyAltar,
            MissionAction.TaxVillage,   MissionAction.IgnoreVillage,
            MissionAction.EnslaveVillage, MissionAction.HelpVillage
        };

        var modules = Modules();
        int hit = 0;

        foreach (var row in table)
        {
            var spectra = new float[7];
            spectra[(int)row.sin] = 80f;
            var soul = new SoulData("К", row.moral, 2, spectra);
            var w = new Warrior { Soul = soul, Loyalty = 50f };

            var ctx = new MissionContext
            {
                HasAltar = true, IsVillageIntact = true, HasInnocentVictims = true,
                RecentMemories = new List<MemoryRecord>(),
                CarriedItems = new List<InventoryItem>()
            };

            var scores = new Dictionary<MissionAction, float>();
            foreach (var a in available) scores[a] = 0f;

            var s2 = Soul.FromWarrior(w);
            int voices = 0;
            foreach (var m in modules)
            {
                if (!(m is IMissionModule mm)) continue;
                voices++;
                foreach (var a in available) scores[a] += mm.EvaluateMission(s2, ctx, a);
            }

            var best = scores.OrderByDescending(kv => kv.Value).First();
            bool ok = best.Key == row.want;
            if (ok) hit++;

            Console.WriteLine($"  {(ok ? "+" : "-")} {row.sin,-8} {row.moral,-8} "
                + $"таблица {row.want,-14} голосование {best.Key,-14}"
                + (voices == 0 ? "  (НЕТ ГОЛОСУЮЩИХ)" : ""));
        }

        Console.WriteLine($"\n  сошлось {hit} из {table.Length}");
    }


    // ---------- где голос упирается в потолок ----------

    static DecisionContext TypicalContext()
    {
        var c = new DecisionContext
        {
            NearbyEnemies = 3, NearbyAllies = 2, NearbyLoot = 1,
            CurrentHP = 60f, MaxHP = 100f, DangerLevel = 0.7f,
            AllyInDanger = true, HasCommand = true, CommandType = "Attack",
            RelationshipWithCommander = 50f, Fatigue = 0.3f,
            RecentMemories = new List<MemoryRecord>(),
            CarriedItems = new List<InventoryItem>()
        };
        c.EngagedWith = 1; c.IsEngaged = true;
        return c;
    }

    static void Saturation(AOSConfig cfg)
    {
        Console.WriteLine("\n=== НАСЫЩЕНИЕ: при каком грехе модуль упирается в MaxVoice ===");
        Console.WriteLine($"MaxVoice = {cfg.MaxVoice}\n");

        var ctx = TypicalContext();
        var actions = new[] { ActionType.Attack, ActionType.Flee, ActionType.Idle,
                              ActionType.Loot, ActionType.SaveAlly, ActionType.ObeyCommand };

        foreach (var m in Modules())
        {
            int at = -1;
            float peakAt100 = 0f;
            for (int v = 1; v <= 200 && at < 0; v++)
            {
                var spectra = new float[7];
                for (int i = 0; i < 7; i++) spectra[i] = v;
                var soul = new Soul
                {
                    Name = "T", Spectra = spectra,
                    Morality = MoralityType.Neutral,
                    Loyalty = Math.Min(v, 100)
                };
                foreach (var a in actions)
                {
                    float voice = Math.Abs(m.Evaluate(soul, ctx, a));
                    if (v == 100 && voice > peakAt100) peakAt100 = voice;
                    if (voice >= cfg.MaxVoice) { at = v; break; }
                }
            }

            string verdict = at < 0
                ? $"не упирается (громче всего при 100: {peakAt100:F0})"
                : at <= 100
                    ? $"упирается при {at}  ← выше этого разницы нет"
                    : $"упирается при {at}";
            Console.WriteLine($"  {m.ModuleID,-10} {verdict}");
        }
    }

    static void CapComparison(int n, AOSConfig cfg)
    {
        Console.WriteLine("\n=== ПОТОЛОК ШКАЛЫ: меняется ли решение выше 100 ===");

        var modules = Modules();
        int[] caps = { 100, 40, 60, 80, 125, 150, 200 };
        var differs = new int[caps.Length];

        var r = new Random(7);
        for (int i = 0; i < n; i++)
        {
            var baseSoul = MakeSoul(r, "Б");
            var spectra = baseSoul.CopySpectra();

            int dom = 0;
            for (int k = 1; k < 7; k++)
                if (Math.Abs(spectra[k]) > Math.Abs(spectra[dom])) dom = k;

            var w = new Warrior { Loyalty = (float)(r.NextDouble() * 100) };
            var c = MakeContext(r, w, r.NextDouble() < 0.5);

            ActionType baseline = default;
            for (int ci = 0; ci < caps.Length; ci++)
            {
                var sp = (float[])spectra.Clone();
                sp[dom] = Math.Sign(sp[dom]) * caps[ci];
                w.Soul = new SoulData("Б", (MoralType)baseSoul.Moral, 2, sp);

                var o = Vote(modules, w, c, cfg, SquadStrategy.Balanced);
                if (ci == 0) baseline = o.Action;
                else if (o.Action != baseline) differs[ci]++;
            }
        }

        Console.WriteLine($"  выборка: {n:N0} положений, доминирующий грех поднят до потолка\n");
        for (int ci = 1; ci < caps.Length; ci++)
            Console.WriteLine($"  потолок {caps[ci],3}: решение отличается от потолка 100 "
                            + $"в {differs[ci] * 100.0 / n:F1}% случаев");
    }


    // ---------- навык командования ----------

    static void LeadershipCheck()
    {
        Console.WriteLine("\n=== НАВЫК КОМАНДОВАНИЯ: одна ось, одно следствие ===");

        int bad = 0;
        void Check(bool ok, string what)
        {
            if (!ok) { bad++; Console.WriteLine($"  ПРОВАЛ: {what}"); }
        }

        // Рядовой ведёт, но мало. Это и есть разница «командир — рядовой».
        Check(Leadership.SquadSize(0f) == Leadership.PrivateSquad, "ноль навыка уводит троих");
        Check(!Leadership.IsExperienced(0f), "ноль навыка — не командир");
        Check(Leadership.IsExperienced(25f), "двадцать пять — уже командир");

        // Предел и края шкалы.
        Check(Leadership.SquadSize(100f) == Leadership.MaxSquad, "сотня уводит двенадцать");
        Check(Leadership.SquadSize(-50f) == Leadership.PrivateSquad, "минус не ломает шкалу");
        Check(Leadership.SquadSize(9000f) == Leadership.MaxSquad, "выше сотни не растёт");

        // Монотонность: больше опыта — не меньше людей.
        int prev = 0;
        for (int v = 0; v <= 100; v++)
        {
            int size = Leadership.SquadSize(v);
            Check(size >= prev, $"навык {v} уводит не меньше предыдущего");
            prev = size;
        }

        // Порог демо: миссия доли 3 требует пятерых. Опытные проходят,
        // рядовой — нет, и именно это объясняет игроку, зачем нужен старший.
        Check(!Leadership.CanLead(0f, 5), "рядовой не уводит пятерых");
        Check(Leadership.CanLead(25f, 5), "Брат Хальд (25) уводит пятерых");
        Check(Leadership.CanLead(40f, 5), "Мара Сквалыга (40) уводит пятерых");
        Check(Leadership.CanLead(55f, 5), "Вейн Тихий (55) уводит пятерых");

        // Игрок не должен видеть цифр нигде.
        foreach (float v in new[] { 0f, 25f, 40f, 55f, 90f })
        {
            string text = Leadership.Describe(v);
            Check(!text.Any(char.IsDigit), $"описание навыка {v} без цифр: «{text}»");
        }
        Check(!Leadership.Shortfall(0f, 5).Any(char.IsDigit), "нехватка описана без цифр");

        Console.WriteLine($"  состав лагеря: рядовой {Leadership.SquadSize(0f)}, "
                        + $"Хальд {Leadership.SquadSize(25f)}, "
                        + $"Мара {Leadership.SquadSize(40f)}, "
                        + $"Вейн {Leadership.SquadSize(55f)}, "
                        + $"Карган {Leadership.SquadSize(90f)}");
        Console.WriteLine($"  словами: рядовой — «{Leadership.Describe(0f)}», "
                        + $"Вейн — «{Leadership.Describe(55f)}»");
        Console.WriteLine($"  не проходит: «{Leadership.Shortfall(0f, 5)}»");
        Console.WriteLine(bad == 0 ? "\n  все проверки прошли" : $"\n  ПРОВАЛОВ: {bad}");
    }

    // ---------- возвращение отряда ----------

    static void HomecomingCheck()
    {
        Console.WriteLine("\n=== ЭПИЛОГ: кто вернулся ===");

        int bad = 0;
        void Check(bool ok, string what)
        {
            if (!ok) { bad++; Console.WriteLine($"  ПРОВАЛ: {what}"); }
        }

        const int sent = 5;

        // Ни один исход не должен обнулить отряд: командир возвращается
        // всегда, иначе рассказывать о вылазке будет некому.
        foreach (SinType sin in Enum.GetValues(typeof(SinType)))
        {
            int back = Homecoming.Returned(sin, sent);
            Check(back >= 1, $"{sin}: вернулся хотя бы один");
            Check(back <= sent, $"{sin}: вернулось не больше ушедших");
            Check(!string.IsNullOrEmpty(Homecoming.Story(sin)), $"{sin}: объяснение есть");
            Check(!Homecoming.Story(sin).Any(char.IsDigit), $"{sin}: объяснение без цифр");
        }

        // Три исхода из сценария должны отличаться друг от друга: ради
        // этого игрок и выбирал старшего полчаса назад.
        int sloth = Homecoming.Returned(SinType.Sloth, sent);
        int wrath = Homecoming.Returned(SinType.Wrath, sent);
        int greed = Homecoming.Returned(SinType.Greed, sent);
        Check(sloth == 1, "Уныние возвращается один");
        Check(wrath == 2, "Гнев приводит одного");
        Check(greed == sent - 1, "Жадность теряет одного");
        Check(sloth != wrath && wrath != greed && sloth != greed,
            "три исхода различимы");

        // Края: пустой отряд и отряд из одного не должны ломать эпилог.
        Check(Homecoming.Returned(SinType.Greed, 0) == 0, "никого не отправляли — никто не вернулся");
        Check(Homecoming.Returned(SinType.Sloth, 1) == 1, "ушёл один — он и вернулся");

        // Догадка Каргана на сцене 3 обещает то же, что эпилог вернёт
        // на сцене 8: обе растут из греха командира. Разные наборы грехов
        // означали бы, что доля 3 обещает исход, которого не будет.
        foreach (SinType sin in Enum.GetValues(typeof(SinType)))
        {
            string guess = Homecoming.Guess(sin);
            Check(!string.IsNullOrEmpty(guess), $"{sin}: догадка есть");
            Check(!guess.Any(char.IsDigit), $"{sin}: догадка без цифр");
        }

        // Догадка обязана отличать командиров друг от друга — иначе
        // выбор старшего не слышен в тот же вечер, когда он сделан.
        var guesses = new HashSet<string>();
        foreach (SinType sin in new[] { SinType.Sloth, SinType.Wrath, SinType.Greed })
            guesses.Add(Homecoming.Guess(sin));
        Check(guesses.Count == 3, "три канонных греха гадают по-разному");

        Console.WriteLine($"  из пятерых вернутся: Уныние {sloth}, Гнев {wrath}, "
                        + $"Жадность {greed}, Гордыня {Homecoming.Returned(SinType.Pride, sent)}, "
                        + $"Зависть {Homecoming.Returned(SinType.Envy, sent)}");
        Console.WriteLine(bad == 0 ? "  все проверки прошли" : $"  ПРОВАЛОВ: {bad}");
    }

    /// <summary>
    /// Куда ложится взгляд игрока и что считается «подойти к столу».
    ///
    /// Тихая ошибка здесь стоила бы всей сцены 2: совет, открывшийся сам
    /// на первом кадре, выглядит точно так же, как совет, к которому
    /// игрок подошёл, — и разницу видно только по тому, что игрок ничего
    /// не делал.
    /// </summary>
    static void CampFocusCheck()
    {
        Console.WriteLine("\n=== ЛАГЕРЬ: куда смотрит игрок ===");

        int bad = 0;
        void Check(bool ok, string what)
        {
            if (!ok) { bad++; Console.WriteLine($"  ПРОВАЛ: {what}"); }
        }

        // Взгляд ровно вниз с высоты 4 падает под ноги.
        Check(CampFocus.TryGroundPoint(new Vector3(2f, 4f, -3f),
                new Vector3(0f, -1f, 0f), 0f, out var under), "вниз: точка есть");
        Check(MathF.Abs(under.x - 2f) < 0.01f && MathF.Abs(under.z + 3f) < 0.01f,
            "вниз: падает под ноги");

        // Горизонт не пересекает землю нигде. Наивная формула вернула бы
        // сюда ноль и решила бы, что игрок стоит в начале координат —
        // то есть у костра, то есть почти у стола.
        Check(!CampFocus.TryGroundPoint(new Vector3(0f, 4f, 0f),
                new Vector3(0f, 0f, 1f), 0f, out _), "горизонт: точки нет");
        Check(!CampFocus.TryGroundPoint(new Vector3(0f, 4f, 0f),
                new Vector3(0f, 1f, 1f), 0f, out _), "вверх: точки нет");
        Check(!CampFocus.TryGroundPoint(new Vector3(0f, 0f, 0f),
                new Vector3(0f, -1f, 1f), 0f, out _), "камера на земле: точки нет");
        Check(!CampFocus.TryGroundPoint(new Vector3(0f, -2f, 0f),
                new Vector3(0f, -1f, 1f), 0f, out _), "камера под землёй: точки нет");

        // Наклон 45 с высоты 4: точка ровно в четырёх метрах впереди.
        Check(CampFocus.TryGroundPoint(new Vector3(0f, 4f, 0f),
                new Vector3(0f, -1f, 1f), 0f, out var slant), "наклон: точка есть");
        Check(MathF.Abs(slant.z - 4f) < 0.01f, "наклон 45 с высоты 4 даёт 4 вперёд");

        // Высота в «подойти» не участвует: подняться над столом — не то же
        // самое, что подойти к нему.
        Check(MathF.Abs(CampFocus.GroundDistance(
                new Vector3(0f, 100f, 0f), new Vector3(3f, 0f, 4f)) - 5f) < 0.01f,
            "расстояние считается по земле");

        // Радиус ноль и меньше никого не пускает — иначе выключённая
        // проверка выглядела бы как пройденная.
        Check(!CampFocus.Reached(new Vector3(0f, 4f, 0f), new Vector3(0f, -1f, 0f),
                Vector3.zero, 0f), "нулевой радиус не срабатывает");

        // Настоящая постановка лагеря: открывающий кадр не должен
        // дотягиваться до шара. Это те же числа, что стоят в сборщике сцен.
        var eye = new Vector3(0f, 3.5f, -12.5f);
        var forward = new Vector3(0f, -MathF.Sin(13f * MathF.PI / 180f),
                                      MathF.Cos(13f * MathF.PI / 180f));
        var ball = new Vector3(3.0f, 1.22f, 2.2f);

        Check(!CampFocus.Reached(eye, forward, ball, CampFocus.TableReach),
            "открывающий кадр НЕ дотягивается до стола");

        CampFocus.TryGroundPoint(eye, forward, ball.y, out var opening);
        float away = CampFocus.GroundDistance(opening, ball);
        Check(away > CampFocus.TableReach + 1.5f, "запас до стола больше полутора метров");

        // А дойти можно: сместим камеру туда, куда ведёт WASD.
        Check(CampFocus.Reached(eye + new Vector3(3.0f, 0f, 4.8f), forward,
                ball, CampFocus.TableReach), "подойдя, игрок стол достаёт");

        Console.WriteLine($"  от открывающего кадра до шара {away:F2} м "
                        + $"при радиусе {CampFocus.TableReach:F1}");
        Console.WriteLine(bad == 0 ? "  все проверки прошли" : $"  ПРОВАЛОВ: {bad}");
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

        // Секции перебора весов правят cfg на месте и не возвращают его
        // обратно — всё, что идёт после них, считалось бы на объедках
        // последнего варианта. Снимок берём здесь, восстанавливаем перед
        // замерами, которые обязаны видеть настоящий конфиг.
        var snapshot = (
            cfg.MaxVoice, cfg.HesitationShare,
            cfg.FearFleeLowHpBonus, cfg.FearFleeDangerMultiplier,
            cfg.FearFleeSurroundedBonus, cfg.LoyaltyObeySinMultiplier);

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
        Rumours();
        (cfg.MaxVoice, cfg.HesitationShare,
         cfg.FearFleeLowHpBonus, cfg.FearFleeDangerMultiplier,
         cfg.FearFleeSurroundedBonus, cfg.LoyaltyObeySinMultiplier) = snapshot;

        LeadershipCheck();
        HomecomingCheck();
        CampFocusCheck();
        Missions(cfg);
        Saturation(cfg);
        CapComparison(Math.Min(n, 50000), cfg);

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
