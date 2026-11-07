// Assets/Scripts/UI/TaskList.cs
// Перевод: текст через Loc
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Задачи справа сверху. Автор, 21 октября, п. 2: «не хватает каких-нибудь
    /// "Задачи" справа сверху, и чтобы игрок их делал и они зачёркивались —
    /// чтоб игрок понимал, что делать».
    ///
    /// Задача видна, когда открылась (предыдущая сделана или игра к ней
    /// подвела), и зачёркивается сама — по состоянию игры, теми же
    /// признаками, что проверяет прогон демо (DemoWalkthrough). Своих флагов
    /// здесь нет: зачёркнуто ровно то, что в игре случилось.
    ///
    /// Только лагерь (и набег в нём). Прячется, пока говорит сцена: заставка,
    /// открытие, наезд, окна.
    ///
    /// Задача, раз открывшись, не закрывается, а зачёркнутая не
    /// раззачёркивается: признаки мгновенные — «охотников нет» снова ложь,
    /// когда выходит вторая волна, и строки мигали бы.
    ///
    /// Необязательная (долг Марге) так и подписана: выбор — не обязанность;
    /// не сделана к набегу — уходит, а не висит упрёком.
    ///
    /// Слова — черновик работника D, сверены облачной 23.10 (`14-HANDOFF.md` §135.50).
    ///
    /// У задачи с местом — метка на экране (<see cref="PlaceMarker"/>; автор, 06.11:
    /// «если есть конкретное место или примерное место — показывать стрелкой как
    /// выход из лагеря»): над местом — его имя, вне кадра — у края со стрелкой.
    /// Показывает одна метка — место первой несделанной задачи; необязательная
    /// уступает обязательной. Последней задаче своя не нужна: круг на востоке
    /// показывает метка выхода.
    /// </summary>
    public class TaskList : MonoBehaviour
    {
        private sealed class Task
        {
            public string Text;
            public Func<bool> Open;
            public Func<bool> Done;
            public Func<bool> Gone;     // необязательная: окно прошло — не показывать
            public string Word;         // имя места на метке
            public Func<Vector3?> Where; // точка над местом; null — места сейчас нет
            public float Near;          // ближе — метка гаснет: пришёл
            public bool Opened, Finished;
            public Text Label;
            public Image Strike;

            /// <summary>Место задачи — для метки на экране.</summary>
            public Task At(string word, Func<Vector3?> where, float near)
            {
                Word = word;
                Where = where;
                Near = near;
                return this;
            }
        }

        private static TaskList _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null || Application.isBatchMode && !Application.isEditor) return;
            var go = new GameObject("Задачи");
            _instance = go.AddComponent<TaskList>();
            DontDestroyOnLoad(go);
        }

        private const float Width = 430f;
        private const float Row = 34f;

        private Canvas _canvas;
        private RectTransform _plate;
        private CanvasGroup _group;
        private readonly List<Task> _tasks = new();
        private bool _enemiesSeen;
        private bool _outOfTent;
        private float _nextLook;

        private PlaceMarker _marker;
        private Transform _weapon, _ball, _chest;
        private TentInterior _tent;
        private Warrior _debtor, _hunter;

        /// <summary>Что показывает метка задач — для прогона; пусто, если не видна.</summary>
        public static string Marked => _instance != null && _instance._marker != null ? _instance._marker.Shown : "";

        /// <summary>На что нацелена метка задач (видна или нет) — для прогона.</summary>
        public static string Aimed => _instance != null && _instance._marker != null ? _instance._marker.Word : "";

        void Start()
        {
            Build();
            _marker = PlaceMarker.On(gameObject, new Color(0.96f, 0.86f, 0.60f));
            Define();
            SceneManager.sceneLoaded += Forget;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= Forget;

        /// <summary>
        /// Сцена загружена заново — новая игра или запись: защёлки прочь, задачи
        /// откроются и зачеркнутся по состоянию игры заново. Иначе после «новой
        /// игры» список стоял бы зачёркнутым с прошлого раза.
        /// </summary>
        private void Forget(Scene scene, LoadSceneMode mode)
        {
            foreach (var t in _tasks) { t.Opened = false; t.Finished = false; }
            _enemiesSeen = false;
            _outOfTent = false;
            _weapon = _ball = _chest = null;
            _tent = null;
            _debtor = _hunter = null;
            if (_marker != null) _marker.Clear();
        }

        private void Define()
        {
            // Одна цепочка: каждая следующая открывается, когда сделана предыдущая.
            Add(Loc.T("Возьмите меч или катану у входа (F)"),
                () => true,
                () => !string.IsNullOrEmpty(WeaponPickup.Taken))
                .At(Loc.T("Оружие"), () => Over(_weapon, 1.3f), 1.8f);
            Add(Loc.T("Выйдите из шатра"),
                () => true,
                () => _outOfTent || (_outOfTent = OutOfTent()))
                .At(Loc.T("Выход из шатра"), () => _tent != null ? _tent.Doorstep(0.6f) + Vector3.up * 1.7f : (Vector3?)null, 0.8f);
            // «Идите к костру за провожатым» убрана: провожатый подходит сам,
            // идти за ним не нужно, — задача обещала дело, которого нет.
            Add(Loc.T("Пошлите отряд к заставе — совет у стола с шаром"),
                () => CampOpening.EscortArrived,
                () => !string.IsNullOrEmpty(SquadRoster.CommanderName))
                .At(Loc.T("Стол с шаром"), () => Over(_ball, 0.8f), CampFocus.TableReach);
            Add(Loc.T("Откройте сундук Марги (F)"),
                () => !string.IsNullOrEmpty(SquadRoster.CommanderName),
                () => TrophyChest.Looted)
                .At(Loc.T("Сундук Марги"), () => Over(_chest, 1.1f), CampFocus.TableReach);
            // Отдать или бежать к шару — оба пути честные (09-PROLOGUE, сцена 3).
            Add(Loc.T("Отдайте Марге долг из рук в руки (необязательно)"),
                () => TrophyChest.Looted && CampOpening.DebtWarned,
                () => { var w = CampOpening.Debtor(); return w == null || w.UnpaidMissions == 0; },
                gone: () => RaidEvent.Running)
                .At(Loc.Name("Марга"), () => _debtor != null && !_debtor.IsDead ? Over(_debtor.transform, 2.5f) : null, 2.5f);
            Add(Loc.T("Взгляните в горящий шар"),
                () => CrystalBall.Raised,
                () => RaidEvent.Running)
                .At(Loc.T("Шар"), () => Over(_ball, 0.8f), CampFocus.TableReach);
            // Охотники — место примерное: ближний из них. Вблизи метка не нужна — их видно.
            Add(Loc.T("Отбейте охотников"),
                () => RaidEvent.Running,
                () => _enemiesSeen && Enemies() == 0)
                .At(Loc.T("Охотники"), () => _hunter != null && !_hunter.IsDead ? Over(_hunter.transform, 2.4f) : null, 9f);
            Add(Loc.T("Уведите отряд к кругу на востоке — по дороге факелов"),
                () => RaidEvent.Running && _enemiesSeen && Enemies() == 0,
                () => false);
        }

        private Task Add(string text, Func<bool> open, Func<bool> done, Func<bool> gone = null)
        {
            var t = new Task { Text = text, Open = open, Done = done, Gone = gone };

            var go = new GameObject("Задача", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_plate, false);
            t.Label = go.GetComponent<Text>();
            t.Label.font = UiStyle.Body;
            t.Label.fontSize = 21;
            t.Label.text = "·  " + text;
            t.Label.alignment = TextAnchor.MiddleLeft;
            t.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.Label.verticalOverflow = VerticalWrapMode.Overflow;
            t.Label.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(Width - 36f, Row);

            var line = new GameObject("Черта", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(go.transform, false);
            t.Strike = line.GetComponent<Image>();
            t.Strike.color = new Color(0.80f, 0.74f, 0.62f, 0.85f);
            t.Strike.raycastTarget = false;
            var lr = (RectTransform)line.transform;
            lr.anchorMin = lr.anchorMax = new Vector2(0f, 0.5f);
            lr.pivot = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(0f, 1f);
            line.SetActive(false);

            go.SetActive(false);
            _tasks.Add(t);
            return t;
        }

        /// <summary>Точка над вещью — где встаёт её имя на метке.</summary>
        private static Vector3? Over(Transform thing, float up)
            => thing != null ? thing.position + Vector3.up * up : (Vector3?)null;

        /// <summary>
        /// Найти места задач: неподвижные — раз за сцену, оружие, должника и ближнего
        /// охотника — каждый осмотр (оружие берут, должник отдаёт долг, охотники гибнут).
        /// </summary>
        private void Seek()
        {
            if (_tent == null)
            {
                var tent = GameObject.Find("Палатка Греховода");
                _tent = tent != null ? tent.GetComponent<TentInterior>() : null;
            }
            if (_ball == null) { var b = FindFirstObjectByType<CrystalBall>(); _ball = b != null ? b.transform : null; }
            if (_chest == null) { var c = FindFirstObjectByType<TrophyChest>(); _chest = c != null ? c.transform : null; }

            var me = SinbinderPlayer.Exists ? SinbinderPlayer.Where : Vector3.zero;
            _weapon = null;
            float best = float.MaxValue;
            if (string.IsNullOrEmpty(WeaponPickup.Taken))
                foreach (var p in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                {
                    if (p == null) continue;
                    float d = CampFocus.GroundDistance(me, p.transform.position);
                    if (d < best) { best = d; _weapon = p.transform; }
                }

            _debtor = TrophyChest.Looted && !RaidEvent.Running ? CampOpening.Debtor() : null;

            _hunter = null;
            best = float.MaxValue;
            if (RaidEvent.Running)
                foreach (var w in FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                {
                    if (w == null || w.IsDead || w.Team != Team.Enemy) continue;
                    float d = CampFocus.GroundDistance(me, w.transform.position);
                    if (d < best) { best = d; _hunter = w; }
                }
        }

        private void Build()
        {
            var cgo = new GameObject("Холст задач", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            _canvas = cgo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 15;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiStyle.Reference;
            scaler.matchWidthOrHeight = 0.5f;
            _group = cgo.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var plate = new GameObject("Табличка задач", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(cgo.transform, false);
            UiStyle.Dress(plate.GetComponent<Image>(), UiStyle.PlateWeight.Board);
            plate.GetComponent<Image>().raycastTarget = false;
            _plate = (RectTransform)plate.transform;
            _plate.anchorMin = _plate.anchorMax = new Vector2(1f, 1f);
            _plate.pivot = new Vector2(1f, 1f);
            _plate.anchoredPosition = new Vector2(-28f, -28f);

            var head = new GameObject("Заголовок", typeof(RectTransform), typeof(Text));
            head.transform.SetParent(_plate, false);
            var title = head.GetComponent<Text>();
            title.font = UiStyle.Title;
            title.fontSize = 26;
            title.text = Loc.T("Задачи");
            title.color = new Color(0.86f, 0.74f, 0.50f);
            title.alignment = TextAnchor.MiddleLeft;
            title.raycastTarget = false;
            var hr = (RectTransform)head.transform;
            hr.anchorMin = hr.anchorMax = new Vector2(0f, 1f);
            hr.pivot = new Vector2(0f, 1f);
            hr.anchoredPosition = new Vector2(20f, -10f);
            hr.sizeDelta = new Vector2(Width - 40f, 36f);
        }

        void Update()
        {
            bool camp = FindFirstObjectByType<CampOpening>() != null;
            bool quiet = !camp || PrologueTitleUI.Showing || OpeningReveal.Busy || Modal.Any
                      || (Letterbox.Instance != null && Letterbox.Instance.Covering);
            float want = quiet ? 0f : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, want, Time.unscaledDeltaTime * 3f);
            if (_marker != null) _marker.Hidden = quiet;
            if (!camp) return;

            // Признаки дешёвые, но ищут объекты — четыре раза в секунду хватит.
            if (Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + 0.25f;

            if (RaidEvent.Running && Enemies() > 0) _enemiesSeen = true;
            Seek();

            float y = -52f;
            int shown = 0;
            float widest = 0f;
            foreach (var t in _tasks)
            {
                t.Opened |= t.Open();
                bool open = t.Opened;
                t.Finished |= open && t.Done();
                bool done = t.Finished;
                if (open && !done && t.Gone != null && t.Gone()) open = false;
                t.Label.gameObject.SetActive(open);
                if (!open) continue;

                shown++;
                var rt = (RectTransform)t.Label.transform;
                rt.anchoredPosition = new Vector2(20f, y - Row * 0.5f);
                y -= Row;

                t.Label.color = done ? new Color(0.62f, 0.58f, 0.52f, 0.75f) : new Color(0.92f, 0.88f, 0.80f);
                t.Strike.gameObject.SetActive(done);
                float w = t.Label.preferredWidth;
                widest = Mathf.Max(widest, w);
                if (done) ((RectTransform)t.Strike.transform).sizeDelta = new Vector2(w, 2f);
            }

            // Метка — у первой несделанной задачи с местом; необязательная уступает.
            Task aim = null;
            foreach (var t in _tasks)
            {
                if (t.Where == null || t.Finished || !t.Label.gameObject.activeSelf) continue;
                bool optional = t.Gone != null;
                if (!optional) { aim = t; break; }
                if (aim == null) aim = t;
            }
            if (aim != null) _marker.Aim(aim.Word, aim.Where, aim.Near);
            else _marker.Clear();

            // Длинная строка не вылезает за табличку: табличка — по самой длинной.
            _plate.sizeDelta = new Vector2(Mathf.Max(Width, widest + 48f), -y + 14f);
            _plate.gameObject.SetActive(shown > 0);
        }

        private static bool OutOfTent()
        {
            if (!SinbinderPlayer.Exists) return false;
            var tent = GameObject.Find("Палатка Греховода");
            var inside = tent != null ? tent.GetComponent<TentInterior>() : null;
            return inside == null || !inside.Contains(SinbinderPlayer.Where + Vector3.up * 0.1f);
        }

        private static int Enemies()
        {
            int n = 0;
            foreach (var w in FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !w.IsDead && w.Team == Team.Enemy) n++;
            return n;
        }
    }
}
