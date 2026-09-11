using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Берёт ли игрок на себя обязательство.
    ///
    /// <see cref="Commitment"/> — правило, а это его единственная связь
    /// с миром, ровно как <see cref="TransparencySettings"/> для
    /// прозрачности. Иначе правило существовало бы только в замерах:
    /// включить его было бы нечем.
    ///
    /// Выбор один на партию и меняется <b>между</b> партиями. Снять
    /// обязательство посреди игры — то же самое, что не брать его вовсе,
    /// поэтому здесь нет ни клавиши, ни строки в настройках: галка
    /// в сцене и запись в сохранении. Место ему — в меню новой игры,
    /// когда меню появится.
    ///
    /// Сохранение при загрузке ставит режим само (он лежит в самом файле),
    /// и это важнее галки: снимок ответственной игры не имеет права
    /// открыться свободной.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GameModeSettings : MonoBehaviour
    {
        private const string Key = "sinbinder.commitment";

        [Tooltip("С обязательством: одно сохранение, переиграть нельзя. "
               + "Это не сложность — это режим, в котором игра работает "
               + "так, как задумана: отказ воина обходится клавишей "
               + "загрузки, и тогда весь движок решений работает впустую.")]
        [SerializeField] private bool _commitment;

        [Tooltip("Помнить выбор между запусками.")]
        [SerializeField] private bool _remember = true;

        void Awake()
        {
            bool wanted = _remember && PlayerPrefs.HasKey(Key)
                        ? PlayerPrefs.GetInt(Key) != 0
                        : _commitment;

            Commitment.Set(wanted);

            Debug.Log($"[РЕЖИМ] {Commitment.Describe(wanted)}.");
        }

        /// <summary>
        /// Выбрать режим. Для меню новой игры, когда оно появится, —
        /// и для проверок.
        /// </summary>
        public void Choose(bool commitment)
        {
            _commitment = commitment;
            Commitment.Set(commitment);

            if (!_remember) return;

            PlayerPrefs.SetInt(Key, commitment ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
