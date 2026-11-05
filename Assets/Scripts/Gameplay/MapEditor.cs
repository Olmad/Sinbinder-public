// Assets/Scripts/Gameplay/MapEditor.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Редактор карт «Перетягивания каната» (автор, 27.10: «нужен редактор карт»; 54-TUG.md, шаг 6).
    /// Правит то же описание (<see cref="MapData"/>), что собирает поле (<see cref="MapLoader"/>):
    /// что поставил здесь — то и будет на поле.
    ///
    /// Управление: камера — W A S D (Shift — быстрее), колесо — выше или ниже, Q / E — поворот того,
    /// что ставишь; левая кнопка — поставить выбранное, правая — убрать ближайшее под мышью.
    /// Слева — палитра и поля карты; «Записать» кладёт файл в папку карт — он сразу в списке
    /// «Карты „Перетягивания“». Окно — простое (IMGUI): это инструмент, а не экран игры.
    /// </summary>
    public class MapEditor : MonoBehaviour
    {
        private enum Kind { Thing, Crypt, Location, Trail, Event }

        private struct Brush { public string Title; public Kind Kind; public string Arg; }

        private static readonly Brush[] Palette =
        {
            new() { Title = Loc.N("Сосна"), Kind = Kind.Thing, Arg = "Pine" },
            new() { Title = Loc.N("Высокая сосна"), Kind = Kind.Thing, Arg = "TallPineA" },
            new() { Title = Loc.N("Сухое дерево"), Kind = Kind.Thing, Arg = "DeadTreeA" },
            new() { Title = Loc.N("Куст"), Kind = Kind.Thing, Arg = "Bush" },
            new() { Title = Loc.N("Камень"), Kind = Kind.Thing, Arg = "Rock" },
            new() { Title = Loc.N("Пень"), Kind = Kind.Thing, Arg = "Stump" },
            new() { Title = Loc.N("Палатка"), Kind = Kind.Thing, Arg = "Tent" },
            new() { Title = Loc.N("Костёр"), Kind = Kind.Thing, Arg = "Campfire" },
            new() { Title = Loc.N("Частокол"), Kind = Kind.Thing, Arg = "Palisade" },
            new() { Title = Loc.N("Бочка"), Kind = Kind.Thing, Arg = "Barrel" },
            new() { Title = Loc.N("Ящик"), Kind = Kind.Thing, Arg = "Crate" },
            new() { Title = Loc.N("Склеп игрока"), Kind = Kind.Crypt },
            new() { Title = Loc.N("Деревня"), Kind = Kind.Location, Arg = "village" },
            new() { Title = Loc.N("Лес"), Kind = Kind.Location, Arg = "forest" },
            new() { Title = Loc.N("Поле"), Kind = Kind.Location, Arg = "field" },
            new() { Title = Loc.N("Тропа охотников"), Kind = Kind.Trail, Arg = "hunters" },
            new() { Title = Loc.N("Событие: обоз"), Kind = Kind.Event, Arg = "caravan" },
            new() { Title = Loc.N("Событие: деревня"), Kind = Kind.Event, Arg = "village" },
        };

        private MapData _map;
        private string _path;
        private int _brush;
        private float _yaw;
        private string _name;
        private string _say = "";
        private Vector2 _scroll;
        private readonly List<GameObject> _shown = new();
        private readonly List<Material> _previewMaterials = new();
        private Camera _cam;
        private float _height = 70f;

        void Start()
        {
            _cam = Camera.main;
            // Свободная камера редактора: тела нет, и игровая камера не нужна.
            var rig = _cam != null ? _cam.GetComponent<RTS_Camera>() : null;
            if (rig != null) rig.enabled = false;

            // Правки не попадают в лобби до успешного сохранения.
            _map = MapSession.Map == null
                ? new MapData { Name = Loc.T("Новая карта"), Size = 300f }
                : JsonUtility.FromJson<MapData>(JsonUtility.ToJson(MapSession.Map));
            _path = MapSession.Path;
            _name = _map.Name;
            if (_cam != null) _cam.transform.SetPositionAndRotation(new Vector3(0f, _height, -_height * 0.35f), Quaternion.Euler(72f, 0f, 0f));
            Redraw();
        }

        void Update()
        {
            if (_cam == null) return;
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            float speed = (Input.GetKey(KeyCode.LeftShift) ? 90f : 35f) * Time.unscaledDeltaTime;
            _height = Mathf.Clamp(_height - Input.mouseScrollDelta.y * 6f, 15f, 260f);
            var p = _cam.transform.position + input * speed;
            p.y = _height;
            _cam.transform.position = p;

            if (Input.GetKey(KeyCode.Q)) _yaw -= 90f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.E)) _yaw += 90f * Time.unscaledDeltaTime;

            // Щелчок по окну палитры — не по карте.
            if (Input.mousePosition.x < PanelWidth + 20f) return;
            if (!Ground(out var at)) return;
            if (Input.GetMouseButtonDown(0)) Put(at);
            if (Input.GetMouseButtonDown(1)) Remove(at);
        }

        private const float PanelWidth = 260f;

        private bool Ground(out Vector3 at)
        {
            at = default;
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float d)) return false;
            at = ray.GetPoint(d);
            float half = _map.Size * 0.5f;
            return Mathf.Abs(at.x) <= half && Mathf.Abs(at.z) <= half;
        }

        private void Put(Vector3 at)
        {
            var b = Palette[_brush];
            switch (b.Kind)
            {
                case Kind.Thing: _map.Things.Add(new MapData.Thing { Prop = b.Arg, X = at.x, Z = at.z, Yaw = _yaw, Scale = 1f }); break;
                case Kind.Crypt: _map.Crypts.Add(new MapData.Crypt { X = at.x, Z = at.z, Yaw = _yaw }); break;
                case Kind.Location: _map.Locations.Add(new MapData.Location { Kind = b.Arg, Name = Loc.T(b.Title), X = at.x, Z = at.z }); break;
                case Kind.Trail: _map.Trails.Add(new MapData.Spot { Kind = b.Arg, X = at.x, Z = at.z }); break;
                case Kind.Event: _map.Events.Add(new MapData.Spot { Kind = b.Arg, X = at.x, Z = at.z }); break;
            }
            Redraw();
        }

        /// <summary>Убрать ближайшее к точке (в 4 м), что бы это ни было.</summary>
        private void Remove(Vector3 at)
        {
            float best = 4f; System.Action drop = null;
            void Try(float x, float z, System.Action act)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(at.x, at.z));
                if (d < best) { best = d; drop = act; }
            }
            foreach (var t in _map.Things) { var k = t; Try(k.X, k.Z, () => _map.Things.Remove(k)); }
            foreach (var c in _map.Crypts) { var k = c; Try(k.X, k.Z, () => _map.Crypts.Remove(k)); }
            foreach (var l in _map.Locations) { var k = l; Try(k.X, k.Z, () => _map.Locations.Remove(k)); }
            foreach (var s in _map.Trails) { var k = s; Try(k.X, k.Z, () => _map.Trails.Remove(k)); }
            foreach (var s in _map.Events) { var k = s; Try(k.X, k.Z, () => _map.Events.Remove(k)); }
            if (drop == null) return;
            drop();
            Redraw();
        }

        // ---------------------------------------------------------- вид

        private void ClearPreview()
        {
            foreach (var go in _shown) if (go != null) Destroy(go);
            _shown.Clear();
            foreach (var material in _previewMaterials) if (material != null) Destroy(material);
            _previewMaterials.Clear();
        }

        void OnDestroy() => ClearPreview();

        private void Redraw()
        {
            ClearPreview();

            var ground = GameObject.Find("Поле");
            if (ground != null) ground.transform.localScale = new Vector3(_map.Size / 10f, 1f, _map.Size / 10f);

            foreach (var t in _map.Things) Model(t.Prop, new Vector3(t.X, 0f, t.Z), t.Yaw, t.Scale);
            // Склеп — тот же, что встанет на поле: пол, стены, дверь, трон (MapLoader).
            foreach (var c in _map.Crypts) Model("Crypt", new Vector3(c.X, 0f, c.Z), c.Yaw, 1f);
            foreach (var l in _map.Locations) Disc(new Vector3(l.X, 0.05f, l.Z), l.Radius, LocationColour(l.Kind));
            foreach (var s in _map.Trails) Disc(new Vector3(s.X, 0.06f, s.Z), 3f, new Color(0.8f, 0.15f, 0.12f));
            foreach (var s in _map.Events) Disc(new Vector3(s.X, 0.07f, s.Z), 2.5f, new Color(0.95f, 0.8f, 0.2f));
        }

        private static Color LocationColour(string kind) => kind switch
        {
            "forest" => new Color(0.15f, 0.35f, 0.18f),
            "field" => new Color(0.55f, 0.50f, 0.22f),
            _ => new Color(0.45f, 0.30f, 0.18f),
        };

        private void Model(string prop, Vector3 at, float yaw, float scale)
        {
            var prefab = MapLoader.Prefab(prop);
            if (prefab == null) return;
            var go = Instantiate(prefab);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
            go.transform.localScale = prefab.transform.localScale * scale;
            _shown.Add(go);
        }

        /// <summary>Плоский круг на земле — граница подлокации, тропа, событие.</summary>
        private void Disc(Vector3 at, float radius, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at;
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            var r = go.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Sprites/Default")) { color = colour };
            r.sharedMaterial = material;
            _previewMaterials.Add(material);
            _shown.Add(go);
        }

        // ---------------------------------------------------------- окно

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, PanelWidth, Screen.height - 20), GUI.skin.box);
            GUILayout.Label(Loc.T("Редактор карт"));

            GUILayout.Label(Loc.T("Имя карты"));
            _name = GUILayout.TextField(_name ?? "");
            GUILayout.Label(Loc.F("Поле: {0} м", Mathf.RoundToInt(_map.Size)));
            float size = GUILayout.HorizontalSlider(_map.Size, 150f, 800f);
            if (Mathf.Abs(size - _map.Size) > 1f) { _map.Size = Mathf.Round(size / 10f) * 10f; Redraw(); }

            GUILayout.Label(Loc.F("Склепов: {0}  ·  подлокаций: {1}  ·  событий: {2}",
                _map.Crypts.Count, _map.Locations.Count, _map.Events.Count));
            GUILayout.Label(Loc.F("Поворот (Q / E): {0}°", Mathf.RoundToInt(Mathf.Repeat(_yaw, 360f))));

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Screen.height * 0.45f));
            for (int i = 0; i < Palette.Length; i++)
            {
                bool on = GUILayout.Toggle(i == _brush, Loc.T(Palette[i].Title), "Button");
                if (on) _brush = i;
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button(Loc.T("Записать"))) Save();
            if (GUILayout.Button(Loc.T("Очистить всё"))) { _map.Things.Clear(); _map.Crypts.Clear(); _map.Locations.Clear(); _map.Trails.Clear(); _map.Events.Clear(); Redraw(); }
            if (GUILayout.Button(Loc.T("Выйти в игру"))) SceneManager.LoadScene(0);
            if (!string.IsNullOrEmpty(_say)) GUILayout.Label(_say);
            GUILayout.Label(Loc.T("ЛКМ — поставить, ПКМ — убрать, W A S D — камера, колесо — высота"));
            GUILayout.EndArea();
        }

        private void Save()
        {
            if (_map.Crypts.Count < 2) { _say = Loc.T("Нужно хотя бы два склепа — по одному на игрока."); return; }
            _map.Name = string.IsNullOrWhiteSpace(_name) ? Loc.T("Новая карта") : _name.Trim();
            try
            {
                string destination = _path;
                if (string.IsNullOrEmpty(destination))
                {
                    string file = string.Join("_", _map.Name.Split(Path.GetInvalidFileNameChars())).Trim().Trim('.');
                    if (string.IsNullOrEmpty(file)) file = "SinbinderMap";
                    destination = Path.Combine(MapData.Folder, file + ".json");
                    int suffix = 2;
                    while (File.Exists(destination))
                        destination = Path.Combine(MapData.Folder, file + "_" + suffix++ + ".json");
                }
                var savedMap = JsonUtility.FromJson<MapData>(JsonUtility.ToJson(_map));
                if (File.Exists(destination)) File.Copy(destination, destination + ".bak", true);
                _map.Save(destination);
                _path = destination;
                if (MapSession.Map != null && MapSession.Seats.Count == savedMap.Crypts.Count)
                {
                    MapSession.Path = _path;
                    MapSession.Map = savedMap;
                    MapSession.Tutorial = false;
                }
                else MapSession.Choose(_path, savedMap);
                _say = Loc.F("Записано: {0}", Path.GetFileName(_path));
            }
            catch (System.Exception error)
            {
                _say = Loc.F("Не удалось сохранить карту: {0}", error.Message);
                Debug.LogError("[Редактор карт] " + error);
            }
        }
    }
}
