// Assets/Scripts/Gameplay/StarField.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Звёзды (КТ1 п. 19: «небо и туман — луна, звёзды»). Неба в сцене нет — фон цвета тумана, а туман густой:
    /// звёзды вдали в нём растворились бы. Поэтому, как луна (<see cref="MoonDisc"/>), — купол <b>на привязи у
    /// камеры</b>: в 58 м, едет за ней, не вращается; Sprites/Default тумана не берёт. Сверху (тактическая камера)
    /// неба не видно — звёзды видны глазами Греховода и в низких планах.
    ///
    /// Раскладка — по формуле (золотой угол), не жребием: те же звёзды в каждой партии. Яркость — три величины,
    /// у части — лёгкое мерцание. Одна сетка из крошечных ромбов: квадрат в 0,1° квадратом не читается.
    /// Ставит себя сам в сцене с «Холодным светом» (ночь).
    /// </summary>
    public class StarField : MonoBehaviour
    {
        private const float Radius = 58f;
        private const int Count = 360;
        private const float LowestDeg = 9f;      // ниже — за лесом, всё равно не видно

        private Mesh _mesh;
        private Color[] _colors, _base;
        private float[] _phase;

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
            if (Object.FindFirstObjectByType<StarField>() != null) return;
            if (GameObject.Find("Холодный свет") == null) return;
            new GameObject("Звёзды", typeof(MeshFilter), typeof(MeshRenderer)).AddComponent<StarField>();
        }

        void Start()
        {
            var verts = new Vector3[Count * 4];
            var tris = new int[Count * 6];
            _colors = new Color[Count * 4];
            _base = new Color[Count];
            _phase = new float[Count];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            float lowest = Mathf.Sin(LowestDeg * Mathf.Deg2Rad);
            for (int i = 0; i < Count; i++)
            {
                // Равномерно по куполу выше LowestDeg: высота — по номеру, азимут — золотым углом.
                float y = Mathf.Lerp(lowest, 0.98f, (i + 0.5f) / Count);
                float r = Mathf.Sqrt(1f - y * y);
                float a = i * golden;
                var dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                var at = dir * Radius;
                // Ромб, лицом к центру купола (к камере).
                var right = Vector3.Cross(Vector3.up, dir).normalized;
                if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
                var up = Vector3.Cross(dir, right).normalized;
                int mag = (i * 7) % 11 == 0 ? 2 : (i * 3) % 5 == 0 ? 1 : 0;      // три величины, ярких мало
                float size = mag == 2 ? 0.22f : mag == 1 ? 0.15f : 0.10f;
                verts[i * 4 + 0] = at + up * size;
                verts[i * 4 + 1] = at + right * size;
                verts[i * 4 + 2] = at - up * size;
                verts[i * 4 + 3] = at - right * size;
                tris[i * 6 + 0] = i * 4; tris[i * 6 + 1] = i * 4 + 1; tris[i * 6 + 2] = i * 4 + 2;
                tris[i * 6 + 3] = i * 4; tris[i * 6 + 4] = i * 4 + 2; tris[i * 6 + 5] = i * 4 + 3;
                // Ближе к горизонту — тусклее (дымка), чуть холоднее или теплее — по номеру.
                float bright = (mag == 2 ? 1f : mag == 1 ? 0.75f : 0.5f) * Mathf.Lerp(0.45f, 1f, (y - lowest) / (1f - lowest));
                var tint = (i % 4) switch { 0 => new Color(0.80f, 0.86f, 1f), 1 => new Color(1f, 0.93f, 0.82f), _ => Color.white };
                _base[i] = tint * bright;
                _phase[i] = (i * 0.618f) % 1f * 6.283f;
                for (int k = 0; k < 4; k++) _colors[i * 4 + k] = _base[i];
            }
            _mesh = new Mesh { name = "Звёзды" };
            _mesh.vertices = verts;
            _mesh.triangles = tris;
            _mesh.colors = _colors;
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * Radius * 2.2f);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mat = new Material(Shader.Find("Sprites/Default")) { name = "Stars" };
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 20;   // за луной
            var r2 = GetComponent<MeshRenderer>();
            r2.sharedMaterial = mat;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || _mesh == null) return;
            transform.position = cam.transform.position;
            // Мерцает каждая пятая — едва заметно; по реальному времени, как огонь.
            float t = Time.unscaledTime;
            for (int i = 0; i < Count; i += 5)
            {
                var c = _base[i] * (0.8f + 0.2f * Mathf.Sin(t * 2.3f + _phase[i]));
                for (int k = 0; k < 4; k++) _colors[i * 4 + k] = c;
            }
            _mesh.colors = _colors;
        }
    }
}
