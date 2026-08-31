// Assets/UI/OverheadUIPool.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.UI
{
    public static class OverheadUIPool
    {
        private static Queue<GameObject> _pool = new();

        public static GameObject Get()
        {
            if (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                obj.SetActive(true);
                return obj;
            }
            return null;
        }

        public static void Return(GameObject obj)
        {
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }

        public static void Clear()
        {
            foreach (var obj in _pool)
                GameObject.Destroy(obj);
            _pool.Clear();
        }
    }
}
