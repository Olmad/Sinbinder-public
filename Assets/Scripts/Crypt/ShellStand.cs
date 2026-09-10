// Assets/Scripts/Crypt/ShellStand.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Тело на столе: его берут и несут к устройству.
    ///
    /// Табличка говорит, <b>к чему тело тянет душу</b>, а не сколько
    /// у него здоровья. Оболочка в этой игре — не набор характеристик,
    /// а давление: «тело волка добавляет Гнева и отнимает Терпения»
    /// (<c>ShellData</c>). Показав вместо этого крепость и скорость,
    /// мы бы описали не ту механику.
    ///
    /// Тела не кончаются: подставка — не склад, а витрина. Считать
    /// оболочки имеет смысл там, где их добывают, и это работа основной
    /// игры, а не полигона.
    /// </summary>
    public class ShellStand : CryptInteractable
    {
        [SerializeField] private ShellType _shell = ShellType.Zombie;

        public ShellType Shell => _shell;

        public override string Label
        {
            get
            {
                var data = ShellLibrary.Get(_shell);
                string pull = data != null ? data.DescribeBias() : "Тело ничего не навязывает.";
                return $"{CryptHands.ShellName(_shell)}\n{pull}";
            }
        }

        protected override string Hint
            => CryptHands.Empty
                ? $"{Flat(Label)} — взять на {_key}."
                : $"{Flat(Label)}. В руках {CryptHands.What}.";

        protected override void Use()
        {
            if (!CryptHands.Empty)
            {
                Say($"Руки заняты: {CryptHands.What}.");
                return;
            }

            CryptHands.TakeShell(_shell);
            Say($"Взято тело: {CryptHands.ShellName(_shell)}.");
        }

        /// <summary>Настроить из сборщика сцены.</summary>
        public void Set(ShellType shell) => _shell = shell;
    }
}
