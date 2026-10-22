// Assets/Scripts/Gameplay/HoodVoid.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Тьма под капюшоном Греховода — не лицо, а провал. Автор, 22 октября:
    /// «тьму под капюшоном, наверное, нужно сделать эффектами. Сейчас это
    /// выглядит как будто это и есть его лицо».
    ///
    /// Два приёма. Первый: материал пустоты («Void») — неосвещаемый, чисто
    /// чёрный: освещённый, он ловил свет костра, показывал выпуклости и
    /// читался гладким чёрным лицом. Второй: из глубины капюшона медленно
    /// тянется тёмная дымка и тает выше головы — тьма живая, выдыхает;
    /// угольки глаз горят в ней.
    ///
    /// Вешает себя на кость головы (<see cref="SinbinderPlayer"/>).
    /// </summary>
    public class HoodVoid : MonoBehaviour
    {
        /// <summary>Сделать провал под капюшоном у модели Греховода.</summary>
        public static void Make(GameObject model)
        {
            if (model == null) return;

            Unlit(model, "Void");

            var head = Find(model.transform, "Head");
            if (head == null) return;
            if (head.GetComponentInChildren<HoodVoid>() != null) return;

            var go = new GameObject("Тьма под капюшоном");
            go.transform.SetParent(head, false);
            // Чуть вперёд и вниз от середины черепа — туда, где лицо.
            go.transform.localPosition = new Vector3(0f, 0.02f, 0.06f);
            go.AddComponent<HoodVoid>().Smoke();
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var hit = Find(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>Материал по имени — неосвещаемый, чистый чёрный.</summary>
        private static void Unlit(GameObject go, string slot)
        {
            // Sprites/Default — всегда в сборке (как у колец выбора) и неосвещаемый.
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                bool touched = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith(slot)) continue;
                    var m = new Material(shader) { name = mats[i].name };
                    m.color = Color.black;
                    mats[i] = m;
                    touched = true;
                }
                if (touched) r.materials = mats;
            }
        }

        private static Texture2D _puff;

        /// <summary>Клубок: белый, прозрачность мягко спадает к краю.</summary>
        private static Texture2D Puff()
        {
            if (_puff != null) return _puff;
            const int n = 64;
            _puff = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    _puff.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            _puff.Apply();
            return _puff;
        }

        private void Smoke()
        {
            var ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
            main.startColor = new Color(0.02f, 0.015f, 0.02f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.04f;          // тянется вверх, медленно
            main.maxParticles = 60;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = 16f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            // Рождается невидимой, густеет и тает: без вспышек на краях.
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.25f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.8f;

            var renderer = GetComponent<ParticleSystemRenderer>();
            // Sprites/Default — всегда в сборке, прозрачный, цвет частицы
            // множит текстуру: дым темнит, а не светит.
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var m = new Material(shader) { mainTexture = Puff() };   // без текстуры частица — квадрат
                renderer.sharedMaterial = m;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
        }
    }
}
