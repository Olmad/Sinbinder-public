// Assets/Scripts/Crypt/BindingSocket.cs
using UnityEngine;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Гнездо устройства: сюда кладут душу или тело, отсюда забирают
    /// обратно. Само ничего не решает — только передаёт устройству.
    /// </summary>
    public class BindingSocket : CryptInteractable
    {
        public enum Slot { Soul = 0, Shell = 1 }

        [SerializeField] private Slot _slot = Slot.Soul;
        [SerializeField] private BindingDevice _device;

        public override string Label
            => _slot == Slot.Soul ? "Гнездо души" : "Ложе тела";

        protected override string Hint
        {
            get
            {
                bool filled = _slot == Slot.Soul
                    ? _device != null && _device.HasSoul
                    : _device != null && _device.HasShell;

                if (filled) return $"{Label} занято — забрать на {_key}.";

                return CryptHands.Empty
                    ? $"{Label} пусто. Принесите то, что сюда кладут."
                    : $"{Label} — положить на {_key}.";
            }
        }

        void Start()
        {
            if (_device == null) _device = Object.FindFirstObjectByType<BindingDevice>();

            if (_device == null)
                Debug.LogError("[СКЛЕП] Гнездо без устройства: класть некуда.");
        }

        protected override void Use()
        {
            if (_device == null) return;

            bool put = _slot == Slot.Soul ? _device.PutSoul() : _device.PutShell();
            if (put) { Announce(); return; }

            bool took = _slot == Slot.Soul ? _device.TakeSoulBack() : _device.TakeShellBack();
            if (took) { Say($"{Label}: забрано обратно."); return; }

            // Ни положить, ни забрать — значит в руках не то. Сказать
            // об этом надо словами: молчащий предмет читается как
            // сломанный.
            Say(CryptHands.Empty
                ? $"{Label}: в руках пусто."
                : $"{Label}: сюда это не кладут. В руках {CryptHands.What}.");
        }

        /// <summary>Что вышло и что теперь можно.</summary>
        private void Announce()
        {
            Say($"{Label}: положено.");

            string no = _device.NotReady;

            if (!string.IsNullOrEmpty(no)) { Say(no); return; }

            string foretell = _device.Foretell();
            if (!string.IsNullOrEmpty(foretell)) Say(foretell);
        }

        /// <summary>Настроить из сборщика сцены.</summary>
        public void Set(Slot slot, BindingDevice device)
        {
            _slot = slot;
            _device = device;
        }
    }
}
