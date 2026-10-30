// Assets/Scripts/Dev/LowMemory.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Dev
{
    /// <summary>
    /// Прогоны на ПК с малой памятью — текстуры вдвое мельче (автор, 30.10: «за счёт чего такое большое потребление
    /// оперативки?»; на 8 ГБ система дважды за день убивала прогон). Прогон — это редактор Unity целиком: проект,
    /// шейдеры, текстуры в полный размер, копия сцены в игре; на встроенной графике текстуры лежат в той же памяти.
    /// Половинные текстуры — заметно меньше, а проверкам (пути, шаги, ошибки) они не мешают.
    ///
    /// Только пакетный режим (прогоны без окна) и только меньше 10 ГБ: в игре игрока не срабатывает никогда, а кадры
    /// для оценки красоты снимаются на машине, где памяти хватает.
    /// </summary>
    public static class LowMemory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Trim()
        {
            if (!Application.isBatchMode || SystemInfo.systemMemorySize >= 10000) return;
            QualitySettings.globalTextureMipmapLimit = 1;
            Debug.Log($"[ПАМЯТЬ] пакетный прогон на {SystemInfo.systemMemorySize} МБ — текстуры вдвое мельче");
        }
    }
}
