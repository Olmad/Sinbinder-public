// Assets/AOS Engine/Rumour.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Слух об одном воине, живущий в голове другого. Копится через
    /// RumourManager: чем лучше отношения слушателя к герою, тем быстрее
    /// растёт Progress. При 100 слух становится Confirmed.
    /// </summary>
    [System.Serializable]
    public class Rumour
    {
        public string SubjectId;
        public string Text;
        public float Progress;
        public bool Confirmed;
    }
}
