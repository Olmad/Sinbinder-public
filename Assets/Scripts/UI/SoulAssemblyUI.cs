// Assets/_Project/Scripts/UI/SoulAssemblyUI.cs
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Экран сборки: душа плюс оболочка, и распечатанное пророчество.
    ///
    /// Удовольствие здесь не в цифрах, а в цикле «гипотеза — проверка —
    /// пересмотр». Игрок собирает воина, читает, как тот поведёт себя
    /// в четырёх положениях, забирает его в бой и смотрит, верно ли
    /// прочитал. Память меняет воина, и в следующий раз пророчество
    /// будет другим.
    ///
    /// Работает на третьей минуте игры — до того, как игрок вообще
    /// понял, что такое AOS.
    ///
    /// Настройка в сцене: повесить на панель, задать четыре Text.
    /// </summary>
    public class SoulAssemblyUI : MonoBehaviour
    {
        [SerializeField] private Text _name;
        [SerializeField] private Text _spectra;
        [SerializeField] private Text _shell;
        [SerializeField] private Text _prophecy;
        [SerializeField] private Image _accent;

        /// <summary>Показать собранного воина.</summary>
        public void Show(Warrior warrior)
        {
            if (warrior == null) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);

            if (_name != null)
                _name.text = $"{warrior.DisplayName} — {warrior.Soul.GetSinName()}";

            if (_spectra != null)
                _spectra.text = warrior.Soul.GetSpectraDescription();

            if (_shell != null)
                _shell.text = warrior.ShellData != null
                    ? $"{warrior.ShellData.shellName}\n{warrior.ShellData.DescribeBias()}"
                    : "Оболочка не задана.";

            // Сердце экрана. Прогоняются настоящие модули — движок
            // становится собственным интерфейсом, ни одной новой формулы.
            if (_prophecy != null)
                _prophecy.text = TemperamentPredictor.Describe(warrior);

            if (_accent != null)
                _accent.color = WarriorTooltipUI.SinColor(warrior.Soul.Sin);
        }

        /// <summary>
        /// Предпросмотр до связывания: что станет с душой в этой оболочке.
        /// Считается на копии — настоящую душу трогать нельзя, пока игрок
        /// не решил.
        /// </summary>
        public static string PreviewBind(SoulData soul, ShellData shell)
        {
            if (soul == null) return "";
            if (shell == null) return soul.GetSpectraDescription();

            var copy = new SoulData(soul);
            ShellBinder.Bind(copy, shell);
            return copy.GetSpectraDescription();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
