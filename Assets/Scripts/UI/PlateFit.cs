// Assets/Scripts/UI/PlateFit.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Тонкая табличка (<see cref="UiStyle.PlateThin"/>) по размеру своего
    /// текста — для летучих подсказок, которые до 15 октября висели голым
    /// текстом поверх мира: прогноз над приказами и строка F у предмета
    /// (опись, 46-UI §1.1, кадр 5).
    ///
    /// Ширина — по самой длинной строке, но не шире предела; высота —
    /// по переносам. Пустой текст — таблички нет. Текст — ребёнок таблички,
    /// растянут по ней с полями; табличка стоит там, где раньше стоял текст.
    /// Мерит, только когда текст сменился: подсказки пишут каждый кадр.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class PlateFit : MonoBehaviour
    {
        private Text _text;
        private Image _image;
        private float _maxWidth;
        private Vector2 _pad;
        private string _last;

        void Awake()
        {
            _image = GetComponent<Image>();
            UiStyle.Dress(_image, UiStyle.PlateWeight.Strip);
            _image.raycastTarget = false;
        }

        /// <summary>
        /// Связать с текстом: текст ложится внутрь с полями <paramref name="pad"/>,
        /// табличка растёт по нему не шире <paramref name="maxWidth"/> (без полей).
        /// </summary>
        public void Bind(Text text, float maxWidth, Vector2 pad)
        {
            _text = text;
            _maxWidth = maxWidth;
            _pad = pad;

            // Высоту ведёт табличка, а не текст: строка, обрезанная по высоте
            // (Truncate), в табличке впритык не рисуется вовсе — меру Unity
            // считает в точках экрана с округлением. Первым прогоном 15 октября
            // строка F так и стояла пустой табличкой.
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = pad;
            rt.offsetMax = -pad;

            _last = null;
            Refit();
        }

        void LateUpdate() => Refit();

        private void Refit()
        {
            if (_text == null) return;
            if (_image == null) _image = GetComponent<Image>();

            string now = _text.text;
            if (now == _last) return;
            _last = now;

            bool shown = !string.IsNullOrEmpty(now);
            _image.enabled = shown;
            if (!shown) return;

            // Сперва ширина — по длинной строке без переносов, потом высота:
            // её текст мерит по своей нынешней ширине, уже новой.
            var rt = (RectTransform)transform;
            float width = Mathf.Min(_maxWidth, Mathf.Ceil(_text.preferredWidth) + 2f);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + 2f * _pad.x);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                                         Mathf.Ceil(_text.preferredHeight) + 2f * _pad.y);
        }
    }
}
