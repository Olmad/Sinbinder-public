// Assets/Scripts/UI/SquadStrategyUI.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

using Sinbinder.Core;
namespace Sinbinder.UI
{
    /// <summary>
    /// Установка отряда: цифры от 1 до 6 переключают, строка внизу
    /// объясняет словами, к чему теперь склонны воины.
    ///
    /// Почему это отдельный рычаг, а не приказ. Приказ достаётся одному
    /// воину и требует исполнения. Установка меняет условия для всех
    /// сразу и ничего не требует: она складывается с характером
    /// и вполне может ему проиграть. Игрок здесь не командует —
    /// он задаёт настроение, а решают всё равно они.
    ///
    /// Настройка в сцене: повесить на Canvas, задать _label (Text).
    /// Работает и без него — тогда остаётся только лог.
    /// </summary>
    public class SquadStrategyUI : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private CanvasGroup _group;

        [Tooltip("Сколько секунд держать подсказку после переключения. "
               + "Ноль — держать всегда.")]
        [SerializeField] private float _showSeconds = 4f;

        private static readonly KeyCode[] Keys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6
        };

        private float _hideAt;

        /// <summary>
        /// Табличка слева вверху больше не всплывает: установка видна
        /// на полосе выбранного — «Установка: … ▾» со списком и описаниями
        /// (<see cref="CommandPanel"/>, 42-INTERFACE §3). Две таблички одного
        /// и того же — шум. Клавиши 1–6 остаются здесь.
        /// </summary>
        private const bool Popup = false;

        void Start()
        {
            SquadOrders.Changed += OnChanged;
            Refresh();
            if (_group != null && !Popup) _group.alpha = 0f;
            if (_showSeconds > 0f && Popup) _hideAt = Time.time + _showSeconds;
        }

        void OnDestroy() => SquadOrders.Changed -= OnChanged;

        void Update()
        {
            // Цифры разговора (меню у GearPanel) — не установка отряда: «1 —
            // как ты?» не должно заодно менять, как отряд ведёт бой.
            var choices = GearPanel.Open ? System.Array.Empty<SquadStrategy>() : SquadOrders.InDemo;
            for (int i = 0; i < Keys.Length && i < choices.Length; i++)
            {
                if (!Input.GetKeyDown(Keys[i])) continue;
                SquadOrders.Set(choices[i]);
                break;
            }

            // Держать погашенной каждый кадр, а не один раз в Start: в кадре
            // прогона 1 октября табличка всё равно стояла видимой — кто-то
            // поднимал её после старта, а искать его дороже, чем держать.
            if (_group != null && !Popup) { _group.alpha = 0f; return; }
            if (_group == null || _showSeconds <= 0f) return;
            _group.alpha = Time.time < _hideAt ? 1f : Mathf.MoveTowards(_group.alpha, 0f, Time.deltaTime * 2f);
        }

        private void OnChanged(SquadStrategy strategy)
        {
            Refresh();
            if (!Popup) return;
            if (_showSeconds > 0f) _hideAt = Time.time + _showSeconds;
            if (_group != null) _group.alpha = 1f;
        }

        private void Refresh()
        {
            var current = SquadOrders.Current;
            Debug.Log($"[ОТРЯД] {SquadOrders.Name(current)} — {SquadOrders.Describe(current)}");

            if (_label == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Loc.F("Отряд: {0}", SquadOrders.Name(current)));
            sb.AppendLine(SquadOrders.Describe(current));
            sb.AppendLine();

            var choices = SquadOrders.InDemo;
            for (int i = 0; i < choices.Length && i < Keys.Length; i++)
            {
                string mark = choices[i] == current ? "▸ " : "  ";
                sb.AppendLine($"{mark}{i + 1}. {SquadOrders.Name(choices[i])}");
            }

            _label.text = sb.ToString().TrimEnd();
        }
    }
}
