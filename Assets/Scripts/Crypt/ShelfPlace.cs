// Assets/Scripts/Crypt/ShelfPlace.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Свободное место на полке: сюда ставят душу из рук.
    ///
    /// До него полка была витриной в одну сторону — с неё брали, на неё
    /// не клали. Душа, собранная жатвой, лежала в суме и деться оттуда
    /// могла только в устройство связывания: между «поднял» и «вселил»
    /// не было ни одного шага, который делал бы игрок сам.
    ///
    /// Решение автора от 12 сентября: души ставятся на стенды руками.
    /// Это и есть здесь.
    ///
    /// Пустует и молчит, когда ставить нечего: <see cref="Ready"/> ложна,
    /// пока в руках не душа. Предмет, который обещает то, чего не сделает,
    /// хуже предмета молчащего.
    /// </summary>
    public class ShelfPlace : CryptInteractable
    {
        private SoulShelf _shelf;

        public void Bind(SoulShelf shelf) => _shelf = shelf;

        /// <summary>Слушается, только когда в руках душа и на полке есть место.</summary>
        protected override bool Ready => CryptHands.HasSoul && _shelf != null;

        public override string Label => "Свободное место";

        protected override string Hint
            => $"Поставить сюда {CryptHands.Soul?.Name} — нажмите {_key}.";

        protected override void Use()
        {
            if (!CryptHands.HasSoul) return;

            var souls = SoulManager.Instance;
            if (souls == null)
            {
                Say("Класть некуда: душами в этой сцене никто не заведует.");
                return;
            }

            string name = CryptHands.Soul.Name;

            // Душа уходит из рук на полку по-настоящему: PutBack кладёт
            // её в тот же список, который полка и показывает. Второй
            // правды о том, где она, не заводим.
            souls.PutBack(new SoulManager.Kept(CryptHands.Soul, CryptHands.Quality));
            CryptHands.Drop();

            Say($"{name} — на полке.");

            _shelf.Rebuild();
        }
    }
}
