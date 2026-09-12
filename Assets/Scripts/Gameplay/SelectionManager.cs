using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class SelectionManager : MonoBehaviour
    {
        /// <summary>
        /// Игрок отдал приказ: какой и скольким. Приказ — не факт, а просьба,
        /// и услышать её должны не только воины: на «отходить» в доле 5
        /// трубит рог, и это единственное место, где такой момент виден.
        /// </summary>
        public static event System.Action<CommandKind, int> OnPlayerOrder;

        public static SelectionManager Instance { get; private set; }

        [SerializeField] private RectTransform _selectionBox;
        [Tooltip("По каким слоям искать юнитов. По умолчанию по всем: "
               + "отдельного слоя для воинов в проекте нет, а пустая маска "
               + "означает «ни по каким» — луч не находил никого и выделение "
               + "не работало вовсе. Что попало под луч действительно юнит, "
               + "решает SelectionComponent, а не слой.")]
        [SerializeField] private LayerMask _unitLayer = ~0;

        [SerializeField] private LayerMask _groundLayer = ~0;

        private List<SelectionComponent> _selectedUnits = new();
        private List<SelectionComponent> _allUnits = new();

        private Vector2 _selectionStart;
        private bool _isSelecting;
        private bool _complainedEmpty;
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
            // На паузе игрок разговаривает с панелью, а не с отрядом.
            //
            // Без этой строки любая открытая панель — совет, выбор тела,
            // карта вылазок — продолжала пропускать выделение и приказы
            // мимо себя: щелчок по строке панели попадал заодно и в землю
            // за ней, а Esc «назад» снимал приказы с отряда. Панель ставит
            // паузу, значит пауза и есть признак того, что руки заняты.
            if (Core.GamePauseController.Instance != null
                && Core.GamePauseController.Instance.IsPaused) return;

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
            // Оборона на G, а не на D: D одновременно ведёт камеру вправо
            // (RTS_Camera), и при выделенном отряде одно нажатие делало
            // и то и другое. WASD принадлежат камере целиком.
            else if (Input.GetKeyDown(KeyCode.G)) kind = CommandKind.Defend;
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

        /// <summary>Имя объекта рамки. Его же ставит сборщик сцен.</summary>
        private const string BoxName = "Рамка выделения";

        /// <summary>
        /// Камера, найденная заново, если прежней не стало.
        ///
        /// Та же беда, что у рамки: менеджер переживает смену сцен,
        /// а камера — нет. Ссылка бралась один раз в Awake, и со второй
        /// сцены каждый щелчок бил бы по уничтоженному объекту.
        /// </summary>
        private Camera Cam()
        {
            if (_cam == null) _cam = Camera.main;
            return _cam;
        }

        /// <summary>
        /// Смотрит ли игрок глазами героя. Тогда курсор заперт в середине
        /// экрана: тянуть рамку нечем, и щелчок означает «то, на что смотрю».
        /// </summary>
        private bool FirstPerson()
        {
            var cam = Cam();
            if (cam == null) return false;

            var view = cam.GetComponent<RTS_Camera>();
            return view != null && view.FirstPersonNow;
        }

        /// <summary>
        /// Рамка выделения, найденная заново, если прежней не стало.
        ///
        /// Менеджер переживает смену сцен (<c>DontDestroyOnLoad</c>
        /// в Awake), а рамка живёт на Canvas и умирает вместе со сценой.
        /// Ссылки, связанной в сборщике, хватало бы ровно на одну сцену:
        /// со второй рамка стала бы невидимой, и понять почему было бы
        /// нечем — выделение-то работает.
        ///
        /// Ищем через <see cref="Transform.Find"/>, а не
        /// <c>GameObject.Find</c>: рамка выключена, пока её не тянут,
        /// а выключенные объекты второй не находит.
        /// </summary>
        private RectTransform Box()
        {
            if (_selectionBox != null) return _selectionBox;

            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.InstanceID))
            {
                var found = canvas.transform.Find(BoxName) as RectTransform;
                if (found == null) continue;

                _selectionBox = found;
                return found;
            }

            return null;
        }

        /// <summary>
        /// Во сколько раз холст растянут против своего эталона.
        /// Единица, если холста нет: тогда и делить не на что.
        /// </summary>
        private static float CanvasScale(RectTransform box)
        {
            var canvas = box.GetComponentInParent<Canvas>();
            if (canvas == null) return 1f;

            return Mathf.Approximately(canvas.scaleFactor, 0f) ? 1f : canvas.scaleFactor;
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

                // В первом лице рамки нет: курсор заперт, тянуть нечем.
                // Щелчок при этом работает как был — луч из середины
                // экрана и есть прицел.
                _isSelecting = !FirstPerson();

                var box = _isSelecting ? Box() : null;
                if (box != null)
                {
                    box.gameObject.SetActive(true);
                    box.position = _selectionStart;
                    box.sizeDelta = Vector2.zero;
                }
            }

            if (_isSelecting && Input.GetMouseButton(0))
            {
                Vector2 currentPos = Input.mousePosition;
                Vector2 min = Vector2.Min(_selectionStart, currentPos);
                Vector2 max = Vector2.Max(_selectionStart, currentPos);

                var box = Box();
                if (box != null)
                {
                    // position — в экранных пикселях (холст экранный),
                    // а sizeDelta — в единицах холста. При CanvasScaler
                    // это разные вещи: без деления на масштаб рамка
                    // совпадала бы с курсором только на 1920×1080,
                    // а на любом другом экране отставала бы от него.
                    box.position = min;
                    box.sizeDelta = (max - min) / CanvasScale(box);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                _isSelecting = false;
                var box = Box();
                if (box != null)
                    box.gameObject.SetActive(false);

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
            Ray ray = Cam().ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _unitLayer))
            {
                var unit = hit.collider.GetComponentInParent<SelectionComponent>();
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
            // Shift копит выделение — и рамкой тоже, а не только щелчком.
            // Раньше рамка сбрасывала набранное всегда, и две половины
            // одного жеста вели себя по-разному: щелчком с Shift воин
            // добавлялся, рамкой с Shift — отряд начинался заново.
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                DeselectAll();

            Vector2 min = Vector2.Min(_selectionStart, Input.mousePosition);
            Vector2 max = Vector2.Max(_selectionStart, Input.mousePosition);
            Rect selectionRect = new Rect(min, max - min);

            // Убитых вычёркиваем на месте: воин мог погибнуть между
            // двумя рамками, а список у менеджера живёт дольше сцены.
            _allUnits.RemoveAll(u => u == null);

            // Пустой список — событие, а не ноль. Ровно из-за него рамка
            // не выделяла ничего и никогда: воины на учёт не вставали,
            // а перебор пустоты выглядел как «рамка кривая», а не как
            // «рамки нет». Жалуемся один раз за запуск: в каждом кадре
            // это была бы стена в консоли.
            if (_allUnits.Count == 0 && !_complainedEmpty)
            {
                _complainedEmpty = true;
                Debug.LogWarning("[ВЫДЕЛЕНИЕ] Рамка обвела пустоту: на учёте "
                               + "нет ни одного воина. Значит SelectionComponent "
                               + "не зарегистрировался — смотреть его Start.");
            }

            foreach (var unit in _allUnits)
            {
                if (unit == null) continue;

                // Греховод в рамку не попадает. Он стоит посреди отряда,
                // и «выделить всех» захватывало бы игрока вместе с ними:
                // приказ идти уводил бы его самого, отбирая управление
                // ровно в тот момент, когда игрок им пользуется.
                //
                // Щелчком по нему выделить можно — так смотрят его строку
                // в нижней панели. Разница в том, что щелчок нарочен,
                // а рамка — нет.
                if (unit.GetComponentInParent<SinbinderPlayer>() != null) continue;

                Vector3 screenPos = Cam().WorldToScreenPoint(unit.transform.position);
                if (selectionRect.Contains(screenPos))
                {
                    SelectUnit(unit);
                }
            }
        }

        private void SelectUnit(SelectionComponent unit)
        {
            // Уже выделенного не добавляем второй раз. С Shift это
            // случается легко — обвести рамкой того, по кому уже щёлкнул, —
            // и стоило бы дорого: приказ рассылается перебором списка,
            // то есть двойник получил бы его дважды, а счёт выделенных
            // показал бы больше, чем на поле.
            if (_selectedUnits.Contains(unit)) return;

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
                Ray ray = Cam().ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    // Приказ записывается на воина и уходит в голосование.
                    // Раньше он шёл прямо в NavMeshAgent, минуя AOS, и
                    // исполнялся всегда — то есть подчинения как решения
                    // не существовало, а модуль Верности был мёртвым кодом.
                    var enemyUnit = hit.collider.GetComponentInParent<SelectionComponent>();
                    bool isAttackOrder = enemyUnit != null && !_selectedUnits.Contains(enemyUnit);

                    // Shift + ПКМ по земле — «отходи», а не «иди туда».
                    // Разница не в ногах: отход звучит для характера иначе,
                    // и исполнить его воин может по-своему — побежав.
                    bool isFallBack = Input.GetKey(KeyCode.LeftShift)
                                   || Input.GetKey(KeyCode.RightShift);

                    int given = 0;

                    foreach (var unit in _selectedUnits)
                    {
                        if (unit == null) continue;
                        var warrior = unit.GetComponent<Warrior>();
                        if (warrior == null || warrior.IsDead) continue;

                        // Греховод не голосует — значит и приказ ему отдавать
                        // некуда: IssueCommand кладёт приказ в бюллетень,
                        // а бюллетень читает AOSWarriorWrapper, которого
                        // у героя нет намеренно. Приказ уходил в пустоту:
                        // выделить его было можно, сдвинуть — нет.
                        //
                        // Он и не должен голосовать: это игрок. Значит ноги
                        // слушают прямо, без спора.
                        if (warrior is SinbinderPlayer)
                        {
                            var legs = warrior.GetComponent<UnitMover>();
                            if (legs != null)
                            {
                                if (isAttackOrder) legs.CommandAttack(enemyUnit.gameObject);
                                else legs.CommandMove(hit.point);
                                given++;
                            }
                            continue;
                        }

                        if (isAttackOrder)
                            warrior.IssueCommand(CommandKind.Attack, enemyUnit.transform.position, enemyUnit.gameObject);
                        else
                            warrior.IssueCommand(isFallBack ? CommandKind.FallBack : CommandKind.Move, hit.point);

                        given++;
                    }

                    if (given > 0)
                    {
                        var kind = isAttackOrder ? CommandKind.Attack
                                 : isFallBack ? CommandKind.FallBack
                                 : CommandKind.Move;

                        OnPlayerOrder?.Invoke(kind, given);
                    }
                }
            }
        }
    }
}