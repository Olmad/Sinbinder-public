// Assets/Scripts/Gameplay/MapLogicRunner.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Warcraft-style events/conditions/actions, intentionally restricted to
    /// data-driven, non-authoritative presentation and local boolean flags.
    /// Map JSON is NEVER executed as C#, Lua, reflection or a file path.
    /// </summary>
    public sealed class MapLogicRunner : MonoBehaviour
    {
        public static MapLogicRunner Instance { get; private set; }

        private MapData _map;
        private readonly HashSet<string> _inside = new();
        private readonly HashSet<string> _fired = new();
        private readonly HashSet<string> _flags = new();
        private readonly Dictionary<string, Transform> _objects = new();
        private const int MaxTriggers = 128;
        private const int MaxMessage = 240;
        private const float UseRange = 3f;

        public void Configure(MapData map)
        {
            _map = map;
            _map?.EnsureLists();
            _inside.Clear();
            _fired.Clear();
            _flags.Clear();
            _objects.Clear();
            Instance = this;
        }

        public void RegisterObject(string id, Transform owner)
        {
            if (!string.IsNullOrEmpty(id) && owner != null && !_objects.ContainsKey(id))
                _objects.Add(id, owner);
        }

        void Start()
        {
            Fire("OnStart", "");
        }

        void Update()
        {
            if (_map == null || !SinbinderPlayer.Exists) return;
            foreach (var region in _map.Regions)
            {
                if (region == null || string.IsNullOrEmpty(region.Id) ||
                    !Finite(region.X) || !Finite(region.Z) ||
                    !Finite(region.Radius) || region.Radius <= 0f) continue;
                bool now = Vector2.Distance(new Vector2(region.X, region.Z),
                    new Vector2(SinbinderPlayer.Where.x, SinbinderPlayer.Where.z))
                    <= region.Radius;
                if (now && _inside.Add(region.Id)) Fire("OnEnterRegion", region.Id);
                else if (!now) _inside.Remove(region.Id);
            }
        }

        /// <summary>Participate in the SAME F priority system as chests, conversations and doors.</summary>
        public static void OfferInteractions(Action<float, string, Action> offer)
        {
            var runner = Instance;
            if (runner == null || runner._map == null ||
                !SinbinderPlayer.Exists || offer == null) return;
            var limit = Math.Min(MaxTriggers, runner._map.Triggers.Count);
            var offered = new HashSet<string>();
            for (int i = 0; i < limit; i++)
            {
                var t = runner._map.Triggers[i];
                if (t == null || !t.Enabled || t.Event != "OnInteractObject" ||
                    string.IsNullOrEmpty(t.TargetId) ||
                    (t.Once && runner._fired.Contains(t.Id)) ||
                    !runner.AllowedCondition(t) || !offered.Add(t.TargetId)) continue;
                if (!runner._objects.TryGetValue(t.TargetId, out var target) || target == null) continue;
                if (CampFocus.GroundDistance(target.position, SinbinderPlayer.Where) > UseRange) continue;
                string id = t.TargetId;
                // No new keyboard binding or parallel F polling.
                offer(Gaze.Miss(target.position + Vector3.up * 0.8f, 1f),
                    Loc.T("F — взаимодействовать"), () => runner.Fire("OnInteractObject", id));
            }
        }

        private void Fire(string eventName, string target)
        {
            if (_map == null || _map.Triggers == null) return;
            int count = Math.Min(MaxTriggers, _map.Triggers.Count);
            for (int i = 0; i < count; i++)
            {
                var t = _map.Triggers[i];
                if (t == null || !t.Enabled || t.Event != eventName ||
                    string.IsNullOrEmpty(t.Id) || (t.Once && _fired.Contains(t.Id))) continue;
                if (eventName != "OnStart" && t.RegionId != target && t.TargetId != target) continue;
                if (!AllowedCondition(t)) continue;
                if (!Apply(t)) continue;
                if (t.Once) _fired.Add(t.Id);
            }
        }

        private bool AllowedCondition(MapData.Trigger t)
        {
            switch (t.Condition)
            {
                case "Always": return true;
                case "FlagIsTrue": return !string.IsNullOrEmpty(t.Flag) && _flags.Contains(t.Flag);
                default: return false; // Unknown condition fails closed.
            }
        }

        private bool Apply(MapData.Trigger t)
        {
            switch (t.Action)
            {
                case "ShowMessage":
                    if (string.IsNullOrWhiteSpace(t.Message) || t.Message.Length > MaxMessage) return false;
                    Herald.Line(Loc.T(t.Message));
                    return true;
                case "SetFlag":
                    if (string.IsNullOrWhiteSpace(t.Flag) || t.Flag.Length > 64) return false;
                    _flags.Add(t.Flag);
                    return true;
                default: return false; // No arbitrary user-supplied code.
            }
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
