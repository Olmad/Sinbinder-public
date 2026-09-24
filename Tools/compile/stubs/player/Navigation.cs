// Заглушка пакета com.unity.ai.navigation: только то, что зовёт игра.
namespace Unity.AI.Navigation
{
    public enum CollectObjects { All = 0, Volume = 1, Children = 2, MarkedWithModifier = 3 }

    public class NavMeshSurface : UnityEngine.MonoBehaviour
    {
        public void BuildNavMesh() { }
        public CollectObjects collectObjects { get; set; }
    }
}
