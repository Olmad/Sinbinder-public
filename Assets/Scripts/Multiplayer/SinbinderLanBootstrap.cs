using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Multiplayer
{
    /// <summary>
    /// Zero-scene-setup LAN entry point. The isolated arena deliberately
    /// disables (and later restores) the current scene's root objects.
    /// Nothing in the single-player gameplay classes is modified.
    /// </summary>
    public sealed class SinbinderLanBootstrap : MonoBehaviour
    {
        private static SinbinderLanBootstrap _instance;

        private LanRoomServer _server;
        private LanRoomClient _client;
        private readonly List<GameObject> _hiddenRoots = new List<GameObject>();
        private readonly List<Material> _temporaryMaterials = new List<Material>();
        private Sinbinder.Gameplay.SelectionManager _pausedSelection;
        private GameObject _arena;
        private Camera _camera;
        private Transform _pawn1;
        private Transform _pawn2;
        private bool _open;
        private bool _arenaActive;
        private string _address = "127.0.0.1";
        private string _portText = "7777";
        private string _notice = "";
        private bool _dedicated;
        private bool _autoReady;
        private bool _launchHost;
        private string _launchConnect;
        private float _nextAutoReconnect;
        private int _autoReconnectAttempts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Sinbinder LAN (автозапуск интерфейса)");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SinbinderLanBootstrap>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _server = new LanRoomServer();
            _client = new LanRoomClient();
            _client.Received += packet =>
            {
                if (_autoReady && packet.type == "welcome") _client.Ready();
            };
            SceneManager.activeSceneChanged += OnSceneChanged;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-port" && i + 1 < args.Length)
                    _portText = args[++i];
                else if (args[i] == "-server" || args[i] == "-sinbinder-server" || args[i] == "--sinbinder-server")
                    _dedicated = true;
                else if (args[i] == "-lan-host")
                    _launchHost = true;
                else if (args[i] == "-lan-connect" && i + 1 < args.Length)
                    _launchConnect = args[++i];
                else if (args[i] == "-lan-auto-ready")
                    _autoReady = true;
            }

            if (_dedicated)
            {
                if (TryPort(out int port) && _server.Start(port))
                {
                    EnterArena();
                    Debug.Log("[Sinbinder LAN] Dedicated test server listening on TCP " + port);
                }
                else Debug.LogError("[Sinbinder LAN] " + (_server.Error ?? "Invalid port"));
            }
            else if (_launchHost)
                StartHost();
            else if (!string.IsNullOrEmpty(_launchConnect))
            {
                _address = _launchConnect;
                StartClient();
            }
        }

        private void OnSceneChanged(Scene previous, Scene next)
        {
            // A new main scene invalidates the temporary arena references.
            if (_arenaActive) Leave();
        }

        private void Update()
        {
            _server.Pump(Time.unscaledDeltaTime);
            _client.Pump();
            if (!_arenaActive || _dedicated) return;

            // A second game window may launch before the host finishes
            // loading its first Unity scene. Retry command-line joins for
            // up to a minute instead of permanently failing on the first
            // refused TCP connection.
            if (_autoReady && !string.IsNullOrEmpty(_launchConnect) &&
                !_client.Connected && !_client.Playing &&
                _autoReconnectAttempts < 30 &&
                (_client.Status.StartsWith("Ошибка соединения:") ||
                 _client.Status == "Сервер закрыл соединение") &&
                Time.realtimeSinceStartup >= _nextAutoReconnect &&
                TryPort(out int retryPort))
            {
                _autoReconnectAttempts++;
                _nextAutoReconnect = Time.realtimeSinceStartup + 2f;
                _client.Connect(_address, retryPort);
            }

            if (_pawn1 != null)
                _pawn1.position = Vector3.Lerp(_pawn1.position, _client.First, Mathf.Clamp01(12f * Time.unscaledDeltaTime));
            if (_pawn2 != null)
                _pawn2.position = Vector3.Lerp(_pawn2.position, _client.Second, Mathf.Clamp01(12f * Time.unscaledDeltaTime));

            if (!_client.Playing || _camera == null) return;

            // Right click moves YOUR pawn. Left click and Tab/V belong to
            // the main game's camera/selection; the arena never steals them.
            if (Input.GetMouseButtonDown(1))
            {
                Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
                var ground = new Plane(Vector3.up, Vector3.zero);
                if (ground.Raycast(ray, out float distance))
                {
                    Vector3 destination = ray.GetPoint(distance);
                    _client.Move(destination);
                }
            }
        }

        private void OnGUI()
        {
            if (Application.isBatchMode || _dedicated) return;
            if (!_open)
            {
                if (GUI.Button(new Rect(Screen.width - 82, 8, 74, 28), "LAN тест"))
                    _open = true;
                return;
            }

            float height = _arenaActive ? 265 : 219;
            var frame = new Rect(Screen.width - 328, 8, 320, height);
            GUI.Box(frame, "Sinbinder: сетевой тест");
            GUILayout.BeginArea(new Rect(frame.x + 12, frame.y + 28, frame.width - 24, frame.height - 37));
            GUILayout.Label("Тест двух игроков (НЕ сюжетный мультиплеер)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Адрес", GUILayout.Width(50));
            _address = GUILayout.TextField(_address, 64);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Порт", GUILayout.Width(50));
            _portText = GUILayout.TextField(_portText, 5);
            GUILayout.EndHorizontal();

            if (!_arenaActive)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Создать LAN"))
                    StartHost();
                if (GUILayout.Button("Подключиться"))
                    StartClient();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Игроков: " + _client.Players + "/2; готовы: " + _client.ReadyCount);
                GUILayout.Label("Ваш номер: " + _client.PlayerId + "; тик: " + _client.Tick);
                GUILayout.BeginHorizontal();
                GUI.enabled = _client.Connected && _client.PlayerId != 0 && !_client.Playing;
                if (GUILayout.Button("Готов")) _client.Ready();
                GUI.enabled = _client.Playing;
                if (GUILayout.Button("Стоять")) _client.Hold();
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (!_client.Connected && TryPort(out int reconnectPort))
                {
                    if (GUILayout.Button("Повторить подключение"))
                        _client.Connect(_server.Active ? "127.0.0.1" : _address, reconnectPort);
                }
                GUILayout.Label("ПКМ по полю: идти. Два окна игры = два игрока.");
                if (GUILayout.Button("Выйти из теста")) Leave();
            }

            GUILayout.Label(_client.Connected ? _client.Status : (_notice.Length > 0 ? _notice : _client.Status));
            if (GUILayout.Button("Свернуть")) _open = false;
            GUILayout.EndArea();
        }

        private bool TryPort(out int port)
        {
            return int.TryParse(_portText, out port) && port > 0 && port <= 65535;
        }

        private void StartHost()
        {
            _notice = "";
            if (!TryPort(out int port)) { _notice = "Неверный порт"; return; }
            if (!_server.Start(port)) { _notice = _server.Error; return; }
            EnterArena();
            _client.Connect("127.0.0.1", port);
            _notice = "Хост открыт на TCP-порту " + port;
        }

        private void StartClient()
        {
            _notice = "";
            if (!TryPort(out int port)) { _notice = "Неверный порт"; return; }
            EnterArena();
            _client.Connect(_address, port);
        }

        private void EnterArena()
        {
            if (_arenaActive) return;
            _arenaActive = true;
            _open = true;

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == gameObject || !root.activeSelf) continue;
                    _hiddenRoots.Add(root);
                    root.SetActive(false);
                }
            }

            // SelectionManager is DontDestroyOnLoad and otherwise keeps
            // consuming right-clicks against the now-inactive game scene.
            var selection = Sinbinder.Gameplay.SelectionManager.Instance;
            if (selection != null && selection.isActiveAndEnabled)
            {
                _pausedSelection = selection;
                selection.enabled = false;
            }

            _arena = new GameObject("Sinbinder LAN: temporary arena");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "LAN arena floor";
            floor.transform.SetParent(_arena.transform);
            floor.transform.localScale = new Vector3(2, 1, 2);
            SetLayer(floor, 30);
            Tint(floor, new Color(0.14f, 0.19f, 0.15f));

            // All original scene roots (including their lights) are inactive.
            // Without a temporary key light the URP/Lit pawns can appear black.
            var lamp = new GameObject("LAN arena key light");
            lamp.transform.SetParent(_arena.transform);
            lamp.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var keyLight = lamp.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.6f;
            keyLight.cullingMask = 1 << 30;

            _pawn1 = SpawnPawn("Греховод 1", new Vector3(-3, 0.8f, 0), new Color(0.38f, 0.8f, 0.55f));
            _pawn2 = SpawnPawn("Греховод 2", new Vector3(3, 0.8f, 0), new Color(0.91f, 0.48f, 0.37f));

            var cam = new GameObject("LAN spectator camera");
            cam.transform.SetParent(_arena.transform);
            cam.transform.position = new Vector3(0, 18, -14);
            cam.transform.LookAt(Vector3.zero);
            _camera = cam.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.07f, 0.09f, 0.1f);
            _camera.depth = 100;
            _camera.cullingMask = 1 << 30;
            _camera.orthographic = true;
            _camera.orthographicSize = 12f;
            _camera.nearClipPlane = 0.1f;

            // The arena is test-only: no scene prefabs, lobby objects or
            // NetworkObject components are needed.
        }

        private Transform SpawnPawn(string name, Vector3 pos, Color color)
        {
            var pawn = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pawn.name = name;
            pawn.transform.SetParent(_arena.transform);
            pawn.transform.position = pos;
            pawn.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            SetLayer(pawn, 30);
            Tint(pawn, color);
            return pawn.transform;
        }

        private static void SetLayer(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayer(child.gameObject, layer);
        }

        private void Tint(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;
            var material = new Material(shader);
            // URP Lit uses _BaseColor, while Standard/Unlit shaders use
            // _Color. Material.color alone is not portable across them.
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            renderer.material = material;
            _temporaryMaterials.Add(material);
        }

        private void Leave()
        {
            _client.Disconnect();
            _server.Stop();
            if (_arena != null) Destroy(_arena);
            _arena = null;
            foreach (var material in _temporaryMaterials)
                if (material != null) Destroy(material);
            _temporaryMaterials.Clear();
            _pawn1 = null;
            _pawn2 = null;
            _camera = null;
            foreach (var root in _hiddenRoots)
                if (root != null) root.SetActive(true);
            _hiddenRoots.Clear();
            if (_pausedSelection != null)
            {
                _pausedSelection.enabled = true;
                _pausedSelection = null;
            }
            _arenaActive = false;
            _autoReconnectAttempts = 30;
            _notice = "";
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Leave();
            if (_instance == this) _instance = null;
        }
    }
}
