using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Плата после вылазки — второй соблазн пролога.
    ///
    /// Механика была написана и не подключена: <c>Warrior.PaySalary</c>
    /// существовал, но его не звал никто, а <c>UnpaidMissions</c> читали
    /// три места и не писало ни одно. Голос Жадности против приказа
    /// (<c>GreedObeyUnpaidPenalty</c>) поэтому не мог сработать никогда —
    /// один из четырёх рычагов игрока был муляжом.
    ///
    /// Соблазн должен быть настоящим, иначе он не соблазн: заплатить —
    /// значит расстаться с золотом, придержать — ничего не потерять
    /// сейчас и потерять верность потом. Игрок сам выбирает, чем
    /// заплатит, и узнаёт цену через четыре минуты.
    ///
    /// Чисел на панели нет: ни суммы, ни остатка. Только слова —
    /// правило проекта одно для всех экранов.
    /// </summary>
    public class SalaryPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Button _payButton;
        [SerializeField] private Text _payLabel;
        [SerializeField] private Button _withholdButton;
        [SerializeField] private Text _withholdLabel;

        [Tooltip("Сколько стоит вылазка одного воина. Игрок этого числа "
               + "не видит — оно только для кошелька.")]
        [SerializeField] private int _costPerWarrior = 10;

        private bool _sawEnemies;
        private bool _asked;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);

            if (_payButton != null) _payButton.onClick.AddListener(Pay);
            if (_withholdButton != null) _withholdButton.onClick.AddListener(Withhold);

            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged += OnUnitsChanged;
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged -= OnUnitsChanged;
        }

        private void OnUnitsChanged()
        {
            if (_asked || CombatManager.Instance == null) return;

            if (CombatManager.Instance.GetAliveEnemyCount() > 0) { _sawEnemies = true; return; }

            // Спрашиваем только после настоящего боя: сцена без врагов
            // не повод требовать плату.
            if (!_sawEnemies) return;
            if (CombatManager.Instance.GetAlivePlayerCount() == 0) return;

            Open();
        }

        public void Open()
        {
            if (_asked || _panel == null) return;
            _asked = true;

            if (_title != null) _title.text = "Вылазка окончена. Отряд ждёт платы.";
            if (_payLabel != null) _payLabel.text = "Заплатить\nзолото уйдёт из мешка";
            if (_withholdLabel != null) _withholdLabel.text = "Придержать\nони запомнят";

            _panel.SetActive(true);
            Core.GamePauseController.Instance?.Pause();
        }

        private void Pay()
        {
            var squad = CombatManager.Instance?.GetAllWarriors();
            if (squad == null) { Close(); return; }

            int owed = 0;
            foreach (var w in squad)
                if (w != null && !w.IsDead && w.Team == Team.Player) owed += _costPerWarrior;

            var purse = Inventory.PlayerInventory.Instance;
            bool paid = owed <= 0 || (purse != null && purse.SpendGold(owed));

            foreach (var w in squad)
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;
                w.PaySalary(paid ? _costPerWarrior : 0f);
            }

            // Честность важнее удобства: если платить было нечем, отряд
            // запомнит долг, а не намерение.
            Log(paid
                ? "Отряду заплачено."
                : "Платить было нечем. Отряд это запомнил.");

            Close();
        }

        private void Withhold()
        {
            var squad = CombatManager.Instance?.GetAllWarriors();
            if (squad != null)
                foreach (var w in squad)
                {
                    if (w == null || w.IsDead || w.Team != Team.Player) continue;
                    w.PaySalary(0f);
                }

            Log("Золото осталось в мешке. Отряд это запомнил.");
            Close();
        }

        private void Close()
        {
            if (_panel != null) _panel.SetActive(false);
            Core.GamePauseController.Instance?.Resume();
        }

        private void Log(string text)
        {
            var log = Object.FindObjectOfType<BattleLogUI>();
            if (log != null) log.Write(text);
            else Debug.Log("[ПЛАТА] " + text);
        }
    }
}
