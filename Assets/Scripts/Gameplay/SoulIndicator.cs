using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class SoulIndicator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private float _bobSpeed = 2f;
        [SerializeField] private float _bobHeight = 0.2f;

        private FadingSoul _soul;
        private Vector3 _basePosition;

        public void Initialize(FadingSoul soul)
        {
            _soul = soul;
            _basePosition = soul.Position;
            transform.position = _basePosition;

            if (_spriteRenderer != null)
            {
                switch (soul.SoulQuality)
                {
                    case Core.SoulQuality.Shock:
                        _spriteRenderer.color = Color.white;
                        break;
                    case Core.SoulQuality.Acceptance:
                        _spriteRenderer.color = Color.yellow;
                        break;
                    case Core.SoulQuality.Fading:
                        _spriteRenderer.color = Color.gray;
                        break;
                    default:
                        _spriteRenderer.color = Color.black;
                        break;
                }
            }
        }

        void Update()
        {
            if (_soul == null)
            {
                Destroy(gameObject);
                return;
            }

            float bob = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
            transform.position = _basePosition + new Vector3(0, bob, 0);

            if (_spriteRenderer != null)
            {
                float alpha = _soul.RemainingTime / 60f;
                Color c = _spriteRenderer.color;
                c.a = Mathf.Clamp01(alpha);
                _spriteRenderer.color = c;
            }
        }
    }
}