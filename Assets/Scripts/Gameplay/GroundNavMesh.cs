// Assets/Scripts/Gameplay/GroundNavMesh.cs
// Перевод: текст через Loc
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
    ///
    /// В Start, а не в Awake. Модификаторы выпечки — нутро палаток,
    /// с 27 сентября (<c>DemoSceneBuilder.Furnish</c>): модель палатки
    /// не идёт в навмеш, стены — объёмами «не пройти» — встают на учёт
    /// в своём OnEnable, а тот у них идёт после нашего Awake: порядок −900
    /// выше их нуля. Печь в Awake значило печь без них, и палатка
    /// оставалась сплошной преградой — Греховода, проснувшегося в ней,
    /// навмеш выталкивал за скат. К Start все OnEnable сцены прошли,
    /// а раньше спавнеров, которые ставят воинов в своих Start, мы
    /// по-прежнему: порядок −900 держит и Start.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    [DefaultExecutionOrder(-900)]
    public class GroundNavMesh : MonoBehaviour
    {
        void Start()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface == null) return;

            surface.BuildNavMesh();
            Debug.Log("[ЗЕМЛЯ] Навмеш испечён.");
        }
    }
}
