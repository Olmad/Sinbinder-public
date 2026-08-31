// Assets/_Project/Scripts/Tests/SelfCheckMenu.cs
//
// Редакторный скрипт. Он лежит не в папке Editor, поэтому обёрнут
// целиком, включая using: без обёртки сборка плеера падает на
// отсутствующем UnityEditor.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Sinbinder.Tests
{
    /// <summary>
    /// Самопроверка без запуска игры.
    ///
    /// Два входа. Из редактора — меню Sinbinder → Проверить движок.
    /// Из командной строки — RunBatch, чтобы проверку можно было
    /// запустить без человека:
    ///
    ///   Unity.exe -batchmode -nographics -quit
    ///             -projectPath "путь_к_проекту"
    ///             -logFile "unity.log"
    ///             -executeMethod Sinbinder.Tests.SelfCheckMenu.RunBatch
    ///
    /// Смысл второго входа: Unity в пакетном режиме компилирует проект
    /// и пишет всё в лог. Значит, ошибки компиляции и результат проверки
    /// можно прочитать из файла — в том числе тому, у кого нет доступа
    /// к редактору.
    /// </summary>
    public static class SelfCheckMenu
    {
        [MenuItem("Sinbinder/Проверить движок %#t")]
        public static void Run()
        {
            var report = SelfCheck.RunAll();

            if (report.Ok) Debug.Log("[SINBINDER] " + report);
            else Debug.LogError("[SINBINDER] " + report);

            // В пакетном режиме диалога быть не должно: там некому нажать
            // кнопку, и Unity просто вернёт true не спрашивая.
            if (Application.isBatchMode) return;

            EditorUtility.DisplayDialog(
                report.Ok ? "Движок в порядке" : "Движок сломан",
                report.ToString(),
                "Понятно");
        }

        /// <summary>
        /// Для командной строки. Возвращает код выхода: 0 — всё сошлось,
        /// 1 — нет. По коду видно результат, даже не читая лог.
        /// </summary>
        public static void RunBatch()
        {
            var report = SelfCheck.RunAll();

            // Отдельные маркеры, чтобы результат вылавливался из лога
            // одной строкой, без разбора формата.
            Debug.Log("=== SINBINDER SELFCHECK BEGIN ===");
            Debug.Log(report.ToString());
            Debug.Log($"=== SINBINDER SELFCHECK END: {(report.Ok ? "OK" : "FAILED")} "
                    + $"{report.Passed}/{report.Total} ===");

            EditorApplication.Exit(report.Ok ? 0 : 1);
        }
    }
}
#endif
