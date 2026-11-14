using System;
using System.Linq;
using System.Reflection;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using UnityEngine;

internal static class MapVisualChecks
{
    public static void Run()
    {
        var map = MultiplayerTestMap.Create();
        if (map.Crypts.Count != 2 || map.Size < 180f || map.Size > 350f)
            throw new Exception("QA map must have two crypts and remain compact");
        var a = map.Crypts[0];
        var b = map.Crypts[1];
        if (Math.Abs(a.X - b.X) > 0.001f ||
            Math.Abs(a.Z + b.Z) > 0.001f ||
            Math.Abs(a.Yaw - b.Yaw) != 180f)
            throw new Exception("Two-Lord test map is not symmetric");

        if (map.Locations.Count < 5 ||
            !map.Locations.Any(x => x.Kind == "forest") ||
            !map.Locations.Any(x => x.Kind == "field") ||
            !map.Locations.Any(x => x.Kind == "village") ||
            map.Events.Count < 4 || map.Trails.Count < 2)
            throw new Exception("Insufficient real-gameplay QA stations");
        if (!map.Things.Any(x => x.Prop == "Campfire") ||
            !map.Things.Any(x => x.Prop == "Tent") ||
            !map.Things.Any(x => x.Prop == "TorchStand"))
            throw new Exception("Missing interactive environmental props");

        foreach (var p in map.Things)
            if (Math.Abs(p.X) >= map.Size / 2f ||
                Math.Abs(p.Z) >= map.Size / 2f || p.Scale <= 0f)
                throw new Exception("Out-of-bounds or invalid test prop: " + p.Prop);
        foreach (var p in map.Trails.Concat(map.Events))
            if (Math.Abs(p.X) >= map.Size / 2f ||
                Math.Abs(p.Z) >= map.Size / 2f)
                throw new Exception("Out-of-bounds event or hunter origin");

        MultiplayerTestMap.Choose();
        if (MapSession.Seats.Count != 2 ||
            MapSession.Seats[0] != MapSession.Seat.Player ||
            MapSession.Seats[1] != MapSession.Seat.AiMedium ||
            MapSession.Tutorial)
            throw new Exception("QA map seat configuration is wrong");

        // Exercise geometry/material code, not just source syntax. It must
        // work without a running gameplay system and without UnityEditor.
        var altar = new GameObject("Test visual altar");
        int before = GameObject.PrimitiveObjects.Count;
        var install = typeof(AltarVisualEnhancer).GetMethod("Install",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (install == null) throw new Exception("Missing altar decorator");
        install.Invoke(null, new object[] { altar, false });
        int after = GameObject.PrimitiveObjects.Count;
        if (after - before < 16)
            throw new Exception("Altar visual factory produced insufficient geometry");
        foreach (var primitive in GameObject.PrimitiveObjects.Skip(before))
        {
            var collider = primitive.GetComponent<Collider>();
            if (collider == null || collider.enabled)
                throw new Exception("Decoration has an active collider: " + primitive.name);
        }
        install.Invoke(null, new object[] { altar, true });
        if (GameObject.PrimitiveObjects.Count != after)
            throw new Exception("Altar decorator duplicated an existing ornament set");
        // The second call must be idempotent on an already decorated target.

        Console.WriteLine("PASS: symmetrical full-gameplay QA map and altar visual factory");
    }
}
