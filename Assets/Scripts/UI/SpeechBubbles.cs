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
        }

        private readonly List<Bubble> _live = new();
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
