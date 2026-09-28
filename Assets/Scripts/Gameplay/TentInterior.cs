// Assets/Scripts/Gameplay/TentInterior.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Нутро палатки: где у неё пол, где крыша и что прятать, когда внутри
    /// Греховод.
    ///
    /// Слово автора после прохода 26 сентября: «Нужно, чтобы в палатки можно
    /// было войти, осмотреться, увидеть внутренний интерьер. Палатка Греховода
    /// должна быть ещё более наполненная. И Греховод будет просыпаться
    /// в палатке». До того палатка была декорацией: навмеш пёкся под её
    /// скатами, как под потолком ниже двух метров, и внутри не оставалось
    /// ни клетки пола — войти было нельзя никому.
    ///
    /// <b>Форма — призма</b> (<c>props.py</c>, <c>tent</c>): у земли полуширина,
    /// наверху конёк, спереди открытый вход, сзади полог. Числа не заданы
    /// здесь руками, а сняты сборщиком сцены с самой модели
    /// (<c>DemoSceneBuilder.Measure</c>): модель правит другая сессия,
    /// и две правды о ширине палатки разошлись бы молча.
    ///
    /// Компонент сидит на корне палатки. Корень стоит на земле посреди
    /// неё, повёрнут входом вперёд (+Z) и не масштабирован — локальные
    /// координаты здесь в метрах.
    /// </summary>
    public class TentInterior : MonoBehaviour
    {
        [Tooltip("Полуширина у земли, м.")]
        [SerializeField] private float _half = 1.05f;

        [Tooltip("Высота конька, м.")]
        [SerializeField] private float _top = 1.9f;

        [Tooltip("Где вход: локальная Z открытой стороны.")]
        [SerializeField] private float _front = 1.2f;

        [Tooltip("Где задний полог: локальная Z.")]
        [SerializeField] private float _back = -1.2f;

        [Tooltip("Скаты и шесты: их прячут от взгляда сверху, когда внутри Греховод.")]
        [SerializeField] private Renderer[] _cover = new Renderer[0];

        private static readonly List<TentInterior> Live = new List<TentInterior>();

        private bool _hidden;
        private RTS_Camera _view;

        /// <summary>Задать форму. Зовёт сборщик сцены, сняв её с модели.</summary>
        public void Shape(float half, float top, float front, float back, Renderer[] cover)
        {
            _half = half;
            _top = top;
            _front = front;
            _back = back;
            _cover = cover ?? new Renderer[0];
        }

        public float Half => _half;
        public float Top => _top;

        void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
        }

        void OnDisable()
        {
            Live.Remove(this);
            Hide(false);
        }

        /// <summary>
        /// Под крышей ли точка: между пологом и входом, между скатами
        /// и ниже конька.
        /// </summary>
        public bool Contains(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);

            return p.z > _back && p.z < _front
                && p.y > -1f && p.y < RoofOver(p.x);
        }

        /// <summary>
        /// Высота крыши над точкой в мировых координатах. Вне палатки —
        /// бесконечность: над открытым небом пригибаться незачем.
        /// </summary>
        public float Ceiling(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);

            if (p.z <= _back || p.z >= _front || Mathf.Abs(p.x) >= _half)
                return float.PositiveInfinity;

            return transform.position.y + RoofOver(p.x);
        }

        private float RoofOver(float x) => _top * (1f - Mathf.Abs(x) / _half);

        /// <summary>Точка на земле перед входом, в <c>beyond</c> метрах от него.</summary>
        public Vector3 Doorstep(float beyond)
            => transform.TransformPoint(new Vector3(0f, 0f, _front + beyond));

        /// <summary>Палатка, под крышей которой стоит эта точка, или никакая.</summary>
        public static TentInterior Around(Vector3 world)
        {
            foreach (var tent in Live)
                if (tent != null && tent.Contains(world)) return tent;

            return null;
        }

        /// <summary>
        /// Крыша не должна прятать того, кем играют.
        ///
        /// Лагерь смотрят сверху, под 84°, и Греховод в палатке был бы виден
        /// только скатом над ним — а начинается демо ровно там. Когда он под
        /// крышей и взгляд не его собственный, скаты перестают рисоваться,
        /// но тень отбрасывают по-прежнему: внутри остаётся полумрак и свет
        /// фонаря, а не лунный пол посреди лагеря. От первого лица крыша
        /// видна — изнутри она и есть потолок.
        /// </summary>
        void LateUpdate()
        {
            bool inside = SinbinderPlayer.Exists && Contains(SinbinderPlayer.Where);
            Hide(inside && !FirstPerson());
        }

        private bool FirstPerson()
        {
            if (_view == null)
            {
                var cam = Camera.main;
                if (cam != null) _view = cam.GetComponent<RTS_Camera>();
            }

            return _view != null && _view.FirstPersonNow;
        }

        private void Hide(bool hide)
        {
            if (hide == _hidden) return;
            _hidden = hide;

            foreach (var r in _cover)
                if (r != null)
                    r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }
    }
}
