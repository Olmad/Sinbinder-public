// Assets/Scripts/Tests/SelfCheck.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.Inventory;

namespace Sinbinder.Tests
{
    /// <summary>
    /// Самопроверка движка.
    ///
    /// Проверяет то, что можно проверить без сцены, без ассетов и без
    /// человека: спектры, перенос старых душ, распад, связывание
    /// с оболочкой, искушения, геометрию спины, правила голосования
    /// и правила текста.
    ///
    /// Зачем это здесь. Игра пишется вечерами, компилятор есть только
    /// у автора, а логика личности такая, что ошибку в ней не видно
    /// глазами: воин просто ведёт себя чуть иначе, и понять, баг это
    /// или характер, нельзя. Самопроверка отвечает на этот вопрос
    /// за секунду.
    ///
    /// Отдельно проверяется правило, которое иначе не проверяется
    /// ничем: игрок никогда не видит цифр. Это требование диздока,
    /// и оно ломается одной случайной интерполяцией в строке.
    ///
    /// Запуск: меню Sinbinder → Проверить движок, либо компонент
    /// SelfCheckRunner на любом объекте сцены.
    /// </summary>
    public static class SelfCheck
    {
        public class Report
        {
            public int Passed;
            public readonly List<string> Failures = new();
            public bool Ok => Failures.Count == 0;
            public int Total => Passed + Failures.Count;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine(Ok
                    ? $"ПРОВЕРКА ДВИЖКА: пройдено {Passed} из {Total}."
                    : $"ПРОВЕРКА ДВИЖКА: провалено {Failures.Count} из {Total}.");
                foreach (var f in Failures) sb.AppendLine("  ✗ " + f);
                return sb.ToString().TrimEnd();
            }
        }

        private static Report _report;
        private static readonly List<GameObject> _temp = new();

        public static Report RunAll()
        {
            _report = new Report();
            _temp.Clear();

            try
            {
                Spectra();
                Migration();
                Copying();
                Decay();
                Shells();
                Temptation();
                Geometry();
                Narration();
                Voting();
                Commanding();
                Epilogue();
                Approach();
                Ladder();
                Spoils();
                Saving();
                Bodies();
                Blows();
                Gear();
                Pocket();
                ReasonByContrast();
                ChestStore();
                LootToHands();
                CampSpots();
                CampTalkLines();
                PatrolAndRush();
                Titles();
                Pursuit();
                Sides();
                HunterGoals();
                Roster();
                Pausing();
                RaidPart();
                VoiceOfSinbinder();
                Fog();
                TextRules();
            }
            catch (Exception e)
            {
                Fail($"проверка сорвалась на исключении: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                foreach (var go in _temp)
                    if (go != null) UnityEngine.Object.DestroyImmediate(go);
                _temp.Clear();
            }

            return _report;
        }

        // ================= правки 24 сентября =================
        //
        // Одиннадцать правок одного дня по жалобам автора из живой игры
        // (14-HANDOFF §56–67). Каждая проверка ниже ловит ровно ту беду,
        // что была: верни старое поведение — и она провалится.
        //
        // Идёт в редакторе, не в игре: Awake у компонентов не зовётся,
        // поэтому проверяется то, что от него не зависит, — свойства,
        // прямые вызовы и закрытое через отражение.

        /// <summary>
        /// Одна правда о здоровье (§58, §60). У воина была своя полоса,
        /// которую бой не трогал: убитый оставался жив для души, раненый —
        /// цел, лечение уходило в пустоту.
        /// </summary>
        /// <summary>
        /// Удар и защита (§60.4, решение автора 24 сентября): от оболочки
        /// плюс от вещей в руках; защита гасит долю, а не вычитает.
        /// Выключено — бой прежний.
        /// </summary>
        private static void Blows()
        {
            bool was = CombatMath.Enabled;

            try
            {
                Near(CombatMath.Absorb(5f, 0f), 5f, "без защиты удар доходит целиком");
                Near(CombatMath.Absorb(8f, CombatMath.Scale), 4f, "защита, равная мере, гасит половину");
                Check(CombatMath.Absorb(5f, 4f) < CombatMath.Absorb(5f, 1f)
                      && CombatMath.Absorb(5f, 1000f) > 0f,
                      "крепче — меньше доходит, но удар не гаснет целиком");

                var bone = NewObject("Удар").AddComponent<Warrior>();
                bone.Initialize(new SoulData("Удар", SinType.Wrath, MoralType.Neutral, 5, 50f),
                    ShellType.Skeleton, new RelationshipSystem(null));
                var young = NewObject("Молодой").AddComponent<Warrior>();
                young.Initialize(new SoulData("Молодой", SinType.Wrath, MoralType.Neutral, 1, 50f),
                    ShellType.Skeleton, new RelationshipSystem(null));
                Near(bone.Attack, young.Attack, "уровень удара не прибавляет — уровней нет");
                Near(bone.Defense, young.Defense, "уровень защиты не прибавляет");

                float attack = bone.Attack, defense = bone.Defense;
                foreach (var item in Inventory.TrophyCatalog.Chest()) bone.Give(item);
                Check(bone.Attack > attack, "топор из сундука прибавляет удара тому, кто его несёт");
                Check(bone.Defense > defense, "кольчужный ворот прибавляет защиты");

                var body = bone.gameObject.AddComponent<Damageable>();
                CombatMath.Enabled = true;
                float before = body.HP;
                body.TakeDamage(10f, null);
                Near(before - body.HP, CombatMath.Absorb(10f, bone.Defense),
                     "тело гасит удар своей защитой — один раз, а не дважды");

                CombatMath.Enabled = false;
                before = body.HP;
                body.TakeDamage(10f, null);
                Near(before - body.HP, 10f, "выключено — удар доходит как прежде");
            }
            finally
            {
                CombatMath.Enabled = was;
            }
        }

        /// <summary>
        /// Обмен вещами (docs/34-GEAR.md): воин отвечает как душа — может
        /// не взять и не отдать; места, а не руки; всё словами.
        /// </summary>
        private static void Gear()
        {
            var sloth = MakeWarrior("Лень", SinType.Sloth, 70f);
            var wrath = MakeWarrior("Ярость", SinType.Wrath, 70f);
            var greed = MakeWarrior("Скупость", SinType.Greed, 80f);

            var axe = new InventoryItem("Топор", "", ItemType.Equipment, attack: 2f);
            var collar = new InventoryItem("Ворот", "", ItemType.Equipment, defense: 2f);
            var coin = new InventoryItem("Монета", "", ItemType.Artifact);

            Check(!SquadGear.WillTake(sloth, collar, out _), "унылый не берёт лишнего");
            Check(SquadGear.WillTake(wrath, axe, out string glad) && glad == "берёт охотно",
                  "гневный рад оружию");
            Check(!SquadGear.WillTake(wrath, collar, out _), "гневный не носит того, чем нельзя ударить");

            greed.Give(coin);
            Check(!SquadGear.WillGive(greed, coin, out _), "жадный не отдаёт ценного");
            Check(greed.Drop(coin) && greed.Carried.Count == 0, "выпустить из рук можно");

            // Места, а не руки (решение автора 24 сентября): одно место —
            // одна вещь, на занятое — замена, и ничто не складывается.
            var calm = MakeWarrior("Покой", SinType.Envy, 30f);
            float bare = calm.Attack;
            var first = new InventoryItem("Первый топор", "", ItemType.Equipment, attack: 2f);
            var second = new InventoryItem("Второй топор", "", ItemType.Equipment, attack: 2f);
            var third = new InventoryItem("Третий топор", "", ItemType.Equipment, attack: 4f);
            Check(calm.Give(first) && calm.Worn(GearSlot.Weapon) == first, "первое оружие — в руку");
            Check(calm.Give(second) && calm.Worn(GearSlot.Offhand) == second, "второе — во вторую руку");
            Near(calm.Attack, bare + 2f + 2f * CombatMath.OffhandShare, "вторая рука бьёт слабее главной");
            Check(!calm.Give(third), "третьему оружию места нет: три топора не бьют как три");
            Check(SquadGear.Place(calm, third, out _, out var weaker) && weaker == second,
                  "третье оружие сменяет слабейшее");
            Check(SquadGear.WillTake(calm, third, out string swap) && swap.Contains("взамен"),
                  "о замене сказано до передачи");

            var store = NewObject("Запасы").AddComponent<PlayerInventory>();
            store.AddItem(third);
            Check(SquadGear.Hand(calm, third, store, out _)
                  && calm.Worn(GearSlot.Weapon) == third && calm.Worn(GearSlot.Offhand) == first
                  && store.GetAllItems().Contains(second) && !store.GetAllItems().Contains(third),
                  "сменённое возвращается в запасы, лучшее — в главной руке");

            // Сравнение с тем, что на месте, — словами (разбор, п. 7).
            var blunt = new InventoryItem("Тупой нож", "", ItemType.Equipment, attack: 1f);
            Check(SquadGear.Compare(calm, blunt).StartsWith("бьёт слабее")
                  && !Regex.IsMatch(SquadGear.Compare(calm, blunt), "[0-9]"),
                  "слабое оружие названо слабее того, что сменит, — без чисел");

            var helm = new InventoryItem("Шлем", "", ItemType.Equipment, defense: 1f, slot: GearSlot.Head);
            var helm2 = new InventoryItem("Второй шлем", "", ItemType.Equipment, defense: 3f, slot: GearSlot.Head);
            Check(calm.Give(helm) && !calm.Give(helm2), "второй шлем поверх первого не надеть");
            Check(SquadGear.Place(calm, helm2, out _, out var was) && was == helm, "второй шлем — замена первого");

            var shield = new InventoryItem("Щит", "", ItemType.Equipment, defense: 2f, slot: GearSlot.Offhand);
            Check(SquadGear.Place(calm, shield, out var where, out var gone)
                  && where == GearSlot.Offhand && gone == first, "щит встаёт вместо второго оружия");
            Check(!calm.Give(new InventoryItem("Монеты", "", ItemType.Gold, 5)), "золото не надевают");

            // Кто своего не отдаёт, тот и не меняет. Унылому лишнего
            // не надо, а сменить одно на другое — не лишнее.
            var proud = MakeWarrior("Спесь", SinType.Pride, 80f);
            proud.Give(new InventoryItem("Свой меч", "", ItemType.Equipment, attack: 1f));
            proud.Give(new InventoryItem("Свой щит", "", ItemType.Equipment, defense: 1f, slot: GearSlot.Offhand));
            Check(!SquadGear.WillTake(proud, new InventoryItem("Чужой меч", "", ItemType.Equipment, attack: 3f), out _),
                  "гордец своего оружия не сменит");

            sloth.Give(new InventoryItem("Старый ворот", "", ItemType.Equipment, defense: 1f));
            Check(SquadGear.WillTake(sloth, collar, out _), "унылый сменить согласен — это не лишнее");

            Check(!Regex.IsMatch(SquadGear.GoldWord(57) + SquadGear.Effect(axe) + SquadGear.Effect(collar), "[0-9]"),
                  "казна и вещи — словами, без чисел");
        }

        /// <summary>
        /// Личный карман (docs/34-GEAR.md §9.3): жадный делит найденное
        /// сам, но не больше половины себе; карман читает Жадность; очень
        /// жадный своего не отдаёт, умеренный — отдаёт, если попросить.
        /// </summary>
        private static void Pocket()
        {
            var greedy = MakeWarrior("Скряга", SinType.Greed, 80f);
            var wrathful = MakeWarrior("Горячий", SinType.Wrath, 80f);

            int kept = SquadGear.Kept(greedy, 20);
            Check(kept > 0 && kept <= 10, "жадный оставляет себе часть, но не больше половины");
            Check(SquadGear.Kept(wrathful, 20) == 0, "не жадный отдаёт всё");

            var module = new AOS.Modules.GreedModule();
            var soul = Soul.FromWarrior(greedy);
            var poor = new DecisionContext { NearbyEnemies = 2 };
            var rich = new DecisionContext { NearbyEnemies = 2, PocketGold = 40 };
            Check(module.Evaluate(soul, rich, ActionType.Attack) < module.Evaluate(soul, poor, ActionType.Attack),
                  "с полным карманом жадный реже лезет в драку");
            Check(module.Evaluate(soul, rich, ActionType.Idle) > module.Evaluate(soul, poor, ActionType.Idle),
                  "и охотнее стоит в стороне: есть что терять");
            Check(Math.Abs(module.Evaluate(soul, rich, ActionType.Flee) - module.Evaluate(soul, poor, ActionType.Flee)) < 0.001f,
                  "стоит, а не бежит: бегство — голос страха, и причина врала бы");

            var store = NewObject("Кошель").AddComponent<PlayerInventory>();
            greedy.Pocket(10);
            Check(!SquadGear.AskPocket(greedy, store, out _) && greedy.PocketGold == 10,
                  "очень жадный своего золота не отдаёт");

            var modest = MakeWarrior("Скромник", SinType.Greed, 30f);
            modest.Pocket(10);
            Check(SquadGear.AskPocket(modest, store, out _) && modest.PocketGold == 0 && store.Gold == 10,
                  "умеренный отдаёт, если попросить, — золото уходит в кошель Греховода");
        }

        /// <summary>
        /// Объяснение «от противного» (docs/35-CRITIQUE.md §3): названа та
        /// причина, без которой приказ был бы исполнен, — и никакая другая.
        /// </summary>
        private static void ReasonByContrast()
        {
            var c = new DecisionContext
            {
                HasCommand = true, CommandType = "Move",
                CommandVolume = 0.5f, UnpaidMissions = 2, Fatigue = 0.5f,
            };

            Check(Counterfactual.Decisive(c, x => x.CommandVolume >= 1f) == Counterfactual.Factor.Distance,
                  "названа причина, без которой приказ исполнили бы");
            Check(Counterfactual.Decisive(c, x => x.UnpaidMissions == 0) == Counterfactual.Factor.Debt,
                  "причина, которая не решила, не названа, даже стоя первой по порядку");
            Check(Counterfactual.Decisive(c, x => x.UnpaidMissions == 0 && x.Fatigue <= 0f) == Counterfactual.Factor.None
                  && Counterfactual.DecisivePair(c, x => x.UnpaidMissions == 0 && x.Fatigue <= 0f, out var a, out var b)
                  && a == Counterfactual.Factor.Debt && b == Counterfactual.Factor.Fatigue,
                  "отказ на двух причинах назван парой");
            Check(Counterfactual.Decisive(c, x => false) == Counterfactual.Factor.None
                  && !Counterfactual.DecisivePair(c, x => false, out _, out _),
                  "ничего не решило — ничего и не выдумано");
            Check(Math.Abs(c.CommandVolume - 0.5f) < 0.001f && c.UnpaidMissions == 2,
                  "пересчёт не трогает настоящее положение");

            var order = new List<string>(Counterfactual.Voices(SinType.Greed));
            Check(order.Count > 0 && order[0] == "Greed" && !order.Contains("Loyalty"),
                  "свой грех проверяется первым, верность — никогда: она всегда за приказ");
            Check(Counterfactual.DecisiveVoice(order, id => id == "Pride") == "Pride",
                  "решил голос души — назван он");

            foreach (Counterfactual.Factor f in Enum.GetValues(typeof(Counterfactual.Factor)))
                Check(!Regex.IsMatch(Counterfactual.Phrase(f, c, SinType.Greed, Gender.Male), "[0-9]"),
                      $"причина «{f}» — словами, без чисел");

            // На настоящем голосовании: если отказ взвешен и причина названа,
            // без неё (или без пары) воин и правда послушался бы.
            bool was = Counterfactual.Enabled;
            try
            {
                Counterfactual.Enabled = true;
                var resolver = new BehaviourResolver();
                var proud = MakeWarrior("Спесь далёкая", SinType.Pride, 90f);
                var far = new DecisionContext
                {
                    HasCommand = true, CommandType = "Move", CommandVolume = 0.2f,
                    CurrentHP = 30f, MaxHP = 30f, Fatigue = 0.5f,
                };
                var d = resolver.DecideDetailed(proud, far);
                if (d.RefusedCommand)
                {
                    Check(d.Weighed, "отказ взвешен «от противного»");
                    if (d.Decisive != Counterfactual.Factor.None)
                    {
                        var without = Counterfactual.Without(far, d.Decisive);
                        if (d.DecisiveAlso != Counterfactual.Factor.None)
                            without = Counterfactual.Without(without, d.DecisiveAlso);
                        Check(resolver.WouldObey(proud, without),
                              "без названной причины воин и правда послушался бы");
                    }
                }
            }
            finally
            {
                Counterfactual.Enabled = was;
            }
        }

        /// <summary>
        /// Сундук — склад (docs/34-GEAR.md §9.4): вещи перекладываются
        /// между сундуком и мешком Греховода туда и обратно, ничего не теряя;
        /// в полный мешок не взять, и вещь остаётся в сундуке.
        /// </summary>
        private static void ChestStore()
        {
            var chest = NewObject("Сундук").AddComponent<TrophyChest>();
            var bag = NewObject("Мешок").AddComponent<PlayerInventory>();
            var axe = new InventoryItem("Топор из сундука", "", ItemType.Equipment, attack: 2f);

            bag.AddItem(axe);
            Check(chest.Put(axe, bag, out _) && new List<InventoryItem>(chest.Contents).Contains(axe) && !bag.GetAllItems().Contains(axe),
                  "из мешка — в сундук");
            Check(chest.Take(axe, bag, out _) && !new List<InventoryItem>(chest.Contents).Contains(axe) && bag.GetAllItems().Contains(axe),
                  "из сундука — в мешок");
            Check(!chest.Take(axe, bag, out _), "чего в сундуке нет, того не взять");

            chest.Put(axe, bag, out _);
            for (int i = 0; bag.Count < bag.MaxSlots && i < 50; i++)
                bag.AddItem(new InventoryItem($"Груз {i}", "", ItemType.Equipment));
            Check(!chest.Take(axe, bag, out _) && new List<InventoryItem>(chest.Contents).Contains(axe),
                  "в полный мешок не взять — вещь остаётся в сундуке");
        }

        /// <summary>
        /// Цепь добычи (§83): гордый забирает трофей с тела врага, и трофей
        /// называет, кому он достался, — чтобы лечь в руки ему, а не отряду.
        /// </summary>
        private static void LootToHands()
        {
            var proud = MakeWarrior("Гордец", SinType.Pride, 80f);
            var body = NewObject("Тело врага").AddComponent<HarvestableBody>();
            body.Initialize(ShellType.Living, 0, true, "Родовой клинок");
            body.Foe = true;

            var loot = LootCarrySystem.DistributeLoot(new List<Warrior> { proud },
                                                      new List<HarvestableBody> { body });
            Check(loot.Trophies.Count == 1 && loot.Trophies[0].Who == proud,
                  "трофей помнит, кто его забрал");

            var trophy = LootChain.Trophy("Родовой клинок");
            Check(trophy.AttackBonus > 0f && trophy.TemptationSin == SinType.Pride,
                  "трофей бьёт тяжелее и тешит гордыню");
            Check(SquadGear.WillTake(proud, trophy, out _), "гордый берёт трофей в руки");
        }

        /// <summary>
        /// Разговоры у костра (docs/32-CAMP.md §6): положение говорит раньше
        /// греха, одна пара — одни слова, немой отвечает жестом, чисел нет.
        /// </summary>
        private static void CampTalkLines()
        {
            var greedy = MakeWarrior("Скупец", SinType.Greed, 60f);
            greedy.UnpaidMissions = 3;
            var proud = MakeWarrior("Спесивец", SinType.Pride, 60f);
            var mute = MakeWarrior("Немой Проба", SinType.Sloth, 30f);

            var (first, answer) = Dialogue.CampLines.Exchange(greedy, proud);
            Check(first.Contains("без платы") && !string.IsNullOrEmpty(answer),
                  "долг звучит раньше греха: реплика предупреждает об отказе");
            Check(Dialogue.CampLines.Exchange(greedy, proud) == (first, answer), "одна пара — одни слова");
            Check(Dialogue.CampLines.Exchange(proud, mute).Answer == "(молча кивает)", "немой отвечает жестом");

            var sins = new[] { SinType.Pride, SinType.Greed, SinType.Sloth };
            foreach (var x in sins)
                foreach (var y in sins)
                {
                    var (l1, l2) = Dialogue.CampLines.Exchange(MakeWarrior("Первый " + x, x, 50f),
                                                               MakeWarrior("Второй " + y, y, 50f));
                    Check(!string.IsNullOrEmpty(l1) && !string.IsNullOrEmpty(l2)
                          && !Regex.IsMatch(l1 + l2, "[0-9]"),
                          $"разговор {x} с {y} — есть и без чисел");
                }
        }

        /// <summary>
        /// Жизнь в лагере (docs/32-CAMP.md): место выбирают модули, по душе,
        /// одинаково в каждом запуске.
        /// </summary>
        private static void CampSpots()
        {
            var voices = new List<ICampModule>
            {
                new AOS.Modules.GreedModule(), new AOS.Modules.PrideModule(),
                new AOS.Modules.WrathModule(), new AOS.Modules.EnvyModule(),
                new AOS.Modules.LustModule(), new AOS.Modules.GluttonyModule(),
                new AOS.Modules.SlothModule(), new AOS.Modules.LoyaltyModule(),
            };

            Soul One(SinType sin, float value, float loyalty = 50f)
            {
                var s = new Soul { Loyalty = loyalty };
                s.Spectra[(int)sin] = value;
                return s;
            }

            Check(CampChoice.Choose(voices, One(SinType.Greed, 80f)) == CampSpot.Chest, "жадный встаёт у сундука");
            Check(CampChoice.Choose(voices, One(SinType.Sloth, 80f)) == CampSpot.Tents, "унылый уходит в палатки");
            Check(CampChoice.Choose(voices, One(SinType.Sloth, -80f)) == CampSpot.Watch, "усердный встаёт в дозор");
            Check(CampChoice.Choose(voices, One(SinType.Pride, 80f)) == CampSpot.Apart, "гордый стоит в стороне");
            Check(CampChoice.Choose(voices, One(SinType.Wrath, 10f, 100f)) == CampSpot.Sinbinder,
                  "верный держится рядом с Греховодом");
            Check(CampChoice.Choose(voices, new Soul()) == CampSpot.Fire, "кому всё равно — греется у огня");
            Check(CampChoice.Choose(voices, One(SinType.Greed, 80f)) == CampChoice.Choose(voices, One(SinType.Greed, 80f)),
                  "одна душа — одно место, без жребия");
        }

        /// <summary>
        /// Приказы шага второго (docs/33-COMMANDS.md): патруль ходит
        /// маршрутом и скучен унылому; атаку с ходу удар по дороге исполняет.
        /// </summary>
        private static void PatrolAndRush()
        {
            var rush = new DecisionContext { HasCommand = true, CommandType = "AttackMove", CommandIsAttackMove = true };
            Check(rush.SatisfiedBy(ActionType.Attack), "удар по дороге исполняет атаку с ходу");

            var patrol = new DecisionContext { HasCommand = true, CommandType = "Patrol", CommandIsPatrol = true };
            Check(!patrol.SatisfiedBy(ActionType.Attack), "бросить маршрут ради драки — не патруль");

            var sloth = new AOS.Modules.SlothModule();
            var lazy = new Soul();
            lazy.Spectra[(int)SinType.Sloth] = 70f;
            var plain = new DecisionContext { HasCommand = true, CommandType = "Move" };
            Check(sloth.Evaluate(lazy, patrol, ActionType.ObeyCommand) < sloth.Evaluate(lazy, plain, ActionType.ObeyCommand),
                  "патруль скучен унылому");

            var walker = MakeWarrior("Дозорный", SinType.Wrath, 40f);
            walker.IssuePatrol(walker.transform.position + Vector3.forward * 10f);
            Check(walker.Command.Kind == CommandKind.Patrol && walker.Command.From == walker.transform.position,
                  "патруль помнит, откуда вышел");
            walker.TurnPatrol();
            Check(walker.Command.Back, "дойдя до конца, патруль разворачивается");
        }

        private static void Bodies()
        {
            var w = MakeWarrior("Тело", SinType.Wrath, 50f);
            var body = w.gameObject.AddComponent<Damageable>();

            Near(w.HP, body.HP, "здоровье воина — это здоровье тела");
            Near(w.MaxHP, body.MaxHP, "запас воина — это запас тела");

            float before = body.HP;
            w.TakeDamage(5f);
            Check(body.HP < before, "урон воину доходит до тела");

            float wounded = body.HP;
            w.Heal(2f);
            Check(body.HP > wounded, "лечение воина доходит до тела");

            body.TakeDamage(100000f, null);
            Check(body.IsDead, "тело можно убить");
            Check(w.IsDead, "убитый в бою мёртв и для души — вторая полоса здоровья (§58)");

            w.Heal(100f);
            Check(body.IsDead, "мёртвого не лечат");

            // Запас — от оболочки, и только от неё (§60).
            var bone = MakeWarrior("Кость", SinType.Wrath, 50f);
            var man = NewObject("Человек").AddComponent<Warrior>();
            man.Initialize(new SoulData("Человек", SinType.Wrath, MoralType.Neutral, 1, 50f),
                ShellType.Living, new RelationshipSystem(null));

            if (bone.ShellHP <= 0f || man.ShellHP <= 0f)
            {
                Fail("оболочки не загрузились — запасу тела не от чего считаться");
            }
            else
            {
                Near(bone.ShellHP, 29.7f, "скелет держит столько, сколько отряд держал в демо", 0.05f);
                Near(man.ShellHP, 40f, "человек — как задумано для второй волны", 0.05f);
            }

            var veteran = NewObject("Ветеран").AddComponent<Warrior>();
            veteran.Initialize(new SoulData("Ветеран", SinType.Wrath, MoralType.Neutral, 5, 50f),
                ShellType.Skeleton, new RelationshipSystem(null));
            Near(veteran.MaxHP, bone.MaxHP, "уровень здоровья не прибавляет — уровней нет");

            var fresh = NewObject("Рана").AddComponent<Damageable>();
            fresh.Wound(0.4f);
            Near(fresh.HP, fresh.MaxHP * 0.4f, "побитый выходит с назначенной долей запаса");
            Check(!fresh.IsDead, "побитый — не убитый");
        }

        /// <summary>
        /// «Тень» за одно выживание получали все, Греховод и мёртвые — тоже
        /// (§57, §58).
        /// </summary>
        private static void Titles()
        {
            var quiet = MakeWarrior("Тихоня", SinType.Sloth, 70f);
            quiet.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.SurviveMission, Importance = 0.3f });
            TitleManager.UpdateTitle(quiet);
            Check(string.IsNullOrEmpty(TitleManager.TitleOf(quiet)),
                "за одно выживание имени не дают");

            quiet.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.StayedOut, Importance = 0.3f });
            quiet.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.StayedOut, Importance = 0.3f });
            TitleManager.UpdateTitle(quiet);
            Same(TitleManager.TitleOf(quiet), "Тень", "два боя без удара — «Тень»");

            var hero = NewObject("Греховод").AddComponent<SinbinderPlayer>();
            hero.Initialize(new SoulData("Греховод", SinType.Pride, MoralType.Neutral, 1, 0f),
                ShellType.Skeleton, null, isCommander: false, team: Team.Player);
            hero.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.StayedOut, Importance = 0.3f });
            hero.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.StayedOut, Importance = 0.3f });
            TitleManager.UpdateTitle(hero);
            Check(string.IsNullOrEmpty(TitleManager.TitleOf(hero)), "Греховод титулов не носит");

            var fallen = MakeWarrior("Павший", SinType.Sloth, 70f);
            var fallenBody = fallen.gameObject.AddComponent<Damageable>();
            fallenBody.TakeDamage(100000f, null);
            fallen.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.StayedOut, Importance = 0.3f });
            fallen.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.StayedOut, Importance = 0.3f });
            TitleManager.UpdateTitle(fallen);
            Check(string.IsNullOrEmpty(TitleManager.TitleOf(fallen)), "павшему имени не присуждают");
        }

        /// <summary>
        /// Погоня (§62): «атаковать» было кандидатом только при враге рядом,
        /// и отбежавший на двенадцать метров переставал существовать.
        /// </summary>
        private static void Pursuit()
        {
            var fury = MakeWarrior("Ярый", SinType.Wrath, 95f);
            var resolver = new BehaviourResolver();

            var far = BaseContext(fury);
            far.NearbyEnemies = 0;
            far.EnemiesInSight = 1;
            far.DangerLevel = 0f;

            var d = resolver.DecideDetailed(fury, far);
            Same(d.TopContender, ActionType.Attack,
                "гневный гонится за видимым, но далёким врагом");

            var hurt = BaseContext(fury);
            hurt.NearbyEnemies = 0;
            hurt.EnemiesInSight = 1;
            hurt.CurrentHP = hurt.MaxHP * 0.2f;

            d = resolver.DecideDetailed(fury, hurt);
            Check(d.Action != ActionType.Flee && d.TopContender != ActionType.Flee,
                "от далёкого врага не бегут — бежать можно только от того, кто рядом");
        }

        /// <summary>
        /// Свои и чужие — с точки зрения спросившего (§62). Охотники считали
        /// своими наш отряд, а врагами — товарищей.
        /// </summary>
        private static void Sides()
        {
            var combat = NewObject("Бой").AddComponent<CombatManager>();
            var ours = NewObject("Наш").AddComponent<Damageable>();
            var theirs = NewObject("Их").AddComponent<Damageable>();

            combat.RegisterPlayerUnit(ours);
            combat.RegisterEnemyUnit(theirs);

            var theirAllies = combat.GetAllies(theirs.gameObject);
            Check(theirAllies.Contains(theirs) && !theirAllies.Contains(ours),
                "свои у охотника — охотники, а не наш отряд");
            Check(combat.GetEnemies(theirs.gameObject).Contains(ours),
                "враги у охотника — наш отряд");
            Check(combat.GetAllies(ours.gameObject).Contains(ours)
                  && !combat.GetAllies(ours.gameObject).Contains(theirs),
                "свои у нашего — наши");
        }

        /// <summary>
        /// Цели охотников (§66): первая волна — к костру, вторая — по следу
        /// Инквизитора. Здесь Инквизиторов нет, поэтому след — остывший.
        /// </summary>
        private static void HunterGoals()
        {
            bool hadTrail = SinbinderTrail.Known;
            var hadWhere = SinbinderTrail.Where;

            try
            {
                var toFire = NewObject("К костру").AddComponent<HunterGoal>();
                toFire.Configure(HunterGoal.Aim.CampCentre, Vector3.zero);

                Check(toFire.TryGet(new Vector3(20f, 0f, 0f), out var where) && where == Vector3.zero,
                    "первая волна идёт к костру");
                Check(!toFire.TryGet(new Vector3(1f, 0f, 1f), out _),
                    "дошла до костра — дальше решает голос");
                Check(!toFire.TryGet(new Vector3(20f, 0f, 0f), out _),
                    "дошедшая к костру не возвращается");

                SinbinderTrail.Forget();
                var tracker = NewObject("По следу").AddComponent<HunterGoal>();
                tracker.Configure(HunterGoal.Aim.Trail, Vector3.zero);

                Check(!tracker.TryGet(new Vector3(20f, 0f, 0f), out _),
                    "пока Инквизитор не искал — следа нет, решает голос");

                var found = new Vector3(10f, 0f, 10f);
                SinbinderTrail.Mark(found);
                Check(tracker.TryGet(new Vector3(-10f, 0f, -10f), out var trail) && trail == found,
                    "стая идёт туда, где Греховода нашли в последний раз");
                Check(!tracker.TryGet(found + new Vector3(1f, 0f, 0f), out _),
                    "остывший след обрывается на последнем месте");
            }
            finally
            {
                if (hadTrail) SinbinderTrail.Mark(hadWhere);
                else SinbinderTrail.Forget();
            }
        }

        /// <summary>
        /// Смена доли стирала пол, ремесло, братство и славу (§63): уникальная
        /// женщина входила в склеп мужчиной.
        /// </summary>
        private static void Roster()
        {
            var saved = new List<SquadRoster.Member>(SquadRoster.Members);

            try
            {
                const string name = "Проверка переноса";
                SquadRoster.Set(new[]
                {
                    new SquadRoster.Member
                    {
                        Name = name, Sin = SinType.Greed, Moral = MoralType.Neutral,
                        Gender = Gender.Female, Trade = Trade.Archer,
                        Brother = true, Legend = true, Intensity = 40f, Loyalty = 60f,
                        Unavailable = "",
                    },
                });

                var her = MakeWarrior(name, SinType.Greed, 40f);
                var axe = new InventoryItem("Топор беглянки", "", ItemType.Equipment, attack: 2f);
                her.Give(axe);
                her.Pocket(7);
                SquadRoster.Remember(new[] { her });

                Check(SquadRoster.TryGet(name, out var m), "воин пережил смену доли");
                Same(m.Gender, Gender.Female, "смена доли не меняет пол");
                Same(m.Trade, Trade.Archer, "смена доли не отнимает ремесло");
                Check(m.Brother, "братство переживает смену доли");
                Check(m.Legend, "слава переживает смену доли");

                // Вещи на сбежавших остаются на них (docs/34-GEAR.md §9.4):
                // до 24 сентября всё надетое пропадало при смене доли.
                Check(m.Gear != null && m.Gear.Contains(axe) && m.Pocket == 7,
                      "надетое и карман переживают смену доли");
                var again = Expedition.Summon(NewObject("Держатель"), m, new RelationshipSystem(null));
                Check(again.Worn(GearSlot.Weapon) == axe && again.PocketGold == 7,
                      "воин из записи снова в своём и при своём золоте");
            }
            finally
            {
                SquadRoster.Set(saved);
            }
        }

        /// <summary>
        /// Пауза без хозяина (§57, §61): наезд снимал чужую паузу, а церемония,
        /// начатая рядом со смертью Греховода, снимала бы паузу конца игры.
        /// </summary>
        private static void Pausing()
        {
            var pause = NewObject("Пауза").AddComponent<GamePauseController>();
            float was = Time.timeScale;

            try
            {
                int before = pause.Stamp;
                pause.Pause();
                Check(pause.IsPaused && pause.Stamp == before + 1,
                    "пауза считает, сколько раз её ставили");

                int mine = pause.Stamp;
                pause.Pause();
                Check(pause.Stamp != mine,
                    "чужая пауза поверх меняет счёт — наезд её уже не снимет");

                pause.Resume();
                Check(!pause.IsPaused, "пауза снимается");

                pause.Halt();
                pause.Resume();
                Check(pause.IsPaused, "конец игры не снимается чужим Resume");

                pause.Unhalt();
                Check(!pause.IsPaused && !pause.Halted, "«начать сначала» снимает конец игры");
            }
            finally
            {
                Time.timeScale = was;
            }
        }

        /// <summary>
        /// Разгром — доля без своей сцены (§70): запись посреди него помнит
        /// «набег», а открывается по ней лагерь. Сцены набега в сборке нет,
        /// и грузить доли по имени значило бы грузить пустоту.
        /// </summary>
        private static void RaidPart()
        {
            string was = SaveSystem.StagedScene;

            try
            {
                Check(RaidEvent.HostOf(RaidEvent.SceneName) == "Prologue_Camp",
                    "запись посреди разгрома открывает лагерь");
                Check(RaidEvent.HostOf("Crypt_Entrance") == "Crypt_Entrance",
                    "остальные доли открывают свои сцены");

                SaveSystem.StagedScene = RaidEvent.SceneName;
                Check(SaveSystem.Here == RaidEvent.SceneName,
                    "посреди разгрома запись пишет «набег», а не «лагерь»");
            }
            finally
            {
                SaveSystem.StagedScene = was;
            }
        }

        /// <summary>
        /// Голос Греховода (docs/31-VOICE.md): громкость по расстоянию и то,
        /// как её читают модули. Выключенный голос не меняет ничего.
        /// </summary>
        private static void VoiceOfSinbinder()
        {
            bool was = Voice.Enabled;

            try
            {
                Voice.Enabled = false;
                Check(Voice.Muffle(Voice.Far * 2f) == 0f, "выключенный голос слышен на любом расстоянии");

                Voice.Enabled = true;
                float mid = Voice.Muffle((Voice.Near + Voice.Far) * 0.5f);
                float edge = Voice.Muffle(Voice.Far);
                Check(Voice.Muffle(Voice.Near) == 0f, "рядом приказ звучит в полную силу");
                Check(mid > 0f && mid < edge, "издали тише, чем рядом, и громче, чем на краю");
                Check(Voice.Heard(edge), "на самом краю приказ ещё слышен");
                Check(!Voice.Heard(Voice.Muffle(Voice.Far + 0.5f)), "за краем приказа не слышно");

                var loyalty = new AOS.Modules.LoyaltyModule();
                float Obey(float loyal, float volume) => loyalty.Evaluate(
                    new Soul { Loyalty = loyal },
                    new DecisionContext { HasCommand = true, CommandType = "Move", CommandVolume = volume },
                    ActionType.ObeyCommand);

                Check(Obey(50f, 0.35f) < Obey(50f, 1f), "далёкий приказ верности тише близкого");
                Check(Obey(95f, 0.35f) / Obey(95f, 1f) > Obey(30f, 0.35f) / Obey(30f, 1f),
                      "верный теряет с расстоянием меньше неверного");

                var pride = new AOS.Modules.PrideModule();
                var proud = new Soul();
                proud.Spectra[(int)SinType.Pride] = 80f;
                var humble = new Soul();
                humble.Spectra[(int)SinType.Pride] = -80f;

                float Heed(Soul s, float volume) => pride.Evaluate(s,
                    new DecisionContext { HasCommand = true, CommandType = "Move", CommandVolume = volume },
                    ActionType.ObeyCommand);

                Check(Heed(proud, 0.35f) < Heed(proud, 1f), "гордец противится приказу, крикнутому издали");
                Check(Heed(humble, 0.35f) >= Heed(humble, 1f), "смиренному расстояние не помеха");
            }
            finally
            {
                Voice.Enabled = was;
            }
        }

        /// <summary>
        /// Туман войны (§67): видно то, что видят свои; враг вне круга
        /// зрения скрыт; свои туманом не скрываются.
        /// </summary>
        private static void Fog()
        {
            // Туман ищет глаза поиском по сцене, а он не видит объектов
            // с HideAndDontSave — такими NewObject делает всё остальное.
            // Здесь объекты обычные; удаляются они в конце, как и прочие.
            var eye = Visible("Глаз").AddComponent<Warrior>();
            eye.Initialize(new SoulData("Глаз", SinType.Wrath, MoralType.Neutral, 1, 50f),
                ShellType.Skeleton, new RelationshipSystem(null));
            eye.transform.position = Vector3.zero;

            var near = Visible("Близкий").AddComponent<Warrior>();
            near.Initialize(new SoulData("Близкий", SinType.Wrath, MoralType.Neutral, 1, 50f),
                ShellType.Living, new RelationshipSystem(null), false, Team.Enemy);
            near.transform.position = new Vector3(5f, 0f, 0f);

            var far = Visible("Дальний").AddComponent<Warrior>();
            far.Initialize(new SoulData("Дальний", SinType.Wrath, MoralType.Neutral, 1, 50f),
                ShellType.Living, new RelationshipSystem(null), false, Team.Enemy);
            far.transform.position = new Vector3(0f, 0f, 19f);

            var fog = NewObject("Туман").AddComponent<FogOfWar>();
            var build = typeof(FogOfWar).GetMethod("Build",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (build == null) { Fail("у тумана нет Build — проверка тумана недостоверна"); return; }

            build.Invoke(fog, new object[] { new Bounds(Vector3.zero, new Vector3(40f, 1f, 40f)) });

            Check(!FogOfWar.Hides(near), "враг в круге зрения виден");
            Check(FogOfWar.Hides(far), "враг вне зрения скрыт туманом");
            Check(!FogOfWar.Hides(eye), "свои туманом не скрываются");
        }

        // ================= добыча =================

        /// <summary>
        /// Чего стоит труп.
        ///
        /// Здесь стоял Random.Range(5, 20), и случайность оттуда доходила
        /// до титула — то есть до рычага игрока. Разбор ведёт стенд
        /// (Tools/bench → ДОБЫЧА), но стенд считает на своей заглушке
        /// Mathf. Эта проверка — единственное место, где те же числа
        /// считает настоящий Юнити: если заглушка и движок разойдутся
        /// в округлении, разойдутся здесь.
        /// </summary>
        private static void Spoils()
        {
            var hunter = new SoulData("Ловчий", SinType.Greed, MoralType.Vicious, 1, 50f);

            int first = BodyWorth.Gold(ShellType.Zombie, hunter);
            int second = BodyWorth.Gold(ShellType.Zombie,
                new SoulData("Ловчий", SinType.Greed, MoralType.Vicious, 1, 50f));
            Same(second, first, "два одинаковых трупа стоят разного");

            // Числа те же, что печатает стенд. Расхождение означает,
            // что заглушка врёт, а не что баланс поехал.
            Same(first, 15, "Ловчий пролога стоит не того, что показал замер");
            Same(BodyWorth.Gold(ShellType.Zombie,
                    new SoulData("Охотник", SinType.Wrath, MoralType.Vicious, 1, 60f)),
                 10, "Охотник пролога стоит не того, что показал замер");

            var greedy = new SoulData("жадный", SinType.Greed, MoralType.Neutral, 1, 80f);
            var generous = new SoulData("щедрый", SinType.Greed, MoralType.Neutral, 1, -80f);
            var idle = new SoulData("унылый", SinType.Sloth, MoralType.Neutral, 1, 80f);

            Check(BodyWorth.Gold(ShellType.Zombie, greedy)
                > BodyWorth.Gold(ShellType.Zombie, generous),
                  "щедрый труп обязан быть беднее жадного");
            Check(BodyWorth.Gold(ShellType.Zombie, greedy)
                > BodyWorth.Gold(ShellType.Zombie, idle),
                  "унылый труп обязан быть беднее жадного");

            // С призрака нечего взять: тела нет, и это не «мало».
            Same(BodyWorth.Gold(ShellType.Ghost, greedy), 0,
                 "с призрака что-то сняли");
            Check(!BodyWorth.HasEquipment(ShellType.Ghost, greedy),
                  "на призраке нашлось снаряжение");

            // Трофей носит тот, кому он что-то значил.
            var proud = new SoulData("гордый", SinType.Pride, MoralType.Neutral, 1, 60f);
            Check(BodyWorth.HasEquipment(ShellType.Skeleton, proud),
                  "гордый лёг без трофея");
            Check(!BodyWorth.HasEquipment(ShellType.Skeleton, idle),
                  "унылый почему-то нёс трофей");
            Same(BodyWorth.Equipment(ShellType.Skeleton, idle), null,
                 "у трупа без снаряжения нашлось название предмета");
        }

        // ================= сохранение =================

        /// <summary>
        /// Снимок игры доходит до файла и возвращается целым.
        ///
        /// Стенду это не проверить: JsonUtility — настоящий Unity,
        /// и ведёт он себя по-своему. Плоские поля, никаких свойств,
        /// никаких словарей — узнаётся это обычно на пустом файле,
        /// который записался без единой ошибки.
        /// </summary>
        private static void Saving()
        {
            var wasCommitment = Commitment.On;
            var kept = new List<SquadRoster.Member>(SquadRoster.Members);

            try
            {
                SquadRoster.Set(new[]
                {
                    new SquadRoster.Member
                    {
                        Name = "Проба", Sin = SinType.Greed, Moral = MoralType.Vicious,
                        Intensity = 60f, Loyalty = 42f, UnpaidMissions = 3,
                        Leadership = 5f, IsCommander = true,
                        Gear = new List<InventoryItem>
                        {
                            new InventoryItem("Топор пробы", "", ItemType.Equipment, attack: 2f),
                        },
                        Pocket = 5,
                    },
                    new SquadRoster.Member
                    {
                        Name = "Вторая", Sin = SinType.Sloth, Moral = MoralType.Pious,
                        Intensity = -30f, Loyalty = 71f, Gender = Gender.Female,
                    },
                });

                Commitment.Set(true);

                var snapshot = SaveSystem.Snapshot();
                Same(snapshot.Squad.Count, 2, "снимок взял не весь отряд");
                snapshot.Bag.Add(new InventoryItem("Мясо пробы", "", ItemType.Provision, 3));
                snapshot.ChestLooted = true;

                // Через текст и обратно: в игре между снимком и возвратом
                // всегда стоит файл, и проверять надо путь целиком.
                string json = JsonUtility.ToJson(snapshot);
                var back = JsonUtility.FromJson<SaveGame>(json);

                Check(back != null, "снимок не прочитался обратно");
                if (back == null) return;

                SquadRoster.Clear();
                Check(SaveSystem.Restore(back), "снимок не принят обратно");

                Same(SquadRoster.Members.Count, 2, "вернулся не весь отряд");
                if (SquadRoster.Members.Count < 2) return;

                var first = SquadRoster.Members[0];
                Same(first.Name, "Проба", "имя не пережило файл");
                Same(first.Sin, SinType.Greed, "грех не пережил файл");
                Same(first.Moral, MoralType.Vicious, "мораль не пережила файл");
                Near(first.Loyalty, 42f, "верность не пережила файл");
                Same(first.UnpaidMissions, 3, "долг не пережил файл");
                Check(first.IsCommander, "старшинство не пережило файл");
                Check(first.Gear != null && first.Gear.Count == 1 && first.Gear[0].Name == "Топор пробы"
                      && Math.Abs(first.Gear[0].AttackBonus - 2f) < 0.01f && first.Gear[0].Slot == GearSlot.Weapon,
                      "надетое не пережило файл");
                Same(first.Pocket, 5, "карман не пережил файл");
                Check(back.Bag.Count == 1 && back.Bag[0].Name == "Мясо пробы" && back.ChestLooted,
                      "мешок Греховода и открытый сундук не пережили файл");

                var second = SquadRoster.Members[1];
                Near(second.Intensity, -30f, "добродетель вернулась грехом: "
                                           + "знак спектра потерян");
                Same(second.Gender, Gender.Female, "пол не пережил файл — "
                                                 + "вернувшаяся женщина стала мужчиной");

                Check(Commitment.On, "режим обязательств не пережил файл — "
                                   + "ответственную игру можно было бы открыть свободной");

                // Начало доли (решение автора): отметка помнит долю, а в игре
                // с обязательством вернуться к ней нельзя — уговор режима.
                SaveSystem.MarkCheckpoint("Prologue_Camp");
                Check(SaveSystem.Checkpoint != null && SaveSystem.Checkpoint.Scene == "Prologue_Camp",
                      "отметка начала доли помнит долю");
                Check(Commitment.On && !SaveSystem.CanRestartPart,
                      "в игре с обязательством «С начала доли» не предлагается");

                // Файл другого уклада обязан быть отвергнут целиком.
                back.Version = SaveGame.Current + 1;
                Check(!SaveSystem.Restore(back),
                      "снимок чужого уклада прочитан: половина состояния "
                    + "хуже, чем ничего, потому что выглядит целой");
            }
            finally
            {
                SquadRoster.Set(kept);
                Commitment.Set(wasCommitment);
            }
        }

        // ================= утверждения =================

        private static void Check(bool condition, string what)
        {
            if (condition) _report.Passed++;
            else _report.Failures.Add(what);
        }

        private static void Fail(string what) => _report.Failures.Add(what);

        private static void Near(float actual, float expected, string what, float eps = 0.01f)
            => Check(Mathf.Abs(actual - expected) <= eps, $"{what}: ожидалось {expected}, получено {actual}");

        private static void Same(object actual, object expected, string what)
            => Check(Equals(actual, expected), $"{what}: ожидалось {expected}, получено {actual}");

        // ================= спектры =================

        private static void Spectra()
        {
            var soul = new SoulData("Пробный", SinType.Greed, MoralType.Neutral, 1, 60f);

            Near(soul.Get(SinType.Greed), 60f, "Жадность легла в свою шкалу");
            Near(soul.Get(SinType.Wrath), 0f, "остальные шкалы пусты");
            Same(soul.Sin, SinType.Greed, "доминирующий грех");
            Near(soul.SinIntensity, 60f, "интенсивность доминирующего");

            soul.Set(SinType.Wrath, 500f);
            Near(soul.Get(SinType.Wrath), 100f, "верхний предел шкалы");

            soul.Change(SinType.Wrath, -1000f);
            Near(soul.Get(SinType.Wrath), -100f, "нижний предел шкалы");

            // Шкалы независимы: это и была вся суть правки.
            Near(soul.Get(SinType.Greed), 60f, "изменение одной шкалы не трогает другие");

            // Доминирует наиболее удалённая от нуля, а не наибольшая:
            // святая душа определяется добродетелью так же, как порочная пороком.
            var saint = new SoulData("Праведник", MoralType.Pious, 1,
                new[] { 30f, 0f, 0f, 0f, 0f, 0f, -80f });
            Same(saint.Sin, SinType.Sloth, "доминирует шкала, дальше всех от нуля");
            Near(saint.SinIntensity, -80f, "доминирующее значение отрицательно");

            var balanced = new SoulData("Ровный", MoralType.Neutral, 1,
                new[] { 70f, 0f, 0f, 0f, 0f, 0f, -70f });
            Near(balanced.AverageSpectrum, 0f, "среднее по шкалам");

            // Ни одна шкала не выражена — описание не должно врать.
            var blank = new SoulData("Никакой", MoralType.Neutral, 1, new float[7]);
            Check(blank.GetSpectraDescription().Contains("Ничем не выделяется"),
                "пустая душа описывается честно");
        }

        // ================= перенос старых душ =================

        private static void Migration()
        {
            // Так выглядит душа, сохранённая до введения семи шкал:
            // массива нет, есть номер греха и интенсивность.
            foreach (var empty in new[] { (float[])null, new float[0] })
            {
                var soul = new SoulData("Старая", SinType.Greed, MoralType.Neutral, 1, 0f);
                SetPrivate(soul, "_spectra", empty);
                SetPrivate(soul, "_sinType", (int)SinType.Wrath);
                SetPrivate(soul, "_sinIntensity", 70f);

                string label = empty == null ? "массив null" : "массив пуст";
                Near(soul.Get(SinType.Wrath), 70f, $"перенос старой души ({label})");
                Same(soul.Sin, SinType.Wrath, $"грех старой души сохранён ({label})");
            }

            // Массив уже заполнен — старые поля не должны его перебить.
            var modern = new SoulData("Новая", MoralType.Neutral, 1,
                new[] { 0f, 0f, 0f, 0f, 0f, 0f, 55f });
            SetPrivate(modern, "_sinType", (int)SinType.Greed);
            SetPrivate(modern, "_sinIntensity", 90f);
            Near(modern.Get(SinType.Greed), 0f, "заполненный массив не перетирается наследием");
            Same(modern.Sin, SinType.Sloth, "доминирующий грех берётся из массива");
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) { Fail($"поле {field} не найдено — проверка переноса недостоверна"); return; }
            f.SetValue(target, value);
        }

        // ================= копирование =================

        private static void Copying()
        {
            var source = new SoulData("Оригинал", MoralType.Vicious, 3,
                new[] { 10f, 20f, 30f, 40f, 50f, 60f, 70f });

            var copy = new SoulData(source);

            for (int i = 0; i < SoulData.SpectrumCount; i++)
                Near(copy.Get((SinType)i), source.Get((SinType)i), $"копия сохраняет шкалу {(SinType)i}");

            Check(copy.Id != source.Id, "у копии новый идентификатор");
            Same(copy.Moral, source.Moral, "копия сохраняет мораль");

            copy.Set(SinType.Greed, 0f);
            Near(source.Get(SinType.Greed), 10f, "правка копии не трогает оригинал");
        }

        // ================= распад души =================

        private static void Decay()
        {
            var seed = new MemorySeed { Story = "Была нянькой", NarrativePerks = new List<NarrativePerk>() };
            var source = new SoulData("Покойник", MoralType.Neutral, 2,
                new[] { 60f, 0f, 0f, 0f, 0f, 0f, 0f }, seed);

            var fresh = SoulDecay.Harvest(source, SoulQuality.Shock);
            Near(fresh.Get(SinType.Greed), 60f, "свежая душа не теряет характера");
            Check(fresh.HasMemory, "свежая душа помнит себя");

            var fading = SoulDecay.Harvest(source, SoulQuality.Fading);
            Near(fading.Get(SinType.Greed), 33f, "гаснущая душа тускнеет");
            Check(!fading.HasMemory, "гаснущая душа теряет историю");
            Check(fading.Memory != null && fading.Memory.NarrativePerks != null,
                "но повадки при ней остаются");

            var gone = SoulDecay.Harvest(source, SoulQuality.Dissolved);
            Near(gone.Get(SinType.Greed), 12f, "распавшаяся душа почти ровная");
            Check(gone.Memory == null, "распавшаяся душа не помнит ничего");

            // Собранная душа — копия. Тело на поле не должно меняться.
            Near(source.Get(SinType.Greed), 60f, "сбор не портит исходную душу");
            Check(source.HasMemory, "сбор не стирает исходную память");

            // Чем позже собрана, тем меньше осталось. Иначе смерть ничего не стоит.
            Check(SoulDecay.SpectrumFactor(SoulQuality.Shock)
                  > SoulDecay.SpectrumFactor(SoulQuality.Acceptance)
                  && SoulDecay.SpectrumFactor(SoulQuality.Acceptance)
                  > SoulDecay.SpectrumFactor(SoulQuality.Fading)
                  && SoulDecay.SpectrumFactor(SoulQuality.Fading)
                  > SoulDecay.SpectrumFactor(SoulQuality.Dissolved),
                "качество убывает строго со временем");

            Check(SoulDecay.Harvest(null, SoulQuality.Shock) == null, "сбор пустой души не падает");

            Shelf();
        }

        /// <summary>
        /// Душа, записанная на полку и прочитанная обратно.
        ///
        /// Полка теряла **пол, ремесло и заслуженное имя**: конструктор
        /// их не берёт, и душа вставала мужчиной без ремесла и без имени.
        /// Женщина переставала быть женщиной — против правила о том, что
        /// женские лица уникальны, — охотник переставал быть охотником,
        /// а первая фраза поднятого теряла и руки, и имя.
        ///
        /// Зовём тот же код, которым пишет и читает игра
        /// (<c>SaveSystem.Record</c> и <c>SaveSystem.Soul</c>), а не свою
        /// копию перекладки: копия проверяла бы саму себя.
        /// </summary>
        private static void Shelf()
        {
            var her = new SoulData("Сквип", MoralType.Vicious, 2,
                new[] { 60f, 0f, 0f, 0f, 0f, 0f, 0f }, null, Gender.Female);
            her.SetTrade(Trade.Hunter);
            her.Remember("Костекоп");

            var back = SaveSystem.Soul(SaveSystem.Record(her, SoulQuality.Shock));

            Check(back.Gender == Gender.Female, "полка не меняет пол души");
            Check(back.Trade == Trade.Hunter, "полка не теряет ремесло");
            Check(back.EarnedTitle == "Костекоп", "полка помнит заслуженное имя");
            Check(back.Moral == MoralType.Vicious, "полка не теряет мораль");
            Near(back.Get(SinType.Greed), 60f, "полка не теряет характера");

            // Старая запись новых полей не знает: нули обязаны читаться
            // как «мужчина, ремесла нет, имени нет», а не падать.
            var old = new SavedSoul
            {
                Name = "Гертон",
                Moral = (int)MoralType.Neutral,
                Level = 1,
                Spectra = new float[7],
                Quality = (int)SoulQuality.Fading,
            };

            var read = SaveSystem.Soul(old);
            Check(read != null && read.Gender == Gender.Male,
                "старая запись читается мужчиной, а не падает");
            Check(read.Trade == Trade.None, "у старой записи ремесла нет");
            Check(read.EarnedTitle == "", "у старой записи имени нет");

            Check(SaveSystem.Record(null, SoulQuality.Shock) == null,
                "пустая душа в запись не ложится");
            Check(SaveSystem.Soul(null) == null, "пустая запись не поднимается");
        }

        // ================= оболочки =================

        private static void Shells()
        {
            var shell = ScriptableObject.CreateInstance<ShellData>();
            shell.shellName = "Волчье тело";
            shell.bindStrength = 0.5f;
            shell.spectrumBias = new float[SoulData.SpectrumCount];
            shell.spectrumBias[(int)SinType.Wrath] = 40f;
            shell.spectrumBias[(int)SinType.Greed] = -20f;

            var soul = new SoulData("Терпеливый", MoralType.Pious, 1, new float[7]);

            ShellBinder.Bind(soul, shell);
            Near(soul.Get(SinType.Wrath), 20f, "плоть тянет душу на себя");
            Near(soul.Get(SinType.Greed), -10f, "смещение работает в обе стороны");

            // Дрейф необратим и накапливается: второй раз в волка — дальше.
            ShellBinder.Bind(soul, shell);
            Near(soul.Get(SinType.Wrath), 40f, "дрейф накапливается при повторном связывании");

            for (int i = 0; i < 20; i++) ShellBinder.Bind(soul, shell);
            Near(soul.Get(SinType.Wrath), 100f, "дрейф упирается в предел шкалы");

            ShellBinder.Bind(null, shell);
            ShellBinder.Bind(soul, null);
            _report.Passed++; // не упало на пустых аргументах

            var quiet = ScriptableObject.CreateInstance<ShellData>();
            quiet.spectrumBias = new float[SoulData.SpectrumCount];
            Check(quiet.DescribeBias().Contains("ничего не навязывает"),
                "нейтральное тело описывается честно");

            UnityEngine.Object.DestroyImmediate(shell);
            UnityEngine.Object.DestroyImmediate(quiet);
        }

        // ================= искушения =================

        private static void Temptation()
        {
            var context = new DecisionContext();
            context.CarriedItems = new List<InventoryItem>
            {
                new InventoryItem("Золочёный клинок", "", ItemType.Equipment, 1, SinType.Greed, 50f)
            };

            var scores = new Dictionary<ActionType, float>
            {
                { ActionType.Loot, 0f }, { ActionType.ObeyCommand, 0f }, { ActionType.Attack, 0f }
            };

            TemptationResolver.Apply(scores, context);

            Near(scores[ActionType.Loot], 10f, "предмет тянет к добыче");
            Near(scores[ActionType.ObeyCommand], -3f, "и мешает слушать приказ");
            Near(scores[ActionType.Attack], 0f, "не задевая непричастные действия");

            Near(TemptationResolver.Sum(context, SinType.Greed), 50f, "сумма искушения по греху");
            Near(TemptationResolver.Sum(context, SinType.Wrath), 0f, "чужой грех не считается");

            // Действия, которых нет среди кандидатов, добавляться не должны.
            var narrow = new Dictionary<ActionType, float> { { ActionType.Idle, 0f } };
            TemptationResolver.Apply(narrow, context);
            Same(narrow.Count, 1, "искушение не выдумывает новых кандидатов");

            TemptationResolver.Apply(null, context);
            TemptationResolver.Apply(scores, null);
            Near(TemptationResolver.Sum(null, SinType.Greed), 0f, "сумма по пустому контексту");
            _report.Passed++; // не упало на пустых аргументах
        }

        // ================= геометрия =================

        private static void Geometry()
        {
            var victim = NewObject("жертва");
            victim.transform.position = Vector3.zero;
            victim.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            Check(Facing.IsFromBehind(victim.transform, new Vector3(0f, 0f, -5f)),
                "удар точно со спины распознан");
            Check(!Facing.IsFromBehind(victim.transform, new Vector3(0f, 0f, 5f)),
                "удар в лицо спиной не считается");
            Check(!Facing.IsFromBehind(victim.transform, new Vector3(5f, 0f, 0f)),
                "удар сбоку спиной не считается");

            Near(Facing.DamageMultiplier(victim.transform, new Vector3(0f, 0f, -5f)),
                Facing.RearMultiplier, "удар в спину бьёт сильнее");
            Near(Facing.DamageMultiplier(victim.transform, new Vector3(0f, 0f, 5f)),
                1f, "удар в лицо бьёт как обычно");

            // Высота не должна превращать удар в лицо в удар в спину.
            Check(!Facing.IsFromBehind(victim.transform, new Vector3(0f, 10f, 5f)),
                "разница высот не меняет сектор");

            Check(!Facing.IsFromBehind(null, Vector3.zero), "пустая цель не падает");
            Check(!Facing.IsFromBehind(victim.transform, Vector3.zero),
                "совпадение позиций не считается ударом в спину");
        }

        // ================= рассказ =================

        private static void Narration()
        {
            Check(BattleNarrator.Build(null).Contains("без единого спора"),
                "пустой бой описывается как бой без споров");
            Check(BattleNarrator.Build(new List<string>()).Contains("без единого спора"),
                "пустой список тоже");

            var repeated = BattleNarrator.Build(new List<string> { "Марга ушла за добычей.", "Марга ушла за добычей.", "Марга ушла за добычей." });
            Same(CountOf(repeated, "Марга ушла за добычей."), 1,
                "повтор сворачивается в одну строку");
            Check(repeated.Contains("раз за разом"), "повтор помечен как привычка");

            var two = BattleNarrator.Build(new List<string> { "Первое.", "Второе." });
            Check(two.Contains("Первое.") && two.Contains("Второе."),
                "разные события не теряются");
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        // ================= голосование =================

        private static void Voting()
        {
            var warrior = MakeWarrior("Марга", SinType.Greed, 80f);
            if (warrior == null) { Fail("не удалось собрать воина для проверки голосования"); return; }

            var resolver = new BehaviourResolver();

            // Без приказа «подчиниться» не должно быть даже среди кандидатов:
            // иначе пустое подчинение может выиграть и выродиться в бездействие.
            var free = BaseContext(warrior);
            for (int i = 0; i < 5; i++)
            {
                var d = resolver.DecideDetailed(warrior, free);
                Check(d.Action != ActionType.ObeyCommand,
                    "без приказа воин не может «подчиниться»");
                Check(!d.RefusedCommand, "без приказа не бывает отказа");
            }

            // Колебание всегда исполняется как бездействие, но настоящий
            // лидер сохраняется — иначе нечем объяснить, между чем выбирали.
            var ordered = BaseContext(warrior);
            ordered.HasCommand = true;
            ordered.CommandType = CommandKind.Move.ToString();
            ordered.CommandLeavesFight = true;

            var decision = resolver.DecideDetailed(warrior, ordered);
            Check(decision.RefusedCommand
                    == (decision.Action != ActionType.ObeyCommand && !decision.Hesitated),
                "признак отказа согласован с выбранным действием");

            // Замереть и ослушаться — разные вещи: за оцепенение движок
            // не имеет права выдать воину и командиру память о непослушании.
            Check(!decision.Hesitated || !decision.RefusedCommand,
                "колебание не считается отказом");

            if (decision.Hesitated)
                Same(decision.Action, ActionType.Idle, "колеблющийся воин бездействует");

            Check(decision.Gap >= 0f, "разрыв между первым и вторым не отрицателен");
            Check(!string.IsNullOrEmpty(decision.TopModule) || decision.Hesitated,
                "у решения есть причина");

            // Приказ отойти — единственный, который исполняется и бегством.
            // Воин, побежавший от врага, когда велено отходить, сделал ровно
            // то, о чём просили: своим способом и не в ту точку, но сделал.
            // Пока движок этого не различал, он записывал такому отказ
            // и выдавал ему с командиром обоюдную память о непослушании —
            // то есть обвинял человека в том, что тот послушался.
            var retreat = BaseContext(warrior);
            retreat.HasCommand = true;
            retreat.CommandType = CommandKind.FallBack.ToString();
            retreat.CommandIsFallBack = true;
            retreat.CommandLeavesFight = true;

            // Положение, из которого бежит кто угодно: окружён, на последних
            // силах, врагов вчетверо. Иначе проверка проходила бы вхолостую.
            retreat.NearbyEnemies = 4;
            retreat.DangerLevel = 0.9f;
            retreat.Surrounded = true;
            retreat.CurrentHP = retreat.MaxHP * 0.2f;

            var underFire = resolver.DecideDetailed(warrior, retreat);
            Same(underFire.Action, ActionType.Flee,
                "окружённый и почти добитый воин выбирает бегство");
            Check(!underFire.RefusedCommand,
                "бегство по приказу отойти не считается отказом");

            // Тот же выбор при обычном приказе «иди туда» отказом остаётся:
            // там бегство — не исполнение, а замена приказа своим решением.
            // Контекст собираем заново: DecisionContext — класс, и присваивание
            // отдало бы ту же ссылку, а не копию.
            var ploughOn = BaseContext(warrior);
            ploughOn.HasCommand = true;
            ploughOn.CommandType = CommandKind.Move.ToString();
            ploughOn.CommandLeavesFight = true;
            ploughOn.NearbyEnemies = 4;
            ploughOn.DangerLevel = 0.9f;
            ploughOn.Surrounded = true;
            ploughOn.CurrentHP = ploughOn.MaxHP * 0.2f;

            var underFireMove = resolver.DecideDetailed(warrior, ploughOn);
            Check(underFireMove.Action != ActionType.Flee || underFireMove.RefusedCommand,
                "бегство вместо приказа «иди туда» остаётся отказом");

            // Одинаковый вход — одинаковый выход. Детерминизм заявлен
            // как сильная сторона движка; проверяем, что он есть.
            var a = resolver.DecideDetailed(warrior, BaseContext(warrior));
            var b = resolver.DecideDetailed(warrior, BaseContext(warrior));
            Same(a.Action, b.Action, "одинаковый вход даёт одинаковый выход");
            Near(a.Gap, b.Gap, "и одинаковый разрыв");

            // Жадный при добыче под ногами не должен вести себя как святой.
            var greedy = MakeWarrior("Скряга", SinType.Greed, 95f);
            var generous = MakeWarrior("Щедрый", SinType.Greed, -95f);
            if (greedy != null && generous != null)
            {
                var loot = BaseContext(greedy);
                loot.NearbyLoot = 4;
                var g1 = resolver.DecideDetailed(greedy, loot);
                var g2 = resolver.DecideDetailed(generous, loot);
                Check(g1.Action != g2.Action || Mathf.Abs(g1.Gap - g2.Gap) > 0.01f,
                    "противоположные характеры решают по-разному");
            }
        }

        private static DecisionContext BaseContext(Warrior warrior)
        {
            return new DecisionContext
            {
                CurrentHP = warrior.MaxHP,
                MaxHP = warrior.MaxHP,
                NearbyEnemies = 2,
                NearbyAllies = 1,
                DangerLevel = 0.3f,
                RelationshipWithCommander = 50f,
                RecentMemories = new List<MemoryRecord>()
            };
        }

        private static Warrior MakeWarrior(string name, SinType sin, float intensity)
        {
            var go = NewObject(name);
            var warrior = go.AddComponent<Warrior>();
            warrior.Initialize(new SoulData(name, sin, MoralType.Neutral, 1, intensity),
                ShellType.Skeleton, new RelationshipSystem(null));
            return warrior;
        }

        /// <summary>Временный объект, который находит поиск по сцене.</summary>
        private static GameObject Visible(string name)
        {
            var go = new GameObject("SelfCheck_" + name);
            _temp.Add(go);
            return go;
        }

        private static GameObject NewObject(string name)
        {
            var go = new GameObject("SelfCheck_" + name);
            go.hideFlags = HideFlags.HideAndDontSave;
            _temp.Add(go);
            return go;
        }

        // ================= правила текста =================

        private static readonly Regex Digit = new Regex(@"\d");

        /// <summary>
        /// Навык командования. Проверяем ровно то, что он обещает:
        /// он решает, скольких уведут, и ничего больше.
        /// </summary>
        private static void Commanding()
        {
            Same(Leadership.SquadSize(0f), Leadership.PrivateSquad,
                "без опыта уводит троих");
            Same(Leadership.SquadSize(100f), Leadership.MaxSquad,
                "полный опыт уводит двенадцать");
            Check(!Leadership.IsExperienced(0f), "рядовой не помечен командиром");
            Check(Leadership.IsExperienced(25f), "с опытом 25 уже командир");

            // Края шкалы не должны ломать список совета.
            Same(Leadership.SquadSize(-10f), Leadership.PrivateSquad,
                "отрицательный опыт не уводит меньше троих");
            Same(Leadership.SquadSize(1000f), Leadership.MaxSquad,
                "опыт выше сотни не уводит больше предела");

            // Порог миссии доли 3: пятеро. Рядовой не проходит, и это
            // единственное объяснение, зачем игроку опытный старший.
            Check(!Leadership.CanLead(0f, 5), "рядовой не поведёт пятерых");
            Check(Leadership.CanLead(25f, 5), "самый слабый из опытных поведёт пятерых");

            // Цифр игроку не показываем нигде, включая эти строки.
            foreach (float skill in new[] { 0f, 25f, 55f, 90f })
            {
                string line = Leadership.Describe(skill);
                Check(!HasDigit(line), $"навык описан без цифр: «{line}»");
            }
            Check(!HasDigit(Leadership.Shortfall(0f, 5)),
                "нехватка навыка описана без цифр");
        }

        /// <summary>
        /// Отправка отряда и его возвращение. Проверяем обещание сценария:
        /// «пятеро уходят, остаются Карган и трое», а в эпилоге состав
        /// зависит от того, кого игрок поставил старшим.
        /// </summary>
        private static void Epilogue()
        {
            var saved = new List<SquadRoster.Member>(SquadRoster.Members);

            try
            {
                SquadRoster.Set(CampLike());
                SquadRoster.ChooseCommander("Опытный А");
                SquadRoster.SendAway("Опытный А", 5);

                int away = 0, stayed = 0;
                bool guardStayed = false, otherLeadersStayed = true;

                foreach (var m in SquadRoster.Members)
                {
                    if (m.IsAway) { away++; continue; }
                    stayed++;
                    if (!string.IsNullOrEmpty(m.Unavailable)) guardStayed = true;
                }

                foreach (var m in SquadRoster.Away)
                    if (Leadership.IsExperienced(m.Leadership) && !m.IsCommander)
                        otherLeadersStayed = false;

                Same(away, 5, "с командиром ушли пятеро");
                Same(stayed, 4, "в лагере остались четверо");
                Check(guardStayed, "телохранитель не уходит");
                Check(otherLeadersStayed, "прочие опытные остаются в лагере");

                // Ушедших нет ни в одной сцене. Если Remember их забудет,
                // возвращаться в эпилоге будет некому.
                SquadRoster.Remember(new List<Warrior>());
                int stillAway = 0;
                foreach (var m in SquadRoster.Away) stillAway++;
                Same(stillAway, 5, "ушедшие пережили смену сцены");

                // Исход вылазки больше не таблица, а настоящий бой
                // (Gameplay/Expedition). Значит и проверять надо не число
                // из таблицы, а свойства боя.
                var went = new List<SquadRoster.Member>();
                foreach (var m in SquadRoster.Away) went.Add(m);

                var first = Expedition.Resolve(went);
                var again = Expedition.Resolve(went);

                Check(first.Count <= went.Count, "вернулось не больше, чем ушло");

                // Повторяемость. Главное свойство: объяснение «он свернул
                // за блеском» врало бы через раз, если бы тот же выбор
                // игрока давал разный исход.
                bool same = first.Count == again.Count;
                for (int i = 0; same && i < first.Count; i++)
                    if (first[i] != again[i]) same = false;
                Check(same, "вылазка повторяема: тот же состав — тот же исход");

                foreach (var name in first)
                {
                    bool known = false;
                    foreach (var m in went) if (m.Name == name) { known = true; break; }
                    Check(known, $"вернулся тот, кто уходил: {name}");
                }

                Check(Expedition.Resolve(new List<SquadRoster.Member>()).Count == 0,
                    "пустой отряд не возвращает никого");

                // Догадка Каргана на сцене 3 и рассказ на сцене 8 растут
                // из одного греха. Разойдись они — доля 3 обещала бы исход,
                // которого не будет.
                foreach (SinType sin in Enum.GetValues(typeof(SinType)))
                {
                    string guess = Homecoming.Guess(sin);
                    Check(!string.IsNullOrEmpty(guess), $"{sin}: догадка есть");
                    Check(!HasDigit(guess), $"{sin}: догадка без цифр");
                }

                Check(Homecoming.Guess(SinType.Sloth) != Homecoming.Guess(SinType.Wrath)
                   && Homecoming.Guess(SinType.Wrath) != Homecoming.Guess(SinType.Greed),
                    "три канонных греха гадают по-разному");
            }
            finally
            {
                SquadRoster.Set(saved);
            }
        }

        /// <summary>
        /// Куда ложится взгляд игрока и что значит «подойти к столу»
        /// (docs/09-PROLOGUE.md §4, сцена 2).
        ///
        /// Ошибка здесь тихая вдвойне: совет, открывшийся сам на первом
        /// кадре, выглядит ровно так же, как совет, к которому игрок
        /// подошёл. Разницу видно только по тому, что игрок ничего не делал.
        /// </summary>
        private static void Approach()
        {
            // Взгляд ровно вниз падает под ноги.
            Check(CampFocus.TryGroundPoint(new Vector3(2f, 4f, -3f),
                    Vector3.down, 0f, out var under), "взгляд вниз даёт точку");
            Near(under.x, 2f, "вниз: точка под ногами по X");
            Near(under.z, -3f, "вниз: точка под ногами по Z");

            // Горизонт не пересекает землю нигде. Наивная формула вернула бы
            // сюда ноль и решила бы, что игрок стоит у костра — то есть
            // почти у стола.
            Check(!CampFocus.TryGroundPoint(new Vector3(0f, 4f, 0f),
                    Vector3.forward, 0f, out _), "горизонт не даёт точки");
            Check(!CampFocus.TryGroundPoint(new Vector3(0f, 4f, 0f),
                    new Vector3(0f, 1f, 1f), 0f, out _), "взгляд вверх не даёт точки");
            Check(!CampFocus.TryGroundPoint(new Vector3(0f, 0f, 0f),
                    new Vector3(0f, -1f, 1f), 0f, out _), "камера на земле не даёт точки");

            // Наклон 45 с высоты 4 кладёт точку ровно в четырёх впереди.
            Check(CampFocus.TryGroundPoint(new Vector3(0f, 4f, 0f),
                    new Vector3(0f, -1f, 1f), 0f, out var slant), "наклон даёт точку");
            Near(slant.z, 4f, "наклон 45 с высоты 4 даёт 4 вперёд");

            // Высота в «подойти» не участвует.
            Near(CampFocus.GroundDistance(new Vector3(0f, 100f, 0f),
                    new Vector3(3f, 0f, 4f)), 5f, "расстояние считается по земле");

            // Выключенная проверка не должна выглядеть пройденной.
            Check(!CampFocus.Reached(new Vector3(0f, 4f, 0f), Vector3.down,
                    Vector3.zero, 0f), "нулевой радиус никого не пускает");

            // Постановка лагеря из DemoSceneBuilder: открывающий кадр
            // не дотягивается до шара, а подойдя — дотягивается.
            var eye = new Vector3(0f, 3.5f, -12.5f);
            var forward = Quaternion.Euler(13f, 0f, 0f) * Vector3.forward;
            var ball = new Vector3(3.0f, 1.22f, 2.2f);

            Check(!CampFocus.Reached(eye, forward, ball, CampFocus.TableReach),
                "открывающий кадр не открывает совет сам");
            Check(CampFocus.Reached(eye + new Vector3(3.0f, 0f, 4.8f), forward,
                    ball, CampFocus.TableReach), "подойдя, игрок стол достаёт");
        }

        /// <summary>
        /// Лестница прозрачности (00-GDD.md §7).
        ///
        /// Проверяем не «выключено ли», а «заперто ли». Разница в том,
        /// что выключенное однажды включат по ошибке — и тогда игрок
        /// увидит очки, веса и разрыв, то есть ровно то, чего правило
        /// «игрок не видит цифр» не допускает нигде.
        ///
        /// Состояние восстанавливаем: в редакторе трассировка открыта,
        /// и отбирать её у автора после проверки нельзя.
        /// </summary>
        private static void Ladder()
        {
            var savedLevel = Transparency.Level;
            bool savedDev = Transparency.DeveloperUnlocked;

            try
            {
                Transparency.Reset();

                Same(Transparency.Level, Clarity.Log, "по умолчанию — с журналом");
                Check(Transparency.Shows(Clarity.Icons), "значки видны");
                Check(Transparency.Shows(Clarity.Tooltips), "подсказки видны");
                Check(Transparency.Shows(Clarity.Log), "журнал виден");
                Check(!Transparency.Shows(Clarity.Trace), "трассировка игроку не видна");

                Same(Transparency.Set(Clarity.Trace), Clarity.Log,
                    "просьба о трассировке зажата до журнала");
                Check(!Transparency.Shows(Clarity.Trace),
                    "и после просьбы трассировки нет");

                Transparency.Set(Clarity.Icons);
                Check(Transparency.Shows(Clarity.Icons), "на первой значки видны");
                Check(!Transparency.Shows(Clarity.Tooltips), "на первой подсказок нет");
                Check(!Transparency.Shows(Clarity.Log), "на первой журнала нет");

                Transparency.Set(Clarity.Silent);
                Check(!Transparency.Shows(Clarity.Icons), "молча — значит молча");

                Transparency.SetDeveloper(true);
                Same(Transparency.Set(Clarity.Trace), Clarity.Trace,
                    "разработчик доходит до трассировки");
                Check(Transparency.Shows(Clarity.Trace), "и видит её");

                // Заперев замок, ступень обязана опуститься сама.
                Transparency.SetDeveloper(false);
                Check(Transparency.Level <= Clarity.Log, "замок опустил ступень");
                Check(!Transparency.Shows(Clarity.Trace), "и трассировки больше нет");

                // Названия ступеней игрок читает глазами — значит без цифр.
                foreach (Clarity c in Enum.GetValues(typeof(Clarity)))
                    Check(!HasDigit(Transparency.Describe(c)),
                        $"{c}: название без цифр");
            }
            finally
            {
                Transparency.SetDeveloper(savedDev);
                Transparency.Set(savedLevel);
            }
        }

        /// <summary>Состав вроде лагерного: страж, трое опытных, пятеро рядовых.</summary>
        private static List<SquadRoster.Member> CampLike()
        {
            var list = new List<SquadRoster.Member>
            {
                new SquadRoster.Member { Name = "Страж", Sin = SinType.Pride,
                    Leadership = 90f, Unavailable = "телохранитель" },
                new SquadRoster.Member { Name = "Опытный А", Sin = SinType.Sloth, Leadership = 55f },
                new SquadRoster.Member { Name = "Опытный Б", Sin = SinType.Greed, Leadership = 40f },
                new SquadRoster.Member { Name = "Опытный В", Sin = SinType.Wrath, Leadership = 25f },
            };

            for (int i = 0; i < 5; i++)
                list.Add(new SquadRoster.Member { Name = $"Рядовой {i + 1}", Sin = SinType.Envy });

            return list;
        }

        private static bool HasDigit(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text) if (char.IsDigit(c)) return true;
            return false;
        }

        private static void TextRules()
        {
            var warrior = MakeWarrior("Карган", SinType.Pride, 70f);
            if (warrior == null) { Fail("не удалось собрать воина для проверки текста"); return; }

            var context = BaseContext(warrior);
            context.UnpaidMissions = 3;
            context.Surrounded = true;
            context.Fatigue = 0.7f;
            context.IsExhausted = true;
            context.TargetBackExposed = true;

            string[] modules =
            {
                "Greed", "Pride", "Wrath", "Envy", "Lust", "Gluttony",
                "Sloth", "Patience", "Fear", "Loyalty", "Morality", "Memory", "Virtue", ""
            };

            var actions = (ActionType[])Enum.GetValues(typeof(ActionType));

            foreach (var module in modules)
            {
                foreach (var action in new[]
                {
                    ActionType.Attack, ActionType.SaveAlly, ActionType.Loot,
                    ActionType.Flee, ActionType.Idle, ActionType.ObeyCommand
                })
                {
                    foreach (bool refused in new[] { false, true })
                    foreach (bool hesitated in new[] { false, true })
                    {
                        var decision = new Decision
                        {
                            Action = hesitated ? ActionType.Idle : action,
                            TopContender = action,
                            RunnerUp = ActionType.Flee,
                            TopModule = module,
                            Gap = hesitated ? 3f : 40f,
                            Hesitated = hesitated,
                            RefusedCommand = refused
                        };

                        string explain = PhraseGenerator.Explain(warrior, context, decision);
                        string log = PhraseGenerator.LogLine(warrior, context, decision);

                        if (Digit.IsMatch(explain))
                        { Fail($"в подсказке появилась цифра: «{explain}»"); return; }
                        if (Digit.IsMatch(log))
                        { Fail($"в журнале появилась цифра: «{log}»"); return; }

                        if (string.IsNullOrWhiteSpace(explain))
                        { Fail($"пустая подсказка: модуль {module}, действие {action}"); return; }
                        if (string.IsNullOrWhiteSpace(log))
                        { Fail($"пустая строка журнала: модуль {module}, действие {action}"); return; }

                        // Одинаковое решение всегда описывается одинаково,
                        // иначе игрок не может учиться на объяснениях.
                        if (explain != PhraseGenerator.Explain(warrior, context, decision))
                        { Fail("подсказка не воспроизводится дословно"); return; }
                    }
                }
            }
            _report.Passed++; // весь перебор прошёл без цифр и пустот

            // Все действия вообще, включая умения: генератор не должен
            // спотыкаться о значение, которого не ждал.
            foreach (var action in actions)
            {
                var decision = new Decision { Action = action, TopContender = action, RunnerUp = ActionType.Idle, TopModule = "Wrath", Gap = 30f };
                string s = PhraseGenerator.Explain(warrior, context, decision);
                if (string.IsNullOrWhiteSpace(s) || Digit.IsMatch(s))
                { Fail($"генератор фраз споткнулся на действии {action}: «{s}»"); return; }
            }
            _report.Passed++;

            // Описание души и оболочки игрок тоже видит.
            string spectra = warrior.Soul.GetSpectraDescription();
            Check(!Digit.IsMatch(spectra), $"в описании души появилась цифра: «{spectra}»");

            // Пророчество — тем более: это первое, что читает игрок при сборке.
            string prophecy = TemperamentPredictor.Describe(warrior);
            Check(!Digit.IsMatch(prophecy), $"в пророчестве появилась цифра: «{prophecy}»");
            Check(!string.IsNullOrWhiteSpace(prophecy), "пророчество не пустое");

            var lines = TemperamentPredictor.Predict(warrior);
            Same(lines.Count, 4, "пророчество состоит из четырёх положений");
            foreach (var line in lines)
                Check(!string.IsNullOrWhiteSpace(line.Outcome), $"положение «{line.Situation}» без исхода");
        }
    }
}
