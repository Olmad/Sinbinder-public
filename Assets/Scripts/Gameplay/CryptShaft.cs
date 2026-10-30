// Assets/Scripts/Gameplay/CryptShaft.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Луч из двери склепа и пыль в нём (КТ1 п. 18: «склеп: луч из двери, пыль»). Зал склепа освещали два факела
    /// у входа и холодный ключ; снаружи в дверь ничего не входило — склеп читался комнатой без улицы.
    ///
    /// Тот же приём, что <see cref="MoonShafts"/>: три мягкие полосы от проёма вниз и внутрь, к алтарю, каждая
    /// лицом к камере вокруг своей оси; в их объёме медленно висит пыль — редкие светлые точки. Раскладка —
    /// по номеру, без жребия. Ставит себя сам туда, где есть «Вход в склеп».
    /// </summary>
    public class CryptShaft : MonoBehaviour
    {
        private static readonly Color Moonlight = new Color(0.70f, 0.78f, 1.00f, 1f);
        private const float Length = 8f;
        private const float Alpha = 0.11f;

        private Transform[] _shafts;
        private Vector3 _axis;

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
            if (Object.FindFirstObjectByType<CryptShaft>() != null) return;
            var gate = GameObject.Find("Вход в склеп");
            if (gate == null) return;
            var go = new GameObject("Луч из двери");
            go.transform.position = gate.transform.position;
            go.AddComponent<CryptShaft>();
        }

        void Start()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) { enabled = false; return; }

            // Из проёма (под перемычкой, ~2,6 м) вниз и внутрь зала — к алтарю, что стоит перед входом.
            var door = transform.position + Vector3.up * 2.6f;
            var inside = new Vector3(0f, 0f, -1f);
            var altar = GameObject.Find("Зал");
            if (altar != null)
            {
                var to = altar.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) inside = to.normalized;
            }
            _axis = (inside * 1.5f + Vector3.down).normalized;

            var mat = new Material(shader) { mainTexture = MoonShafts.Ray(), name = "Луч из двери" };
            var mesh = MoonShafts.Quad();
            var side = Vector3.Cross(Vector3.up, inside).normalized;
            _shafts = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Луч");
                go.transform.SetParent(transform, false);
                go.transform.position = door + side * ((i - 1) * 0.55f) + _axis * (Length * 0.5f - 0.3f * i);
                float width = 1.1f + 0.35f * ((i * 2) % 3);
                go.transform.localScale = new Vector3(width, Length - 0.8f * i, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.material.color = new Color(Moonlight.r, Moonlight.g, Moonlight.b, Alpha * (i == 1 ? 1.2f : 0.8f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _shafts[i] = go.transform;
            }

            Dust(shader, door);
        }

        /// <summary>Пыль в луче: точки висят и медленно плывут, видны только в его объёме.</summary>
        private void Dust(Shader shader, Vector3 door)
        {
            var go = new GameObject("Пыль");
            go.transform.SetParent(transform, false);
            go.transform.position = door + _axis * (Length * 0.5f);
            go.transform.rotation = Quaternion.LookRotation(_axis);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);
            main.startColor = new Color(0.85f, 0.88f, 1f, 0.55f);
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            // Коробка вдоль луча: ширина луча поперёк, его длина вдоль.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.4f, 0.8f, Length * 0.9f);

            var drift = ps.noise;
            drift.enabled = true;
            drift.strength = 0.04f;
            drift.frequency = 0.3f;
            drift.scrollSpeed = 0.1f;

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(g);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = new Material(shader) { mainTexture = Dot(16), name = "Пыль" };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || _shafts == null) return;
            foreach (var s in _shafts)
            {
                if (s == null) continue;
                var face = Vector3.ProjectOnPlane(cam.transform.position - s.position, _axis);
                if (face.sqrMagnitude > 1e-4f) s.rotation = Quaternion.LookRotation(-face, -_axis);
            }
        }

        private static Texture2D Dot(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Пылинка" };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            t.Apply();
            return t;
        }
    }
}
