// Only for compiling/testing networking scripts under .NET, NOT part of
// the Unity Assets folder and NEVER shipped with the actual game.
// These minimal shapes let CI catch pure C# mistakes and test TCP behavior.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType mode) { }
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad }
    public enum PrimitiveType { Plane, Capsule, Cube, Cylinder, Sphere }
    public enum LightType { Directional, Point }
    public enum LightShadows { None }
    public enum LightRenderMode { ForcePixel }
    public enum FindObjectsSortMode { None }
    public struct Quaternion
    {
        public static Quaternion Euler(float x, float y, float z) { return new Quaternion(); }
        public static Quaternion identity { get { return new Quaternion(); } }
    }
    public enum CameraClearFlags { SolidColor }

    public class Object
    {
        public static void Destroy(Object target) { }
        public static void DontDestroyOnLoad(Object target) { }
        private static int _idCounter;
        private readonly int _id = System.Threading.Interlocked.Increment(ref _idCounter);
        public int GetInstanceID() { return _id; }
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object
        { return Array.Empty<T>(); }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform { get { return gameObject?.transform; } }
        public T GetComponent<T>() where T : class { return gameObject?.GetComponent<T>(); }
    }

    public class MonoBehaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled { get { return enabled && (gameObject == null || gameObject.activeSelf); } }
    }

    public class GameObject : Object
    {
        public static readonly List<GameObject> PrimitiveObjects = new List<GameObject>();
        private Renderer _renderer;
        private Collider _collider;
        public string name;
        public int layer;
        public bool activeSelf = true;
        public bool activeInHierarchy { get { return activeSelf; } }
        public Transform transform { get; }

        public GameObject(string title = "")
        {
            name = title;
            transform = new Transform { gameObject = this };
        }

        public static GameObject CreatePrimitive(PrimitiveType kind)
        {
            var go = new GameObject(kind.ToString());
            go._collider = new Collider { gameObject = go };
            go._renderer = new Renderer { gameObject = go };
            PrimitiveObjects.Add(go);
            return go;
        }

        public static GameObject Find(string name) { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }

        public T AddComponent<T>() where T : Component, new()
        {
            return new T { gameObject = this };
        }

        public T GetComponent<T>() where T : class
        {
            if (typeof(T) == typeof(Renderer)) return _renderer as T;
            if (typeof(T) == typeof(Collider)) return _collider as T;
            return null;
        }

        public void SetActive(bool active) { activeSelf = active; }
    }

    public class Transform : Component, IEnumerable<Transform>
    {
        public Vector3 position;
        public Vector3 localPosition;
        public Vector3 localScale;
        public Quaternion rotation;
        public Quaternion localRotation;
        public void SetParent(Transform parent) { }
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public void LookAt(Vector3 target) { }
        public IEnumerator<Transform> GetEnumerator() { return Enumerable.Empty<Transform>().GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float xx, float yy, float zz) { x = xx; y = yy; z = zz; }
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }

        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float maximum)
        {
            float dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
            float length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length <= maximum || length == 0) return b;
            return new Vector3(a.x + dx * maximum / length,
                               a.y + dy * maximum / length,
                               a.z + dz * maximum / length);
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t,
                               a.y + (b.y - a.y) * t,
                               a.z + (b.z - a.z) * t);
        }
    }

    public struct Color
    {
        public float r, g, b;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
        public static Color operator *(Color c, float f)
        { return new Color(c.r * f, c.g * f, c.b * f); }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h)
        { this.x = x; this.y = y; width = w; height = h; }
    }

    public struct Ray
    {
        public Vector3 GetPoint(float distance) { return Vector3.zero; }
    }
    public struct Plane
    {
        public Plane(Vector3 normal, Vector3 position) { }
        public bool Raycast(Ray ray, out float distance) { distance = 0; return false; }
    }

    public class Camera : Component
    {
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public float depth;
        public int cullingMask;
        public bool orthographic;
        public float orthographicSize;
        public float nearClipPlane;
        public Ray ScreenPointToRay(Vector3 point) { return new Ray(); }
    }

    public class Collider : Component { public bool enabled = true; }
    public class Light : Component
    {
        public LightType type;
        public LightShadows shadows;
        public LightRenderMode renderMode;
        public Color color;
        public float range;
        public float intensity;
        public int cullingMask;
    }
    public class Renderer : Component
    {
        public Material material;
        public Material sharedMaterial;
    }
    public class Shader : Object { public static Shader Find(string title) { return new Shader(); } }
    public class Material : Object
    {
        public Color color;
        public Material(Shader shader) { }
        public bool HasProperty(string name) { return true; }
        public void SetColor(string name, Color value) { color = value; }
        public void SetFloat(string name, float value) { }
        public void EnableKeyword(string keyword) { }
    }

    public static class Mathf
    {
        public static float Abs(float value) { return Math.Abs(value); }
        public static float Clamp(float value, float lo, float hi) { return Math.Min(hi, Math.Max(lo, value)); }
        public static float Clamp01(float value) { return Clamp(value, 0f, 1f); }
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Sin(float v) { return (float)Math.Sin(v); }
    }

    public static class Time
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();
        private static float _offset;
        public static float realtimeSinceStartup { get { return (float)Watch.Elapsed.TotalSeconds + _offset; } }
        public static float unscaledDeltaTime { get { return 0.02f; } }
        // Harness-only clock jumps make 20-second TCP timeouts testable
        // without keeping a CI worker idle for 20 real seconds.
        public static void Advance(float seconds) { _offset += seconds; }
    }

    public static class Screen
    {
        public static int width { get { return 1280; } }
        public static int height { get { return 720; } }
    }
    public static class Application { public static bool isBatchMode { get { return true; } } }
    public static class Input
    {
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static bool GetMouseButtonDown(int button) { return false; }
    }

    public static class Debug
    {
        public static void Log(object obj) { Console.WriteLine(obj); }
        public static void LogError(object obj) { Console.Error.WriteLine(obj); }
    }

    public static class GUI
    {
        public static bool enabled = true;
        public static void Box(Rect bounds, string label) { }
        public static bool Button(Rect bounds, string label) { return false; }
    }

    public sealed class GUILayoutOption { }
    public static class GUILayout
    {
        public static void BeginArea(Rect area) { }
        public static void EndArea() { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static void Label(string text) { }
        public static bool Button(string label) { return false; }
        public static string TextField(string value, int maxLength) { return value; }
        public static GUILayoutOption Width(float width) { return new GUILayoutOption(); }
        public static void Label(string text, params GUILayoutOption[] options) { }
    }

    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Settings = new JsonSerializerOptions
        {
            IncludeFields = true
        };

        public static string ToJson(object obj) { return JsonSerializer.Serialize(obj, Settings); }
        public static T FromJson<T>(string json) { return JsonSerializer.Deserialize<T>(json, Settings); }
    }
}

namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }
    public struct Scene
    {
        public string name { get { return "Multiplayer_Test"; } }
        public bool IsValid() { return true; }
        public UnityEngine.GameObject[] GetRootGameObjects() { return Array.Empty<UnityEngine.GameObject>(); }
    }

    public static class SceneManager
    {
        public static event Action<Scene, Scene> activeSceneChanged;
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static void LoadScene(string name) { }
        public static Scene GetActiveScene() { return new Scene(); }
    }
}

// Only for the Unity networking harness; the actual project supplies this.
namespace Sinbinder.Gameplay
{
    public class SelectionManager : UnityEngine.MonoBehaviour
    {
        public static SelectionManager Instance { get; set; }
    }
}
