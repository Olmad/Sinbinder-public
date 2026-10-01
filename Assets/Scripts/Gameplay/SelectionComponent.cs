// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class SelectionComponent : MonoBehaviour
    {
        [SerializeField] private GameObject _selectionCircle;

        private Warrior _warrior;
        private bool _isSelected;

        public Warrior Warrior => _warrior;
        public bool IsSelected => _isSelected;

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
            if (_selectionCircle != null)
                _selectionCircle.SetActive(false);
        }

        /// <summary>
        /// Встать на учёт у менеджера выделения.
        ///
        /// Без этого рамкой не выделялось <b>ничего</b>, и не с какой-то
        /// сцены, а никогда: HandleBoxSelection перебирает список
        /// зарегистрированных, а список заполнял один UnitFactory, которым
        /// пролог не пользуется. Щелчок при этом работал — он идёт лучом,
        /// а не по списку, — и потому поломка выглядела как «рамка кривая»,
        /// а не как «рамки нет».
        ///
        /// В Start, а не в Awake: менеджер ставит себе Instance в своём
        /// Awake, и порядок Awake между объектами Unity не определяет.
        /// </summary>
        void Start()
        {
            // Кольцо отношения — у всех и всегда (docs/42-INTERFACE.md §4):
            // в покое тусклое, у выбранного — в полную силу.
            if (_selectionCircle == null) _selectionCircle = BuildCircle();
            Paint();

            if (SelectionManager.Instance != null)
            {
                SelectionManager.Instance.RegisterUnit(this);
                return;
            }

            Debug.LogWarning($"[ВЫДЕЛЕНИЕ] {name} не встал на учёт: "
                           + "SelectionManager в сцене нет. Рамкой его "
                           + "не выделить.");
        }

        void OnDestroy()
        {
            // Менеджер переживает смену сцен, а воины — нет: не сняться
            // с учёта значит копить в списке мёртвые ссылки от всех
            // прошлых сцен.
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.UnregisterUnit(this);
        }

        public void Select()
        {
            _isSelected = true;

            // Круга, связанного в сцене, у воинов пролога нет: их собирают
            // кодом, и поле оставалось пустым — выделенный ничем не отличался
            // от прочих, кроме строки в нижней панели. Автор: «сделай зелёный
            // круг у выбранных воинов, у врага — красный. Ну, по классике».
            if (_selectionCircle == null) _selectionCircle = BuildCircle();
            Paint();
        }

        /// <summary>
        /// Кольцо видно, пока воин жив и не спрятан туманом: туман выключает
        /// отрисовку врага на смене видимости (<see cref="FogOfWar"/>), а кольцо,
        /// построенное позже, об этом не знало бы — и выдавало бы охотника
        /// в темноте. Конец страху набега (42-INTERFACE §5).
        /// </summary>
        void Update()
        {
            if (_selectionCircle == null) return;

            bool show = _warrior != null && !_warrior.IsDead && !FogOfWar.Hides(_warrior);
            if (_selectionCircle.activeSelf != show) _selectionCircle.SetActive(show);

            // Подкуп меняет сторону — кольцо обязано сказать правду о нынешней.
            if (show && _warrior.Team != _paintedTeam) Paint();
        }

        // Ярче и чище болотной Зависти и тёмно-красного Гнева (Core/SinPalette):
        // кольцо — сторона, а не грех, и путать их нельзя (42-INTERFACE §4).
        private static readonly Color Ours = new Color(0.35f, 0.92f, 0.38f);
        private static readonly Color Theirs = new Color(0.95f, 0.24f, 0.18f);

        /// <summary>В покое — тускло: десять ярких красных колец в бою перекричали бы отказы.</summary>
        private const float RestAlpha = 0.30f;

        /// <summary>Зубцы противника — для дальтонизма: красный с зелёным путают чаще всего.</summary>
        private const float Teeth = 1.14f;

        private Team _paintedTeam;

        private const float CircleRadius = 0.75f;
        private const int CircleSegments = 48;

        private static Material _circleMaterial;
        private static bool _toldAboutCircle;

        /// <summary>
        /// Свой — ровное зелёное кольцо, противник — красное с зубцами
        /// (решение автора 1 октября, 42-INTERFACE §4). У выбранного — в полную
        /// силу и толще, в покое — тускло. Нейтральных (пунктир цвета кости)
        /// в игре пока нет: <see cref="Team"/> знает только две стороны.
        /// </summary>
        private void Paint()
        {
            if (_selectionCircle == null) return;

            if (_warrior == null) _warrior = GetComponent<Warrior>();
            bool enemy = _warrior != null && _warrior.Team == Team.Enemy;
            _paintedTeam = enemy ? Team.Enemy : Team.Player;

            var color = enemy ? Theirs : Ours;
            if (!_isSelected) color.a = RestAlpha;

            var line = _selectionCircle.GetComponent<LineRenderer>();
            if (line == null) return;

            line.startColor = line.endColor = color;
            line.widthMultiplier = _isSelected ? 0.07f : 0.045f;

            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i / (float)CircleSegments * Mathf.PI * 2f;
                float r = CircleRadius * (enemy && i % 2 == 1 ? Teeth : 1f);
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
        }

        /// <summary>
        /// Кольцо у ног: ломаная из сорока восьми отрезков, лежащая плашмя.
        /// Шейдер Sprites/Default стоит в списке всегда включаемых в сборку,
        /// поэтому в собранной игре кольцо не пропадёт; не нашёлся — говорим
        /// один раз и выделяем без круга.
        /// </summary>
        private GameObject BuildCircle()
        {
            if (_circleMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    if (!_toldAboutCircle)
                    {
                        _toldAboutCircle = true;
                        Debug.LogWarning("[ВЫДЕЛЕНИЕ] Шейдера Sprites/Default нет: "
                                       + "круг под выбранными рисовать нечем.");
                    }
                    return null;
                }
                _circleMaterial = new Material(shader);
            }

            var go = new GameObject("Круг выбора");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            // Ломаная рисуется в плоскости XY и ширину откладывает поперёк
            // оси Z. Повернув кольцо на четверть оборота, кладём его на землю.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _circleMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.alignment = LineAlignment.TransformZ;
            line.widthMultiplier = 0.07f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = CircleSegments;

            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i / (float)CircleSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * CircleRadius,
                                                Mathf.Sin(a) * CircleRadius, 0f));
            }

            go.SetActive(false);
            return go;
        }

        public void Deselect()
        {
            _isSelected = false;
            Paint();    // кольцо остаётся — тусклым
        }
    }
}