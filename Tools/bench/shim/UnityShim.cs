// Заглушка UnityEngine: ровно то, чего касается логика голосования.
// Нужна, чтобы собрать настоящие модули вне редактора. Ни одна строка
// самих модулей и конфига при этом не меняется — веса и пороги берутся
// из его кода как есть.
using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float Epsilon = 1.401298E-45f;
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Abs(float v) => Math.Abs(v);
        public static float Round(float v) => (float)Math.Round(v, MidpointRounding.AwayFromZero);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float InverseLerp(float a, float b, float v)
            => Math.Abs(b - a) < 1e-6f ? 0f : Clamp01((v - a) / (b - a));
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 1e-5f;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sign(float v) => v >= 0f ? 1f : -1f;
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Floor(float v) => (float)Math.Floor(v);
        public static float Ceil(float v) => (float)Math.Ceiling(v);
        public static float MoveTowards(float a, float b, float d)
            => Math.Abs(b - a) <= d ? b : a + Sign(b - a) * d;
    }

    public static class Debug
    {
        public static bool Mute = true;
        public static void Log(object m)        { if (!Mute) Console.WriteLine(m); }
        public static void LogWarning(object m) { if (!Mute) Console.WriteLine("WARN: " + m); }
        public static void LogError(object m)   { if (!Mute) Console.WriteLine("ERR: " + m); }
        public static void Break() { }
    }

    public class Object { public string name; }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
    }

    [AttributeUsage(AttributeTargets.Field)]      public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.All)]        public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)]        public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)]        public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Class)]      public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }

    /// <summary>
    /// Подмена загрузчика ассетов. Модули берут конфиг через
    /// Resources.Load, и без этой подмены каждый получал бы собственный
    /// запасной экземпляр со значениями по умолчанию — тогда подбор
    /// весов на стенде ничего бы не менял.
    /// </summary>
    public static class Resources
    {
        public static readonly System.Collections.Generic.Dictionary<Type, object> Registry = new();
        public static void Register<T>(T value) where T : class => Registry[typeof(T)] = value;
        public static T Load<T>(string p) where T : class
            => Registry.TryGetValue(typeof(T), out var v) ? (T)v : null;
    }
    public static class Time { public static float time => 0f; public static float deltaTime => 0.016f; }
}
