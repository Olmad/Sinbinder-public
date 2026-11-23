// Список долгоживущего состояния для сторожа сохранений (SelfCheck.SaveCoverage).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sinbinder.EditorTools
{
    public static class SaveCoverageList
    {
        /// <summary>Все статические коллекции игры, которых ещё нет в таблице сторожа, — в Logs/save-coverage.txt.</summary>
        public static string Dump()
        {
            var missing = Sinbinder.Tests.SelfCheck.UnclassifiedState();
            var path = Path.Combine(Application.dataPath, "..", "Logs", "save-coverage.txt");
            File.WriteAllText(path, string.Join("\n", missing), new UTF8Encoding(false));
            return $"без пометки: {missing.Count}";
        }
    }
}
