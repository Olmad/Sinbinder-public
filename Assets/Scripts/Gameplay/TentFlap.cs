// Assets/Scripts/Gameplay/TentFlap.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Полог входа в шатёр. Автор, 15 октября: «палатка закрыта тканью,
    /// при выходе Греховод должен отодвинуть ткань от первого лица».
    ///
    /// Два треугольных полотнища закрывают вход; каждое висит на петле
    /// вдоль своего ската — от конька до угла у земли. Греховод подходит
    /// к порогу — полотнища откидываются наружу, как распахнутые створки;
    /// отошёл подальше — медленно падают обратно. Изнутри, от первого лица,
    /// видно, как ткань расходится и за ней открывается лагерь.
    ///
    /// Форма — та же, что у нутра (<see cref="TentInterior"/>): снята
    /// сборщиком сцены с модели. Полотнища строятся при запуске, в сцене
    /// хранятся только числа и ткань. Модель шатра не трогается — она
    /// у сессии моделей.
    ///
    /// Сидит на корне шатра: локальные координаты — метры, вход — по +Z.
    /// </summary>
    public class TentFlap : MonoBehaviour
    {
        [SerializeField] private float _half = 1.9f;
        [SerializeField] private float _top = 3.6f;
        [SerializeField] private float _front = 2.2f;
        [SerializeField] private Material _cloth;

        /// <summary>На сколько градусов откидывается полотнище.</summary>
        private const float Open = 100f;

        /// <summary>Ближе этого к порогу — откинуть, метров по земле.</summary>
        private const float Near = 1.7f;

        /// <summary>Дальше этого — пусть падает обратно.</summary>
        private const float Far = 3.8f;

        private const float OpenSpeed = 1.0f / 0.45f;     // доля хода в секунду
        private const float CloseSpeed = 1.0f / 1.6f;

        private readonly Transform[] _hinge = new Transform[2];
        private readonly Quaternion[] _rest = new Quaternion[2];
        private readonly float[] _sign = new float[2];

        /// <summary>Сколько хода сделано: 0 — закрыт, 1 — откинут.</summary>
        private float _k;
        private bool _wantOpen;

        /// <summary>Открыт ли полог сейчас. Спрашивает прогон.</summary>
        public bool IsOpen => _k > 0.95f;

        /// <summary>Задать форму входа и ткань. Зовёт сборщик сцены.</summary>
        public void Shape(float half, float top, float front, Material cloth)
        {
            _half = half;
            _top = top;
            _front = front;
            _cloth = cloth;
        }

        void Awake()
        {
            for (int side = 0; side < 2; side++) Build(side);
            Pose();
        }

        /// <summary>
        /// Полотнище: треугольник «конёк — середина низа — угол у земли».
        /// Петля — по линии «конёк — угол»: вокруг неё полотнище и ходит.
        /// </summary>
        private void Build(int side)
        {
            float sx = side == 0 ? -1f : 1f;
            float z = _front + 0.03f;              // чуть снаружи кромки ткани шатра
            var apex = new Vector3(0f, _top, z);
            var corner = new Vector3(sx * _half, 0f, z);
            var centre = new Vector3(0f, 0f, z);

            // Ось X петли — вдоль ската; Z — наружу, по нормали входа.
            Vector3 x = (corner - apex).normalized;
            Vector3 forward = Vector3.forward;
            Vector3 up = Vector3.Cross(forward, x);
            var rest = Quaternion.LookRotation(forward, up);

            var hinge = new GameObject(side == 0 ? "Полог — левое полотнище" : "Полог — правое полотнище").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = apex;
            hinge.localRotation = rest;

            var inv = Quaternion.Inverse(rest);
            Vector3 a = Vector3.zero;
            Vector3 b = inv * (centre - apex);
            Vector3 c = inv * (corner - apex);

            // Две стороны: снаружи и изнутри (изнутри его видно от первого лица).
            var mesh = new Mesh { name = "Полог" };  // ключ
            mesh.vertices = new[] { a, b, c, a, b, c };
            mesh.uv = new[] { new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f),
                              new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var cloth = new GameObject("Ткань");
            cloth.transform.SetParent(hinge, false);
            cloth.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = cloth.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _cloth;

            _hinge[side] = hinge;
            _rest[side] = rest;

            // Куда откидывать: туда, где середина низа уходит наружу (+Z).
            float plus = (rest * (Quaternion.AngleAxis(30f, Vector3.right) * b)).z;
            float minus = (rest * (Quaternion.AngleAxis(-30f, Vector3.right) * b)).z;
            _sign[side] = plus > minus ? 1f : -1f;
        }

        void Update()
        {
            if (SinbinderPlayer.Exists)
            {
                Vector3 door = transform.TransformPoint(new Vector3(0f, 0f, _front));
                float d = CampFocus.GroundDistance(SinbinderPlayer.Where, door);
                if (d < Near) _wantOpen = true;
                else if (d > Far) _wantOpen = false;
            }

            _k = Mathf.MoveTowards(_k, _wantOpen ? 1f : 0f,
                                   (_wantOpen ? OpenSpeed : CloseSpeed) * Time.deltaTime);
            Pose();
        }

        /// <summary>
        /// Ход — плавный на входе и на выходе; открытая ткань едва
        /// дышит на ветру. Без жребия: качка — по часам.
        /// </summary>
        private void Pose()
        {
            float k = _k * _k * (3f - 2f * _k);
            float sway = Mathf.Sin(Time.time * 1.6f) * 3f * k;

            for (int side = 0; side < 2; side++)
            {
                if (_hinge[side] == null) continue;
                float angle = _sign[side] * (Open * k + sway * (side == 0 ? 1f : -1f));
                _hinge[side].localRotation = _rest[side] * Quaternion.AngleAxis(angle, Vector3.right);
            }
        }
    }
}
