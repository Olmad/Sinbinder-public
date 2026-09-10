// Assets/Scripts/Crypt/TestLever.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Рычаг на тренировочной площадке.
    ///
    /// Подойти ногами и нажать. «Подойти» значит именно дойти, а не
    /// навести взгляд: на этом уже обжёгся совет, когда Греховода
    /// в сцене не существовало и подойти к столу можно было, не сходя
    /// с места (`14-HANDOFF.md` §8.1).
    ///
    /// Рычаг ничего не считает сам — он дёргает
    /// <see cref="TestChamber"/>. Вся причина живёт в одном месте,
    /// иначе площадка рассыпется на пять маленьких правд.
    /// </summary>
    public class TestLever : MonoBehaviour
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

        [Tooltip("С какого расстояния рычаг слушается.")]
        [SerializeField] private float _reach = 2.2f;

        [SerializeField] private KeyCode _key = KeyCode.F;

        [Tooltip("Табличка над рычагом. Пусто — соберём из каталога.")]
        [SerializeField] private string _label = "";

        private bool _wasNear;

        /// <summary>Надпись на табличке. Читает и сборщик сцены, и подсказка.</summary>
        public string Label
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

        void Update()
        {
            if (_chamber == null) return;

            bool near = Near();

            // Подсказку показываем один раз на подход, а не каждый кадр.
            if (near && !_wasNear)
            {
                string flat = Label.Replace(System.Environment.NewLine, " ")
                                   .Replace("\n", " ");
                Say($"{flat} — нажмите {_key}.");
            }
            _wasNear = near;

            if (!near || !Input.GetKeyDown(_key)) return;

            Pull();
        }

        private bool Near()
        {
            if (!SinbinderPlayer.Exists) return false;

            return CampFocus.GroundDistance(SinbinderPlayer.Where, transform.position)
                   <= _reach;
        }

        private void Pull()
        {
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

        private static void Say(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
