// Assets/Scripts/UI/BattleLogUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Третья ступень прозрачности: решение словами, в момент решения.
    ///
    /// Устроен как консоль: записи копятся, ничего не гаснет, прокрутка
    /// на месте. Так было не всегда — раньше журнал показывал по одной
    /// строке и гасил её через пять секунд, а остальные ждали в очереди.
    /// Когда в бою говорят все сразу, очередь отстаёт от боя на минуту,
    /// и к моменту, когда строка про отказ доезжает, отказ уже забыт.
    /// А ушедшую строку было не вернуть: перечитать нельзя, отмотать
    /// некуда.
    ///
    /// Для игры, вся суть которой в том, чтобы игрок понял, <b>почему</b>
    /// воин поступил так, — это дороже любой другой потери. Объяснение
    /// обязано оставаться на экране столько, сколько игрок захочет.
    ///
    /// Настройка в сцене: повесить на Canvas, задать _line (текст внутри
    /// прокручиваемой области) и _scroll (сама область).
    /// </summary>
    public class BattleLogUI : MonoBehaviour
    {
        [SerializeField] private Text _line;
        [SerializeField] private ScrollRect _scroll;

        [Tooltip("Сколько строк держать на экране. Свыше — самые старые "
               + "уходят: бесконечный текст в одном Text рано или поздно "
               + "упирается в предел вершин меша и перестаёт рисоваться "
               + "целиком, молча.")]
        [SerializeField] private int _keep = 200;

        private readonly List<string> _history = new();
        private readonly List<string> _shown = new();

        /// <summary>Всё, что случилось за бой. Для рассказа после боя.</summary>
        public IReadOnlyList<string> History => _history;

        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // История собирается всегда: она нужна рассказу после боя
            // и разработчику, а стоит ничего. Показ — другое дело.
            _history.Add(text);

            if (_line == null) return;

            // Третья ступень прозрачности. Игрок, выбравший меньше,
            // журнала не видит — но и не теряет его насовсем.
            if (!Core.Transparency.Shows(Core.Clarity.Log)) return;

            _shown.Add(text);
            if (_shown.Count > _keep) _shown.RemoveRange(0, _shown.Count - _keep);

            _line.text = string.Join(System.Environment.NewLine, _shown);

            Bottom();
        }

        /// <summary>
        /// Прокрутить к последней записи.
        ///
        /// <see cref="Canvas.ForceUpdateCanvases"/> обязателен: размер
        /// содержимого пересчитывается в конце кадра, и без него прокрутка
        /// уехала бы к тому низу, который был до новой строки, — то есть
        /// каждый раз на строку выше нужного.
        /// </summary>
        private void Bottom()
        {
            if (_scroll == null) return;

            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = 0f;
        }
    }
}
