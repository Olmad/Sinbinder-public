// Assets/Scripts/Core/DataFolder.cs
// Куда игра пишет свои файлы: сохранения, настройки, черновики карт, журнал сна.
//
// Автор, 26.11: «нужно, чтобы сохранение несобранной игры сохранялось у тебя, а если это билд — сохранялось
// на ПК»; «на рабочем ПК ничего не должно сохраняться».
// - Редактор (несобранная игра) с флешки — в рабочей папке Claude: <диск>\Claude\Сохранения Sinbinder\редактор.
//   Едет вместе с флешкой; прогоны и проверки больше не пишут на ПК.
// - Сборка — как прежде, папка игры на ПК (Application.persistentDataPath). Кроме рабочих ПК: их имена — в
//   <диск сборки>\Claude\рабочие-пк.txt (по одному в строке; файл на флешке, не в репозитории). Там сборка пишет
//   в <диск>\Claude\Сохранения Sinbinder\сборка, а настройки — файлом там же (Prefs), не в реестр.
// Системный диск (C:) флешкой не считается: проект или сборка на нём пишут, как прежде, на ПК.
using System;
using System.IO;
using UnityEngine;

namespace Sinbinder.Core
{
    public static class DataFolder
    {
        private static bool _decided;
        private static string _offPcRoot;

        /// <summary>Пишем не на ПК (редактор с флешки или рабочий ПК): настройки — файлом, а не в реестр.</summary>
        public static bool OffPc { get { Decide(); return _offPcRoot != null; } }

        /// <summary>Папка для файлов игры. На ПК — persistentDataPath, читается каждый раз заново.</summary>
        public static string Root { get { Decide(); return _offPcRoot ?? Application.persistentDataPath; } }

        private static void Decide()
        {
            if (_decided) return;
            _decided = true;
            string root = null;
            try
            {
                string drive = Path.GetPathRoot(Path.GetFullPath(Application.dataPath));
                string system = Path.GetPathRoot(Environment.SystemDirectory);
                string claude = string.IsNullOrEmpty(drive) ? null : Path.Combine(drive, "Claude");
                // Только флешка с рабочей папкой Claude: копия проекта без неё (или на диске C:) пишет на ПК.
                if (claude != null && !string.Equals(drive, system, StringComparison.OrdinalIgnoreCase) && Directory.Exists(claude))
                {
#if UNITY_EDITOR
                    root = Path.Combine(claude, "Сохранения Sinbinder", "редактор");
#else
                    if (WorkPc(Path.Combine(claude, "рабочие-пк.txt")))
                        root = Path.Combine(claude, "Сохранения Sinbinder", "сборка");
#endif
                    if (root != null) Directory.CreateDirectory(root);
                }
            }
            catch (Exception e)
            {
                root = null;
                Debug.LogWarning("[ЗАПИСЬ] папка на флешке недоступна, пишу на ПК: " + e.Message);
            }
            _offPcRoot = root;
        }

        /// <summary>Этот ПК — в списке рабочих (имя ПК без учёта регистра; # — примечание).</summary>
        private static bool WorkPc(string list)
        {
            if (!File.Exists(list)) return false;
            string me = Environment.MachineName;
            foreach (var raw in File.ReadAllLines(list))
            {
                string line = raw.Split('#')[0].Trim();
                if (line.Length > 0 && string.Equals(line, me, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
