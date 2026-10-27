// Assets/Scripts/Gameplay/MapLoader.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Собирает поле «Перетягивания каната» из файла карты (<see cref="MapData"/>; 54-TUG.md) —
    /// в игре, а не в редакторе: карты лежат в папке и правятся без пересборки сцен.
    ///
    /// В <c>Awake</c> — до выпечки навмеша (<see cref="GroundNavMesh"/> печёт в <c>Start</c>):
    /// деревья, палатки и склепы должны попасть в навмеш препятствиями.
    ///
    /// Пока ставит: землю по размеру карты, вещи, склепы с тронами, метки подлокаций, троп
    /// и событий, Греховода игрока в его склепе. Жители подлокаций, Греховоды-ИИ, охотники
    /// волнами и события — шаги 1–4 (54-TUG.md).
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        public static MapData Current { get; private set; }

        void Awake()
        {
            var map = MapSession.Map;
            if (map == null)
            {
                var all = MapData.List();
                if (all.Count == 0) { Debug.LogError("[КАРТЫ] В папке карт нет ни одной карты."); return; }
                MapSession.Choose(all[0].path, all[0].map);
                map = all[0].map;
            }
            Current = map;

            var ground = GameObject.Find("Поле");
            if (ground != null) ground.transform.localScale = new Vector3(map.Size / 10f, 1f, map.Size / 10f);

            var root = new GameObject(Loc.F("Карта — {0}", map.Name)).transform;
            foreach (var t in map.Things) Place(t.Prop, root, new Vector3(t.X, 0f, t.Z), t.Yaw, t.Scale);

            for (int i = 0; i < map.Crypts.Count; i++)
            {
                var c = map.Crypts[i];
                var at = new Vector3(c.X, 0f, c.Z);
                var rot = Quaternion.Euler(0f, c.Yaw, 0f);
                var crypt = new GameObject(Loc.F("Склеп {0}", i + 1)).transform;
                crypt.SetParent(root, false);
                crypt.SetPositionAndRotation(at, rot);
                Place("CryptGate", crypt, at + rot * new Vector3(0f, 0f, 6f), c.Yaw, 1f);
                Place("Throne", crypt, at + rot * new Vector3(0f, 0f, -2f), c.Yaw, 1f);
            }

            foreach (var l in map.Locations) Mark(root, Loc.F("Подлокация — {0}", Loc.T(l.Name)), new Vector3(l.X, 0f, l.Z));
            foreach (var s in map.Trails) Mark(root, Loc.T("Тропа охотников"), new Vector3(s.X, 0f, s.Z));
            foreach (var s in map.Events) Mark(root, Loc.F("Событие — {0}", s.Kind), new Vector3(s.X, 0f, s.Z));

            // Греховод игрока — в склепе первой стороны с местом «Игрок».
            int seat = Mathf.Max(0, MapSession.Seats.IndexOf(MapSession.Seat.Player));
            if (seat < map.Crypts.Count)
            {
                var c = map.Crypts[seat];
                var entry = new GameObject("Появление Греховода");
                entry.transform.position = new Vector3(c.X, 0f, c.Z) + Quaternion.Euler(0f, c.Yaw, 0f) * new Vector3(0f, 0f, 9f);
                entry.AddComponent<Crypt.TestChamberEntry>();
            }
        }

        /// <summary>Модель из Resources/Props — как ставит сборщик сцен: поворот импорта сохраняется.</summary>
        private static void Place(string prop, Transform parent, Vector3 at, float yaw, float scale)
        {
            var prefab = Resources.Load<GameObject>("Props/" + prop);
            if (prefab == null) { Debug.LogWarning($"[КАРТЫ] Нет модели «{prop}» в Resources/Props."); return; }
            var go = Instantiate(prefab, parent);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
            go.transform.localScale = prefab.transform.localScale * scale;
        }

        /// <summary>Невидимая метка места: по ней ставят жителей, охотников и события (шаги 1–4).</summary>
        private static void Mark(Transform parent, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
        }
    }
}
