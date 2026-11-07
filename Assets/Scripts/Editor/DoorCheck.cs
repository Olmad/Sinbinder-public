#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Проверка двери в игру (07.11): новая игра начинается со сна Греховода.
    /// Путь человека, щелчками: лагерь → дверь → «Начать заново» → сон загружен →
    /// меню паузы → «Проснуться» → лагерь, и дверь второй раз не спрашивает.
    /// Прогоны лагеря отвечают на дверь в обход сна (StartPanel.ChooseFresh), а прогон сна
    /// входит в сон сразу — этот путь не ходил никто. Отчёт — Logs/door-check.txt;
    /// выход с кодом: 0 — прошло, 1 — нет.
    ///
    /// Запуск: Unity -batchmode -executeMethod Sinbinder.EditorTools.DoorCheck.Run (без -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class DoorCheck
    {
        private const string Active = "Sinbinder.DoorCheck.Active";
        private const string ReportPath = "Logs/door-check.txt";
        private const float Limit = 240f;

        private static float _t0 = -1f, _at;
        private static int _stage, _errors;
        private static bool _done, _woke;
        private static string NL => Environment.NewLine;

        static DoorCheck()
        {
            if (!SessionState.GetBool(Active, false)) return;
            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            EditorUtility.audioMasterMute = true;
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, "");
            SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Prologue_Camp.unity");
            EditorApplication.isPlaying = true;
            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;
        }

        private static void Write(string line)
            => File.AppendAllText(ReportPath, "[" + Time.realtimeSinceStartup.ToString("F0") + "] " + line + NL);

        private static void Catch(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (type == LogType.Exception || type == LogType.Error)
            {
                _errors++;
                Write("  [ОШИБКА] " + message + (string.IsNullOrEmpty(stack) ? "" : " @ " + stack.Split('\n')[0]));
            }
            else if (message.StartsWith("[ДВЕРЬ]")) Write("  " + message);
        }

        /// <summary>Щёлкнуть строку по её заголовку — как мышью: тот же onClick.</summary>
        private static bool Press(string title)
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!b.isActiveAndEnabled) continue;
                bool named = b.gameObject.name == title;
                if (!named)
                    foreach (var t in b.GetComponentsInChildren<Text>()) if (t.text == title) { named = true; break; }
                if (!named) continue;
                b.onClick.Invoke();
                Write($"щелчок: «{title}»");
                return true;
            }
            return false;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || _done) return;
            float now = Time.realtimeSinceStartup;
            if (_t0 < 0f) { _t0 = now; _at = now; Write("лагерь: ждём дверь"); }
            if (now - _t0 > Limit) { End(false, $"ЗАСТРЯЛО на шаге {_stage}, сцена {SceneManager.GetActiveScene().name}"); return; }
            string scene = SceneManager.GetActiveScene().name;

            switch (_stage)
            {
                case 0:   // дверь лагеря
                    if (!UI.StartPanel.Waiting) return;
                    if (now - _at < 1f) return;
                    if (!Press(Core.Loc.T("Начать заново"))) { End(false, "на двери нет строки «Начать заново»"); return; }
                    _stage = 1; _at = now;
                    break;
                case 1:   // сон загружен
                    if (scene != UI.StartPanel.Dream || DreamTutorialDirector.Instance == null) return;
                    Write("сон загружен: " + scene + ", стадия " + DreamTutorialDirector.StageName);
                    _stage = 2; _at = now;
                    break;
                case 2:   // меню паузы во сне — «Проснуться»
                    if (now - _at < 3f) return;
                    var menu = UnityEngine.Object.FindFirstObjectByType<UI.PauseMenu>(FindObjectsInactive.Include);
                    if (menu == null) { End(false, "во сне нет меню паузы"); return; }
                    typeof(UI.PauseMenu).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(menu, null);
                    if (!Press(Core.Loc.T("Проснуться"))) { End(false, "в меню паузы сна нет строки «Проснуться»"); return; }
                    _stage = 3; _at = now;
                    break;
                case 3:   // снова лагерь: дверь молчит, открытие лагеря (огонёк, пробуждение, надпись) идёт само
                    if (scene != "Prologue_Camp") return;
                    if (now - _at < 2f) return;
                    if (UI.StartPanel.Waiting) { End(false, "дверь спрашивает второй раз"); return; }
                    Gameplay.OpeningReveal.Autopilot = true;
                    Write($"лагерь после сна: сон-огонёк {Gameplay.OpeningReveal.Dreaming}, лежит {Gameplay.OpeningReveal.Lying}, "
                        + $"открытие {Gameplay.OpeningReveal.Busy}, заставка {UI.PrologueTitleUI.Showing}");
                    _stage = 4; _at = now;
                    break;
                case 4:   // открытие новой игры пошло: огонёк и пробуждение в шатре, пауза снята
                    // Дальше открытие ждёт, пока Греховод выйдет из шатра, — это уже прогон лагеря.
                    var pause = Core.GamePauseController.Instance;
                    bool paused = pause != null && (pause.IsPaused || pause.Halted);
                    if (!_woke && (Gameplay.OpeningReveal.Dreaming || Gameplay.OpeningReveal.Lying)) { _woke = true; Write("открытие лагеря: огонёк во тьме — пробуждение в шатре"); }
                    if (!_woke || paused || UI.PrologueTitleUI.Showing)
                    {
                        if (now - _at > 90f) { End(false, $"открытие лагеря не началось за 90 с: пауза {paused}, заставка {UI.PrologueTitleUI.Showing}, открытие {Gameplay.OpeningReveal.Busy}"); return; }
                        return;
                    }
                    End(true, $"лагерь → дверь → сон → «Проснуться» → лагерь: дверь молчит, открытие новой игры пошло за {now - _at:F0} с");
                    break;
            }
        }

        private static void End(bool ok, string why)
        {
            _done = true;
            Write((ok ? "=== ДВЕРЬ ПРОЙДЕНА: " : "=== ДВЕРЬ НЕ ПРОЙДЕНА: ") + why + $"; ошибок в журнале {_errors}");
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Catch;
            int code = ok && _errors == 0 ? 0 : 1;
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
#endif
