// Assets/Scripts/Editor/TextureImport.cs
using UnityEditor;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Как ложатся текстуры из <c>Assets/Textures</c>.
    ///
    /// Материалы приходят с ambientCG (<c>Tools/textures/ambientcg.py</c>),
    /// по четыре карты на материал, и три из четырёх Unity по умолчанию
    /// импортирует неверно — молча:
    ///
    /// <list type="bullet">
    /// <item><b>Нормали</b> приходят как цветная картинка. Материал их
    /// принимает, рельеф выходит кривым, и выглядит это как «такая
    /// текстура», а не как ошибка.</item>
    /// <item><b>Шероховатость и затенение</b> — данные, а не цвет.
    /// В sRGB они искажаются гаммой, и поверхность блестит не там.</item>
    /// </list>
    ///
    /// Поэтому настройки ставятся по суффиксу файла, при импорте. Скачал
    /// новый материал — он лёг правильно, и помнить об этом не нужно.
    ///
    /// Предел размера — 1024: скрипт качает 1K нарочно (камера под 84°
    /// на двадцати двух метрах, 2K сверху неотличим), и этот предел
    /// не даёт случайно положенной 4K-карте съесть видеопамять машины
    /// с четырьмя гигабайтами.
    /// </summary>
    public class TextureImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Textures/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;

            var im = (TextureImporter)assetImporter;

            im.maxTextureSize = 1024;
            im.mipmapEnabled = true;

            if (assetPath.EndsWith("_NormalGL.jpg"))
            {
                im.textureType = TextureImporterType.NormalMap;
                return;
            }

            im.textureType = TextureImporterType.Default;

            bool data = assetPath.EndsWith("_Roughness.jpg")
                     || assetPath.EndsWith("_AmbientOcclusion.jpg");

            im.sRGBTexture = !data;
        }
    }
}
