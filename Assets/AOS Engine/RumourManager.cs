// Assets/AOS Engine/RumourManager.cs
using System.Collections.Generic;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public static class RumourManager
    {
        private static Dictionary<string, List<Rumour>> _rumours = new();

        public static void SpreadRumour(Warrior listener, Warrior hero, DeedType deed, float value)
        {
            if (listener.Team != hero.Team) return;
            float relationship = listener.Relationships.GetRelationship(listener.Id, hero.Id);
            if (relationship < -50f) return;
            string key = $"{listener.Id}_{hero.Id}";
            if (!_rumours.ContainsKey(key))
                _rumours[key] = new List<Rumour>();
            var rumour = _rumours[key].Find(r => r.SubjectId == hero.Id);
            if (rumour == null)
            {
                rumour = new Rumour { SubjectId = hero.Id, Text = $"{hero.DisplayName} совершил {deed}" };
                _rumours[key].Add(rumour);
            }
            rumour.Progress += value * (relationship > 50f ? 1.5f : 1f);
            if (rumour.Progress > 100f && !rumour.Confirmed)
            {
                rumour.Confirmed = true;
            }
        }
    }
}