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
using System.Globalization;
using System.Text.RegularExpressions;
using System.Reflection;
using System.IO;
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
        public ActionType Runner;
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
            Runner = sorted.Count > 1 ? sorted[1].Key : best.Key,
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

    /// <summary>
    /// Лестница прозрачности: кому что позволено видеть.
    ///
    /// Главное здесь — что четвёртая ступень заперта, а не выключена.
    /// Выключенное однажды включают по ошибке, и тогда игрок видит очки
    /// и веса — то самое, чего правило «игрок не видит цифр» не допускает
    /// нигде и никогда.
    /// </summary>
    static void TransparencyCheck()
    {
        Console.WriteLine("\n=== ПРОЗРАЧНОСТЬ: кому что видно ===");

        int bad = 0;
        void Check(bool ok, string what)
        {
            if (!ok) { bad++; Console.WriteLine($"  ПРОВАЛ: {what}"); }
        }

        Transparency.Reset();

        // Игрок, ничего не настраивавший, получает журнал: игра, которая
        // продаёт понятный отказ, не имеет права начинаться с непонятного.
        Check(Transparency.Level == Clarity.Log, "по умолчанию — с журналом");
        Check(Transparency.Shows(Clarity.Icons), "значки видны");
        Check(Transparency.Shows(Clarity.Tooltips), "подсказки видны");
        Check(Transparency.Shows(Clarity.Log), "журнал виден");
        Check(!Transparency.Shows(Clarity.Trace), "трассировка не видна");

        // Замок: игрок не может добраться до цифр никакими настройками.
        Check(Transparency.Set(Clarity.Trace) == Clarity.Log,
            "просьба о трассировке зажата до журнала");
        Check(!Transparency.Shows(Clarity.Trace), "и после просьбы не видна");

        // Ступень ниже гасит то, что выше, и не гасит то, что ниже.
        Transparency.Set(Clarity.Icons);
        Check(Transparency.Shows(Clarity.Icons), "на первой значки видны");
        Check(!Transparency.Shows(Clarity.Tooltips), "на первой подсказок нет");
        Check(!Transparency.Shows(Clarity.Log), "на первой журнала нет");

        Transparency.Set(Clarity.Silent);
        Check(!Transparency.Shows(Clarity.Icons), "молча — значит молча");
        Check(Transparency.Set((Clarity)(-5)) == Clarity.Silent,
            "ступень ниже нуля не проваливается");

        // Разработчику открыто всё.
        Transparency.SetDeveloper(true);
        Check(Transparency.Set(Clarity.Trace) == Clarity.Trace,
            "разработчик доходит до трассировки");
        Check(Transparency.Shows(Clarity.Trace), "и видит её");

        // Заперев замок обратно, ступень обязана опуститься сама, иначе
        // замок не запирает ничего.
        Transparency.SetDeveloper(false);
        Check(Transparency.Level <= Clarity.Log, "замок опустил ступень");
        Check(!Transparency.Shows(Clarity.Trace), "и трассировки больше нет");

        // Названия ступеней — тоже текст для игрока: без цифр.
        foreach (Clarity c in Enum.GetValues(typeof(Clarity)))
        {
            string name = Transparency.Describe(c);
            Check(!string.IsNullOrEmpty(name), $"{c}: название есть");
            Check(!name.Any(char.IsDigit), $"{c}: название без цифр");
        }

        Transparency.Reset();
        Console.WriteLine(bad == 0 ? "  все проверки прошли" : $"  ПРОВАЛОВ: {bad}");
    }

    /// <summary>
    /// Имена на колышках. Пять пустых палаток — вся предыстория отряда,
    /// и держится она на том, что имена есть, различимы и не пусты.
    /// </summary>
    static void FallenCheck()
    {
        Console.WriteLine("\n=== ПАВШИЕ: имена на колышках ===");

        int bad = 0;
        void Check(bool ok, string what)
        {
            if (!ok) { bad++; Console.WriteLine($"  ПРОВАЛ: {what}"); }
        }

        const int mourning = 5;   // столько пустых палаток ставит сборщик

        Check(Fallen.Count >= mourning, "имён хватает на все пустые палатки");
        Check(Fallen.Names[0] == "Кир Бессонный", "Кир Бессонный стоит первым");

        var seen = new HashSet<string>();
        foreach (var name in Fallen.Names)
        {
            Check(!string.IsNullOrWhiteSpace(name), "имя не пустое");
            Check(!name.Any(char.IsDigit), $"«{name}» без цифр");
            Check(seen.Add(name), $"«{name}» не повторяется");
        }

        // Колышки просят имена по кругу и обязаны получить их все,
        // ни одного пустого: пустой колышек в сцене никто не заметит.
        var used = new HashSet<string>();
        for (int i = 0; i < mourning; i++)
        {
            string n = Fallen.NameFor(i);
            Check(!string.IsNullOrEmpty(n), $"колышек {i} получил имя");
            used.Add(n);
        }
        Check(used.Count == mourning, "пять колышков — пять разных имён");

        // Край: отрицательный и большой индекс не должны падать.
        Check(!string.IsNullOrEmpty(Fallen.NameFor(-1)), "отрицательный индекс даёт имя");
        Check(!string.IsNullOrEmpty(Fallen.NameFor(999)), "большой индекс даёт имя");

        Console.WriteLine($"  павших названо: {Fallen.Count}");
        Console.WriteLine(bad == 0 ? "  все проверки прошли" : $"  ПРОВАЛОВ: {bad}");
    }

    /// <summary>
    /// Распад души во времени. Вторая ставка боя помимо победы — успеть.
    ///
    /// Правила не было вовсе, и потому не работало всё остальное:
    /// множители спектров, память и перки расписаны подробно, а качество
    /// стояло на Shock до самого конца.
    /// </summary>
    static void SoulDecayCheck()
    {
        Console.WriteLine("\n=== ДУША: цена промедления ===");

        int bad = 0;
        void Check(bool ok, string what)
        {
            if (!ok) { bad++; Console.WriteLine($"  ПРОВАЛ: {what}"); }
        }

        const float fade = 60f;

        Check(SoulDecay.QualityAt(fade, fade) == SoulQuality.Shock,
            "сразу после смерти — Shock");
        Check(SoulDecay.QualityAt(fade * 0.5f, fade) == SoulQuality.Acceptance,
            "на половине — Acceptance");
        Check(SoulDecay.QualityAt(fade * 0.2f, fade) == SoulQuality.Fading,
            "к концу — Fading");
        Check(SoulDecay.QualityAt(fade * 0.05f, fade) == SoulQuality.Dissolved,
            "перед самым угасанием — Dissolved");
        Check(SoulDecay.QualityAt(0f, fade) == SoulQuality.Dissolved,
            "угасшая — Dissolved");

        // Ноль в знаменателе — событие, а не Shock: душа, которой некуда
        // угасать, не должна выглядеть свежайшей.
        Check(SoulDecay.QualityAt(10f, 0f) == SoulQuality.Dissolved,
            "нулевое время угасания не даёт свежую душу");

        // Порядок обязан быть монотонным: чем позже пришёл, тем хуже.
        var seen = new List<SoulQuality>();
        for (int i = 100; i >= 0; i -= 5)
            seen.Add(SoulDecay.QualityAt(fade * i / 100f, fade));

        for (int i = 1; i < seen.Count; i++)
            Check((int)seen[i] >= (int)seen[i - 1], "качество только падает");

        // Промедление обязано стоить: множители и память различимы.
        Check(SoulDecay.SpectrumFactor(SoulQuality.Shock)
            > SoulDecay.SpectrumFactor(SoulQuality.Dissolved),
            "распавшаяся душа тусклее свежей");
        Check(SoulDecay.KeepsMemory(SoulQuality.Shock), "свежая помнит себя");
        Check(!SoulDecay.KeepsMemory(SoulQuality.Fading), "гаснущая уже не помнит");
        Check(!SoulDecay.KeepsPerks(SoulQuality.Dissolved), "распавшаяся без перков");

        // Слова для игрока: без цифр.
        foreach (SoulQuality q in Enum.GetValues(typeof(SoulQuality)))
        {
            string d = SoulDecay.Describe(q);
            Check(!string.IsNullOrEmpty(d), $"{q}: описание есть");
            Check(!d.Any(char.IsDigit), $"{q}: описание без цифр");
        }

        Console.WriteLine("  из шестидесяти секунд: свежая до 24-й, "
                        + "помнит себя до 42-й, гаснет до 54-й, дальше шелуха");
        Console.WriteLine(bad == 0 ? "  все проверки прошли" : $"  ПРОВАЛОВ: {bad}");
    }

    /// <summary>
    /// Вылазка, которой игрок не видит: тот самый отряд, что уходит
    /// на сцене 2 и возвращается на сцене 8.
    ///
    /// Вопрос ровно один и он про правило пролога (§2): «ни одна
    /// постановочная сцена не имеет права показать воина, делающего то,
    /// чего движок AOS не мог бы решить сам». Исход вылазки сейчас берут
    /// из таблицы Homecoming — то есть из руки сценариста. Здесь мы
    /// спрашиваем движок: даёт ли он сам по себе разные исходы для разных
    /// грехов командира?
    ///
    /// Если даёт — таблицу надо выбрасывать, и эпилог считать боем.
    /// Если не даёт — таблица остаётся, но тогда это осознанное
    /// исключение из правила, а не недосмотр.
    /// </summary>
    static void ExpeditionCheck(AOSConfig cfg)
    {
        Console.WriteLine("\n=== ВЫЛАЗКА: решает ли исход грех командира ===");

        var modules = Modules();
        const int sent = 5;
        const int runs = 400;

        // Три расклада, а не один. Почти нулевая разница на лёгком враге
        // ничего не доказывает: если выживают все, различать нечего,
        // и мы измерили бы не движок, а лёгкость боя.
        foreach (var (foeCount, foeAttack, label) in new[]
                 {
                     (3, 4f, "враг слабее"),
                     (5, 6f, "вровень"),
                     (7, 7f, "враг сильнее"),
                 })
            Expedition(cfg, modules, sent, runs, foeCount, foeAttack, label);
    }

    static void Expedition(AOSConfig cfg, List<IPersonalityModule> modules,
        int sent, int runs, int foeCount, float foeAttack, string label)
    {
        Console.WriteLine($"\n  --- {label}: {sent} против {foeCount} ---");
        Console.WriteLine($"  {"грех",-10} {"стратегия",-16} {"вернулось",10} {"разброс",9}");

        var table = new Dictionary<SinType, double>();

        foreach (SinType sin in new[] { SinType.Sloth, SinType.Wrath, SinType.Greed,
                                        SinType.Pride, SinType.Envy })
        {
            var strategy = SquadOrders.FromSin(sin);
            var counts = new List<int>();

            for (int b = 0; b < runs; b++)
            {
                // Зерно от номера прогона, а не от часов: одинаковый вход
                // обязан давать одинаковый выход.
                var r = new Random(1000 + b);

                var squad = new List<Warrior>();
                var foes = new List<Warrior>();

                for (int i = 0; i < sent; i++)
                {
                    var soul = i == 0
                        ? new SoulData("Старший", sin, MoralType.Neutral, 1, 70f)
                        : MakeSoul(r, "Рядовой" + i);

                    squad.Add(new Warrior
                    {
                        Soul = soul,
                        Attack = 5f + (float)r.NextDouble() * 3f,
                        Loyalty = 50f + (float)r.NextDouble() * 40f,
                        Team = Team.Player,
                        IsCommander = i == 0,
                        Relationships = new RelationshipSystem(),
                    });
                }

                for (int i = 0; i < foeCount; i++)
                    foes.Add(new Warrior
                    {
                        Soul = MakeSoul(r, "Чужой" + i),
                        Attack = foeAttack + (float)r.NextDouble() * 3f,
                        Loyalty = 50f,
                        Team = Team.Enemy,
                        Relationships = new RelationshipSystem(),
                    });

                int turns = 0;
                while (turns < 20 && squad.Exists(w => !w.IsDead)
                                  && foes.Exists(e => !e.IsDead))
                {
                    Fight(modules, squad, foes, cfg, strategy);
                    Fight(modules, foes, squad, cfg, SquadStrategy.Balanced);
                    turns++;
                }

                counts.Add(squad.Count(w => !w.IsDead));
            }

            double avg = counts.Average();
            double spread = Math.Sqrt(counts.Average(c => (c - avg) * (c - avg)));
            table[sin] = avg;

            Console.WriteLine($"  {sin,-10} {SquadOrders.Name(strategy),-16} "
                            + $"{avg,10:F2} {spread,9:F2}");
        }

        double lo = table.Values.Min(), hi = table.Values.Max();
        Console.WriteLine($"  разница между лучшим и худшим грехом: {hi - lo:F2} из {sent}"
                        + (hi - lo >= 1.0 ? "  ← различает" : "  ← почти не различает"));
    }

    /// <summary>Один ход одной стороны.</summary>
    static void Fight(List<IPersonalityModule> modules, List<Warrior> side,
        List<Warrior> other, AOSConfig cfg, SquadStrategy strategy)
    {
        foreach (var w in side.Where(x => !x.IsDead).ToList())
        {
            var ctx = AutoBattleContext.Create(w, side, other);
            var o = Vote(modules, w, ctx, cfg, strategy);

            if (o.Action == ActionType.Attack)
            {
                var t = other.Where(x => !x.IsDead).OrderBy(x => x.HP).FirstOrDefault();
                t?.TakeDamage(w.Attack);
            }
            else if (o.Action == ActionType.SaveAlly)
            {
                var a = side.Where(x => !x.IsDead && x != w && x.HP < x.MaxHP * 0.5f)
                            .OrderBy(x => x.HP).FirstOrDefault();
                a?.Heal(5f);
            }
        }
    }

    /// <summary>
    /// Лагерь под приказом отходить: кто послушается и почему.
    ///
    /// Проверяются две вещи, которые не проверялись никогда.
    ///
    /// <b>Первая — третье требование к демо</b> (00-GDD.md §8): «отказ
    /// можно было предотвратить, и игрок это видит». Рычаг заявлен как
    /// важнейший, а работает ли он — не измерялось ни разу. Если уплата
    /// долга почти не меняет вероятность отказа, то рычаг декоративный,
    /// и демо обещает игроку власть, которой у него нет.
    ///
    /// <b>Вторая — существует ли главный скриншот</b>: один приказ,
    /// несколько душ, у каждой свой поступок и своя причина. Если
    /// объяснения у всех сойдутся в одно, показывать нечего.
    ///
    /// Души взяты настоящие, из PrologueCampSpawner.
    /// </summary>
    static void CampUnderOrderCheck(AOSConfig cfg)
    {
        Console.WriteLine("\n=== ЛАГЕРЬ ПОД ПРИКАЗОМ «ОТХОДИТЬ» ===");

        var modules = Modules();
        const int runs = 600;

        // Состав как в PrologueCampSpawner: имя, грех, мораль,
        // интенсивность, верность, долг.
        var camp = new (string Name, SinType Sin, MoralType Moral,
                        float Intensity, float Loyalty, int Unpaid)[]
        {
            ("Карган",  SinType.Pride,    MoralType.Neutral, 90f, 75f, 0),
            ("Вейн",    SinType.Sloth,    MoralType.Pious,   40f, 90f, 0),
            ("Марга",   SinType.Greed,    MoralType.Vicious, 65f, 70f, 3),
            ("Хальд",   SinType.Wrath,    MoralType.Pious,   35f, 95f, 0),
            ("Хорь",    SinType.Envy,     MoralType.Vicious, 45f, 65f, 0),
            ("Ю",       SinType.Gluttony, MoralType.Neutral, 55f, 80f, 0),
            ("Лиска",   SinType.Lust,     MoralType.Neutral, 30f, 85f, 0),
            ("Гурт",    SinType.Sloth,    MoralType.Vicious, 20f, 85f, 0),
            ("Ждан",    SinType.Pride,    MoralType.Neutral, 30f, 80f, 0),
        };

        // Три положения. Одно ничего не доказало бы: в голом поле
        // грехам не за что зацепиться, и разница могла бы отсутствовать
        // не потому, что её нет, а потому, что не за что хотеть.
        foreach (var (loot, ally, label) in new[]
                 {
                     (0, false, "голое поле: ни добычи, ни раненых"),
                     (2, false, "рядом добыча"),
                     (2, true,  "добыча и раненый свой"),
                 })
        {
            Console.WriteLine($"\n  --- {label} ---");
            Console.WriteLine($"  {"кто",-8} {"грех",-9} {"отказов",8}  причина отказа");

            var reasons = new HashSet<string>();

            foreach (var m in camp)
            {
                var (rate, reason) = OrderRun(modules, cfg, m.Sin, m.Moral,
                    m.Intensity, m.Loyalty, m.Unpaid, runs, loot, ally);

                if (rate > 0.01) reasons.Add(reason);
                Console.WriteLine($"  {m.Name,-8} {m.Sin,-9} {rate * 100,7:F1}%  {reason}");
            }

            Console.WriteLine($"  разных причин: {reasons.Count}"
                + (reasons.Count >= 4 ? "  ← хватает на скриншот" : "  ← сливаются"));
        }

        ObedienceOrPanic(modules, cfg);

        // --- работает ли рычаг ---
        Console.WriteLine("\n  --- Предотвратим ли отказ? Долг Марги ---");
        Console.WriteLine($"  {"долг",-6} {"отказов",8}  причина");

        // В голом поле: там, где без долга он послушался бы. В богатом
        // положении он отказывается и так, и рычага не видно — не потому,
        // что его нет, а потому, что отказ уже насыщен.
        double paid = 0, owed = 0;
        for (int unpaid = 0; unpaid <= 3; unpaid++)
        {
            var (rate, reason) = OrderRun(modules, cfg, SinType.Greed,
                MoralType.Vicious, 65f, 70f, unpaid, runs, loot: 0, allyInDanger: false);

            if (unpaid == 0) paid = rate;
            if (unpaid == 3) owed = rate;

            Console.WriteLine($"  {unpaid,-6} {rate * 100,7:F1}%  {reason}");
        }

        double lever = owed - paid;
        Console.WriteLine($"\n  уплата долга меняет отказ на {lever * 100:F1} процентных пункта");
        Console.WriteLine(lever >= 0.15
            ? "  ВЫВОД: рычаг настоящий — заплатив, игрок правда меняет исход."
            : "  ВЫВОД: рычаг слаб. Демо обещает власть, которой у игрока нет.");
    }

    /// <summary>
    /// Чем на самом деле кончается приказ отходить: какое действие
    /// выбрано и какой модуль его протолкнул.
    ///
    /// Нужно, чтобы отличить подчинение от совпадения. Приказ «отходить»
    /// исполняется действием Flee — тем же, которое выбирает Страх,
    /// когда ему просто страшно. Значит по одному «послушался» нельзя
    /// сказать, послушался он или сбежал.
    /// </summary>
    static void ObedienceOrPanic(List<IPersonalityModule> modules, AOSConfig cfg)
    {
        Console.WriteLine("\n  --- подчинение или совпадение? ---");
        Console.WriteLine($"  {"кто",-8} {"грех",-9} {"действие",-12} {"громче всех",-10} доля");

        var camp = new (string Name, SinType Sin, MoralType Moral, float I, float L, int U)[]
        {
            ("Карган", SinType.Pride,    MoralType.Neutral, 90f, 75f, 0),
            ("Вейн",   SinType.Sloth,    MoralType.Pious,   40f, 90f, 0),
            ("Марга",  SinType.Greed,    MoralType.Vicious, 65f, 70f, 3),
            ("Хальд",  SinType.Wrath,    MoralType.Pious,   35f, 95f, 0),
            ("Хорь",   SinType.Envy,     MoralType.Vicious, 45f, 65f, 0),
        };

        foreach (var m in camp)
        {
            var actions = new Dictionary<ActionType, int>();
            var mods = new Dictionary<string, int>();

            for (int i = 0; i < 600; i++)
            {
                var r = new Random(7000 + i);
                var w = new Warrior
                {
                    Soul = new SoulData("Воин", m.Sin, m.Moral, 1, m.I),
                    Attack = 5f, Loyalty = m.L, Team = Team.Player,
                    Relationships = new RelationshipSystem(),
                };
                var ctx = new DecisionContext
                {
                    CurrentHP = 12f + (float)r.NextDouble() * 10f,
                    MaxHP = 30f,
                    NearbyEnemies = 1 + r.Next(3),
                    NearbyLoot = 0,
                    Fatigue = (float)r.NextDouble() * 0.6f,
                    UnpaidMissions = m.U,
                    RelationshipWithCommander = m.L,
                    HasCommand = true,
                    CommandType = "FallBack",
                    CommandIsFallBack = true,
                };

                var d = Vote(modules, w, ctx, cfg, SquadStrategy.Balanced);
                actions[d.Action] = actions.TryGetValue(d.Action, out var a) ? a + 1 : 1;
                string mod = string.IsNullOrEmpty(d.Module) ? "—" : d.Module;
                mods[mod] = mods.TryGetValue(mod, out var c) ? c + 1 : 1;
            }

            var topAction = ActionType.Idle; int ba = 0;
            foreach (var kv in actions) if (kv.Value > ba) { ba = kv.Value; topAction = kv.Key; }

            var topMod = "—"; int bm = 0;
            foreach (var kv in mods) if (kv.Value > bm) { bm = kv.Value; topMod = kv.Key; }

            Console.WriteLine($"  {m.Name,-8} {m.Sin,-9} {topAction,-12} {topMod,-10} "
                            + $"{ba * 100.0 / 600,5:F1}%");
        }

        // Кто и насколько громко голосует за само подчинение.
        Console.WriteLine("\n  --- голоса ЗА и ПРОТИВ подчинения ---");
        Console.WriteLine($"  {"грех",-10} {"верность",9} {"голос Loyalty",14} "
                        + $"{"против него",12}  перевесит?");

        foreach (SinType sin in new[] { SinType.Pride, SinType.Wrath, SinType.Sloth,
                                        SinType.Greed, SinType.Envy, SinType.Lust })
        foreach (float loyalty in new[] { 30f, 65f, 95f })
        {
            var w = new Warrior
            {
                Soul = new SoulData("Воин", sin, MoralType.Neutral, 1, 80f),
                Attack = 5f, Loyalty = loyalty, Team = Team.Player,
                Relationships = new RelationshipSystem(),
            };
            var ctx = new DecisionContext
            {
                CurrentHP = 20f, MaxHP = 30f, NearbyEnemies = 2, NearbyLoot = 0,
                UnpaidMissions = 0, RelationshipWithCommander = loyalty,
                HasCommand = true, CommandType = "FallBack", CommandIsFallBack = true,
            };

            float pro = 0f, con = 0f;
            foreach (var mod in modules)
            {
                float v = mod.Evaluate(Soul.FromWarrior(w), ctx, ActionType.ObeyCommand);
                v = Math.Clamp(v, -cfg.MaxVoice, cfg.MaxVoice);
                if (v > 0) pro += v; else con += v;
            }

            if (loyalty == 65f)
                Console.WriteLine($"  {sin,-10} {loyalty,9:F0} {pro,14:F1} {con,12:F1}"
                                + $"  {(pro + con > 0 ? "подчинится" : "откажется")}");
        }

        LoyaltySweep(modules, cfg);
    }

    /// <summary>
    /// Что будет, если голос за подчинение перестанет упираться в потолок.
    ///
    /// Множитель 2.5 при потолке 80 означает, что верность выше 32 не значит
    /// ничего: и преданный, и почти чужой звучат одинаково громко. Мерим,
    /// с какого множителя верность снова начинает быть шкалой, а грех —
    /// иметь шанс.
    /// </summary>
    static void LoyaltySweep(List<IPersonalityModule> modules, AOSConfig cfg)
    {
        Console.WriteLine("\n  --- цена приказа: сцеплен ли воин ближним боем ---");
        Console.WriteLine($"  {"положение",-22} {"голос за",9} {"против",9}  итог");

        foreach (var (engaged, label) in new[]
                 {
                     (false, "свободен"),
                     (true,  "сцеплен, приказ уводит"),
                 })
        {
            var w = new Warrior
            {
                Soul = new SoulData("Воин", SinType.Wrath, MoralType.Neutral, 1, 80f),
                Attack = 5f, Loyalty = 65f, Team = Team.Player,
                Relationships = new RelationshipSystem(),
            };
            var ctx = new DecisionContext
            {
                CurrentHP = 20f, MaxHP = 30f, NearbyEnemies = 2,
                RelationshipWithCommander = 65f,
                HasCommand = true, CommandType = "FallBack", CommandIsFallBack = true,
                IsEngaged = engaged, CommandLeavesFight = engaged,
            };

            float pro = 0f, con = 0f;
            foreach (var mod in modules)
            {
                float v = mod.Evaluate(Soul.FromWarrior(w), ctx, ActionType.ObeyCommand);
                v = Math.Clamp(v, -cfg.MaxVoice, cfg.MaxVoice);
                if (v > 0) pro += v; else con += v;
            }

            Console.WriteLine($"  {label,-22} {pro,9:F1} {con,9:F1}  {pro + con,6:F1}");
        }

        Console.WriteLine("\n  --- если опустить множитель верности (воин сцеплен) ---");
        Console.WriteLine($"  {"множ.",6} {"верн.30",9} {"верн.65",9} {"верн.95",9}"
                        + $" {"разброс",9}  отказов всего");

        float saved = cfg.LoyaltyObeySinMultiplier;

        foreach (float mult in new[] { 2.5f, 1.6f, 1.0f, 0.8f, 0.6f })
        {
            cfg.LoyaltyObeySinMultiplier = mult;

            var rates = new List<double>();
            double all = 0;

            foreach (float loyalty in new[] { 30f, 65f, 95f })
            {
                int refused = 0;
                const int runs = 600;

                for (int i = 0; i < runs; i++)
                {
                    var r = new Random(7000 + i);
                    var sin = (SinType)(i % 7);

                    var w = new Warrior
                    {
                        Soul = new SoulData("Воин", sin, MoralType.Neutral, 1,
                                            50f + (float)r.NextDouble() * 45f),
                        Attack = 5f, Loyalty = loyalty, Team = Team.Player,
                        Relationships = new RelationshipSystem(),
                    };
                    var ctx = new DecisionContext
                    {
                        CurrentHP = 12f + (float)r.NextDouble() * 14f,
                        MaxHP = 30f,
                        NearbyEnemies = 1 + r.Next(3),
                        NearbyLoot = r.Next(2),
                        Fatigue = (float)r.NextDouble() * 0.6f,
                        RelationshipWithCommander = loyalty,
                        AllyInDanger = r.Next(3) == 0,
                        HasCommand = true,
                        CommandType = "FallBack",
                        CommandIsFallBack = true,

                        // Приказ отходить имеет цену только для того, кто
                        // уже сцеплен: уйти — значит подставить спину.
                        // Без этого мерилась бы обстановка, в которой
                        // приказ бесплатен, а таких в сцене 5 не бывает.
                        IsEngaged = true,
                        CommandLeavesFight = true,
                    };

                    var d = Vote(modules, w, ctx, cfg, SquadStrategy.Balanced);
                    if (!ctx.SatisfiedBy(d.Action) && !d.Hesitated) refused++;
                }

                double rate = refused / (double)runs;
                rates.Add(rate);
                all += rate;
            }

            double spread = rates.Max() - rates.Min();
            Console.WriteLine($"  {mult,6:F1} {rates[0] * 100,8:F1}% {rates[1] * 100,8:F1}%"
                            + $" {rates[2] * 100,8:F1}% {spread * 100,8:F1}п.п."
                            + $"  {all / 3 * 100,6:F1}%");
        }

        cfg.LoyaltyObeySinMultiplier = saved;

        Console.WriteLine("\n  Разброс — это и есть «верность что-то значит».");
        Console.WriteLine("  При 2.5 он нулевой: преданный и чужой ведут себя одинаково.");

        // Немонотонность: откуда. Один грех, мелкий шаг по верности.
        Console.WriteLine("\n  --- откуда немонотонность: Гнев, шаг по верности ---");
        Console.WriteLine($"  {"верн.",6} {"голос за",9} {"против",8} {"итог",7} "
                        + $"{"отказов",8}  громче всех");

        foreach (float loyalty in new[] { 20f, 30f, 40f, 50f, 65f, 80f, 95f })
        {
            var w = new Warrior
            {
                Soul = new SoulData("Воин", SinType.Wrath, MoralType.Neutral, 1, 70f),
                Attack = 5f, Loyalty = loyalty, Team = Team.Player,
                Relationships = new RelationshipSystem(),
            };
            var probe = new DecisionContext
            {
                CurrentHP = 20f, MaxHP = 30f, NearbyEnemies = 2,
                RelationshipWithCommander = loyalty,
                HasCommand = true, CommandType = "FallBack", CommandIsFallBack = true,
                IsEngaged = true, CommandLeavesFight = true,
            };

            float pro = 0f, con = 0f;
            foreach (var mod in modules)
            {
                float v = mod.Evaluate(Soul.FromWarrior(w), probe, ActionType.ObeyCommand);
                v = Math.Clamp(v, -cfg.MaxVoice, cfg.MaxVoice);
                if (v > 0) pro += v; else con += v;
            }

            int refused = 0;
            var mods = new Dictionary<string, int>();

            for (int i = 0; i < 400; i++)
            {
                var r = new Random(7000 + i);
                var ctx = new DecisionContext
                {
                    CurrentHP = 12f + (float)r.NextDouble() * 14f,
                    MaxHP = 30f,
                    NearbyEnemies = 1 + r.Next(3),
                    NearbyLoot = r.Next(2),
                    Fatigue = (float)r.NextDouble() * 0.6f,
                    RelationshipWithCommander = loyalty,
                    AllyInDanger = r.Next(3) == 0,
                    HasCommand = true, CommandType = "FallBack", CommandIsFallBack = true,
                    IsEngaged = true, CommandLeavesFight = true,
                };
                var d = Vote(modules, w, ctx, cfg, SquadStrategy.Balanced);
                if (!ctx.SatisfiedBy(d.Action) && !d.Hesitated) refused++;

                string mm = string.IsNullOrEmpty(d.Module) ? "—" : d.Module;
                mods[mm] = mods.TryGetValue(mm, out var c) ? c + 1 : 1;
            }

            var top = "—"; int bm = 0;
            foreach (var kv in mods) if (kv.Value > bm) { bm = kv.Value; top = kv.Key; }

            Console.WriteLine($"  {loyalty,6:F0} {pro,9:F1} {con,8:F1} {pro + con,7:F1} "
                            + $"{refused * 100.0 / 400,7:F1}%  {top}");
        }
    }

    /// <summary>Одна душа под приказом отходить: доля отказов и типичная причина.</summary>
    static (double Rate, string Reason) OrderRun(List<IPersonalityModule> modules,
        AOSConfig cfg, SinType sin, MoralType moral, float intensity,
        float loyalty, int unpaid, int runs, int loot = 1, bool allyInDanger = true)
    {
        int refused = 0;
        var seen = new Dictionary<string, int>();

        for (int i = 0; i < runs; i++)
        {
            var r = new Random(7000 + i);

            var w = new Warrior
            {
                Soul = new SoulData("Воин", sin, moral, 1, intensity),
                Attack = 5f,
                Loyalty = loyalty,
                Team = Team.Player,
                Relationships = new RelationshipSystem(),
            };

            // Положение боя, в котором приказ отходить осмыслен: враг рядом,
            // здоровье потрёпано, кто-то из своих ранен.
            var ctx = new DecisionContext
            {
                CurrentHP = 12f + (float)r.NextDouble() * 10f,
                MaxHP = 30f,
                NearbyEnemies = 1 + r.Next(3),
                NearbyLoot = loot,
                Fatigue = (float)r.NextDouble() * 0.6f,
                UnpaidMissions = unpaid,
                RelationshipWithCommander = loyalty,
                AllyInDanger = allyInDanger && r.Next(2) == 0,
                Surrounded = r.Next(4) == 0,
                HasCommand = true,
                CommandType = "FallBack",
                CommandIsFallBack = true,
            };

            var d = Vote(modules, w, ctx, cfg, SquadStrategy.Balanced);

            bool obeyed = ctx.SatisfiedBy(d.Action);
            if (!obeyed) refused++;

            // Стендовый Outcome — не Decision движка: перекладываем поля,
            // чтобы объяснение считал настоящий PhraseGenerator, а не копия.
            var decision = new Decision
            {
                Action = d.Action,
                TopModule = d.Module,
                Gap = d.Gap,
                Confidence = d.Confidence,
                Hesitated = d.Hesitated,
                RefusedCommand = !obeyed && !d.Hesitated,

                // Без этих двух фраза колебания выходила «Драка и Драка»:
                // движок их заполняет, а стендовый мостик — забыл. Чуть
                // не объявили чужим багом свой.
                TopContender = d.Action,
                RunnerUp = d.Runner,
            };

            // Считаем причину только у отказов: игрока интересует, почему
            // не послушались, а не почему послушались. Смешав их, мы бы
            // показывали объяснение послушания при половине отказов.
            if (obeyed) continue;

            string phrase = PhraseGenerator.Explain(w, ctx, decision);
            if (!string.IsNullOrEmpty(phrase))
                seen[phrase] = seen.TryGetValue(phrase, out var c) ? c + 1 : 1;
        }

        string top = "—";
        int best = 0;
        foreach (var kv in seen)
            if (kv.Value > best || (kv.Value == best && string.CompareOrdinal(kv.Key, top) < 0))
            { best = kv.Value; top = kv.Key; }

        return (refused / (double)runs, top);
    }

    /// <summary>
    /// Влить в конфиг значения из Resources/AOSConfig.asset.
    ///
    /// Стенд создавал конфиг с нуля и брал значения по умолчанию из кода,
    /// а игра грузит ассет. Четыре поля расходились: Страх в игре втрое
    /// громче, голос за подчинение впятеро тише. То есть весь баланс,
    /// записанный в 12-BALANCE.md, измерялся на конфигурации, которая
    /// в игре не запускается ни разу.
    ///
    /// Читаем YAML грубо, парой регулярок: формат ассета простой,
    /// а тащить в стенд разбор Unity-сериализации незачем.
    /// </summary>
    static void ApplyAsset(AOSConfig cfg)
    {
        string path = Path.Combine("..", "..", "Assets", "Resources", "AOSConfig.asset");

        if (!File.Exists(path))
        {
            Console.WriteLine("[СТЕНД] Resources/AOSConfig.asset не найден — "
                            + "меряем на значениях по умолчанию из кода.");
            return;
        }

        var text = File.ReadAllText(path);
        var fields = typeof(AOSConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);

        int applied = 0, differs = 0;

        foreach (var f in fields)
        {
            var m = Regex.Match(text, @"^  " + Regex.Escape(f.Name) + @": (-?[\d.]+)$",
                                RegexOptions.Multiline);
            if (!m.Success) continue;

            if (f.FieldType == typeof(float))
            {
                float v = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (Math.Abs((float)f.GetValue(cfg) - v) > 1e-6f) differs++;
                f.SetValue(cfg, v);
                applied++;
            }
            else if (f.FieldType == typeof(int))
            {
                int v = (int)float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if ((int)f.GetValue(cfg) != v) differs++;
                f.SetValue(cfg, v);
                applied++;
            }
        }

        Console.WriteLine($"[СТЕНД] Взято из ассета полей: {applied}, "
                        + $"из них отличались от кода: {differs}.");
    }

    /// <summary>
    /// Подбор разумных чисел, а не идеального баланса.
    ///
    /// Уникальность воинов несут грех, память, верность и эмоции —
    /// значит важно не среднее, а <b>разброс</b>. Средняя доля отказов
    /// должна быть просто вменяемой; расходиться друг с другом воины
    /// обязаны сильно.
    ///
    /// Поэтому меряем сразу две величины на каждой настройке: сколько
    /// отказов вообще и насколько далеко расходятся девять настоящих душ
    /// лагеря. Настройка, где средняя приличная, а разброс схлопнулся, —
    /// хуже, чем чуть кривая средняя при широком разбросе.
    ///
    /// Крутим три числа Страха разом, одним множителем: в ассете они
    /// заданы согласованно (90/85/60), и растаскивать их порознь значит
    /// подбирать три числа вместо одного.
    /// </summary>
    static void FearSweep(AOSConfig cfg)
    {
        Console.WriteLine("\n=== ПОДБОР: сколько отказов и насколько разные ===");

        var modules = Modules();

        var camp = new (string Name, SinType Sin, MoralType Moral, float I, float L, int U)[]
        {
            ("Карган", SinType.Pride,    MoralType.Neutral, 90f, 75f, 0),
            ("Вейн",   SinType.Sloth,    MoralType.Pious,   40f, 90f, 0),
            ("Марга",  SinType.Greed,    MoralType.Vicious, 65f, 70f, 3),
            ("Хальд",  SinType.Wrath,    MoralType.Pious,   35f, 95f, 0),
            ("Хорь",   SinType.Envy,     MoralType.Vicious, 45f, 65f, 0),
            ("Ю",      SinType.Gluttony, MoralType.Neutral, 55f, 80f, 0),
            ("Лиска",  SinType.Lust,     MoralType.Neutral, 30f, 85f, 0),
            ("Гурт",   SinType.Sloth,    MoralType.Vicious, 20f, 85f, 0),
            ("Ждан",   SinType.Pride,    MoralType.Neutral, 30f, 80f, 0),
        };

        float d0 = cfg.FearFleeDangerMultiplier;
        float h0 = cfg.FearFleeLowHpBonus;
        float s0 = cfg.FearFleeSurroundedBonus;

        Console.WriteLine($"  {"множ.",6} {"опасн.",7} {"мало HP",8} {"окруж.",7}"
                        + $" {"средне",8} {"мин",6} {"макс",6} {"разброс",8}  различимых");

        foreach (float k in new[] { 1.0f, 0.8f, 0.6f, 0.45f, 0.35f, 0.25f })
        {
            cfg.FearFleeDangerMultiplier = d0 * k;
            cfg.FearFleeLowHpBonus = h0 * k;
            cfg.FearFleeSurroundedBonus = s0 * k;

            var rates = new List<double>();

            foreach (var m in camp)
            {
                var (rate, _) = OrderRun(modules, cfg, m.Sin, m.Moral,
                    m.I, m.L, m.U, 400, loot: 0, allyInDanger: false);
                rates.Add(rate);
            }

            double avg = rates.Average();
            double lo = rates.Min(), hi = rates.Max();

            // Различимых — сколько воинов попадают в разные десятки
            // процентов. Грубая мера того, отличит ли их игрок на глаз.
            var bands = new HashSet<int>(rates.Select(x => (int)(x * 10)));

            Console.WriteLine($"  {k,6:F2} {d0 * k,7:F0} {h0 * k,8:F0} {s0 * k,7:F0}"
                            + $" {avg * 100,7:F1}% {lo * 100,5:F0}% {hi * 100,5:F0}%"
                            + $" {(hi - lo) * 100,7:F0}п.п.  {bands.Count,2} из 9");
        }

        cfg.FearFleeDangerMultiplier = d0;
        cfg.FearFleeLowHpBonus = h0;
        cfg.FearFleeSurroundedBonus = s0;

        Console.WriteLine("\n  «Различимых» — сколько воинов попадают в разные десятки");
        Console.WriteLine("  процентов. Это и есть уникальность на глаз игрока.");

        // Страх не та ручка: уменьшая его, мы уменьшаем Flee, а Flee и есть
        // исполнение приказа отходить. Настоящая ручка — голос Верности.
        Console.WriteLine("\n  --- голос за подчинение (LoyaltyObeySinMultiplier) ---");
        Console.WriteLine($"  {"множ.",6} {"голос@75",9} {"средне",8} {"мин",6} {"макс",6}"
                        + $" {"разброс",8}  различимых  миссии");

        float saved = cfg.LoyaltyObeySinMultiplier;

        foreach (float mult in new[] { 0.5f, 0.8f, 1.1f, 1.4f, 1.7f, 2.0f })
        {
            cfg.LoyaltyObeySinMultiplier = mult;

            var rates = new List<double>();
            foreach (var m in camp)
            {
                var (rate, _) = OrderRun(modules, cfg, m.Sin, m.Moral,
                    m.I, m.L, m.U, 400, loot: 0, allyInDanger: false);
                rates.Add(rate);
            }

            double avg = rates.Average();
            double lo = rates.Min(), hi = rates.Max();
            var bands = new HashSet<int>(rates.Select(x => (int)(x * 10)));

            int missions = MissionsMatched(cfg);

            Console.WriteLine($"  {mult,6:F1} {Math.Min(75f * mult, cfg.MaxVoice),9:F1}"
                            + $" {avg * 100,7:F1}% {lo * 100,5:F0}% {hi * 100,5:F0}%"
                            + $" {(hi - lo) * 100,7:F0}п.п.  {bands.Count,2} из 9"
                            + $"    {missions}/15");
        }

        cfg.LoyaltyObeySinMultiplier = saved;
    }

    /// <summary>Сколько строк таблицы миссий сходится при этом конфиге.</summary>
    static int MissionsMatched(AOSConfig cfg)
    {
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

        var available = new[]
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
            var w = new Warrior { Soul = new SoulData("К", row.moral, 2, spectra), Loyalty = 50f };

            var ctx = new MissionContext
            {
                HasAltar = true, IsVillageIntact = true, HasInnocentVictims = true,
                RecentMemories = new List<MemoryRecord>(),
                CarriedItems = new List<InventoryItem>()
            };

            var scores = new Dictionary<MissionAction, float>();
            foreach (var a in available) scores[a] = 0f;

            var soul = Soul.FromWarrior(w);
            foreach (var m in modules)
            {
                if (!(m is IMissionModule mm)) continue;
                foreach (var a in available) scores[a] += mm.EvaluateMission(soul, ctx, a);
            }

            if (scores.OrderByDescending(kv => kv.Value).First().Key == row.want) hit++;
        }

        return hit;
    }

    /// <summary>
    /// Мораль отдельно от греха: тот же грех, та же сила, та же верность,
    /// меняется только мораль. Если разницы нет — мораль не участвует,
    /// и «воины уникальны» держится на одном грехе.
    /// </summary>
    static void MoralityCheck(AOSConfig cfg)
    {
        Console.WriteLine("\n=== МОРАЛЬ: различает ли она воинов сама по себе ===");

        var modules = Modules();

        Console.WriteLine($"  {"грех",-10} {"Vicious",9} {"Neutral",9} {"Pious",9}"
                        + $" {"разброс",9}");

        var spreads = new List<double>();

        foreach (SinType sin in new[] { SinType.Pride, SinType.Wrath, SinType.Greed,
                                        SinType.Sloth, SinType.Envy })
        {
            var r = new List<double>();

            foreach (MoralType moral in new[] { MoralType.Vicious, MoralType.Neutral,
                                                MoralType.Pious })
            {
                var (rate, _) = OrderRun(modules, cfg, sin, moral, 60f, 80f, 0, 400,
                                         loot: 0, allyInDanger: false);
                r.Add(rate);
            }

            double spread = r.Max() - r.Min();
            spreads.Add(spread);

            Console.WriteLine($"  {sin,-10} {r[0] * 100,8:F1}% {r[1] * 100,8:F1}%"
                            + $" {r[2] * 100,8:F1}% {spread * 100,8:F0}п.п.");
        }

        Console.WriteLine($"\n  средний разброс по морали: {spreads.Average() * 100:F0} п.п.");
        Console.WriteLine(spreads.Average() >= 0.15
            ? "  Мораль участвует: при одном грехе она меняет поведение заметно."
            : "  Мораль почти не участвует — уникальность держится на грехе.");
    }

    /// <summary>
    /// Чувствительность шкал: меняет ли воина разница в одну единицу.
    ///
    /// Замысел прямой: «в идеале разница в 1 единицу где угодно заметно
    /// меняет воина». Это проверяемо. Шагаем по шкале по единице и смотрим,
    /// как часто соседние значения дают разное решение.
    ///
    /// Меряем в одинаковых положениях — иначе мерили бы шум обстановки,
    /// а не чувствительность шкалы. Разное решение при одном и том же
    /// положении и разнице в единицу — это и есть «единица что-то значит».
    ///
    /// Мёртвая зона — участок, где ни один шаг ничего не меняет. Её видно
    /// сразу: там, где голос упёрся в потолок, шкала перестаёт быть шкалой.
    /// </summary>
    static void SensitivityCheck(AOSConfig cfg)
    {
        Console.WriteLine("\n=== ЧУВСТВИТЕЛЬНОСТЬ: значит ли одна единица ===");

        var modules = Modules();
        const int probes = 200;

        Console.WriteLine($"  {"шкала",-20} {"ход",-10} {"в среднем меняет",17} "
                        + $"{"лучший шаг",11}  мёртвая зона");

        foreach (var axis in new[] { "верность", "сила греха", "усталость", "здоровье" })
        {
            (int from, int to) = axis switch
            {
                "верность"   => (20, 100),
                "сила греха" => (20, 100),
                "усталость"  => (0, 100),
                _            => (3, 30),
            };

            double sum = 0;
            int steps = 0;
            double best = 0; int bestAt = from;
            int dead = 0, bestDead = 0, deadFrom = -1, curFrom = -1, deadTo = -1;

            ActionType[] previous = null;

            for (int v = from; v <= to; v++)
            {
                var now = new ActionType[probes];

                for (int i = 0; i < probes; i++)
                {
                    var spectra = new float[7];
                    spectra[(int)SinType.Greed] = 60f;

                    var w = new Warrior
                    {
                        Soul = new SoulData("Воин", MoralType.Neutral, 1, spectra),
                        Attack = 5f, Loyalty = 70f, Team = Team.Player,
                        Relationships = new RelationshipSystem(),
                    };

                    // Двести разных положений, но ОДНИ И ТЕ ЖЕ на обоих шагах:
                    // иначе мерили бы шум обстановки, а не чувствительность.
                    var ctx = new DecisionContext
                    {
                        CurrentHP = 8f + (i % 20),
                        MaxHP = 30f,
                        NearbyEnemies = 1 + (i % 4),
                        NearbyLoot = i % 3,
                        Fatigue = (i % 10) / 10f,
                        RelationshipWithCommander = 70f,
                        AllyInDanger = i % 3 == 0,
                        Surrounded = i % 7 == 0,
                        HasCommand = true, CommandType = "FallBack",
                        CommandIsFallBack = true,
                        IsEngaged = true, CommandLeavesFight = true,
                    };

                    switch (axis)
                    {
                        case "верность":
                            w.Loyalty = v; ctx.RelationshipWithCommander = v; break;
                        case "сила греха":
                            spectra[(int)SinType.Greed] = v;
                            w.Soul = new SoulData("Воин", MoralType.Neutral, 1, spectra);
                            break;
                        case "усталость":
                            ctx.Fatigue = v / 100f; ctx.IsExhausted = v > 70; break;
                        default:
                            ctx.CurrentHP = v; break;
                    }

                    now[i] = Vote(modules, w, ctx, cfg, SquadStrategy.Balanced).Action;
                }

                if (previous != null)
                {
                    int differ = 0;
                    for (int i = 0; i < probes; i++)
                        if (now[i] != previous[i]) differ++;

                    double share = differ / (double)probes;
                    sum += share; steps++;

                    if (share > best) { best = share; bestAt = v; }

                    if (differ == 0)
                    {
                        if (curFrom < 0) curFrom = v;
                        dead++;
                        if (dead > bestDead) { bestDead = dead; deadFrom = curFrom; deadTo = v; }
                    }
                    else { dead = 0; curFrom = -1; }
                }

                previous = now;
            }

            double avg = sum / Math.Max(steps, 1);
            string deadText = bestDead > 3
                ? $"{deadFrom}–{deadTo} ({bestDead} подряд)"
                : "нет";

            Console.WriteLine($"  {axis,-20} {from + "–" + to,-10} {avg * 100,16:F1}% "
                            + $"{best * 100,9:F1}% @{bestAt,-3}  {deadText}");
        }

        Console.WriteLine("\n  «В среднем меняет» — в скольких положениях из двухсот одна");
        Console.WriteLine("  единица шкалы даёт другое решение. Это и есть «единица значит».");
        Console.WriteLine("  Мёртвая зона — участок, где не меняет ни в одном.");
    }

    static void Main(string[] args)
    {
        Debug.Mute = true;
        int n = args.Length > 0 ? int.Parse(args[0]) : 200000;
        var cfg = ScriptableObject.CreateInstance<AOSConfig>();
        ApplyAsset(cfg);

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
        TransparencyCheck();
        FallenCheck();
        SoulDecayCheck();
        ExpeditionCheck(cfg);
        CampUnderOrderCheck(cfg);
        FearSweep(cfg);
        MoralityCheck(cfg);
        SensitivityCheck(cfg);
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
