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

        // 0,95 с при огоньке-искре читались как телепорт (автор, 16.11): белый огонёк над телом
        // гас, а у Греховода мигало что-то цветное. Теперь тот же огонёк — того же цвета и
        // размера — сначала поднимается над телом (Lift), потом дугой летит в банку.
        private const float Lift = 0.45f;
        private const float Travel = 1.25f;
        private const float Seconds = Lift + Travel;
        private const float Arc = 1.2f;

        /// <summary>Сколько душ сейчас в воздухе: сума зажигает банку, когда долетит (SatchelUI).</summary>
        public static int InAir { get; private set; }

        /// <summary>Сколько летит одна душа — до вспышки у Греховода.</summary>
        public const float FlightSeconds = Seconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { _instance = null; InAir = 0; }

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
            if (_bound != null)
            {
                _bound.OnSoulHarvested += Fly;
                Debug.Log("[ДУША] полёт душ слушает жатву сцены " + _bound.gameObject.scene.name);
            }
        }

        void OnDestroy()
        {
            if (_bound != null) _bound.OnSoulHarvested -= Fly;
        }

        private void Fly(FadingSoul soul)
        {
            var me = SinbinderPlayer.Instance;
            if (soul == null || me == null)
            {
                Debug.LogWarning("[ДУША] полёта нет: " + (soul == null ? "души нет" : "Греховода нет"));
                return;
            }
            Debug.Log("[ДУША] летит в банку: " + (soul.Warrior != null ? soul.Warrior.DisplayName : "?"));

            var sinner = soul.Warrior != null ? soul.Warrior.Soul : null;
            var color = sinner != null ? SinPalette.Of(sinner.Sin) : new Color(0.80f, 0.79f, 0.76f);

            StartCoroutine(Flight(soul.Position, me.transform, SoulIndicator.Colour(soul.SoulQuality),
                                  SoulIndicator.Size(soul.SoulQuality), color));
        }

        /// <summary>
        /// Куда летит: глазами — в банку на полосе сумы (низ справа, там она и вспыхнет);
        /// сверху — к поясу Греховода, где висят банки.
        /// </summary>
        private static Vector3 Target(Transform hero)
        {
            var cam = Camera.main;
            var view = cam != null ? cam.GetComponent<RTS_Camera>() : null;
            if (view != null && view.FirstPersonNow)
                return cam.ViewportToWorldPoint(new Vector3(0.87f, 0.09f, 0.9f));
            return hero.position + Vector3.up * 1.0f + hero.right * 0.25f;
        }

        private IEnumerator Flight(Vector3 from, Transform to, Color was, float size, Color sin)
        {
            var orb = new GameObject("Душа в банку").transform;
            // С того же места, где висел огонёк над телом (SoulIndicator: +0,9).
            orb.position = from + Vector3.up * 0.9f;
            InAir++;

            var coreMat = SoulGlow.Core(was);
            var haloMat = SoulGlow.Halo();
            var core = SoulGlow.Shape(PrimitiveType.Sphere, orb, coreMat, Vector3.zero, size);
            var halo = SoulGlow.Shape(PrimitiveType.Quad, orb, haloMat, Vector3.zero, size * 4.2f);
            var light = SoulGlow.Lamp(orb, was, 3f, 1.6f);
            var trail = Sparks(orb, "Шлейф", sin, rate: 60f, life: 0.5f, size: size * 0.45f, local: false);

            Vector3 start = orb.position;
            Vector3 top = start + Vector3.up * 0.55f;
            float t = 0f;
            while (t < Seconds && to != null)
            {
                // Шаг не длиннее 1/30 с. Жатва — тяжёлый кадр (урок кончается, строится полоса):
                // настоящее время за него было больше всего полёта, и душа оказывалась в банке
                // за кадр — «телепортируется, как и раньше» (автор, 16.11; на кадре прогона огонька нет).
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                t += dt;
                Vector3 at;
                float k;
                if (t < Lift)
                {
                    // Отрывается от тела: медленно вверх, разгораясь.
                    k = 0f;
                    float u = Mathf.SmoothStep(0f, 1f, t / Lift);
                    at = Vector3.Lerp(start, top, u);
                }
                else
                {
                    // Дугой — в банку; цель берётся каждый кадр: догоняет и идущего.
                    k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - Lift) / Travel));
                    at = Vector3.Lerp(top, Target(to), k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * Arc * (1f - k * 0.5f);
                }
                orb.position = at;

                // Цвет — из огонька над телом в цвет греха: в банке душа уже «своя».
                var colour = Color.Lerp(was, sin, k);
                coreMat.color = Color.Lerp(colour, Color.white, 0.45f) * 1.3f;
                SoulGlow.Paint(haloMat, colour, 0.7f);
                light.color = colour;
                light.intensity = 1.6f + (t < Lift ? t / Lift : 1f) * 0.8f;

                // Размер на экране почти один: у самой камеры (глазами) огонёк не раздувается.
                var cam = Camera.main;
                float dist = cam != null ? Vector3.Distance(cam.transform.position, at) : 10f;
                float s = Mathf.Min(size * (1f + 0.25f * Mathf.Sin(t * 9f) * (t < Lift ? 1f : 0.3f)), dist * 0.035f);
                core.localScale = Vector3.one * s;
                halo.localScale = Vector3.one * s * 4.2f;
                if (cam != null) halo.rotation = cam.transform.rotation;

                // Частицы считаются по настоящему времени: мир может стоять.
                trail.Simulate(dt, true, false, false);
                yield return null;
            }

            // Пришла — вспышка и гаснет: душа в банке (банка на полосе вспыхивает сама, SatchelUI).
            InAir = Mathf.Max(0, InAir - 1);
            var emission = trail.emission; emission.enabled = false;
            Vector3 coreAt = core.localScale;
            float fade = 0f;
            while (fade < 0.4f)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                fade += dt;
                float f = fade / 0.4f;
                core.localScale = coreAt * (1f + f * 0.8f) * (1f - f);
                halo.localScale = coreAt * 4.2f * (1f + f * 1.5f);
                SoulGlow.Paint(haloMat, sin, 0.7f * (1f - f));
                light.intensity = Mathf.Lerp(3.2f, 0f, f);
                trail.Simulate(dt, true, false, false);
                var cam = Camera.main;
                if (cam != null) halo.rotation = cam.transform.rotation;
                yield return null;
            }

            Destroy(orb.gameObject);
            Destroy(coreMat);
            Destroy(haloMat);
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
