using System;
using System.Collections.Generic;
using Sinbinder.Core;

internal static class SoulDescriptionChecks
{
    // Descriptions in ascending spectrum order, from strong virtue to strong vice.
    private static readonly (SinType Spectrum, string[] Descriptions)[] Spectra =
    {
        (SinType.Greed, new[] { "Презирает богатство", "Равнодушен к золоту", "Ценит золото", "Любит золото", "Одержим золотом до безумия" }),
        (SinType.Pride, new[] { "Скромен до самоуничижения", "Признаёт других", "Уверен в себе", "Высокомерен", "Считает себя богом" }),
        (SinType.Wrath, new[] { "Невозмутим как камень", "Терпелив", "Сдержан", "Вспыльчив", "Неудержим в ярости" }),
        (SinType.Envy, new[] { "Восхищается другими", "Рад за других", "Нейтрален", "Завидует молча", "Завидует всему живому" }),
        (SinType.Lust, new[] { "Неприступен", "Верен", "Сдержан", "Падок на соблазны", "Ненасытен в желаниях" }),
        (SinType.Gluttony, new[] { "Воздержан", "Умерен", "Ест в меру", "Любит поесть", "Готов сожрать всё" }),
        (SinType.Sloth, new[] { "Неутомим", "Старателен", "Работает без огня", "Ленив", "Апатичен ко всему" }),
    };

    private static readonly (float Value, int Description)[] RepresentativeValues =
    {
        (-100f, 0), (-30f, 1), (0f, 2), (30f, 3), (100f, 4),
    };

    private static readonly (float Value, int Description)[] ThresholdValues =
    {
        (-60.01f, 0), (-60f, 0), (-59.99f, 1),
        (-10.01f, 1), (-10f, 1), (-9.99f, 2),
        (9.99f, 2), (10f, 2), (10.01f, 3),
        (59.99f, 3), (60f, 3), (60.01f, 4),
    };

    internal static void Run(Action<bool, string> check, string englishCatalog)
    {
        CheckLanguage(check, Loc.Source, null,
            "Щедрость: Презирает богатство", "Гнев: Неудержим в ярости");
        CheckLanguage(check, "en", englishCatalog,
            "Generosity: Despises wealth", "Wrath: Unstoppable in rage");
        Loc.Use(Loc.Source, null);
    }

    private static void CheckLanguage(Action<bool, string> check, string language, string catalog,
        string generosity, string wrath)
    {
        Loc.Use(language, catalog);
        foreach (var (spectrum, descriptions) in Spectra)
        {
            if (!Loc.IsSource)
                check(Array.TrueForAll(descriptions, key => Loc.Has(key) && Loc.T(key) != key),
                    $"{language} {spectrum} has translations for all five descriptions");

            var soul = new SoulData("Spectrum fixture", spectrum, MoralType.Neutral, 1);
            string[] expected = Array.ConvertAll(descriptions, Loc.T);
            CheckValues(check, soul, spectrum, expected, RepresentativeValues, "five signed tiers");
            CheckValues(check, soul, spectrum, expected, ThresholdValues, "exact threshold boundaries");
        }

        var mixed = new SoulData("Mixed spectrum fixture", MoralType.Neutral, 1, null);
        mixed.Set(SinType.Greed, -80f);
        mixed.Set(SinType.Wrath, 80f);
        mixed.Set(SinType.Envy, 5f);
        check(mixed.GetSpectraDescription() == generosity + Environment.NewLine + wrath,
            $"{language} displayed spectrum names and descriptions agree on virtue and vice");
    }

    private static void CheckValues(Action<bool, string> check, SoulData soul, SinType spectrum,
        string[] expected, (float Value, int Description)[] values, string group)
    {
        var mismatches = new List<string>();
        foreach (var (value, description) in values)
        {
            soul.Set(spectrum, value);
            string actual = soul.GetIntensityDescription(spectrum);
            if (actual != expected[description])
                mismatches.Add($"{value}: expected '{expected[description]}', got '{actual}'");
        }

        string details = mismatches.Count == 0 ? "" : " | " + string.Join(" | ", mismatches);
        check(mismatches.Count == 0, $"{Loc.Language} {spectrum} {group}{details}");
    }
}
