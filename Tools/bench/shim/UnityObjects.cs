// Заглушка объектной модели Unity: GameObject, Transform и компоненты.
// Нужна ровно настолько, насколько её касается логика решений —
// самопроверка движка создаёт объекты, а Facing считает углы.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public enum HideFlags { None = 0, HideAndDontSave = 61 }

    public struct Quaternion
    {
        public Vector3 Forward;
        public static Quaternion identity => new Quaternion { Forward = new Vector3(0, 0, 1) };
        public static Quaternion LookRotation(Vector3 dir) => new Quaternion { Forward = dir.normalized };
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => t >= 0.5f ? b : a;
    }

    public class Transform
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 localScale = new Vector3(1, 1, 1);
        public Vector3 forward => rotation.Forward;
        public GameObject gameObject;
    }

    public class Component
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public string name { get => gameObject.name; set => gameObject.name = value; }
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T[] GetComponents<T>() where T : class => gameObject.GetComponents<T>();
        public T GetComponentInChildren<T>() where T : class => gameObject.GetComponent<T>();
    }

    public class MonoBehaviour : Component { }

    public class GameObject
    {
        public string name;
        public HideFlags hideFlags;
        public readonly Transform transform;
        private readonly List<object> _components = new();

        public GameObject(string n = "GameObject")
        {
            name = n;
            transform = new Transform { gameObject = this };
        }

        public T AddComponent<T>() where T : class, new()
        {
            var c = new T();
            if (c is Component comp) comp.gameObject = this;
            _components.Add(c);
            return c;
        }

        public T GetComponent<T>() where T : class
        {
            foreach (var c in _components) if (c is T t) return t;
            return null;
        }

        public T[] GetComponents<T>() where T : class
        {
            var list = new List<T>();
            foreach (var c in _components) if (c is T t) list.Add(t);
            return list.ToArray();
        }

        public static void DestroyImmediate(object o) { }
        public static void Destroy(object o) { }
    }
}
