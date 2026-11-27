// Assets/Scripts/Gameplay/CartRoll.cs
// Отпущенный обоз уезжает (27.11, обоз руками): журнал говорил «Обоз ушёл по дороге», а повозки
// стояли на месте. Теперь повозка катится туда же, куда уходит охрана, и пропадает за краем видимого.
// Тело повозки было стеной навмеша — по пути она его отпускает, а у цели карта пересобирает проходы,
// чтобы на месте уехавшей повозки не осталось невидимой преграды.
using Unity.AI.Navigation;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public sealed class CartRoll : MonoBehaviour
    {
        private const float Speed = 2.2f, Longest = 22f;
        private Vector3 _to;
        private float _left;

        /// <summary>Повозка трогается к точке <paramref name="to"/> (мировые координаты).</summary>
        public static void Send(GameObject cart, Vector3 to)
        {
            if (cart == null) return;
            foreach (var c in cart.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var roll = cart.AddComponent<CartRoll>();
            roll._to = to;
            roll._left = Longest;
        }

        private void Update()
        {
            _left -= Time.deltaTime;
            var centre = Centre();
            var way = _to - centre; way.y = 0f;
            if (way.magnitude < 0.5f || _left <= 0f) { Arrive(); return; }
            var step = Vector3.ClampMagnitude(way, Speed * Time.deltaTime);
            transform.position += step;
        }

        /// <summary>Середина повозки: сама она стоит в начале карты, а кузов — на своём месте.</summary>
        private Vector3 Centre()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return transform.position;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.center;
        }

        private void Arrive()
        {
            enabled = false;
            var surface = GetComponentInParent<NavMeshSurface>();
            gameObject.SetActive(false);
            if (surface != null && surface.navMeshData != null) surface.UpdateNavMesh(surface.navMeshData);
            Destroy(gameObject);
        }
    }
}
