// Assets/Scripts/Gameplay/EventPlaque.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Плашка события экспедиции (MapEvent) — та же, что у обучения и конца партии: тёмная, золотая рамка,
    /// внизу экрана. Одна на сцену: событие просит её каждый кадр (<see cref="Ask"/>), не попросили — прячется.
    /// Вместо временного окна IMGUI.
    /// </summary>
    public class EventPlaque : MonoBehaviour
    {
        private static EventPlaque _one;
        private Text _title, _body;
        private GameObject _panel;
        private int _askedFrame = -1;

        /// <summary>Показать в этом кадре.</summary>
        public static void Ask(string title, string body)
        {
            if (_one == null) _one = new GameObject("Плашка события").AddComponent<EventPlaque>();
            _one.Set(title, body);
        }

        private void Set(string title, string body)
        {
            if (_panel == null) Build();
            _title.text = title;
            _body.text = body;
            _askedFrame = Time.frameCount;
            if (!_panel.activeSelf) _panel.SetActive(true);
        }

        void LateUpdate()
        {
            if (_panel != null && _panel.activeSelf && _askedFrame < Time.frameCount) _panel.SetActive(false);
        }

        void OnDestroy() { if (_one == this) _one = null; }

        private void Build()
        {
            var canvasGo = new GameObject("Плашка события — холст", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            _panel = new GameObject("Плашка", typeof(RectTransform), typeof(Image), typeof(Outline));
            var rt = (RectTransform)_panel.transform;
            rt.SetParent(canvasGo.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 190f);
            rt.sizeDelta = new Vector2(760f, 176f);
            _panel.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.06f, 1f);
            var edge = _panel.GetComponent<Outline>();
            edge.effectColor = new Color(0.72f, 0.58f, 0.30f, 0.9f);
            edge.effectDistance = new Vector2(2f, 2f);

            var font = Resources.Load<Font>("Fonts/PTSerif-Regular");
            _title = Label(rt, font, 21, new Color(0.86f, 0.70f, 0.38f), new Vector2(0f, -10f), 30f);
            _body = Label(rt, font, 17, new Color(0.93f, 0.89f, 0.80f), new Vector2(0f, -40f), 130f);
        }

        private static Text Label(RectTransform parent, Font font, int size, Color colour, Vector2 at, float height)
        {
            var go = new GameObject("Строка", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(-36f, height);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = colour;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
