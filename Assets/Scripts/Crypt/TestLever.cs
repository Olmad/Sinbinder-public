// Assets/Scripts/Crypt/TestLever.cs
using UnityEngine;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Рычаг на тренировочной площадке.
    ///
    /// Ничего не считает сам — дёргает <see cref="TestChamber"/>. Вся
    /// причина живёт в одном месте, иначе площадка рассыплется на пять
    /// маленьких правд.
    ///
    /// Подход и нажатие — общие для всего склепа
    /// (<see cref="CryptInteractable"/>).
    /// </summary>
    public class TestLever : CryptInteractable
    {
        public enum LeverKind
        {
            /// <summary>Внести или убрать условие.</summary>
            Condition = 0,

            /// <summary>Поставить то же самое заново. Главный рычаг.</summary>
            Repeat = 1,

            /// <summary>Тот же набор, другая душа.</summary>
            NextSubject = 2,
        }

        [SerializeField] private LeverKind _kind = LeverKind.Condition;
        [SerializeField] private Trial _trial = Trial.Loot;
        [SerializeField] private TestChamber _chamber;

        [Tooltip("Табличка. Пусто — соберём из каталога.")]
        [SerializeField] private string _label = "";

        public override string Label
        {
            get
            {
                if (!string.IsNullOrEmpty(_label)) return _label;

                switch (_kind)
                {
                    case LeverKind.Repeat:      return "Повторить\nтот же опыт";
                    case LeverKind.NextSubject: return "Другая душа\nтот же опыт";
                    default:
                        return $"{TrialCatalog.Title(_trial)}\nбудит: {TrialCatalog.Wakes(_trial)}";
                }
            }
        }

        void Start()
        {
            if (_chamber == null) _chamber = Object.FindFirstObjectByType<TestChamber>();

            if (_chamber == null)
                Debug.LogError("[ПОЛИГОН] Рычаг без площадки: дёргать нечего.");
        }

        protected override void Use()
        {
            if (_chamber == null) return;

            switch (_kind)
            {
                case LeverKind.Repeat:
                    _chamber.Repeat();
                    Say("Тот же опыт заново. Если ничего не менялось — "
                      + "и решение будет то же.");
                    break;

                case LeverKind.NextSubject:
                    _chamber.NextSubject();
                    Say("Условия те же. Душа другая.");
                    break;

                default:
                    _chamber.Toggle(_trial);
                    Say(_chamber.IsOn(_trial)
                        ? $"{TrialCatalog.Title(_trial)}: теперь так."
                        : $"{TrialCatalog.Title(_trial)}: убрано.");
                    break;
            }
        }

        /// <summary>Настроить из сборщика сцены.</summary>
        public void Set(LeverKind kind, Trial trial, TestChamber chamber)
        {
            _kind = kind;
            _trial = trial;
            _chamber = chamber;
        }
    }
}
