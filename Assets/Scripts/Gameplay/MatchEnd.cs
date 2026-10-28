// Assets/Scripts/Gameplay/MatchEnd.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Экран конца партии «Перетягивания» — вместо временного окна IMGUI (54-TUG, «обработка: экран конца»).
    /// Та же плашка, что у обучения (TugTutorial): тёмная, с золотой рамкой. Итог — словами, без счёта:
    /// кто был с тобой до конца (по именам), что добыто в экспедициях, сколько шла партия. Две кнопки —
    /// ещё партию и в меню.
    /// </summary>
    public static class MatchEnd
    {
        private const int Names = 6;

        public static void Show(bool won, string why)
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("События интерфейса", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Конец партии", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            // Затемнение за плашкой — мир остановлен (Time.timeScale = 0 в TugMatch.End).
            var shade = new GameObject("Тень", typeof(RectTransform), typeof(Image));
            var srt = (RectTransform)shade.transform;
            srt.SetParent(canvasGo.transform, false);
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var panel = new GameObject("Плашка", typeof(RectTransform), typeof(Image), typeof(Outline));
            var rt = (RectTransform)panel.transform;
            rt.SetParent(canvasGo.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720f, 420f);
            panel.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.06f, 1f);
            var edge = panel.GetComponent<Outline>();
            edge.effectColor = new Color(0.72f, 0.58f, 0.30f, 0.9f);
            edge.effectDistance = new Vector2(2f, 2f);

            var font = Resources.Load<Font>("Fonts/PTSerif-Regular");
            var gold = new Color(0.86f, 0.70f, 0.38f);
            var paper = new Color(0.93f, 0.89f, 0.80f);
            Label(rt, font, 40, gold, TextAnchor.MiddleCenter, new Vector2(0f, -22f), 56f, won ? Loc.T("Победа") : Loc.T("Поражение"));
            Label(rt, font, 19, paper, TextAnchor.UpperCenter, new Vector2(0f, -84f), 230f, Story(won, why));

            Button(rt, font, Loc.T("Ещё партию"), new Vector2(-130f, 36f), () =>
            { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); });
            Button(rt, font, Loc.T("В меню"), new Vector2(130f, 36f), () =>
            { Time.timeScale = 1f; SceneManager.LoadScene(0); });
        }

        /// <summary>Итог словами: почему, сколько шла, кто до конца, что добыто.</summary>
        public static string Story(bool won, string why)
        {
            var lines = new List<string> { why };
            lines.Add(won ? Loc.T("Ваш трон стоит. Других — нет.") : Loc.T("Ваша сторона выбыла из партии."));
            lines.Add(Loc.F("Партия шла {0} мин.", Mathf.Max(1, Mathf.RoundToInt(Time.timeSinceLevelLoad / 60f))));

            var with = new List<string>();
            int more = 0;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w.Team != Team.Neutral || Lords.Player == null) continue;
                if (Allegiance.Dearest(w.Id) != Lords.Player || Allegiance.Of(w.Id, w.Loyalty, Lords.Player) < Hostility.Follows) continue;
                if (with.Count < Names) with.Add(Loc.Name(w.DisplayName)); else more++;
            }
            lines.Add(with.Count == 0 ? Loc.T("До конца с тобой не было никого.")
                    : more > 0 ? Loc.F("До конца с тобой: {0} — и другие.", string.Join(", ", with))
                    : Loc.F("До конца с тобой: {0}.", string.Join(", ", with)));

            var got = new List<string>();
            foreach (var k in Trophies.Of(Lords.Player)) if (k != Trophies.Kind.Coins && k != Trophies.Kind.Jar) got.Add(Trophies.Word(k));
            if (got.Count > 0) lines.Add(Loc.F("Добыто в экспедициях: {0}.", string.Join(", ", got)));
            return string.Join("\n", lines);
        }

        private static Text Label(RectTransform parent, Font font, int size, Color colour, TextAnchor align, Vector2 at, float height, string text)
        {
            var go = new GameObject("Строка", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(-48f, height);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = colour; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        private static void Button(RectTransform parent, Font font, string text, Vector2 at, UnityEngine.Events.UnityAction act)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(UnityEngine.UI.Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(220f, 46f);
            go.GetComponent<Image>().color = new Color(0.16f, 0.12f, 0.09f, 1f);
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0.72f, 0.58f, 0.30f, 0.8f);
            o.effectDistance = new Vector2(1.5f, 1.5f);
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(act);
            var label = Label(rt, font, 20, new Color(0.86f, 0.70f, 0.38f), TextAnchor.MiddleCenter, new Vector2(0f, -4f), 38f, text);
            label.rectTransform.sizeDelta = new Vector2(-12f, 38f);
        }
    }
}
