// Editor-only selection helpers. They never change a map, its IDs or history.
using System;
using System.Collections.Generic;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    internal static class MapObjectSelection
    {
        internal enum Property { Model, Material }

        // Expand the existing multi-selection to every prop sharing any selected
        // model/material. Original (empty) materials are a valid explicit group.
        // Only actual authored Thing IDs count; stale IDs cannot select anything.
        internal static HashSet<string> Matching(IList<MapData.Thing> all,
            ISet<string> selectedIds, Property property)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (all == null || selectedIds == null || selectedIds.Count == 0 ||
                (property != Property.Model && property != Property.Material))
                return result;

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var thing in all)
            {
                if (thing == null || string.IsNullOrEmpty(thing.Id) ||
                    !selectedIds.Contains(thing.Id))
                    continue;
                if (property == Property.Model)
                {
                    if (!string.IsNullOrEmpty(thing.Prop)) keys.Add(thing.Prop);
                }
                else keys.Add(thing.MaterialId ?? "");
            }

            if (keys.Count == 0) return result;
            foreach (var thing in all)
            {
                if (thing == null || string.IsNullOrEmpty(thing.Id)) continue;
                string value = property == Property.Model ? thing.Prop : thing.MaterialId ?? "";
                if (keys.Contains(value)) result.Add(thing.Id);
            }
            return result;
        }

        // Invert across ordinary map props only, not crypts, regions or events.
        internal static HashSet<string> Inverted(IList<MapData.Thing> all,
            ISet<string> selectedIds)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (all == null) return result;
            foreach (var thing in all)
                if (thing != null && !string.IsNullOrEmpty(thing.Id) &&
                    (selectedIds == null || !selectedIds.Contains(thing.Id)))
                    result.Add(thing.Id);
            return result;
        }
    }
}
