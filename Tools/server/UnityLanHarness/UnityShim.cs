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
    public enum PrimitiveType { Plane, Capsule }
    public enum LightType { Directional }
    public struct Quaternion
    {
        public static Quaternion Euler(float x, float y, float z) { return new Quaternion(); }
    }
    public enum CameraClearFlags { SolidColor }

    public class Object
    {
        public static void Destroy(Object target) { }
        public static void DontDestroyOnLoad(Object target) { }
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
        public string name;
        public int layer;
        public bool activeSelf = true;
        public Transform transform { get; }

        public GameObject(string title = "")
        {
            name = title;
            transform = new Transform { gameObject = this };
        }

        public static GameObject CreatePrimitive(PrimitiveType kind) { return new GameObject(); }

        public T AddComponent<T>() where T : Component, new()
        {
            return new T { gameObject = this };
        }

        public T GetComponent<T>() where T : class
        {
            if (typeof(T) == typeof(Renderer)) return new Renderer() as T;
            return null;
        }

        public void SetActive(bool active) { activeSelf = active; }
    }

    public class Transform : Component, IEnumerable<Transform>
    {
        public Vector3 position;
        public Vector3 localScale;
        public Quaternion rotation;
        public void SetParent(Transform parent) { }
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

    public class Light : Component
    {
        public LightType type;
        public float intensity;
        public int cullingMask;
    }
    public class Renderer : Component { public Material material; }
    public class Shader : Object { public static Shader Find(string title) { return new Shader(); } }
    public class Material : Object
    {
        public Color color;
        public Material(Shader shader) { }
        public bool HasProperty(string name) { return true; }
        public void SetColor(string name, Color value) { color = value; }
    }

    public static class Mathf
    {
        public static float Abs(float value) { return Math.Abs(value); }
        public static float Clamp(float value, float lo, float hi) { return Math.Min(hi, Math.Max(lo, value)); }
        public static float Clamp01(float value) { return Clamp(value, 0f, 1f); }
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

    public static class Screen { public static int width { get { return 1280; } } }
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
    public struct Scene
    {
        public bool IsValid() { return true; }
        public UnityEngine.GameObject[] GetRootGameObjects() { return Array.Empty<UnityEngine.GameObject>(); }
    }

    public static class SceneManager
    {
        public static event Action<Scene, Scene> activeSceneChanged;
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
