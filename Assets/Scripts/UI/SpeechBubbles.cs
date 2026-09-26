// Assets/Scripts/UI/SpeechBubbles.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Строка над головой — то, что воин сказал вслух (разговоры у костра,
    /// <see cref="CampTalk"/>). Держится несколько секунд и идёт за ним.
    /// Одна строка на воина: новая сменяет старую.
    ///
    /// Первая ступень ясности и выше: это не объяснение, а то, что слышно
    /// в лагере. Чисел нет — строки пишет банк реплик.
    ///
    /// Ставит себя сам и живёт между сценами.
    /// </summary>
    public class SpeechBubbles : MonoBehaviour
    {
        private static SpeechBubbles _instance;

        private sealed class Bubble
        {
            public Warrior Who;
            public Text Line;
            public float Until;
            public int Said;
        }

        /// <summary>Зазор между разведёнными строками, в точках экрана.</summary>
        private const float Gap = 4f;

        private readonly List<Bubble> _live = new();
        private int _said;
        private readonly List<Bubble> _order = new();
        private readonly List<Rect> _taken = new();
        private Canvas _canvas;
        private Font _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Реплики над головой");
            _instance = go.AddComponent<SpeechBubbles>();
            DontDestroyOnLoad(go);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Сказать вслух: строка над головой на столько секунд.</summary>
        public static void Say(Warrior who, string line, float seconds = 4.5f)
        {
            if (_instance == null || who == null || string.IsNullOrEmpty(line)) return;
            if (!Core.Transparency.Shows(Core.Clarity.Icons)) return;
            _instance.Put(who, line, seconds);
        }

        private void Put(Warrior who, string line, float seconds)
        {
            if (_canvas == null) Build();

            var bubble = _live.Find(b => b.Who == who);
            if (bubble == null)
            {
                var go = new GameObject("Реплика", typeof(RectTransform), typeof(Text));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_canvas.transform, false);
                rt.sizeDelta = new Vector2(380f, 90f);
                rt.pivot = new Vector2(0.5f, 0f);

                var text = go.GetComponent<Text>();
                text.font = _font;
                text.fontSize = 20;
                text.alignment = TextAnchor.LowerCenter;
                text.color = new Color(0.95f, 0.91f, 0.80f);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

                bubble = new Bubble { Who = who, Line = text };
                _live.Add(bubble);
            }

            bubble.Line.text = line;
            bubble.Until = Time.unscaledTime + seconds;
            bubble.Said = ++_said;
        }

        void LateUpdate()
        {
            if (_live.Count == 0) return;
            var cam = Camera.main;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var b = _live[i];
                if (b.Who == null || b.Who.IsDead || Time.unscaledTime > b.Until || b.Line == null)
                {
                    if (b.Line != null) Destroy(b.Line.gameObject);
                    _live.RemoveAt(i);
                    continue;
                }

                if (cam == null) { b.Line.enabled = false; continue; }

                var screen = cam.WorldToScreenPoint(b.Who.transform.position + Vector3.up * 2.3f);
                b.Line.enabled = screen.z > 0f;
                if (b.Line.enabled) b.Line.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
            }

            Unstack();
        }

        /// <summary>
        /// Развести строки, легшие одна на другую.
        ///
        /// Пару для разговора <see cref="CampTalk"/> берёт из тех, кто стоит
        /// ближе трёх с половиной метров, а с высоты тактической камеры это
        /// сотня точек при строке шириной в триста восемьдесят: реплика
        /// и ответ рисовались в одном месте и не читались — два кадра
        /// из трёх в прогоне 25 сентября.
        ///
        /// Последняя сказанная строка стоит над говорящим, раньше сказанные
        /// поднимаются над ней: читается сверху вниз в том порядке,
        /// в каком говорили. Места считаются заново каждый кадр от голов,
        /// так что строка возвращается к своему, как только разошлись.
        /// </summary>
        private void Unstack()
        {
            _order.Clear();
            foreach (var b in _live)
                if (b.Line != null && b.Line.enabled) _order.Add(b);

            if (_order.Count < 2) return;

            // Свежие первыми. Счётчик, а не часы: двух одинаковых у него
            // не бывает, и порядок не зависит от того, как легла сортировка.
            _order.Sort((x, y) => y.Said.CompareTo(x.Said));

            _taken.Clear();
            foreach (var b in _order)
            {
                var rt = b.Line.rectTransform;
                var at = rt.position;
                float half = Mathf.Min(b.Line.preferredWidth, rt.rect.width) * 0.5f;
                float tall = b.Line.preferredHeight;

                // Подняли над одной — могли лечь на другую: проверяем, пока
                // не встанет на свободное. Больше строк, чем есть, подъёмов
                // не бывает.
                for (int pass = 0; pass <= _taken.Count; pass++)
                {
                    bool moved = false;
                    foreach (var r in _taken)
                    {
                        bool across = Mathf.Abs(at.x - r.center.x) < half + r.width * 0.5f;
                        bool over = at.y < r.yMax && at.y + tall > r.yMin;
                        if (!across || !over) continue;

                        at.y = r.yMax + Gap;
                        moved = true;
                    }
                    if (!moved) break;
                }

                rt.position = at;
                _taken.Add(new Rect(at.x - half, at.y, half * 2f, tall));
            }
        }

        private void Build()
        {
            var go = new GameObject("Холст реплик", typeof(Canvas));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;

            var any = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            _font = any != null && any.font != null ? any.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
