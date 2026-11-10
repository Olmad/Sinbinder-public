// Assets/Scripts/Gameplay/TugTutorial.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Обучение «Перетягиванию» (автор, 28.10: «новому игроку нужно объяснять все правила… отдельный
    /// одиночный обучающий уровень с плашками, уроками»). Девять уроков по одному правилу: плашка внизу —
    /// что сделать и почему; сделал — следующий. Правило показывают руками игрока, а не перечисляют.
    /// ИИ ждёт до урока о чужом троне (<see cref="RivalLord.Hold"/>), охотники — до своего урока
    /// (<see cref="HunterWaves.Paused"/>).
    /// </summary>
    public class TugTutorial : MonoBehaviour
    {
        private static readonly (string title, string text)[] Lessons =
        {
            (Loc.N("Склеп и трон"),
             Loc.N("Ты — Греховод. Это твой склеп, у задней стены — трон: падёт трон — партия проиграна. Сам ты не умираешь насовсем — встанешь у трона через минуту. Посмотри на дверь и нажми F.")),
            (Loc.N("Ничьи воины"),
             Loc.N("Воины на карте — ничьи. Дойди до деревни и наведи мышь на любого: первая строка подсказки — как он к тебе. Слушаются тех, к кому верны.")),
            (Loc.N("Приказ"),
             Loc.N("Выдели воина левой кнопкой и прикажи идти правой. Он может отказать: голосует сам, верностью к тебе — и в журнале будет сказано почему.")),
            (Loc.N("Расположить"),
             Loc.N("Каждый грех ценит своё. Жадному — монета: подойди, посмотри на него, F. Гордому — приказ ему одному. Унылому — стоять на месте. Добейся, чтобы кто-нибудь стал тебе верен.")),
            (Loc.N("Свои люди"),
             Loc.N("Собери троих верных. Спасай их в бою, давай им дело: без дела верные остывают.")),
            (Loc.N("Охотники"),
             Loc.N("С троп идут охотники Ордена — враги всем. Отбей их. Каждые пять минут их будет больше, и к сильнейшему — больше всех.")),
            (Loc.N("Алтарь"),
             Loc.N("С павшего возьми тело (F) и отнеси к столу в склепе. Душу собери в банку, подойдя к ней. На алтаре: банку — в гнездо, тело — на ложе, рычаг. Поднятый сразу почти предан тебе.")),
            (Loc.N("Постройка"),
             Loc.N("Перед склепом — места для построек. Заложи сигнальный костёр (F у колышков) и приведи к нему верных — построят сами. Склеп бьют — у костра верные бегут на помощь.")),
            (Loc.N("Чужой трон"),
             Loc.N("Греховод на севере проснулся. Веди верных на его склеп: правой кнопкой по двери, потом по трону. Разрушь его трон.")),
        };

        // Короткий глагол виден всегда. Длинная причина открывается F1:
        // девять лекций подряд не помогают человеку найти нужную кнопку.
        private static readonly string[] Objectives =
        {
            Loc.N("Откройте дверь своего склепа клавишей F."),
            Loc.N("Наведите мышь на нейтрального воина у деревни."),
            Loc.N("Выделите воина ЛКМ и отдайте приказ ПКМ."),
            Loc.N("Добейтесь верности одного нейтрального воина."),
            Loc.N("Соберите трёх верных соратников."),
            Loc.N("Отразите учебное нападение охотников."),
            Loc.N("Поднимите нового воина на алтаре склепа."),
            Loc.N("Постройте сигнальный костёр при помощи верных."),
            Loc.N("Разрушьте чужой трон и выиграйте учебный матч.")
        };

        private int _step = -1;
        private bool _showDetails;
        private float _since, _doneAt = -1f;
        private int _raisedBefore;
        private bool _released;
        // Этот урок проверяет только охотников, выпущенных им самим.
        private readonly List<Warrior> _trainingHunters = new();
        private Warrior _attitudeTarget;
        private float _attitudeReadFor;
        private bool _attitudeRead;
        private const float AttitudeReadSeconds = 0.8f;

        private Text _title, _body;
        private GameObject _panel;

        public int Step => _step;
        public static TugTutorial Instance { get; private set; }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; RivalLord.Hold = false; }

        void Start()
        {
            Build();
            Next();
        }

        void Update()
        {
            if (_step < 0 || _step >= Lessons.Length) return;
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _showDetails = !_showDetails;
                RefreshInstruction();
            }
            if (_doneAt > 0f)
            {
                if (Time.unscaledTime - _doneAt > 1.6f) Next();
                return;
            }
            if (_step == 1) WatchAttitude();
            if (Done()) { _doneAt = Time.unscaledTime; _title.text = Loc.F("Урок {0} из {1} — готово", _step + 1, Lessons.Length); }
        }

        /// <summary>Сделан ли нынешний урок.</summary>
        private bool Done()
        {
            var match = TugMatch.Instance;
            if (match == null || !SinbinderPlayer.Exists) return false;
            int me = match.PlayerSide;
            switch (_step)
            {
                case 0: { var (door, _) = match.PartsOf(me); return door != null && door.Open; }
                case 1: return _attitudeRead;
                case 2: return AnyOrdered();
                // Монета сама по себе не делает воина верным. Проверяем результат, а не расход.
                case 3: return HunterWaves.Loyal(me) >= 1;
                case 4: return HunterWaves.Loyal(me) >= 3;
                // Посторонний враг или страж события не должны задерживать обучающую волну.
                case 5: return _released && _trainingHunters.Count > 0
                              && _trainingHunters.TrueForAll(w => w == null || w.IsDead);
                case 6: { var device = Device(); return device != null && device.Raised > _raisedBefore; }
                case 7:
                    foreach (var s in BuildSite.All)
                        if (s != null && s.Side == me && s.What == BuildSite.Kind.Fire && s.Done) return true;
                    return false;
                case 8: return TugMatch.Over && TugMatch.Won;
            }
            return false;
        }

        private void Next()
        {
            _step++;
            _doneAt = -1f;
            _since = Time.time;
            if (_step >= Lessons.Length)
            {
                _title.text = Loc.T("Обучение пройдено");
                _body.text = Loc.T("Ты знаешь всё, что нужно для настоящей партии. Карты — в паузе, «Карты „Перетягивания“».");
                return;
            }
            // Счётчик чтения сбрасывается при повторном прохождении шага.
            if (_step == 1) { _attitudeTarget = null; _attitudeReadFor = 0f; _attitudeRead = false; }
            // Запоминаем именно воинов обучающей волны, а не всех врагов карты.
            if (_step == 5 && HunterWaves.Instance != null)
            {
                _trainingHunters.Clear();
                _released = HunterWaves.Instance.Release(false, _trainingHunters) > 0;
                if (!_released) Debug.LogWarning("[ОБУЧЕНИЕ] не удалось выпустить обучающую волну: проверьте тропы карты.");
            }
            if (_step == 6) { var device = Device(); _raisedBefore = device != null ? device.Raised : 0; }
            if (_step == 8) RivalLord.Hold = false;

            _title.text = Loc.F("Урок {0} из {1}: {2}", _step + 1, Lessons.Length, Loc.T(Lessons[_step].title));
            _showDetails = false;
            RefreshInstruction();
            Debug.Log($"[ОБУЧЕНИЕ] урок {_step + 1}: {Lessons[_step].title}");
        }

        private void RefreshInstruction()
        {
            if (_step < 0 || _step >= Lessons.Length || _body == null || _panel == null) return;
            _body.text = _showDetails
                ? Loc.T(Lessons[_step].text) + "\n" + Loc.T("F1 — свернуть объяснение")
                : Loc.T(Objectives[_step]) + "\n" + Loc.T("F1 — объяснить подробнее");
            // Длинный контекст не запихиваем в прежние 84 пикселя:
            // при открытии объяснения панель подрастает вверх.
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(820f, _showDetails ? 245f : 132f);
            var bodyRect = _body.GetComponent<RectTransform>();
            bodyRect.sizeDelta = new Vector2(-36f, _showDetails ? 178f : 84f);
        }

        /// <summary>Перейти к уроку сразу (прогон обучения).</summary>
        public void Skip() { if (_step < Lessons.Length) { _doneAt = -1f; Next(); } }

        // Complete означает завершение всех девяти уроков, а не условие очередного шага.
        public bool Complete => _step >= Lessons.Length;

        /// <summary>Игрок действительно задержал взгляд на подсказке отношения к ничьему.</summary>
        private void WatchAttitude()
        {
            var hovered = UI.WarriorTooltipUI.Hovered;
            if (hovered == null || hovered.IsDead || hovered.Team != Team.Neutral || !NearVillage())
            {
                _attitudeTarget = null;
                _attitudeReadFor = 0f;
                return;
            }
            if (_attitudeTarget != hovered) { _attitudeTarget = hovered; _attitudeReadFor = 0f; }
            _attitudeReadFor += Time.unscaledDeltaTime;
            if (_attitudeReadFor >= AttitudeReadSeconds) _attitudeRead = true;
        }

#if UNITY_EDITOR
        /// <summary>Только для пакетного прогона, который не может двигать системный курсор.</summary>
        public void TestRecordAttitude(Warrior warrior)
        {
            if (_step == 1 && warrior != null && !warrior.IsDead && warrior.Team == Team.Neutral)
                _attitudeRead = true;
        }
#endif

        // ---------------------------------------------------------- приметы уроков

        private static bool NearVillage()
        {
            foreach (var r in FindObjectsByType<Residents>(FindObjectsSortMode.None))
                if (CampFocus.GroundDistance(SinbinderPlayer.Where, r.transform.position) < 18f) return true;
            return false;
        }

        private static bool AnyOrdered()
        {
            foreach (var w in FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w.Team == Team.Neutral && !w.IsDead && w.HasCommand && w.Command.Lord == Lords.Player) return true;
            return false;
        }

        private static Crypt.BindingDevice Device()
        {
            var m = TugMatch.Instance;
            return m != null && m.PlayerSide < m.Crypts.Count && m.Crypts[m.PlayerSide] != null
                ? m.Crypts[m.PlayerSide].GetComponentInChildren<Crypt.BindingDevice>() : null;
        }

        // ---------------------------------------------------------- плашка

        private void Build()
        {
            var canvasGo = new GameObject("Обучение — плашка", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            _panel = new GameObject("Плашка", typeof(RectTransform), typeof(Image), typeof(Outline));
            var rt = (RectTransform)_panel.transform;
            rt.SetParent(canvasGo.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 190f);
            rt.sizeDelta = new Vector2(820f, 132f);
            _panel.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.06f, 1f);   // непрозрачно: иначе рамка (Outline) просвечивает золотом
            var edge = _panel.GetComponent<Outline>();
            edge.effectColor = new Color(0.72f, 0.58f, 0.30f, 0.9f);
            edge.effectDistance = new Vector2(2f, 2f);

            var font = Resources.Load<Font>("Fonts/PTSerif-Regular");
            _title = Label(rt, font, 21, new Color(0.86f, 0.70f, 0.38f), new Vector2(0f, -10f), 30f);
            _body = Label(rt, font, 18, new Color(0.93f, 0.89f, 0.80f), new Vector2(0f, -42f), 84f);
        }

        private static Text Label(RectTransform parent, Font font, int size, Color colour, Vector2 at, float height)
        {
            var go = new GameObject("Строка", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(-36f, height);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = colour;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
