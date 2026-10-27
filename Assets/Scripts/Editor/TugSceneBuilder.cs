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
        /// <summary>Сцены полей — в списке сборки с 27.10: по ним гуляют из паузы.</summary>
        private const string TugDir = "Assets/Scenes/Tug";

        [MenuItem("Sinbinder/Нагрузка/Собрать поля «Перетягивания» (2 и 4 стороны)")]
        public static void BuildTugScenes()
        {
            BuildTug(2);
            BuildTug(4);
        }

        public static void BuildTugScenesBatch() { BuildTugScenes(); BuildField(); }

        /// <summary>
        /// Сцена «Поле» — пустая основа, в которой карта из файла собирается в игре
        /// (<see cref="MapLoader"/>; 54-TUG.md): земля, свет, менеджеры, интерфейс, камера.
        /// Всё остальное — из описания карты.
        /// </summary>
        [MenuItem("Sinbinder/Нагрузка/Собрать сцену «Поле» (карты из файлов)")]
        public static void BuildField()
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Поле", 40f, AssetDatabase.IsValidFolder("Assets/Textures/Ground077") ? "Ground077" : "Ground048");
            Look();
            Managers();
            Interface();
            CameraRig(new Vector3(0f, 40f, -40f), new Vector3(55f, 0f, 0f), movable: true);
            new GameObject("Карта").AddComponent<MapLoader>();

            if (!AssetDatabase.IsValidFolder(TugDir)) AssetDatabase.CreateFolder("Assets/Scenes", "Tug");
            string path = $"{TugDir}/Field.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            RegisterInBuildSettings(path);
            Debug.Log($"[НАГРУЗКА] Собрана: {path}");
        }

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

            // Прогулка по полю (автор, 27.10): Греховод — на диагонали, между сторонами,
            // чтобы выпущенные по B армии шли не сквозь него. Камера следует за ним сама.
            var entry = new GameObject("Появление Греховода");
            entry.transform.position = new Vector3(40f, 0f, -40f);
            entry.AddComponent<Sinbinder.Crypt.TestChamberEntry>();

            if (!AssetDatabase.IsValidFolder(TugDir)) AssetDatabase.CreateFolder("Assets/Scenes", "Tug");
            string path = $"{TugDir}/Tug_{sides}.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            // В сборку — чтобы гулять по полю из паузы (PauseMenu, «Поля „Перетягивания“»).
            RegisterInBuildSettings(path);
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
