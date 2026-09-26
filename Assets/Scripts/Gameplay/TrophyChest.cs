// Assets/Scripts/Gameplay/TrophyChest.cs
using System.Collections;
using System.Collections.Generic;
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
    /// В сундуке простое снаряжение и ничего больше. Вещи-искусители —
    /// рычаг полной версии (docs/09-PROLOGUE.md §7), и в демо их нет
    /// намеренно: у всего, что лежит здесь, искушение равно нулю.
    ///
    /// <b>Сундук — склад</b> (docs/34-GEAR.md §9.4, решение автора
    /// 24 сентября): открытый сундук не высыпается в мешок Греховода,
    /// а стоит в лагере с вещами. Греховод берёт, сколько унесёт; что
    /// осталось — осталось в лагере и при бегстве достаётся охотникам.
    /// Выключатель: до прогона — прежнее «всё сразу в мешок»; «склад»
    /// в консоли (~).
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
        public static void Forget()
        {
            Looted = false;
            _restoreLooted = null;
            _restoreContents = null;
        }

        // Сундук из записи, если сцена с ним ещё впереди (загрузка в лагерь
        // из другой сцены или другой доли). Берёт его Awake, и только при
        // приходе по записи (SaveSystem.Arriving) — не при новой игре.
        private static bool? _restoreLooted;
        private static List<InventoryItem> _restoreContents;

        /// <summary>
        /// Вернуть сундук из записи. Сундук в сцене — сразу; сцена будет
        /// перезагружена — при её приходе. Без этого загрузка в лагерь
        /// открывала сундук заново, и его монеты ложились в кошель дважды.
        /// </summary>
        public static void Restore(bool looted, List<InventoryItem> contents)
        {
            _restoreLooted = looted;
            _restoreContents = contents != null ? new List<InventoryItem>(contents) : null;

            var chest = Object.FindFirstObjectByType<TrophyChest>();
            if (chest == null) return;
            Looted = looted;
            chest._contents.Clear();
            if (contents != null) chest._contents.AddRange(contents);
        }

        /// <summary>Что лежит в сундуке сцены — для записи. Сундука нет — пусто.</summary>
        public static List<InventoryItem> Remaining()
        {
            var chest = Object.FindFirstObjectByType<TrophyChest>();
            return chest != null ? new List<InventoryItem>(chest._contents) : new List<InventoryItem>();
        }

        /// <summary>Сундук — склад, а не раздача. Выключено — всё сразу в мешок, как прежде.</summary>
        public static bool Store { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            Store = false;
            _restoreLooted = null;
            _restoreContents = null;
        }

        private readonly List<InventoryItem> _contents = new();

        /// <summary>Что лежит в сундуке сейчас.</summary>
        public IReadOnlyList<InventoryItem> Contents => _contents;

        private Transform _eye;
        private bool _invited;
        private bool _opening;

        void Awake()
        {
            // Приход по записи — сундук такой, каким его записали; иначе новый.
            if (Core.SaveSystem.Arriving && _restoreLooted.HasValue)
            {
                Looted = _restoreLooted.Value;
                if (_restoreContents != null) _contents.AddRange(_restoreContents);
            }
            else Looted = false;
            _restoreLooted = null;
            _restoreContents = null;

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
                    Herald.Line(_invite);
            }

            // Тем же правилом, что и стол совета: есть тело — открывает
            // тело. «Подойти к сундуку», не сходя с места, — не подход.
            if (SinbinderPlayer.Exists)
            {
                if (CampFocus.GroundDistance(SinbinderPlayer.Where,
                                             transform.position) > _reach) return;
            }
            else if (!CampFocus.Reached(_eye.position, _eye.forward,
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

            int taken = Store ? Stock(log) : Fill(log);

            Looted = true;
            _opening = false;

            // «Мародёр» стоит на FindTreasure, и писать его было некому.
            // Сундук разбирает игрок, но деяние — у того, кто рядом:
            // добычу делят на отряд, и заслуга тоже общая не бывает.
            var nearest = NearestOwn(transform.position);
            if (nearest != null)
            {
                nearest.Reputation.Deeds.Add(new AOS.DeedRecord
                    { Type = AOS.DeedType.FindTreasure, Importance = 1.2f });
                AOS.TitleManager.UpdateTitle(nearest);
            }

            // Экран сундука — когда мир снова идёт: за клад тут же даётся
            // «Мародёр», и церемония титула ставит свою паузу. Панель поверх
            // неё сняла бы паузу церемонии своим закрытием.
            if (Store && _contents.Count > 0) StartCoroutine(OfferWhenFree());

            if (taken == 0)
                log?.Write("В сундуке пусто. Марга объяснится, когда вернётся.");
        }

        /// <summary>
        /// Сундук — склад: вещи остаются в нём, игрок сам решает, что взять.
        /// Называем их так же, одной строкой, и открываем экран сундука.
        /// </summary>
        private int Stock(UI.BattleLogUI log)
        {
            if (_contents.Count == 0)
                foreach (var item in TrophyCatalog.Chest()) _contents.Add(item);

            var names = new System.Text.StringBuilder();
            foreach (var item in _contents)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append(item.Name.ToLowerInvariant());
            }

            if (_contents.Count > 0)
                log?.Write($"В сундуке: {names}. Что не унесёте, останется в лагере.");

            return _contents.Count;
        }

        private IEnumerator OfferWhenFree()
        {
            // Два кадра: церемония титула встаёт не в тот же кадр, что деяние.
            yield return null;
            yield return null;

            var pause = Core.GamePauseController.Instance;
            while (pause != null && pause.IsPaused) yield return null;

            UI.GearPanel.OpenChest(this);
        }

        /// <summary>Взять из сундука в мешок Греховода. Золото — в кошель.</summary>
        public bool Take(InventoryItem item, PlayerInventory bag, out string word)
        {
            if (item == null || !_contents.Contains(item)) { word = "этого в сундуке уже нет"; return false; }
            if (bag == null || !bag.AddItem(item)) { word = "в мешке нет места"; return false; }

            _contents.Remove(item);
            word = item.Type == ItemType.Gold ? "в кошель" : "в мешок";
            return true;
        }

        /// <summary>Положить из мешка в сундук. Оставить можно всё, кроме золота.</summary>
        public bool Put(InventoryItem item, PlayerInventory bag, out string word)
        {
            if (item == null || bag == null || !bag.RemoveItem(item.Id)) { word = "этого в мешке уже нет"; return false; }

            _contents.Add(item);
            word = "в сундук";
            return true;
        }

        /// <summary>
        /// Сундук, до которого Греховод дотягивается сейчас: открытый,
        /// склад, рядом. Нет такого — null.
        /// </summary>
        public static TrophyChest Reachable()
        {
            if (!Store || !Looted) return null;

            var chest = Object.FindFirstObjectByType<TrophyChest>();
            if (chest == null) return null;
            if (SinbinderPlayer.Exists
                && CampFocus.GroundDistance(SinbinderPlayer.Where, chest.transform.position) > chest._reach)
                return null;
            return chest;
        }

        /// <summary>
        /// Отряд ушёл с поля — лагерь брошен, и сундук достался охотникам
        /// (решение автора). Называем, что в нём осталось: потеря, о которой
        /// не сказали, для игрока не случилась.
        /// </summary>
        public static void Abandon()
        {
            if (!Store) return;

            var chest = Object.FindFirstObjectByType<TrophyChest>();
            if (chest == null || chest._contents.Count == 0) return;

            var names = new System.Text.StringBuilder();
            foreach (var item in chest._contents)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append(item.Name.ToLowerInvariant());
            }
            chest._contents.Clear();

            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                $"Сундук Марги остался охотникам: {names}.");
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
            var names = new System.Text.StringBuilder();

            foreach (var item in TrophyCatalog.Chest())
            {
                if (!purse.AddItem(item)) break;

                if (taken > 0) names.Append(", ");
                names.Append(item.Name.ToLowerInvariant());
                taken++;
            }

            // Одной строкой, а не по строке на вещь: четыре подряд —
            // это шесть секунд чтения посреди сцены, где игрок только
            // что научился ходить.
            if (taken > 0) log?.Write($"Из сундука — в мешок Греховода: {names}.");

            return taken;
        }

        /// <summary>
        /// Ближайший свой воин. Заслугу за сундук получает он: добычу
        /// делят на отряд, но деяние общим не бывает.
        ///
        /// Греховод не в счёт. Сундук открывается, когда к нему подходит
        /// он сам, — то есть ближе всех к сундуку всегда Греховод, и до
        /// 24 сентября заслуга доставалась ему, а титулов он не носит.
        /// </summary>
        private static Warrior NearestOwn(Vector3 where)
        {
            Warrior best = null;
            float least = float.MaxValue;

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;
                if (w is SinbinderPlayer) continue;

                float d = Vector3.Distance(w.transform.position, where);
                if (d < least) { least = d; best = w; }
            }

            return best;
        }
    }
}
