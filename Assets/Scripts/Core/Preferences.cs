// Assets/Scripts/Core/Preferences.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Что игрок настроил под себя, а не под игру.
    ///
    /// Заведено 18 сентября под сборку: её получают незнакомые люди,
    /// у которых другой монитор, другая мышь и своя привычка.
    /// До этого дня выйти из полного экрана было нечем, а чувствительность
    /// жила в поле <c>RTS_Camera</c>, до которого игроку не дотянуться.
    ///
    /// <b>Здесь только то, чего больше нигде нет.</b> Ясность настраивается
    /// своей панелью (клавиша O), сохранения — своими гнёздами, и заводить
    /// им вторую настройку значило бы развести две правды при первой же
    /// правке. Громкости здесь нет по другой причине: **звука в игре
    /// ноль файлов**, и рычаг для несуществующего — это мишура.
    ///
    /// Значения помнятся между запусками тем же <c>PlayerPrefs</c>,
    /// что и выбор прозрачности.
    /// </summary>
    public static class Preferences
    {
        private const string FullscreenKey = "sinbinder.fullscreen";
        private const string SensitivityKey = "sinbinder.sensitivity";
        private const string GraphicsKey = "sinbinder.graphics.light";

        /// <summary>
        /// Облегчённая графика: без теней и без затенения углов
        /// (<see cref="FrameBudget"/>). Пока игрок не выбрал сам — по
        /// видеокарте: на встроенной облегчённая (<see cref="Integrated"/>).
        /// </summary>
        public static bool LightGraphics { get; private set; }

        /// <summary>
        /// Во сколько раз игрок просит быстрее или медленнее. Множитель,
        /// а не замена: основа остаётся в сцене, у камеры, и подбирал её
        /// автор.
        /// </summary>
        public static float SensitivityScale { get; private set; } = 1f;

        /// <summary>Ступени чувствительности. Цифр игрок не видит — только слова.</summary>
        private static readonly (float Scale, string Name)[] Steps =
        {
            (0.5f,  Loc.N("медленно")),
            (0.75f, Loc.N("неспешно")),
            (1.0f,  Loc.N("как задумано")),
            (1.5f,  Loc.N("быстро")),
            (2.0f,  Loc.N("очень быстро")),
        };

        /// <summary>
        /// Применить сохранённое до того, как соберётся первая сцена.
        /// Иначе игрок, выставивший окно, всё равно получал бы полный
        /// экран при каждом запуске — то есть настройка не настраивала бы.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            SensitivityScale = PlayerPrefs.GetFloat(SensitivityKey, 1f);

            LightGraphics = PlayerPrefs.HasKey(GraphicsKey)
                ? PlayerPrefs.GetInt(GraphicsKey, 0) != 0
                : Integrated();

            // Отсутствие выбора — событие, а не ноль: не трогаем полный
            // экран вовсе, пока игрок не сказал своего.
            if (PlayerPrefs.HasKey(FullscreenKey))
                Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, 1) != 0;
        }

        public static string ScreenName() => Screen.fullScreen ? Loc.T("полный экран") : Loc.T("в окне");

        public static void ToggleScreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            PlayerPrefs.SetInt(FullscreenKey, Screen.fullScreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static string SensitivityName()
        {
            // Имена ступеней в таблице — русские (Loc.N): статическая таблица
            // собирается один раз, и переведённая при загрузке не сменила бы
            // язык вслед за игроком. Переводятся при показе.
            foreach (var step in Steps)
                if (Mathf.Approximately(step.Scale, SensitivityScale)) return Loc.T(step.Name);

            return Loc.T(Steps[2].Name);
        }

        /// <summary>
        /// Следующая ступень по кругу. По кругу, а не до упора: игрок,
        /// промахнувшийся мимо своей, иначе застревал бы на краю.
        /// </summary>
        public static void CycleSensitivity()
        {
            int at = 0;
            for (int i = 0; i < Steps.Length; i++)
                if (Mathf.Approximately(Steps[i].Scale, SensitivityScale)) { at = i; break; }

            SensitivityScale = Steps[(at + 1) % Steps.Length].Scale;
            PlayerPrefs.SetFloat(SensitivityKey, SensitivityScale);
            PlayerPrefs.Save();
        }

        public static string GraphicsName() => LightGraphics
            ? Loc.T("облегчённая — без теней")
            : Loc.T("полная");

        /// <summary>Полная или облегчённая — и сразу в игру, без перезапуска.</summary>
        public static void ToggleGraphics()
        {
            LightGraphics = !LightGraphics;
            PlayerPrefs.SetInt(GraphicsKey, LightGraphics ? 1 : 0);
            PlayerPrefs.Save();
            FrameBudget.Apply();
        }

        /// <summary>
        /// Встроенная ли видеокарта — по производителю и имени. Память
        /// не подсказка: встроенная Intel UHD 630 показывает 4 ГБ, общие
        /// с процессором. У Intel встроенные все, кроме Arc (UHD, HD Graphics,
        /// Iris); у AMD встроенные зовутся «Radeon Graphics», «Vega 8
        /// Graphics», а отдельные несут серию — RX.
        ///
        /// Прогон в пакетном режиме — всегда полная: его снимки смотрит автор.
        /// </summary>
        private static bool Integrated()
        {
            if (Application.isBatchMode) return false;

            string name = SystemInfo.graphicsDeviceName ?? "";
            switch (SystemInfo.graphicsDeviceVendorID)
            {
                case 0x8086: return !name.Contains("Arc");
                case 0x1002: return name.Contains("Graphics") && !name.Contains("RX");
                default: return false;
            }
        }
    }
}
