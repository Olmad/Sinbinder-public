// Assets/Scripts/Gameplay/ExitCircle.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Круг выхода — «круг силы», как в Warcraft 3. Слово автора,
    /// 27 сентября: «хочется, чтобы были не ворота, а круг типа
    /// „контрольная точка“ как в Warcraft 3».
    ///
    /// Два кольца на земле, руны между ними и столб холодного света.
    /// Пока уходить рано, круг тлеет, а свет не горит; когда край открыт
    /// (<see cref="EscapeZone.Arm"/>), круг разгорается и дышит, руны
    /// медленно идут по кругу, над ним встаёт метка «Выход». Цвет —
    /// некроэфир, холодный: золота в палитре игры нет, кроме знамени.
    ///
    /// Сам круг ничего не решает: кто ушёл, считает <see cref="EscapeZone"/>
    /// тем же радиусом, каким считал у ворот. Коллайдеров нет: навмеш уже
    /// испечён, а луч щелчка «иди сюда» обязан падать в землю, в круг.
    ///
    /// Дыхание берёт время и потому не повторяется от запуска к запуску —
    /// как у шара совета: правило повторяемости охраняет решения, а свет
    /// на решения не влияет.
    /// </summary>
    public class ExitCircle : MonoBehaviour
    {
        private static readonly Color Ether = new Color(0.32f, 1.0f, 0.72f);

        private const float Outer = 2.6f;
        private const float Inner = 2.25f;
        private const float Faint = 1.5f;
        private const int Runes = 8;

        private EscapeZone _zone;
        private Material _glow;
        private Light _light;
        private Transform _ring;

        /// <summary>Одеть край кругом. Зовёт <see cref="RaidEvent"/> сразу после зоны.</summary>
        public static void Dress(Transform zone)
        {
            if (zone == null) return;
            zone.gameObject.AddComponent<ExitCircle>().Build();
        }

        private void Build()
        {
            _zone = GetComponent<EscapeZone>();
            _glow = Glow();

            _ring = new GameObject("Кольца").transform;
            _ring.SetParent(transform, false);
            _ring.localPosition = new Vector3(0f, 0.03f, 0f);

            Flat("Кольцо", Annulus(Inner, Outer, 64));
            Flat("Кольцо тонкое", Annulus(Faint - 0.06f, Faint, 48));

            // Руны — между кольцами, поперёк радиуса, как в образце.
            for (int i = 0; i < Runes; i++)
            {
                float a = i * 360f / Runes;
                var rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rune.name = "Руна";
                Destroy(rune.GetComponent<Collider>());
                rune.transform.SetParent(_ring, false);
                rune.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                rune.transform.localPosition = rune.transform.localRotation
                                             * new Vector3(0f, 0f, (Inner + Faint) * 0.5f);
                rune.transform.localScale = new Vector3(0.34f, 0.02f, 0.12f);
                Paint(rune);
            }

            var lamp = new GameObject("Свет круга");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            _light = lamp.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Ether;
            _light.range = 7.5f;
            _light.intensity = 2f;
            _light.shadows = LightShadows.None;
        }

        void Update()
        {
            if (_glow == null) return;

            bool open = _zone != null && _zone.Open;
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f);

            // Закрытый круг тлеет: его видно, но он не зовёт.
            float glow = open ? Mathf.Lerp(0.9f, 1.6f, breath) : 0.12f;
            _glow.SetColor("_EmissionColor", Ether * glow);

            if (_light != null) _light.intensity = Mathf.Lerp(1.6f, 2.8f, breath);
            if (open && _ring != null) _ring.Rotate(0f, 9f * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        void OnDestroy()
        {
            if (_glow != null) Destroy(_glow);
        }

        /// <summary>
        /// Свой материал, а не общий: светимость меняется каждый кадр, и общий
        /// перекрасил бы всё, что его носит. Шейдер — тот же URP Lit, что
        /// у всего мира; нет его — берём материал у примитива.
        /// </summary>
        private static Material Glow()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Material m;

            if (shader != null) m = new Material(shader);
            else
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                m = new Material(probe.GetComponent<Renderer>().sharedMaterial);
                Destroy(probe);
            }

            m.name = "Круг выхода";
            var ink = new Color(0.05f, 0.11f, 0.09f);
            m.color = ink;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", ink);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);

            // Кольцо — плоское, и сверху его видно всегда; снизу — никогда.
            // Обе стороны на случай, если его увидят с низкой камеры наезда.
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);

            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Ether * 0.12f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        private void Flat(string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_ring, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Paint(go.AddComponent<MeshRenderer>().gameObject);
        }

        private void Paint(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Плоское кольцо на земле, лицом вверх.</summary>
        private static Mesh Annulus(float inner, float outer, int segs)
        {
            var verts = new Vector3[segs * 2];
            var normals = new Vector3[segs * 2];
            var tris = new int[segs * 6];

            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

                verts[i * 2] = d * inner;
                verts[i * 2 + 1] = d * outer;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;

                int n = (i + 1) % segs;
                tris[i * 6 + 0] = i * 2;
                tris[i * 6 + 1] = n * 2;
                tris[i * 6 + 2] = i * 2 + 1;
                tris[i * 6 + 3] = n * 2;
                tris[i * 6 + 4] = n * 2 + 1;
                tris[i * 6 + 5] = i * 2 + 1;
            }

            var mesh = new Mesh { name = "Кольцо", vertices = verts, normals = normals, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
