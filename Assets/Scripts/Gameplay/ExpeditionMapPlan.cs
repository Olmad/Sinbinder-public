// Two actual, deterministic expedition layouts. Pure C# shared by Unity
// map generator and .NET tests. Coordinates are LOCAL to each battlefield.
using System;
using System.Collections.Generic;

namespace Sinbinder.Gameplay
{
    public enum ExpeditionMapKind { SaltRoad = 1, Chapel = 2 }
    public enum ExpeditionPropKind { Rock, DeadTree, Pine, Grave, Wall, Wagon, Tent, Column, Altar, Torch, Rubble }

    public readonly struct ExpeditionProp
    {
        public readonly ExpeditionPropKind Kind;
        public readonly float X, Z, Size, Yaw;
        public ExpeditionProp(ExpeditionPropKind kind, float x, float z, float size = 1f, float yaw = 0f)
        { Kind = kind; X = x; Z = z; Size = size; Yaw = yaw; }
    }

    public sealed class ExpeditionMapPlan
    {
        public ExpeditionMapKind Kind { get; }
        public string Id { get; }
        public string Title { get; }
        public float Width { get; }
        public float Depth { get; }
        public float EntranceZ { get; }
        public float GoalZ { get; }
        public IReadOnlyList<ExpeditionProp> Props { get; }
        public ExpeditionMapPlan(ExpeditionMapKind kind, string id, string title,
                                 float width, float depth, float entranceZ,
                                 float goalZ, List<ExpeditionProp> props)
        {
            Kind = kind; Id = id; Title = title; Width = width;
            Depth = depth; EntranceZ = entranceZ; GoalZ = goalZ;
            Props = props.AsReadOnly();
        }

        public static ExpeditionMapPlan ForName(string mission)
        {
            if (mission == "Соляной обоз") return Build(ExpeditionMapKind.SaltRoad);
            if (mission == "Придорожная часовня") return Build(ExpeditionMapKind.Chapel);
            return null;
        }

        public static ExpeditionMapPlan Build(ExpeditionMapKind kind)
        {
            if (kind != ExpeditionMapKind.SaltRoad && kind != ExpeditionMapKind.Chapel)
                throw new ArgumentOutOfRangeException(nameof(kind));

            var props = new List<ExpeditionProp>();
            if (kind == ExpeditionMapKind.SaltRoad)
            {
                // A winding caravan road with dunes, rock cover and two
                // wagons. It has navigable space in the middle, not a
                // random heap of blocks disguised as terrain.
                for (int i = 0; i < 34; i++)
                {
                    float z = -47f + i * 3f;
                    float side = i % 2 == 0 ? -1f : 1f;
                    float x = (16f + (i * 13 % 17)) * side;
                    props.Add(new ExpeditionProp(i % 5 == 0 ? ExpeditionPropKind.DeadTree :
                        ExpeditionPropKind.Rock, x, z, 0.7f + i % 4 * 0.25f, i * 37f));
                }
                for (int i = 0; i < 15; i++)
                {
                    float z = -37f + i * 6f;
                    props.Add(new ExpeditionProp(ExpeditionPropKind.Rock, -38f, z, 0.6f + i % 3 * 0.3f));
                    props.Add(new ExpeditionProp(ExpeditionPropKind.Rock, 38f, z, 0.6f + i % 4 * 0.2f));
                }
                props.Add(new ExpeditionProp(ExpeditionPropKind.Wagon, 6f, 12f, 1.15f, 14f));
                props.Add(new ExpeditionProp(ExpeditionPropKind.Wagon, -6f, 25f, 1.0f, -18f));
                props.Add(new ExpeditionProp(ExpeditionPropKind.Tent, 23f, 28f, 1.1f));
                props.Add(new ExpeditionProp(ExpeditionPropKind.Torch, 11f, 22f));
                props.Add(new ExpeditionProp(ExpeditionPropKind.Torch, -12f, 34f));
                return new ExpeditionMapPlan(kind, "expedition:salt-road", "Соляной обоз",
                    100f, 116f, -49f, 31f, props);
            }

            // Chapel: a ruined stone sanctuary, rows of graves, broken
            // arcades and dense pines. Clear entrance lane from the south.
            for (int i = 0; i < 28; i++)
            {
                float z = -40f + i * 3.1f;
                float side = i % 2 == 0 ? -1f : 1f;
                props.Add(new ExpeditionProp(ExpeditionPropKind.Pine,
                    side * (34f + i % 4 * 3f), z, 0.75f + (i % 3) * 0.15f));
            }
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 5; col++)
                    props.Add(new ExpeditionProp(ExpeditionPropKind.Grave,
                        (col < 3 ? -30f + col * 7f : 18f + (col - 3) * 7f), -12f + row * 8f, 0.85f, col % 2 == 0 ? 0f : 8f));
            for (int i = 0; i < 8; i++)
            {
                props.Add(new ExpeditionProp(ExpeditionPropKind.Column, -19f + i * 5.4f, 33f, 1.15f));
                if (i % 2 == 0) props.Add(new ExpeditionProp(ExpeditionPropKind.Rubble,
                    -16f + i * 5.4f, 29f, 1.2f));
            }
            props.Add(new ExpeditionProp(ExpeditionPropKind.Wall, 0f, 45f, 2.2f));
            props.Add(new ExpeditionProp(ExpeditionPropKind.Altar, 2f, 39f, 1.5f));
            props.Add(new ExpeditionProp(ExpeditionPropKind.Torch, -16f, 41f));
            props.Add(new ExpeditionProp(ExpeditionPropKind.Torch, 16f, 41f));
            return new ExpeditionMapPlan(kind, "expedition:roadside-chapel",
                "Придорожная часовня", 105f, 118f, -49f, 39f, props);
        }
    }
}
