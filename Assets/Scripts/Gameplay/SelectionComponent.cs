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
            if (_selectionCircle != null)
                _selectionCircle.SetActive(true);
        }

        public void Deselect()
        {
            _isSelected = false;
            if (_selectionCircle != null)
                _selectionCircle.SetActive(false);
        }
    }
}