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
    /// Самопроверка без запуска игры: меню Sinbinder → Проверить движок.
    ///
    /// Смысл в том, чтобы проверка стоила одно нажатие. Проверка, ради
    /// которой надо собрать сцену и дождаться загрузки, не запускается
    /// никогда.
    /// </summary>
    public static class SelfCheckMenu
    {
        [MenuItem("Sinbinder/Проверить движок %#t")]
        public static void Run()
        {
            var report = SelfCheck.RunAll();

            if (report.Ok) Debug.Log("[SINBINDER] " + report);
            else Debug.LogError("[SINBINDER] " + report);

            EditorUtility.DisplayDialog(
                report.Ok ? "Движок в порядке" : "Движок сломан",
                report.ToString(),
                "Понятно");
        }
    }
}
#endif
