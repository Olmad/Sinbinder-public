// A compact REAL-gameplay proving ground for future Sinbinder multiplayer.
// Important: map scenarios exercise existing local mechanics; the current LAN
// transport still only replicates the two synthetic test pawns.
using UnityEngine;

namespace Sinbinder.Core
{
    public static class MultiplayerTestMap
    {
        public static MapData Create()
        {
            var map = new MapData
            {
                Name = Loc.N("Сетевой полигон"),
                Author = "Sinbinder QA",
                Size = 240f
            };
            // Symmetric two-crypt layout: siege/door, throne defeat and
            // commander resurrection all use the real TugMatch logic.
            map.Crypts.Add(new MapData.Crypt { X = 0f, Z = -91f, Yaw = 0f });
            map.Crypts.Add(new MapData.Crypt { X = 0f, Z = 91f, Yaw = 180f });

            // The residents are neutral, not decorative dummies: this
            // exercises their AOS, recruitment, loyalty and rival interaction.
            map.Locations.Add(new MapData.Location {
                Kind = "village", Name = Loc.N("Нейтральная деревня"),
                X = 0f, Z = 0f, Radius = 18f, People = 10
            });
            map.Locations.Add(new MapData.Location {
                Kind = "forest", Name = Loc.N("Лес и дозор"),
                X = -54f, Z = -12f, Radius = 20f, People = 7
            });
            map.Locations.Add(new MapData.Location {
                Kind = "field", Name = Loc.N("Открытое поле"),
                X = 54f, Z = 12f, Radius = 20f, People = 7
            });
            map.Locations.Add(new MapData.Location {
                Kind = "village", Name = Loc.N("Северный опорный пункт"),
                X = -49f, Z = 51f, Radius = 15f, People = 5
            });
            map.Locations.Add(new MapData.Location {
                Kind = "forest", Name = Loc.N("Южный дозор"),
                X = 49f, Z = -51f, Radius = 15f, People = 5
            });

            // Real event director and hunter-wave origins. Keep trails at
            // edge but inside the 240 m field.
            map.Trails.Add(new MapData.Spot { Kind = "hunters", X = -111f, Z = 0f });
            map.Trails.Add(new MapData.Spot { Kind = "hunters", X = 111f, Z = 0f });
            map.Events.Add(new MapData.Spot { Kind = "caravan", X = -34f, Z = -37f });
            map.Events.Add(new MapData.Spot { Kind = "caravan", X = 34f, Z = 37f });
            map.Events.Add(new MapData.Spot { Kind = "village", X = -38f, Z = 35f });
            map.Events.Add(new MapData.Spot { Kind = "village", X = 38f, Z = -35f });

            // MapLoader instantiates the existing MapProps prefabs. No new
            // physics, scripts or surrogate combat rules are introduced.
            Prop(map, "Campfire", -7f, -4f);
            Prop(map, "Well", 8f, 5f);
            Prop(map, "Tent", -16f, 7f, 50f, 1.1f);
            Prop(map, "Tent", 14f, -10f, -40f, 1.1f);
            Prop(map, "Crate", -8f, -9f, 25f);
            Prop(map, "Barrel", 11f, 10f, -15f);
            Prop(map, "Woodpile", -52f, -19f);
            Prop(map, "LogBench", -48f, 50f);
            Prop(map, "TorchStand", -13f, -13f);
            Prop(map, "TorchStand", 13f, 13f);

            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side * 56f;
                for (int i = 0; i < 14; i++)
                {
                    float angle = (i * 137f + (side == -1 ? 0f : 60f)) * Mathf.Deg2Rad;
                    float distance = 8f + (i % 4) * 3.1f;
                    Prop(map, i % 3 == 0 ? "TallPineA" : "Pine",
                        sx + Mathf.Cos(angle) * distance,
                        (side < 0 ? -12f : 12f) + Mathf.Sin(angle) * distance,
                        i * 23f, 0.82f + (i % 4) * 0.09f);
                }
            }

            // The central corridor must stay clear so AOS and NavMesh
            // decisions can be observed instead of fighting scenery.
            Prop(map, "Rock", -30f, 19f, 48f, 1.1f);
            Prop(map, "Rock", 30f, -19f, 20f, 1.1f);
            return map;
        }

        public static void Choose()
        {
            MapSession.Choose(null, Create());
            // Real opponent AI is deliberate here: AOS, combat and orders can
            // run before the two-Lord networking layer is integrated.
            if (MapSession.Seats.Count >= 2)
                MapSession.Seats[1] = MapSession.Seat.AiMedium;
            // Allow the existing long-match Great Hunt to be tested too.
            MapSession.GreatHunt = true;
            MapSession.Tutorial = false;
        }

        private static void Prop(MapData map, string name,
            float x, float z, float yaw = 0f, float scale = 1f)
        {
            map.Things.Add(new MapData.Thing
            {
                Prop = name, X = x, Z = z, Yaw = yaw, Scale = scale
            });
        }
    }
}
