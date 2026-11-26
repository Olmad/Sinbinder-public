// Assets/Scripts/Editor/StorageCheck.cs
// Где игра хранит записи и настройки (автор, 26.11: «несобранная игра — сохранялась у тебя, а если это билд —
// на ПК»; «на рабочем ПК ничего не должно сохраняться»). Записать как игрок (меню «Записать»), сменить
// настройку — и посмотреть, где что легло и не появилось ли чего на ПК.
//
// Верстак: open Assets/Scenes/Prologue_Camp.unity ;; play ;; wait 25 ;; call Sinbinder.EditorTools.StorageCheck.Save ;;
//          call Sinbinder.UI.MiniMap.Toggle ;; get Sinbinder.EditorTools.StorageCheck.Where
using System;
using System.IO;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.EditorTools
{
    public static class StorageCheck
    {
        public static string Save()
        {
            if (!Application.isPlaying) return "нужна игра";
            bool ok = SaveSystem.Write(SaveSystem.Snapshot(), SaveSystem.QuickPath);
            return $"записано {(ok ? "да" : "НЕТ")}: {SaveSystem.QuickPath}";
        }

        public static string Where
        {
            get
            {
                string settings = Path.Combine(DataFolder.Root, "настройки.txt");
                string pc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "DefaultCompany", "Sinbinder");
                return $"папка: {DataFolder.Root} (не на ПК: {(DataFolder.OffPc ? "да" : "НЕТ")}); быстрое: {(File.Exists(SaveSystem.QuickPath) ? "есть" : "НЕТ")}; "
                     + $"настройки файлом: {(File.Exists(settings) ? File.ReadAllLines(settings).Length + " строк" : "НЕТ")}; "
                     + $"на ПК — LocalLow: {(Directory.Exists(pc) ? "ЕСТЬ" : "нет")}";
            }
        }
    }
}
