// Assets/Scripts/Gameplay/TrophyChest.cs
using System.Collections;
using UnityEngine;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Сундук с трофеями Марги Копателя. Сцена 3 пролога, первая половина
    /// (docs/09-PROLOGUE.md §4).
    ///
    /// > «Давайте осмотрим, что принёс Марга Копатель. Я уверен, он это
    /// > всё просто выкопал возле лагеря».
    ///
    /// Одной фразой — характер Жадности и юмор мира. А по делу это
    /// обучение снаряжению без слова «снаряжение»: игрок подходит,
    /// сундук открывается, вещи оказываются у него. Ни окна, ни вкладки,
    /// ни подсказки «нажмите I».
    ///
    /// Подходят к нему тем же правилом, что и к столу совета
    /// (<see cref="CampFocus"/>): в лагере всё, что можно сделать,
    /// делается ногами.
    ///
    /// До сундука у игрока нет ни одной вещи, и это правильно: восьмой
    /// рычаг — вложить вещь в руку воину — должен сперва быть заработан,
    /// а не выдан на старте сцены.
    /// </summary>
    public class TrophyChest : MonoBehaviour
    {
        [Tooltip("Крышка. Открывается поворотом — одна анимация на весь "
               + "предмет, и та без аниматора.")]
        [SerializeField] private Transform _lid;

        [Tooltip("Насколько близко подвести взгляд, чтобы сундук открылся.")]
        [SerializeField] private float _reach = CampFocus.TableReach;

        [SerializeField] private float _lidAngle = -72f;
        [SerializeField] private float _lidSeconds = 0.9f;

        [Tooltip("Что говорит Карган, ведя к сундуку.")]
        [SerializeField] private string _invite =
            "Карган: «Давайте осмотрим, что принёс Марга Копатель. "
          + "Я уверен, он это всё просто выкопал возле лагеря».";

        /// <summary>
        /// Разобрали ли трофеи. Статично: сцена 3 идёт дальше только после
        /// этого, а сцену могли и перезапустить.
        /// </summary>
        public static bool Looted { get; private set; }

        /// <summary>Забыть трофеи. Начало пролога.</summary>
        public static void Forget() => Looted = false;

        private Transform _eye;
        private bool _invited;
        private bool _opening;

        void Awake()
        {
            Looted = false;
            if (_lid == null) _lid = transform.Find("Крышка");
        }

        void Start()
        {
            var cam = Camera.main;
            if (cam != null) _eye = cam.transform;
        }

        void Update()
        {
            if (Looted || _opening || _eye == null) return;

            // Сундук ждёт своей очереди: пока старший не назначен, идёт
            // сцена 2, и открывать его рано.
            if (string.IsNullOrEmpty(SquadRoster.CommanderName)) return;

            if (!_invited)
            {
                _invited = true;
                if (!string.IsNullOrEmpty(_invite))
                    Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(_invite);
            }

            if (!CampFocus.Reached(_eye.position, _eye.forward,
                                   transform.position, _reach)) return;

            Open();
        }

        /// <summary>Открыть и раздать. Публично: сцену может вести и не игрок.</summary>
        public void Open()
        {
            if (Looted || _opening) return;

            _opening = true;
            StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();

            if (_lid != null)
            {
                var from = _lid.localRotation;
                var to = from * Quaternion.Euler(_lidAngle, 0f, 0f);

                float t = 0f;
                while (t < _lidSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    _lid.localRotation = Quaternion.Slerp(from, to,
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _lidSeconds)));
                    yield return null;
                }

                _lid.localRotation = to;
            }

            int taken = Fill(log);

            Looted = true;
            _opening = false;

            if (taken == 0)
                log?.Write("В сундуке пусто. Марга объяснится, когда вернётся.");
        }

        /// <summary>
        /// Переложить трофеи игроку. Каждую вещь называем вслух — это
        /// и есть обучение: игрок узнаёт, что вещи бывают, из того, что
        /// они у него появились.
        /// </summary>
        private int Fill(UI.BattleLogUI log)
        {
            var purse = PlayerInventory.Instance;

            if (purse == null)
            {
                // Молча потерять трофеи нельзя: сцена прошла бы «успешно»,
                // а рычаг остался бы пустым, и никто бы не понял почему.
                Debug.LogWarning("[СУНДУК] PlayerInventory в сцене нет — "
                               + "трофеи забирать некому.");
                log?.Write("Забрать это некуда.");
                return 0;
            }

            int taken = 0;

            foreach (var item in TemptationCatalog.Demo())
            {
                if (!purse.AddItem(item)) break;

                taken++;
                log?.Write($"{item.Name}. {item.Description}");
            }

            return taken;
        }
    }
}
