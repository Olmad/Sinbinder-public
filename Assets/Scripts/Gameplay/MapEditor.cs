// Assets/Scripts/Gameplay/MapEditor.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
        private enum Kind { Thing, Crypt, Location, Trail, Event, Region, Trigger }
        private enum Mode { Place, Select, Erase, Terrain, Paint, Vegetation, Template }

        private struct Brush { public string Title; public Kind Kind; public string Arg; }

        private static readonly Brush[] Palette =
        {
            new() { Title = Loc.N("Сосна"), Kind = Kind.Thing, Arg = "Pine" },
            new() { Title = Loc.N("Высокая сосна"), Kind = Kind.Thing, Arg = "TallPineA" },
            new() { Title = Loc.N("Высокая сосна B"), Kind = Kind.Thing, Arg = "TallPineB" },
            new() { Title = Loc.N("Высокая сосна C"), Kind = Kind.Thing, Arg = "TallPineC" },
            new() { Title = Loc.N("Сухое дерево"), Kind = Kind.Thing, Arg = "DeadTreeA" },
            new() { Title = Loc.N("Сухое дерево B"), Kind = Kind.Thing, Arg = "DeadTreeB" },
            new() { Title = Loc.N("Сухое дерево C"), Kind = Kind.Thing, Arg = "DeadTreeC" },
            new() { Title = Loc.N("Пучок травы"), Kind = Kind.Thing, Arg = "GrassTuft" },
            new() { Title = Loc.N("Куст"), Kind = Kind.Thing, Arg = "Bush" },
            new() { Title = Loc.N("Камень"), Kind = Kind.Thing, Arg = "Rock" },
            new() { Title = Loc.N("Пень"), Kind = Kind.Thing, Arg = "Stump" },
            new() { Title = Loc.N("Палатка"), Kind = Kind.Thing, Arg = "Tent" },
            new() { Title = Loc.N("Костёр"), Kind = Kind.Thing, Arg = "Campfire" },
            new() { Title = Loc.N("Частокол"), Kind = Kind.Thing, Arg = "Palisade" },
            new() { Title = Loc.N("Бочка"), Kind = Kind.Thing, Arg = "Barrel" },
            new() { Title = Loc.N("Ящик"), Kind = Kind.Thing, Arg = "Crate" },
            new() { Title = Loc.N("Сундук"), Kind = Kind.Thing, Arg = "Chest" },
            new() { Title = Loc.N("Поленница"), Kind = Kind.Thing, Arg = "Woodpile" },
            new() { Title = Loc.N("Плетень"), Kind = Kind.Thing, Arg = "Wattle" },
            new() { Title = Loc.N("Колодец"), Kind = Kind.Thing, Arg = "Well" },
            new() { Title = Loc.N("Бревно-скамья"), Kind = Kind.Thing, Arg = "LogBench" },
            new() { Title = Loc.N("Факел"), Kind = Kind.Thing, Arg = "Torch" },
            new() { Title = Loc.N("Хижина"), Kind = Kind.Thing, Arg = "Izba" },
            new() { Title = Loc.N("Склеп игрока"), Kind = Kind.Crypt },
            new() { Title = Loc.N("Деревня"), Kind = Kind.Location, Arg = "village" },
            new() { Title = Loc.N("Лес"), Kind = Kind.Location, Arg = "forest" },
            new() { Title = Loc.N("Поле"), Kind = Kind.Location, Arg = "field" },
            new() { Title = Loc.N("Тропа охотников"), Kind = Kind.Trail, Arg = "hunters" },
            new() { Title = Loc.N("Событие: обоз"), Kind = Kind.Event, Arg = "caravan" },
            new() { Title = Loc.N("Событие: деревня"), Kind = Kind.Event, Arg = "village" },
            new() { Title = Loc.N("Область события"), Kind = Kind.Region, Arg = "region" },
        };

        private MapData _map;
        private string _path;
        private string _openedFingerprint;
        private bool _unreadableSource;
        private int _brush;
        private float _yaw;
        private string _name;
        private string _say = "";
        private Vector2 _scroll;
        private readonly List<GameObject> _shown = new();
        private readonly HashSet<string> _missingPreviewModels = new();
        private int _loadedPreviewModels;
        private readonly List<Material> _previewMaterials = new();
        // Existing prop and marker previews remain alive while painting/sculpting;
        // only their height needs updating after a geometry change.
        private struct HeightFollower
        {
            public GameObject Object;
            public float X, Z, Offset;
        }
        private readonly List<HeightFollower> _heightFollowers = new();
        private Camera _cam;
        private float _height = 70f;
        private Mode _mode = Mode.Place;
        private Kind _selectionKind;
        private int _selectionIndex = -1;
        private readonly HashSet<string> _selectedThingIds = new();
        private bool _selectionDragActive;
        private bool _selectionDragAdditive;
        private bool _selectionDragMovesGroup;
        private List<string> _validationIssues = new();
        private Vector3 _selectionDragWorld;
        private Vector3 _selectionDragScreen;
        private float _groupStep = 5f;
        private float _snap = 2f;
        private float _terrainRadius = 18f;
        private float _terrainDelta = 3f;
        private bool _terrainRaise = true;
        private bool _terrainFlatten;
        private string _paintMaterial = "Ground048";
        private float _paintRadius = 16f;
        private string _vegetationProp = "GrassTuft";
        private string _vegetationPreset = "Single";
        private int _vegetationCount = 12;
        private float _vegetationRadius = 18f;
        private string _customProp = "";
        private string _templateDraftName = "Camp";
        private string _activeTemplateName = "";
        private MapObjectTemplate.Layout _activeTemplateLayout;
        private readonly List<GameObject> _templatePreviewMarkers = new();
        private readonly List<Material> _templatePreviewMaterials = new();
        private readonly List<GameObject> _templateGhostMeshes = new();
        private bool _templateAssetsAvailable;
        private List<string> _templateNames = new();
        private Vector2 _templateScroll;
        private GameObject[] _customPrefabs = new GameObject[0];
        private Material[] _terrainMaterials = new Material[0];
        private Material[] _objectMaterials = new Material[0];
        private bool _draggingBrush;
        private Vector3 _lastBrush;
        private float _dragFlattenHeight;
        private GameObject _brushCursor;
        private Material _brushCursorMaterial;
        private Mesh _terrainMesh;
        private Mesh _originalGroundMesh;
        private string _filter = "";
        private Vector2 _inspectorScroll;
        private readonly List<string> _undo = new();
        private readonly List<string> _redo = new();
        private bool _dirty;
        private bool _confirmExit;
        private string _savedSnapshot;
        private const int MaxHistory = 35;
        private const float InspectorWidth = 300f;
        private const float ToolbarHeight = 44f;

        void Start()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                // Scene/pipeline modifications should not leave a black
                // authoring viewport just because the MainCamera tag is absent.
                var fallback = new GameObject("Sinbinder - резервная камера редактора");
                fallback.tag = "MainCamera";
                _cam = fallback.AddComponent<Camera>();
                _say = Loc.T("Основная камера отсутствовала: создана резервная камера.");
            }
            _cam.enabled = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.28f, 0.38f, 0.48f);
            // Свободная камера редактора: тела нет, и игровая камера не нужна.
            var rig = _cam != null ? _cam.GetComponent<RTS_Camera>() : null;
            if (rig != null) rig.enabled = false;

            // Правки не попадают в лобби до успешного сохранения.
            _map = MapSession.Map == null
                ? new MapData { Name = Loc.T("Новая карта"), Size = 300f }
                : JsonUtility.FromJson<MapData>(JsonUtility.ToJson(MapSession.Map));
            _path = MapSession.Path;
            // Always read the current on-disk map when available. Using an old
            // lobby snapshot and fingerprinting a newer file would conceal
            // external changes made between the lobby and opening this scene.
            try
            {
                if (!string.IsNullOrEmpty(_path) && File.Exists(_path))
                {
                    var latest = MapData.Load(_path);
                    if (latest != null) _map = latest;
                    else
                    {
                        _unreadableSource = true;
                        _say = Loc.T("Карта на диске повреждена. Оригинал защищён; используйте «Сохранить копию».");
                    }
                }
                _openedFingerprint = FileFingerprint(_path);
            }
            catch (System.Exception error)
            {
                // A locked/unreadable disk must not crash the editor or allow
                // it to overwrite a map whose original version is unknown.
                _unreadableSource = true;
                _say = Loc.T("Не удалось прочитать карту с диска. Оригинал защищён; используйте «Сохранить копию».");
                Debug.LogWarning("[Редактор карт] Невозможно проверить файл: " + error.Message);
            }
            _map.EnsureLists();
            RefreshAssets();
            RefreshTemplates();
            EnsureIds();
            _name = _map.Name;
            _savedSnapshot = JsonUtility.ToJson(_map);
            CenterCamera();
            Redraw();
        }

        private void CenterCamera()
        {
            if (_cam == null) return;
            _height = 70f;
            _cam.transform.SetPositionAndRotation(
                new Vector3(0f, _height, -_height * 0.35f),
                Quaternion.Euler(72f, 0f, 0f));
        }


        /// <summary>Center the authoring camera on props or another selected item.</summary>
        private void FocusSelection()
        {
            if (_cam == null) return;
            float x = 0f, z = 0f;
            var group = GroupThings();
            if (group.Count > 0)
            {
                foreach (var thing in group) { x += thing.X; z += thing.Z; }
                x /= group.Count;
                z /= group.Count;
            }
            else if (SelectedPosition(out var selection))
            {
                x = selection.x;
                z = selection.z;
            }
            else
            {
                _say = Loc.T("Сначала выберите объект или область.");
                return;
            }
            _cam.transform.position = new Vector3(x, _height, z - _height * 0.35f);
            _say = Loc.T("Камера переведена к выделению.");
        }

        // Move only props that were selected BEFORE the drag began.
        private bool NearSelectedThing(Vector3 at)
        {
            if (_selectedThingIds.Count == 0) return false;
            foreach (var t in _map.Things)
                if (t != null && _selectedThingIds.Contains(t.Id))
                {
                    float dx = t.X - at.x, dz = t.Z - at.z;
                    if (dx * dx + dz * dz <= 16f) return true;
                }
            return false;
        }

        // One mouse drag = one boundary-checked group edit and one Undo step.
        private void CommitSelectionDrag(Vector3 start, Vector3 finish)
        {
            float dx = finish.x - start.x, dz = finish.z - start.z;
            if (_snap > 0f)
            {
                dx = Mathf.Round(dx / _snap) * _snap;
                dz = Mathf.Round(dz / _snap) * _snap;
            }
            MoveSelectedThings(dx, dz);
        }

        // Display all failures, not merely the first of potentially many.
        private int RunValidation()
        {
            _validationIssues = Validate();
            _say = _validationIssues.Count == 0
                ? Loc.T("Ошибок формата карты нет.")
                : Loc.F("Ошибок карты: {0}. Исправьте список ниже.", _validationIssues.Count);
            return _validationIssues.Count;
        }

        void Update()
        {
            if (!Input.GetMouseButton(0)) _draggingBrush = false;
            if (_cam == null) return;
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            float speed = (Input.GetKey(KeyCode.LeftShift) ? 90f : 35f) * Time.unscaledDeltaTime;
            _height = Mathf.Clamp(_height - Input.mouseScrollDelta.y * 6f, 15f, 260f);
            var p = _cam.transform.position + input * speed;
            p.y = _height;
            _cam.transform.position = p;

            if (Input.GetKey(KeyCode.Q)) _yaw -= 90f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.E)) _yaw += 90f * Time.unscaledDeltaTime;

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                _draggingBrush = false;
                HideBrushCursor();
                if (Input.GetKeyDown(KeyCode.Z)) Undo();
                else if (Input.GetKeyDown(KeyCode.Y)) Redo();
                else if (Input.GetKeyDown(KeyCode.S))
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                        SaveCopy();
                    else Save();
                }
                else if (Input.GetKeyDown(KeyCode.F)) FocusSelection();
                else if (Input.GetKeyDown(KeyCode.D)) DuplicateSelected();
                else if (Input.GetKeyDown(KeyCode.A) && _mode == Mode.Select) SelectAllThings();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Delete)) DeleteSelected();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_mode == Mode.Template)
                {
                    _mode = Mode.Select;
                    _activeTemplateName = "";
                    _activeTemplateLayout = null;
                    HideBrushCursor();
                    ClearTemplateGhostMeshes();
                    _say = Loc.T("Размещение шаблона отменено.");
                    return;
                }
                _selectionDragActive = false;
                _selectionDragMovesGroup = false;
                _selectionIndex = -1;
                _selectedThingIds.Clear();
                Redraw();
            }
            // The GUI takes no world clicks, including the right-hand inspector.
            if (Input.mousePosition.y > Screen.height - ToolbarHeight ||
                Input.mousePosition.x < PanelWidth + 20f ||
                Input.mousePosition.x > Screen.width - InspectorWidth - 20f)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    _selectionDragActive = false;
                    _selectionDragMovesGroup = false;
                }
                HideBrushCursor();
                return;
            }
            if (!Ground(out var at))
            {
                if (Input.GetMouseButtonUp(0))
                {
                    _selectionDragActive = false;
                    _selectionDragMovesGroup = false;
                }
                HideBrushCursor();
                return;
            }
            ShowBrushCursor(at);
            if (Input.GetMouseButtonDown(0))
            {
                if (_mode == Mode.Place) Put(Snapped(at));
                else if (_mode == Mode.Select)
                {
                    _selectionDragActive = true;
                    _selectionDragWorld = at;
                    _selectionDragScreen = Input.mousePosition;
                    _selectionDragAdditive = Input.GetKey(KeyCode.LeftShift) ||
                                             Input.GetKey(KeyCode.RightShift);
                    _selectionDragMovesGroup = !_selectionDragAdditive && NearSelectedThing(at);
                    if (!_selectionDragMovesGroup) Select(at, _selectionDragAdditive);
                }
                else if (_mode == Mode.Terrain || _mode == Mode.Paint)
                {
                    int before = _mode == Mode.Terrain
                        ? _map.TerrainStrokes.Count : _map.TerrainPaints.Count;
                    var point = Snapped(at);
                    if (_mode == Mode.Terrain) Sculpt(point);
                    else PaintTerrain(point);
                    int after = _mode == Mode.Terrain
                        ? _map.TerrainStrokes.Count : _map.TerrainPaints.Count;
                    _draggingBrush = after > before;
                    _lastBrush = point;
                }
                else if (_mode == Mode.Vegetation) ScatterVegetation(Snapped(at));
                else if (_mode == Mode.Template) PasteActiveTemplate(Snapped(at));
                else Remove(at);
            }
            else if (_draggingBrush && Input.GetMouseButton(0) &&
                     (_mode == Mode.Terrain || _mode == Mode.Paint))
                ContinueBrush(Snapped(at));
            if (_selectionDragActive && Input.GetMouseButtonUp(0))
            {
                float sx = Input.mousePosition.x - _selectionDragScreen.x;
                float sy = Input.mousePosition.y - _selectionDragScreen.y;
                if (_mode == Mode.Select && sx * sx + sy * sy > 64f)
                {
                    if (_selectionDragMovesGroup) CommitSelectionDrag(_selectionDragWorld, at);
                    else SelectRectangle(_selectionDragWorld, at, _selectionDragAdditive);
                }
                _selectionDragActive = false;
                _selectionDragMovesGroup = false;
            }
            if (Input.GetMouseButtonDown(1) && _mode != Mode.Terrain &&
                _mode != Mode.Paint && _mode != Mode.Vegetation &&
                _mode != Mode.Template) Select(at);
        }

        private const float PanelWidth = 260f;

        private bool Ground(out Vector3 at)
        {
            at = default;
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            // Hit the authored physics surface first, not the old horizontal y=0 plane.
            var ground = GameObject.Find("Поле");
            var collider = ground != null ? ground.GetComponent<Collider>() : null;
            if (collider != null && collider.Raycast(ray, out var hit, 2000f))
                at = hit.point;
            else
            {
                var plane = new Plane(Vector3.up, Vector3.zero);
                if (!plane.Raycast(ray, out float d)) return false;
                at = ray.GetPoint(d);
            }
            float half = _map.Size * 0.5f;
            return Mathf.Abs(at.x) <= half && Mathf.Abs(at.z) <= half;
        }

        private void Put(Vector3 at)
        {
            Record();
            var b = _brush < 0
                ? new Brush { Title = _customProp, Kind = Kind.Thing, Arg = _customProp }
                : Palette[_brush];
            switch (b.Kind)
            {
                case Kind.Thing: _map.Things.Add(new MapData.Thing { Id = NewId(), Prop = b.Arg, X = at.x, Z = at.z, Yaw = _yaw, Scale = 1f }); break;
                case Kind.Crypt: _map.Crypts.Add(new MapData.Crypt { X = at.x, Z = at.z, Yaw = _yaw }); break;
                case Kind.Location: _map.Locations.Add(new MapData.Location { Kind = b.Arg, Name = Loc.T(b.Title), X = at.x, Z = at.z }); break;
                case Kind.Trail: _map.Trails.Add(new MapData.Spot { Kind = b.Arg, X = at.x, Z = at.z }); break;
                case Kind.Event: _map.Events.Add(new MapData.Spot { Kind = b.Arg, X = at.x, Z = at.z }); break;
                case Kind.Region: _map.Regions.Add(new MapData.Region { Id = NewId(), Name = Loc.T("Новая область"), X = at.x, Z = at.z }); break;
            }
            Redraw();
        }

        private void RefreshAssets()
        {
            _customPrefabs = Resources.LoadAll<GameObject>("MapUserProps");
            _terrainMaterials = Resources.LoadAll<Material>("MapTerrainMaterials");
            _objectMaterials = Resources.LoadAll<Material>("MapUserMaterials");
        }

        private void RefreshTemplates() =>
            _templateNames = MapObjectTemplate.ListNames();

        // Template files are a separate library; saving never mutates a map or Undo.
        private void SaveSelectedTemplate()
        {
            if (!MapObjectTemplate.TryCapture(_map, _selectedThingIds,
                _templateDraftName, out var layout))
            {
                _say = Loc.T("Не удалось создать шаблон. Выберите до 64 моделей и имя латиницей (A-Z, 0-9, _ или -).");
                return;
            }
            if (MapObjectTemplate.TrySaveNew(layout, out _, out string error))
            {
                RefreshTemplates();
                _say = Loc.F("Шаблон сохранён: {0}. Объектов: {1}.", layout.Name, layout.Items.Count);
            }
            else _say = error;
        }

        private void ChooseTemplate(string name)
        {
            if (!MapObjectTemplate.TryLoad(name, out var layout, out string error))
            {
                _say = error;
                return;
            }
            _activeTemplateName = name;
            _activeTemplateLayout = layout;
            PrepareTemplateGhostMeshes();
            _mode = Mode.Template;
            _say = Loc.F("Шаблон {0}: ЛКМ — установить; Q/E — повернуть; Esc — отмена.", name);
        }

        private void PasteActiveTemplate(Vector3 at)
        {
            if (string.IsNullOrEmpty(_activeTemplateName))
            {
                _say = Loc.T("Шаблон не выбран.");
                return;
            }
            if (!MapObjectTemplate.TryLoad(_activeTemplateName, out var layout, out string error))
            {
                _activeTemplateLayout = null;
                HideBrushCursor();
                ClearTemplateGhostMeshes();
                _say = error;
                return;
            }
            // Reload from disk on every paste: an edited/deleted template cannot
            // be silently inserted from a stale in-memory preview.
            _activeTemplateLayout = layout;
            PrepareTemplateGhostMeshes();
            if (!MapObjectTemplate.TryPlanPaste(layout, _map, at.x, at.z, _yaw,
                    out var things, out var rules))
            {
                _say = Loc.T("Шаблон не помещается на карте, повреждён или превышен лимит объектов/правил.");
                return;
            }
            foreach (var thing in things)
            {
                if (MapLoader.Prefab(thing.Prop) == null ||
                    (!string.IsNullOrEmpty(thing.MaterialId) &&
                     MapAssets.ObjectMaterial(thing.MaterialId) == null))
                {
                    _say = Loc.F("Для шаблона отсутствует модель или материал: {0}", thing.Prop);
                    return;
                }
            }
            Change(() =>
            {
                _map.Things.AddRange(things);
                _map.Triggers.AddRange(rules);
                _selectedThingIds.Clear();
                foreach (var thing in things) _selectedThingIds.Add(thing.Id);
                _selectionKind = Kind.Thing;
                _selectionIndex = _map.Things.Count - things.Count;
            });
            _say = Loc.F("Шаблон установлен: {0}. Ctrl+Z отменит всю группу.", layout.Name);
        }

        private void AppendPaint(Vector3 at) =>
            _map.TerrainPaints.Add(new MapData.TerrainPaint
            { X = at.x, Z = at.z, Radius = _paintRadius, MaterialId = _paintMaterial });

        /// <summary>Paint real terrain triangles using a bundled or imported Unity material.</summary>
        private void PaintTerrain(Vector3 at)
        {
            if (_map.TerrainPaints.Count >= MapTerrain.MaxPaints)
            {
                _say = Loc.T("Достигнут предел кистей покрытия (256).");
                return;
            }
            if (!MapAssets.ValidId(_paintMaterial) ||
                (_paintMaterial != MapTerrain.RestoreOriginalMaterial &&
                 MapAssets.TerrainMaterial(_paintMaterial) == null))
            {
                _say = Loc.T("Материал земли отсутствует. Импортируйте его в Unity.");
                return;
            }
            var ids = MapTerrain.PaintMaterialIds(_map);
            if (_paintMaterial != MapTerrain.RestoreOriginalMaterial &&
                !ids.Contains(_paintMaterial) && ids.Count >= MapTerrain.MaxTerrainMaterials)
            {
                _say = Loc.T("На карте может быть не более семи материалов земли.");
                return;
            }
            Record();
            AppendPaint(at);
            RedrawTerrainOnly(false);
        }

        private string VegetationProp(int index)
        {
            string[] kinds = _vegetationPreset switch
            {
                "Forest" => new[] { "Pine", "TallPineA", "TallPineB", "Bush", "GrassTuft",
                                    "Pine", "Stump", "Rock", "GrassTuft" },
                "DeadForest" => new[] { "DeadTreeA", "DeadTreeB", "DeadTreeC", "Stump",
                                        "Rock", "DeadTreeA", "GrassTuft" },
                "Meadow" => new[] { "GrassTuft", "GrassTuft", "Bush",
                                    "GrassTuft", "Rock", "Bush" },
                "Wasteland" => new[] { "Rock", "Rock", "Stump", "DeadTreeA",
                                       "Rock", "GrassTuft" },
                _ => null
            };
            return kinds == null ? _vegetationProp
                                 : kinds[(index * 7 + index / 3) % kinds.Length];
        }

        /// <summary>Deterministic terrain-aware vegetation cluster; one Undo = whole cluster.</summary>
        private void ScatterVegetation(Vector3 at)
        {
            if (MapLoader.Prefab(VegetationProp(0)) == null)
            {
                _say = Loc.T("Модель растительности не найдена.");
                return;
            }
            float half = _map.Size * 0.5f;
            int placed = 0;
            Record();
            for (int i = 0; i < _vegetationCount; i++)
            {
                float angle = i * 2.39996323f;
                float r = _vegetationRadius * Mathf.Sqrt((i + 0.5f) / _vegetationCount);
                float x = at.x + Mathf.Cos(angle) * r;
                float z = at.z + Mathf.Sin(angle) * r;
                if (Mathf.Abs(x) > half || Mathf.Abs(z) > half) continue;
                bool inCrypt = false;
                foreach (var crypt in _map.Crypts)
                {
                    float dx = x - crypt.X, dz = z - crypt.Z;
                    if (dx * dx + dz * dz < 144f) { inCrypt = true; break; }
                }
                if (inCrypt) continue;
                string kind = VegetationProp(i);
                if (MapLoader.Prefab(kind) == null) continue;
                _map.Things.Add(new MapData.Thing
                {
                    Id = NewId(), Prop = kind, X = x, Z = z,
                    Yaw = Mathf.Repeat(i * 137.5f, 360f),
                    Scale = 0.8f + (i % 7) * 0.07f
                });
                placed++;
            }
            if (placed == 0) { Undo(); return; }
            Redraw();
            _say = Loc.F("Добавлено растений: {0}", placed);
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
            foreach (var region in _map.Regions) { var k = region; Try(k.X, k.Z, () => _map.Regions.Remove(k)); }
            if (drop == null) return;
            Record();
            drop();
            _selectionIndex = -1;
            Redraw();
        }

        /// <summary>One true height edit, saved in JSON and shared with playable terrain.</summary>
        private void Sculpt(Vector3 at)
        {
            _map.EnsureLists();
            if (_map.TerrainStrokes.Count >= MapTerrain.MaxStrokes)
            {
                _say = Loc.T("Достигнут предел изменений рельефа (128).");
                return;
            }
            float half = _map.Size * 0.5f;
            if (Mathf.Abs(at.x) > half || Mathf.Abs(at.z) > half) return;
            _dragFlattenHeight = MapTerrain.HeightAt(_map, at.x, at.z);
            Record();
            AppendTerrain(at);
            RedrawTerrainOnly(true);
        }

        private void AppendTerrain(Vector3 at) =>
            _map.TerrainStrokes.Add(new MapData.TerrainStroke
            {
                X = at.x, Z = at.z,
                Radius = _terrainRadius,
                Delta = _terrainRaise ? _terrainDelta : -_terrainDelta,
                Mode = _terrainFlatten ? "Flatten" : "Add",
                TargetHeight = _terrainFlatten ? _dragFlattenHeight : 0f
            });

        /// <summary>One mouse drag is one Undo transaction, not dozens of history entries.</summary>
        private void ContinueBrush(Vector3 at)
        {
            float radius = _mode == Mode.Terrain ? _terrainRadius : _paintRadius;
            float spacing = Mathf.Max(4f, radius * 0.6f);
            float dx = at.x - _lastBrush.x, dz = at.z - _lastBrush.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance < spacing) return;
            int count = Mathf.Min(8, (int)(distance / spacing));
            bool touched = false;
            for (int i = 0; i < count; i++)
            {
                if (_mode == Mode.Terrain &&
                    _map.TerrainStrokes.Count >= MapTerrain.MaxStrokes) break;
                if (_mode == Mode.Paint &&
                    _map.TerrainPaints.Count >= MapTerrain.MaxPaints) break;
                if (!MapTerrain.Valid(_map)) break;
                float fraction = spacing / distance;
                float x = _lastBrush.x + (at.x - _lastBrush.x) * fraction;
                float z = _lastBrush.z + (at.z - _lastBrush.z) * fraction;
                var next = new Vector3(x, 0f, z);
                float half = _map.Size * 0.5f;
                if (Mathf.Abs(next.x) > half || Mathf.Abs(next.z) > half) break;
                if (_mode == Mode.Terrain) AppendTerrain(next);
                else AppendPaint(next);
                _lastBrush = next;
                touched = true;
                dx = at.x - _lastBrush.x;
                dz = at.z - _lastBrush.z;
                distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance < spacing) break;
            }
            if (touched) RedrawTerrainOnly(_mode == Mode.Terrain);
        }

        private void ClearTemplateGhostMeshes()
        {
            foreach (var ghost in _templateGhostMeshes)
                if (ghost != null) Destroy(ghost);
            _templateGhostMeshes.Clear();
            _templateAssetsAvailable = false;
        }

        // Build previews only when selecting/reloading a template, never on
        // each pointer frame. The helper copies just mesh renderers and meshes:
        // user prefab scripts, colliders and animation are not instantiated.
        private void PrepareTemplateGhostMeshes()
        {
            ClearTemplateGhostMeshes();
            if (_activeTemplateLayout == null) return;
            _templateAssetsAvailable = true;
            int remainingNodes = 256;
            foreach (var item in _activeTemplateLayout.Items)
            {
                var prefab = MapLoader.Prefab(item.Prop);
                var overrideMaterial = string.IsNullOrEmpty(item.MaterialId)
                    ? null : MapAssets.ObjectMaterial(item.MaterialId);
                if (prefab == null ||
                    (!string.IsNullOrEmpty(item.MaterialId) && overrideMaterial == null))
                {
                    _templateAssetsAvailable = false;
                    _templateGhostMeshes.Add(null);
                    continue;
                }
                var ghost = MapTemplateGhost.Create(prefab, overrideMaterial,
                    Mathf.Min(48, remainingNodes), out int nodesUsed);
                if (ghost != null) remainingNodes -= nodesUsed;
                // A complex or skinned mesh keeps its footprint marker even
                // when a safe render-only clone cannot be built.
                _templateGhostMeshes.Add(ghost);
            }
        }

        private void HideTemplatePreview()
        {
            foreach (var marker in _templatePreviewMarkers)
                if (marker != null) marker.SetActive(false);
            foreach (var ghost in _templateGhostMeshes)
                if (ghost != null) ghost.SetActive(false);
        }

        private void HideBrushCursor()
        {
            if (_brushCursor != null) _brushCursor.SetActive(false);
            HideTemplatePreview();
        }

        // Map-only ghost markers. No new authored objects, colliders, selection
        // or Undo entries are generated while the mouse moves.
        private void ShowTemplatePreview(Vector3 center)
        {
            if (_activeTemplateLayout == null || _map == null)
            {
                HideTemplatePreview();
                return;
            }
            bool withinBounds = MapObjectTemplate.TryProject(
                _activeTemplateLayout, _map.Size, center.x, center.z, _yaw,
                out var positions);
            if (positions == null)
            {
                HideTemplatePreview();
                return;
            }
            bool fits = withinBounds && _templateAssetsAvailable &&
                _map.Things.Count + _activeTemplateLayout.Items.Count <= 4096 &&
                _map.Triggers.Count + _activeTemplateLayout.Interactions.Count <= 128;
            while (_templatePreviewMarkers.Count < positions.Count)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Sinbinder template footprint preview only";
                Destroy(marker.GetComponent<Collider>());
                var material = new Material(Shader.Find("Sprites/Default"));
                marker.GetComponent<Renderer>().sharedMaterial = material;
                _templatePreviewMarkers.Add(marker);
                _templatePreviewMaterials.Add(material);
            }
            for (int i = 0; i < _templatePreviewMarkers.Count; i++)
            {
                var marker = _templatePreviewMarkers[i];
                bool visible = i < positions.Count;
                marker.SetActive(visible);
                if (!visible) continue;
                var point = positions[i];
                float radius = Mathf.Clamp(
                    _activeTemplateLayout.Items[i].Scale * 0.8f, 0.35f, 2.5f);
                marker.transform.position = new Vector3(point.x,
                    MapTerrain.HeightAt(_map, point.x, point.y) + 0.22f, point.y);
                marker.transform.localScale = new Vector3(
                    radius * 2f, 0.02f, radius * 2f);
                bool pointFits = Mathf.Abs(point.x) <= _map.Size * 0.5f &&
                                 Mathf.Abs(point.y) <= _map.Size * 0.5f;
                _templatePreviewMaterials[i].color = fits && pointFits
                    ? new Color(0.25f, 0.9f, 0.5f, 0.42f)
                    : new Color(0.95f, 0.25f, 0.25f, 0.55f);
                if (i >= _templateGhostMeshes.Count ||
                    _templateGhostMeshes[i] == null) continue;
                var ghost = _templateGhostMeshes[i];
                // Do not show a false 3D promise outside the map or when the
                // placement would fail: the red footprint remains visible.
                ghost.SetActive(fits && pointFits);
                if (!(fits && pointFits)) continue;
                var item = _activeTemplateLayout.Items[i];
                var source = MapLoader.Prefab(item.Prop);
                if (source == null) { ghost.SetActive(false); continue; }
                ghost.transform.SetPositionAndRotation(
                    new Vector3(point.x, MapTerrain.HeightAt(_map, point.x, point.y), point.y),
                    Quaternion.Euler(0f, item.Yaw + _yaw, 0f) * source.transform.rotation);
                ghost.transform.localScale = source.transform.localScale * item.Scale;
            }
        }

        private void ShowBrushCursor(Vector3 at)
        {
            if (_mode == Mode.Template) at = Snapped(at);
            if (_mode != Mode.Terrain && _mode != Mode.Paint &&
                _mode != Mode.Vegetation && _mode != Mode.Template)
            {
                HideBrushCursor();
                return;
            }
            if (_brushCursor == null)
            {
                _brushCursor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _brushCursor.name = "Sinbinder editor brush preview only";
                Destroy(_brushCursor.GetComponent<Collider>());
                _brushCursorMaterial = new Material(Shader.Find("Sprites/Default"));
                _brushCursor.GetComponent<Renderer>().sharedMaterial = _brushCursorMaterial;
            }
            _brushCursor.SetActive(true);
            float radius = _mode == Mode.Terrain ? _terrainRadius :
                           _mode == Mode.Paint ? _paintRadius :
                           _mode == Mode.Template ? 3f : _vegetationRadius;
            _brushCursor.transform.position = new Vector3(at.x,
                MapTerrain.HeightAt(_map, at.x, at.z) + 0.18f, at.z);
            _brushCursor.transform.localScale =
                new Vector3(radius * 2f, 0.02f, radius * 2f);
            _brushCursorMaterial.color = _mode == Mode.Template
                ? new Color(0.85f, 0.55f, 0.95f, 0.35f)
                : _mode == Mode.Paint
                    ? new Color(0.2f, 0.75f, 0.95f, 0.24f)
                : _mode == Mode.Vegetation
                    ? new Color(0.2f, 0.9f, 0.25f, 0.24f)
                    : _terrainFlatten
                        ? new Color(0.9f, 0.76f, 0.3f, 0.24f)
                        : _terrainRaise
                            ? new Color(0.4f, 0.85f, 0.25f, 0.24f)
                            : new Color(0.8f, 0.25f, 0.55f, 0.24f);
            if (_mode == Mode.Template) ShowTemplatePreview(at);
            else HideTemplatePreview();
        }

        // ---------------------------------------------------------- selection and history

        private static string NewId() => System.Guid.NewGuid().ToString("N");

        private void EnsureIds()
        {
            foreach (var item in _map.Things)
                if (item != null && string.IsNullOrEmpty(item.Id)) item.Id = NewId();
            foreach (var region in _map.Regions)
                if (region != null && string.IsNullOrEmpty(region.Id)) region.Id = NewId();
            foreach (var rule in _map.Triggers)
                if (rule != null && string.IsNullOrEmpty(rule.Id)) rule.Id = NewId();
        }

        private Vector3 Snapped(Vector3 p)
        {
            if (_snap < 0.5f) return p;
            p.x = Mathf.Round(p.x / _snap) * _snap;
            p.z = Mathf.Round(p.z / _snap) * _snap;
            return p;
        }

        private void Record()
        {
            _dirty = true;
            _confirmExit = false;
            _validationIssues.Clear();
            _undo.Add(JsonUtility.ToJson(_map));
            if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
            _redo.Clear();
        }

        private void Undo()
        {
            _draggingBrush = false;
            if (_undo.Count == 0) return;
            _redo.Add(JsonUtility.ToJson(_map));
            Restore(_undo[_undo.Count - 1]);
            _undo.RemoveAt(_undo.Count - 1);
            _say = Loc.T("Действие отменено");
        }

        private void Redo()
        {
            _draggingBrush = false;
            if (_redo.Count == 0) return;
            _undo.Add(JsonUtility.ToJson(_map));
            if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
            Restore(_redo[_redo.Count - 1]);
            _redo.RemoveAt(_redo.Count - 1);
            _say = Loc.T("Действие повторено");
        }

        private void Restore(string data)
        {
            _map = JsonUtility.FromJson<MapData>(data);
            _map.EnsureLists();
            _name = _map.Name;
            _selectionIndex = -1;
            _selectedThingIds.Clear();
            _validationIssues.Clear();
            if (_savedSnapshot != null)
                _dirty = JsonUtility.ToJson(_map) != _savedSnapshot;
            Redraw();
        }

        private void Change(System.Action action)
        {
            Record();
            action();
            Redraw();
        }

        /// <summary>Select only regular props in groups; crypts, regions and triggers
        /// retain their existing single-item inspector, and are never moved accidentally.</summary>
        private void SelectAllThings()
        {
            _selectedThingIds.Clear();
            foreach (var t in _map.Things)
                if (t != null && !string.IsNullOrEmpty(t.Id)) _selectedThingIds.Add(t.Id);
            _selectionKind = Kind.Thing;
            _selectionIndex = _map.Things.Count > 0 ? 0 : -1;
            Redraw();
        }

        // Selection is editor-only: no MapData writes and no Undo transactions.
        private void SetThingSelection(HashSet<string> next)
        {
            if (next == null || _selectedThingIds.SetEquals(next)) return;
            _selectedThingIds.Clear();
            foreach (var id in next) _selectedThingIds.Add(id);
            _selectionKind = Kind.Thing;
            _selectionIndex = _map.Things.FindIndex(t =>
                t != null && _selectedThingIds.Contains(t.Id));
            Redraw();
        }

        private void SelectMatchingThings(MapObjectSelection.Property property)
        {
            if (_selectedThingIds.Count == 0)
            {
                _say = Loc.T("Сначала выберите модель или группу моделей.");
                return;
            }
            var next = MapObjectSelection.Matching(_map.Things, _selectedThingIds, property);
            if (next.Count == 0)
            {
                _say = Loc.T("Не найдено подходящих объектов.");
                return;
            }
            SetThingSelection(next);
        }

        private void InvertThingSelection() =>
            SetThingSelection(MapObjectSelection.Inverted(_map.Things, _selectedThingIds));

        private void Select(Vector3 at, bool additive = false)
        {
            float best = 6f;
            int selected = -1;
            Kind kind = Kind.Thing;
            void Candidate(Kind candidate, int index, float x, float z)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(at.x, at.z));
                if (d >= best) return;
                best = d;
                kind = candidate;
                selected = index;
            }
            for (int i = 0; i < _map.Things.Count; i++)
                Candidate(Kind.Thing, i, _map.Things[i].X, _map.Things[i].Z);
            for (int i = 0; i < _map.Crypts.Count; i++)
                Candidate(Kind.Crypt, i, _map.Crypts[i].X, _map.Crypts[i].Z);
            for (int i = 0; i < _map.Locations.Count; i++)
                Candidate(Kind.Location, i, _map.Locations[i].X, _map.Locations[i].Z);
            for (int i = 0; i < _map.Trails.Count; i++)
                Candidate(Kind.Trail, i, _map.Trails[i].X, _map.Trails[i].Z);
            for (int i = 0; i < _map.Events.Count; i++)
                Candidate(Kind.Event, i, _map.Events[i].X, _map.Events[i].Z);
            for (int i = 0; i < _map.Regions.Count; i++)
                Candidate(Kind.Region, i, _map.Regions[i].X, _map.Regions[i].Z);
            if (selected < 0)
            {
                if (!additive) { _selectedThingIds.Clear(); _selectionIndex = -1; }
            }
            else if (kind == Kind.Thing)
            {
                var id = _map.Things[selected].Id;
                if (!additive) _selectedThingIds.Clear();
                if (!string.IsNullOrEmpty(id))
                {
                    if (additive && _selectedThingIds.Contains(id)) _selectedThingIds.Remove(id);
                    else _selectedThingIds.Add(id);
                }
                _selectionKind = Kind.Thing;
                _selectionIndex = _selectedThingIds.Contains(id) ? selected
                    : _map.Things.FindIndex(t => t != null && _selectedThingIds.Contains(t.Id));
            }
            else if (!additive)
            {
                _selectedThingIds.Clear();
                _selectionKind = kind;
                _selectionIndex = selected;
            }
            Redraw();
        }

        /// <summary>
        /// World-space rectangle selects only props, not crypts or gameplay regions.
        /// Keeps existing selection when Shift is held. This is an editor operation,
        /// never serialized into the map.
        /// </summary>
        private void SelectRectangle(Vector3 first, Vector3 last, bool additive = false)
        {
            float minX = Mathf.Min(first.x, last.x), maxX = Mathf.Max(first.x, last.x);
            float minZ = Mathf.Min(first.z, last.z), maxZ = Mathf.Max(first.z, last.z);
            if (!additive) _selectedThingIds.Clear();
            foreach (var t in _map.Things)
                if (t != null && !string.IsNullOrEmpty(t.Id) &&
                    t.X >= minX && t.X <= maxX && t.Z >= minZ && t.Z <= maxZ)
                    _selectedThingIds.Add(t.Id);
            _selectionKind = Kind.Thing;
            _selectionIndex = _map.Things.FindIndex(t => t != null &&
                _selectedThingIds.Contains(t.Id));
            Redraw();
        }

        private List<MapData.Thing> GroupThings()
        {
            var group = new List<MapData.Thing>();
            foreach (var t in _map.Things)
                if (t != null && _selectedThingIds.Contains(t.Id)) group.Add(t);
            return group;
        }

        private void MoveSelectedThings(float dx, float dz)
        {
            var group = GroupThings();
            if (group.Count == 0 || (dx == 0f && dz == 0f)) return;
            float half = _map.Size * 0.5f;
            foreach (var t in group)
                if (Mathf.Abs(t.X + dx) > half || Mathf.Abs(t.Z + dz) > half)
                {
                    _say = Loc.T("Группа выходит за границы карты. Изменения отменены.");
                    return;
                }
            Change(() => { foreach (var t in group) { t.X += dx; t.Z += dz; } });
        }

        // Keep arrangement separate from gameplay logic and number-field editing.
        // A fully validated plan is committed in one transaction; no partial moves.
        private void ArrangeSelectedThings(MapObjectArrange.Axis axis, MapObjectArrange.Operation operation)
        {
            var selected = GroupThings();
            if (!MapObjectArrange.TryPlan(selected, _map.Size, axis, operation, out var planned))
            {
                _say = Loc.T("Расстановка не изменена: выберите минимум два объекта, для равных интервалов — три.");
                return;
            }
            Change(() =>
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    selected[i].X = planned[i].x;
                    selected[i].Z = planned[i].y;
                }
            });
        }

        private void SnapSelectedThingsToGrid()
        {
            var selected = GroupThings();
            if (!MapObjectArrange.TrySnap(selected, _map.Size, _snap, out var planned))
            {
                _say = Loc.T("Группа уже на сетке, сетка выключена или перенос невозможен.");
                return;
            }
            Change(() =>
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    selected[i].X = planned[i].x;
                    selected[i].Z = planned[i].y;
                }
            });
        }

        private void RotateSelectedThings(float degrees)
        {
            var group = GroupThings();
            if (group.Count < 2 || degrees == 0f) return;
            float cx = 0f, cz = 0f;
            foreach (var t in group) { cx += t.X; cz += t.Z; }
            cx /= group.Count; cz /= group.Count;
            float angle = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(angle), v = Mathf.Sin(angle);
            var positions = new List<Vector2>();
            float half = _map.Size * 0.5f;
            foreach (var t in group)
            {
                float dx = t.X - cx, dz = t.Z - cz;
                // Same clockwise convention as a positive Unity Y yaw.
                float nx = cx + dx * c + dz * v;
                float nz = cz - dx * v + dz * c;
                if (Mathf.Abs(nx) > half || Mathf.Abs(nz) > half)
                {
                    _say = Loc.T("Группа после поворота выйдет за границы карты.");
                    return;
                }
                positions.Add(new Vector2(nx, nz));
            }
            Change(() =>
            {
                for (int i = 0; i < group.Count; i++)
                {
                    group[i].X = positions[i].x;
                    group[i].Z = positions[i].y;
                    group[i].Yaw = Mathf.Repeat(group[i].Yaw + degrees, 360f);
                }
            });
        }

        private void ScaleSelectedThings(float factor)
        {
            var group = GroupThings();
            if (group.Count < 2 || factor <= 0f) return;
            float cx = 0f, cz = 0f;
            foreach (var t in group) { cx += t.X; cz += t.Z; }
            cx /= group.Count; cz /= group.Count;
            float half = _map.Size * 0.5f;
            foreach (var t in group)
            {
                float x = cx + (t.X - cx) * factor;
                float z = cz + (t.Z - cz) * factor;
                float scale = t.Scale * factor;
                if (Mathf.Abs(x) > half || Mathf.Abs(z) > half ||
                    scale < 0.1f || scale > 10f)
                {
                    _say = Loc.T("Невозможно изменить масштаб: объект выйдет за границы или допустимый размер.");
                    return;
                }
            }
            Change(() =>
            {
                foreach (var t in group)
                {
                    t.X = cx + (t.X - cx) * factor;
                    t.Z = cz + (t.Z - cz) * factor;
                    t.Scale *= factor;
                }
            });
        }

        private void ApplyGroupMaterial(string materialId)
        {
            var group = GroupThings();
            if (group.Count < 2) return;
            if (!string.IsNullOrEmpty(materialId) && MapAssets.ObjectMaterial(materialId) == null)
            {
                _say = Loc.T("Материал не найден в библиотеке.");
                return;
            }
            Change(() => { foreach (var t in group) t.MaterialId = materialId; });
        }

        private bool CanDuplicateThings(List<MapData.Thing> group, float offset)
        {
            float half = _map.Size * 0.5f;
            var ids = new HashSet<string>();
            foreach (var t in group)
            {
                if (Mathf.Abs(t.X + offset) > half || Mathf.Abs(t.Z + offset) > half)
                {
                    _say = Loc.T("Группа не помещается на карте.");
                    return false;
                }
                ids.Add(t.Id);
            }
            int linked = 0;
            foreach (var rule in _map.Triggers)
                if (rule != null && rule.Event == "OnInteractObject" &&
                    ids.Contains(rule.TargetId)) linked++;
            if (_map.Triggers.Count + linked > 128)
            {
                _say = Loc.T("Копии превысят лимит правил взаимодействия.");
                return false;
            }
            return true;
        }

        private MapData.Thing CopyThingWithRules(MapData.Thing t, float offset)
        {
            var copy = new MapData.Thing
            {
                Id = NewId(), Prop = t.Prop, MaterialId = t.MaterialId,
                X = t.X + offset, Z = t.Z + offset, Yaw = t.Yaw, Scale = t.Scale
            };
            _map.Things.Add(copy);
            int before = _map.Triggers.Count;
            for (int i = 0; i < before; i++)
            {
                var rule = _map.Triggers[i];
                if (rule == null || rule.Event != "OnInteractObject" ||
                    rule.TargetId != t.Id) continue;
                _map.Triggers.Add(new MapData.Trigger
                {
                    Id = NewId(), Name = rule.Name, Enabled = rule.Enabled,
                    Once = rule.Once, Event = rule.Event, RegionId = rule.RegionId,
                    TargetId = copy.Id, Condition = rule.Condition, Flag = rule.Flag,
                    Action = rule.Action, Message = rule.Message
                });
            }
            return copy;
        }

        private void DuplicateSelectedThings()
        {
            var group = GroupThings();
            if (group.Count == 0) return;
            float offset = Mathf.Max(2f, _snap);
            if (!CanDuplicateThings(group, offset)) return;
            Change(() =>
            {
                var copies = new List<MapData.Thing>();
                foreach (var t in group) copies.Add(CopyThingWithRules(t, offset));
                _selectedThingIds.Clear();
                foreach (var item in copies) _selectedThingIds.Add(item.Id);
                _selectionKind = Kind.Thing;
                _selectionIndex = _map.Things.IndexOf(copies[0]);
            });
            _say = Loc.F("Скопировано объектов: {0}. Их взаимодействия также скопированы.", group.Count);
        }

        private void DeleteSelectedThings()
        {
            var group = GroupThings();
            if (group.Count == 0) return;
            var ids = new HashSet<string>();
            foreach (var t in group) ids.Add(t.Id);
            int removedRules = 0;
            Change(() =>
            {
                _map.Things.RemoveAll(t => t != null && ids.Contains(t.Id));
                // Never leave dangling OnInteractObject references.
                removedRules = _map.Triggers.RemoveAll(t =>
                    t != null && t.Event == "OnInteractObject" && ids.Contains(t.TargetId));
                _selectedThingIds.Clear();
                _selectionIndex = -1;
            });
            _say = Loc.F("Удалено объектов: {0}, правил: {1}. Ctrl+Z вернёт всё.", group.Count, removedRules);
        }

        private void DrawGroupInspector()
        {
            GUILayout.Label(Loc.F("Группа объектов: {0}", GroupThings().Count));
            GUILayout.Label(Loc.T("Shift+ЛКМ: добавить/исключить; протянуть ЛКМ: рамка; Ctrl+A: всё."));
            GUILayout.Label(Loc.F("Шаг перемещения: {0} м", Mathf.RoundToInt(_groupStep)));
            _groupStep = Mathf.Round(GUILayout.HorizontalSlider(_groupStep, 1f, 20f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("←")) MoveSelectedThings(-_groupStep, 0f);
            if (GUILayout.Button("→")) MoveSelectedThings(_groupStep, 0f);
            if (GUILayout.Button("↑")) MoveSelectedThings(0f, _groupStep);
            if (GUILayout.Button("↓")) MoveSelectedThings(0f, -_groupStep);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("↶ 15°")) RotateSelectedThings(-15f);
            if (GUILayout.Button("↷ 15°")) RotateSelectedThings(15f);
            if (GUILayout.Button("↶ 90°")) RotateSelectedThings(-90f);
            if (GUILayout.Button("↷ 90°")) RotateSelectedThings(90f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.T("Уменьшить ×0.9"))) ScaleSelectedThings(0.9f);
            if (GUILayout.Button(Loc.T("Увеличить ×1.1"))) ScaleSelectedThings(1.1f);
            GUILayout.EndHorizontal();
            GUILayout.Label(Loc.T("Выравнивание группы (X / Z):"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.T("X: мин")))
                ArrangeSelectedThings(MapObjectArrange.Axis.X, MapObjectArrange.Operation.Minimum);
            if (GUILayout.Button(Loc.T("X: центр")))
                ArrangeSelectedThings(MapObjectArrange.Axis.X, MapObjectArrange.Operation.Center);
            if (GUILayout.Button(Loc.T("X: макс")))
                ArrangeSelectedThings(MapObjectArrange.Axis.X, MapObjectArrange.Operation.Maximum);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.T("Z: мин")))
                ArrangeSelectedThings(MapObjectArrange.Axis.Z, MapObjectArrange.Operation.Minimum);
            if (GUILayout.Button(Loc.T("Z: центр")))
                ArrangeSelectedThings(MapObjectArrange.Axis.Z, MapObjectArrange.Operation.Center);
            if (GUILayout.Button(Loc.T("Z: макс")))
                ArrangeSelectedThings(MapObjectArrange.Axis.Z, MapObjectArrange.Operation.Maximum);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(Loc.T("Равные интервалы по X")))
                ArrangeSelectedThings(MapObjectArrange.Axis.X, MapObjectArrange.Operation.Distribute);
            if (GUILayout.Button(Loc.T("Равные интервалы по Z")))
                ArrangeSelectedThings(MapObjectArrange.Axis.Z, MapObjectArrange.Operation.Distribute);
            GUILayout.Label(Loc.T("Выравнивание сохраняет выделение, порядок и ID."));
            if (GUILayout.Button(Loc.T("Привязать всю группу к сетке")))
                SnapSelectedThingsToGrid();
            GUILayout.Label(Loc.T("Материал всей группы:"));
            if (GUILayout.Button(Loc.T("Вернуть исходные материалы")))
                ApplyGroupMaterial("");
            foreach (var material in _objectMaterials)
                if (material != null && GUILayout.Button(material.name))
                    ApplyGroupMaterial(material.name);
            if (GUILayout.Button(Loc.T("Копировать группу"))) DuplicateSelectedThings();
            if (GUILayout.Button(Loc.T("Удалить группу"))) DeleteSelectedThings();
            if (GUILayout.Button(Loc.T("Снять выделение")))
            {
                _selectedThingIds.Clear();
                _selectionIndex = -1;
                Redraw();
            }
        }

        private bool SelectedPosition(out Vector3 position)
        {
            position = Vector3.zero;
            int i = _selectionIndex;
            if (i < 0) return false;
            float x, z;
            switch (_selectionKind)
            {
                case Kind.Thing when i < _map.Things.Count: x = _map.Things[i].X; z = _map.Things[i].Z; break;
                case Kind.Crypt when i < _map.Crypts.Count: x = _map.Crypts[i].X; z = _map.Crypts[i].Z; break;
                case Kind.Location when i < _map.Locations.Count: x = _map.Locations[i].X; z = _map.Locations[i].Z; break;
                case Kind.Trail when i < _map.Trails.Count: x = _map.Trails[i].X; z = _map.Trails[i].Z; break;
                case Kind.Event when i < _map.Events.Count: x = _map.Events[i].X; z = _map.Events[i].Z; break;
                case Kind.Region when i < _map.Regions.Count: x = _map.Regions[i].X; z = _map.Regions[i].Z; break;
                default: return false;
            }
            // Disc() samples terrain height itself. Do not add it twice.
            position = new Vector3(x, 0.12f, z);
            return true;
        }

        private void HighlightSelection()
        {
            if (_selectedThingIds.Count > 1)
            {
                foreach (var t in GroupThings())
                    Disc(new Vector3(t.X, 0.12f, t.Z), 1.4f,
                        new Color(1f, 0.78f, 0.14f, 0.55f));
            }
            else if (SelectedPosition(out var p))
                Disc(p, 1.4f, new Color(1f, 0.78f, 0.14f, 0.55f));
        }

        private void DeleteSelected()
        {
            if (_selectedThingIds.Count > 1) { DeleteSelectedThings(); return; }
            int i = _selectionIndex;
            if (i < 0) return;
            Change(() =>
            {
                switch (_selectionKind)
                {
                    case Kind.Thing when i < _map.Things.Count:
                        var removedId = _map.Things[i].Id;
                        _map.Things.RemoveAt(i);
                        _map.Triggers.RemoveAll(rule => rule != null &&
                            rule.Event == "OnInteractObject" && rule.TargetId == removedId);
                        _selectedThingIds.Clear();
                        break;
                    case Kind.Crypt when i < _map.Crypts.Count: _map.Crypts.RemoveAt(i); break;
                    case Kind.Location when i < _map.Locations.Count: _map.Locations.RemoveAt(i); break;
                    case Kind.Trail when i < _map.Trails.Count: _map.Trails.RemoveAt(i); break;
                    case Kind.Event when i < _map.Events.Count: _map.Events.RemoveAt(i); break;
                    case Kind.Region when i < _map.Regions.Count: _map.Regions.RemoveAt(i); break;
                    case Kind.Trigger when i < _map.Triggers.Count: _map.Triggers.RemoveAt(i); break;
                }
                _selectionIndex = -1;
            });
        }

        private void DuplicateSelected()
        {
            if (_selectedThingIds.Count > 1) { DuplicateSelectedThings(); return; }
            int i = _selectionIndex;
            if (_selectionKind == Kind.Thing && i >= 0 && i < _map.Things.Count)
            {
                _selectedThingIds.Clear();
                _selectedThingIds.Add(_map.Things[i].Id);
                DuplicateSelectedThings();
                return;
            }
            if (i < 0) return;
            Change(() =>
            {
                switch (_selectionKind)
                {
                    case Kind.Thing when i < _map.Things.Count:
                        var t = _map.Things[i];
                        _map.Things.Add(new MapData.Thing { Id = NewId(), Prop = t.Prop,
                            X = t.X + Mathf.Max(2f, _snap), Z = t.Z + Mathf.Max(2f, _snap),
                            Yaw = t.Yaw, Scale = t.Scale, MaterialId = t.MaterialId });
                        _selectionIndex = _map.Things.Count - 1;
                        break;
                    case Kind.Region when i < _map.Regions.Count:
                        var region = _map.Regions[i];
                        _map.Regions.Add(new MapData.Region { Id = NewId(),
                            Name = region.Name + " (копия)", X = region.X + 5f,
                            Z = region.Z + 5f, Radius = region.Radius });
                        _selectionIndex = _map.Regions.Count - 1;
                        break;
                    case Kind.Trigger when i < _map.Triggers.Count:
                        var src = _map.Triggers[i];
                        _map.Triggers.Add(new MapData.Trigger { Id = NewId(),
                            Name = src.Name + " (копия)", Event = src.Event,
                            RegionId = src.RegionId, TargetId = src.TargetId,
                            Condition = src.Condition, Flag = src.Flag,
                            Action = src.Action, Message = src.Message,
                            Enabled = src.Enabled, Once = src.Once });
                        _selectionIndex = _map.Triggers.Count - 1;
                        break;
                }
            });
        }

        // ---------------------------------------------------------- вид

        private void ClearPreview()
        {
            foreach (var go in _shown) if (go != null) Destroy(go);
            _shown.Clear();
            _heightFollowers.Clear();
            _missingPreviewModels.Clear();
            _loadedPreviewModels = 0;
            foreach (var material in _previewMaterials) if (material != null) Destroy(material);
            _previewMaterials.Clear();
        }

        void OnDestroy()
        {
            ClearPreview();
            foreach (var marker in _templatePreviewMarkers)
                if (marker != null) Destroy(marker);
            foreach (var material in _templatePreviewMaterials)
                if (material != null) Destroy(material);
            _templatePreviewMarkers.Clear();
            _templatePreviewMaterials.Clear();
            ClearTemplateGhostMeshes();
            if (_brushCursor != null) Destroy(_brushCursor);
            if (_brushCursorMaterial != null) Destroy(_brushCursorMaterial);
            if (_terrainMesh != null) Destroy(_terrainMesh);
        }

        private void RedrawGround()
        {
            var ground = GameObject.Find("Поле");
            if (ground != null)
                MapTerrain.ApplyGround(ground, _map, ref _terrainMesh, ref _originalGroundMesh);
        }

        /// <summary>
        /// Terrain brush feedback should not reinstantiate every forest prop, crypt,
        /// region and marker for every mouse sample. Paint changes submeshes only;
        /// sculpting also lifts preview transforms onto the updated collider.
        /// </summary>
        private void RedrawTerrainOnly(bool geometryChanged)
        {
            if (_shown.Count == 0) { Redraw(); return; }
            RedrawGround();
            if (!geometryChanged) return;
            foreach (var follower in _heightFollowers)
            {
                if (follower.Object == null) continue;
                var position = follower.Object.transform.position;
                position.y = MapTerrain.HeightAt(_map, follower.X, follower.Z) +
                             follower.Offset;
                follower.Object.transform.position = position;
            }
        }

        private void Redraw()
        {
            ClearPreview();
            RedrawGround();

            foreach (var t in _map.Things) Model(t.Prop, new Vector3(t.X, MapTerrain.HeightAt(_map, t.X, t.Z), t.Z), t.Yaw, t.Scale, t.MaterialId);
            // Склеп — тот же, что встанет на поле: пол, стены, дверь, трон (MapLoader).
            foreach (var c in _map.Crypts) Model("Crypt", new Vector3(c.X, MapTerrain.HeightAt(_map, c.X, c.Z), c.Z), c.Yaw, 1f);
            foreach (var l in _map.Locations) Disc(new Vector3(l.X, 0.05f, l.Z), l.Radius, LocationColour(l.Kind));
            foreach (var s in _map.Trails) Disc(new Vector3(s.X, 0.06f, s.Z), 3f, new Color(0.8f, 0.15f, 0.12f));
            foreach (var s in _map.Events) Disc(new Vector3(s.X, 0.07f, s.Z), 2.5f, new Color(0.95f, 0.8f, 0.2f));
            foreach (var region in _map.Regions)
                Disc(new Vector3(region.X, 0.045f, region.Z), region.Radius, new Color(0.25f, 0.4f, 0.75f, 0.23f));
            HighlightSelection();
        }

        private static Color LocationColour(string kind) => kind switch
        {
            "forest" => new Color(0.15f, 0.35f, 0.18f),
            "field" => new Color(0.55f, 0.50f, 0.22f),
            _ => new Color(0.45f, 0.30f, 0.18f),
        };

        private void Model(string prop, Vector3 at, float yaw, float scale, string materialId = "")
        {
            var prefab = MapLoader.Prefab(prop);
            if (prefab == null)
            {
                _missingPreviewModels.Add(prop ?? "(empty)");
                MissingModelMarker(at, prop);
                return;
            }
            var go = Instantiate(prefab);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
            go.transform.localScale = prefab.transform.localScale * scale;
            MapAssets.ApplyObjectMaterial(go, materialId);
            // A prefab can exist while its renderer or mesh is missing. That is
            // still a broken visual asset, not a successfully previewed model.
            bool hasRenderer = false;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                if (renderer != null && renderer.enabled)
                {
                    hasRenderer = true;
                    break;
                }
            if (!hasRenderer)
            {
                _missingPreviewModels.Add(prop ?? "(empty)");
                Destroy(go);
                MissingModelMarker(at, prop);
                return;
            }
            _loadedPreviewModels++;
            _shown.Add(go);
            _heightFollowers.Add(new HeightFollower
            { Object = go, X = at.x, Z = at.z, Offset = 0f });
        }

        /// <summary>Never make an absent prop silently indistinguishable from empty terrain.</summary>
        private void MissingModelMarker(Vector3 at, string prop)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Отсутствующая модель карты: " + prop;
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = at + new Vector3(0f, 1.5f, 0f);
            marker.transform.localScale = new Vector3(2f, 3f, 2f);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var material = new Material(shader) { color = new Color(1f, 0.15f, 0.65f) };
                marker.GetComponent<Renderer>().sharedMaterial = material;
                _previewMaterials.Add(material);
            }
            _shown.Add(marker);
            _heightFollowers.Add(new HeightFollower
            { Object = marker, X = at.x, Z = at.z, Offset = 1.5f });
        }

        /// <summary>Плоский круг на земле — граница подлокации, тропа, событие.</summary>
        private void Disc(Vector3 at, float radius, Color colour)
        {
            float offset = at.y;
            at.y += MapTerrain.HeightAt(_map, at.x, at.z);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at;
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            var r = go.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Sprites/Default")) { color = colour };
            r.sharedMaterial = material;
            _previewMaterials.Add(material);
            _shown.Add(go);
            _heightFollowers.Add(new HeightFollower
            { Object = go, X = at.x, Z = at.z, Offset = offset });
        }

        // ---------------------------------------------------------- окно

        private bool _logicTab;
        private bool _confirmClear;

        void OnGUI()
        {
            // Warcraft-style editor layout: top commands, left catalog,
            // central live map, right properties/trigger inspector.
            GUILayout.BeginArea(new Rect(10f, 7f, Mathf.Max(350f, Screen.width - 20f),
                                         ToolbarHeight - 8f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("SINBINDER • РЕДАКТОР КАРТ"), GUILayout.Width(245f));
            if (GUILayout.Button(Loc.T("Разместить"), GUILayout.Width(92f))) _mode = Mode.Place;
            if (GUILayout.Button(Loc.T("Выделить"), GUILayout.Width(92f))) _mode = Mode.Select;
            if (GUILayout.Button(Loc.T("Стереть"), GUILayout.Width(92f))) _mode = Mode.Erase;
            if (GUILayout.Button(Loc.T("Рельеф"), GUILayout.Width(92f))) _mode = Mode.Terrain;
            if (GUILayout.Button(Loc.T("Материал"), GUILayout.Width(92f))) _mode = Mode.Paint;
            if (GUILayout.Button(Loc.T("Растения"), GUILayout.Width(92f))) _mode = Mode.Vegetation;
            if (GUILayout.Button("↶", GUILayout.Width(36f))) Undo();
            if (GUILayout.Button("↷", GUILayout.Width(36f))) Redo();
            if (GUILayout.Button(Loc.T("Записать"), GUILayout.Width(95f))) Save();
            if (GUILayout.Button(Loc.T("Сохранить копию"), GUILayout.Width(144f))) SaveCopy();
            GUILayout.Label(Loc.F("Режим: {0}", _mode), GUILayout.Width(160f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(10f, ToolbarHeight + 10f, PanelWidth,
                                        Mathf.Max(120f, Screen.height - ToolbarHeight - 20f)),
                                        GUI.skin.box);
            GUILayout.Label(Loc.T("Карта"));
            if (GUILayout.Button(Loc.T("Центрировать камеру")))
            {
                CenterCamera();
                _say = Loc.T("Камера направлена в центр карты.");
            }
            GUILayout.Label(Loc.F("Объектов: {0}; моделей в предпросмотре: {1}",
                _map.Things.Count, _loadedPreviewModels));
            if (_missingPreviewModels.Count > 0)
            {
                GUILayout.Label(Loc.F("Не загружено видов моделей: {0}", _missingPreviewModels.Count));
                foreach (var missing in _missingPreviewModels)
                {
                    GUILayout.Label(Loc.F("Отсутствует: {0}", missing));
                    break;
                }
            }
            GUILayout.Label(string.IsNullOrEmpty(_path)
                ? Loc.T("Источник: новая карта")
                : Loc.F("Файл: {0}", Path.GetFileName(_path)));
            if (GUILayout.Button(Loc.T("Копировать путь к карте")))
            {
                GUIUtility.systemCopyBuffer = string.IsNullOrEmpty(_path)
                    ? MapData.Folder : _path;
                _say = Loc.T("Путь к карте скопирован.");
            }
            if (_mode == Mode.Select)
            {
                if (GUILayout.Button(Loc.T("Выделить все объекты (Ctrl+A)")))
                    SelectAllThings();
                if (GUILayout.Button(Loc.T("К выделению (Ctrl+F)"))) FocusSelection();
                if (GUILayout.Button(Loc.T("Выделить такие же модели")))
                    SelectMatchingThings(MapObjectSelection.Property.Model);
                if (GUILayout.Button(Loc.T("Выделить с тем же материалом")))
                    SelectMatchingThings(MapObjectSelection.Property.Material);
                if (GUILayout.Button(Loc.T("Инвертировать выделение")))
                    InvertThingSelection();
                GUILayout.Label(Loc.T("Имя нового шаблона (латиница, 1–48 символов):"));
                _templateDraftName = GUILayout.TextField(_templateDraftName ?? "");
                if (GUILayout.Button(Loc.T("Сохранить выделение как шаблон")))
                    SaveSelectedTemplate();
                GUILayout.Label(Loc.F("Выделено объектов: {0}", _selectedThingIds.Count));
                GUILayout.Label(Loc.T("ЛКМ по выделенному + протянуть: переместить группу."));
            }
            GUILayout.Label(Loc.T("Имя карты"));
            string name = GUILayout.TextField(_name ?? "");
            if (name != _name) { Record(); _name = name; _map.Name = name; }
            GUILayout.Label(Loc.F("Поле: {0} м", Mathf.RoundToInt(_map.Size)));
            float size = GUILayout.HorizontalSlider(_map.Size, 150f, 800f);
            float rounded = Mathf.Round(size / 10f) * 10f;
            if (Mathf.Abs(rounded - _map.Size) >= 1f)
                Change(() => _map.Size = rounded);
            GUILayout.Label(Loc.F("Склепов: {0} · мест: {1} · событий: {2}",
                _map.Crypts.Count, _map.Locations.Count, _map.Events.Count));
            GUILayout.Label(Loc.F("Поворот Q/E: {0}°", Mathf.RoundToInt(Mathf.Repeat(_yaw, 360f))));

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("Сетка:"), GUILayout.Width(50f));
            foreach (var step in new[] { 0f, 1f, 2f, 5f, 10f })
                if (GUILayout.Toggle(_snap == step, step == 0f ? "Off" : step.ToString("0"), "Button"))
                    _snap = step;
            GUILayout.EndHorizontal();

            // A sculpt click updates the real mesh collider in both editor and game.
            GUILayout.Label(Loc.F("Рельеф: {0}/{1}", _map.TerrainStrokes.Count, MapTerrain.MaxStrokes));
            if (_mode == Mode.Terrain)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_terrainRaise && !_terrainFlatten, Loc.T("Поднять"), "Button"))
                    { _terrainRaise = true; _terrainFlatten = false; }
                if (GUILayout.Toggle(!_terrainRaise && !_terrainFlatten, Loc.T("Опустить"), "Button"))
                    { _terrainRaise = false; _terrainFlatten = false; }
                GUILayout.EndHorizontal();
                if (GUILayout.Toggle(_terrainFlatten, Loc.T("Выровнять по точке"), "Button"))
                    _terrainFlatten = true;
                GUILayout.Label(Loc.F("Радиус: {0} м", Mathf.RoundToInt(_terrainRadius)));
                _terrainRadius = Mathf.Round(GUILayout.HorizontalSlider(_terrainRadius, 8f, 60f));
                GUILayout.Label(Loc.F("Шаг высоты: {0} м", Mathf.RoundToInt(_terrainDelta)));
                _terrainDelta = Mathf.Round(GUILayout.HorizontalSlider(_terrainDelta, 1f, 8f));
                GUILayout.Label(Loc.T("ЛКМ удерживать: рисовать; Ctrl+Z: отменить весь мазок."));
            }
            if (_mode == Mode.Paint)
            {
                GUILayout.Label(Loc.T("Материалы ландшафта"));
                if (GUILayout.Toggle(_paintMaterial == MapTerrain.RestoreOriginalMaterial,
                    Loc.T("Ластик: вернуть исходную землю"), "Button"))
                    _paintMaterial = MapTerrain.RestoreOriginalMaterial;
                foreach (var mat in _terrainMaterials)
                    if (mat != null && GUILayout.Toggle(_paintMaterial == mat.name, mat.name, "Button"))
                        _paintMaterial = mat.name;
                GUILayout.Label(Loc.F("Радиус кисти: {0} м", Mathf.RoundToInt(_paintRadius)));
                _paintRadius = Mathf.Round(GUILayout.HorizontalSlider(_paintRadius, 4f, 60f));
                GUILayout.Label(Loc.T("Удерживайте ЛКМ для покраски. Ctrl+Z отменяет мазок."));
                GUILayout.Label(Loc.T("Новые материалы: Sinbinder → Редактор карт → Импорт."));
            }
            if (_mode == Mode.Vegetation)
            {
                GUILayout.Label(Loc.T("Генератор растительности"));
                foreach (var option in new[]
                {
                    ("Single", Loc.T("Один вид")),
                    ("Forest", Loc.T("Смешанный лес")),
                    ("DeadForest", Loc.T("Мёртвый лес")),
                    ("Meadow", Loc.T("Луг")),
                    ("Wasteland", Loc.T("Каменистая пустошь"))
                })
                    if (GUILayout.Toggle(_vegetationPreset == option.Item1,
                        option.Item2, "Button")) _vegetationPreset = option.Item1;
                if (_vegetationPreset == "Single")
                {
                foreach (string kind in new[] { "GrassTuft", "Bush", "Pine", "TallPineA",
                                                 "TallPineB", "TallPineC", "DeadTreeA",
                                                 "DeadTreeB", "Stump", "Rock" })
                    if (GUILayout.Toggle(_vegetationProp == kind, kind, "Button"))
                        _vegetationProp = kind;
                foreach (var prefab in _customPrefabs)
                    if (prefab != null && GUILayout.Toggle(_vegetationProp == "user:" + prefab.name,
                                                           prefab.name, "Button"))
                        _vegetationProp = "user:" + prefab.name;
                }
                GUILayout.Label(Loc.F("Количество: {0}", _vegetationCount));
                _vegetationCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(_vegetationCount, 1, 40));
                GUILayout.Label(Loc.F("Радиус: {0} м", Mathf.RoundToInt(_vegetationRadius)));
                _vegetationRadius = Mathf.Round(GUILayout.HorizontalSlider(_vegetationRadius, 4, 65));
            }

            if (GUILayout.Button(Loc.T("Обновить библиотеку моделей и материалов")))
            {
                RefreshAssets();
                RefreshTemplates();
                _say = Loc.T("Библиотека ассетов обновлена.");
            }

            _logicTab = GUILayout.Toolbar(_logicTab ? 1 : 0,
                new[] { Loc.T("Объекты"), Loc.T("Логика") }) == 1;
            if (!_logicTab)
            {
                GUILayout.Label(Loc.T("Поиск в палитре"));
                _filter = GUILayout.TextField(_filter ?? "");
                _scroll = GUILayout.BeginScrollView(_scroll,
                    GUILayout.Height(Mathf.Max(110f, Screen.height * 0.37f)));
                for (int i = 0; i < Palette.Length; i++)
                {
                    if (!string.IsNullOrEmpty(_filter) &&
                        Palette[i].Title.IndexOf(_filter,
                            System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    bool current = _mode == Mode.Place && i == _brush;
                    if (GUILayout.Toggle(current, Loc.T(Palette[i].Title), "Button"))
                    { _brush = i; _mode = Mode.Place; }
                }
                GUILayout.EndScrollView();
                GUILayout.Label(Loc.T("Импортированные модели"));
                foreach (var prefab in _customPrefabs)
                {
                    if (prefab == null) continue;
                    string key = "user:" + prefab.name;
                    if (GUILayout.Toggle(_mode == Mode.Place && _brush == -1 &&
                        _customProp == key, prefab.name, "Button"))
                    { _brush = -1; _customProp = key; _mode = Mode.Place; }
                }
            }
            else
            {
                if (GUILayout.Button(Loc.T("+ Новый триггер")))
                {
                    Change(() =>
                    {
                        var region = _map.Regions.Count > 0 ? _map.Regions[0] : null;
                        _map.Triggers.Add(new MapData.Trigger
                        {
                            Id = NewId(), Name = Loc.T("Новое правило"),
                            Event = region == null ? "OnStart" : "OnEnterRegion",
                            RegionId = region == null ? "" : region.Id,
                            Message = Loc.T("Вы вошли в область.")
                        });
                        _selectionKind = Kind.Trigger;
                        _selectionIndex = _map.Triggers.Count - 1;
                    });
                }
                _scroll = GUILayout.BeginScrollView(_scroll,
                    GUILayout.Height(Mathf.Max(110f, Screen.height * 0.37f)));
                for (int i = 0; i < _map.Triggers.Count; i++)
                {
                    int index = i;
                    var t = _map.Triggers[i];
                    if (GUILayout.Button((t.Enabled ? "● " : "○ ") + (t.Name ?? t.Id)))
                    { _selectionKind = Kind.Trigger; _selectionIndex = index; Redraw(); }
                }
                GUILayout.EndScrollView();
                GUILayout.Label(Loc.T("Событие → условие → действие"));
            }
            GUILayout.Label(Loc.T("Шаблоны композиций:"));
            if (GUILayout.Button(Loc.T("Обновить список шаблонов"))) RefreshTemplates();
            _templateScroll = GUILayout.BeginScrollView(_templateScroll,
                GUILayout.Height(112f));
            foreach (var templateName in _templateNames)
                if (GUILayout.Button(Loc.F("Установить: {0}", templateName)))
                    ChooseTemplate(templateName);
            GUILayout.EndScrollView();
            if (_mode == Mode.Template)
                GUILayout.Label(Loc.T("ЛКМ: вставить · Q/E: повернуть · Esc: выйти. Зелёный: внутри карты; красный: границы/лимиты."));
            if (GUILayout.Button(Loc.T("Проверить карту"))) RunValidation();
            if (_validationIssues.Count > 0)
            {
                GUILayout.Label(Loc.F("Ошибок проверки: {0}", _validationIssues.Count));
                for (int i = 0; i < Mathf.Min(3, _validationIssues.Count); i++)
                    GUILayout.Label((i + 1) + ". " + _validationIssues[i]);
                if (_validationIssues.Count > 3)
                    GUILayout.Label(Loc.F("Ещё ошибок: {0}. Скопируйте весь список.", _validationIssues.Count - 3));
                if (GUILayout.Button(Loc.T("Копировать список ошибок")))
                    GUIUtility.systemCopyBuffer = string.Join("\n", _validationIssues);
            }
            if (GUILayout.Button(Loc.T("Очистить всё")))
            {
                if (_confirmClear)
                {
                    Change(() =>
                    {
                        _map.Things.Clear(); _map.Crypts.Clear(); _map.Locations.Clear();
                        _map.Trails.Clear(); _map.Events.Clear(); _map.Regions.Clear();
                        _map.Triggers.Clear(); _map.TerrainStrokes.Clear(); _map.TerrainPaints.Clear();
                        _selectionIndex = -1;
                    });
                    _confirmClear = false;
                }
                else { _confirmClear = true; _say = Loc.T("Нажмите «Очистить всё» ещё раз для подтверждения."); }
            }
            if (GUILayout.Button(_confirmExit
                ? Loc.T("Выйти без сохранения?")
                : Loc.T("Выйти в игру"))) RequestExit();
            if (_confirmExit && GUILayout.Button(Loc.T("Отменить выход")))
                _confirmExit = false;
            GUILayout.Label(_dirty ? Loc.T("Есть несохранённые изменения")
                : string.IsNullOrEmpty(_path) ? Loc.T("Новая карта ещё не записана")
                : Loc.T("Изменения сохранены"));
            GUILayout.Label(Loc.T("Ctrl+S — сохранить · Ctrl+Shift+S — копию · Ctrl+Z/Y — история"));
            if (!string.IsNullOrEmpty(_say)) GUILayout.Label(_say);
            GUILayout.EndArea();

            // Keep the 3D view usable. The right pane edits selected objects.
            if (Screen.width > PanelWidth + InspectorWidth + 100f)
            {
                GUILayout.BeginArea(new Rect(Screen.width - InspectorWidth - 10f,
                    ToolbarHeight + 10f, InspectorWidth,
                    Mathf.Max(120f, Screen.height - ToolbarHeight - 20f)),
                    GUI.skin.box);
                _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
                GUILayout.Label(Loc.T("ИНСПЕКТОР"));
                DrawInspector();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            if (_selectionDragActive && !_selectionDragMovesGroup &&
                _mode == Mode.Select && Input.GetMouseButton(0))
            {
                float startX = _selectionDragScreen.x, endX = Input.mousePosition.x;
                float startY = _selectionDragScreen.y, endY = Input.mousePosition.y;
                float width = Mathf.Abs(endX - startX), height = Mathf.Abs(endY - startY);
                if (width + height > 8f)
                    GUI.Box(new Rect(Mathf.Min(startX, endX),
                        Screen.height - Mathf.Max(startY, endY),
                        width, height), "");
            }
        }

        private void FloatField(string label, float value, float min, float max,
                                System.Action<float> apply)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T(label), GUILayout.Width(108f));
            string shown = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            string entered = GUILayout.TextField(shown);
            if (entered != shown && float.TryParse(entered.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result)
                && !float.IsNaN(result) && !float.IsInfinity(result))
                Change(() => apply(Mathf.Clamp(result, min, max)));
            GUILayout.EndHorizontal();
        }

        private void IntField(string label, int value, int min, int max,
                              System.Action<int> apply)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T(label), GUILayout.Width(108f));
            string entered = GUILayout.TextField(value.ToString());
            if (entered != value.ToString() && int.TryParse(entered, out int result))
                Change(() => apply(Mathf.Clamp(result, min, max)));
            GUILayout.EndHorizontal();
        }

        private void TextField(string label, string value, System.Action<string> apply,
                               bool multiline = false)
        {
            GUILayout.Label(Loc.T(label));
            string previous = value ?? "";
            string entered = multiline
                ? GUILayout.TextArea(previous, GUILayout.MinHeight(64f))
                : GUILayout.TextField(previous);
            if (entered != previous)
                Change(() => apply(entered.Length <= 240 ? entered : entered.Substring(0, 240)));
        }

        private void ToggleField(string label, bool value, System.Action<bool> apply)
        {
            bool next = GUILayout.Toggle(value, Loc.T(label));
            if (next != value) Change(() => apply(next));
        }

        private void ChoiceField(string label, string current, string[] variants,
                                 System.Action<string> apply)
        {
            GUILayout.Label(Loc.T(label) + ": " + current);
            GUILayout.BeginVertical();
            foreach (string option in variants)
            {
                if (GUILayout.Toggle(current == option, option, "Button") && current != option)
                    Change(() => apply(option));
            }
            GUILayout.EndVertical();
        }

        private void DrawInspector()
        {
            if (_selectedThingIds.Count > 1)
            {
                DrawGroupInspector();
                return;
            }
            int i = _selectionIndex;
            if (i < 0)
            {
                GUILayout.Label(Loc.T("Выберите объект правой кнопкой мыши или инструментом «Выделить»."));
                GUILayout.Label(Loc.T("Области и предметы можно связывать с правилами на вкладке «Логика»."));
                return;
            }
            GUILayout.Label(_selectionKind + " #" + (i + 1));
            float half = _map.Size * 0.5f;
            switch (_selectionKind)
            {
                case Kind.Thing when i < _map.Things.Count:
                    var t = _map.Things[i];
                    TextField("Модель Resources", t.Prop, value => t.Prop = value);
                    FloatField("X", t.X, -half, half, value => t.X = value);
                    FloatField("Z", t.Z, -half, half, value => t.Z = value);
                    FloatField("Поворот", t.Yaw, -360, 360, value => t.Yaw = value);
                    FloatField("Масштаб", t.Scale, 0.1f, 10f, value => t.Scale = value);
                    GUILayout.Label("ID: " + t.Id);
                    GUILayout.Label(Loc.T("Материал модели: оригинальный / импортированный"));
                    if (GUILayout.Toggle(string.IsNullOrEmpty(t.MaterialId), Loc.T("Оригинальный"), "Button") &&
                        !string.IsNullOrEmpty(t.MaterialId))
                        Change(() => t.MaterialId = "");
                    foreach (var material in _objectMaterials)
                        if (material != null && GUILayout.Toggle(t.MaterialId == material.name,
                            material.name, "Button") && t.MaterialId != material.name)
                            Change(() => t.MaterialId = material.name);
                    if (GUILayout.Button(Loc.T("Добавить событие взаимодействия (F)")))
                        AddObjectInteraction(t);
                    break;
                case Kind.Crypt when i < _map.Crypts.Count:
                    var crypt = _map.Crypts[i];
                    FloatField("X", crypt.X, -half, half, value => crypt.X = value);
                    FloatField("Z", crypt.Z, -half, half, value => crypt.Z = value);
                    FloatField("Поворот", crypt.Yaw, -360, 360, value => crypt.Yaw = value);
                    break;
                case Kind.Location when i < _map.Locations.Count:
                    var loc = _map.Locations[i];
                    TextField("Имя", loc.Name, value => loc.Name = value);
                    ChoiceField("Тип", loc.Kind, new[] { "village", "forest", "field" },
                        value => loc.Kind = value);
                    FloatField("X", loc.X, -half, half, value => loc.X = value);
                    FloatField("Z", loc.Z, -half, half, value => loc.Z = value);
                    FloatField("Радиус", loc.Radius, 3f, 80f, value => loc.Radius = value);
                    IntField("Жители", loc.People, 0, 100, value => loc.People = value);
                    break;
                case Kind.Trail when i < _map.Trails.Count:
                    var trail = _map.Trails[i];
                    FloatField("X", trail.X, -half, half, value => trail.X = value);
                    FloatField("Z", trail.Z, -half, half, value => trail.Z = value);
                    break;
                case Kind.Event when i < _map.Events.Count:
                    var ev = _map.Events[i];
                    ChoiceField("Тип", ev.Kind, new[] { "caravan", "village" },
                        value => ev.Kind = value);
                    FloatField("X", ev.X, -half, half, value => ev.X = value);
                    FloatField("Z", ev.Z, -half, half, value => ev.Z = value);
                    break;
                case Kind.Region when i < _map.Regions.Count:
                    var rg = _map.Regions[i];
                    TextField("Название области", rg.Name, value => rg.Name = value);
                    FloatField("X", rg.X, -half, half, value => rg.X = value);
                    FloatField("Z", rg.Z, -half, half, value => rg.Z = value);
                    FloatField("Радиус", rg.Radius, 1f, half, value => rg.Radius = value);
                    GUILayout.Label("ID: " + rg.Id);
                    break;
                case Kind.Trigger when i < _map.Triggers.Count:
                    DrawTrigger(_map.Triggers[i]);
                    break;
                default:
                    GUILayout.Label(Loc.T("Объект удалён или изменился. Выберите другой."));
                    return;
            }
            if (GUILayout.Button(Loc.T("Создать копию"))) DuplicateSelected();
            if (GUILayout.Button(Loc.T("Удалить выбранное"))) DeleteSelected();
        }

        /// <summary>
        /// Turns an existing placed object into an interactive map object using
        /// the same F priority stack as the game. Does not attach arbitrary code.
        /// </summary>
        private void AddObjectInteraction(MapData.Thing thing)
        {
            if (thing == null || string.IsNullOrWhiteSpace(thing.Id))
            {
                _say = Loc.T("У предмета нет ID. Сначала исправьте карту.");
                return;
            }
            if (_map.Triggers.Count >= 128)
            {
                _say = Loc.T("Нельзя добавить правило: достигнут предел 128.");
                return;
            }
            Change(() =>
            {
                _map.Triggers.Add(new MapData.Trigger
                {
                    Id = NewId(),
                    Name = Loc.T("Взаимодействие: ") + thing.Prop,
                    Enabled = true, Once = false,
                    Event = "OnInteractObject",
                    TargetId = thing.Id,
                    Condition = "Always",
                    Action = "ShowMessage",
                    Message = Loc.T("Вы осмотрели предмет.")
                });
                _logicTab = true;
                _selectionKind = Kind.Trigger;
                _selectionIndex = _map.Triggers.Count - 1;
            });
            _say = Loc.T("Событие создано. Отредактируйте его сообщение и условия на вкладке «Логика».");
        }

        private void DrawTrigger(MapData.Trigger t)
        {
            TextField("Название правила", t.Name, value => t.Name = value);
            ToggleField("Включено", t.Enabled, value => t.Enabled = value);
            ToggleField("Только один раз", t.Once, value => t.Once = value);
            ChoiceField("Событие", t.Event,
                new[] { "OnStart", "OnEnterRegion", "OnInteractObject" },
                value => t.Event = value);
            if (t.Event == "OnEnterRegion")
            {
                GUILayout.Label(Loc.T("Область:"));
                foreach (var region in _map.Regions)
                    if (GUILayout.Toggle(region.Id == t.RegionId, region.Name ?? region.Id, "Button") &&
                        region.Id != t.RegionId)
                        Change(() => t.RegionId = region.Id);
            }
            else if (t.Event == "OnInteractObject")
            {
                GUILayout.Label(Loc.T("Предмет:"));
                foreach (var thing in _map.Things)
                    if (GUILayout.Toggle(thing.Id == t.TargetId,
                        thing.Prop + " · " + (thing.Id.Length > 6 ? thing.Id.Substring(0, 6) : thing.Id), "Button") &&
                        thing.Id != t.TargetId)
                        Change(() => t.TargetId = thing.Id);
            }
            ChoiceField("Условие", t.Condition, new[] { "Always", "FlagIsTrue", "FlagIsFalse" },
                value => t.Condition = value);
            ChoiceField("Действие", t.Action, new[] { "ShowMessage", "SetFlag", "ClearFlag" },
                value => t.Action = value);
            if (t.Action == "SetFlag" || t.Action == "ClearFlag" ||
                t.Condition == "FlagIsTrue" || t.Condition == "FlagIsFalse")
                TextField("Логический флаг", t.Flag, value => t.Flag = value);
            if (t.Action == "ShowMessage")
                TextField("Сообщение игроку", t.Message, value => t.Message = value, true);
            GUILayout.Label(Loc.T("Это безопасная система событий без произвольного исполнения кода."));
        }

        private List<string> Validate()
        {
            _map.EnsureLists();
            var errors = new List<string>();
            if (_map.Crypts.Count < 2)
                errors.Add(Loc.T("Нужно минимум два склепа игроков."));
            if (_map.Crypts.Count > 8)
                errors.Add(Loc.T("Поддерживается не более восьми склепов."));
            if (float.IsNaN(_map.Size) || float.IsInfinity(_map.Size) ||
                _map.Size < 150f || _map.Size > 800f)
                errors.Add(Loc.T("Неверный размер поля."));
            float half = _map.Size * 0.5f;
            bool InField(float x, float z)
                => !float.IsNaN(x) && !float.IsNaN(z) &&
                   !float.IsInfinity(x) && !float.IsInfinity(z) &&
                   Mathf.Abs(x) <= half && Mathf.Abs(z) <= half;
            foreach (var c in _map.Crypts)
                if (c == null || !InField(c.X, c.Z)) errors.Add(Loc.T("Склеп вне карты."));
            foreach (var t in _map.Things)
            {
                if (t == null || !InField(t.X, t.Z) || t.Scale <= 0f ||
                    float.IsNaN(t.Scale) || float.IsInfinity(t.Scale))
                    errors.Add(Loc.T("Неверная позиция или размер объекта."));
                else if (string.IsNullOrWhiteSpace(t.Prop) || MapLoader.Prefab(t.Prop) == null)
                    errors.Add(Loc.F("Модель не найдена: {0}", t.Prop));
            }
            foreach (var l in _map.Locations)
                if (l == null || !InField(l.X, l.Z) || l.Radius <= 0f ||
                    l.People < 0) errors.Add(Loc.T("Неверная подлокация."));
            foreach (var t in _map.Trails)
                if (t == null || !InField(t.X, t.Z)) errors.Add(Loc.T("Тропа вне карты."));
            foreach (var t in _map.Events)
                if (t == null || !InField(t.X, t.Z)) errors.Add(Loc.T("Событие вне карты."));
            if (!MapTerrain.Valid(_map))
                errors.Add(Loc.T("Некорректные параметры рельефа или материалов земли."));
            foreach (var paint in _map.TerrainPaints)
                if (paint != null && (string.IsNullOrWhiteSpace(paint.MaterialId) ||
                    (paint.MaterialId != MapTerrain.RestoreOriginalMaterial &&
                     MapAssets.TerrainMaterial(paint.MaterialId) == null)))
                    errors.Add(Loc.F("Материал земли не найден: {0}", paint.MaterialId));
            foreach (var t in _map.Things)
                if (t != null && !string.IsNullOrEmpty(t.MaterialId) &&
                    MapAssets.ObjectMaterial(t.MaterialId) == null)
                    errors.Add(Loc.F("Нет материала объекта: {0}", t.MaterialId));
            var regionIds = new HashSet<string>();
            foreach (var region in _map.Regions)
                if (region == null || string.IsNullOrEmpty(region.Id) ||
                    !regionIds.Add(region.Id) || !InField(region.X, region.Z) ||
                    region.Radius <= 0f || float.IsNaN(region.Radius))
                    errors.Add(Loc.T("Неверная область или повтор ID."));
            var objectIds = new HashSet<string>();
            foreach (var thing in _map.Things)
                if (thing != null && (string.IsNullOrEmpty(thing.Id) ||
                    !objectIds.Add(thing.Id)))
                    errors.Add(Loc.T("Объект без уникального ID."));
            if (_map.Triggers.Count > 128)
                errors.Add(Loc.T("Максимум 128 правил на карте."));
            var triggerIds = new HashSet<string>();
            foreach (var rule in _map.Triggers)
            {
                if (rule == null || string.IsNullOrEmpty(rule.Id) ||
                    !triggerIds.Add(rule.Id))
                { errors.Add(Loc.T("Триггер без уникального ID.")); continue; }
                if (rule.Event != "OnStart" && rule.Event != "OnEnterRegion" &&
                    rule.Event != "OnInteractObject")
                    errors.Add(Loc.T("Неизвестное событие правила."));
                if (rule.Condition != "Always" && rule.Condition != "FlagIsTrue" &&
                    rule.Condition != "FlagIsFalse")
                    errors.Add(Loc.T("Неизвестное условие правила."));
                if (rule.Action != "ShowMessage" && rule.Action != "SetFlag" &&
                    rule.Action != "ClearFlag")
                    errors.Add(Loc.T("Неизвестное действие правила."));
                if (rule.Event == "OnEnterRegion" && !regionIds.Contains(rule.RegionId))
                    errors.Add(Loc.T("Триггер ссылается на отсутствующую область."));
                if (rule.Event == "OnInteractObject" && !objectIds.Contains(rule.TargetId))
                    errors.Add(Loc.T("Триггер ссылается на отсутствующий предмет."));
                if ((rule.Action == "SetFlag" || rule.Action == "ClearFlag" ||
                    rule.Condition == "FlagIsTrue" || rule.Condition == "FlagIsFalse") &&
                    (string.IsNullOrWhiteSpace(rule.Flag) || rule.Flag.Length > 64))
                    errors.Add(Loc.T("Условие или действие не содержит допустимый флаг."));
                if (rule.Action == "ShowMessage" &&
                    (string.IsNullOrWhiteSpace(rule.Message) || rule.Message.Length > 240))
                    errors.Add(Loc.T("Сообщение правила отсутствует или слишком длинное."));
            }
            return errors;
        }

        /// <summary>
        /// Compare actual file contents, not timestamps: clocks and sync tools can
        /// preserve mtimes while changing map bytes. Errors block normal saves.
        /// </summary>
        private void RequestExit()
        {
            if (_dirty && !_confirmExit)
            {
                _confirmExit = true;
                _say = Loc.T("Карта изменена. Сохраните её или подтвердите выход без сохранения.");
                return;
            }
            SceneManager.LoadScene(0);
        }

        private static string FileFingerprint(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            using (var hash = SHA256.Create())
                return System.Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }

        private void Save() => SaveInternal(false);
        private void SaveCopy() => SaveInternal(true);

        private void SaveInternal(bool saveCopy)
        {
            var errors = Validate();
            if (errors.Count > 0)
            {
                _validationIssues = errors;
                _say = Loc.F("Карта не записана: {0} ошибок. Первая: {1}",
                    errors.Count, errors[0]);
                return;
            }
            _map.Name = string.IsNullOrWhiteSpace(_name) ? Loc.T("Новая карта") : _name.Trim();
            try
            {
                bool createNew = saveCopy || string.IsNullOrEmpty(_path);
                string destination = createNew ? null : _path;
                if (!createNew)
                {
                    // No overwrite of unreadable or externally edited data.
                    if (_unreadableSource || FileFingerprint(destination) != _openedFingerprint)
                    {
                        _say = Loc.T("Файл карты изменён или повреждён на диске. Оригинал не перезаписан. Используйте «Сохранить копию».");
                        return;
                    }
                }
                else
                {
                    string file = string.Join("_", _map.Name.Split(Path.GetInvalidFileNameChars())).Trim().Trim('.');
                    if (string.IsNullOrEmpty(file)) file = "SinbinderMap";
                    string folder = !string.IsNullOrEmpty(_path)
                        ? Path.GetDirectoryName(_path) : MapData.Folder;
                    if (string.IsNullOrEmpty(folder)) folder = MapData.Folder;
                    destination = Path.Combine(folder, file + ".json");
                    int suffix = 2;
                    while (File.Exists(destination))
                        destination = Path.Combine(folder, file + "_" + suffix++ + ".json");
                }

                // CreateNew must not overwrite an existing destination even if
                // another process creates the name after the File.Exists check.
                _map.Save(destination, overwrite: !createNew);
                _path = destination;
                _openedFingerprint = FileFingerprint(_path);
                _unreadableSource = false;
                _savedSnapshot = JsonUtility.ToJson(_map);
                _dirty = false;
                _confirmExit = false;
                _validationIssues.Clear();
                var savedMap = JsonUtility.FromJson<MapData>(JsonUtility.ToJson(_map));
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
