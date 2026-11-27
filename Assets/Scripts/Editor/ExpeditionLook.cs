// Взгляд на карту экспедиции без похода: верстак строит карту и ночь
// так же, как PersonalExpedition, и снимает кадры (wb: call ExpeditionLook.Show обоз).
using System.Reflection;
using Sinbinder.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Sinbinder.EditorTools
{
    public static class ExpeditionLook
    {
        private static GameObject _map;

        /// <summary>«обоз» или «часовня»: карта и ночь, ответ — где стоять для кадра.</summary>
        public static string Show(string which)
        {
            if (!EditorApplication.isPlaying) return "нужен режим игры (play)";
            if (_map != null) Object.Destroy(_map);
            var kind = which == "обоз" ? ExpeditionMapKind.SaltRoad : ExpeditionMapKind.Chapel;
            var plan = ExpeditionMapPlan.Build(kind);
            _map = ExpeditionMapBuilder.Build(plan);
            if (_map == null) return "карта не построилась (навмеш без входа)";

            var night = typeof(PersonalExpedition).GetNestedType("Night", BindingFlags.NonPublic);
            night?.GetMethod("Fall", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { _map.transform });

            // Туман — над этой картой (как Cover в PersonalExpedition), и для
            // кадра чистый: снимаем землю, а не разведку.
            FogOfWar.Cover(new Bounds(ExpeditionMapBuilder.Point(plan, 0f, 0f),
                new Vector3(plan.Width + 40f, 1f, plan.Depth + 40f)));
            FogOfWar.ShotClear(true);

            var o = ExpeditionMapBuilder.Origin(kind);
            int models = 0;
            foreach (var r in _map.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.GetComponent<Collider>() == null) models++;
            return $"{plan.Title}: начало {o.x:0},{o.z:0}; вход z={plan.EntranceZ:0}, цель z={plan.GoalZ:0}; " +
                   $"видимых моделей без тела {models}";
        }
    }
}
