// Assets/Scripts/Core/LocSetup.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Выбор языка в игре: какой язык помнить, откуда брать таблицу
    /// (docs/38-LANG.md). Сам перевод — <see cref="Loc"/>; здесь только то,
    /// что требует Unity: <c>PlayerPrefs</c> и <c>Resources</c>.
    ///
    /// Таблицы — <c>Resources/Lang/&lt;код&gt;.txt</c>. Новый язык — это новый
    /// файл и строка в <see cref="Languages"/>, кода больше не нужно.
    ///
    /// По умолчанию — русский, даже на английской системе: пока перевод
    /// не закончен, игра наполовину по-английски хуже, чем целиком по-русски.
    /// Когда английский будет полным — <see cref="EnglishByDefault"/>.
    /// </summary>
    public static class LocSetup
    {
        private const string Key = "sinbinder.language";

        /// <summary>
        /// Языки в меню, по кругу. Название — на самом языке: игрок,
        /// случайно включивший чужой, должен узнать свой, не читая чужого.
        /// </summary>
        public static readonly (string Code, string Name)[] Languages =
        {
            ("ru", "Русский"),   // ключ: название языка — на нём самом
            ("en", "English"),
        };

        /// <summary>Английский перевод полон — нерусской системе давать его сразу.</summary>
        private static readonly bool EnglishByDefault = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            string code = PlayerPrefs.GetString(Key, Default());
            Apply(code, remember: false);
        }

        private static string Default()
        {
            if (!EnglishByDefault) return Loc.Source;
            var system = Application.systemLanguage;
            return system == SystemLanguage.Russian || system == SystemLanguage.Ukrainian
                || system == SystemLanguage.Belarusian ? "ru" : "en";
        }

        /// <summary>Название текущего языка — для строки меню.</summary>
        public static string CurrentName()
        {
            foreach (var l in Languages)
                if (l.Code == Loc.Language) return l.Name;
            return Languages[0].Name;
        }

        /// <summary>Следующий язык по кругу — строка «Язык» в меню паузы.</summary>
        public static void Cycle()
        {
            int i = 0;
            for (; i < Languages.Length; i++)
                if (Languages[i].Code == Loc.Language) break;
            Apply(Languages[(i + 1) % Languages.Length].Code, remember: true);
        }

        /// <summary>
        /// Включить язык. Таблицы нет — остаёмся на русском и говорим
        /// об этом один раз, а не молчим: иначе «перевод не работает»
        /// искали бы в коде, а не в пропавшем файле.
        /// </summary>
        public static void Apply(string code, bool remember)
        {
            if (string.IsNullOrEmpty(code) || code == Loc.Source)
            {
                Loc.Use(Loc.Source, null);
            }
            else
            {
                var table = Resources.Load<TextAsset>("Lang/" + code);
                if (table == null)
                {
                    Debug.LogWarning($"[ЯЗЫК] Нет таблицы Resources/Lang/{code}.txt — остаюсь на русском.");
                    Loc.Use(Loc.Source, null);
                    code = Loc.Source;
                }
                else
                {
                    int n = Loc.Use(code, table.text);
                    Debug.Log($"[ЯЗЫК] {code}: переведено строк — {n}.");
                }
            }

            if (!remember) return;
            PlayerPrefs.SetString(Key, code);
            PlayerPrefs.Save();
        }
    }
}
