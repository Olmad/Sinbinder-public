// Assets/Scripts/Gameplay/EmberBed.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Угли и пепел под костром (КТ1 п. 3: «под костром угли и пепел»). Огонь стоял на чистой земле: пламя
    /// и поленья были, а того, что остаётся от горевшего, — нет, и сверху костёр читался наклейкой.
    ///
    /// Пепел — тёмное пятно с рваным краем шире огня; угли — россыпь мелких граней на нём, каждая тлеет
    /// своим медленным дыханием. Раскладка и дыхание — по формуле (золотой угол), не жребием. Материал —
    /// Sprites/Default, как у звёзд: свет сцены ему не нужен, угли светят сами. После полена
    /// (<see cref="CampfireStoke.Flare"/>) угли разгораются вместе с огнём.
    ///
    /// Ставит себя сам под «Костёр».
    /// </summary>
    public class EmberBed : MonoBehaviour
    {
        private const int Coals = 54;
        private const float CoalRadius = 0.55f;
        private const float AshRadius = 0.95f;

        private Mesh _mesh;
        private Color[] _colors;
        private Color[] _base;
        private float[] _phase, _speed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void First()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Install();
        }

        private static void OnScene(Scene scene, LoadSceneMode mode) => Install();

        private static void Install()
        {
            if (Object.FindFirstObjectByType<EmberBed>() != null) return;
            var fire = GameObject.Find("Костёр");
            if (fire == null) return;
            var go = new GameObject("Угли и пепел");
            go.transform.SetParent(fire.transform, false);
            go.AddComponent<EmberBed>();
        }

        void Start()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) { enabled = false; return; }

            // «Костёр» стоит на земле (DemoSceneBuilder.Campfire): его точка — и есть земля под огнём.
            // Луч сверху ловил бы поленья модели.
            Ash(shader);
            Coal(shader);
        }

        private void Ash(Shader shader)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Пепел";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.012f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * AshRadius * 2f;
            var mat = new Material(shader) { name = "Ash", mainTexture = Patch(64) };
            mat.color = new Color(0.10f, 0.095f, 0.09f, 0.92f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void Coal(Shader shader)
        {
            var verts = new Vector3[Coals * 4];
            var tris = new int[Coals * 6];
            _colors = new Color[Coals * 4];
            _base = new Color[Coals];
            _phase = new float[Coals];
            _speed = new float[Coals];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < Coals; i++)
            {
                // Гуще к середине: радиус — корень номера, азимут — золотым углом.
                float k = Mathf.Sqrt((i + 0.5f) / Coals);
                float a = i * golden;
                var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * CoalRadius * k + Vector3.up * (0.02f + 0.004f * (i % 3));
                float s = 0.035f + 0.035f * ((i * 0.618f) % 1f);
                float turn = i * 1.7f;
                var u = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * s;
                var v = new Vector3(-u.z, 0f, u.x) * (0.6f + 0.4f * ((i * 0.382f) % 1f));
                verts[i * 4 + 0] = c + u;
                verts[i * 4 + 1] = c + v;
                verts[i * 4 + 2] = c - u * 0.8f;
                verts[i * 4 + 3] = c - v * 1.1f;
                tris[i * 6 + 0] = i * 4; tris[i * 6 + 1] = i * 4 + 2; tris[i * 6 + 2] = i * 4 + 1;
                tris[i * 6 + 3] = i * 4; tris[i * 6 + 4] = i * 4 + 3; tris[i * 6 + 5] = i * 4 + 2;

                // У середины — жар жёлто-оранжевый, к краю — багровый и тусклый; каждый седьмой — уже чёрный.
                float heat = 1f - k * 0.75f;
                _base[i] = i % 7 == 3 ? new Color(0.06f, 0.04f, 0.035f)
                         : Color.Lerp(new Color(0.55f, 0.08f, 0.02f), new Color(1f, 0.55f, 0.16f), heat);
                _phase[i] = (i * 0.618f) % 1f * 6.283f;
                _speed[i] = 0.5f + 0.9f * ((i * 0.414f) % 1f);
                for (int j = 0; j < 4; j++) _colors[i * 4 + j] = _base[i];
            }

            var go = new GameObject("Угли", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            _mesh = new Mesh { name = "Угли", vertices = verts, triangles = tris, colors = _colors };
            _mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = new Material(shader) { name = "Coals" };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void Update()
        {
            if (_mesh == null) return;
            // Тлеют по реальному времени, как огонь; полено раздувает жар.
            float t = Time.unscaledTime;
            float flare = 1f + 0.6f * CampfireStoke.Flare;
            for (int i = 0; i < Coals; i++)
            {
                float breath = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * _speed[i] + _phase[i]));
                var c = _base[i] * Mathf.Min(1.4f, breath * flare);
                c.a = 1f;
                for (int j = 0; j < 4; j++) _colors[i * 4 + j] = c;
            }
            _mesh.colors = _colors;
        }

        /// <summary>Пятно пепла: плотное в середине, рваный край — шумом Перлина.</summary>
        private static Texture2D Patch(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Ash" };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float n = Mathf.PerlinNoise(x * 0.15f + 7f, y * 0.15f);
                float a = Mathf.Clamp01((1f - d) * 2.2f + (n - 0.5f) * 0.9f);
                float grain = 0.8f + 0.2f * Mathf.PerlinNoise(x * 0.6f, y * 0.6f + 11f);
                t.SetPixel(x, y, new Color(grain, grain, grain, a));
            }
            t.Apply();
            return t;
        }
    }
}
