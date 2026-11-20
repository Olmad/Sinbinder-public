using System;
using System.Collections.Generic;

namespace Sinbinder.Core
{
    internal enum MapIssueCode
    {
        MapMissing, MapNameMissing, UnsupportedSchema, FieldSize, TooFewCrypts, TooManyCrypts, CryptPosition,
        ObjectGeometry, ObjectModelMissing, ObjectMaterialMissing, ObjectId,
        LocationGeometry, TrailPosition, EventPosition, TerrainInvalid, TerrainMaterialMissing,
        RegionGeometry, RegionId, TooManyRules, RuleId, RuleEvent, RuleCondition, RuleAction,
        RuleRegionMissing, RuleTargetMissing, RuleFlag, RuleMessage
    }

    internal readonly struct MapIssue
    {
        public MapIssueCode Code { get; }
        public int Index { get; }
        public string EntityId { get; }
        public string Value { get; }
        public bool IsError => Code != MapIssueCode.MapNameMissing;

        public MapIssue(MapIssueCode code, int index = -1, string entityId = null, string value = null)
        {
            Code = code;
            Index = index;
            EntityId = entityId;
            Value = value;
        }
    }

    /// <summary>Checks map data without changing it or depending on the editor UI.</summary>
    internal static class MapValidator
    {
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static IReadOnlyList<T> Items<T>(List<T> values) => values ?? (IReadOnlyList<T>)Array.Empty<T>();

        public static List<MapIssue> Check(MapData map, Func<MapData, bool> terrainValid,
            Func<string, bool> modelExists, Func<string, bool> objectMaterialExists,
            Func<string, bool> terrainMaterialExists)
        {
            var issues = new List<MapIssue>();
            void Add(MapIssueCode code, int index = -1, string id = null, string value = null)
                => issues.Add(new MapIssue(code, index, id, value));

            if (map == null)
            {
                Add(MapIssueCode.MapMissing);
                return issues;
            }
            if (!MapData.SupportsSchema(map.SchemaVersion))
            {
                Add(MapIssueCode.UnsupportedSchema, value: map.SchemaVersion.ToString());
                return issues;
            }

            var crypts = Items(map.Crypts);
            if (crypts.Count < 2) Add(MapIssueCode.TooFewCrypts);
            if (crypts.Count > 8) Add(MapIssueCode.TooManyCrypts);
            bool validSize = Finite(map.Size) && map.Size >= 150f && map.Size <= 800f;
            if (!validSize) Add(MapIssueCode.FieldSize);
            float half = map.Size * 0.5f;
            // A broken field size has its own issue. Do not report every finite position as outside it.
            bool InField(float x, float z) => Finite(x) && Finite(z) &&
                (!validSize || (Math.Abs(x) <= half && Math.Abs(z) <= half));

            for (int i = 0; i < crypts.Count; i++)
            {
                var crypt = crypts[i];
                if (crypt == null || !InField(crypt.X, crypt.Z) || !Finite(crypt.Yaw))
                    Add(MapIssueCode.CryptPosition, i);
            }

            var objects = Items(map.Things);
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < objects.Count; i++)
            {
                var thing = objects[i];
                if (thing == null || !InField(thing.X, thing.Z) || !Finite(thing.Yaw) ||
                    !Finite(thing.Scale) || thing.Scale <= 0f)
                    Add(MapIssueCode.ObjectGeometry, i, thing?.Id);
                else if (string.IsNullOrWhiteSpace(thing.Prop) || !modelExists(thing.Prop))
                    Add(MapIssueCode.ObjectModelMissing, i, thing.Id, thing.Prop);
                if (thing == null) continue;
                if (string.IsNullOrWhiteSpace(thing.Id) || !objectIds.Add(thing.Id))
                    Add(MapIssueCode.ObjectId, i, thing.Id);
                if (!string.IsNullOrEmpty(thing.MaterialId) && !objectMaterialExists(thing.MaterialId))
                    Add(MapIssueCode.ObjectMaterialMissing, i, thing.Id, thing.MaterialId);
            }

            var locations = Items(map.Locations);
            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location == null || !InField(location.X, location.Z) || !Finite(location.Radius) ||
                    location.Radius <= 0f || location.People < 0)
                    Add(MapIssueCode.LocationGeometry, i);
            }

            void CheckSpots(IReadOnlyList<MapData.Spot> spots, MapIssueCode code)
            {
                for (int i = 0; i < spots.Count; i++)
                    if (spots[i] == null || !InField(spots[i].X, spots[i].Z)) Add(code, i);
            }
            CheckSpots(Items(map.Trails), MapIssueCode.TrailPosition);
            CheckSpots(Items(map.Events), MapIssueCode.EventPosition);

            if (validSize && !terrainValid(map)) Add(MapIssueCode.TerrainInvalid);
            var paints = Items(map.TerrainPaints);
            for (int i = 0; i < paints.Count; i++)
            {
                var paint = paints[i];
                if (paint != null && (string.IsNullOrWhiteSpace(paint.MaterialId) || !terrainMaterialExists(paint.MaterialId)))
                    Add(MapIssueCode.TerrainMaterialMissing, i, value: paint.MaterialId);
            }

            var regions = Items(map.Regions);
            var regionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                if (region == null || !InField(region.X, region.Z) || !Finite(region.Radius) || region.Radius <= 0f)
                    Add(MapIssueCode.RegionGeometry, i, region?.Id);
                if (region != null && (string.IsNullOrWhiteSpace(region.Id) || !regionIds.Add(region.Id)))
                    Add(MapIssueCode.RegionId, i, region.Id);
            }

            var rules = Items(map.Triggers);
            if (rules.Count > 128) Add(MapIssueCode.TooManyRules);
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || !ruleIds.Add(rule.Id))
                {
                    Add(MapIssueCode.RuleId, i, rule?.Id);
                    if (rule == null) continue;
                }
                if (rule.Event != "OnStart" && rule.Event != "OnEnterRegion" && rule.Event != "OnInteractObject")
                    Add(MapIssueCode.RuleEvent, i, rule.Id);
                bool flagCondition = rule.Condition == "FlagIsTrue" || rule.Condition == "FlagIsFalse";
                if (!flagCondition && rule.Condition != "Always") Add(MapIssueCode.RuleCondition, i, rule.Id);
                bool flagAction = rule.Action == "SetFlag" || rule.Action == "ClearFlag";
                if (!flagAction && rule.Action != "ShowMessage") Add(MapIssueCode.RuleAction, i, rule.Id);
                if (rule.Event == "OnEnterRegion" && !regionIds.Contains(rule.RegionId))
                    Add(MapIssueCode.RuleRegionMissing, i, rule.Id, rule.RegionId);
                if (rule.Event == "OnInteractObject" && !objectIds.Contains(rule.TargetId))
                    Add(MapIssueCode.RuleTargetMissing, i, rule.Id, rule.TargetId);
                if ((flagAction || flagCondition) && (string.IsNullOrWhiteSpace(rule.Flag) || rule.Flag.Length > 64))
                    Add(MapIssueCode.RuleFlag, i, rule.Id);
                if (rule.Action == "ShowMessage" && (string.IsNullOrWhiteSpace(rule.Message) || rule.Message.Length > 240))
                    Add(MapIssueCode.RuleMessage, i, rule.Id);
            }

            if (string.IsNullOrWhiteSpace(map.Name)) Add(MapIssueCode.MapNameMissing);
            return issues;
        }
    }
}
