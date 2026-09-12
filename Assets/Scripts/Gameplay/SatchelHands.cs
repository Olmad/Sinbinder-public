// Assets/Scripts/Gameplay/SatchelHands.cs
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Crypt;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Руки и сума: чем Греховод перекладывает.
    ///
    /// Висит на нём самом, а не на суме: сума статическая и в сцене
    /// её нет, а нажатия слушать кто-то должен. Заодно это верно
    /// и по смыслу — перекладывает он, а не сума.
    ///
    /// <see cref="_next"/> ходит по ячейкам, <see cref="_swap"/>
    /// перекладывает между рукой и выбранной ячейкой. Две клавиши,
    /// а не шесть цифр: цифры с первой по шестую заняты установкой
    /// отряда, и вешать на них второе значило бы повторить ошибку
    /// с клавишей D.
    ///
    /// Пустую банку в руки не берём. Она нужна не в руке, а в суме:
    /// жатва наливает душу прямо туда (<see cref="SoulHarvester"/>),
    /// и носить пустую посуду в руках незачем — руки одни, и заняты
    /// они были бы ничем.
    /// </summary>
    public class SatchelHands : MonoBehaviour
    {
        [Tooltip("Перейти к следующей ячейке сумы.")]
        [SerializeField] private KeyCode _next = KeyCode.Tab;

        [Tooltip("Переложить: из руки в ячейку или из ячейки в руку.")]
        [SerializeField] private KeyCode _swap = KeyCode.R;

        void Update()
        {
            // В разговоре и на паузе руки заняты чтением.
            if (Dialogue.DialogueCameraController.Instance != null &&
                Dialogue.DialogueCameraController.Instance.InDialogue) return;

            if (Core.GamePauseController.Instance != null
                && Core.GamePauseController.Instance.IsPaused) return;

            if (Input.GetKeyDown(_next))
            {
                Satchel.Next();
                Say($"Ячейка {Satchel.Selected + 1}: {Satchel.Describe(Satchel.Selected)}.");
            }

            if (Input.GetKeyDown(_swap)) Swap();
        }

        private void Swap()
        {
            int i = Satchel.Selected;

            if (CryptHands.Empty) { FromSatchel(i); return; }

            ToSatchel(i);
        }

        /// <summary>Из ячейки в руку.</summary>
        private void FromSatchel(int i)
        {
            var slot = Satchel.At(i);

            if (slot.Empty)
            {
                Say($"Ячейка {i + 1} пуста.");
                return;
            }

            if (slot.EmptyJar)
            {
                // Не ошибка игрока, а разъяснение устройства: пустая банка
                // работает из сумы, в руки её брать незачем.
                Say("Пустая банка нужна в суме, а не в руках: душа наливается прямо туда.");
                return;
            }

            Satchel.Take(i);

            bool took = slot.Kind == CarryKind.Shell
                ? CryptHands.TakeShell(slot.Shell)
                : CryptHands.TakeSoul(slot.Soul, slot.Quality);

            if (!took)
            {
                // Руки оказались заняты между проверкой и взятием —
                // класть обратно обязательно, иначе вещь исчезнет молча.
                Satchel.PutAt(i, slot);
                return;
            }

            Say($"В руках: {CryptHands.What}.");
        }

        /// <summary>Из руки в ячейку.</summary>
        private void ToSatchel(int i)
        {
            var slot = CryptHands.HasShell
                ? new Satchel.Slot { Kind = CarryKind.Shell, Shell = CryptHands.Shell }
                : new Satchel.Slot
                {
                    Kind = CarryKind.Jar,
                    Soul = CryptHands.Soul,
                    Quality = CryptHands.Quality,
                };

            if (!Satchel.PutAt(i, slot))
            {
                Say($"Ячейка {i + 1} занята: {Satchel.Describe(i)}.");
                return;
            }

            CryptHands.Drop();
            Say($"В суму, ячейка {i + 1}: {Satchel.Describe(i)}.");
        }

        private static void Say(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
