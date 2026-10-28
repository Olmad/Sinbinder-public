// Assets/Scripts/Gameplay/Gaze.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Куда смотрит Греховод — для F (автор, 26.10: «взаимодействие всё ещё не зависит от взгляда»;
    /// 27.10: «по-прежнему РАДИУС взаимодействия, а не взгляд»). Глазами — середина кадра, сверху —
    /// мышь, как у разговора с воином. Досягаемость (радиус) — только предел: из всего, до чего рука
    /// достаёт, F получает то, что ближе всех к взгляду на экране, а не ближе всех к телу.
    /// </summary>
    public static class Gaze
    {
        /// <summary>
        /// Насколько взгляд может промахнуться мимо вещи, в долях высоты экрана: ~3,5 % — палец
        /// у края вещи, а не через полкадра от неё.
        /// </summary>
        public const float Slack = 0.035f;

        /// <summary>Не смотрит — такое число промаха.</summary>
        public const float Away = float.MaxValue;

        /// <summary>Глазами ли смотрит сейчас (вид от первого лица).</summary>
        public static bool Eyes
        {
            get
            {
                var cam = Camera.main;
                var view = cam != null ? cam.GetComponent<RTS_Camera>() : null;
                return view != null && view.FirstPersonNow;
            }
        }

        /// <summary>Точка взгляда на экране: середина кадра глазами, мышь сверху.</summary>
        public static Vector2 Aim
            => Eyes ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : (Vector2)Input.mousePosition;

        /// <summary>
        /// Промах взгляда мимо шара вокруг точки: 0 — взгляд внутри шара на экране, дальше — доля
        /// высоты экрана до его края; <see cref="Away"/> — за спиной или дальше <see cref="Slack"/>.
        /// Под взглядом сразу несколько (пластинки на полке стоят теснее своего шара) — выигрывает
        /// та, чья середина ближе к взгляду (сотая доля расстояния до неё), а при равенстве — ближняя к глазу.
        /// </summary>
        public static float Miss(Vector3 centre, float radius)
        {
            var cam = Camera.main;
            if (cam == null) return Away;
            var s = cam.WorldToScreenPoint(centre);
            if (s.z <= 0.05f) return Away;
            var edge = cam.WorldToScreenPoint(centre + cam.transform.right * radius);
            float r = Vector2.Distance(s, edge);
            float h = Mathf.Max(1, Screen.height);
            float d = Vector2.Distance(s, Aim);
            float miss = Mathf.Max(0f, d - r) / h;
            return miss <= Slack ? miss + Mathf.Min(d, r) / h * 0.01f + s.z * 1e-5f : Away;
        }

        /// <summary>
        /// Промах мимо видимых частей вещи — по прямоугольнику её рамки на экране. Искры и дым
        /// не в счёт: их рамка шире самой вещи. Частей нет — шар в полметра над её точкой.
        /// </summary>
        public static float Miss(Transform thing)
        {
            var cam = Camera.main;
            if (cam == null || thing == null) return Away;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            float depth = float.MaxValue;
            bool any = false;
            foreach (var r in thing.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var s = cam.WorldToScreenPoint(corner);
                    if (s.z <= 0.05f) continue;
                    any = true;
                    depth = Mathf.Min(depth, s.z);
                    min = Vector2.Min(min, s);
                    max = Vector2.Max(max, s);
                }
            }
            if (!any) return Miss(thing.position + Vector3.up * 0.5f, 0.3f);

            var aim = Aim;
            float h = Mathf.Max(1, Screen.height);
            float dx = Mathf.Max(0f, Mathf.Max(min.x - aim.x, aim.x - max.x));
            float dy = Mathf.Max(0f, Mathf.Max(min.y - aim.y, aim.y - max.y));
            float miss = Mathf.Sqrt(dx * dx + dy * dy) / h;
            float half = Vector2.Distance(min, max) * 0.5f;
            float toMiddle = Mathf.Min(Vector2.Distance((min + max) * 0.5f, aim), half);
            return miss <= Slack ? miss + toMiddle / h * 0.01f + depth * 1e-5f : Away;
        }
    }
}
