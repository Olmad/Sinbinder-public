// Assets/Scripts/Gameplay/MoonDisc.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Сама луна (автор, 27.10: «вероятно, нам не хватает луны — лучи есть, а источника нет»).
    ///
    /// Неба в сцене нет — фон цвета тумана, а туман густой (0,018): луна в километре
    /// растворилась бы в нём. Поэтому диск стоит не далеко, а <b>на привязи у камеры</b>:
    /// в 60 м по направлению на луну («Холодный свет» — тот же, что ведёт лучи
    /// <see cref="MoonShafts"/>), и с камерой переезжает. Шейдер тумана не берёт, а кроны
    /// ближе 60 м её закрывают — луна видна в просветах. Сверху (камера почти отвесно) её не видно — небо видно
    /// только глазами Греховода.
    ///
    /// Ставит себя сам в сцене с «Холодным светом»; сборщик сцен не трогаем.
    /// </summary>
    public class MoonDisc : MonoBehaviour
    {
        private const float Distance = 60f;
        private const float Size = 4.5f;        // ~4°: луна крупнее настоящей — так её читают
        private const float HaloSize = 14f;

        private Transform _key;
        private Transform _disc, _halo;

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
            if (Object.FindFirstObjectByType<MoonDisc>() != null) return;
            var key = GameObject.Find("Холодный свет");
            if (key == null) return;
            var go = new GameObject("Луна");
            var moon = go.AddComponent<MoonDisc>();
            moon._key = key.transform;
        }

        void Start()
        {
            // Круг — формой, а не картинкой на квадрате: прозрачность края у этого материала
            // в сцене не работала, и луна вышла квадратом (кадр прогона 27.10). Ореол — два
            // диска побольше и темнее позади: на тёмном небе это и читается свечением.
            _disc = Quad("Диск", Size, Disc(128, 0.46f, 0.43f), new Color(0.92f, 0.94f, 1.0f, 1f));
            _halo = Quad("Ореол", Size * 1.35f, null, new Color(0.16f, 0.19f, 0.30f, 1f));
            _haloFar = Quad("Ореол дальний", Size * 1.9f, null, new Color(0.09f, 0.10f, 0.17f, 1f));
        }

        private Transform _haloFar;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || _key == null) return;

            var toMoon = -_key.forward;
            var at = cam.transform.position + toMoon * Distance;
            var face = Quaternion.LookRotation(-toMoon, Vector3.up);
            _disc.SetPositionAndRotation(at, face);
            _halo.SetPositionAndRotation(at + toMoon * 0.3f, face);
            _haloFar.SetPositionAndRotation(at + toMoon * 0.6f, face);
        }

        private Transform Quad(string name, float size, Texture2D tex, Color tint)
        {
            var q = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            q.GetComponent<MeshFilter>().sharedMesh = Circle(48);
            q.transform.SetParent(transform, false);
            q.transform.localScale = Vector3.one * size;

            // Sprites/Default — как у лучей (MoonShafts): есть в сборке, прозрачный, тумана не берёт.
            var mat = new Material(Shader.Find("Sprites/Default")) { name = "Moon", mainTexture = tex, color = tint };
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;

            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q.transform;
        }

        /// <summary>Круг диаметром 1 в плоскости XY, лицом к −Z; UV — по кругу, как у квадрата.</summary>
        private static Mesh Circle(int segs)
        {
            var v = new Vector3[segs + 1];
            var uv = new Vector2[segs + 1];
            var tri = new int[segs * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segs; i++)
            {
                float a = 2f * Mathf.PI * i / segs;
                v[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
                tri[i * 3] = 0;
                tri[i * 3 + 1] = (i + 1) % segs + 1;
                tri[i * 3 + 2] = i + 1;
            }
            var m = new Mesh { name = "Moon Circle", vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Круг с мягким краем: до <paramref name="solid"/> радиуса — полный, к <paramref name="edge"/> тает.
        /// У диска — лёгкие тёмные «моря» по формуле, без жребия.
        /// </summary>
        private static Texture2D Disc(int n, float edge, float solid)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = solid > 0f
                    ? 1f - Mathf.SmoothStep(solid, edge, r)
                    : Mathf.Pow(Mathf.Clamp01(1f - r / edge), 2.2f);
                float shade = 1f;
                if (solid > 0f)
                {
                    // Моря — три размытых пятна в левой верхней четверти, как у настоящей.
                    shade -= 0.16f * Mathf.Exp(-((u + 0.12f) * (u + 0.12f) + (v - 0.10f) * (v - 0.10f)) / 0.006f);
                    shade -= 0.12f * Mathf.Exp(-((u - 0.05f) * (u - 0.05f) + (v - 0.15f) * (v - 0.15f)) / 0.004f);
                    shade -= 0.10f * Mathf.Exp(-((u + 0.02f) * (u + 0.02f) + (v + 0.10f) * (v + 0.10f)) / 0.008f);
                }
                px[y * n + x] = new Color(shade, shade, shade, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
