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
    /// Режим, а не правило (автор, 30.10: «можешь сделать это как режим и включать, если обычный прогон не
    /// получается?»): включается переменной окружения SINBINDER_LOWMEM=1 — её ставит цепочка прогона, если обычный
    /// убила нехватка памяти. Только пакетный режим: в игре игрока не срабатывает никогда.
    /// </summary>
    public static class LowMemory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Trim()
        {
            if (!Application.isBatchMode || System.Environment.GetEnvironmentVariable("SINBINDER_LOWMEM") != "1") return;
            QualitySettings.globalTextureMipmapLimit = 1;
            Debug.Log($"[ПАМЯТЬ] экономный прогон ({SystemInfo.systemMemorySize} МБ) — текстуры вдвое мельче");
        }
    }
}
