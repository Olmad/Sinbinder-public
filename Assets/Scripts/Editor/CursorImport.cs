// Assets/Scripts/Editor/CursorImport.cs
using UnityEditor;
using UnityEngine;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Картинки курсора (<c>Resources/Cursors</c>, рисует
    /// <c>Tools/cursors/cursors.py</c>) — читаемые, RGBA32 без сжатия и без
    /// мип-карт. Так их берёт <c>Cursor.SetCursor</c>, и так игра может
    /// докрасить душу в банке цветом греха (<see cref="Sinbinder.UI.GameCursor"/>):
    /// сжатую текстуру не прочесть, а с мип-картами курсор мылится.
    /// </summary>
    public class CursorImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Cursors/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
