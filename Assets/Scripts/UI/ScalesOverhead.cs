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
    ///
    /// С 06.11 и до приказа (п. 7, <see cref="Foresee"/>): пока игрок наводит приказ,
    /// над выделенными — куда склонится каждый. Те же весы, подпись — «послушается»,
    /// «вряд ли: Гнев», «не услышит».
    /// </summary>
    public class ScalesOverhead : MonoBehaviour
    {
        private sealed class Mark
        {
            public Warrior Who;
            public Collider Body;
            public Text Label;
            public bool Preview;            // весы до приказа: живут, пока игрок наводит
            public CommandKind Kind;
            public float Touched, NextLook;
            public RectTransform Root;
            public RectTransform Beam, PanFor, PanAgainst;
            public Image BowlFor, BowlAgainst;
            public Vector2 BeamAt;
            public CanvasGroup Group;
            public float Born, Angle, Swing, Aim;
            public int Side;   // 0 — ещё не решено, 1 — над головой, ±2 — справа/слева от неё
        }

        private const float Life = 3.5f, FadeOut = 0.5f, Cooldown = 10f, Size = 64f, Above = 0.35f;
        private const int Most = 2, MostForeseen = 8;
        private const float LookEvery = 0.3f;
        private static readonly Color Faint = new Color(0.70f, 0.65f, 0.55f);

        private static ScalesOverhead _instance;
        private static readonly Dictionary<Warrior, float> NextFor = new();
        private static Sprite _disc, _bowl;
        // Коромысло ходит на ту же долю наклона, что в зале (SoulCourt: 22° из 70°).
        private const float BeamGain = 22f / 70f;
        private static readonly Color Brass = new Color(0.86f, 0.74f, 0.48f);
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

        /// <summary>
        /// Весы до приказа (63-TEACHING, п. 7; как Into the Breach — намерение видно до хода):
        /// пока игрок наводит приказ — кнопку, прицел, курсор на враге, — над каждым выделенным
        /// малые весы: куда склонится, если приказать сейчас. Звать каждый кадр, пока наводит;
        /// перестал звать — гаснут сами. Счёт — без последствий для души (SoulCourt.Foresee).
        /// </summary>
        public static void Foresee(List<Warrior> who, CommandKind kind)
        {
            if (who == null || who.Count == 0 || SoulCourt.Sitting) return;
            var self = Get();
            float now = Time.unscaledTime;
            int shown = 0;
            foreach (var w in who)
            {
                if (w == null || w.IsDead) continue;
                if (shown++ >= MostForeseen) break;
                var m = self._marks.Find(x => x.Who == w && x.Preview && x.Kind == kind);
                if (m != null)
                {
                    m.Touched = now;
                    if (now < m.NextLook) continue;
                }

                float lean;
                string label;
                float muffle = Voice.MuffleFor(w);
                if (!Voice.Heard(muffle))
                {
                    lean = 0f;
                    label = Paint(Faint, Loc.T("не услышит"));
                }
                else
                {
                    var ctx = CombatDecisionContext.Imagine(w, kind, muffle);
                    if (!SoulCourt.Foresee(w, ctx, out bool obeys, out lean, out string voice, out Color colour)) continue;
                    label = obeys ? Paint(SoulCourt.ForOrderTone, Loc.T("послушается"))
                          : voice != null ? Paint(SoulCourt.AgainstTone, Loc.T("вряд ли") + ":") + " " + Paint(colour, voice)
                          : Paint(SoulCourt.AgainstTone, Loc.T("вряд ли"));
                }

                if (m == null)
                {
                    m = self.Add(w, lean, label, preview: true);
                    m.Kind = kind;
                }
                else
                {
                    m.Aim = lean;
                    if (m.Label != null) m.Label.text = label;
                }
                m.Touched = now;
                m.NextLook = now + LookEvery;
            }
        }

        private static string Paint(Color c, string s) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

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

        private Mark Add(Warrior w, float lean, string label, bool preview = false)
        {
            for (int i = _marks.Count - 1; i >= 0; i--)
                if (_marks[i].Who == w) Drop(i);
            // Больше двух — только весов до приказа: их зовёт сам игрок, и гаснут они с наведением.
            if (!preview)
            {
                int live = _marks.FindAll(x => !x.Preview).Count;
                for (int i = 0; i < _marks.Count && live >= Most;)
                {
                    if (_marks[i].Preview) { i++; continue; }
                    Drop(i);
                    live--;
                }
            }

            var root = Rect("Весы — " + w.DisplayName, transform, Vector2.zero, new Vector2(Size * 3f, Size + 34f));
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0f);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Весы как в зале (автор, 07.11: «более стилизованные весы»; до того — циферблат со
            // стрелкой): на тёмном круге стойка, коромысло с язычком и две чаши — слева «за приказ»,
            // справа «против». Перевесившая опускается и светлеет.
            Vector2 centre = new Vector2(0f, 34f + Size * 0.5f);
            Img("Обод", root, centre, new Vector2(Size + 6f, Size + 6f), new Color(0.78f, 0.70f, 0.52f, 0.95f), Disc());
            Img("Круг", root, centre, new Vector2(Size, Size), new Color(0.11f, 0.12f, 0.19f, 0.95f), Disc());
            Img("Стойка", root, centre + new Vector2(0f, -Size * 0.10f), new Vector2(3f, Size * 0.48f), Brass, null);
            Img("Основание", root, centre + new Vector2(0f, -Size * 0.34f), new Vector2(Size * 0.34f, 3.5f), Brass, null);
            var beamAt = centre + new Vector2(0f, Size * 0.14f);
            var beam = Img("Коромысло", root, beamAt, new Vector2(Size * 0.76f, 3.5f), Brass, null);
            Img("Язычок", beam, new Vector2(0f, 1.75f + Size * 0.11f), new Vector2(2.5f, Size * 0.22f), new Color(0.97f, 0.93f, 0.80f), null);
            Img("Ось", root, beamAt, new Vector2(7f, 7f), new Color(0.97f, 0.93f, 0.80f), Disc());
            var panFor = Pan("Чаша за", root, SoulCourt.ForOrderTone, out var bowlFor);
            var panAgainst = Pan("Чаша против", root, SoulCourt.AgainstTone, out var bowlAgainst);

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

            var mark = new Mark
            {
                Who = w, Body = w.GetComponentInChildren<Collider>(), Root = root, Group = group,
                Beam = beam, BeamAt = beamAt, PanFor = panFor, PanAgainst = panAgainst, BowlFor = bowlFor, BowlAgainst = bowlAgainst,
                Label = text, Preview = preview, Born = Time.unscaledTime, Touched = Time.unscaledTime, Aim = lean,
            };
            _marks.Add(mark);
            return mark;
        }

        // Показанные в этом кадре весы: место, ширина подписи (доли экрана).
        private readonly List<(Mark m, Vector2 p, float w)> _shown = new List<(Mark m, Vector2 p, float w)>();

        void LateUpdate()
        {
            var eye = Camera.main;
            float now = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _shown.Clear();
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                float age = now - m.Born;
                bool over = m.Preview ? now - m.Touched > 0.25f : age > Life;
                if (m.Root == null || m.Who == null || m.Who.IsDead || over || SoulCourt.Sitting || eye == null)
                {
                    Drop(i);
                    continue;
                }

                // Стрелка ищет своё, как в зале: качнулась, проскочила, встала.
                m.Swing += ((m.Aim - m.Angle) * 28f - m.Swing * 6.5f) * dt;
                m.Angle += m.Swing * dt;
                Weigh(m);

                Vector3 at = m.Who.transform.position;
                float top = m.Body != null ? m.Body.bounds.max.y : at.y + 1.9f;
                Vector3 v = eye.WorldToViewportPoint(new Vector3(at.x, top + Above, at.z));
                // Сам воин должен быть в кадре: весы, прижатые к краю, висели в пустоте,
                // когда он ушёл за край (кадр «сон — весы до приказа», 06.11).
                Vector3 body = eye.WorldToViewportPoint(at + Vector3.up);
                bool seen = v.z > 0.5f && !FogOfWar.Hides(m.Who)
                         && body.x > -0.02f && body.x < 1.02f && body.y > -0.02f && body.y < 1.02f;
                float alpha = m.Preview
                    ? Mathf.Min(Mathf.Clamp01(age / 0.15f), Mathf.Clamp01(1f - (now - m.Touched - 0.1f) / 0.15f))
                    : Mathf.Min(Mathf.Clamp01(age / 0.15f), Mathf.Clamp01((Life - age) / FadeOut));
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
                float w = Mathf.Max(Size + 12f, m.Label.preferredWidth + 10f) / wide;
                _shown.Add((m, new Vector2(Mathf.Clamp(spot.x, half, 1f - half), Mathf.Max(spot.y, 0.2f)), w));
            }
            Unstack();
        }

        /// <summary>
        /// Два воина рядом — весы и «послушается» ложились друг на друга (КТ1, кадр 10б).
        /// Старшие весы стоят, младшие отходят: вверх на высоту весов, а если выше некуда —
        /// вбок. Порядок по времени появления, а не по высоте: иначе при равной высоте
        /// весы менялись бы местами каждый кадр.
        /// </summary>
        private void Unstack()
        {
            if (_shown.Count == 0) return;
            var area = ((RectTransform)_canvas.transform).rect;
            float h = Mathf.Max(1f, area.height), wide = Mathf.Max(1f, area.width);
            // Высота весов с подписью — Size + 34 и обод; ещё 18 — зазор: при 42 вверху
            // циферблат нижних касался «послушается» верхних (прогон w0711za).
            float tall = (Size + 52f) / h;
            float roof = 0.80f - (Size + 40f) / h;
            float half = Size * 1.5f / wide;
            _shown.Reverse();   // _marks обходили с конца — теперь старшие первыми
            for (int a = 0; a < _shown.Count; a++)
            {
                var (m, p, w) = _shown[a];
                for (int pass = 0; pass < 4; pass++)
                {
                    bool moved = false;
                    for (int b = 0; b < a; b++)
                    {
                        var q = _shown[b];
                        float needX = (w + q.w) * 0.5f;
                        if (Mathf.Abs(p.x - q.p.x) >= needX || Mathf.Abs(p.y - q.p.y) >= tall) continue;
                        if (q.p.y + tall <= roof) p.y = q.p.y + tall;
                        else p.x = Mathf.Clamp(q.p.x + (p.x >= q.p.x ? needX : -needX), half, 1f - half);
                        moved = true;
                    }
                    if (!moved) break;
                }
                _shown[a] = (m, p, w);
                m.Root.anchorMin = m.Root.anchorMax = p;
                m.Root.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>Коромысло — на долю наклона; чаши висят отвесно на его концах, перевесившая светлее.</summary>
        private static void Weigh(Mark m)
        {
            float deg = m.Angle * BeamGain;
            m.Beam.localRotation = Quaternion.Euler(0f, 0f, deg);
            Vector2 half = Quaternion.Euler(0f, 0f, deg) * new Vector3(Size * 0.37f, 0f, 0f);
            m.PanFor.anchoredPosition = m.BeamAt - half;
            m.PanAgainst.anchoredPosition = m.BeamAt + half;
            float t = Mathf.Clamp(m.Angle / 70f, -1f, 1f);
            m.BowlFor.color = Color.Lerp(SoulCourt.ForOrderTone * 0.7f, Color.Lerp(SoulCourt.ForOrderTone, Color.white, 0.25f), Mathf.Max(0f, t));
            m.BowlAgainst.color = Color.Lerp(SoulCourt.AgainstTone * 0.7f, Color.Lerp(SoulCourt.AgainstTone, Color.white, 0.25f), Mathf.Max(0f, -t));
            var c = m.BowlFor.color; c.a = 1f; m.BowlFor.color = c;
            c = m.BowlAgainst.color; c.a = 1f; m.BowlAgainst.color = c;
        }

        /// <summary>Чаша на шнуре: точка подвеса — верх (pivot), под ней шнур, край и чаша.</summary>
        private static RectTransform Pan(string name, RectTransform root, Color tone, out Image bowl)
        {
            float h = Size * 0.32f;
            var pan = Rect(name, root, Vector2.zero, new Vector2(Size * 0.34f, h));
            pan.pivot = new Vector2(0.5f, 1f);
            Img("Шнур", pan, new Vector2(0f, h - Size * 0.08f), new Vector2(1.5f, Size * 0.16f), new Color(0.75f, 0.66f, 0.48f), null);
            var cup = Img("Чаша", pan, new Vector2(0f, h - Size * 0.16f - Size * 0.075f), new Vector2(Size * 0.32f, Size * 0.15f), tone, Bowl());
            Img("Край", pan, new Vector2(0f, h - Size * 0.16f), new Vector2(Size * 0.36f, 2.5f), Brass, null);
            bowl = cup.GetComponent<Image>();
            return pan;
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
        /// <summary>Нижняя половина круга — чаша весов.</summary>
        private static Sprite Bowl()
        {
            if (_bowl != null) return _bowl;
            const int w = 64, h = 32;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Чаша весов" };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - w * 0.5f, dy = h - (y + 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(w * 0.5f - d)));
                }
            t.Apply();
            _bowl = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _bowl;
        }

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
