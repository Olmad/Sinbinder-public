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

        /// <summary>Свой набор галочек. Минус один — своего набора нет.</summary>
        private const string CustomKey = "sinbinder.clarity.custom";

        /// <summary>Смотрит ли игрок сейчас свой набор, а не ступень.</summary>
        private const string CustomOnKey = "sinbinder.clarity.custom.on";

        [Tooltip("Что показывать игроку. Выше третьей ступени в сборке "
               + "не поднимется: четвёртая — не настройка, а замок.")]
        [SerializeField] private Clarity _level = Transparency.Default;

        [Tooltip("Помнить выбор между запусками. Снять, чтобы сцена всегда "
               + "начиналась с того, что выставлено здесь.")]
        [SerializeField] private bool _remember = true;

        [Tooltip("Смотреть глазами игрока. В редакторе четвёртая ступень "
               + "открыта всегда, и увидеть то, что видит игрок, иначе "
               + "нечем: автор проверяет игру, которой не существует. "
               + "Поставить — и редактор ведёт себя как сборка.")]
        [SerializeField] private bool _asPlayer;

        void Awake()
        {
#if UNITY_EDITOR
            // Автор смотрит на голоса модулей — это его работа. Но
            // проверять демо надо и с той стороны: галка «глазами игрока»
            // закрывает замок и в редакторе.
            Transparency.SetDeveloper(!_asPlayer);

            if (_asPlayer)
            {
                if (_level > Transparency.PlayerCeiling) _level = Transparency.PlayerCeiling;
                Debug.Log($"[ПРОЗРАЧНОСТЬ] Глазами игрока: "
                        + $"«{Transparency.Describe(_level)}». "
                        + "Трассировки не будет — так и задумано.");
            }
            else if (_level < Clarity.Trace)
            {
                _level = Clarity.Trace;
            }
#else
            Transparency.SetDeveloper(false);
#endif

            var wanted = _level;

            // Отсутствие сохранённого выбора — событие, а не ноль: берём
            // то, что выставлено в сцене, а не молчаливую нулевую ступень.
            if (_remember && PlayerPrefs.HasKey(Key))
                wanted = (Clarity)PlayerPrefs.GetInt(Key, (int)_level);

            var got = Transparency.Set(wanted);

            // Свой набор восстанавливаем после ступени: Set снимает
            // с него выбор, и в обратном порядке он бы гас собственной
            // загрузкой. Сам набор восстанавливаем всегда, а включаем
            // только если игрок на нём и остановился: «набор есть»
            // и «смотрим набор» — разные вещи.
            if (_remember && PlayerPrefs.GetInt(CustomKey, -1) >= 0)
            {
                Transparency.SetCustom((Detail)PlayerPrefs.GetInt(CustomKey, 0));
                if (PlayerPrefs.GetInt(CustomOnKey, 0) == 0) Transparency.Set(got);
            }

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
                PlayerPrefs.SetInt(CustomOnKey, 0);
                PlayerPrefs.Save();
            }

            return got;
        }

        /// <summary>
        /// Поставить свой набор галочек и запомнить его.
        /// Возвращает тот, что получился: просьбу показать цифры
        /// обрежут, и вызывающий обязан это увидеть.
        /// </summary>
        public Detail ChooseCustom(Detail wanted)
        {
            var got = Transparency.SetCustom(wanted);

            if (_remember)
            {
                PlayerPrefs.SetInt(CustomKey, (int)got);
                PlayerPrefs.SetInt(CustomOnKey, 1);
                PlayerPrefs.Save();
            }

            return got;
        }

        /// <summary>Вернуться к собранному набору, не пересобирая его.</summary>
        public bool UseCustom()
        {
            if (!Transparency.UseCustom()) return false;

            if (_remember)
            {
                PlayerPrefs.SetInt(CustomOnKey, 1);
                PlayerPrefs.Save();
            }

            return true;
        }
    }
}
