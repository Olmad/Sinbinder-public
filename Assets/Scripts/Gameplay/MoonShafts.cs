// Assets/Scripts/Gameplay/MoonShafts.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Лучи луны сквозь кроны бора. Автор, 26 октября, спросил о трассировке лучей
    /// для луны; в URP её нет (только HDRP и RTX), и переход не окупается — поэтому
    /// обманка: бледные полосы света, косо падающие между сосен по направлению
    /// холодного ключевого света («Холодный свет»), как в туманном лесу.
    ///
    /// Степень — своя настройка «Лунный свет» (<see cref="GraphicsOptions.Moon"/>),
    /// отдельно от наборов графики: низкий — без лучей, средний — редкие,
    /// высокий — гуще, и они медленно дышат и колышутся.
    ///
    /// Дёшево: плоские полосы с мягкой прозрачной текстурой, повёрнутые вокруг
    /// своей оси к камере; без теней, без света. Ставит себя сам в сцену с костром
    /// (<see cref="CampOpening"/>) — сцены пересобирать не нужно.
    /// </summary>
    public class MoonShafts : MonoBehaviour
    {
        private const float Inner = 15f, Outer = 30f;   // кольцо бора за палатками, метров
        private const float Length = 15f;                // от крон до земли, метров
        private static readonly Color Moonlight = new Color(0.66f, 0.76f, 1.00f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Place();
        }

        private static void OnScene(Scene s, LoadSceneMode m) => Place();

        private static void Place()
        {
            if (Object.FindFirstObjectByType<CampOpening>() == null) return;
            if (Object.FindFirstObjectByType<MoonShafts>() != null) return;
            new GameObject("Лучи луны").AddComponent<MoonShafts>();
        }

        private Transform[] _shafts = new Transform[0];
        private Material _material;
        private GraphicsOptions.MoonLevel _built = (GraphicsOptions.MoonLevel)(-1);
        private Vector3 _down;

        void Update()
        {
            if (_built != GraphicsOptions.Moon) Build(GraphicsOptions.Moon);
            if (_shafts.Length == 0) return;

            var cam = Camera.main;
            if (cam == null) return;

            bool high = GraphicsOptions.Moon == GraphicsOptions.MoonLevel.High;
            float t = Time.time;
            for (int i = 0; i < _shafts.Length; i++)
            {
                var s = _shafts[i];
                if (s == null) continue;
                // Вокруг своей оси — лицом к камере: полоса не видна ребром.
                var axis = _down;
                var toCam = cam.transform.position - s.position;
                var face = Vector3.ProjectOnPlane(toCam, axis);
                if (face.sqrMagnitude > 1e-4f) s.rotation = Quaternion.LookRotation(-face, -axis);

                if (high)
                {
                    // Дышит и колышется — медленно, у каждого своя фаза.
                    float breathe = 0.75f + 0.25f * Mathf.Sin(t * 0.35f + i * 1.7f);
                    var r = s.GetComponent<Renderer>();
                    if (r != null) r.material.color = new Color(Moonlight.r, Moonlight.g, Moonlight.b, Alpha * breathe);
                    s.position += Vector3.Cross(axis, Vector3.up).normalized * (Mathf.Sin(t * 0.2f + i) * 0.002f);
                }
            }
        }

        private float Alpha => GraphicsOptions.Moon == GraphicsOptions.MoonLevel.High ? 0.16f : 0.12f;

        private void Build(GraphicsOptions.MoonLevel level)
        {
            foreach (var s in _shafts) if (s != null) Destroy(s.gameObject);
            _built = level;

            int count = level switch
            {
                GraphicsOptions.MoonLevel.Low => 0,
                GraphicsOptions.MoonLevel.High => 16,
                _ => 8,
            };
            if (count == 0) { _shafts = new Transform[0]; return; }

            var key = GameObject.Find("Холодный свет");
            _down = key != null ? key.transform.forward : Quaternion.Euler(20f, 152f, 0f) * Vector3.forward;
            var fire = Object.FindFirstObjectByType<CampOpening>();
            var centre = fire != null ? fire.transform.position : Vector3.zero;

            if (_material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) { _shafts = new Transform[0]; return; }
                _material = new Material(shader) { mainTexture = Ray(), name = "Луч луны" };
            }
            var mesh = Quad();

            _shafts = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                // По кругу и по радиусу — по номеру, без жребия: тот же бор, те же лучи.
                float a = (i * 360f / count + (i * 37 % 23)) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(Inner, Outer, (i * 0.618f) % 1f);
                var ground = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;

                var go = new GameObject("Луч");
                go.transform.SetParent(transform, false);
                go.transform.position = ground - _down * (Length * 0.5f);
                float width = 1.4f + (i % 3) * 0.6f;
                go.transform.localScale = new Vector3(width, Length, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var rend = go.AddComponent<MeshRenderer>();
                rend.sharedMaterial = _material;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                rend.material.color = new Color(Moonlight.r, Moonlight.g, Moonlight.b, Alpha);
                _shafts[i] = go.transform;
            }
            Debug.Log($"[ЛУНА] лучей сквозь кроны: {count} (степень «{level}»)");
        }

        /// <summary>Квадрат 1×1, ось Y — вдоль луча, середина в нуле.</summary>
        private static Mesh Quad()
        {
            var m = new Mesh { name = "Луч" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                                 new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };   // с обеих сторон
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Мягкая полоса: гаснет к краям поперёк и к обоим концам вдоль.</summary>
        private static Texture2D Ray()
        {
            const int w = 32, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Луч" };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = 1f - Mathf.Abs(x / (w - 1f) * 2f - 1f);
                float along = Mathf.Sin(Mathf.PI * y / (h - 1f));
                float a = Mathf.SmoothStep(0f, 1f, across) * Mathf.Pow(along, 0.7f);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
