// Assets/Scripts/UI/MapPulse.cs
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Огонь отряда на карте в шаре дышит — тот же свет, что плывёт в самом шаре
    /// (<see cref="Gameplay.CrystalBall"/>). Время — без масштаба: карта ставит игру на паузу.
    /// Свет на решения не влияет, поэтому Time здесь допустим.
    /// </summary>
    public class MapPulse : MonoBehaviour
    {
        private Graphic _g;
        private Vector3 _scale;
        private float _phase;

        void Awake()
        {
            _g = GetComponent<Graphic>();
            _scale = transform.localScale;
            _phase = (transform.GetSiblingIndex() * 0.37f) % 1f * Mathf.PI * 2f;
        }

        void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f + _phase);
            transform.localScale = _scale * (0.85f + 0.3f * k);
            if (_g != null) { var c = _g.color; c.a = 0.55f + 0.45f * k; _g.color = c; }
        }
    }
}
