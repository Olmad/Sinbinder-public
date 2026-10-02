// Assets/Scripts/Gameplay/SoulFlight.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Душа летит в банку (41-SHOWCASE п. 7 — «душа, летящая к банке»).
    ///
    /// Жатва — главный жест Греховода, а выглядела она никак: E, строка
    /// в журнале, ячейка сумы сменила слово. Теперь собранная душа видна:
    /// огонёк цвета её греха поднимается над павшим и дугой летит
    /// к Греховоду, оставляя шлейф, и гаснет у него вспышкой — в суму.
    ///
    /// Время — настоящее: первую жатву объясняет урок стоп-кадром, мир
    /// стоит, а полёт обязан долететь. Ставит себя сам и живёт между
    /// сценами; к <see cref="SoulManager"/> подписывается заново в каждой
    /// сцене — тот живёт сценой.
    /// </summary>
    public class SoulFlight : MonoBehaviour
    {
        private static SoulFlight _instance;
        private SoulManager _bound;

        private const float Seconds = 0.95f;
        private const float Arc = 1.4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("Полёт душ");
            _instance = go.AddComponent<SoulFlight>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            var souls = SoulManager.Instance;
            if (souls == _bound) return;

            if (_bound != null) _bound.OnSoulHarvested -= Fly;
            _bound = souls;
            if (_bound != null) _bound.OnSoulHarvested += Fly;
        }

        void OnDestroy()
        {
            if (_bound != null) _bound.OnSoulHarvested -= Fly;
        }

        private void Fly(FadingSoul soul)
        {
            var me = SinbinderPlayer.Instance;
            if (soul == null || me == null) return;

            var sinner = soul.Warrior != null ? soul.Warrior.Soul : null;
            var color = sinner != null ? SinPalette.Of(sinner.Sin) : new Color(0.80f, 0.79f, 0.76f);

            StartCoroutine(Flight(soul.Position, me.transform, color));
        }

        private IEnumerator Flight(Vector3 from, Transform to, Color color)
        {
            var orb = new GameObject("Душа в банку");
            orb.transform.position = from + Vector3.up * 0.6f;

            var light = orb.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = color;
            light.range = 2.8f;
            light.intensity = 2.2f;

            var core = Sparks(orb.transform, "Огонёк", color, rate: 70f, life: 0.10f, size: 0.34f, local: true);
            var trail = Sparks(orb.transform, "Шлейф", color, rate: 55f, life: 0.45f, size: 0.12f, local: false);

            Vector3 start = orb.transform.position;
            float t = 0f;
            while (t < Seconds && to != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Seconds));

                // Цель — грудь Греховода, где бы он ни был: догоняет и идущего.
                Vector3 end = to.position + Vector3.up * 1.3f;
                orb.transform.position = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * Arc;

                // Частицы считаются по настоящему времени: мир может стоять.
                core.Simulate(Time.unscaledDeltaTime, true, false, false);
                trail.Simulate(Time.unscaledDeltaTime, true, false, false);

                yield return null;
            }

            // Пришла — вспышка и гаснет: душа в суме.
            var core2 = core.emission; core2.enabled = false;
            var trail2 = trail.emission; trail2.enabled = false;
            core.Emit(new ParticleSystem.EmitParams { startSize = 0.6f, startColor = color }, 1);

            float fade = 0f;
            while (fade < 0.45f)
            {
                fade += Time.unscaledDeltaTime;
                light.intensity = Mathf.Lerp(3.2f, 0f, fade / 0.45f);
                core.Simulate(Time.unscaledDeltaTime, true, false, false);
                trail.Simulate(Time.unscaledDeltaTime, true, false, false);
                yield return null;
            }

            Destroy(orb);
        }

        /// <summary>Искры огонька: материал общий с ударами (<see cref="HitBurst.Dot"/>).</summary>
        private static ParticleSystem Sparks(Transform parent, string name, Color color,
                                             float rate, float life, float size, bool local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Считает его сам полёт (Simulate по настоящему времени): иначе на
            // стоящем мире огонёк повис бы без шлейфа.
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.useUnscaledTime = true;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.startLifetime = life;
            main.startSpeed = local ? 0f : 0.15f;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = local ? 0.02f : 0.06f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = HitBurst.Dot();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return ps;
        }
    }
}
