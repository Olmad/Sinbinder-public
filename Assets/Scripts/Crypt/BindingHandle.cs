// Assets/Scripts/Crypt/BindingHandle.cs
// Перевод: текст через Loc
using UnityEngine;

using Sinbinder.Core;
namespace Sinbinder.Crypt
{
    /// <summary>
    /// Рычаг устройства связывания. Тот самый «БУМ».
    ///
    /// Не гаснет и не прячется, когда дёргать нельзя: он <b>отвечает</b>.
    /// Игрок, дошедший до рычага с истлевшей душой и големом, обязан
    /// услышать причину, а не увидеть серую кнопку. Отказ, у которого
    /// названа причина, — это механика; отказ без причины — интерфейс,
    /// который сломался.
    /// </summary>
    public class BindingHandle : CryptInteractable
    {
        [SerializeField] private BindingDevice _device;

        public override string Label => Loc.T("Рычаг связывания");

        protected override string Hint
        {
            get
            {
                if (_device == null) return Label;

                string no = _device.NotReady;
                if (!string.IsNullOrEmpty(no)) return $"{Label}: {no}";

                string foretell = _device.Foretell();
                return string.IsNullOrEmpty(foretell)
                    ? Loc.F("{0} — дёрнуть на {1}.", Label, _key)
                    : Loc.F("{0} Дёрнуть на {1}.", foretell, _key);
            }
        }

        void Start()
        {
            if (_device == null) _device = Object.FindFirstObjectByType<BindingDevice>();

            if (_device == null)
                Debug.LogError("[СКЛЕП] Рычаг без устройства: дёргать нечего.");
        }

        protected override void Use() => _device?.Bind();

        /// <summary>Настроить из сборщика сцены.</summary>
        public void Set(BindingDevice device) => _device = device;
    }
}
