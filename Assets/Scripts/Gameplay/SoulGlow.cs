// Assets/Scripts/Gameplay/SoulGlow.cs
// Перевод: текста нет
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Как светится душа — одно на всю игру (план 66, сцена 1: «душа выходит светом»).
    ///
    /// До 16.11 огонёк над телом был обычным шаром с обычным материалом, а свет сидел у него
    /// внутри — и шар им не освещался: в тёмном сне душа выглядела серым мячиком. Теперь у
    /// души три слоя: ядро без теней (светится само), ореол сложением цвета, мягкий к краю,
    /// и слабый точечный свет на то, что рядом. Одни и те же — над телом (SoulIndicator),
    /// в полёте к банке (SoulFlight) и над ладонью (SoulInHandView).
    /// </summary>
    public static class SoulGlow
    {
        /// <summary>Ядро: светится само, тени не знает.</summary>
        public static Material Core(Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            return new Material(shader) { name = "Душа — ядро", color = colour };
        }

        /// <summary>Свет в воздухе: мягкое пятно, сложением цвета, виден с обеих сторон.</summary>
        public static Material Halo(string name = "Душа — ореол")
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { name = name };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0f));
            var soft = Soft();
            mat.SetTexture("_BaseMap", soft);
            mat.mainTexture = soft;
            return mat;
        }

        /// <summary>Цвет ореола (с прозрачностью — она же яркость при сложении).</summary>
        public static void Paint(Material halo, Color colour, float alpha)
        {
            if (halo == null) return;
            colour.a = alpha;
            halo.SetColor("_BaseColor", colour);
        }

        /// <summary>Примитив без коллайдера и тени.</summary>
        public static Transform Shape(PrimitiveType type, Transform parent, Material mat, Vector3 at, float size)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>Слабый точечный свет без теней.</summary>
        public static Light Lamp(Transform parent, Color colour, float range, float intensity)
        {
            var light = new GameObject("Свет души").AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.transform.SetParent(parent, false);
            return light;
        }

        private static Texture2D _soft;

        /// <summary>Мягкое пятно: к краю гаснет квадратом — у ореола нет кромки.</summary>
        public static Texture2D Soft()
        {
            if (_soft != null) return _soft;
            const int n = 64;
            _soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Мягкое пятно" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float k = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(k * k * 255f));
                }
            _soft.SetPixels32(px);
            _soft.Apply(false, true);
            return _soft;
        }
    }
}
