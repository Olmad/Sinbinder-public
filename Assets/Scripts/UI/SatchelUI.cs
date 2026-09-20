// Assets/Scripts/UI/SatchelUI.cs
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.UI
{
    /// <summary>
    /// Сума на экране: шесть ячеек и та, что под рукой.
    ///
    /// Без неё сума существовала бы только в голове у игрока: он нажимает
    /// <c>Tab</c>, что-то там переключается, а увидеть это негде. Предел
    /// «сколько душ унесёшь» тем более обязан быть виден — предел, о котором
    /// узнаёшь по отказу, читается как поломка.
    ///
    /// Ни одной цифры (00-GDD.md §7): ячейки считаются от единицы просто
    /// как места, а что в них лежит, названо словом.
    /// </summary>
    public class SatchelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text[] _cells;
        [SerializeField] private Image[] _frames;

        [Tooltip("Как часто перечитывать. Каждый кадр не нужно: сума меняется "
               + "нажатием, а не сама.")]
        [SerializeField] private float _refreshEvery = 0.2f;

        private static readonly Color Chosen = new(0.62f, 0.54f, 0.30f, 0.95f);
        private static readonly Color Plain = new(0.10f, 0.09f, 0.09f, 0.80f);

        private static readonly Color Full = new(0.94f, 0.92f, 0.86f);
        private static readonly Color Idle = new(0.55f, 0.52f, 0.48f);

        /// <summary>
        /// Чернила по золоту: текст выбранной ячейки.
        ///
        /// Раньше выбранная ячейка красилась золотом, а текст в ней
        /// оставался прежним — и у пустой ячейки серый `Idle` (яркость
        /// 0.52) ложился на золото (яркость 0.54). Контраст **1.03 к 1**,
        /// то есть текста не было видно вовсе; на снимках автора
        /// от 19 сентября выбранная ячейка стоит пустой надписью.
        ///
        /// Тёмные чернила дают против того же золота **4.9 к 1** —
        /// читается и в кадре, и на сжатом видео.
        /// </summary>
        private static readonly Color Ink = new(0.08f, 0.07f, 0.04f);

        private float _next;

        void Start()
        {
            if (_cells == null || _cells.Length != Satchel.Size)
            {
                Debug.LogWarning("[СУМА] Полоса сумы не связана в сцене или "
                               + "ячеек не столько, сколько в суме. "
                               + "Пересоберите сцены.");
                enabled = false;
                return;
            }

            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _refreshEvery;

            Refresh();
        }

        /// <summary>
        /// Пока сума пуста, её не видно. Шесть ячеек «пусто» на экране
        /// с первой секунды — не сума, а рамка, которую игрок читает
        /// поломкой: ему нечего туда класть до первой собранной души.
        ///
        /// Гасим прозрачностью, а не выключением: панель ищут по типу,
        /// а выключенный объект не находится (этим уже ломались тут
        /// подсказка, плата и конец демо).
        /// </summary>
        private void Fade(bool show)
        {
            if (_panel == null) return;

            var group = _panel.GetComponent<CanvasGroup>();
            if (group == null) group = _panel.AddComponent<CanvasGroup>();

            group.alpha = show ? 1f : 0f;
        }

        private void Refresh()
        {
            Fade(Satchel.Anything());

            int chosen = Satchel.Selected;

            for (int i = 0; i < _cells.Length; i++)
            {
                var slot = Satchel.At(i);

                if (_cells[i] != null)
                {
                    _cells[i].text = Satchel.Describe(i);
                    _cells[i].color = i == chosen
                        ? Ink
                        : (slot.Empty || slot.EmptyJar ? Idle : Full);
                }

                if (_frames != null && i < _frames.Length && _frames[i] != null)
                    _frames[i].color = i == chosen ? Chosen : Plain;
            }
        }
    }
}
