// Assets/Scripts/Editor/ExpeditionProbe.cs
// Проверка экспедиции в настоящей Unity — путём игрока, без заглушек:
// лагерь (канон отряда) → склеп → отряд уходит в шар → бой AOS по ходам → возвращение → запись похода.
// Тесты ветки ботов (Tools/expeditions) проверяют те же правила на подменах Unity; здесь — сама игра.
//
// Верстак: open Assets/Scenes/Prologue_Camp.unity → play → call Sinbinder.EditorTools.ExpeditionProbe.Begin
// Журнал: Logs/expedition-probe.txt; в конце открыт просмотр записи похода — для кадра (shot).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class ExpeditionProbe
    {
        private const string LogPath = "Logs/expedition-probe.txt";
        private enum Step { Camp, Crypt, Send, Journey, Done }

        private static Step _step;
        private static double _since, _nextStatus;
        private static string _lastReport, _lastPhases;
        private static int _archiveBefore, _goldBefore;
        private static readonly Dictionary<string, SquadRoster.Member> Before = new Dictionary<string, SquadRoster.Member>();

        /// <summary>Итог для верстака: «идёт», «ПРОЙДЕНО …» или «НЕ ПРОЙДЕНО …».</summary>
        public static string Result { get; private set; } = "не запускалась";

        private static bool _fight;

        /// <summary>Поход с боем наверняка: точка без развилки, где стража есть всегда (меньшая — первой).</summary>
        public static string BeginFight() { _fight = true; return Start(); }

        /// <summary>Поход как у прогона демо: обоз первым — там старший сам решает, драться ли.</summary>
        public static string Begin() { _fight = false; return Start(); }

        /// <summary>
        /// Поход из полигона (Crypt_Test): послать с его доски и ждать там же (автор, 26.11: «отряд ждал в полигоне»).
        /// Верстак: open Assets/Scenes/Crypt_Test.unity → play → call Sinbinder.EditorTools.ExpeditionProbe.Polygon
        /// </summary>
        public static string Polygon()
        {
            _fight = false;
            string r = Start();
            if (Board() == null) { Finish("НЕ ПРОЙДЕНО: в сцене нет доски вылазок"); return Result; }
            _step = Step.Send;
            return r + " (полигон)";
        }

        /// <summary>Доска склепа, а без склепа — доска этой сцены (полигон).</summary>
        private static MissionBoard Board()
        {
            if (CryptMap.Board != null) return CryptMap.Board;
            var scene = SceneManager.GetActiveScene();
            foreach (var b in Object.FindObjectsByType<MissionBoard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null && b.gameObject.scene == scene) return b;
            return null;
        }

        private static string Start()
        {
            if (!EditorApplication.isPlaying) return "нужен режим игры (play)";
            File.WriteAllText(LogPath, "", new UTF8Encoding(false));
            Before.Clear();
            _lastReport = _lastPhases = null;
            _step = Step.Camp;
            _since = EditorApplication.timeSinceStartup;
            Result = "идёт";
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Write($"=== ЭКСПЕДИЦИЯ В UNITY, сцена {SceneManager.GetActiveScene().name}");
            return "запущена";
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish("НЕ ПРОЙДЕНО: игра остановлена посреди проверки"); return; }
            double now = EditorApplication.timeSinceStartup, spent = now - _since;
            switch (_step)
            {
                case Step.Camp:
                    // Стартовое окно ждёт ответа, как ждёт человека (прогон демо отвечает так же).
                    if (UI.StartPanel.Waiting) { UI.StartPanel.ChooseFresh(); Write("стартовое окно: «начать заново»"); _since = now; return; }
                    if (spent < 3) return;
                    if (!SquadRoster.HasSquad) { if (spent > 90) Finish("НЕ ПРОЙДЕНО: в лагере за 90 с не завёлся отряд"); return; }
                    Write("отряд " + Roster());
                    // Уже в склепе (второй поход подряд) — не перезагружать: склеп жив, отряд дома.
                    if (SceneManager.GetActiveScene().name != "Crypt_Entrance") SceneManager.LoadScene("Crypt_Entrance");
                    Next(Step.Crypt);
                    return;

                case Step.Crypt:
                    if (SceneManager.GetActiveScene().name != "Crypt_Entrance" || CryptMap.Board == null)
                    {
                        if (spent > 90) Finish("НЕ ПРОЙДЕНО: склеп или доска вылазок не появились за 90 с");
                        return;
                    }
                    if (spent < 3) return;   // дать склепу расставить отряд
                    int inScene = 0;
                    foreach (var w in MissionBoard.Squad()) if (w != null) inScene++;
                    Write($"склеп: доска вылазок есть, воинов отряда в сцене {inScene}");
                    Next(Step.Send);
                    return;

                case Step.Send:
                {
                    var board = Board();
                    foreach (var m in SquadRoster.Members) Before[m.Name] = m;
                    _archiveBefore = ExpeditionReplayArchive.History.Count;
                    _goldBefore = Inventory.PlayerInventory.Instance != null ? Inventory.PlayerInventory.Instance.Gold : -1;
                    // Тот же выбор, что у прогона демо (DemoWalkthrough.SendFromCrypt): обоз первым.
                    var missions = new List<Mission>(board.Missions);
                    if (_fight)
                        missions.Sort((a, b) => a.Junction != b.Junction
                            ? (a.Junction == Junction.None ? -1 : b.Junction == Junction.None ? 1 : 0)
                            : a.Guards.CompareTo(b.Guards));
                    else
                        missions.Sort((a, b) => (b.Junction == Junction.Caravan).CompareTo(a.Junction == Junction.Caravan));
                    for (int pass = 0; pass < 2; pass++)
                        foreach (var mission in missions)
                        {
                            if (pass == 0 && !board.EnoughPeople(mission)) continue;
                            foreach (var m in SquadRoster.Members)
                            {
                                if (!string.IsNullOrEmpty(board.WhyNot(m, mission))) continue;
                                Write($"отправка: «{mission.Name}», старший {m.Name}, развилка {mission.Junction}, стража {mission.Guards}");
                                Write("  доска: " + board.Send(mission, m.Name, null));
                                Next(Step.Journey);
                                return;
                            }
                        }
                    Finish("НЕ ПРОЙДЕНО: послать не на что — ни одна точка не подходит ни одному старшему");
                    return;
                }

                case Step.Journey:
                {
                    var board = Board();
                    if (board == null) { Finish("НЕ ПРОЙДЕНО: доска вылазок пропала посреди похода"); return; }
                    var phases = new StringBuilder();
                    foreach (var j in MissionBoard.Journeys)
                        phases.Append($"[{j.Commander}: фаза {j.Phase}{(j.CombatBlocked ? ", бой ЗАБЛОКИРОВАН" : "")}{(j.ReturnBlocked ? ", возврат ЗАБЛОКИРОВАН" : "")}] ");
                    string p = phases.ToString();
                    if (p != _lastPhases) { _lastPhases = p; Write($"+{spent:F0} с  {(p.Length > 0 ? p : "в пути никого")}"); }
                    if (board.LastReport != _lastReport) { _lastReport = board.LastReport; Write("  отчёт: " + _lastReport); }
                    // Окно платы склепа ставит игру на паузу и ждёт игрока — с ним стоят и часы похода.
                    // Проверка выбирает «не платить» (кнопка окна) и пишет это в журнал.
                    var salary = Object.FindFirstObjectByType<UI.SalaryPanelUI>();
                    if (salary != null && UI.Modal.Any && Core.GamePauseController.Instance != null
                        && Core.GamePauseController.Instance.IsPaused)
                    {
                        typeof(UI.SalaryPanelUI).GetMethod("Withhold",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(salary, null);
                        Write($"+{spent:F0} с  окно платы отряду поставило игру на паузу — проверка выбрала «Не платить никому»");
                    }
                    if (now >= _nextStatus)
                    {
                        _nextStatus = now + 15;
                        var pause = Core.GamePauseController.Instance;
                        Write($"  часы: timeScale {Time.timeScale}, пауза {(pause != null && pause.IsPaused ? "да" : "нет")}, "
                            + $"часы мира {(ExpeditionWorldClock.IsRunning ? "идут" : "нет — доска сама")}"
                            + string.Concat(Remaining()));
                    }
                    if (MissionBoard.Journeys.Count == 0) { After(board); return; }
                    if (spent > 300) Finish("НЕ ПРОЙДЕНО: отряд не вернулся за 300 с");
                    return;
                }
            }
        }

        private static IEnumerable<string> Remaining()
        {
            foreach (var j in MissionBoard.Journeys) yield return $"; {j.Commander}: осталось {j.SecondsRemaining:F0} с";
        }

        private static void After(MissionBoard board)
        {
            Write("вернулись. отряд " + Roster());
            var changes = new List<string>();
            foreach (var m in SquadRoster.Members)
            {
                if (!Before.TryGetValue(m.Name, out var was)) { changes.Add($"{m.Name}: новый"); continue; }
                if (Mathf.Abs(was.HealthFraction - m.HealthFraction) > 0.001f || was.Pocket != m.Pocket
                    || Mathf.Abs(was.Loyalty - m.Loyalty) > 0.001f || was.IsAway != m.IsAway)
                    changes.Add($"{m.Name}: здоровье {was.HealthFraction:P0}→{m.HealthFraction:P0}, кошель {was.Pocket}→{m.Pocket}, "
                              + $"верность {was.Loyalty:F0}→{m.Loyalty:F0}{(m.IsAway ? ", ВСЁ ЕЩЁ В ПУТИ" : "")}");
            }
            foreach (var name in Before.Keys)
                if (!SquadRoster.TryGet(name, out _)) changes.Add($"{name}: нет в отряде (пал?)");
            Write("перемены: " + (changes.Count > 0 ? string.Join("; ", changes) : "никаких"));
            int gold = Inventory.PlayerInventory.Instance != null ? Inventory.PlayerInventory.Instance.Gold : -1;
            Write($"золото Греховода {_goldBefore}→{gold}; записей походов {_archiveBefore}→{ExpeditionReplayArchive.History.Count}");

            int back = 0, hidden = 0;
            foreach (var w in MissionBoard.Squad()) if (w != null) { if (w.gameObject.activeInHierarchy) back++; else hidden++; }
            Write($"воины отряда в сцене: видны {back}, скрыты {hidden}");

            var replay = board.LastReplay;
            bool route = replay != null && replay.HasCompleteRoute;
            if (replay != null)
            {
                Write($"запись: {replay.Frames.Count} событий, полный путь (выход…возврат) {(route ? "да" : "НЕТ")}");
                foreach (var f in replay.Frames)
                    Write($"  {f.Sequence,2} {f.Kind,-15} ход {f.Round} {f.Actor}: {f.Description}"
                        + (f.Damage > 0 ? $" (урон {f.Damage:F1})" : "") + (f.TargetKilled ? " — цель пала" : ""));
                UI.ExpeditionReplayViewer.Open(replay);
            }
            else Write("запись: НЕТ");

            Finish(route && hidden == 0 ? "ПРОЙДЕНО: отряд ушёл, бой и событие решены, отряд вернулся, запись полная"
                                        : "НЕ ПРОЙДЕНО: " + (replay == null ? "нет записи похода" : !route ? "запись неполная" : "часть отряда не вернулась в сцену"));
        }

        private static string Roster()
        {
            var names = new List<string>();
            foreach (var m in SquadRoster.Members)
                names.Add($"{m.Name} ({m.HealthFraction:P0}{(m.IsAway ? ", в пути" : "")}{(string.IsNullOrEmpty(m.Unavailable) ? "" : ", " + m.Unavailable)})");
            return $"{names.Count}: " + string.Join(", ", names);
        }

        private static void Next(Step s) { _step = s; _since = EditorApplication.timeSinceStartup; }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            _step = Step.Done;
            Result = result;
            Write("=== " + result);
        }

        private static void Write(string line)
        {
            Debug.Log("[ЭКСПЕДИЦИЯ] " + line);
            File.AppendAllText(LogPath, line + "\n", new UTF8Encoding(false));
        }
    }
}
