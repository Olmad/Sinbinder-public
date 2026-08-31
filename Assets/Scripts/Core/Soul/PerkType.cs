// Assets/_Project/Scripts/Core/Soul/PerkType.cs
namespace Sinbinder.Core
{
    /// <summary>
    /// Категория нарративного перка души. Определяет, откуда перк родом,
    /// а не что он делает — поведение задаётся списком PerkEffect.
    /// </summary>
    public enum PerkType
    {
        Secret,         // Тайна, о которой никто не знает
        Family,         // Кровная связь (мать, брат, дитя)
        Mentor,         // Учитель или ученик
        FormerFaction,  // Прошлая принадлежность (Охотник, Магистр)
        Debtor,         // Долг перед кем-то
        Rival           // Личное соперничество
    }
}
