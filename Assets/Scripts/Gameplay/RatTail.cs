// Assets/Scripts/Gameplay/RatTail.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Хвост крысы — трубка из колец, которая гнётся волной. Жёсткая модель
    /// хвоста из пакета v306 поворачивалась целиком от корня и махала палкой
    /// (05.11, переделка «под крыс»). Вторая половина хвоста лежит на земле,
    /// как у настоящей крысы; на ходу волна шире и чаще.
    /// Превью рисует тот же хвост той же формулой: Tools/rats/generate_rat_models.py.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RatTail : MonoBehaviour
    {
        private const int Rings = 14;
        private const int Sides = 6;

        /// <summary>Идёт ли крыса: хвост метёт шире.</summary>
        public bool Lively { get; set; }

        private Mesh _mesh;
        private Vector3[] _verts;
        private Vector3[] _normals;
        private readonly Vector3[] _spine = new Vector3[Rings];
        private float _length, _rootRadius, _tipRadius, _height, _phase, _swing;

        /// <summary>
        /// Вырастить хвост. Всё — в единицах родителя: длина, толщина у корня
        /// и на конце, высота корня над землёй.
        /// </summary>
        public static RatTail Grow(Transform parent, Vector3 root, float length, float rootRadius,
                                   float tipRadius, float height, Material material, float phase)
        {
            var go = new GameObject("Хвост");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = root;
            var tail = go.AddComponent<RatTail>();
            tail._length = length;
            tail._rootRadius = rootRadius;
            tail._tipRadius = tipRadius;
            tail._height = height;
            tail._phase = phase;
            tail._swing = 0.06f * length;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            tail.Build();
            return tail;
        }

        private void Build()
        {
            _mesh = new Mesh { name = "Хвост крысы" };
            _mesh.MarkDynamic();
            _verts = new Vector3[Rings * Sides + 1];
            _normals = new Vector3[_verts.Length];

            var tris = new List<int>();
            for (int i = 0; i < Rings - 1; i++)
                for (int j = 0; j < Sides; j++)
                {
                    int a = i * Sides + j, b = i * Sides + (j + 1) % Sides;
                    int c = a + Sides, d = b + Sides;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            int tip = Rings * Sides;
            for (int j = 0; j < Sides; j++)
            {
                int a = (Rings - 1) * Sides + j, b = (Rings - 1) * Sides + (j + 1) % Sides;
                tris.Add(a); tris.Add(tip); tris.Add(b);
            }

            Shape(0f);
            _mesh.vertices = _verts;
            _mesh.normals = _normals;
            _mesh.SetTriangles(tris, 0);
            // Границы с запасом на размах: пересчитывать их каждый кадр незачем.
            _mesh.bounds = new Bounds(new Vector3(0f, -_height * 0.5f, -_length * 0.5f),
                                      new Vector3(_length * 0.7f, _height + _rootRadius * 2f, _length * 1.1f));
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void LateUpdate()
        {
            if (_mesh == null) return;
            // Размах и частота тянутся к нужным плавно: хвост не дёргается,
            // когда крыса трогается или встаёт.
            float want = (Lively ? 0.16f : 0.06f) * _length;
            _swing = Mathf.MoveTowards(_swing, want, Time.deltaTime * _length * 0.4f);
            Shape(Time.time * (Lively ? 7f : 2.2f) + _phase);
            _mesh.vertices = _verts;
            _mesh.normals = _normals;
        }

        private void Shape(float wave)
        {
            for (int i = 0; i < Rings; i++)
            {
                float s = i / (Rings - 1f);
                // Корень — у крупа, конец — на земле; волна сильнее к концу.
                float x = _swing * Mathf.Pow(s, 1.3f) * Mathf.Sin(wave - s * 4.5f);
                float y = -_height * (1f - (1f - s) * (1f - s)) + _tipRadius * s;
                _spine[i] = new Vector3(x, y, -_length * s);
            }

            for (int i = 0; i < Rings; i++)
            {
                var along = _spine[Mathf.Min(Rings - 1, i + 1)] - _spine[Mathf.Max(0, i - 1)];
                along.Normalize();
                var side = Vector3.Cross(along, Vector3.up).normalized;
                var up = Vector3.Cross(side, along);
                float s = i / (Rings - 1f);
                float r = Mathf.Lerp(_rootRadius, _tipRadius, Mathf.Pow(s, 0.8f));
                for (int j = 0; j < Sides; j++)
                {
                    float a = j * Mathf.PI * 2f / Sides;
                    var n = Mathf.Cos(a) * side + Mathf.Sin(a) * up;
                    _verts[i * Sides + j] = _spine[i] + n * r;
                    _normals[i * Sides + j] = n;
                }
            }
            var last = _spine[Rings - 1] - _spine[Rings - 2];
            _verts[Rings * Sides] = _spine[Rings - 1] + last.normalized * _tipRadius;
            _normals[Rings * Sides] = last.normalized;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
