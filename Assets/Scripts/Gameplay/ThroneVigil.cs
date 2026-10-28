// Assets/Scripts/Gameplay/ThroneVigil.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Пустой трон павшего Греховода «Перетягивания» (автор, 28.10: «если Греховод пал — на троне
    /// лежит корона или что-то в этом роде, и сверху прогресс-бар, который видит только игрок этой
    /// команды»). Корона — золото на сиденье: видна всем, по ней ясно, что Греховода нет. Полоса над
    /// троном — сколько осталось до того, как он встанет; без чисел и только своей стороне: чужому
    /// незачем знать, когда ты вернёшься.
    /// </summary>
    public class ThroneVigil : MonoBehaviour
    {
        private GameObject _crown;
        private Transform _back, _fill;
        private float _from, _until;
        private bool _mine;

        /// <summary>Положить корону на этот трон до <paramref name="until"/>; полоса — если трон свой.</summary>
        public static ThroneVigil Begin(Sturdy throne, float until, bool mine)
        {
            if (throne == null) return null;
            if (!throne.TryGetComponent<ThroneVigil>(out var v)) v = throne.gameObject.AddComponent<ThroneVigil>();
            v._from = Time.time;
            v._until = until;
            v._mine = mine;
            v.Build();
            return v;
        }

        /// <summary>Встал — корону убрать.</summary>
        public void End()
        {
            if (_crown != null) Destroy(_crown);
            if (_back != null) Destroy(_back.gameObject);
            Destroy(this);
        }

        private void Build()
        {
            if (_crown != null) return;
            var gold = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Crown Gold" };
            gold.SetColor("_BaseColor", new Color(0.78f, 0.60f, 0.22f));
            gold.SetFloat("_Metallic", 0.9f);
            gold.SetFloat("_Smoothness", 0.7f);

            // Корона: обруч из двенадцати звеньев и шесть зубцов — на сиденье трона.
            _crown = new GameObject("Корона на троне");
            _crown.transform.SetParent(transform, true);
            _crown.transform.position = SeatTop();
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var link = Piece(_crown.transform, gold, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.12f,
                                 new Vector3(0.07f, 0.05f, 0.025f));
                link.transform.localRotation = Quaternion.Euler(0f, -i * 30f + 90f, 0f);
                if (i % 2 != 0) continue;
                var tooth = Piece(_crown.transform, gold, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.12f + Vector3.up * 0.06f,
                                  new Vector3(0.035f, 0.035f, 0.035f));
                tooth.transform.localRotation = Quaternion.Euler(45f, -i * 30f, 45f);
            }

            if (!_mine) return;
            // Полоса — над троном, лицом к камере: тёмная подложка и золотая заливка слева направо.
            _back = Bar("Полоса до восстания", new Color(0.08f, 0.07f, 0.06f, 0.85f), null);
            _back.position = transform.position + Vector3.up * 2.7f;
            _fill = Bar("Заливка", new Color(0.86f, 0.68f, 0.30f, 0.95f), _back);
        }

        private Vector3 SeatTop()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<Renderer>()) if (r.gameObject != _crown) b.Encapsulate(r.bounds);
            // Сиденье — около четверти высоты трона (спинка — выше): props.py, throne.
            return new Vector3(b.center.x, b.min.y + b.size.y * 0.24f + 0.03f, b.center.z) + transform.forward * 0.12f;
        }

        private static GameObject Piece(Transform parent, Material m, Vector3 at, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private Transform Bar(string name, Color colour, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Sprites/Default")) { color = colour };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (parent == null)
            {
                go.transform.SetParent(transform, true);
                go.transform.localScale = new Vector3(1.6f, 0.16f, 1f);
            }
            else
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                go.transform.localScale = new Vector3(0f, 0.7f, 1f);
            }
            return go.transform;
        }

        void LateUpdate()
        {
            if (_back == null) return;
            var cam = Camera.main;
            if (cam != null) _back.rotation = Quaternion.LookRotation(_back.position - cam.transform.position);
            float k = Mathf.Clamp01((Time.time - _from) / Mathf.Max(0.01f, _until - _from));
            // Заливка растёт от левого края подложки.
            _fill.localScale = new Vector3(k * 0.94f, 0.7f, 1f);
            _fill.localPosition = new Vector3(-0.47f + k * 0.47f, 0f, -0.01f);
        }
    }
}
