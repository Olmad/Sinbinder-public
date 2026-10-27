// Assets/Scripts/Core/MapData.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Карта «Перетягивания каната» — файлом (54-TUG.md; автор, 27.10: «нужен редактор карт, и меню,
    /// где отображаются карты в определённой папке»). Описание, а не сцена: поле, склепы с тронами,
    /// подлокации, вещи, тропы охотников, места событий. Собирает его в игре <c>Gameplay.MapLoader</c>,
    /// пишет и правит — редактор карт. Формат — JSON (JsonUtility): читается глазами и правится руками.
    ///
    /// Папка — <see cref="Folder"/>: рядом с игрой (в редакторе — в корне проекта). Пусто — кладём
    /// две карты по умолчанию, на двоих и на четверых (<see cref="Defaults"/>).
    /// </summary>
    [System.Serializable]
    public class MapData
    {
        public string Name = "";
        public string Author = "";
        /// <summary>Сторона квадрата поля, метры.</summary>
        public float Size = 400f;
        public List<Crypt> Crypts = new();
        public List<Location> Locations = new();
        public List<Thing> Things = new();
        public List<Spot> Trails = new();
        public List<Spot> Events = new();

        /// <summary>Склеп игрока с троном: здесь появляется Греховод стороны.</summary>
        [System.Serializable] public class Crypt { public float X, Z, Yaw; }

        /// <summary>Подлокация: деревня, лес, поле… — очаг, у которого живут её воины.</summary>
        [System.Serializable] public class Location { public string Kind = "village"; public string Name = ""; public float X, Z, Radius = 18f; public int People = 12; }

        /// <summary>Вещь: модель из Resources/Props (Pine, Tent, Rock…), место, поворот, размер.</summary>
        [System.Serializable] public class Thing { public string Prop = "Pine"; public float X, Z, Yaw, Scale = 1f; }

        /// <summary>Точка: тропа охотников или событие («обоз», «деревня»…).</summary>
        [System.Serializable] public class Spot { public string Kind = ""; public float X, Z; }

        public int Players => Crypts.Count;

        // ---------------------------------------------------------- папка

        /// <summary>Папка карт: рядом с игрой; в редакторе — <c>Maps</c> в корне проекта.</summary>
        public static string Folder
        {
            get
            {
                var root = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(root ?? ".", "Maps");
            }
        }

        /// <summary>Все карты папки: имя файла → описание. Битый файл — пропускаем, с предупреждением.</summary>
        public static List<(string path, MapData map)> List()
        {
            var all = new List<(string, MapData)>();
            Directory.CreateDirectory(Folder);
            if (Directory.GetFiles(Folder, "*.json").Length == 0) WriteDefaults();

            foreach (var path in Directory.GetFiles(Folder, "*.json"))
            {
                var map = Load(path);
                if (map != null) all.Add((path, map));
            }
            all.Sort((a, b) => string.CompareOrdinal(a.Item2.Name, b.Item2.Name));
            return all;
        }

        public static MapData Load(string path)
        {
            try
            {
                var map = JsonUtility.FromJson<MapData>(File.ReadAllText(path));
                if (map == null || map.Crypts.Count < 2) { Debug.LogWarning($"[КАРТЫ] {path}: меньше двух склепов — не карта."); return null; }
                if (string.IsNullOrEmpty(map.Name)) map.Name = Path.GetFileNameWithoutExtension(path);
                return map;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[КАРТЫ] {path}: не читается ({e.Message}).");
                return null;
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Folder);
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        // ---------------------------------------------------------- по умолчанию

        private static void WriteDefaults()
        {
            foreach (var map in Defaults())
                map.Save(Path.Combine(Folder, map.Name + ".json"));
        }

        /// <summary>
        /// Две карты: «Перекрёсток» на двоих и «Четыре склепа» на четверых. Склепы — по краям;
        /// в середине деревня, по сторонам лес и поле; тропы охотников — на середине краёв,
        /// дальше всего от склепов (автор, 27.10); событий — вдвое больше, чем игроков.
        /// Деревья — кольцами по формуле, без жребия.
        /// </summary>
        public static List<MapData> Defaults()
        {
            var two = new MapData { Name = Loc.N("Перекрёсток"), Author = "Sinbinder", Size = 300f };
            two.Crypts.Add(new Crypt { X = 0, Z = -125, Yaw = 0 });
            two.Crypts.Add(new Crypt { X = 0, Z = 125, Yaw = 180 });
            two.Locations.Add(new Location { Kind = "village", Name = Loc.N("Деревня"), X = 0, Z = 0, Radius = 22, People = 14 });
            two.Locations.Add(new Location { Kind = "forest", Name = Loc.N("Бор"), X = -80, Z = 10, Radius = 26, People = 8 });
            two.Locations.Add(new Location { Kind = "field", Name = Loc.N("Поле"), X = 80, Z = -10, Radius = 26, People = 8 });
            two.Trails.Add(new Spot { Kind = "hunters", X = -145, Z = 0 });
            two.Trails.Add(new Spot { Kind = "hunters", X = 145, Z = 0 });
            two.Events.Add(new Spot { Kind = "caravan", X = -40, Z = -60 });
            two.Events.Add(new Spot { Kind = "caravan", X = 40, Z = 60 });
            two.Events.Add(new Spot { Kind = "village", X = -90, Z = -70 });
            two.Events.Add(new Spot { Kind = "village", X = 90, Z = 70 });
            Grove(two, -80, 10, 26);
            Grove(two, 0, 0, 0, tents: 6);

            var four = new MapData { Name = Loc.N("Четыре склепа"), Author = "Sinbinder", Size = 400f };
            four.Crypts.Add(new Crypt { X = -165, Z = -165, Yaw = 45 });
            four.Crypts.Add(new Crypt { X = 165, Z = -165, Yaw = -45 });
            four.Crypts.Add(new Crypt { X = 165, Z = 165, Yaw = -135 });
            four.Crypts.Add(new Crypt { X = -165, Z = 165, Yaw = 135 });
            four.Locations.Add(new Location { Kind = "village", Name = Loc.N("Деревня"), X = 0, Z = 0, Radius = 26, People = 16 });
            four.Locations.Add(new Location { Kind = "forest", Name = Loc.N("Бор"), X = -110, Z = 0, Radius = 28, People = 10 });
            four.Locations.Add(new Location { Kind = "forest", Name = Loc.N("Ельник"), X = 110, Z = 0, Radius = 28, People = 10 });
            four.Locations.Add(new Location { Kind = "field", Name = Loc.N("Северное поле"), X = 0, Z = 110, Radius = 28, People = 10 });
            four.Locations.Add(new Location { Kind = "field", Name = Loc.N("Южное поле"), X = 0, Z = -110, Radius = 28, People = 10 });
            foreach (var (x, z) in new[] { (0f, -195f), (195f, 0f), (0f, 195f), (-195f, 0f) })
                four.Trails.Add(new Spot { Kind = "hunters", X = x, Z = z });
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                four.Events.Add(new Spot { Kind = i % 2 == 0 ? "caravan" : "village", X = Mathf.Cos(a) * 75f, Z = Mathf.Sin(a) * 75f });
            }
            Grove(four, -110, 0, 28);
            Grove(four, 110, 0, 28);
            Grove(four, 0, 0, 0, tents: 8);

            return new List<MapData> { two, four };
        }

        /// <summary>Лес — кольцами сосен; деревня — палатки по кругу. По формуле, не жребием.</summary>
        private static void Grove(MapData map, float cx, float cz, float radius, int tents = 0)
        {
            if (tents > 0)
            {
                for (int i = 0; i < tents; i++)
                {
                    float a = i * 360f / tents;
                    var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 12f);
                    map.Things.Add(new Thing { Prop = "Tent", X = cx + p.x, Z = cz + p.z, Yaw = a + 180f, Scale = 1.5f });
                }
                map.Things.Add(new Thing { Prop = "Campfire", X = cx, Z = cz });
                return;
            }
            for (int ring = 0; ring < 3; ring++)
            {
                int count = 10 + ring * 6;
                float r = radius * (0.35f + ring * 0.3f);
                for (int i = 0; i < count; i++)
                {
                    float a = (i * 360f / count + ring * 17f) * Mathf.Deg2Rad;
                    string kind = (i + ring) % 3 == 0 ? "TallPineA" : (i + ring) % 3 == 1 ? "TallPineB" : "Pine";
                    map.Things.Add(new Thing { Prop = kind, X = cx + Mathf.Cos(a) * r, Z = cz + Mathf.Sin(a) * r, Yaw = i * 37f % 360f, Scale = 0.9f + (i * 7 % 5) * 0.06f });
                }
            }
        }
    }

    /// <summary>
    /// Что выбрано перед партией: карта и кто играет каждой стороной (лобби, как в Warcraft III).
    /// Живёт между сценами: меню карт → лобби → поле.
    /// </summary>
    public static class MapSession
    {
        public enum Seat { Player, AiWeak, AiMedium, AiStrong, Empty }

        public static string Path { get; set; }
        public static MapData Map { get; set; }
        public static List<Seat> Seats { get; } = new();

        public static void Choose(string path, MapData map)
        {
            Path = path;
            Map = map;
            Seats.Clear();
            for (int i = 0; i < map.Players; i++) Seats.Add(i == 0 ? Seat.Player : Seat.AiWeak);
        }
    }
}
