// Assets/Scripts/Editor/ArtImport.cs
using UnityEditor;
using UnityEngine;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Картинки игры (<c>Resources/Art</c>) — надпись «Sinbinder» и всё, что
    /// рисуется для экрана, а не для мира: без сжатия и мип-уровней,
    /// прозрачность — прозрачность. Сжатое золото на краях букв идёт грязью,
    /// а с мип-уровнями надпись мылится при уменьшении.
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Art/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
        }
    }
}
