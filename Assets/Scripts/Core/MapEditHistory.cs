using System;
using System.Collections.Generic;

namespace Sinbinder.Core
{
    /// <summary>Undo history for map commands, stored as reversible spans of serialized data.</summary>
    internal sealed class MapEditHistory
    {
        private const int Capacity = 200;
        private readonly List<Delta> _undo = new();
        private readonly List<Delta> _redo = new();
        private string _pending;

        public void Begin(string before)
        {
            Complete(before);
            _pending = before;
        }

        public void Cancel() => _pending = null;

        public void Complete(string current)
        {
            if (_pending != null && _pending != current)
            {
                _undo.Add(new Delta(_pending, current));
                if (_undo.Count > Capacity) _undo.RemoveAt(0);
                _redo.Clear();
            }
            _pending = null;
        }

        public bool TryUndo(string current, out string result)
            => TryStep(current, _undo, _redo, backwards: true, out result);

        public bool TryRedo(string current, out string result)
            => TryStep(current, _redo, _undo, backwards: false, out result);

        private bool TryStep(string current, List<Delta> from, List<Delta> to, bool backwards, out string result)
        {
            Complete(current);
            result = null;
            if (from.Count == 0) return false;
            int last = from.Count - 1;
            var change = from[last];
            if (!change.TryApply(current, backwards, out result)) return false;
            from.RemoveAt(last);
            to.Add(change);
            return true;
        }

        /// <summary>Updates the current endpoint after save normalizes fields, without adding an undo action.</summary>
        public void Rebase(string before, string after)
        {
            Complete(before);
            if (before == after) return;
            UpdateEndpoint(_undo, before, after, backwards: true);
            UpdateEndpoint(_redo, before, after, backwards: false);
        }

        private static void UpdateEndpoint(List<Delta> history, string before, string after, bool backwards)
        {
            if (history.Count == 0) return;
            int last = history.Count - 1;
            if (!history[last].TryApply(before, backwards, out var adjacent))
            {
                history.Clear();
                return;
            }
            if (adjacent == after) history.RemoveAt(last);
            else history[last] = backwards ? new Delta(adjacent, after) : new Delta(after, adjacent);
        }

        private readonly struct Delta
        {
            private readonly int _start, _beforeLength, _afterLength, _beforeHash, _afterHash;
            private readonly string _removed, _inserted;

            public Delta(string before, string after)
            {
                _beforeLength = before.Length;
                _afterLength = after.Length;
                _beforeHash = StringComparer.Ordinal.GetHashCode(before);
                _afterHash = StringComparer.Ordinal.GetHashCode(after);
                int maxCommon = Math.Min(_beforeLength, _afterLength);
                int prefix = 0;
                while (prefix < maxCommon && before[prefix] == after[prefix]) prefix++;
                int suffix = 0;
                while (suffix < maxCommon - prefix &&
                    before[_beforeLength - suffix - 1] == after[_afterLength - suffix - 1]) suffix++;
                _start = prefix;
                _removed = before.Substring(prefix, _beforeLength - prefix - suffix);
                _inserted = after.Substring(prefix, _afterLength - prefix - suffix);
            }

            public bool TryApply(string current, bool backwards, out string result)
            {
                result = null;
                if (current.Length != (backwards ? _afterLength : _beforeLength) ||
                    StringComparer.Ordinal.GetHashCode(current) != (backwards ? _afterHash : _beforeHash)) return false;
                string from = backwards ? _inserted : _removed;
                string to = backwards ? _removed : _inserted;
                if (string.CompareOrdinal(current, _start, from, 0, from.Length) != 0) return false;
                result = current.Substring(0, _start) + to + current.Substring(_start + from.Length);
                return true;
            }
        }
    }
}
