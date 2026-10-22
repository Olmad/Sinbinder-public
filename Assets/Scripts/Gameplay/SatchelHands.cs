// Assets/Scripts/Gameplay/SatchelHands.cs
// Перевод: текст через Loc
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
    /// <see cref="_next"/> ходит по банкам, <see cref="_swap"/>
    /// перекладывает душу между рукой и банкой. Две клавиши, а не шесть
    /// цифр: цифры с первой по шестую заняты установкой отряда, и вешать
    /// на них второе значило бы повторить ошибку с клавишей D.
    ///
    /// Банка из сумы не уходит (<see cref="Satchel"/>, с 22 октября): в руки
    /// берут душу, пустая банка остаётся ждать следующей жатвы. Тело в суму
    /// не кладётся — его носят в руках.
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

            // Руки мёртвого сумой не распоряжаются.
            var self = GetComponent<Warrior>();
            if (self != null && self.IsDead) return;

            if (Input.GetKeyDown(_next))
            {
                Satchel.Next();
                // Без номера банки: номер — тоже цифра на экране (автор,
                // 24 сентября). Какая банка под рукой, видно по рамке сумы.
                Say(Satchel.Jars == 0 ? Loc.T("Банок при себе нет.")
                                      : Loc.F("Под рукой: {0}.", Satchel.Describe(Satchel.Selected)));
            }

            if (Input.GetKeyDown(_swap)) Swap();
        }

        private void Swap()
        {
            if (CryptHands.Empty) { FromSatchel(Satchel.Selected); return; }

            ToSatchel();
        }

        /// <summary>Душу из банки — в руку. Банка остаётся в суме.</summary>
        private void FromSatchel(int i)
        {
            var slot = Satchel.At(i);

            if (slot.Empty)
            {
                Say(Loc.T("Банок при себе нет."));
                return;
            }

            if (slot.EmptyJar)
            {
                // Не ошибка игрока, а разъяснение устройства: пустая банка
                // работает из сумы, душа наливается в неё при жатве.
                Say(Loc.T("Банка пуста. Душа нальётся в неё сама, когда вы её соберёте."));
                return;
            }

            Satchel.TakeSoul(i);

            if (!CryptHands.TakeSoul(slot.Soul, slot.Quality))
            {
                // Руки оказались заняты между проверкой и взятием —
                // налить обратно обязательно, иначе душа исчезнет молча.
                Satchel.Fill(i, slot.Soul, slot.Quality);
                return;
            }

            Say(Loc.F("В руках: {0}.", CryptHands.What));
        }

        /// <summary>Душу из руки — в банку: под рукой, если пуста, иначе в первую пустую.</summary>
        private void ToSatchel()
        {
            if (CryptHands.HasShell)
            {
                Say(Loc.T("Тело в суму не влезет — его носят в руках."));
                return;
            }

            int jar = Satchel.JarFor();
            if (jar < 0)
            {
                Say(Satchel.Jars == 0 ? Loc.T("Банок при себе нет.")
                                      : Loc.T("Пустых банок нет — душу некуда налить."));
                return;
            }

            if (!Satchel.Fill(jar, CryptHands.Soul, CryptHands.Quality)) return;

            CryptHands.Drop();
            Say(Loc.F("В банку: {0}.", Satchel.Describe(jar)));
        }

        private static void Say(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
