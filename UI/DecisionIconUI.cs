// Assets/_Project/Scripts/UI/DecisionIconUI.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Sinbinder.UI
{
    public class DecisionIconUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        private Coroutine _hideCoroutine;

        public void SetIconImage(Image img)
        {
            _iconImage = img;
            _iconImage.enabled = false;
        }

        public void Show(Sprite icon, float duration = 2f)
        {
            if (_iconImage == null) return;

            _iconImage.sprite = icon;
            _iconImage.enabled = true;

            if (_hideCoroutine != null)
                StopCoroutine(_hideCoroutine);

            _hideCoroutine = StartCoroutine(HideAfter(duration));
        }

        private IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_iconImage != null)
                _iconImage.enabled = false;
        }
    }
}