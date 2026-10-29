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

            // Брошенные палатки закрыты целиком (DemoSceneBuilder.Furnish: closed) — к павшим не ходят, — но
            // вещи внутри пеклись ступенькой в шаг агента (0,4 м), и воин шёл поверх палатки (проход демо
            // 29.10: Косой Ждан у «Палатки павшего 2» на 0,40 м). Всё под такой палаткой — не дорога.
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Палатка павшего") || t.GetComponent<NavMeshModifier>() != null) continue;   // ключ: имя сборщика сцены
                var not = t.gameObject.AddComponent<NavMeshModifier>();
                not.overrideArea = true;
                not.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
                not.applyToChildren = true;
            }

            surface.BuildNavMesh();
            Debug.Log("[ЗЕМЛЯ] Навмеш испечён.");
        }

        private static bool _again;

        /// <summary>
        /// Испечь заново в конце кадра — встали постройки (Residents: избы, в которые входят). Сколько бы
        /// их ни встало за кадр — печём один раз.
        /// </summary>
        public static void Rebake() => _again = true;

        void LateUpdate()
        {
            if (!_again) return;
            _again = false;
            var surface = GetComponent<NavMeshSurface>();
            if (surface == null) return;
            // Подвижное — не земля: двери склепов и прочее с препятствием (NavMeshObstacle) вырезают себя сами;
            // запёкшись, открытая створка становилась стеной (поле 29.10: «наружу при открытой двери: PathPartial»).
            foreach (var o in FindObjectsByType<UnityEngine.AI.NavMeshObstacle>(FindObjectsSortMode.None))
                if (o.GetComponent<NavMeshModifier>() == null) o.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            foreach (var d in FindObjectsByType<CryptDoor>(FindObjectsSortMode.None))
                if (d.Leaf != null && d.Leaf.GetComponent<NavMeshModifier>() == null) d.Leaf.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            surface.BuildNavMesh();
            Debug.Log("[ЗЕМЛЯ] Навмеш перепечён: встали постройки.");
        }
    }
}
