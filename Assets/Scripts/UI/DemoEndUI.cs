using System.Collections.Generic;
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

            if (_body != null) _body.text = wiped ? Epitaph() : Roll() + Comeback();

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
                if (m.IsAway) continue;   // они возвращаются ниже, отдельно

                sb.Append(m.Name);

                // Долг — единственное, что отряд уносит с собой к следующей
                // вылазке. Числа игрок не видит, только факт.
                if (m.UnpaidMissions > 0) sb.Append(" — ему всё ещё должны");
                else if (m.IsCommander) sb.Append(" — старший");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Возвращение отряда, ушедшего на доле 2, — закрытие демо.
        ///
        /// Игрок выбрал старшего полчаса назад, прочитав пророчество.
        /// Здесь ему возвращают счёт, и состав зависит от того самого
        /// выбора (00-GDD.md §8). Считает не эта панель, а <see
        /// cref="Homecoming"/>: правило проверяется стендом, а исход
        /// зависит от греха командира, не от его имени.
        /// </summary>
        private string Comeback()
        {
            var away = new List<SquadRoster.Member>();
            foreach (var m in SquadRoster.Away) away.Add(m);

            if (away.Count == 0) return "\nДемо окончено.";

            // Командир идёт первым: он вернулся, если вернулся хоть кто-то.
            away.Sort((a, b) =>
            {
                if (a.IsCommander != b.IsCommander) return a.IsCommander ? -1 : 1;
                return string.CompareOrdinal(a.Name, b.Name);
            });

            var leader = away[0];
            int back = Homecoming.Returned(leader.Sin, away.Count);

            var sb = new StringBuilder();
            sb.AppendLine().AppendLine("В склеп входит отряд, ушедший из лагеря.");
            sb.AppendLine();

            for (int i = 0; i < back && i < away.Count; i++)
            {
                sb.Append(away[i].Name);
                if (away[i].IsCommander) sb.Append(" — вёл их");
                sb.AppendLine();
            }

            if (back < away.Count)
            {
                sb.AppendLine();
                sb.AppendLine(Homecoming.Story(leader.Sin));
            }

            sb.AppendLine().Append("Демо окончено.");
            return sb.ToString();
        }

        private string Epitaph()
            => "Никто не дошёл до склепа.\n\nДуши разойдутся Некроэфиром,"
             + " и помнить о них будет некому.\n\nДемо окончено.";
    }
}
