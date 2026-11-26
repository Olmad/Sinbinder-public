// Assets/Scripts/UI/MiniMap.cs
// Перевод: текст через Loc
//
// Мини-карта — выдвижная (решение автора 01.10, 42-INTERFACE §5: «мини-карта всё-таки нужна, но чтобы она была
// выдвигаемой»; 26.11: «Думаю, она нам необходима — на больших картах можно легко потеряться»).
//
// - Выезжает вниз из-под названия места (ярлык слева вверху); K или щелчок по ярлыку. M занята приказом «идти».
// - По умолчанию: на больших картах (сон, экспедиции, поля) открыта, в лагере, склепе и полигоне — закрыта;
//   выбор игрока запоминается.
// - Сама, на событие: закрыта, а рядом показался враг — выезжает на 3 секунды.
// - Только видимое: враг в тумане войны (FogOfWar.Hides) на карте не стоит.
// - Земля — где можно пройти (навигационная сетка): тропы, поляны и проходы светлее, остальное — тьма.
// - Знаки: Греховод — стрелка по взгляду, свои — зелёные, враги — красные, мирные — янтарные; цели мест —
//   ромбы с подписью (Mark/Unmark), за краем — у края, в их сторону.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    public class MiniMap : MonoBehaviour
    {
        public static KeyCode Key => Sinbinder.Core.Controls.Key(Sinbinder.Core.Act.Map);

        private const float Span = 96f;          // метров по стороне видимого окна
        private const float RasterSpan = 160f;   // метров в запечённой земле — с запасом на ходьбу
        private const int RasterPx = 320;        // полметра на точку
        private const float Side = 340f;         // сторона карты в единицах холста (UiStyle.Reference)
        private const float Inset = 10f;

        private static MiniMap _instance;

        public struct MarkInfo { public Vector3 At; public Color Colour; public string Label; }
        private static readonly Dictionary<string, MarkInfo> Marks = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { _instance = null; Marks.Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Мини-карта");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MiniMap>();
        }

        /// <summary>Знак места на карте: цель, выход, засада. Тот же id — переставить.</summary>
        public static void Mark(string id, Vector3 at, Color colour, string label)
        {
            if (!string.IsNullOrEmpty(id)) Marks[id] = new MarkInfo { At = at, Colour = colour, Label = label };
        }

        public static void Unmark(string id) { if (!string.IsNullOrEmpty(id)) Marks.Remove(id); }

        /// <summary>Открыта ли (выехала) — для проверок.</summary>
        public static bool Opened => _instance != null && _instance._open;
        public static bool Shown => _instance != null && _instance._canvas != null && _instance._canvas.enabled;

        /// <summary>Проверке и игроку без клавиатуры: то же, что K.</summary>
        public static void Toggle()
        {
            if (_instance == null) return;
            _instance._open = !_instance._open;
            _instance._peekUntil = 0f;
            try { Sinbinder.Core.Prefs.SetInt(_instance.PrefKey, _instance._open ? 1 : 0); } catch { }
        }

        private Canvas _canvas;
        private RectTransform _sheet, _dots;
        private RawImage _ground;
        private Text _title;
        private Texture2D _raster;
        private Color32[] _pixels;
        private Vector3 _rasterCentre;
        private bool _rasterValid;
        private NavMeshTriangulation _tri;
        private int _triKey = int.MinValue;
        private bool _open;
        private string _openFor;
        private float _slide;            // 0 — задвинута, 1 — выехала
        private float _peekUntil, _nextScan, _nextRaster;
        private int _enemiesNear;
        private readonly List<Warrior> _warriors = new();
        private readonly List<Image> _pool = new();
        private readonly List<MarkInfo> _shown = new();
        private readonly List<Text> _labels = new();
        private Image _hero;
        private static Sprite _dot, _diamond, _arrow;

        private void Awake()
        {
            Build();
            SceneManager.activeSceneChanged += (_, __) => { _openFor = null; _rasterValid = false; };
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 35;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiStyle.Reference;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Ярлык места — за него карта и выдвигается.
            var tab = Rect("Название места", transform, new Vector2(28f, -22f), new Vector2(Side, 44f));
            var tabImage = tab.gameObject.AddComponent<Image>();
            UiStyle.Dress(tabImage, UiStyle.PlateWeight.Strip);
            var button = tab.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Toggle);
            _title = Label(tab, "", 21, TextAnchor.MiddleCenter);
            _title.rectTransform.offsetMin = new Vector2(10f, 0f);
            _title.rectTransform.offsetMax = new Vector2(-10f, 0f);

            // Окно под ярлыком: всё, что выше его верха, обрезано — лист «выезжает из-под» названия.
            var window = Rect("Окно карты", transform, new Vector2(28f, -70f), new Vector2(Side, Side));
            window.gameObject.AddComponent<RectMask2D>();

            _sheet = Rect("Лист", window, Vector2.zero, new Vector2(Side, Side));
            var frame = _sheet.gameObject.AddComponent<Image>();
            UiStyle.Dress(frame, UiStyle.PlateWeight.Board);
            frame.raycastTarget = false;

            var groundRt = Rect("Земля", _sheet, new Vector2(Inset, -Inset), new Vector2(Side - Inset * 2f, Side - Inset * 2f));
            _ground = groundRt.gameObject.AddComponent<RawImage>();
            _ground.raycastTarget = false;
            _raster = new Texture2D(RasterPx, RasterPx, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Мини-карта · земля" };
            _pixels = new Color32[RasterPx * RasterPx];
            _ground.texture = _raster;

            _dots = Rect("Знаки", groundRt, Vector2.zero, groundRt.sizeDelta);
            _hero = Dot(Arrow, Color.white, 22f);
            _hero.transform.SetAsLastSibling();
        }

        // ── кадр ──

        private void Update()
        {
            bool can = SinbinderPlayer.Exists && !Hidden();
            if (_canvas.enabled != can) _canvas.enabled = can;
            if (!can) return;

            string key = PrefKey;
            if (_openFor != key)
            {
                _openFor = key;
                int saved = -1;
                try { saved = Sinbinder.Core.Prefs.GetInt(key, -1); } catch { }
                _open = saved < 0 ? Large() : saved == 1;
                _slide = _open ? 1f : 0f;
                _title.text = Place();
            }

            if (Input.GetKeyDown(Key) && !TextFieldFocused()) Toggle();

            var hero = SinbinderPlayer.Instance;
            var at = hero.transform.position;

            if (Time.unscaledTime >= _nextScan) Scan(at);

            bool out_ = _open || Time.unscaledTime < _peekUntil;
            _slide = Mathf.MoveTowards(_slide, out_ ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(Side + 8f, 0f, Smooth(_slide)));
            if (_slide <= 0f) return;

            int triKey = SceneManager.GetActiveScene().handle * 2 + (PersonalExpedition.Active ? 1 : 0);
            if (triKey != _triKey) { _triKey = triKey; _tri = NavMesh.CalculateTriangulation(); _rasterValid = false; }
            var off = at - _rasterCentre; off.y = 0f;
            if ((!_rasterValid || off.magnitude > (RasterSpan - Span) * 0.5f - 4f) && Time.unscaledTime >= _nextRaster)
            { Raster(at); _nextRaster = Time.unscaledTime + 0.5f; }

            float u = (at.x - Span * 0.5f - (_rasterCentre.x - RasterSpan * 0.5f)) / RasterSpan;
            float v = (at.z - Span * 0.5f - (_rasterCentre.z - RasterSpan * 0.5f)) / RasterSpan;
            _ground.uvRect = new Rect(u, v, Span / RasterSpan, Span / RasterSpan);

            Draw(hero, at);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Кино, разговор, окно, пауза — карты нет: она не застит то, на что смотрят.</summary>
        private static bool Hidden()
        {
            var bars = Letterbox.Instance;
            if (bars != null && bars.Covering) return true;
            if (TalkView.Shown || GearPanel.Open || Modal.Any) return true;
            var pause = GamePauseController.Instance;
            return pause != null && pause.IsPaused;
        }

        private static bool TextFieldFocused()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            return go != null && go.GetComponent<InputField>() != null;
        }

        /// <summary>Большая карта — где легко потеряться: сон, экспедиции, поля. Лагерь, склеп и полигон — малые.</summary>
        private static bool Large()
        {
            if (PersonalExpedition.Active) return true;
            switch (SceneManager.GetActiveScene().name)
            {
                case "Prologue_Camp": case "Crypt_Entrance": case "Crypt_Test": return false;
                default: return true;
            }
        }

        private string PrefKey => "minimap.open." + (PersonalExpedition.Active ? "expedition" : SceneManager.GetActiveScene().name);

        private static string Place()
        {
            string t = PersonalExpedition.CurrentTitle;
            if (!string.IsNullOrEmpty(t)) return Loc.T(t) + "  ·  " + Sinbinder.Core.Controls.Name(Sinbinder.Core.Act.Map);
            string name;
            switch (SceneManager.GetActiveScene().name)
            {
                case "Prologue_Camp": name = Loc.T("Лагерь"); break;
                case "Prologue_Dream": name = Loc.T("Сон"); break;
                case "Prologue_Raid": name = Loc.T("Набег"); break;
                case "Crypt_Entrance": name = Loc.T("Склеп"); break;
                case "Crypt_Test": name = Loc.T("Полигон"); break;
                default: name = Loc.T("Карта"); break;
            }
            return name + "  ·  " + Sinbinder.Core.Controls.Name(Sinbinder.Core.Act.Map);
        }

        // ── кто где ──

        private void Scan(Vector3 at)
        {
            _nextScan = Time.unscaledTime + 0.5f;
            _warriors.Clear();
            int enemies = 0;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w is SinbinderPlayer || !w.gameObject.activeInHierarchy) continue;
                if (w.Team == Team.Enemy && FogOfWar.Hides(w)) continue;
                _warriors.Add(w);
                if (w.Team == Team.Enemy && Flat(w.transform.position - at) < Span * 0.5f) enemies++;
            }
            // Сама, на событие (42 §5): враг показался рядом, а карта задвинута — выезжает на 3 секунды.
            if (enemies > _enemiesNear && !_open) _peekUntil = Time.unscaledTime + 3f;
            _enemiesNear = enemies;
        }

        private void Draw(SinbinderPlayer hero, Vector3 at)
        {
            float w = Side - Inset * 2f;
            int used = 0;
            foreach (var u in _warriors)
            {
                if (u == null || u.IsDead) continue;
                var local = ToMap(u.transform.position, at, w, out bool inside);
                if (!inside) continue;
                Color c = u.Team == Team.Player ? new Color(0.42f, 0.86f, 0.42f)
                        : u.Team == Team.Enemy ? new Color(0.92f, 0.28f, 0.22f) : new Color(0.95f, 0.72f, 0.30f);
                var img = Take(used++, Dotted, c, 10f);
                img.rectTransform.anchoredPosition = local;
                img.rectTransform.localRotation = Quaternion.identity;
            }

            int label = 0;
            // Место нынешнего шага (стрелка задач, TaskList.Place) — тот же знак и на карте.
            _shown.Clear();
            foreach (var kv in Marks) _shown.Add(kv.Value);
            if (TaskList.Placed(out var word, out var spot))
                _shown.Add(new MarkInfo { At = spot, Colour = new Color(0.96f, 0.80f, 0.42f), Label = word });
            foreach (var m in _shown)
            {
                var local = ToMap(m.At, at, w, out bool inside);
                var img = Take(used++, Diamond, m.Colour, inside ? 16f : 12f);
                img.rectTransform.anchoredPosition = local;
                img.rectTransform.localRotation = Quaternion.identity;
                var text = TakeLabel(label++);
                text.text = string.IsNullOrEmpty(m.Label) ? "" : Loc.T(m.Label);
                text.color = m.Colour;
                // Подпись — внутрь карты от знака: у правого края слева от него.
                bool right = local.x > w * 0.62f;
                text.alignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                text.rectTransform.anchoredPosition = local + new Vector2(right ? -118f : 12f, 0f);
            }
            for (int i = used; i < _pool.Count; i++) if (_pool[i].enabled) _pool[i].enabled = false;
            for (int i = label; i < _labels.Count; i++) if (_labels[i].enabled) _labels[i].enabled = false;

            _hero.rectTransform.anchoredPosition = new Vector2(w * 0.5f, -w * 0.5f);
            var cam = Camera.main;
            float yaw = cam != null && cam.GetComponent<RTS_Camera>() is RTS_Camera rts && rts.FirstPersonNow
                ? cam.transform.eulerAngles.y : hero.transform.eulerAngles.y;
            _hero.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            _hero.transform.SetAsLastSibling();
        }

        /// <summary>Мир → лист: север вверх, Греховод в середине. Вне окна — к краю (inside=false).</summary>
        private static Vector2 ToMap(Vector3 p, Vector3 centre, float w, out bool inside)
        {
            float x = (p.x - centre.x) / Span + 0.5f;
            float y = (p.z - centre.z) / Span + 0.5f;
            inside = x >= 0f && x <= 1f && y >= 0f && y <= 1f;
            if (!inside)
            {
                // К краю по направлению от середины: туда идти.
                var d = new Vector2(x - 0.5f, y - 0.5f);
                float k = 0.47f / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                x = 0.5f + d.x * k; y = 0.5f + d.y * k;
            }
            return new Vector2(x * w, -(1f - y) * w);
        }

        // ── земля ──

        /// <summary>
        /// Где можно пройти — светлее: треугольники навигационной сетки вокруг Греховода, запечённые в картинку.
        /// Пересчёт — когда он отошёл от середины запечённого куска; сетка — раз на сцену и вход/выход экспедиции.
        /// </summary>
        private void Raster(Vector3 centre)
        {
            _rasterCentre = new Vector3(centre.x, 0f, centre.z);
            _rasterValid = true;
            var dark = new Color32(14, 12, 11, 235);
            var lit = new Color32(92, 80, 62, 245);
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = dark;

            var verts = _tri.vertices;
            var idx = _tri.indices;
            if (verts != null && idx != null)
            {
                float x0 = _rasterCentre.x - RasterSpan * 0.5f, z0 = _rasterCentre.z - RasterSpan * 0.5f;
                float k = RasterPx / RasterSpan;
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    var a = verts[idx[t]]; var b = verts[idx[t + 1]]; var c = verts[idx[t + 2]];
                    // Другой ярус (карта экспедиции под склепом, мост над рекой) — не здесь.
                    if (Mathf.Abs(a.y - centre.y) > 12f) continue;
                    Fill(new Vector2((a.x - x0) * k, (a.z - z0) * k), new Vector2((b.x - x0) * k, (b.z - z0) * k),
                         new Vector2((c.x - x0) * k, (c.z - z0) * k), lit);
                }
            }
            _raster.SetPixels32(_pixels);
            _raster.Apply(false);
        }

        private void Fill(Vector2 a, Vector2 b, Vector2 c, Color32 colour)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(RasterPx - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(RasterPx - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            if (minX > maxX || minY > maxY) return;
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < 1e-5f) return;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross(b - a, p - a) / area, w1 = Cross(c - b, p - b) / area, w2 = Cross(a - c, p - c) / area;
                    // Полпикселя запаса: щели между соседними треугольниками не остаются чёрными нитками.
                    if (w0 >= -0.02f && w1 >= -0.02f && w2 >= -0.02f) _pixels[y * RasterPx + x] = colour;
                }
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        // ── рисунки ──

        private Image Take(int i, Sprite sprite, Color c, float size)
        {
            while (_pool.Count <= i) _pool.Add(Dot(Dotted, Color.white, 10f));
            var img = _pool[i];
            img.enabled = true;
            img.sprite = sprite;
            img.color = c;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        private Text TakeLabel(int i)
        {
            while (_labels.Count <= i)
            {
                var rt = Rect("Подпись", _dots, Vector2.zero, new Vector2(110f, 26f));
                rt.pivot = new Vector2(0f, 0.5f);
                var t = Label(rt, "", 17, TextAnchor.MiddleLeft);
                t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
                _labels.Add(t);
            }
            var label = _labels[i];
            label.enabled = true;
            return label;
        }

        private Image Dot(Sprite sprite, Color c, float size)
        {
            var rt = Rect("Знак", _dots, Vector2.zero, new Vector2(size, size));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ?? Dotted;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite Dotted => _dot ??= Shape(32, (x, y) => (x * x + y * y) <= 1f ? 1f : 0f, true);
        private static Sprite Diamond => _diamond ??= Shape(32, (x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 0.95f ? 1f : 0f, true);
        private static Sprite Arrow => _arrow ??= Shape(32, (x, y) =>
        {
            // Наконечник вверх: острие (0, 1), крылья (±0.75, −0.85), выемка до (0, −0.4).
            float yy = (y + 0.85f) / 1.85f;
            if (yy < 0f || yy > 1f) return 0f;
            if (Mathf.Abs(x) > (1f - yy) * 0.75f) return 0f;
            return y < -0.4f - 0.6f * Mathf.Abs(x) ? 0f : 1f;
        }, true);

        private static Sprite Shape(int px, System.Func<float, float, float> inside, bool rim)
        {
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[px * px];
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    float fx = (x + 0.5f) / px * 2f - 1f, fy = (y + 0.5f) / px * 2f - 1f;
                    float a = inside(fx * 1.08f, fy * 1.08f);
                    float core = inside(fx * 1.3f, fy * 1.3f);
                    // Тёмный ободок — знак читается и на светлой тропе.
                    byte v = (byte)(core > 0f || !rim ? 255 : 30);
                    cols[y * px + x] = new Color32(v, v, v, (byte)(a > 0f ? 255 : 0));
                }
            tex.SetPixels32(cols);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text Label(RectTransform parent, string text, int size, TextAnchor align)
        {
            var go = new GameObject("Строка", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = UiStyle.Body;
            t.fontSize = size;
            t.text = text;
            t.alignment = align;
            t.color = new Color(0.90f, 0.84f, 0.72f);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }
    }
}
