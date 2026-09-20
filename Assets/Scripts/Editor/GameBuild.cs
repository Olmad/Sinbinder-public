// Assets/Scripts/Editor/GameBuild.cs
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Собрать игру в исполняемый файл.
    ///
    /// Заведено 20 сентября, в день показа, и по неприятному поводу:
    /// демо ни разу не собиралось в игру. Всё, что проверялось до сих
    /// пор — редактор. А редактор прощает то, чего сборка не прощает:
    /// он держит в памяти ассеты, которых нет в билде, читает меши без
    /// флага доступа и подхватывает скрипты из папки <c>Editor</c>.
    ///
    /// Про меш без флага здесь уже спотыкались (<see cref="PropImport"/>):
    /// навмеш пёкся в редакторе и не пёкся бы в игре. Такое видно только
    /// сборкой, и лучше увидеть это до зрителя, а не при нём.
    ///
    /// Сцены берём из настроек сборки, а не списком в коде: список
    /// в двух местах разойдётся, и разойдётся молча.
    /// </summary>
    public static class GameBuild
    {
        private const string Folder = "Build/Sinbinder";
        private const string Exe = Folder + "/Sinbinder.exe";

        [MenuItem("Sinbinder/Собрать игру")]
        public static void Build()
        {
            var scenes = System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled);

            if (scenes.Length == 0)
            {
                Debug.LogError("[СБОРКА] В настройках сборки нет ни одной включённой "
                             + "сцены — собирать нечего.");
                return;
            }

            var paths = new string[scenes.Length];
            for (int i = 0; i < scenes.Length; i++) paths[i] = scenes[i].path;

            Debug.Log($"[СБОРКА] Сцен в сборке: {paths.Length}. Первая — {paths[0]}.");

            Directory.CreateDirectory(Folder);

            var options = new BuildPlayerOptions
            {
                scenes = paths,
                locationPathName = Exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[СБОРКА] Не вышло: {summary.result}, "
                             + $"ошибок {summary.totalErrors}.");
                return;
            }

            Debug.Log($"[СБОРКА] Готово: {Exe}, "
                    + $"{summary.totalSize / (1024 * 1024)} МБ, "
                    + $"за {summary.totalTime.TotalMinutes:0.0} мин, "
                    + $"предупреждений {summary.totalWarnings}.");
        }

        /// <summary>Точка входа для пакетного режима.</summary>
        public static void BuildBatch() => Build();
    }
}
