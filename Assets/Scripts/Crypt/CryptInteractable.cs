// Assets/Scripts/Crypt/CryptInteractable.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// То, к чему в склепе подходят и что нажимают.
    ///
    /// Один общий предок на все рычаги, банки, подставки и гнёзда.
    /// Заведён сразу, а не после пятой копии: «подойти и нажать» —
    /// правило мира, и разъехавшись по пяти файлам, оно разъедется
    /// и по смыслу. В одном месте рычаг слушался бы с двух метров,
    /// в другом с трёх, и игрок решил бы, что игра барахлит.
    ///
    /// <b>Подойти значит дойти ногами.</b> Не навести взгляд. На этом
    /// проект уже обжёгся: пока Греховода не существовало в сцене,
    /// «подойти к столу» означало посмотреть на него, и совет падал
    /// на игрока внезапно (`14-HANDOFF.md` §8.1).
    /// </summary>
    public abstract class CryptInteractable : MonoBehaviour
    {
        [Tooltip("С какого расстояния слушается.")]
        [SerializeField] protected float _reach = 2.2f;

        [SerializeField] protected KeyCode _key = KeyCode.F;

        private bool _wasNear;

        /// <summary>Что на нём написано. Читают и табличка, и подсказка.</summary>
        public abstract string Label { get; }

        /// <summary>Что происходит по нажатию.</summary>
        protected abstract void Use();

        /// <summary>
        /// Готов ли слушаться прямо сейчас. Неготовое молчит и не мигает
        /// подсказкой: обещание, которого предмет не выполнит, хуже
        /// молчания.
        /// </summary>
        protected virtual bool Ready => true;

        /// <summary>Что сказать при подходе.</summary>
        protected virtual string Hint => $"{Flat(Label)} — нажмите {_key}.";

        void Update()
        {
            bool near = Near();

            // Подсказка один раз на подход, а не каждый кадр: иначе
            // журнал заполнится одной строкой за две секунды.
            if (near && !_wasNear && Ready) Say(Hint);
            _wasNear = near;

            if (!near || !Ready) return;
            if (!Input.GetKeyDown(_key)) return;

            Use();
        }

        protected bool Near()
        {
            if (!SinbinderPlayer.Exists) return false;

            return CampFocus.GroundDistance(SinbinderPlayer.Where, transform.position)
                   <= _reach;
        }

        /// <summary>Табличка бывает в две строки, а журнал — в одну.</summary>
        protected static string Flat(string text)
            => string.IsNullOrEmpty(text)
                ? ""
                : text.Replace(System.Environment.NewLine, " ").Replace("\n", " ");

        protected static void Say(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
