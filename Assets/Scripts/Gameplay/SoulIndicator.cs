// Assets/Scripts/Gameplay/SoulIndicator.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Огонёк на месте угасающей души.
    ///
    /// Единственное, чем игрок узнаёт, что душу можно забрать и где.
    /// До сих пор индикатор требовал префаба, а префаба не было ни одного:
    /// души были невидимы, и урок сцены 4 сводился к нажатию E наугад.
    /// Поэтому огонёк теперь собирается из примитивов — как и весь
    /// остальной мир демо, в котором нет ни одного художника.
    ///
    /// И главное: цвет ставился один раз, в момент смерти. То есть
    /// угасание — вся суть урока — не было видно вообще. Теперь огонёк
    /// тускнеет и сжимается на глазах, и «успел или не успел» игрок
    /// читает не из журнала, а из того, что перед ним.
    /// </summary>
    public class SoulIndicator : MonoBehaviour
    {
        [SerializeField] private Renderer _body;
        [SerializeField] private Light _glow;
        [SerializeField] private float _bobSpeed = 2f;
        [SerializeField] private float _bobHeight = 0.2f;

        private FadingSoul _soul;
        private Vector3 _basePosition;

        /// <summary>Собрать огонёк на месте смерти. Префаба не требует.</summary>
        public static SoulIndicator Create(FadingSoul soul)
        {
            if (soul == null) return null;

            var go = new GameObject($"Душа: {soul.Warrior?.DisplayName}");
            go.transform.position = soul.Position + Vector3.up * 0.9f;

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Огонёк";
            ball.transform.SetParent(go.transform, false);
            ball.transform.localScale = Vector3.one * 0.28f;

            // Коллайдер только мешает: по душе не стреляют и её не выделяют,
            // а лучу выделения воинов он попадался бы первым.
            var collider = ball.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var lightGo = new GameObject("Свечение");
            lightGo.transform.SetParent(go.transform, false);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;

            var indicator = go.AddComponent<SoulIndicator>();
            indicator._body = ball.GetComponent<Renderer>();
            indicator._glow = light;
            indicator.Initialize(soul);

            return indicator;
        }

        public void Initialize(FadingSoul soul)
        {
            _soul = soul;
            _basePosition = soul.Position + Vector3.up * 0.9f;
            transform.position = _basePosition;

            Repaint();
        }

        void Update()
        {
            if (_soul == null) { Destroy(gameObject); return; }

            // Качается на месте: неподвижный огонёк теряется среди тел.
            float bob = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
            transform.position = _basePosition + Vector3.up * bob;

            Repaint();
        }

        /// <summary>
        /// Цвет и размер по нынешнему качеству. Каждый кадр, а не однажды:
        /// душа угасает, и это должно быть видно, пока смотришь.
        /// </summary>
        private void Repaint()
        {
            var colour = Colour(_soul.SoulQuality);

            if (_body != null)
            {
                _body.material.color = colour;
                _body.transform.localScale = Vector3.one * Size(_soul.SoulQuality);
            }

            if (_glow != null)
            {
                _glow.color = colour;
                _glow.intensity = Brightness(_soul.SoulQuality);
            }
        }

        private static Color Colour(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return new Color(0.93f, 0.95f, 1.00f);
                case SoulQuality.Acceptance: return new Color(0.95f, 0.85f, 0.55f);
                case SoulQuality.Fading:     return new Color(0.55f, 0.58f, 0.65f);
                default:                     return new Color(0.30f, 0.26f, 0.28f);
            }
        }

        private static float Size(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return 0.30f;
                case SoulQuality.Acceptance: return 0.24f;
                case SoulQuality.Fading:     return 0.18f;
                default:                     return 0.12f;
            }
        }

        private static float Brightness(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return 3.2f;
                case SoulQuality.Acceptance: return 2.1f;
                case SoulQuality.Fading:     return 1.1f;
                default:                     return 0.4f;
            }
        }
    }
}
