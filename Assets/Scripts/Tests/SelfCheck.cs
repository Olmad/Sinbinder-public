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
                    },
                    new SquadRoster.Member
                    {
                        Name = "Второй", Sin = SinType.Sloth, Moral = MoralType.Pious,
                        Intensity = -30f, Loyalty = 71f,
                    },
                });

                Commitment.Set(true);

                var snapshot = SaveSystem.Snapshot();
                Same(snapshot.Squad.Count, 2, "снимок взял не весь отряд");

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

                var second = SquadRoster.Members[1];
                Near(second.Intensity, -30f, "добродетель вернулась грехом: "
                                           + "знак спектра потерян");

                Check(Commitment.On, "режим обязательств не пережил файл — "
                                   + "ответственную игру можно было бы открыть свободной");

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
