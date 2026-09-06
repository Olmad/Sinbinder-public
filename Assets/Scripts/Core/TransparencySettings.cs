// Assets/Scripts/Core/TransparencySettings.cs
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Что игрок выбрал показывать — и почему в редакторе это другое.
    ///
    /// <see cref="Transparency"/> — правило, а это его единственная связь
    /// с миром: применить выбор при запуске сцены и запомнить его между
    /// запусками. Настройка одна на игру, поэтому висит на Managers.
    ///
    /// В редакторе четвёртая ступень открыта, и по умолчанию стоит она:
    /// трассировка с очками и весами — рабочий инструмент автора, и
    /// отбирать его нельзя. В сборке замок закрыт, и открыть его изнутри
    /// нечем: <c>UNITY_EDITOR</c> там просто не определён.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class TransparencySettings : MonoBehaviour
    {
        private const string Key = "sinbinder.clarity";

        [Tooltip("Что показывать игроку. Выше третьей ступени в сборке "
               + "не поднимется: четвёртая — не настройка, а замок.")]
        [SerializeField] private Clarity _level = Transparency.Default;

        [Tooltip("Помнить выбор между запусками. Снять, чтобы сцена всегда "
               + "начиналась с того, что выставлено здесь.")]
        [SerializeField] private bool _remember = true;

        void Awake()
        {
#if UNITY_EDITOR
            // Автор смотрит на голоса модулей — это его работа.
            Transparency.SetDeveloper(true);
            if (_level < Clarity.Trace) _level = Clarity.Trace;
#else
            Transparency.SetDeveloper(false);
#endif

            var wanted = _level;

            // Отсутствие сохранённого выбора — событие, а не ноль: берём
            // то, что выставлено в сцене, а не молчаливую нулевую ступень.
            if (_remember && PlayerPrefs.HasKey(Key))
                wanted = (Clarity)PlayerPrefs.GetInt(Key, (int)_level);

            var got = Transparency.Set(wanted);

            if (got != wanted)
                Debug.Log($"[ПРОЗРАЧНОСТЬ] Просили «{Transparency.Describe(wanted)}», "
                        + $"доступно «{Transparency.Describe(got)}».");
        }

        /// <summary>
        /// Сменить ступень на ходу — из меню настроек, когда оно появится.
        /// Возвращает ту, что получилась.
        /// </summary>
        public Clarity Choose(Clarity level)
        {
            var got = Transparency.Set(level);
            _level = got;

            if (_remember)
            {
                PlayerPrefs.SetInt(Key, (int)got);
                PlayerPrefs.Save();
            }

            return got;
        }
    }
}
