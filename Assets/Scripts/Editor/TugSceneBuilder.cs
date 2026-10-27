#if UNITY_EDITOR
// Assets/Scripts/Editor/TugSceneBuilder.cs
// Нагрузочные сцены «Перетягивания каната» — отдельным файлом (автор, 27.10:
// «её сделать отдельным файлом»). Общий низ сцен — тот же, что у демо (DemoSceneBuilder).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    public static partial class DemoSceneBuilder
    {
        /// <summary>Сцены стенда — не в списке сборки: в игру они не едут.</summary>
        private const string TugDir = "Assets/Scenes/Tug";

        [MenuItem("Sinbinder/Нагрузка/Собрать поля «Перетягивания» (2 и 4 стороны)")]
        public static void BuildTugScenes()
        {
            BuildTug(2);
            BuildTug(4);
        }

        public static void BuildTugScenesBatch() => BuildTugScenes();

        /// <summary>
        /// Поле на 2 или 4 стороны по сотне: 800 на 800 м плит (самая большая карта,
        /// какую стоит печь навмешем при запуске), холодная ночь, камера сверху и стенд
        /// (<see cref="TugBench"/>), который ставит воинов и мерит кадр.
        /// </summary>
        private static void BuildTug(int sides)
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Поле", 80f, "PavingStones127");
            Look();
            Managers();
            Interface();

            CameraRig(new Vector3(0f, 60f, -55f), new Vector3(55f, 0f, 0f), movable: true);

            var bench = new GameObject("Стенд нагрузки").AddComponent<TugBench>();
            var so = new SerializedObject(bench);
            so.FindProperty("_sides").intValue = sides;
            so.FindProperty("_perSide").intValue = 100;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder(TugDir)) AssetDatabase.CreateFolder("Assets/Scenes", "Tug");
            string path = $"{TugDir}/Tug_{sides}.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[НАГРУЗКА] Собрана: {path}");
        }
    }
}

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Замер поля пакетно: открыть сцену, войти в игру, дождаться итога стенда, выйти.
    /// Сцена — аргумент <c>-tugSides 2|4</c>. Отчёт — <see cref="TugBench.Report"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class TugRun
    {
        private const string Active = "Sinbinder.TugRun.Active";

        static TugRun()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            int sides = 2;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-tugSides" && int.TryParse(args[i + 1], out var s)) sides = s;

            EditorUtility.audioMasterMute = true;
            SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene($"Assets/Scenes/Tug/Tug_{sides}.unity");
            EditorApplication.isPlaying = true;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;

            // Вопрос о сохранении ставит игру на паузу — отвечаем, как игрок.
            Sinbinder.UI.StartPanel.ChooseFresh();
            if (!TugBench.Done) return;

            EditorApplication.update -= Tick;
            SessionState.SetBool(Active, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
#endif
