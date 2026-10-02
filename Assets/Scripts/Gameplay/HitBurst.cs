// Assets/Scripts/Gameplay/HitBurst.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Вспышка удара (41-SHOWCASE п. 7 — «вспышка удара, пыль и кость
    /// скелета; дёшево, заметно»).
    ///
    /// До 2 октября удар в игре был числом в полосе здоровья, а с этого
    /// утра — ещё и звуком (<see cref="Audio.Sfx.Hit"/>). Видно его не было:
    /// бой, который не читается, прячет и отказы. Теперь в точке удара —
    /// короткая вспышка света и горсть частиц по телу ударенного, тем же
    /// разбором, что звук: скелет сыплет костяной пылью, голем — каменной
    /// крошкой, зомби — тёмной сукровицей, живой — тёмно-красным, призрак —
    /// бледными клочьями, которые не падают, а тают вверх; по щиту
    /// в стойке (провокация Каргана) — искры.
    ///
    /// Два общих облака частиц на всю игру (пыль падает, клочья всплывают),
    /// выброс — <c>Emit</c> в точку: ни одного объекта на удар. Материал —
    /// на <c>Sprites/Default</c>: этот шейдер всегда в сборке (на нём же
    /// кольца выбора), а мягкая точка рисуется формулой.
    ///
    /// В тумане удара не видно, как и самого врага (<see cref="FogOfWar.Hides"/>).
    /// Ставит себя сам и живёт между сценами.
    /// </summary>
    public class HitBurst : MonoBehaviour
    {
        private static HitBurst _instance;
        private static Material _material;

        private ParticleSystem _dust;   // падает
        private ParticleSystem _wisp;   // всплывает
        private Light _flash;
        private float _flashUntil;
        private float _flashPeak;

        private const float FlashSeconds = 0.12f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("Вспышки ударов");
            _instance = go.AddComponent<HitBurst>();
            DontDestroyOnLoad(go);
        }

        /// <summary>Удар по этому воину в этой точке (ноги; вспышка — на уровне груди).</summary>
        public static void At(Warrior victim, Vector3 where)
        {
            if (_instance == null) return;
            if (victim != null && FogOfWar.Hides(victim)) return;
            _instance.Burst(victim, where + Vector3.up * 1.1f);
        }

        void Awake()
        {
            _dust = Cloud("Пыль удара", gravity: 0.9f, lifetime: 0.75f, speed: 2.6f);
            _wisp = Cloud("Клочья удара", gravity: -0.12f, lifetime: 1.1f, speed: 0.9f);

            var flash = new GameObject("Вспышка");
            flash.transform.SetParent(transform, false);
            _flash = flash.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.shadows = LightShadows.None;
            _flash.range = 3.2f;
            _flash.intensity = 0f;
            _flash.enabled = false;
        }

        void Update()
        {
            if (!_flash.enabled) return;

            float left = _flashUntil - Time.time;
            if (left <= 0f) { _flash.enabled = false; return; }
            _flash.intensity = _flashPeak * (left / FlashSeconds);
        }

        private void Burst(Warrior victim, Vector3 at)
        {
            bool shield = victim != null && Provocation.StanceBonus(victim) > 0f;
            var shell = victim != null ? victim.Shell : ShellType.Living;

            Color color; int count; bool rises = false; float size = 0.09f; Color light;
            if (shield)
            {
                color = new Color(1f, 0.78f, 0.40f); count = 16; size = 0.06f;
                light = new Color(1f, 0.75f, 0.40f);
            }
            else switch (shell)
            {
                case ShellType.Skeleton:
                    color = new Color(0.86f, 0.82f, 0.72f); count = 14;
                    light = new Color(0.95f, 0.90f, 0.80f); break;
                case ShellType.Golem:
                    color = new Color(0.46f, 0.45f, 0.47f); count = 12; size = 0.11f;
                    light = new Color(0.80f, 0.78f, 0.74f); break;
                case ShellType.Zombie:
                    color = new Color(0.30f, 0.24f, 0.12f); count = 10;
                    light = new Color(0.70f, 0.60f, 0.40f); break;
                case ShellType.Ghost:
                    color = new Color(0.72f, 0.86f, 1f, 0.7f); count = 12; size = 0.13f; rises = true;
                    light = new Color(0.65f, 0.80f, 1f); break;
                default:
                    color = new Color(0.45f, 0.08f, 0.06f); count = 10;
                    light = new Color(0.95f, 0.70f, 0.55f); break;
            }

            var cloud = rises ? _wisp : _dust;
            var emit = new ParticleSystem.EmitParams
            {
                position = at,
                applyShapeToPosition = true,
                startColor = color,
                startSize = size,
            };
            cloud.Emit(emit, count);

            _flash.transform.position = at;
            _flash.color = light;
            _flashPeak = shield ? 3.2f : 2.2f;
            _flash.intensity = _flashPeak;
            _flash.enabled = true;
            _flashUntil = Time.time + FlashSeconds;
        }

        /// <summary>Облако частиц в мировых координатах: само не испускает, только по Emit.</summary>
        private ParticleSystem Cloud(string name, float gravity, float lifetime, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            // Зациклено при выключенном испускании: система идёт всегда и считает
            // выброшенные частицы; незацикленная встала бы через пять секунд.
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.gravityModifier = gravity;
            main.maxParticles = 600;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var shrink = ps.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Dot();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }

        /// <summary>
        /// Мягкая точка на <c>Sprites/Default</c>. Общая с полётом души
        /// (<see cref="SoulFlight"/>): один материал на все искры игры.
        /// </summary>
        public static Material Dot()
        {
            if (_material != null) return _material;

            const int size = 32;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            t.SetPixels32(px);
            t.Apply();

            var shader = Shader.Find("Sprites/Default");
            _material = new Material(shader) { name = "Искра (код)", mainTexture = t };  // ключ
            return _material;
        }
    }
}
