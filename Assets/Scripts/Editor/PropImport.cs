// Assets/Scripts/Editor/PropImport.cs
using UnityEditor;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Как ложатся предметы мира из <c>Resources/Props</c>.
    ///
    /// Найдено прогоном <c>DemoWalkthrough</c> 18 сентября: навмеш печётся
    /// в рантайме (<see cref="Sinbinder.Gameplay.GroundNavMesh"/>,
    /// <c>CollectObjects.All</c>) и собирает геометрию со всей сцены —
    /// значит и с этих предметов тоже, палатка не должна быть проходной.
    /// По умолчанию Unity импортирует меш без доступа на чтение
    /// (<c>isReadable: false</c>, экономия памяти) — печь навмеш это
    /// не мешало **в редакторе**: там геометрия доступна помимо флага.
    /// В собранной игре флаг соблюдается всерьёз, и без него
    /// <c>NavMeshBuilder</c> тихо не увидит эти меши вовсе — палатки
    /// и ящики стали бы проходными молча, ровно там, где играбельность
    /// проверить может только билд, а не редактор.
    ///
    /// Отдельным постпроцессором, а не веткой в <see cref="WearImport"/>:
    /// у частей гардероба чтение не нужно, они не участвуют в навмеше.
    /// </summary>
    public class PropImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Props/";

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

            // Единственная причина файла: без этого NavMeshBuilder не
            // может прочитать меш в собранной игре (см. заметку класса).
            im.isReadable = true;
        }
    }
}
