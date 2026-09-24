// Assets/Scripts/Gameplay/VoiceRing.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Круг голоса вокруг Греховода (docs/31-VOICE.md §4): внутреннее кольцо —
    /// до сих пор приказ слышен в полную силу, внешнее, бледное, — край,
    /// дальше которого не слышен вовсе.
    ///
    /// Виден, пока выделены свои воины и голос включён (<see cref="Voice"/>):
    /// круг нужен в тот миг, когда игрок собирается приказывать, а не всё
    /// время. Лежит по земле, а не плашмя: лагерь стоит на холме, и ровное
    /// кольцо ушло бы под склон.
    ///
    /// Ставит <see cref="SinbinderPlayer"/> себе сам.
    /// </summary>
    public class VoiceRing : MonoBehaviour
    {
        private const int Segments = 72;
        private const float Lift = 0.08f;

        private static readonly Color NearColor = new(0.96f, 0.86f, 0.56f, 0.55f);
        private static readonly Color FarColor = new(0.96f, 0.86f, 0.56f, 0.18f);

        private static Material _material;
        private static bool _toldAboutShader;

        private LineRenderer _near;
        private LineRenderer _far;
        private Vector3 _drawnAt = new(float.MaxValue, 0f, 0f);
        private bool _shown;

        void Start()
        {
            if (_material == null)
            {
                // Тот же шейдер, что у кругов выделения: он в списке всегда
                // включаемых в сборку.
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    if (!_toldAboutShader)
                    {
                        _toldAboutShader = true;
                        Debug.LogWarning("[ГОЛОС] Шейдера Sprites/Default нет: круг голоса "
                                       + "рисовать нечем. Голос работает и без него.");
                    }
                    enabled = false;
                    return;
                }
                _material = new Material(shader);
            }

            _near = Ring("Голос: рядом", NearColor, 0.09f);
            _far = Ring("Голос: край", FarColor, 0.06f);
            Show(false);
        }

        void Update()
        {
            bool show = Voice.Enabled && OwnSelected();
            if (show != _shown) Show(show);
            if (!show) return;

            // Перерисовать, когда Греховод ушёл: семь десятков лучей в землю
            // на каждый кадр ни к чему.
            if ((transform.position - _drawnAt).sqrMagnitude < 0.09f) return;
            _drawnAt = transform.position;

            Draw(_near, Voice.Near);
            Draw(_far, Voice.Far);
        }

        private void Show(bool on)
        {
            _shown = on;
            if (_near != null) _near.enabled = on;
            if (_far != null) _far.enabled = on;
            if (on) _drawnAt = new Vector3(float.MaxValue, 0f, 0f);
        }

        /// <summary>Выделен ли хоть один свой живой, кроме самого Греховода.</summary>
        private static bool OwnSelected()
        {
            var manager = SelectionManager.Instance;
            if (manager == null) return false;

            foreach (var unit in manager.GetSelectedUnits())
            {
                if (unit == null) continue;
                var w = unit.GetComponentInParent<Warrior>();
                if (w != null && !(w is SinbinderPlayer) && !w.IsDead && w.Team == Team.Player)
                    return true;
            }
            return false;
        }

        private LineRenderer Ring(string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = Segments;
            return line;
        }

        private void Draw(LineRenderer line, float radius)
        {
            var centre = transform.position;
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                var p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                line.SetPosition(i, OnGround(p));
            }
        }

        /// <summary>Точка на том, что под ней: земля, холм, реквизит. Воинов пропускаем.</summary>
        private static Vector3 OnGround(Vector3 p)
        {
            var from = p + Vector3.up * 20f;
            var hits = Physics.RaycastAll(from, Vector3.down, 60f);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<Warrior>() != null) continue;
                return hit.point + Vector3.up * Lift;
            }
            return p + Vector3.up * Lift;
        }
    }
}
