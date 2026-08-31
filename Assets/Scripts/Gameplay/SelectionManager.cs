using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        [SerializeField] private RectTransform _selectionBox;
        [SerializeField] private LayerMask _unitLayer;
        [SerializeField] private LayerMask _groundLayer;

        private List<SelectionComponent> _selectedUnits = new();
        private List<SelectionComponent> _allUnits = new();

        private Vector2 _selectionStart;
        private bool _isSelecting;
        private Camera _cam;

        public System.Action<List<SelectionComponent>> OnSelectionChanged;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                _cam = Camera.main;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            HandleSelectionInput();
            HandleCommandInput();
            HandleStanceInput();
        }

        /// <summary>
        /// H — держать позицию, D — обороняться, Esc — снять приказ.
        /// Это тоже предложения, а не команды: голосование решает.
        /// </summary>
        private void HandleStanceInput()
        {
            if (_selectedUnits.Count == 0) return;

            CommandKind kind = CommandKind.None;
            bool clear = false;

            if (Input.GetKeyDown(KeyCode.H)) kind = CommandKind.Hold;
            else if (Input.GetKeyDown(KeyCode.D)) kind = CommandKind.Defend;
            else if (Input.GetKeyDown(KeyCode.Escape)) clear = true;
            else return;

            foreach (var unit in _selectedUnits)
            {
                if (unit == null) continue;
                var warrior = unit.GetComponent<Warrior>();
                if (warrior == null || warrior.IsDead) continue;

                if (clear) warrior.ClearCommand();
                else warrior.IssueCommand(kind, warrior.transform.position);
            }
        }

        public void RegisterUnit(SelectionComponent unit)
        {
            if (!_allUnits.Contains(unit))
                _allUnits.Add(unit);
        }

        public void UnregisterUnit(SelectionComponent unit)
        {
            _allUnits.Remove(unit);
            _selectedUnits.Remove(unit);
        }

        private void HandleSelectionInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _selectionStart = Input.mousePosition;
                _isSelecting = true;

                if (_selectionBox != null)
                {
                    _selectionBox.gameObject.SetActive(true);
                    _selectionBox.position = _selectionStart;
                    _selectionBox.sizeDelta = Vector2.zero;
                }
            }

            if (_isSelecting && Input.GetMouseButton(0))
            {
                Vector2 currentPos = Input.mousePosition;
                Vector2 min = Vector2.Min(_selectionStart, currentPos);
                Vector2 max = Vector2.Max(_selectionStart, currentPos);

                if (_selectionBox != null)
                {
                    _selectionBox.position = min;
                    _selectionBox.sizeDelta = max - min;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                _isSelecting = false;
                if (_selectionBox != null)
                    _selectionBox.gameObject.SetActive(false);

                float dragDistance = Vector2.Distance(_selectionStart, Input.mousePosition);

                if (dragDistance < 10f)
                {
                    HandleSingleClick();
                }
                else
                {
                    HandleBoxSelection();
                }
            }
        }

        private void HandleSingleClick()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _unitLayer))
            {
                var unit = hit.collider.GetComponent<SelectionComponent>();
                if (unit != null)
                {
                    if (!Input.GetKey(KeyCode.LeftShift))
                        DeselectAll();

                    SelectUnit(unit);
                }
                else
                {
                    DeselectAll();
                }
            }
            else
            {
                DeselectAll();
            }
        }

        private void HandleBoxSelection()
        {
            DeselectAll();

            Vector2 min = Vector2.Min(_selectionStart, Input.mousePosition);
            Vector2 max = Vector2.Max(_selectionStart, Input.mousePosition);
            Rect selectionRect = new Rect(min, max - min);

            foreach (var unit in _allUnits)
            {
                if (unit == null) continue;

                Vector3 screenPos = _cam.WorldToScreenPoint(unit.transform.position);
                if (selectionRect.Contains(screenPos))
                {
                    SelectUnit(unit);
                }
            }
        }

        private void SelectUnit(SelectionComponent unit)
        {
            unit.Select();
            _selectedUnits.Add(unit);
            OnSelectionChanged?.Invoke(_selectedUnits);
        }

        private void DeselectAll()
        {
            foreach (var unit in _selectedUnits)
            {
                if (unit != null)
                    unit.Deselect();
            }
            _selectedUnits.Clear();
            OnSelectionChanged?.Invoke(_selectedUnits);
        }

        public List<SelectionComponent> GetSelectedUnits()
        {
            _selectedUnits.RemoveAll(u => u == null);
            return _selectedUnits;
        }

        private void HandleCommandInput()
        {
            if (Input.GetMouseButtonDown(1) && _selectedUnits.Count > 0)
            {
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    // Приказ записывается на воина и уходит в голосование.
                    // Раньше он шёл прямо в NavMeshAgent, минуя AOS, и
                    // исполнялся всегда — то есть подчинения как решения
                    // не существовало, а модуль Верности был мёртвым кодом.
                    var enemyUnit = hit.collider.GetComponent<SelectionComponent>();
                    bool isAttackOrder = enemyUnit != null && !_selectedUnits.Contains(enemyUnit);

                    // Shift + ПКМ по земле — «отходи», а не «иди туда».
                    // Разница не в ногах: отход звучит для характера иначе,
                    // и исполнить его воин может по-своему — побежав.
                    bool isFallBack = Input.GetKey(KeyCode.LeftShift)
                                   || Input.GetKey(KeyCode.RightShift);

                    foreach (var unit in _selectedUnits)
                    {
                        if (unit == null) continue;
                        var warrior = unit.GetComponent<Warrior>();
                        if (warrior == null || warrior.IsDead) continue;

                        if (isAttackOrder)
                            warrior.IssueCommand(CommandKind.Attack, enemyUnit.transform.position, enemyUnit.gameObject);
                        else
                            warrior.IssueCommand(isFallBack ? CommandKind.FallBack : CommandKind.Move, hit.point);
                    }
                }
            }
        }
    }
}