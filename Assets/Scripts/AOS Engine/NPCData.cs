// Assets/Scripts/AOS Engine/NPCData.cs
// Перевод: текст через Loc
namespace Sinbinder.AOS
{
    /// <summary>
    /// Небоевой персонаж миссии (путник, старейшина). Не имеет души
    /// и не проходит через AOS — это объект решения, а не участник.
    /// </summary>
    [System.Serializable]
    public class NPCData
    {
        public string Id;
        public string DisplayName;
        public bool IsInnocent = true;
        public bool IsAlive = true;
    }
}
