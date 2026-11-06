// Assets/Scripts/UI/ScalesOverhead.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Малые весы над головой в миг отказа (docs/63-TEACHING.md, п. 3): та же
    /// картинка, что в суде души во сне, — стрелка качается к «против», под ней
    /// громче всех голос той стороны, его цветом. Суд игрок видел однажды; здесь
    /// он узнаёт его за секунду, не открывая подсказки.
    ///
    /// Три с половиной секунды — и гаснут. Не чаще раза в 10 с на воина и не
    /// больше двух сразу: отказывающий отказывает на каждом решении, а весы над
    /// всеми — каша. В суде (мир — зал) не показываются.
    /// </summary>
    public class ScalesOverhead : MonoBehaviour
    {
        private sealed class Mark
        {
            public Warrior Who;
            public Collider Body;
            public RectTransform Root;
            public RectTransform Needle;
            public CanvasGroup Group;
            public float Born, Angle, Swing, Aim;
            public int Side;   // 0 — ещё не решено, 1 — над головой, ±2 — справа/слева от неё
        }

        private const float Life = 3.5f, FadeOut = 0.5f, Cooldown = 10f, Size = 64f, Above = 0.35f;
        private const int Most = 2;

        private static ScalesOverhead _instance;
        private static readonly Dictionary<Warrior, float> NextFor = new();
        private static Sprite _disc;
        private readonly List<Mark> _marks = new();
        private Canvas _canvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { _instance = null; NextFor.Clear(); }

        /// <summary>
        /// Показать весы этого решения над воином, если ему сейчас можно. <paramref name="force"/> —
        /// мимо паузы между показами: сон показывает их перед судом всегда, даже если отказ
        /// в мире только что уже показал их (прогон 06.11: показ перед судом съела пауза).
        /// </summary>
        public static void Show(Warrior w, Decision d, DecisionContext c, bool force = false)
        {
            if (w == null || w.IsDead || SoulCourt.Sitting) return;
            if (!force && !Transparency.Shows(Detail.Moments)) return;
            float now = Time.unscaledTime;
            if (!force && NextFor.TryGetValue(w, out float next) && now < next) return;
            if (!SoulCourt.Lean(w, d, c, out float lean, out string voice, out Color colour)) return;
            NextFor[w] = now + Cooldown;

            string label = d.Hesitated
                ? Loc.T("вровень")
                : $"<color=#{ColorUtility.ToHtmlStringRGB(d.RefusedCommand ? SoulCourt.AgainstTone : SoulCourt.ForOrderTone)}>"
                  + (d.RefusedCommand ? Loc.T("против") : Loc.T("за приказ")) + ":</color> "
                  + $"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{voice}</color>";
            Get().Add(w, lean, label);
        }

        private static ScalesOverhead Get()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("Весы над головой");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ScalesOverhead>();
            _instance._canvas = go.AddComponent<Canvas>();
            _instance._canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _instance._canvas.sortingOrder = 30;   // под полосами, панелями и подсказкой
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiStyle.Reference;
            scaler.matchWidthOrHeight = 0.5f;
            return _instance;
        }

        private void Add(Warrior w, float lean, string label)
        {
            for (int i = _marks.Count - 1; i >= 0; i--)
                if (_marks[i].Who == w) Drop(i);
            while (_marks.Count >= Most) Drop(0);

            var root = Rect("Весы — " + w.DisplayName, transform, Vector2.zero, new Vector2(Size * 3f, Size + 34f));
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0f);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Циферблат как в зале: обод цвета кости, тёмный круг, слева «за», справа «против», вверху — середина.
            Vector2 centre = new Vector2(0f, 34f + Size * 0.5f);
            Img("Обод", root, centre, new Vector2(Size + 6f, Size + 6f), new Color(0.78f, 0.70f, 0.52f, 0.95f), Disc());
            Img("Круг", root, centre, new Vector2(Size, Size), new Color(0.11f, 0.12f, 0.19f, 0.95f), Disc());
            Img("За", root, centre + new Vector2(-Size * 0.34f, 0f), new Vector2(11f, 4f), SoulCourt.ForOrderTone, null);
            Img("Против", root, centre + new Vector2(Size * 0.34f, 0f), new Vector2(11f, 4f), SoulCourt.AgainstTone, null);
            Img("Середина", root, centre + new Vector2(0f, Size * 0.34f), new Vector2(3f, 10f), new Color(0.92f, 0.90f, 0.86f), null);
            var needle = Img("Стрелка", root, centre, new Vector2(3.5f, Size * 0.40f), new Color(0.96f, 0.91f, 0.78f), null);
            needle.pivot = new Vector2(0.5f, 0f);
            needle.anchoredPosition = centre;
            Img("Ось", root, centre, new Vector2(8f, 8f), new Color(0.96f, 0.91f, 0.78f), Disc());

            var textRect = Rect("Голос", root, new Vector2(0f, 0f), new Vector2(Size * 3f, 30f));
            textRect.pivot = new Vector2(0.5f, 0f);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = UiStyle.BodyBold;
            text.fontSize = 22;
            text.alignment = TextAnchor.LowerCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = label;
            var outline = textRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            _marks.Add(new Mark
            {
                Who = w, Body = w.GetComponentInChildren<Collider>(), Root = root, Needle = needle, Group = group,
                Born = Time.unscaledTime, Aim = lean,
            });
        }

        void LateUpdate()
        {
            var eye = Camera.main;
            float now = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                float age = now - m.Born;
                if (m.Root == null || m.Who == null || m.Who.IsDead || age > Life || SoulCourt.Sitting || eye == null)
                {
                    Drop(i);
                    continue;
                }

                // Стрелка ищет своё, как в зале: качнулась, проскочила, встала.
                m.Swing += ((m.Aim - m.Angle) * 28f - m.Swing * 6.5f) * dt;
                m.Angle += m.Swing * dt;
                m.Needle.localRotation = Quaternion.Euler(0f, 0f, m.Angle);

                Vector3 at = m.Who.transform.position;
                float top = m.Body != null ? m.Body.bounds.max.y : at.y + 1.9f;
                Vector3 v = eye.WorldToViewportPoint(new Vector3(at.x, top + Above, at.z));
                bool seen = v.z > 0.5f && !FogOfWar.Hides(m.Who);
                float alpha = Mathf.Min(Mathf.Clamp01(age / 0.15f), Mathf.Clamp01((Life - age) / FadeOut));
                m.Group.alpha = seen ? alpha : 0f;
                if (!seen) continue;
                // По долям экрана, как подписи суда: пиксели экрана и кадра в пакетном прогоне
                // расходятся. Не за край и не выше 80% высоты: на крупном плане (сон, миг решения)
                // голова у верха кадра, и весы над ней уходили под чёрную полосу (прогон 06.11).
                var area = ((RectTransform)_canvas.transform).rect;
                float h = Mathf.Max(1f, area.height), wide = Mathf.Max(1f, area.width);
                float ceiling = 0.80f - (Size + 40f) / h;
                float half = Size * 1.5f / wide;
                // Места над головой нет — весы сбоку, на высоте головы, а не на лбу (прогон 06.11).
                // С запасом: пока камера едет, весы не прыгают с места на место.
                bool room = m.Side == 1 ? v.y <= ceiling + 0.04f : v.y <= ceiling - 0.04f;
                if (room) m.Side = 1;
                else if (m.Side == 1 || m.Side == 0) m.Side = v.x <= 0.5f ? 2 : -2;
                else if (m.Side == 2 && v.x > 0.62f) m.Side = -2;
                else if (m.Side == -2 && v.x < 0.38f) m.Side = 2;
                Vector2 spot = new Vector2(v.x, Mathf.Min(v.y, ceiling));
                if (m.Side != 1)
                {
                    // Сбоку от головы: от её края — полкруга циферблата и ещё чуть-чуть.
                    Vector3 brow = new Vector3(at.x, top - 0.18f, at.z);
                    Vector3 mid = eye.WorldToViewportPoint(brow);
                    Vector3 edge = eye.WorldToViewportPoint(brow + eye.transform.right * 0.13f);
                    float off = Mathf.Abs(edge.x - mid.x) + (Size * 0.5f + 40f) / wide;
                    spot = new Vector2(mid.x + Mathf.Sign(m.Side) * off,
                                       Mathf.Min(mid.y - (34f + Size * 0.5f) / h, ceiling));
                }
                m.Root.anchorMin = m.Root.anchorMax = new Vector2(Mathf.Clamp(spot.x, half, 1f - half), Mathf.Max(spot.y, 0.2f));
                m.Root.anchoredPosition = Vector2.zero;
            }
        }

        private void Drop(int i)
        {
            if (_marks[i].Root != null) Destroy(_marks[i].Root.gameObject);
            _marks.RemoveAt(i);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 at, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = at;
            return rt;
        }

        private static RectTransform Img(string name, RectTransform parent, Vector2 at, Vector2 size, Color colour, Sprite sprite)
        {
            var rt = Rect(name, parent, at, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = colour;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>Круг с мягким краем — формулой: встроенных круглых картинок в сборке нет.</summary>
        private static Sprite Disc()
        {
            if (_disc != null) return _disc;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Круг весов" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(n * 0.5f - d)));
                }
            t.Apply();
            _disc = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _disc;
        }
    }
}
