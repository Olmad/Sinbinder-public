// Assets/Scripts/Gameplay/CampAir.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Воздух лагеря: дым над костром, туман в бору, светлячки у огня.
    ///
    /// Автор, 26 октября: «человеку просто не интересно играть в „хрень
    /// с кубиками без стиля и звуков“. СЕЙЧАС нам нужно навести красоту».
    /// На кадрах того дня лагерь сверху — палатки на голой земле, а ночью
    /// вблизи — тёмная пустота: воздуха между вещами нет. Искры у костра уже
    /// были (<c>DemoSceneBuilder.Embers</c>); здесь — то, что делает ночь
    /// ночью: дым тянет ветром, у земли в бору стоит холодный туман, у огня
    /// мерцают светлячки.
    ///
    /// Ставит себя сам в каждую сцену, где есть костёр — самый широкий
    /// живой огонь (<see cref="FireFlicker"/>, свет шире 15 м): у факелов
    /// и жаровен склепа своего дыма и тумана не нужно. Пересобирать сцены
    /// не надо.
    ///
    /// Частицы — по времени мира: на уроке и паузе дым стоит, как всё.
    /// Разброс частиц — свой у Unity; на решения он не влияет, правило
    /// повторяемости охраняет движок, а не дым (как пульсация шара).
    /// Материал — <c>Sprites/Default</c>: он всегда в сборке.
    /// </summary>
    public class CampAir : MonoBehaviour
    {
        /// <summary>Свет костра шире этого, м; у факелов — девять.</summary>
        private const float CampfireRange = 15f;

        /// <summary>Ветер в бору, м/с: дым и туман тянет в одну сторону.</summary>
        private static readonly Vector3 Wind = new(0.35f, 0f, 0.15f);

        private static Material _soft;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void First() => Build();

        private static void OnScene(Scene scene, LoadSceneMode mode) => Build();

        private static void Build()
        {
            if (Object.FindFirstObjectByType<CampAir>() != null) return;

            Light fire = null;
            foreach (var f in Object.FindObjectsByType<FireFlicker>(FindObjectsSortMode.InstanceID))
            {
                var l = f.GetComponent<Light>();
                if (l != null && l.range >= CampfireRange && (fire == null || l.range > fire.range)) fire = l;
            }
            if (fire == null) return;

            if (Soft() == null)
            {
                Debug.LogWarning("[ВОЗДУХ] Шейдера Sprites/Default нет — дым и туман рисовать нечем.");
                return;
            }

            var at = fire.transform.position;
            float ground = Physics.Raycast(at + Vector3.up, Vector3.down, out var hit, 20f) ? hit.point.y : at.y - 1.5f;

            var go = new GameObject("Воздух лагеря");
            go.transform.position = new Vector3(at.x, ground, at.z);
            var air = go.AddComponent<CampAir>();
            air.Smoke(new Vector3(at.x, ground + 1.1f, at.z));
            air.Mist();
            air.Fireflies();
        }

        /// <summary>
        /// Дым: тёплый снизу, где его освещает огонь, холодный и прозрачный
        /// наверху; расходится и уходит по ветру. Сверху — серая полоса от костра:
        /// видно, откуда дует, и лагерь перестаёт быть картинкой.
        /// </summary>
        private void Smoke(Vector3 at)
        {
            var ps = System("Дым костра", at);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 4f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var drift = ps.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0f, Wind.x * 2.2f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(0f, Wind.z * 2.2f);

            Fade(ps, new[] { new GradientColorKey(new Color(0.62f, 0.44f, 0.32f), 0f),
                             new GradientColorKey(new Color(0.30f, 0.31f, 0.34f), 0.35f),
                             new GradientColorKey(new Color(0.20f, 0.22f, 0.26f), 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.32f, 0.12f),
                             new GradientAlphaKey(0.16f, 0.6f), new GradientAlphaKey(0f, 1f) });

            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(4.5f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));

            var turn = ps.rotationOverLifetime;
            turn.enabled = true;
            turn.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            ps.Play();
        }

        /// <summary>
        /// Туман в бору: широкие бледные пятна у самой земли, кольцом за краем
        /// тёплого острова (16–40 м от огня). У огня тумана нет — его держит жар;
        /// чем дальше в бор, тем холоднее.
        /// </summary>
        private void Mist()
        {
            var ps = System("Туман в бору", transform.position + Vector3.up * 0.4f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 24f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 70;

            var emission = ps.emission;
            emission.rateOverTime = 3.2f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = 28f;
            shape.donutRadius = 12f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var drift = ps.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(Wind.x * 0.6f, Wind.x * 0.6f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(Wind.z * 0.6f, Wind.z * 0.6f);

            Fade(ps, new[] { new GradientColorKey(new Color(0.46f, 0.52f, 0.62f), 0f),
                             new GradientColorKey(new Color(0.40f, 0.46f, 0.56f), 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.09f, 0.3f),
                             new GradientAlphaKey(0.09f, 0.7f), new GradientAlphaKey(0f, 1f) });

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sortingFudge = 10f;    // за искрами и дымом, а не поверх

            ps.Play();
        }

        /// <summary>
        /// Светлячки: редкие тёплые точки в полутьме вокруг огня — бродят,
        /// вспыхивают и гаснут. Видны и сверху, и от первого лица.
        /// </summary>
        private void Fireflies()
        {
            var ps = System("Светлячки", transform.position + Vector3.up * 1.2f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startColor = new Color(0.86f, 1f, 0.52f);
            main.maxParticles = 50;

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = 11f;
            shape.donutRadius = 5f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;

            // Мигают: вспыхнул — погас — ещё раз, тише.
            Fade(ps, new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                             new GradientAlphaKey(0.1f, 0.45f), new GradientAlphaKey(0.8f, 0.65f),
                             new GradientAlphaKey(0f, 1f) });

            ps.Play();
        }

        /// <summary>Петля частиц в мировых координатах, с запасом на старте: сцена открывается с дымом, а не с пустотой.</summary>
        private ParticleSystem System(string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = at;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _soft;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Fade(ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Мягкое пятно: белое в середине, к краю тает. Рисуется формулой.</summary>
        private static Material Soft()
        {
            if (_soft != null) return _soft;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            tex.Apply();

            _soft = new Material(shader) { mainTexture = tex };
            return _soft;
        }
    }
}
