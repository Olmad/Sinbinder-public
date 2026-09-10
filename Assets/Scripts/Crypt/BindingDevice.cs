// Assets/Scripts/Crypt/BindingDevice.cs
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Устройство связывания: два гнезда и рычаг.
    ///
    /// <b>Правило выбора тела становится предметом.</b> На экране выбора
    /// недоступное тело — это серая строка, которую игрок пролистывает.
    /// Здесь он несёт истлевшую душу к голему через всю комнату,
    /// и устройство её <b>не принимает</b>, сказав почему. Правило
    /// то же самое (<see cref="ShellChoice"/>), и считает его тот же
    /// код — но ошибка теперь стоит дороги, а значит запоминается.
    ///
    /// Устройство ничего не решает само: и допуск, и предсказание,
    /// и сборку поднятого оно спрашивает у общих правил. Своя проверка
    /// здесь была бы второй правдой и разошлась бы с экраном выбора
    /// при первой же правке смещений.
    /// </summary>
    public class BindingDevice : MonoBehaviour
    {
        [Tooltip("Куда встаёт поднятый.")]
        [SerializeField] private Transform _riseSpot;

        private SoulData _soul;
        private SoulQuality _quality;

        private bool _hasShell;
        private ShellType _shell;

        private RelationshipSystem _relations;

        public bool HasSoul => _soul != null;
        public bool HasShell => _hasShell;

        /// <summary>Готово ли к рывку рычага — и если нет, то почему.</summary>
        public string NotReady
        {
            get
            {
                if (!HasSoul && !HasShell) return "Устройство пусто.";
                if (!HasSoul) return "Нет души.";
                if (!HasShell) return "Нет тела.";

                var data = ShellLibrary.Get(_shell);
                if (data == null) return "";

                // Тот же допуск, что и на экране выбора тела. Не копия
                // правила, а оно само.
                return ShellChoice.Allows(data, _quality)
                    ? ""
                    : ShellChoice.Refusal(data, _quality);
            }
        }

        /// <summary>Положить в гнездо то, что в руках. Возвращает, вышло ли.</summary>
        public bool PutSoul()
        {
            if (!CryptHands.HasSoul) return false;
            if (HasSoul) return false;

            _soul = CryptHands.Soul;
            _quality = CryptHands.Quality;
            CryptHands.Drop();
            return true;
        }

        public bool PutShell()
        {
            if (!CryptHands.HasShell) return false;
            if (HasShell) return false;

            _shell = CryptHands.Shell;
            _hasShell = true;
            CryptHands.Drop();
            return true;
        }

        /// <summary>Забрать душу обратно в руки — передумал.</summary>
        public bool TakeSoulBack()
        {
            if (!HasSoul || !CryptHands.Empty) return false;

            CryptHands.TakeSoul(_soul, _quality);
            _soul = null;
            return true;
        }

        public bool TakeShellBack()
        {
            if (!HasShell || !CryptHands.Empty) return false;

            CryptHands.TakeShell(_shell);
            _hasShell = false;
            return true;
        }

        /// <summary>Что выйдет, если дёрнуть. Считает настоящий ShellBinder.</summary>
        public string Foretell()
        {
            if (!HasSoul || !HasShell) return "";

            var data = ShellLibrary.Get(_shell);
            var after = ShellChoice.Preview(_soul, data);
            if (after == null) return "";

            return after.Sin == _soul.Sin
                ? $"Встанет прежним: {SoulData.GetSinName(after.Sin)}."
                : $"Встанет другим: {SoulData.GetSinName(after.Sin)}. Тело перетянуло.";
        }

        /// <summary>Дёрнуть рычаг.</summary>
        public bool Bind()
        {
            string no = NotReady;
            if (!string.IsNullOrEmpty(no)) { Log(no); return false; }

            _relations ??= new RelationshipSystem(AOS.MemoryProcessor.Instance);

            var at = _riseSpot != null ? _riseSpot.position
                                       : transform.position + transform.forward * 2f;

            var risen = Raising.Rise(_soul, _shell, at, transform.rotation, _relations);
            if (risen == null) return false;

            Log($"{risen.DisplayName} поднялся и встал рядом.");

            if (_soul.Memory == null)
                Log("Он не помнит, кем был. Слушается — и только.");

            _soul = null;
            _hasShell = false;
            return true;
        }

        /// <summary>
        /// Вернуть недовселённую душу на полку при уходе со сцены.
        ///
        /// Душа, оставленная в гнезде, иначе пропала бы совсем: с полки
        /// её сняли, а в тело не положили. Потерянная душа — это потеря
        /// без причины, а в этой игре у всякой потери обязана быть причина.
        /// </summary>
        void OnDestroy()
        {
            if (_soul == null) return;

            SoulManager.Instance?.PutBack(new SoulManager.Kept(_soul, _quality));
            _soul = null;
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
