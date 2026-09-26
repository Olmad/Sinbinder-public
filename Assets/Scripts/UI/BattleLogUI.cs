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

        /// <summary>
        /// Показать журнал. Гасим прозрачностью, а не выключением:
        /// этот же объект ищут по типу, а выключенный не находится.
        /// </summary>
        private void Show()
        {
            // Пока полосы подняты, журнал молчит на экране. Во время
            // набега души собираются как раз под полосами, и каждая
            // запись вытаскивала журнал обратно поверх кино.
            if (Letterbox.Instance != null && Letterbox.Instance.Shown) return;

            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            group.alpha = 1f;
        }

        void Awake()
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
        }

        /// <summary>
        /// Отказ и поступок без приказа — то, ради чего журнал заведён.
        ///
        /// Подписка пропала 11 сентября, когда журнал переделывали
        /// в консоль (3e62bfc): с тех пор в него писали все, кроме воинов.
        /// Строка «…не выполнил приказ: ему не платили третью вылазку
        /// подряд» доходила до лога Unity и ни разу — до экрана, а на неё
        /// ссылаются и 10-REFUSAL (ступень 3, «логика готова»), и наезд
        /// камеры («слово пишется всегда — журналом»). Нашёл прогон
        /// 25 сентября: отказ Марги в логе есть, на кадре — ни слова.
        /// </summary>
        void Start()
        {
            if (AOS.AOSEventHub.Instance == null) return;

            AOS.AOSEventHub.Instance.OnRefusal += Told;
            AOS.AOSEventHub.Instance.OnSelfWill += Told;
        }

        void OnDestroy()
        {
            if (AOS.AOSEventHub.Instance == null) return;

            AOS.AOSEventHub.Instance.OnRefusal -= Told;
            AOS.AOSEventHub.Instance.OnSelfWill -= Told;
        }

        /// <summary>
        /// Одна строка на оба случая: воин и его причина описываются одними
        /// словами, спорил он с игроком или тот просто молчал.
        /// </summary>
        private void Told(Gameplay.Warrior warrior, AOS.Decision decision, AOS.DecisionContext context)
        {
            // Врага в тумане не видно — не слышно и строки о нём:
            // журнал выдал бы, где он и что задумал.
            if (warrior == null || Gameplay.FogOfWar.Hides(warrior)) return;

            string line = AOS.PhraseGenerator.LogLine(warrior, context, decision);

            if (_last.TryGetValue(warrior, out var was)
                && was.Line == line && Time.time - was.At < Repeat) return;
            _last[warrior] = (line, Time.time);

            Write(line);
        }

        /// <summary>
        /// Сколько молчать о том же самом, секунды игры.
        ///
        /// Бегущий то бежит, то оборачивается, и каждый новый побег — смена
        /// решения: «Охотник-следопыт сбежал: ему страшно» легло в журнал
        /// трижды за десять секунд (первый прогон после починки подписки,
        /// 26 сентября), и строку отказа Марги вытеснило за край. Новое
        /// о воине пишется сразу, то же самое — не раньше, чем через
        /// полминуты.
        /// </summary>
        private const float Repeat = 30f;

        private readonly Dictionary<Gameplay.Warrior, (string Line, float At)> _last = new();

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

            // Пустой журнал не показываем: рамка в четверть экрана
            // с одной строкой внутри читалась поломкой. Появляется
            // с первой записью и больше не прячется.
            Show();

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
