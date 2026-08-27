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