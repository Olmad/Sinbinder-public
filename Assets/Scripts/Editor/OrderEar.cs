// Assets/Scripts/Editor/OrderEar.cs
// Замер «как скоро воин слышит приказ» (26.11, полировка AOS): каждому живому воину с тактом решения
// отдаётся приказ «идти» на два шага вперёд, и пишется, через сколько его голосование дошло до этого приказа.
// До правки AutoAttackAOS ответ ждал своего такта — в среднем полсекунды, до целой; после — короткий вдох.
//
// Верстак: open Prologue_Camp ;; play ;; wait 4 ;; call Sinbinder.EditorTools.OrderEar.Begin ;; wait 2 ;; get Sinbinder.EditorTools.OrderEar.Result
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class OrderEar
    {
        private sealed class Ear
        {
            public Warrior W;
            public AOSWarriorWrapper Mind;
            public uint Order;
            public float At;
            public float Heard = -1f;
        }

        private static readonly List<Ear> _ears = new List<Ear>();
        private static bool _on;

        public static string Begin()
        {
            if (!Application.isPlaying) return "ОШИБКА: игра не идёт";
            _ears.Clear();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                var mind = w.GetComponent<AOSWarriorWrapper>();
                var tick = w.GetComponent<AutoAttackAOS>();
                var body = w.GetComponent<Damageable>();
                if (mind == null || tick == null || !tick.isActiveAndEnabled || (body != null && body.IsDead)) continue;
                w.IssueCommand(CommandKind.Move, w.transform.position + w.transform.forward * 2f);
                _ears.Add(new Ear { W = w, Mind = mind, Order = w.CommandRevision, At = Time.time });
            }
            if (!_on) { EditorApplication.update += Tick; _on = true; }
            return $"приказ «идти» отдан воинам: {_ears.Count}";
        }

        private static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; _on = false; return; }
            foreach (var e in _ears)
                if (e.Heard < 0f && e.W != null && e.Mind.LastDecisionCommandRevision == e.Order)
                    e.Heard = Time.time - e.At;
        }

        public static string Result
        {
            get
            {
                if (_ears.Count == 0) return "замера не было";
                var heard = _ears.Where(e => e.Heard >= 0f).Select(e => e.Heard).ToList();
                if (heard.Count == 0) return $"из {_ears.Count} не услышал никто";
                string who = string.Join(", ", _ears.Take(6).Select(e =>
                    (e.W != null ? e.W.DisplayName : "?") + " " + (e.Heard >= 0f ? e.Heard.ToString("0.00") + " с" : "—")));
                return $"услышали {heard.Count} из {_ears.Count}: быстрее всех {heard.Min():0.00} с, "
                     + $"в среднем {heard.Average():0.00} с, дольше всех {heard.Max():0.00} с; {who}";
            }
        }
    }
}
