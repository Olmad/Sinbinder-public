// Assets/Scripts/UI/TalkView.cs
// Перевод: текст через Loc
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Разговор с воином — как в Skyrim и Fallout 3–4 (автор, 21 октября:
    /// «сделай диалоговое окно с воинами больше в стиле Skyrim или Fallout»).
    ///
    /// До 22 октября разговор был коробкой посреди экрана поверх лагеря:
    /// говорящего не видно, реплика — строкой в низу коробки. Теперь камера
    /// подъезжает к лицу (<see cref="Dialogue.DialogueCameraController"/>),
    /// реплика печатается субтитром на нижней полосе (<see cref="Letterbox"/>),
    /// а ответы — короткий список справа, без коробок: номер, строка, под ней
    /// мелко — что будет. Наведённый горит старым золотом.
    ///
    /// Содержание — у <see cref="GearPanel"/> (её пункты, её клавиши 1–3, F/Esc);
    /// здесь только вид.
    /// </summary>
    public class TalkView : MonoBehaviour
    {
        public struct Option
        {
            public string Text;
            public string Sub;
            public Action Act;
        }

        private static TalkView _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        public static bool Shown => _instance != null && _instance._canvas != null && _instance._canvas.enabled;

        private Canvas _canvas;
        private RectTransform _list;
        private Text _footer;
        private readonly List<GameObject> _rows = new();
        private string _lastLine;

        private static TalkView Get()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("Разговор вблизи");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TalkView>();
            _instance.Build();
            return _instance;
        }

        /// <summary>
        /// Показать: что сказал воин (субтитром) и что можно ответить.
        /// <paramref name="voice"/> — кто говорит: его голос звучит, пока
        /// строка печатается (автор, отзыв 26.10, п. 20: «при диалоге про
        /// знамя нет голоса» — его не было ни в одном разговоре вблизи).
        /// </summary>
        public static void Show(string speaker, string line, IList<Option> options, string footer,
                                Gameplay.Warrior voice = null)
        {
            var v = Get();
            v._canvas.enabled = true;

            // Реплику — заново, только если она новая: иначе перерисовка меню
            // перепечатывала бы ту же строку с начала.
            string said = speaker + "|" + line;
            if (said != v._lastLine)
            {
                v._lastLine = said;
                Letterbox.Instance?.Say(speaker, line);
                Voice(voice, line);
            }

            foreach (var r in v._rows) if (r != null) Destroy(r);
            v._rows.Clear();
            float y = 0f;
            for (int i = 0; i < options.Count; i++)
            {
                v._rows.Add(v.Row(options[i], y));
                y -= string.IsNullOrEmpty(options[i].Sub) ? 46f : 64f;
            }
            v._list.sizeDelta = new Vector2(v._list.sizeDelta.x, -y);
            v._footer.text = footer ?? "";
        }

        /// <summary>
        /// Голос под строкой — бип на букву (<see cref="Audio.VoiceGenerator"/>),
        /// столько, сколько строка печатается на полосе: голос кончается вместе
        /// с буквами. Ремарка в скобках — «(молча кивает)» у Гурта — без голоса:
        /// это не слова.
        /// </summary>
        private static void Voice(Gameplay.Warrior who, string line)
        {
            if (who == null || string.IsNullOrEmpty(line) || line.TrimStart().StartsWith("(")) return;
            if (!who.TryGetComponent<Audio.VoiceGenerator>(out var voice)) return;

            float pace = Letterbox.Instance != null ? Letterbox.Instance.LettersPerSecond : 40f;
            voice.Babble(line, Mathf.Min(6f, line.Length / pace + 0.3f), 0.8f);
        }

        public static void Hide()
        {
            if (_instance == null || _instance._canvas == null) return;
            _instance._canvas.enabled = false;
            _instance._lastLine = null;
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 70;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiStyle.Reference;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _canvas.enabled = false;

            // Справа от лица, над нижней полосой: говорящий — слева от середины
            // кадра наезда, ответы ему не застят.
            var list = new GameObject("Ответы", typeof(RectTransform));
            list.transform.SetParent(transform, false);
            _list = (RectTransform)list.transform;
            _list.anchorMin = _list.anchorMax = new Vector2(0.60f, 0.27f);
            _list.pivot = new Vector2(0f, 0f);
            _list.sizeDelta = new Vector2(760f, 200f);

            // Тень под ответами — мягкая, без рамки: читать на любом фоне.
            var shade = new GameObject("Тень", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(_list, false);
            var sr = (RectTransform)shade.transform;
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(-28f, -56f); sr.offsetMax = new Vector2(28f, 18f);
            var si = shade.GetComponent<Image>();
            si.color = new Color(0f, 0f, 0f, 0.45f);
            si.raycastTarget = false;

            var foot = new GameObject("Под ответами", typeof(RectTransform), typeof(Text));
            foot.transform.SetParent(_list, false);
            var fr = (RectTransform)foot.transform;
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 0f);
            fr.pivot = new Vector2(0f, 1f);
            fr.anchoredPosition = new Vector2(0f, -8f);
            fr.sizeDelta = new Vector2(760f, 44f);
            _footer = foot.GetComponent<Text>();
            _footer.font = UiStyle.Body;
            _footer.fontSize = 18;
            _footer.color = new Color(0.66f, 0.61f, 0.53f);
            _footer.raycastTarget = false;
            _footer.horizontalOverflow = HorizontalWrapMode.Wrap;
            _footer.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static readonly Color Idle = new Color(0.88f, 0.84f, 0.76f);
        private static readonly Color Lit = new Color(0.95f, 0.78f, 0.42f);
        private static readonly Color Off = new Color(0.52f, 0.49f, 0.44f);

        private GameObject Row(Option o, float y)
        {
            var go = new GameObject("Ответ", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_list, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, string.IsNullOrEmpty(o.Sub) ? 44f : 62f);
            var hit = go.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // ловит мышь, не видно

            var main = Label(rt, o.Text, 25, 0f, o.Act != null ? Idle : Off);
            Text sub = string.IsNullOrEmpty(o.Sub) ? null : Label(rt, o.Sub, 17, -32f, Off);

            if (o.Act != null)
            {
                var button = go.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                var act = o.Act;
                button.onClick.AddListener(() => act());

                var trigger = go.AddComponent<EventTrigger>();
                void On(EventTriggerType type, Color c)
                {
                    var e = new EventTrigger.Entry { eventID = type };
                    e.callback.AddListener(_ => { if (main != null) main.color = c; });
                    trigger.triggers.Add(e);
                }
                On(EventTriggerType.PointerEnter, Lit);
                On(EventTriggerType.PointerExit, Idle);
            }
            return go;
        }

        private static Text Label(RectTransform parent, string text, int size, float y, Color colour)
        {
            var go = new GameObject("Строка", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, size + 10f);
            var t = go.GetComponent<Text>();
            t.font = UiStyle.Body;
            t.fontSize = size;
            t.text = text;
            t.color = colour;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }
    }
}
