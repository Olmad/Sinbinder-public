// Test-only UnityEditor API doubles. The importer itself is compiled unmodified.
// No actual Unity asset import occurs in this .NET job.
using System;
using UnityEngine;
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string path) { }
    }
    public enum MessageType { None, Info, Error }
    public enum ImportAssetOptions { ForceSynchronousImport }
    public class EditorWindow : UnityEngine.Object
    {
        public Vector2 minSize;
        public static T GetWindow<T>(string name) where T : EditorWindow, new() => new T();
    }
    public static class EditorStyles
    {
        public static GUIStyle boldLabel = new GUIStyle();
    }
    public static class EditorGUILayout
    {
        public static void LabelField(string label, GUIStyle style = null) { }
        public static void HelpBox(string text, MessageType type) { }
        public static string TextField(string label, string input) => input;
        public static UnityEngine.Object ObjectField(string label,
            UnityEngine.Object source, Type type, bool allowSceneObjects) => source;
        public static bool Toggle(string label, bool value) => value;
    }
    public static class EditorUtility
    {
        public static string OpenFilePanel(string title, string directory, string extension) => "";
        public static bool DisplayDialog(string title, string message, string ok) => true;
    }
    public static class AssetDatabase
    {
        public static void Refresh() { }
        public static UnityEngine.Object LoadMainAssetAtPath(string path) => null;
        public static void ImportAsset(string path, ImportAssetOptions options) { }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void SaveAssets() { }
        public static void CreateAsset(UnityEngine.Object obj, string path) { }
    }
    public static class PrefabUtility
    {
        public static UnityEngine.Object InstantiatePrefab(GameObject source) => new GameObject(source.name);
        public static GameObject SaveAsPrefabAsset(GameObject source,string path) => source;
    }
}
