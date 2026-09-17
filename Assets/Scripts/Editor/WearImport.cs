// Assets/Scripts/Editor/WearImport.cs
using UnityEditor;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Как ложатся части гардероба из <c>Resources/Wear</c>.
    ///
    /// Они не тела: костей нет, движений нет, аватар не нужен. Всё, что
    /// им требуется, — масштаб из файла и материалы внутри модели, а не
    /// отдельными ассетами рядом.
    ///
    /// Отдельным постпроцессором, а не веткой в <see cref="BodyImport"/>:
    /// у тел настройки прямо противоположные — там аватар обязателен,
    /// без него Unity не вешает <c>Animator</c>. Одна ветка на два
    /// противоположных случая разъехалась бы при первой правке.
    /// </summary>
    public class WearImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Wear/";

        private static bool Ours(string path)
            => path != null
            && path.StartsWith(Folder)
            && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;

            var im = (ModelImporter)assetImporter;

            im.useFileScale = true;
            im.globalScale = 1f;

            im.importNormals = ModelImporterNormals.Import;
            im.importBlendShapes = false;
            im.importCameras = false;
            im.importLights = false;
            im.importVisibility = false;
            im.importAnimation = false;

            im.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            im.materialLocation = ModelImporterMaterialLocation.InPrefab;

            im.animationType = ModelImporterAnimationType.None;
            im.optimizeMeshPolygons = true;
            im.optimizeMeshVertices = true;
        }
    }
}
