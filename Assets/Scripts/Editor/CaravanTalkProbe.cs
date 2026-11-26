// Assets/Scripts/Editor/CaravanTalkProbe.cs
// Разговор у обоза — как игрок (автор, 26.11: «выбор во время диалога, как в Ведьмаке 3»; «не хватает более
// ручно-глазной проверки от тебя»). Лагерь → склеп → «Соляной обоз»; Греховод идёт к охране щелчком по земле,
// старший окликает его, F (тот же общий выбор, что у игрока) открывает разговор, ответы — по номерам, как цифры.
// Кадр на каждом шаге — в D:\Claude\обоз-разговор.
//
// Верстак: open Assets/Scenes/Prologue_Camp.unity ;; play ;; call Sinbinder.EditorTools.CaravanTalkProbe.Begin pass
//          (pass — отпустить, attack — напасть, toll — доля, если её можно взять) ;; wait 150 ;;
//          get Sinbinder.EditorTools.CaravanTalkProbe.Result
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class CaravanTalkProbe
    {
        private const string LogPath = "Logs/caravan-talk-probe.txt";
        private const string Shots = "D:/Claude/обоз-разговор";
        private enum Step { Camp, Crypt, Enter, Walk, Talk, Ask, Decide, Close, After, Done }

        private static Step _step;
        private static double _since, _nextClick;
        private static string _mode;
        private static int _shots;
        private static bool _hailSeen;
        private static int _goldBefore;
        private static readonly StringBuilder Log = new StringBuilder();

        public static string Result { get; private set; } = "не запускалась";

        public static string Begin(string mode)
        {
            if (!EditorApplication.isPlaying) return "нужна игра (play) в лагере";
            _mode = string.IsNullOrEmpty(mode) ? "pass" : mode.Trim().ToLowerInvariant();
            Log.Clear();
            _shots = 0;
            _hailSeen = false;
            Directory.CreateDirectory(Shots);
            foreach (var f in Directory.GetFiles(Shots, "*.png")) File.Delete(f);
            Result = "идёт";
            Next(Step.Camp);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Write($"=== разговор у обоза: исход «{_mode}»");
            return "запущена: " + _mode;
        }

        private static void Next(Step s) { _step = s; _since = EditorApplication.timeSinceStartup; }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish("НЕ ПРОЙДЕНО: игра остановлена"); return; }
            if (EditorApplication.isPaused) return;
            double spent = EditorApplication.timeSinceStartup - _since;
            var hero = SinbinderPlayer.Exists ? SinbinderPlayer.Instance : null;
            // Окна, которые ждут игрока, — ответ, как у проверки личной экспедиции (PersonalExpeditionProbe).
            DismissSalary();
            if (AnswerLesson(hero)) return;
            switch (_step)
            {
                case Step.Camp:
                    if (UI.StartPanel.Waiting) { UI.StartPanel.ChooseFresh(); _since = EditorApplication.timeSinceStartup; return; }
                    if (spent < 3) return;
                    if (!SquadRoster.HasSquad) { if (spent > 90) Finish("НЕ ПРОЙДЕНО: отряд не завёлся"); return; }
                    if (SceneManager.GetActiveScene().name != "Crypt_Entrance") SceneManager.LoadScene("Crypt_Entrance");
                    Next(Step.Crypt);
                    return;

                case Step.Crypt:
                    if (SceneManager.GetActiveScene().name != "Crypt_Entrance" || CryptMap.Board == null || hero == null)
                    { if (spent > 90) Finish("НЕ ПРОЙДЕНО: склеп не появился"); return; }
                    if (CryptArrival.Running || !UI.SalaryPanelUI.Answered)
                    {
                        if (UI.Letterbox.CanSkip && !UI.Letterbox.Skipping) UI.Letterbox.Instance.SkipAll();
                        if (spent > 120) Finish("НЕ ПРОЙДЕНО: встреча в склепе не кончилась");
                        return;
                    }
                    if (spent < 2) return;
                    Next(Step.Enter);
                    return;

                case Step.Enter:
                {
                    Mission caravan = default; bool found = false;
                    foreach (var m in CryptMap.Board.Missions) if (m.Name == "Соляной обоз") { caravan = m; found = true; }
                    if (!found) { Finish("НЕ ПРОЙДЕНО: нет «Соляного обоза»"); return; }
                    var names = new List<string>();
                    foreach (var m in SquadRoster.Members) if (names.Count < caravan.Squad - 1 && SquadRoster.CanGo(m)) names.Add(m.Name);
                    if (!PersonalExpedition.Enter(caravan, names, out var error)) { Finish("НЕ ПРОЙДЕНО: вход отказан: " + error); return; }
                    _goldBefore = Inventory.PlayerInventory.Instance != null ? Inventory.PlayerInventory.Instance.Gold : -1;
                    var story = Story();
                    Write($"вход к обозу, с Греховодом {string.Join(", ", names)}; история «{(story != null ? story.Id : "—")}»");
                    Next(Step.Walk);
                    return;
                }

                case Step.Walk:
                {
                    if (spent < 2) return;
                    var captain = Captain();
                    if (captain == null) { Finish("НЕ ПРОЙДЕНО: старшего охраны нет (обоз не мирный?)"); return; }
                    float d = Flat(hero.transform.position - captain.transform.position);
                    // Как игрок: щелчок по земле рядом со старшим, раз в 2 с — пока он не в шаге.
                    if (EditorApplication.timeSinceStartup >= _nextClick)
                    {
                        _nextClick = EditorApplication.timeSinceStartup + 2.0;
                        var legs = hero.GetComponent<UnitMover>();
                        if (legs != null) legs.CommandMove(Vector3.MoveTowards(captain.transform.position, hero.transform.position, 3f));
                    }
                    if (!_hailSeen && Hailed())
                    {
                        _hailSeen = true;
                        Shot("оклик");
                        Write($"оклик: старший {captain.DisplayName} в {d:F1} м");
                    }
                    var offer = Offers().Find(o => o.label.StartsWith("F — говорить"));
                    if (offer.act != null)
                    {
                        if (!_hailSeen) Write("  НЕ ПРОЙДЕНО (мягко): разговор доступен, а оклика не было");
                        Write($"на F предлагают: «{offer.label}» (до старшего {d:F1} м)");
                        offer.act();
                        Next(Step.Talk);
                        return;
                    }
                    if (spent > 60) Finish($"НЕ ПРОЙДЕНО: за 60 с F не предложило разговор (до старшего {d:F1} м)");
                    return;
                }

                case Step.Talk:
                    if (spent < 2.5) return;   // камера наезжает, строка печатается
                    Write("разговор: " + PersonalExpedition.TalkNow);
                    Shot("разговор — начало");
                    if (!PersonalExpedition.TalkNow.StartsWith("Open")) { Finish("НЕ ПРОЙДЕНО: разговор не открылся"); return; }
                    PersonalExpedition.TalkSay(1);   // первый расспрос
                    Next(Step.Ask);
                    return;

                case Step.Ask:
                {
                    if (spent < 2.5) return;
                    Write("расспрос: " + PersonalExpedition.TalkNow);
                    Shot("разговор — расспрос");
                    var story = Story();
                    string want = _mode == "attack" ? story.AttackAsk : _mode == "toll" && !string.IsNullOrEmpty(story.TollAsk) ? story.TollAsk : story.PassAsk;
                    int pick = Option(Sinbinder.Core.Loc.T(want));
                    if (pick < 1) { Finish($"НЕ ПРОЙДЕНО: среди ответов нет «{want}»"); return; }
                    Write($"ответ {pick}: «{want}»");
                    PersonalExpedition.TalkSay(pick);
                    Next(Step.Decide);
                    return;
                }

                case Step.Decide:
                    if (spent < 2.5) return;
                    Write("ответ старшего: " + PersonalExpedition.TalkNow);
                    Shot("разговор — исход");
                    PersonalExpedition.TalkSay(1);   // «К бою» / «Уйти» / «Взять долю и уйти»
                    Next(Step.Close);
                    return;

                case Step.Close:
                    if (spent < 1.5) return;
                    Write("после разговора: " + PersonalExpedition.TalkNow + "; пауза " + (Core.GamePauseController.Instance != null && Core.GamePauseController.Instance.IsPaused ? "ДА" : "нет"));
                    Shot("после разговора");
                    Next(Step.After);
                    return;

                case Step.After:
                {
                    if (spent < 5) return;
                    Shot("через 5 с");
                    int enemies = 0, neutral = 0;
                    foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                        if (w != null && !w.IsDead && !(w is SinbinderPlayer)) { if (w.Team == Team.Enemy) enemies++; else if (w.Team == Team.Neutral) neutral++; }
                    int gold = Inventory.PlayerInventory.Instance != null ? Inventory.PlayerInventory.Instance.Gold : -1;
                    var state = State();
                    Write($"итог: врагов {enemies}, мирных {neutral}; золото {_goldBefore}→{gold}; обоз: пропущен {state?.CaravanPassed}, доля {state?.Tolled}, напали {state?.CaravanAssaulted}");
                    Object.FindFirstObjectByType<PersonalExpedition>()?.Exit();
                    var plan = ExpeditionMapPlan.ForName("Соляной обоз");
                    var card = plan != null ? PersonalExpedition.KnownAbout(plan) : new List<string>();
                    Write("карточка места у шара: " + string.Join(" / ", card));
                    bool ok = _mode == "attack" ? enemies > 0 && state != null && state.CaravanAssaulted
                            : state != null && state.CaravanPassed && (_mode != "toll" || gold > _goldBefore || string.IsNullOrEmpty(Story().TollAsk));
                    Finish(ok ? $"ПРОЙДЕНО: оклик {(_hailSeen ? "был" : "НЕ был")}, разговор, расспрос, исход «{_mode}» наступил после реплики старшего; кадров {_shots}"
                              : "НЕ ПРОЙДЕНО: исход не наступил — " + Log.ToString().Split('\n').Length + " строк журнала");
                    return;
                }
            }
        }

        private static string _lessonSeen;
        private static double _lessonNext;

        private static bool AnswerLesson(SinbinderPlayer hero)
        {
            if (!Lesson.Holding) { _lessonSeen = null; return false; }
            double now = EditorApplication.timeSinceStartup;
            string kind = Lesson.Now.ToString();
            if (kind != _lessonSeen) { _lessonSeen = kind; Write($"урок «{kind}» ждёт игрока — отвечаю, как прогон демо"); }
            if (now < _lessonNext) return true;
            _lessonNext = now + 0.5;
            if (Lesson.Now == LessonKind.Refusal) { UI.WarriorTooltipUI.Pinned = Lesson.Partner; return true; }
            if (Lesson.Now == LessonKind.Backstab)
            {
                var flanker = Lesson.Flanker;
                if (flanker != null) flanker.IssueCommand(CommandKind.Move, Lesson.Mark);
                return true;
            }
            hero?.GetComponent<UnitMover>()?.CommandMove(Lesson.Target);
            return true;
        }

        private static void DismissSalary()
        {
            if (SoulCourt.Sitting && UI.Letterbox.CanSkip && !UI.Letterbox.Skipping) UI.Letterbox.Instance.SkipAll();
            var salary = Object.FindFirstObjectByType<UI.SalaryPanelUI>();
            if (salary == null || !UI.Modal.Any || Core.GamePauseController.Instance == null || !Core.GamePauseController.Instance.IsPaused) return;
            typeof(UI.SalaryPanelUI).GetMethod("Withhold", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(salary, null);
            Write("окно платы — «Не платить никому»");
        }

        private static Warrior Captain()
        {
            var live = Object.FindFirstObjectByType<PersonalExpedition>();
            return live == null ? null : typeof(PersonalExpedition).GetMethod("Captain", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(live, null) as Warrior;
        }

        private static bool Hailed()
        {
            var live = Object.FindFirstObjectByType<PersonalExpedition>();
            return live != null && (bool)(typeof(PersonalExpedition).GetField("_hailed", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(live) ?? false);
        }

        private static SavedPersonalExpedition State()
        {
            foreach (var s in PersonalExpedition.SnapshotProgress()) if (s.Id == "expedition:salt-road") return s;
            return null;
        }

        private static CaravanStory Story()
        {
            var s = State();
            return CaravanStories.Get(s != null ? s.Story : 0);
        }

        /// <summary>Номер ответа, в котором есть эта строка (как видит игрок: «2 · …»).</summary>
        private static int Option(string text)
        {
            string now = PersonalExpedition.TalkNow;
            int bar = now.IndexOf(" | ");
            if (bar < 0) return -1;
            var labels = now.Substring(bar + 3).Split(new[] { " / " }, System.StringSplitOptions.None);
            for (int i = 0; i < labels.Length; i++) if (labels[i].Contains(text)) return i + 1;
            return -1;
        }

        private static List<(string label, System.Action act)> Offers()
        {
            var list = new List<(string label, System.Action act)>();
            PersonalExpedition.OfferInteractions((gaze, label, act) => list.Add((label, act)));
            return list;
        }

        private static void Shot(string name)
        {
            try { Utilets.Snapshot.Now(Shots, $"{_shots:00} {name}"); _shots++; } catch { }
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        private static void Write(string line)
        {
            Log.AppendLine(line);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, Log.ToString());
        }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            Result = result;
            Write("=== " + result);
            _step = Step.Done;
        }
    }
}
