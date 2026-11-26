// Assets/Scripts/Editor/CursorCheck.cs
// Курсор в окне снаряжения (автор, 26.11: «нередко в меню снаряжения/передачи снаряжения пропадает курсор»).
// Как игрок: первое лицо, F-разговор со своим воином рядом, «Покажи снаряжение» — и что с мышью.
// Скрытый верстак без фокуса окна отпускал бы мышь всегда — фокус ставится как у игрока.
//
// Верстак: open Assets/Scenes/Prologue_Camp.unity ;; play ;; wait 20 ;; call Sinbinder.EditorTools.CursorCheck.Talk ;;
//          wait 2 ;; get Sinbinder.EditorTools.CursorCheck.State ;; call Sinbinder.EditorTools.CursorCheck.Gear ;;
//          wait 2 ;; get Sinbinder.EditorTools.CursorCheck.State
using System.Reflection;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class CursorCheck
    {
        public static string State
        {
            get
            {
                var talk = Dialogue.DialogueCameraController.Instance;
                return $"курсор {(Cursor.visible ? "виден" : "СКРЫТ")}, {Cursor.lockState}; снаряжение {(UI.GearPanel.Open ? "открыто" : "закрыто")}, "
                     + $"ответы {(UI.TalkView.Shown ? "на экране" : "нет")}, наезд {(talk != null && talk.InDialogue ? "да" : "нет")}, "
                     + $"фокус окна {(View != null && Focus != null && (bool)Focus.GetValue(View) ? "да" : "НЕТ")}";
            }
        }

        private static RTS_Camera View => Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
        private static readonly FieldInfo Focus = typeof(RTS_Camera).GetField("_hasFocus", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Первое лицо с фокусом окна, как у игрока: курсор должен запереться — иначе проверка ничего не доказывает.</summary>
        public static string First()
        {
            var cam = View;
            if (!Application.isPlaying || cam == null) return "нужна игра с камерой";
            Focus?.SetValue(cam, true);
            cam.SetView(RTS_Camera.CameraView.FirstPerson);
            return "первое лицо";
        }

        public static string Talk()
        {
            if (!Application.isPlaying || !SinbinderPlayer.Exists) return "нужна игра с Греховодом";
            var cam = View;
            if (cam == null) return "нет камеры";
            Focus?.SetValue(cam, true);
            cam.SetView(RTS_Camera.CameraView.FirstPerson);
            Warrior near = null; float best = 99f;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w is SinbinderPlayer || w.IsDead || w.Team != Team.Player) continue;
                float d = Vector3.Distance(w.transform.position, SinbinderPlayer.Where);
                if (d < best) { best = d; near = w; }
            }
            if (near == null) return "своих воинов нет";
            UI.GearPanel.TalkTo(near);
            return $"разговор с {near.DisplayName} ({best:F1} м), первое лицо {(cam.FirstPersonNow ? "да" : "НЕТ")}";
        }

        /// <summary>«Покажи снаряжение» — третий пункт, как цифра у игрока.</summary>
        public static string Gear() => UI.GearPanel.Say(3) ? "пункт «снаряжение»" : "меню разговора не открыто";
    }
}
