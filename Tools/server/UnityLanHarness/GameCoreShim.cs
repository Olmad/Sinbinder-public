// This file is ONLY a CI shim. The real MapData and BindingDevice are
// compiled by Unity from Assets/Scripts. Keep their API signatures aligned.
using System.Collections.Generic;

namespace Sinbinder.Crypt
{
    public class BindingDevice : UnityEngine.MonoBehaviour { }
}

namespace Sinbinder.Core
{
    public static class Loc
    {
        public static string N(string key) { return key; }
    }
    public class MapData
    {
        public string Name = "";
        public string Author = "";
        public float Size;
        public readonly List<Crypt> Crypts = new List<Crypt>();
        public readonly List<Location> Locations = new List<Location>();
        public readonly List<Thing> Things = new List<Thing>();
        public readonly List<Spot> Trails = new List<Spot>();
        public readonly List<Spot> Events = new List<Spot>();

        public class Crypt { public float X, Z, Yaw; }
        public class Location
        {
            public string Kind, Name;
            public float X, Z, Radius;
            public int People;
        }
        public class Thing
        {
            public string Prop;
            public float X, Z, Yaw, Scale;
        }
        public class Spot
        {
            public string Kind;
            public float X, Z;
        }
    }

    public static class MapSession
    {
        public enum Seat { Player, AiWeak, AiMedium, AiStrong, Empty }
        public static readonly List<Seat> Seats = new List<Seat>();
        public static bool GreatHunt;
        public static bool Tutorial;
        public static MapData Map;
        public static void Choose(string path, MapData map)
        {
            Map = map;
            Seats.Clear();
            for (int i = 0; i < map.Crypts.Count; i++)
                Seats.Add(i == 0 ? Seat.Player : Seat.AiWeak);
        }
    }
}
