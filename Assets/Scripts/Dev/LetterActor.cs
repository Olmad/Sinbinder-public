// Assets/Scripts/Dev/LetterActor.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Dev
{
    /// <summary>
    /// Буква-актёр для роликов: A — ведущий с микрофоном, O — оператор
    /// (буква и есть объектив), S — звукач с удочкой. Замысел автора,
    /// 24 сентября: «буквы A-O-S ходят по сценам и общаются
    /// с персонажами».
    ///
    /// Собирается кодом, без моделей: буква — трубка по контуру, глаза —
    /// два шара. Всё движение считается в реальном времени, поэтому
    /// буквы ходят и говорят и в замершем мире.
    ///
    /// Позы — без жребия: одна и та же поза всегда двигается одинаково,
    /// иначе второй дубль не совпадёт с первым при монтаже.
    /// </summary>
    public class LetterActor : MonoBehaviour
    {
        public enum Motion { Idle, Talk, Shout, Point, Jump, Bow, Walk }

        /// <summary>Поза словом — для строки на экране съёмки.</summary>
        public static string Word(Motion motion)
        {
            switch (motion)
            {
                case Motion.Talk:  return "говорит";
                case Motion.Shout: return "кричит";
                case Motion.Point: return "показывает";
                case Motion.Jump:  return "прыгает";
                case Motion.Bow:   return "кланяется";
                case Motion.Walk:  return "шагает";
                default:           return "стоит";
            }
        }

        /// <summary>Кто это — для строки на экране съёмки.</summary>
        public string Title { get; private set; }

        public Motion Now { get; private set; } = Motion.Idle;

        public bool Walking => _walking;

        private const float Radius = 0.085f;
        private const float Speed = 1.4f;

        private Transform _body;
        private Vector3 _target;
        private bool _walking;
        private float _clock;

        // ──────────────────────────────────
        // Сборка
        // ──────────────────────────────────

        /// <summary>Поставить три буквы в ряд перед точкой, лицом к ней.</summary>
        public static List<LetterActor> Troupe(Vector3 centre, Vector3 facing)
        {
            var list = new List<LetterActor>();
            var right = Vector3.Cross(Vector3.up, facing).normalized;

            list.Add(Build('A', centre - right * 1.3f, facing));
            list.Add(Build('O', centre, facing));
            list.Add(Build('S', centre + right * 1.3f, facing));
            return list;
        }

        public static LetterActor Build(char letter, Vector3 at, Vector3 facing)
        {
            var root = new GameObject($"Буква {letter}");
            root.transform.position = Ground(at);
            if (facing.sqrMagnitude > 0.001f)
                root.transform.rotation = Quaternion.LookRotation(Flat(facing), Vector3.up);

            var actor = root.AddComponent<LetterActor>();
            actor._body = new GameObject("Тело").transform;
            actor._body.SetParent(root.transform, false);

            var pick = root.AddComponent<CapsuleCollider>();
            pick.center = new Vector3(0f, 0.72f, 0f);
            pick.height = 1.5f;
            pick.radius = 0.5f;

            switch (letter)
            {
                case 'A':
                    actor.Title = "A — ведущий";
                    actor.ShapeA(Paint(new Color(0.78f, 0.13f, 0.15f)));
                    break;
                case 'O':
                    actor.Title = "O — оператор";
                    actor.ShapeO(Paint(new Color(0.95f, 0.72f, 0.18f)));
                    break;
                default:
                    actor.Title = "S — звук";
                    actor.ShapeS(Paint(new Color(0.12f, 0.56f, 0.64f)));
                    break;
            }

            actor._target = root.transform.position;
            return actor;
        }

        // Буквы строятся лицом к +Z: зритель смотрит на них с +Z, и справа
        // у него −X. Поэтому x в контуре — «как видит зритель», а в мир
        // идёт со знаком минус: иначе S читалась бы зеркально.

        private void ShapeA(Material paint)
        {
            float top = 1.38f, foot = Radius;
            Stroke(paint, V(-0.45f, foot), V(0f, top));
            Stroke(paint, V(0.45f, foot), V(0f, top));
            Stroke(paint, V(-0.24f, 0.56f), V(0.24f, 0.56f));
            Ball(paint, V(0f, top), Radius * 1.25f);

            Eyes(V(-0.15f, 0.93f), V(0.15f, 0.93f));

            // Ручной микрофон у перекладины, головкой к зрителю.
            var dark = Paint(new Color(0.12f, 0.12f, 0.13f));
            Stroke(dark, V(0.30f, 0.62f, 0.10f), V(0.30f, 0.80f, 0.26f), 0.025f);
            Ball(dark, V(0.30f, 0.84f, 0.30f), 0.06f);
        }

        private void ShapeO(Material paint)
        {
            var ring = new List<Vector3>();
            for (int i = 0; i < 36; i++)
            {
                float a = i * Mathf.PI * 2f / 36f;
                ring.Add(V(Mathf.Cos(a) * 0.42f, 0.66f + Mathf.Sin(a) * 0.58f));
            }
            Tube(paint, ring, Radius, closed: true);

            Eyes(V(-0.20f, 1.15f), V(0.20f, 1.15f));

            // Камера на плече: корпус и объектив, смотрящий вперёд.
            var dark = Paint(new Color(0.10f, 0.10f, 0.11f));
            var box = Primitive(PrimitiveType.Cube, dark);
            box.localPosition = V(-0.62f, 0.86f, 0.05f);
            box.localScale = new Vector3(0.26f, 0.24f, 0.44f);
            var lens = Primitive(PrimitiveType.Cylinder, Paint(new Color(0.25f, 0.27f, 0.30f)));
            lens.localPosition = V(-0.62f, 0.86f, 0.33f);
            lens.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lens.localScale = new Vector3(0.16f, 0.07f, 0.16f);
        }

        private void ShapeS(Material paint)
        {
            var path = new List<Vector3>();
            const float r = 0.30f, upper = 1.02f, lower = 0.40f;

            for (int i = 0; i <= 16; i++)
            {
                float deg = Mathf.Lerp(35f, 270f, i / 16f);
                float a = deg * Mathf.Deg2Rad;
                path.Add(V(Mathf.Cos(a) * r, upper + Mathf.Sin(a) * r));
            }
            for (int i = 1; i <= 16; i++)
            {
                float deg = Mathf.Lerp(90f, -145f, i / 16f);
                float a = deg * Mathf.Deg2Rad;
                path.Add(V(Mathf.Cos(a) * r, lower + Mathf.Sin(a) * r));
            }
            Tube(paint, path, Radius, closed: false);
            Ball(paint, path[0], Radius);
            Ball(paint, path[path.Count - 1], Radius);

            Eyes(V(-0.12f, 1.27f), V(0.12f, 1.27f));

            // Удочка с микрофоном над головой, чуть вперёд — в кадр
            // она не лезет, пока звукач держит её ровно.
            var dark = Paint(new Color(0.14f, 0.14f, 0.15f));
            Stroke(dark, V(0.30f, 1.05f, 0f), V(0.55f, 1.95f, 0.45f), 0.022f);
            var fuzz = Primitive(PrimitiveType.Sphere, Paint(new Color(0.30f, 0.30f, 0.32f)));
            fuzz.localPosition = V(0.55f, 1.95f, 0.62f);
            fuzz.localScale = new Vector3(0.14f, 0.14f, 0.34f);
        }

        private void Eyes(Vector3 left, Vector3 right)
        {
            var white = Paint(Color.white);
            var black = Paint(new Color(0.03f, 0.03f, 0.03f));

            foreach (var at in new[] { left, right })
            {
                var eye = at + new Vector3(0f, 0f, Radius * 0.7f);
                Ball(white, eye, 0.075f);
                Ball(black, eye + new Vector3(0f, 0f, 0.055f), 0.038f);
            }
        }

        // ──────────────────────────────────
        // Геометрия
        // ──────────────────────────────────

        private static Vector3 V(float x, float y, float z = 0f) => new Vector3(-x, y, z);

        private void Stroke(Material paint, Vector3 from, Vector3 to, float radius = Radius)
        {
            var path = new List<Vector3>();
            for (int i = 0; i <= 6; i++) path.Add(Vector3.Lerp(from, to, i / 6f));
            Tube(paint, path, radius, closed: false);
            Ball(paint, from, radius);
            Ball(paint, to, radius);
        }

        private void Ball(Material paint, Vector3 at, float radius)
        {
            var ball = Primitive(PrimitiveType.Sphere, paint);
            ball.localPosition = at;
            ball.localScale = Vector3.one * radius * 2f;
        }

        private Transform Primitive(PrimitiveType type, Material paint)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(_body, false);

            // Ловит щелчок капсула корня; колайдеры частей только мешали бы.
            var hit = go.GetComponent<Collider>();
            if (hit != null) Destroy(hit);

            go.GetComponent<Renderer>().sharedMaterial = paint;
            return go.transform;
        }

        /// <summary>
        /// Трубка по ломаной в плоскости буквы. Кольца стоят поперёк пути;
        /// обход треугольников — по часовой, если смотреть снаружи:
        /// так Unity считает лицевую сторону.
        /// </summary>
        private void Tube(Material paint, IList<Vector3> path, float radius, bool closed)
        {
            const int sides = 12;
            int n = path.Count;
            var vertices = new List<Vector3>(n * sides);
            var normals = new List<Vector3>(n * sides);
            var triangles = new List<int>();

            for (int i = 0; i < n; i++)
            {
                Vector3 prev = closed ? path[(i - 1 + n) % n] : path[Mathf.Max(i - 1, 0)];
                Vector3 next = closed ? path[(i + 1) % n] : path[Mathf.Min(i + 1, n - 1)];
                Vector3 along = (next - prev).normalized;

                // Путь идёт в плоскости буквы, поэтому поперёк — это «вбок
                // в плоскости» и «вперёд, к зрителю». Для удочки и микрофона,
                // что выходят из плоскости, берём другую опору.
                Vector3 up = Mathf.Abs(Vector3.Dot(along, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
                Vector3 side = Vector3.Cross(along, up).normalized;
                Vector3 depth = Vector3.Cross(side, along).normalized;

                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    Vector3 normal = side * Mathf.Cos(a) + depth * Mathf.Sin(a);
                    vertices.Add(path[i] + normal * radius);
                    normals.Add(normal);
                }
            }

            int rings = closed ? n : n - 1;
            for (int i = 0; i < rings; i++)
            {
                int a0 = i * sides;
                int b0 = ((i + 1) % n) * sides;
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    triangles.Add(a0 + s); triangles.Add(b0 + s); triangles.Add(a0 + s1);
                    triangles.Add(a0 + s1); triangles.Add(b0 + s); triangles.Add(b0 + s1);
                }
            }

            var mesh = new Mesh { name = "Трубка буквы" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var go = new GameObject("Штрих");
            go.transform.SetParent(_body, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = paint;
        }

        /// <summary>
        /// Краска буквы. Основа — материал, который конвейер рендера сам
        /// даёт примитиву: так не нужно искать шейдер по имени, который
        /// в собранную игру мог не попасть.
        /// </summary>
        private static Material Paint(Color color)
        {
            if (_base == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _base = probe.GetComponent<Renderer>().sharedMaterial;
                Destroy(probe);
            }

            return new Material(_base) { color = color, name = "Краска буквы" };
        }

        private static Material _base;

        // ──────────────────────────────────
        // Движение
        // ──────────────────────────────────

        public void Set(Motion motion)
        {
            Now = motion;
            _clock = 0f;
        }

        public void Cycle(int step)
        {
            int count = System.Enum.GetValues(typeof(Motion)).Length;
            Set((Motion)(((int)Now + step + count) % count));
        }

        /// <summary>Пойти к точке по прямой, прыжками.</summary>
        public void WalkTo(Vector3 point)
        {
            _target = Ground(point);
            _walking = true;
        }

        /// <summary>Встать в точку сразу — для расстановки перед дублем.</summary>
        public void PlaceAt(Vector3 point)
        {
            _walking = false;
            transform.position = Ground(point);
            _target = transform.position;
        }

        public void Turn(float degrees) => transform.Rotate(0f, degrees, 0f, Space.World);

        public void Face(Vector3 point)
        {
            var to = Flat(point - transform.position);
            if (to.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _clock += dt;

            if (_walking) Step(dt);

            Animate(_walking ? Motion.Walk : Now, _clock);
        }

        private void Step(float dt)
        {
            var to = Flat(_target - transform.position);
            float left = to.magnitude;

            if (left < 0.05f)
            {
                _walking = false;
                return;
            }

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to / left, Vector3.up), 10f * dt);

            var next = transform.position + to / left * Mathf.Min(left, Speed * dt);
            transform.position = Ground(next);
        }

        /// <summary>Поза как функция времени — одинаковая в каждом дубле.</summary>
        private void Animate(Motion motion, float t)
        {
            Vector3 lift = Vector3.zero;
            Vector3 tilt = Vector3.zero;
            Vector3 size = Vector3.one;

            switch (motion)
            {
                case Motion.Idle:
                    size.y = 1f + 0.02f * Mathf.Sin(t * 2f);
                    tilt.z = 1.5f * Mathf.Sin(t * 0.9f);
                    break;

                case Motion.Talk:
                    tilt.z = 5f * Mathf.Sin(t * 9f);
                    size = Vector3.one * (1f + 0.04f * Mathf.Abs(Mathf.Sin(t * 9f)));
                    break;

                case Motion.Shout:
                    // Рывок вверх в начале, потом дрожь: «СТОЙТЕ!»
                    float burst = Mathf.Clamp01(1f - t * 3f);
                    size = Vector3.one * (1f + 0.18f * burst + 0.05f);
                    tilt.z = 3f * Mathf.Sin(t * 37f);
                    tilt.x = -6f;
                    break;

                case Motion.Point:
                    tilt.z = -14f + 2f * Mathf.Sin(t * 3f);
                    lift.y = 0.03f * Mathf.Abs(Mathf.Sin(t * 3f));
                    break;

                case Motion.Jump:
                    float hop = Mathf.Max(0f, Mathf.Sin(t * 4.5f));
                    lift.y = 0.35f * hop;
                    size.y = hop > 0.05f ? 1.08f : 0.9f;
                    break;

                case Motion.Bow:
                    tilt.x = 28f * (0.5f - 0.5f * Mathf.Cos(t * 1.6f));
                    break;

                case Motion.Walk:
                    float stride = Mathf.Abs(Mathf.Sin(t * 8f));
                    lift.y = 0.12f * stride;
                    tilt.x = 6f;
                    size.y = 0.94f + 0.08f * stride;
                    break;
            }

            _body.localPosition = lift;
            _body.localRotation = Quaternion.Euler(tilt);
            _body.localScale = size;
        }

        // ──────────────────────────────────
        // Земля
        // ──────────────────────────────────

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Опустить точку на землю под ней. Земли нет — оставить как есть.</summary>
        public static Vector3 Ground(Vector3 point)
        {
            var from = point + Vector3.up * 30f;
            var hits = Physics.RaycastAll(from, Vector3.down, 80f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                // Себе подобных и воинов за землю не считаем.
                if (hit.collider.GetComponentInParent<LetterActor>() != null) continue;
                if (hit.collider.GetComponentInParent<Gameplay.Warrior>() != null) continue;
                if (hit.point.y > point.y + 3f) continue;
                return hit.point;
            }
            return point;
        }
    }
}
