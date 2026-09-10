// Assets/Scripts/Crypt/BindingHandle.cs
using UnityEngine;

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

        public override string Label => "Рычаг связывания";

        protected override string Hint
        {
            get
            {
                if (_device == null) return Label;

                string no = _device.NotReady;
                if (!string.IsNullOrEmpty(no)) return $"{Label}: {no}";

                string foretell = _device.Foretell();
                return string.IsNullOrEmpty(foretell)
                    ? $"{Label} — дёрнуть на {_key}."
                    : $"{foretell} Дёрнуть на {_key}.";
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
