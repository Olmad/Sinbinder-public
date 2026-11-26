// Assets/Scripts/Editor/TraitProbe.cs
// Перепись особенностей на сцене (26.11; автор: «пустые» воины в «Перетягивании»). Census — у кого сколько
// особенностей и примет; Pick — выделить первого воина с особенностями, чтобы снять панель.
//
// Верстак: open Tug_2 ;; play ;; wait 8 ;; get Sinbinder.EditorTools.TraitProbe.Census ;; call Sinbinder.EditorTools.TraitProbe.Pick ;; shot
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class TraitProbe
    {
        public static string Census
        {
            get
            {
                if (!Application.isPlaying) return "ОШИБКА: игра не идёт";
                var all = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None)
                                .Where(w => !(w is SinbinderPlayer) && w.Soul != null).ToList();
                int empty = 0, none = 0;
                var lines = new System.Collections.Generic.List<string>();
                foreach (var w in all)
                {
                    var names = (w.Soul.Memory?.NarrativePerks ?? new System.Collections.Generic.List<Core.NarrativePerk>())
                                .Where(p => Traits.Find(p.PerkName) != null).Select(p => p.PerkName).ToList();
                    bool lines0 = string.IsNullOrEmpty(Signature.Why(w));
                    if (names.Count == 0) none++;
                    if (names.Count == 0 && lines0) empty++;
                    if (lines.Count < 8) lines.Add($"{w.DisplayName} ({w.Team}, {w.Soul.Sin}): {string.Join(", ", names)}{(lines0 ? "; без приметы" : "")}");
                }
                return $"воинов {all.Count}: без особенностей {none}, совсем пустых {empty}\n  " + string.Join("\n  ", lines);
            }
        }

        public static string Pick()
        {
            if (!Application.isPlaying) return "ОШИБКА: игра не идёт";
            var manager = SelectionManager.Instance;
            if (manager == null) return "нет SelectionManager";
            var w = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID)
                          .FirstOrDefault(x => !(x is SinbinderPlayer) && Traits.Any(x.Soul?.Memory)
                                               && x.GetComponentInChildren<SelectionComponent>() != null);
            if (w == null) return "некого выделить";
            var unit = w.GetComponentInChildren<SelectionComponent>();
            typeof(SelectionManager).GetMethod("SelectUnit", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(manager, new object[] { unit });
            return $"выделен {w.DisplayName}: {Traits.Describe(w.Soul.Memory).Replace('\n', ' ')}";
        }
    }
}
