// Minimal .NET 8 compile/test doubles. NOT a replacement for Unity PlayMode.
using System;
namespace UnityEngine
{
    public class Object
    {
        public static void Destroy(Object o) { }
        public static void DestroyImmediate(Object o) { }
        public static T Instantiate<T>(T prefab) where T : Object => prefab;
    }
    public class Component : Object
    {
        public Transform transform;
        public Component() { transform = this is Transform own ? own : new Transform(); }
        public T GetComponent<T>() where T : Component, new() => new T();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 localScale = new Vector3(1, 1, 1);
        public Vector3 localPosition;
        public Quaternion rotation;
        public Vector3 InverseTransformPoint(Vector3 p) => p;
        public Vector3 InverseTransformVector(Vector3 v) => v;
        public void SetPositionAndRotation(Vector3 at, Quaternion orientation)
        { position = at; rotation = orientation; }
    }
    public enum PrimitiveType { Cylinder, Cube }
    public class GameObject : Object
    {
        private readonly System.Collections.Generic.Dictionary<Type, Component> _components = new();
        public string name;
        public Transform transform = new Transform();
        public GameObject(string name = "") { this.name = name; }
        public bool activeSelf = true;
        public void SetActive(bool active) { activeSelf = active; }
        public static GameObject Find(string name) => null;
        public static GameObject CreatePrimitive(PrimitiveType t) => new GameObject();
        public T[] GetComponentsInChildren<T>() where T : Component, new()
        {
            return new T[] { GetComponent<T>() };
        }
        public T GetComponentInChildren<T>() where T : Component, new() => GetComponent<T>();
        public T AddComponent<T>() where T : Component, new() => GetComponent<T>();
        public T GetComponent<T>() where T : Component, new()
        {
            if (!_components.TryGetValue(typeof(T), out var component))
                _components[typeof(T)] = component = new T();
            return (T)component;
        }
    }
    public struct RaycastHit { public Vector3 point; }
    public class Collider : Component
    {
        public bool Raycast(Ray ray, out RaycastHit hit, float distance)
        { hit = default; return false; }
    }
    public class Mesh : Object
    {
        public string name;
        public Vector3[] vertices;
        public Vector2[] uv;
        public int[] triangles;
        public int subMeshCount;
        private readonly System.Collections.Generic.Dictionary<int, int[]> _submeshes = new();
        public void SetTriangles(int[] triangles, int index)
        {
            _submeshes[index] = triangles;
            var combined = new System.Collections.Generic.List<int>();
            for (int i = 0; i < subMeshCount; i++)
                if (_submeshes.TryGetValue(i, out var data)) combined.AddRange(data);
            this.triangles = combined.ToArray();
        }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
    }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class MeshCollider : Collider { public Mesh sharedMesh; }
    public class BoxCollider : Collider
    {
        public Vector3 center;
        public Vector3 size;
    }
    public struct Bounds
    {
        public Vector3 center;
        public Vector3 size;
        public void Encapsulate(Bounds b) { }
    }
    public class Renderer : Component
    {
        public bool enabled = true;
        public Bounds bounds;
        private Material[] _shared = Array.Empty<Material>();
        public Material sharedMaterial
        {
            get => _shared.Length > 0 ? _shared[0] : null;
            set => _shared = new[] { value };
        }
        public Material[] sharedMaterials
        {
            get => _shared;
            set => _shared = value;
        }
    }
    public class MeshRenderer : Renderer { }
    public static class Resources
    {
        private static readonly System.Collections.Generic.Dictionary<string,Object> Assets = new();
        static Resources()
        {
            foreach (var name in new[] { "Ground048", "Ground077", "Gravel043", "Rock022" })
                Assets["MapTerrainMaterials/" + name] = new Material(Shader.Find("Lit")) { name = name };
        }
        public static void Register(string path,Object asset) => Assets[path] = asset;
        public static T Load<T>(string path) where T : Object
            => Assets.TryGetValue(path, out var asset) ? asset as T : null;
        public static T[] LoadAll<T>(string directory) where T : Object
        {
            var all = new System.Collections.Generic.List<T>();
            foreach (var pair in Assets)
                if (pair.Key.StartsWith(directory + "/", StringComparison.Ordinal) &&
                    pair.Value is T resource) all.Add(resource);
            return all.ToArray();
        }
    }
    public class Shader : Object { public static Shader Find(string name) => new Shader(); }
    public class Material : Object
    {
        public string name;
        public Color color;
        public Material(Shader shader) { }
        public Material(Material other) { name = other.name; }
        public void SetTexture(string property, Texture2D texture) { }
        public void SetTextureScale(string property, Vector2 scale) { }
    }
    public class Texture2D : Object { }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float red, float green, float blue, float alpha = 1)
        { r = red; g = green; b = blue; a = alpha; }
    }
    public struct Vector2
    {
        public float x, y;
        public static Vector2 one => new Vector2(1,1);
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static float Distance(Vector2 a, Vector2 b)
        { float x = a.x - b.x, y = a.y - b.y; return MathF.Sqrt(x*x + y*y); }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3();
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 forward => new Vector3(0,0,1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x*b,a.y*b,a.z*b);
    }
    public struct Quaternion
    {
        public static Quaternion Euler(float x, float y, float z) => new Quaternion();
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion();
        public static Vector3 operator *(Quaternion a, Vector3 b) => b;
    }
    public static class Mathf
    {
        public const float Deg2Rad = MathF.PI/180f;
        public static float Max(float a,float b) => MathF.Max(a,b);
        public static int Max(int a,int b) => Math.Max(a,b);
        public static float Min(float a,float b) => MathF.Min(a,b);
        public static int Min(int a,int b) => Math.Min(a,b);
        public static float Abs(float a) => MathF.Abs(a);
        public static int Abs(int a) => Math.Abs(a);
        public static float Cos(float a) => MathF.Cos(a);
        public static float Sin(float a) => MathF.Sin(a);
        public static float Sqrt(float a) => MathF.Sqrt(a);
        public static float Round(float a) => MathF.Round(a);
        public static int RoundToInt(float a) => (int)MathF.Round(a);
        public static float Clamp(float v,float min,float max)=> MathF.Max(min,MathF.Min(max,v));
        public static int Clamp(int v,int min,int max) => Math.Max(min,Math.Min(max,v));
        public static float Repeat(float value, float length) => value-MathF.Floor(value/length)*length;
    }
    public class Camera : Behaviour
    {
        public static Camera main;
        public Ray ScreenPointToRay(Vector3 point) => new Ray();
    }
    public struct Ray { public Vector3 GetPoint(float d) => Vector3.zero; }
    public struct Plane
    {
        public Plane(Vector3 normal,Vector3 point) { }
        public bool Raycast(Ray ray,out float d){d=0;return true;}
    }
    public enum KeyCode { LeftShift, RightShift, LeftControl, RightControl, A, Z, Y, S, D, Delete, Escape, Q, E }
    public static class Input
    {
        public static Vector3 mousePosition;
        public static Vector2 mouseScrollDelta;
        public static bool GetKey(KeyCode code) => false;
        public static bool GetKeyDown(KeyCode code) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static bool GetMouseButton(int button) => false;
        public static bool GetMouseButtonUp(int button) => false;
        public static float GetAxisRaw(string name) => 0;
    }
    public static class Screen { public static int width = 1600, height = 900; }
    public static class Time { public static float unscaledDeltaTime = 0.016f; }
    public class GUILayoutOption { }
    public class GUIStyle { }
    public class GUISkin { public GUIStyle box = new GUIStyle(); }
    public static class GUI
    {
        public static GUISkin skin = new GUISkin();
        public static void Box(Rect r, string caption) { }
    }
    public struct Rect
    {
        public Rect(float x,float y,float width,float height) { }
    }
    public static class GUILayout
    {
        public static GUILayoutOption Height(float size)=>new GUILayoutOption();
        public static GUILayoutOption MinHeight(float size)=>new GUILayoutOption();
        public static GUILayoutOption Width(float size)=>new GUILayoutOption();
        public static void BeginArea(Rect bounds, GUIStyle style) { }
        public static void Space(float amount) { }
        public static void EndArea() { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static void BeginVertical() { }
        public static void EndVertical() { }
        public static void Label(string value, params GUILayoutOption[] options) { }
        public static bool Button(string label, params GUILayoutOption[] options) => false;
        public static bool Toggle(bool value,string label,string style)=>value;
        public static bool Toggle(bool value,string label)=>value;
        public static Vector2 BeginScrollView(Vector2 value,params GUILayoutOption[] options)=>value;
        public static void EndScrollView() { }
        public static string TextField(string value)=>value;
        public static string TextArea(string value,params GUILayoutOption[] options)=>value;
        public static int Toolbar(int selected,string[] names)=>selected;
        public static float HorizontalSlider(float current,float min,float max)=>current;
    }
    public static class Application
    {
        public static string dataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SinbinderMapSmoke","Assets");
    }
    public static class JsonUtility
    {
        private static readonly System.Text.Json.JsonSerializerOptions Settings = new()
        { IncludeFields = true, WriteIndented = true };
        public static T FromJson<T>(string source)=>System.Text.Json.JsonSerializer.Deserialize<T>(source,Settings);
        public static string ToJson(object data,bool pretty = false)=>System.Text.Json.JsonSerializer.Serialize(data,data.GetType(),Settings);
    }
    public static class Debug
    {
        public static void LogWarning(string text) { }
        public static void Log(string text) { }
        public static void LogError(string text) { }
    }
}
namespace UnityEngine.SceneManagement
{
    public static class SceneManager
    {
        public static int LastLoadedScene = -1;
        public static void LoadScene(int index) { LastLoadedScene = index; }
    }
}
namespace Sinbinder.Core
{
    public static class Loc
    {
        public static string T(string value)=>value;
        public static string N(string value)=>value;
        public static string F(string format,params object[] args)=>string.Format(format,args);
    }
}
namespace Sinbinder.Gameplay
{
    public class RTS_Camera:UnityEngine.Behaviour { }
    public static class MapLoader
    {
        public static UnityEngine.GameObject Prefab(string name)
            => string.IsNullOrWhiteSpace(name) ? null : new UnityEngine.GameObject(name);
    }
    public static class Gaze
    {
        public static float Miss(UnityEngine.Vector3 p,float radius)=>0f;
    }
    public static class SinbinderPlayer
    {
        public static bool Exists;
        public static UnityEngine.Vector3 Where;
    }
    public static class CampFocus
    {
        public static float GroundDistance(UnityEngine.Vector3 a,UnityEngine.Vector3 b)
            => UnityEngine.Vector2.Distance(new UnityEngine.Vector2(a.x,a.z),
                                              new UnityEngine.Vector2(b.x,b.z));
    }
    public static class Herald
    {
        public static readonly System.Collections.Generic.List<string> Lines = new();
        public static void Line(string message)=>Lines.Add(message);
    }
}
