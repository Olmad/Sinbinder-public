// Safe, editor-only layout calculations for regular MapData.Thing props.
// This planner never mutates a map; MapEditor commits the validated plan as one Undo.
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    internal static class MapObjectArrange
    {
        internal enum Axis { X, Z }
        internal enum Operation { Minimum, Center, Maximum, Distribute }
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// Build an atomic, stable alignment/distribution plan for a selected set.
        /// Returns false for invalid data, unchanged positions, or too few points.
        /// Objects outside the selected set, gameplay anchors and rule IDs are not read.
        /// </summary>
        internal static bool TryPlan(
            IList<MapData.Thing> selected, float mapSize, Axis axis, Operation operation,
            out Vector2[] positions)
        {
            positions = null;
            if (selected == null || selected.Count < 2 ||
                !Finite(mapSize) || mapSize <= 0f ||
                (axis != Axis.X && axis != Axis.Z) ||
                (operation != Operation.Minimum && operation != Operation.Center &&
                 operation != Operation.Maximum && operation != Operation.Distribute))
                return false;

            if (operation == Operation.Distribute && selected.Count < 3) return false;

            float half = mapSize * 0.5f;
            if (!Finite(half)) return false;
            int n = selected.Count;
            var result = new Vector2[n];
            var original = new float[n];
            var sorted = new List<int>(n);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;

            for (int i = 0; i < n; i++)
            {
                var thing = selected[i];
                if (thing == null || !Finite(thing.X) || !Finite(thing.Z) ||
                    Mathf.Abs(thing.X) > half || Mathf.Abs(thing.Z) > half)
                    return false;

                result[i] = new Vector2(thing.X, thing.Z);
                float value = axis == Axis.X ? thing.X : thing.Z;
                original[i] = value;
                if (value < minimum) minimum = value;
                if (value > maximum) maximum = value;
                sorted.Add(i);
            }

            if (operation == Operation.Distribute)
            {
                if (maximum - minimum <= Epsilon) return false;
                // Tie handling is stable by input order. Equal coordinate values
                // must not lead to different arrangements on different machines.
                sorted.Sort((a, b) =>
                {
                    int c = original[a].CompareTo(original[b]);
                    return c != 0 ? c : a.CompareTo(b);
                });
                for (int rank = 0; rank < n; rank++)
                {
                    int index = sorted[rank];
                    float value = minimum + (maximum - minimum) * rank / (n - 1);
                    if (axis == Axis.X) result[index].x = value;
                    else result[index].y = value;
                }
            }
            else
            {
                float target = operation == Operation.Minimum ? minimum
                    : operation == Operation.Maximum ? maximum
                    : minimum + (maximum - minimum) * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    if (axis == Axis.X) result[i].x = target;
                    else result[i].y = target;
                }
            }

            bool changed = false;
            for (int i = 0; i < n; i++)
            {
                if (!Finite(result[i].x) || !Finite(result[i].y) ||
                    Mathf.Abs(result[i].x) > half || Mathf.Abs(result[i].y) > half)
                    return false;
                if (Mathf.Abs(result[i].x - selected[i].X) > Epsilon ||
                    Mathf.Abs(result[i].y - selected[i].Z) > Epsilon)
                    changed = true;
            }
            if (!changed) return false;
            positions = result;
            return true;
        }

        /// <summary>
        /// Snap ALL selected props to the current map grid as one validated plan.
        /// A no-op or one out-of-bounds result cannot create a history entry.
        /// Coordinates on the other axis are retained if already on grid.
        /// </summary>
        internal static bool TrySnap(IList<MapData.Thing> selected,
            float mapSize, float step, out Vector2[] positions)
        {
            positions = null;
            if (selected == null || selected.Count == 0 ||
                !Finite(mapSize) || mapSize <= 0f ||
                !Finite(step) || step <= 0f)
                return false;

            float half = mapSize * 0.5f;
            if (!Finite(half)) return false;
            var result = new Vector2[selected.Count];
            bool changed = false;

            for (int i = 0; i < selected.Count; i++)
            {
                var thing = selected[i];
                if (thing == null || !Finite(thing.X) || !Finite(thing.Z) ||
                    Mathf.Abs(thing.X) > half || Mathf.Abs(thing.Z) > half)
                    return false;
                float x = Mathf.Round(thing.X / step) * step;
                float z = Mathf.Round(thing.Z / step) * step;
                if (!Finite(x) || !Finite(z) ||
                    Mathf.Abs(x) > half || Mathf.Abs(z) > half)
                    return false;
                result[i] = new Vector2(x, z);
                if (Mathf.Abs(x - thing.X) > Epsilon ||
                    Mathf.Abs(z - thing.Z) > Epsilon)
                    changed = true;
            }

            if (!changed) return false;
            positions = result;
            return true;
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
