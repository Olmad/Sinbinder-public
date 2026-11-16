// Assets/Scripts/Gameplay/SoulIndicator.cs
// Перевод: текст через Loc
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
        private Transform _halo;
        private Material _haloMat;

        /// <summary>Чья это душа: жатва гасит огонёк именно её (SoulManager.RemoveIndicator).</summary>
        public FadingSoul Soul => _soul;

        /// <summary>Собрать огонёк на месте смерти. Префаба не требует.</summary>
        public static SoulIndicator Create(FadingSoul soul)
        {
            if (soul == null) return null;

            var go = new GameObject($"Душа: {soul.Warrior?.DisplayName}");
            go.transform.position = soul.Position + Vector3.up * 0.9f;

            // Ядро светится само, вокруг ореол (SoulGlow, 16.11): обычный шар со светом внутри
            // этим светом не освещался, и в тёмном сне душа была серым мячиком. Коллайдера нет:
            // по душе не стреляют и её не выделяют, а лучу выделения он попадался бы первым.
            var ball = SoulGlow.Shape(PrimitiveType.Sphere, go.transform, SoulGlow.Core(Color.white), Vector3.zero, 0.28f);
            ball.name = "Огонёк";
            var halo = SoulGlow.Halo();
            var haloQuad = SoulGlow.Shape(PrimitiveType.Quad, go.transform, halo, Vector3.zero, 1f);
            haloQuad.name = "Ореол";

            var light = SoulGlow.Lamp(go.transform, Color.white, 4f, 2f);
            light.name = "Свечение";

            var indicator = go.AddComponent<SoulIndicator>();
            indicator._body = ball.GetComponent<Renderer>();
            indicator._glow = light;
            indicator._halo = haloQuad;
            indicator._haloMat = halo;
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

        void LateUpdate()
        {
            // Ореол плоский — лицом к камере, откуда бы ни смотрели.
            var cam = Camera.main;
            if (_halo != null && cam != null) _halo.rotation = cam.transform.rotation;
        }

        /// <summary>
        /// Цвет и размер по нынешнему качеству. Каждый кадр, а не однажды:
        /// душа угасает, и это должно быть видно, пока смотришь.
        /// </summary>
        private void Repaint()
        {
            var colour = Colour(_soul.SoulQuality);

            float size = Size(_soul.SoulQuality);
            if (_body != null)
            {
                // Ядро светлее ореола: середина огня всегда почти белая.
                _body.material.color = _halo != null ? Color.Lerp(colour, Color.white, 0.45f) * 1.3f : colour;
                _body.transform.localScale = Vector3.one * size;
            }
            if (_halo != null)
            {
                // Ореол тускнеет быстрее ядра: угасание видно издали.
                _halo.localScale = Vector3.one * size * 4.2f;
                SoulGlow.Paint(_haloMat, colour, Mathf.Clamp01(Brightness(_soul.SoulQuality) / 3.2f) * 0.7f);
            }

            if (_glow != null)
            {
                _glow.color = colour;
                _glow.intensity = Brightness(_soul.SoulQuality);
            }
        }

        /// <summary>Цвет огонька над телом — с ним же душа и взлетает (SoulFlight).</summary>
        internal static Color Colour(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return new Color(0.93f, 0.95f, 1.00f);
                case SoulQuality.Acceptance: return new Color(0.95f, 0.85f, 0.55f);
                case SoulQuality.Fading:     return new Color(0.55f, 0.58f, 0.65f);
                default:                     return new Color(0.30f, 0.26f, 0.28f);
            }
        }

        internal static float Size(SoulQuality quality)
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
