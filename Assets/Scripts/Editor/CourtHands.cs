// Assets/Scripts/Editor/CourtHands.cs
// Рука для первого суда «вы — судья»: в прогоне фигуру ведут клавиши хода (Controls.Pretend), как у игрока,
// а не игра за него. Автор, 26.11: «хоть и есть подсказка WASD, никуда пойти нельзя и идёт суд».
// Держит W, подруливает A/D к кафедре Верности (SoulCourt.WalkTarget) и пишет, дошла ли фигура сама.
//
// Верстак: call Sinbinder.EditorTools.CourtHands.Begin ;; dream stop=убийцы ;; get Sinbinder.EditorTools.CourtHands.Result
using UnityEditor;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    [InitializeOnLoad]
    public static class CourtHands
    {
        private const string Armed = "Sinbinder.CourtHands.armed";
        private static bool _on;

        // Вход в игру перезагружает скрипты: рука, поставленная до «dream», иначе слетает (прогон 26.11 — «довела игра»).
        static CourtHands()
        {
            if (SessionState.GetBool(Armed, false)) { _on = true; EditorApplication.update += Tick; }
        }
        private static float _walkFrom = -1f;
        private static string _log = "";

        public static string Begin()
        {
            if (!_on) EditorApplication.update += Tick;
            _on = true;
            SessionState.SetBool(Armed, true);
            _walkFrom = -1f;
            _log = "";
            SoulCourt.HandsOn = true;
            return "рука на клавишах хода в первом суде";
        }

        public static string Result =>
            (string.IsNullOrEmpty(SoulCourt.TookSeat) ? "место не занято" : "место: " + SoulCourt.TookSeat) + (_log.Length > 0 ? "; " + _log : "");

        private static void Tick()
        {
            if (!Application.isPlaying) return;   // до входа в игру и в перезагрузке — ждём
            SoulCourt.HandsOn = true;
            Controls.Pretend.Clear();
            if (!string.IsNullOrEmpty(SoulCourt.TookSeat)) { Release(); return; }
            if (SoulCourt.Step != "walk") return;
            var me = GameObject.Find("Вы");
            if (me == null) return;
            if (_walkFrom < 0f) { _walkFrom = Time.realtimeSinceStartup; _log = "шли от " + Flat(me.transform.position); }
            // Первые 2 с — стоим, как игрок, читающий строку: игра не должна увести фигуру.
            if (Time.realtimeSinceStartup - _walkFrom < 2f) return;
            Vector3 to = SoulCourt.WalkTarget - me.transform.position;
            to.y = 0f;
            Controls.Pretend.Add(Act.Forward);
            // Курс W — на кафедру с начала пути; подруливаем, куда смотрит цель относительно фигуры.
            float side = Vector3.Dot(Vector3.Cross(Vector3.up, me.transform.forward), to.normalized);
            if (side > 0.2f) Controls.Pretend.Add(Act.Right);
            else if (side < -0.2f) Controls.Pretend.Add(Act.Left);
        }

        private static string Flat(Vector3 p) => $"({p.x:0.0}, {p.z:0.0})";

        private static void Release()
        {
            Controls.Pretend.Clear();
            SoulCourt.HandsOn = false;
            SessionState.SetBool(Armed, false);
            if (_on) EditorApplication.update -= Tick;
            _on = false;
        }
    }
}
