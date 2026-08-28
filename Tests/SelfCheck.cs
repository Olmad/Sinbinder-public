// Assets/_Project/Scripts/Tests/SelfCheck.cs
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

            var repeated = BattleNarrator.Build(new List<string> { "Марга ушёл за добычей.", "Марга ушёл за добычей.", "Марга ушёл за добычей." });
            Same(CountOf(repeated, "Марга ушёл за добычей."), 1,
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
            ordered.CommandType = "Move";

            var decision = resolver.DecideDetailed(warrior, ordered);
            Check(decision.RefusedCommand == (decision.Action != ActionType.ObeyCommand),
                "признак отказа согласован с выбранным действием");
            if (decision.Hesitated)
                Same(decision.Action, ActionType.Idle, "колеблющийся воин бездействует");

            Check(decision.Gap >= 0f, "разрыв между первым и вторым не отрицателен");
            Check(!string.IsNullOrEmpty(decision.TopModule) || decision.Hesitated,
                "у решения есть причина");

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

        private static GameObject NewObject(string name)
        {
            var go = new GameObject("SelfCheck_" + name);
            go.hideFlags = HideFlags.HideAndDontSave;
            _temp.Add(go);
            return go;
        }

        // ================= правила текста =================

        private static readonly Regex Digit = new Regex(@"\d");

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
