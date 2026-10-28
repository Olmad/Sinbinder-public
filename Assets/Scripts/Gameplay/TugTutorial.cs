// Assets/Scripts/Gameplay/TugTutorial.cs
// Перевод: текст через Loc
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

        private int _step = -1;
        private float _since, _doneAt = -1f;
        private int _raisedBefore;
        private bool _released;

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
            if (_doneAt > 0f)
            {
                if (Time.unscaledTime - _doneAt > 1.6f) Next();
                return;
            }
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
                case 1: return NearVillage() && Time.time - _since > 2f;
                case 2: return AnyOrdered();
                case 3: return HunterWaves.Loyal(me) >= 1 || Favor.Purse < Favor.StartPurse;
                case 4: return HunterWaves.Loyal(me) >= 3;
                case 5: return _released && Hunters() == 0;
                case 6: return Device() != null && Device().Raised > _raisedBefore;
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
            // Урок начинает своё: охотников выпускает, ИИ будит, счёт поднятых запоминает.
            if (_step == 5 && HunterWaves.Instance != null) { HunterWaves.Instance.Release(); _released = true; }
            if (_step == 6) _raisedBefore = Device() != null ? Device().Raised : 0;
            if (_step == 8) RivalLord.Hold = false;

            _title.text = Loc.F("Урок {0} из {1}: {2}", _step + 1, Lessons.Length, Loc.T(Lessons[_step].title));
            _body.text = Loc.T(Lessons[_step].text);
            Debug.Log($"[ОБУЧЕНИЕ] урок {_step + 1}: {Lessons[_step].title}");
        }

        /// <summary>Перейти к уроку сразу (прогон обучения).</summary>
        public void Skip() { if (_step < Lessons.Length) { _doneAt = -1f; Next(); } }

        public bool Complete => Done();

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

        private static int Hunters()
        {
            int n = 0;
            foreach (var w in FindObjectsByType<Warrior>(FindObjectsSortMode.None)) if (w.Team == Team.Enemy && !w.IsDead) n++;
            return n;
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
