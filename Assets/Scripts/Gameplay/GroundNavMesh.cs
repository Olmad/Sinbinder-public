// Assets/Scripts/Gameplay/GroundNavMesh.cs
using UnityEngine;
using Unity.AI.Navigation;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Печёт навмеш при запуске сцены.
    ///
    /// Навмеша в собранных сценах не было вовсе, и без него агент —
    /// мёртвый груз: <c>SetDestination</c> не находит поверхности, и воин
    /// стоит. Печь заранее и хранить данные ассетом можно, но тогда их
    /// надо не забыть пересобрать после каждой правки геометрии — а забыть
    /// это ровно тот вид ошибки, от которого здесь страдали больше всего:
    /// молчаливый.
    ///
    /// Земля в демо — плоскость, поэтому выпечка стоит доли секунды.
    /// В Awake, а не в Start: спавнеры создают воинов в своих Start,
    /// и поверхность обязана существовать раньше их.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    [DefaultExecutionOrder(-900)]
    public class GroundNavMesh : MonoBehaviour
    {
        void Awake()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface == null) return;

            surface.BuildNavMesh();
            Debug.Log("[ЗЕМЛЯ] Навмеш испечён.");
        }
    }
}
