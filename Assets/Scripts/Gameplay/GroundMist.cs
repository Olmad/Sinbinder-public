// Assets/Scripts/Gameplay/GroundMist.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Туман у земли (КТ1 п. 19: «туман стелется по земле, чёрных провалов нет»). Туман сцены — ровная дымка по
    /// расстоянию; у земли ничего не движется, и ночь между палатками и лесом читалась пустой. Здесь — кольцо
    /// медленных бледных пластов вокруг лагеря: ближе 9 м к огню их нет (тёплый островок чистый), дальше —
    /// стелются до опушки, тянутся и тают. Сверху — светлые разводы по тёмной земле, глазами — дымка по колено.
    ///
    /// Частицы с мягким пятном, нарисованным формулой; материал — частицы URP, прозрачность обычная (не
    /// сложение: туман не светится). Ставит себя сам там, где есть «Костёр» и «Холодный свет» (ночной лагерь).
    /// </summary>
    public class GroundMist : MonoBehaviour
    {
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
            if (Object.FindFirstObjectByType<GroundMist>() != null) return;
            var fire = GameObject.Find("Костёр");
            if (fire == null || GameObject.Find("Холодный свет") == null) return;
            var go = new GameObject("Туман у земли");
            go.transform.position = fire.transform.position + Vector3.up * 0.35f;
            go.AddComponent<GroundMist>();
        }

        void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) { enabled = false; return; }
            var mat = new Material(shader) { name = "Mist" };
            var tex = Puff(64);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            // Обычная прозрачность (как у лепестка в EffectsBuilder.Blend): туман не светится, а застилает.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            var ps = gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 30f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(26f, 38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.68f, 0.80f, 0.075f);
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 1.4f;

            // Кольцо 9–28 м вокруг огня: у костра чисто, к лесу — гуще.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 28f;
            shape.radiusThickness = (28f - 9f) / 28f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(g);

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

            var r = GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;   // лицом к камере: сверху — разводы по земле, сбоку — дымка, а не ребро пласта
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 10f;   // позади огня и искр
        }

        /// <summary>Мягкое пятно с рваным краем — формулой, как искра и пламя.</summary>
        private static Texture2D Puff(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Mist" };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.65f + Mathf.PerlinNoise(x * 0.21f + 3f, y * 0.21f) * 0.35f;
                float a = Mathf.Clamp01(1f - d) * Mathf.Lerp(0.35f, 1f, n);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            t.Apply();
            return t;
        }
    }
}
