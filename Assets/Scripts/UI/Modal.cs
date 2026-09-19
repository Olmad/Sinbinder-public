// Assets/Scripts/UI/Modal.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// Окно, которое требует ответа: пока оно открыто, остального не видно.
    ///
    /// Заведено 18 сентября, когда появились снимки прохождения и стало
    /// видно то, чего не показывал ни один отчёт: совет открывался поверх
    /// незакрытого разговора, а под ними ещё светились приказ отряду,
    /// журнал и полка душ. Всё это читалось одной кашей — а игру в таком
    /// виде собирались показывать людям.
    ///
    /// <b>Прячем прозрачностью, а не выключением.</b> На панелях живут
    /// сопрограммы, а выключенный объект их останавливает: этим уже
    /// сломались подсказка, конец демо и плата, когда их компоненты
    /// сидели на панели, которую сами же выключали. Прозрачная панель
    /// остаётся живой и возвращается в точности такой, какой была.
    /// </summary>
    public static class Modal
    {
        private sealed class Was
        {
            public CanvasGroup Group;
            public float Alpha;
            public bool Interactable;
            public bool Blocks;
        }

        private static readonly Dictionary<GameObject, List<Was>> Dimmed = new();

        /// <summary>
        /// Открыть окно, убрав с глаз всё прочее на том же холсте.
        /// </summary>
        public static void Open(GameObject panel)
        {
            if (panel == null || Dimmed.ContainsKey(panel)) return;

            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var hidden = new List<Was>();

            foreach (Transform child in canvas.transform)
            {
                var go = child.gameObject;
                if (go == panel || !go.activeInHierarchy) continue;

                // Полосы не трогаем: они и есть кадр, а не панель поверх
                // него, и гасить их ради окна значит гасить сцену.
                if (go.GetComponent<Letterbox>() != null) continue;

                var group = go.GetComponent<CanvasGroup>();
                if (group == null) group = go.AddComponent<CanvasGroup>();

                hidden.Add(new Was
                {
                    Group = group,
                    Alpha = group.alpha,
                    Interactable = group.interactable,
                    Blocks = group.blocksRaycasts,
                });

                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            Dimmed[panel] = hidden;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        /// <summary>Закрыть окно и вернуть всё, что было под ним.</summary>
        public static void Close(GameObject panel)
        {
            if (panel != null) panel.SetActive(false);
            if (panel == null || !Dimmed.TryGetValue(panel, out var hidden)) return;

            foreach (var was in hidden)
            {
                if (was.Group == null) continue;

                was.Group.alpha = was.Alpha;
                was.Group.interactable = was.Interactable;
                was.Group.blocksRaycasts = was.Blocks;
            }

            Dimmed.Remove(panel);
        }
    }
}
