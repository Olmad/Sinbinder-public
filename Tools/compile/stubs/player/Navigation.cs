// Заглушка пакета com.unity.ai.navigation: только то, что зовёт игра.
namespace Unity.AI.Navigation
{
    public enum CollectObjects { All = 0, Volume = 1, Children = 2, MarkedWithModifier = 3 }

    public class NavMeshSurface : UnityEngine.MonoBehaviour
    {
        public void BuildNavMesh() { }
        public CollectObjects collectObjects { get; set; }
    }

    // Нутро палаток (DemoSceneBuilder.Furnish): модель не идёт в выпечку,
    // стены — объёмами «не пройти». Свойства — как в пакете 2.0.
    public class NavMeshModifier : UnityEngine.MonoBehaviour
    {
        public bool ignoreFromBuild { get; set; }
    }

    public class NavMeshModifierVolume : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Vector3 size { get; set; }
        public UnityEngine.Vector3 center { get; set; }
        public int area { get; set; }
    }
}
