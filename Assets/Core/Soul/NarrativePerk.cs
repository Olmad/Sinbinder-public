// Assets/Core/Soul/NarrativePerk.cs
namespace Sinbinder.Core
{
    /// <summary>
    /// Экземпляр перка на конкретной душе. Ссылается на SoulPerk по имени
    /// и хранит личное состояние: найден ли объект перка, к кому он привязан.
    /// </summary>
    [System.Serializable]
    public class NarrativePerk
    {
        public string PerkName;
        public bool IsFound;
        public string RelatedCharacterID;
    }
}
