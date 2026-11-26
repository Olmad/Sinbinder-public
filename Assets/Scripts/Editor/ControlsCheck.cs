// Assets/Scripts/Editor/ControlsCheck.cs
// Страница «Управление» (автор, 26.11: «необходима настройка управления, чтобы можно было переназначить кнопки»)
// глазами и щелчками: открыть паузу, щёлкнуть «Управление», щёлкнуть действие, «нажать» клавишу (тот же
// PauseMenu.Press, что зовёт страница по нажатию), посмотреть подсказки и файл настроек.
//
// Верстак: open Prologue_Camp ;; play ;; wait 25 ;; call Sinbinder.EditorTools.ControlsCheck.Pause ;; click Управление ;;
//          shot "управление" ;; click Взаимодействие ;; call Sinbinder.EditorTools.ControlsCheck.Press Interact E ;;
//          get Sinbinder.EditorTools.ControlsCheck.State ;; click Сбросить ;; get Sinbinder.EditorTools.ControlsCheck.State
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.UI;

namespace Sinbinder.EditorTools
{
    public static class ControlsCheck
    {
        /// <summary>Открыть меню паузы, как по свободному Esc.</summary>
        public static string Pause()
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<PauseMenu>();
            if (menu == null) return "ОШИБКА: меню паузы нет в сцене";
            if (PauseMenu.IsOpen) return "меню уже открыто";
            typeof(PauseMenu).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(menu, null);
            return PauseMenu.IsOpen ? "меню открыто" : "ОШИБКА: меню не открылось";
        }

        /// <summary>Нажать клавишу на странице, которая ждёт (как игрок после щелчка по действию).</summary>
        public static string Press(string act, string key)
        {
            if (!Enum.TryParse(act, out Act a)) return "ОШИБКА: нет действия " + act;
            if (!Enum.TryParse(key, out KeyCode k)) return "ОШИБКА: нет клавиши " + key;
            string waiting = PauseMenu.Capturing ? "страница ждала клавишу" : "страница НЕ ждала клавишу";
            return waiting + "; " + PauseMenu.Press(a, k);
        }

        /// <summary>Размер доски меню и холста — влезает ли меню в экран.</summary>
        public static string Board
        {
            get
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<PauseMenu>();
                var panel = menu == null ? null : typeof(PauseMenu).GetField("_panel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(menu) as GameObject;
                if (panel == null) return "доски нет";
                var rt = (RectTransform)panel.transform;
                var canvas = panel.GetComponentInParent<Canvas>();
                var root = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
                var parent = rt.parent as RectTransform;
                return $"доска {rt.rect.width:0}×{rt.rect.height:0} (sizeDelta {rt.sizeDelta.y:0}), масштаб {rt.localScale.x:0.00}, якоря {rt.anchorMin}–{rt.anchorMax}; "
                     + $"родитель {(parent != null ? parent.name + " " + parent.rect.height.ToString("0") : "—")}; холст {(root != null ? root.name + " " + root.rect.height.ToString("0") + ", scale " + root.localScale.x.ToString("0.00") : "—")}; экран {Screen.width}×{Screen.height}";
            }
        }

        public static string State
        {
            get
            {
                string file = Path.Combine(DataFolder.Root, "настройки.txt");
                int keys = 0;
                if (File.Exists(file)) foreach (var l in File.ReadAllLines(file)) if (l.StartsWith("keys.")) keys++;
                return $"F-действие: {Controls.Name(Act.Interact)}, сбор: {Controls.Name(Act.Harvest)}, карта: {Controls.Name(Act.Map)}; "
                     + $"подсказки: «{Controls.Relabel("F — поговорить: Гурт")}», «{Controls.Relabel("Возьмите банку в суму (E)")}», "
                     + $"плашка «{Controls.KeyWord("F")}»; в файле клавиш: {keys}";
            }
        }
    }
}
