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

        /// <summary>
        /// Стекло банки души — прозрачное (27 сентября, <c>SoulJar</c>,
        /// <c>SoulJarFull</c>). Импорт делает любой материал модели
        /// непрозрачным Lit, и душу внутри банки не было бы видно вовсе:
        /// банка читалась бы серой кружкой.
        ///
        /// Правится здесь, при импорте, а не в игре: материал уезжает
        /// в сборку внутри модели уже прозрачным, и вариант шейдера
        /// с прозрачностью сборка не выбросит — его использует материал,
        /// который она видит. Переключи его кодом в игре — и в собранной
        /// игре варианта могло бы не оказаться.
        ///
        /// Тени стекло не бросает: прозрачная банка с тенью сплошного
        /// цилиндра выглядит поломкой.
        /// </summary>
        void OnPostprocessMaterial(UnityEngine.Material material)
        {
            if (!Ours(assetPath) || material == null || !material.name.StartsWith("Glass")) return;

            var c = material.color;
            material.color = new UnityEngine.Color(c.r, c.g, c.b, 0.24f);

            material.SetFloat("_Surface", 1f);                 // прозрачная
            material.SetFloat("_Blend", 0f);                   // по альфе
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.88f);
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
        }
    }
}
