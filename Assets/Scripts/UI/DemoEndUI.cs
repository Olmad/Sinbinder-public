using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Конец демо: кто вернулся.
    ///
    /// Последняя строка сценария 00-GDD.md §8 — «возвращение выжившего
    /// отряда, состав которого зависит от выбора командира в начале».
    /// Экран не подводит итог в очках и не ставит оценку: он просто
    /// называет тех, кто дошёл, и того, кого игрок поставил старшим.
    ///
    /// Морального счётчика в игре нет (05-BOUNDS), поэтому и здесь
    /// нет ни «хорошей», ни «плохой» концовки — есть список имён.
    /// </summary>
    public class DemoEndUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Text _body;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        public void Show(bool wiped)
        {
            if (_panel == null) return;

            if (_title != null)
                _title.text = wiped ? "Отряд не вернулся." : "Отряд вернулся.";

            if (_body != null) _body.text = wiped ? Epitaph() : Roll();

            _panel.SetActive(true);
            Core.GamePauseController.Instance?.Pause();
        }

        /// <summary>Кто дошёл и кто их вёл.</summary>
        private string Roll()
        {
            var sb = new StringBuilder();

            string commander = SquadRoster.CommanderName;
            if (!string.IsNullOrEmpty(commander))
                sb.AppendLine($"Отряд вёл {commander}.").AppendLine();

            foreach (var m in SquadRoster.Members)
            {
                sb.Append(m.Name);

                // Долг — единственное, что отряд уносит с собой к следующей
                // вылазке. Числа игрок не видит, только факт.
                if (m.UnpaidMissions > 0) sb.Append(" — ему всё ещё должны");
                else if (m.IsCommander) sb.Append(" — старший");

                sb.AppendLine();
            }

            sb.AppendLine().Append("Демо окончено.");
            return sb.ToString();
        }

        private string Epitaph()
            => "Никто не дошёл до склепа.\n\nДуши разойдутся Некроэфиром,"
             + " и помнить о них будет некому.\n\nДемо окончено.";
    }
}
