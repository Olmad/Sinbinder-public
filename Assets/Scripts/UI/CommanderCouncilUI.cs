// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

using Sinbinder.Core;
namespace Sinbinder.UI
{
    /// <summary>
    /// Доля 3 пролога: военный совет. Три кандидата, у каждого —
    /// распечатанное пророчество (docs/09-PROLOGUE.md §3).
    ///
    /// Это самая важная панель демо, и не потому, что красивая. Строка
    /// «когда велено отойти — не отходит» закрывает третье требование
    /// к демо из 00-GDD.md §8: «отказ можно было предотвратить, и игрок
    /// это видит». Игрок прочитал. Игрок выбрал. Через четыре минуты
    /// строка сбудется, и это будет не подстава, а исполнившееся обещание.
    ///
    /// Пророчество не пишется руками: <see cref="TemperamentPredictor"/>
    /// прогоняет настоящие модули против выдуманных положений и печатает,
    /// что победит. Движок здесь становится собственным интерфейсом —
    /// иначе панель однажды пообещает не то, что случится.
    ///
    /// Настройка в сцене: повесить на объект под Canvas, задать _panel
    /// (корень панели, выключается после выбора) и _rows (контейнер,
    /// в который кладутся строки кандидатов).
    /// </summary>
    public class CommanderCouncilUI : MonoBehaviour
    {
        /// <summary>
        /// Греховод у стола, и совет ждёт F. Разговор с воином и сундук
        /// (<see cref="GearPanel"/>) эту F не берут: совет — главная сцена
        /// пролога, и F открывала бы оба экрана разом, одну паузу поверх другой.
        /// </summary>
        public static bool AtTable { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => AtTable = false;

        void OnDestroy() => AtTable = false;

        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Font _font;

        [Header("Три столбца")]
        [Tooltip("Средний столбец: кто выбран и чего от него ждать.")]
        [SerializeField] private Text _detail;

        [Tooltip("Правый столбец: куда отряд идёт и что там известно.")]
        [SerializeField] private Text _quest;

        [SerializeField] private Button _confirm;
        [SerializeField] private Text _confirmLabel;

        [Header("Открытие")]
        [Tooltip("Совет открывается нажатием, а не подходом: игрок должен "
               + "решить, что он к столу подошёл, а не оказаться перед "
               + "выбором из трёх судеб оттого, что прошёл мимо.")]
        [SerializeField] private KeyCode _openKey = KeyCode.F;

        [Tooltip("Запасной срок — только для сцены без шара: подходить "
               + "не к чему, и совет откроется сам и скажет об этом в консоль. "
               + "Шар есть — совет ждёт игрока у стола, сколько потребуется "
               + "(с 24 сентября: «можно просто стоять, а сюжет будет "
               + "двигаться», слово автора)." + Break
               + "Было 45 секунд, и это столкнулось со сбором к столу: "
               + "Карган просит собрать отряд на четырнадцатой секунде, "
               + "и на весь сбор оставалось полминуты. Игрок, делающий "
               + "ровно то, о чём его попросили, получал совет, открытый "
               + "у него за спиной. Страховке незачем спешить: она нужна "
               + "для сломанной сцены, а не для медлительного игрока.")]
        [SerializeField] private float _openAfterSeconds = 180f;

        private bool _approached;

        /// <summary>
        /// Позвал ли Карган. До зова к столу подходить незачем: порядок
        /// пролога — вышел из палатки, подошёл провожатый, и только тогда
        /// Карган подзывает (14-HANDOFF §25).
        /// </summary>
        private bool _summoned;

        [Tooltip("Страховка зова: провожатый так и не дошёл. Истекая, "
               + "пишет предупреждение.")]
        [SerializeField] private float _summonSafety = 90f;

        [Tooltip("Как часто Карган напоминает, что ждёт у стола, пока игрок "
               + "не подошёл. Напоминает, а не открывает совет сам.")]
        [SerializeField] private float _summonNudge = 60f;

        /// <summary>Кого игрок отметил. Назначения ещё не было.</summary>
        private Option? _picked;

        /// <summary>
        /// Второй шаг: старший выбран, игрок собирает ему отряд. До 16 октября
        /// шага не было — спутников подбирал список (автор: «меня очень
        /// напрягает, что… войны, отправляемые на них, фиксированные»).
        /// </summary>
        private bool _gathering;

        /// <summary>Кто идёт со старшим — голые имена (DisplayName), без него самого.</summary>
        private readonly HashSet<string> _party = new();

        /// <summary>
        /// Кого игрок предпочитает — и потому чья половина списка сверху.
        ///
        /// Не настройка, а наблюдение: список перестраивается под того,
        /// кого игрок только что отметил. Отметил опытного — опытные идут
        /// первыми; отметил новичка — первыми новички. Панель показывает
        /// игроку его же выбор, а не спорит с ним.
        /// </summary>
        private bool _preferExperienced = true;

        [Tooltip("Сколько человек требует миссия доли 3. Карган объясняет "
               + "это вслух: «там довольно опасно, нужно пятеро». Тот, чей "
               + "навык командования столько не уводит, в списке виден, но "
               + "не выбирается — и рядом написано почему.")]
        [SerializeField] private int _requiredSquad = 5;

        private readonly List<GameObject> _spawned = new();
        private bool _done;
        private CrystalBall _ball;

        void Start()
        {
            // Панель, которую выключает сам компонент, не должна быть его
            // собственным объектом: выключенный объект не крутит Update,
            // и совет перестал бы замечать подходящего игрока. Сборщик
            // вешает компонент на Canvas, но сцену могли собрать и руками.
            if (_panel == gameObject)
                Debug.LogError("[СОВЕТ] Компонент висит на панели, которую сам "
                             + "выключает: подход к столу замечен не будет. "
                             + "Перевесить его на Canvas.");

            if (_panel != null) _panel.SetActive(false);

            // Совет бывает один раз за пролог. Если игрок уже выбрал —
            // сцена перезапущена или это следующая доля, панель не нужна.
            if (!string.IsNullOrEmpty(SquadRoster.CommanderName)) { _done = true; return; }

            // Совет собирает шар: игрок подходит к столу, и Карган его
            // окликает (docs/09-PROLOGUE.md §4, сцена 2). Счётчик остаётся
            // страховкой — без него сцена без шара запирает демо насмерть,
            // потому что PrologueDirector ждёт назначенного старшего.
            _ball = Object.FindFirstObjectByType<CrystalBall>();

            if (_ball == null)
                Debug.LogWarning("[СОВЕТ] Шара в сцене нет: подходить не к чему, "
                               + "совет откроется по счётчику.");

            StartCoroutine(Summoning());
        }

        /// <summary>
        /// Карган зовёт к шару, когда первый воин пошёл рядом, — не в первом
        /// кадре сцены. До 13 сентября оклик звучал ещё до выхода из палатки,
        /// а к столу можно было подойти и открыть совет раньше, чем лагерь
        /// успел хоть что-то сказать.
        ///
        /// Счётчик страховки заводится отсюда же, после зова: отсчитывать
        /// «игрок не подошёл» раньше, чем его позвали, значило бы корить
        /// за медлительность того, кого ещё не просили.
        /// </summary>
        private System.Collections.IEnumerator Summoning()
        {
            yield return Gameplay.Beat.Until(() => Gameplay.CampOpening.EscortArrived,
                _summonSafety,
                Loc.T("Провожатый так и не пошёл рядом — Карган зовёт к шару без него."));

            Summon();
            _summoned = true;

            // Сам совет открывается только в сцене без шара. С шаром
            // шаг — игрока: подойти к столу. До 24 сентября через три
            // минуты совет открывался у него за спиной.
            if (_ball == null)
            {
                Invoke(nameof(OpenBySelf), _openAfterSeconds);
                yield break;
            }

            yield return Gameplay.Beat.UntilPlayer(() => _approached || _done,
                _summonNudge, Loc.T("Карган ждёт у стола с шаром."));
        }

        /// <summary>
        /// Карган окликает игрока и объясняет, зачем идти к столу.
        /// Без этой строки «подойди к шару» остаётся догадкой, а игра,
        /// которая никогда не пишет цифр, обязана хотя бы писать словами.
        /// </summary>
        private void Summon()
        {
            var log = Object.FindFirstObjectByType<BattleLogUI>();
            if (log == null) return;

            // Одной строкой, а не двумя: в лагере до разгрома их проходило
            // одиннадцать подряд — двадцать одна секунда сплошного текста
            // в сцене, где игрок ещё учится ходить. Он либо читает
            // и не играет, либо играет и не читает.
            // Строка по проходу текстов (47-TEXTS §1, п. 1; автор: «да»): «точка
            // интереса» — слово из редактора карт, а зачем посылать, не говорилось.
            Gameplay.Herald.Line(Loc.T("Карган: «Владыка, трофеев с прошлой вылазки надолго "
                    + "не хватит — людям платить нечем. Разведка нашла брошенную "
                    + "заставу. Пошлём туда отряд». Он ждёт у стола с шаром."));
        }

        void Update()
        {
            AtTable = !_done && _ball != null && _summoned && _ball.PlayerIsClose();
            if (!AtTable) return;

            // Подойти — не значит согласиться. Раньше панель открывалась
            // в тот же кадр, в который игрок оказался рядом, и выбор из трёх
            // судеб падал на него без спроса. Теперь у стола Карган говорит,
            // зачем звал, а совет держится нажатием: решение начинается
            // с решения его начать.
            if (!_approached)
            {
                _approached = true;
                Greet();
            }

            if (!Sinbinder.Core.Controls.Down(Sinbinder.Core.Act.Interact)) return;

            // F, набранная в консоли (~), — буква, а не «открыть совет».
            if (Dev.CheatConsole.Open) return;
            if (GearPanel.Open) return;

            Open();
        }

        /// <summary>
        /// Что Карган говорит, когда игрок дошёл, — до того, как открылась
        /// панель. Отдельная реплика, а не повтор оклика: та звала к столу,
        /// эта объявляет, что сейчас придётся выбирать.
        /// </summary>
        private void Greet()
        {
            var log = Object.FindFirstObjectByType<BattleLogUI>();
            if (log == null) return;

            Gameplay.Herald.Line(Loc.F("Карган: «Пришли. Нажмите {0} — и решайте, владыка: "
                    + "кого отправить, а кого при себе оставить. Я скажу, "
                    + "чего от каждого ждать».", Sinbinder.Core.Controls.Name(Sinbinder.Core.Act.Interact)));
        }

        /// <summary>
        /// Открыть по истечении страховочного срока. Отдельный вход,
        /// чтобы отличить «игрок пришёл» от «его не дождались»: второе
        /// обязано быть слышно.
        /// </summary>
        private void OpenBySelf()
        {
            if (_done || _panel == null || _panel.activeSelf) return;

            Debug.LogWarning("[СОВЕТ] К столу так и не подошли: совет открыт "
                           + "по счётчику.");
            Open();
        }

        public void Open()
        {
            if (_done || _panel == null || _panel.activeSelf) return;

            var options = FindOptions();
            if (options.Count == 0) return;

            _picked = null;
            _gathering = false;
            _party.Clear();

            // Кнопку связываем здесь, а не в Start: панель может быть
            // собрана руками, и слушатель, повешенный дважды, назначил бы
            // старшего дважды.
            if (_confirm != null)
            {
                _confirm.onClick.RemoveAllListeners();
                _confirm.onClick.AddListener(Appoint);
            }

            if (_quest != null)
            {
                Map();
                _quest.text = QuestText();
            }
            FitDetail();
            ShowDetail(null);
            Build(options);

            // Сперва — шар: куда идти, и только потом — кто поведёт.
            ShowMap();

            Modal.Open(_panel);
            Core.GamePauseController.Instance?.Pause();
        }

        /// <summary>
        /// Строка совета: воин, его навык и причина, по которой его нельзя
        /// поставить старшим (пусто — можно).
        /// </summary>
        private readonly struct Option
        {
            public readonly Warrior Warrior;
            public readonly float Skill;
            public readonly string Blocked;

            public Option(Warrior warrior, float skill, string blocked)
            {
                Warrior = warrior;
                Skill = skill;
                Blocked = blocked;
            }

            public bool CanChoose => string.IsNullOrEmpty(Blocked);
        }

        /// <summary>
        /// В списке — <b>все</b> живые свои, а не трое отмеченных.
        /// Повести отряд может и рядовой: навык командования решает не
        /// «можно ли», а «скольких уведёт» (docs/09-PROLOGUE.md §4).
        /// Опытные идут первыми и подписаны — это и есть то выделение,
        /// ради которого список вообще существует.
        ///
        /// Пророчество спрашиваем у настоящей души со сцены, а не у записи
        /// в составе: у неё есть смещения оболочки, а у записи их нет.
        /// </summary>
        private List<Option> FindOptions()
        {
            var options = new List<Option>();

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;

                // Греховод в старшие не годится: он тот, кто отдаёт приказ
                // и остаётся. В списке он стоял наравне с отрядом и прятался
                // только за нехваткой навыка — то есть подними ему навык,
                // и игрок отправил бы сам себя, а ждать его возвращения
                // было бы некому.
                //
                // Отличаем так же, как отличает панель выделения, — по ногам:
                // WASD ведёт только его.
                if (w.GetComponent<PlayerWalk>() != null) continue;

                float skill = 0f;
                string blocked = "";

                if (SquadRoster.TryGet(w.DisplayName, out var member))
                {
                    skill = member.Leadership;
                    blocked = Loc.T(member.Unavailable);
                }

                // Навыка не хватает на столько, сколько советует Карган, —
                // до 16 октября это был запрет: рядовой виден, но не выбирается.
                // Теперь это риск (автор: «нужно, чтобы была возможность
                // отправить и меньше… даже просто лидера»): поведёт, но уведёт
                // троих, и строка говорит это сама. Заодно ушла страховка
                // «никто не проходит порог — порог снят»: порога нет.
                options.Add(new Option(w, skill, blocked));
            }

            // Порядок обхода сцены не гарантирован, а список обязан
            // выглядеть одинаково при каждом запуске: ничего случайного.
            // Сперва те, кого выбрать можно, среди них — по навыку.
            options.Sort((a, b) =>
            {
                if (a.CanChoose != b.CanChoose) return a.CanChoose ? -1 : 1;

                // Половина, которую игрок предпочитает, идёт первой.
                bool ea = Leadership.IsExperienced(a.Skill);
                bool eb = Leadership.IsExperienced(b.Skill);
                if (ea != eb) return ea == _preferExperienced ? -1 : 1;

                if (!Mathf.Approximately(a.Skill, b.Skill)) return b.Skill.CompareTo(a.Skill);
                return string.CompareOrdinal(a.Warrior.DisplayName, b.Warrior.DisplayName);
            });

            return options;
        }

        private void Build(List<Option> options)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_title != null) _title.text = Loc.T("Кто поведёт отряд к заставе");

            // Шаг строки считаем по месту, а не берём готовым. Имён
            // в лагере двенадцать, а места в столбце — на семь строк;
            // снимок прохождения 18 сентября показал, что пятеро
            // последних просто вываливались из панели наружу, поверх
            // мира. Выбрать из списка, которого не видно, нельзя.
            float room = _rows == null ? 0f : _rows.rect.height;
            float step = options.Count > 0 && room > 1f
                       ? Mathf.Min(78f, room / options.Count)
                       : 78f;

            float y = 0f;
            foreach (var option in options)
            {
                var row = Row(option, y, Mathf.Max(30f, step - 8f));
                _spawned.Add(row);
                y -= step;
            }
        }

        private GameObject Row(Option option, float y, float height)
        {
            var warrior = option.Warrior;

            // Кнопка вешается только на тех, кого можно выбрать. Строка,
            // которая не сработает, не должна и нажиматься: игра про
            // настоящие решения не имеет права соврать на первом же.
            //
            // Щелчок отмечает, а не назначает. Пророчество — четыре
            // строки, и решать по ним, не прочитав, игрок не должен:
            // раньше первый же щелчок был окончательным.
            var chosen = option;
            var rt = Plate(warrior.DisplayName, y, height, option.CanChoose,
                           option.CanChoose ? () => Select(chosen) : (System.Action)null);

            Label(rt, Loc.Name(warrior.ShownName), 26, new Vector2(18f, -8f), 32f,
                option.CanChoose);

            // Навык командования словами, без шкалы: цифры игроку не
            // показываются нигде (docs/00-GDD.md §7). Для невыбираемого
            // здесь стоит причина — и она же объясняет, чего ему не хватает.
            Label(rt, !option.CanChoose ? option.Blocked
                    : Leadership.CanLead(option.Skill, _requiredSquad) ? Leadership.Describe(option.Skill)
                    : Leadership.Shortfall(option.Skill, _requiredSquad),
                18, new Vector2(18f, -Mathf.Min(38f, height * 0.55f)), 24f,
                option.CanChoose);

            return rt.gameObject;
        }

        /// <summary>
        /// Плашка строки в списке слева: место, цвет, кнопка. Кнопка — только
        /// если щелчок что-то сделает.
        /// </summary>
        private RectTransform Plate(string name, float y, float height, bool bright, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);

            var image = go.AddComponent<Image>();
            image.color = bright
                ? new Color(0.10f, 0.09f, 0.08f, 0.92f)
                : new Color(0.07f, 0.06f, 0.06f, 0.80f);

            if (onClick != null)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => onClick());
            }

            return rt;
        }

        private void Label(RectTransform parent, string text, int size,
            Vector2 offset, float height, bool bright = true)
        {
            var go = new GameObject("Строка", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(offset.x, 0f);
            rt.offsetMax = new Vector2(-18f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(offset.x, offset.y);

            var label = go.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.alignment = TextAnchor.UpperLeft;
            label.color = bright
                ? new Color(0.91f, 0.89f, 0.85f)
                : new Color(0.55f, 0.52f, 0.48f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;      // клик обязан доходить до строки
            label.text = text;
        }

        /// <summary>
        /// Отметить кандидата: середина рассказывает о нём, список
        /// перестраивается под его половину, кнопка назначения оживает.
        /// </summary>
        private void Select(Option option)
        {
            if (_done) return;

            _picked = option;
            _preferExperienced = Leadership.IsExperienced(option.Skill);

            ShowDetail(option);
            Build(FindOptions());
        }

        /// <summary>
        /// Средний столбец. Пока никто не отмечен — говорит, что делать,
        /// а не молчит пустотой: пустая колонка читается как поломка.
        /// </summary>
        private void ShowDetail(Option? option)
        {
            bool has = option.HasValue;

            if (_confirm != null) _confirm.interactable = has;

            if (_confirmLabel != null)
                _confirmLabel.text = has
                    ? Loc.F("Дальше: кого возьмёт {0}", Loc.Name(option.Value.Warrior.ShownName))
                    : Loc.T("Выберите, кого поставить");

            if (_detail == null) return;

            if (!has)
            {
                _detail.text = Loc.T("Выберите имя слева.") + Break
                             + Loc.T("Здесь Карган скажет, чего от него ждать: "
                             + "сколько уведёт и как поведёт себя, "
                             + "когда станет трудно.");
                return;
            }

            var o = option.Value;

            // Пророчество не пишется руками: его считает движок теми же
            // модулями, что решают в бою. Иначе панель однажды пообещает
            // не то, что случится.
            _detail.text = Loc.Name(o.Warrior.ShownName) + Break
                         + Leadership.Describe(o.Skill) + Break
                         + Leads(o.Warrior) + Break
                         + TemperamentPredictor.Describe(o.Warrior);
        }

        /// <summary>
        /// Как отряд поведёт себя под ним в вылазке (47-TEXTS §1, п. 11; автор:
        /// «да»). Пророчество ниже — о нём самом в бою, а исход вылазки решает
        /// его грех как старшего: та же установка (<see cref="SquadOrders.FromSin"/>)
        /// уходит в автобой (<c>Expedition</c>). Обещание, которое игра сдержит.
        /// </summary>
        private static string Leads(Warrior warrior)
        {
            if (warrior.Soul == null) return "";
            string how = SquadOrders.Describe(SquadOrders.FromSin(warrior.Soul.Sin));
            if (string.IsNullOrEmpty(how)) return "";
            how = char.ToLower(how[0]) + how.Substring(1);
            return Grammar.Pick(warrior.Gender,
                Loc.F("В вылазке под его началом: {0}", how),
                Loc.F("В вылазке под её началом: {0}", how));
        }

        /// <summary>Пустая строка между абзацами. Одно место на весь класс.</summary>
        private const string Break = "\n\n";

        /// <summary>
        /// Правый столбец: куда идёт отряд. Без чисел — сколько нужно
        /// людей, сказано словами, как и всё остальное.
        /// </summary>
        private string QuestText()
        {
            // До 24.11 первой шла строка «Огни на карте — два отряда уже в пути…» под плоской
            // картой; теперь это говорит Карган у шара (ShowMap), а над текстом — кадр заставы.
            return Scouts() + Break + Advice() + Break + Leaving();
        }

        private static string Scouts() => Loc.T("Разведка донесла: за холмами стоит брошенная застава. "
                 + "Взять там есть что, и потому там небезопасно.");

        private string Advice() => Loc.F("Карган: «Там довольно опасно, владыка. Я бы послал {0}. "
                 + "Рядовой столько не уведёт — за ним пойдут трое. "
                 + "Пошлёте меньше — рискуете всеми, кто пойдёт».", Leadership.Count(_requiredSquad));

        private static string Leaving() => Loc.T("Отряд уйдёт сразу и вернётся не скоро — в лагере с вами "
                 + "останутся только те, кого вы не пошлёте. Вернётся не весь "
                 + "и не таким, каким уходил.");

        // ---------- до выбора: шар с картой и карточка места ----------

        /// <summary>Куда идёт отряд — имя места на карте (<see cref="ReliefMap"/>); в лагере его зовут брошенной.</summary>
        private const string Outpost = "Сожжённая застава";

        /// <summary>Где уже стоят отряды — их огни в шаре гаснут на тревоге (<see cref="CrystalBall"/>).</summary>
        private static string[] Away => new[] { "Придорожная часовня", "Затопленная каменоломня" };

        private static readonly Color Heading = new(0.95f, 0.90f, 0.80f);
        private static readonly Color Body = new(0.86f, 0.83f, 0.78f);
        private static readonly Color Dim = new(0.68f, 0.64f, 0.58f);

        /// <summary>Шаг до столбцов — шар или карточка; столбцы под ним выключены.</summary>
        private GameObject _stage;
        private Text _hint;

        /// <summary>
        /// Первый шаг совета — шар с картой окрестностей, та же земля, что в стекле на столе
        /// и потом на карте склепа (замысел автора: «открывается карта, несколько мест интереса
        /// с уже отправленными отрядами и одно свободное — куда и отправляется»). Это и есть
        /// обучение вылазке: место → что о нём известно → кто поведёт → кого возьмёт.
        /// Нажать можно только заставу — остальные заняты.
        /// </summary>
        private void ShowMap()
        {
            if (_panel == null || _done) return;
            Columns(false);
            if (_title != null) _title.text = Loc.T("Военный совет: куда послать отряд");

            var stage = Stage(out float w, out float h);
            float ball = Mathf.Min(h - 8f, w * 0.52f);
            var sphere = ReliefMap.Ball(stage, ball, new Vector2(0f, -4f));
            // Огни крупные: земля в шаре светлая, а огни — то, о чём Карган говорит первым.
            foreach (var place in Away) ReliefMap.Glow(sphere, place, SquadLight, 70f);

            ReliefMap.Point(sphere, ReliefMap.Camp, Loc.T("Лагерь"), Loc.T("вы здесь"),
                            new Color(0.75f, 0.70f, 0.62f), true, _font, null, home: true);
            foreach (var place in Away)
            {
                string p = place;
                // Яркая, но без нажатия: кнопку Point вешает только при click.
                ReliefMap.Point(sphere, p, Loc.Name(p), Loc.T("там ваш отряд"), SquadLight, true, _font,
                    null, () => Hint($"<size=24>{Loc.Name(p)}</size>\n"
                        + Loc.T("Туда уже ушёл ваш отряд — огонь над местом его. Вернутся — расскажут, что нашли.")));
            }
            string free = $"<size=24>{Loc.T("Брошенная застава")}</size>\n"
                        + Loc.T("Туда не ушёл никто. Нажмите — прочтёте, что о ней известно.");
            ReliefMap.Point(sphere, Outpost, Loc.T("Брошенная застава"), Loc.T("свободна"), FreePlace, true, _font,
                ShowPlace, () => Hint(free));

            float x = ball + 28f, tw = Mathf.Max(240f, w - x);
            Words(stage, x, -6f, tw, 40f, Loc.T("Окрестности лагеря"), 28, Heading);
            Words(stage, x, -54f, tw, 170f, Loc.T("Карган: «Вот наши края, владыка. Огни в шаре — наши отряды: "
                + "один ушёл к придорожной часовне, другой — к затопленной каменоломне. Пока огонь горит — "
                + "они живы. Свободна одна брошенная застава — туда и пошлём»."), 20, Body);
            Words(stage, x, -232f, tw, 56f, Loc.T("Нажмите на заставу в шаре — прочтёте, что о ней известно."), 18, Dim);
            _hint = Words(stage, x, -300f, tw, 200f, free, 20, Body);
            Press(stage, x, -(h - 70f), tw, 64f, Loc.T("Брошенная застава →"), ShowPlace);
        }

        private void Hint(string text)
        {
            if (_hint != null) _hint.text = text;
        }

        /// <summary>
        /// Карточка заставы: кадр места, что донесла разведка и что советует Карган — слева;
        /// как ходят в вылазку — справа. «К карте» — назад, дальше — выбор старшего.
        /// </summary>
        private void ShowPlace()
        {
            if (_panel == null || _done) return;
            if (_title != null) _title.text = Loc.T("Военный совет: брошенная застава");

            var stage = Stage(out float w, out float h);
            float top = -ReliefMap.Header(stage, 0f, 250f, Outpost, Loc.T("Брошенная застава"), _font) - 20f;
            float half = (w - 36f) * 0.5f, room = h + top - 92f;
            Words(stage, 0f, top, half, room, Scouts() + Break + Advice(), 20, Body);
            Words(stage, half + 36f, top, half, room,
                Loc.T("Отряд поведёт старший — вы останетесь в лагере. Бой там решат сами души: "
                    + "как кто поступит, когда станет трудно, сказано у каждого имени.") + Break
                + Loc.T("Сперва выберите старшего, потом — кого он возьмёт с собой.") + Break
                + Leaving(), 20, Body);
            Press(stage, 0f, -(h - 70f), 300f, 64f, Loc.T("← К карте"), ShowMap);
            Press(stage, w - 440f, -(h - 70f), 440f, 64f, Loc.T("Решить, кто поведёт →"), ShowColumns);
        }

        /// <summary>К столбцам: кто поведёт. Шар больше не нужен.</summary>
        private void ShowColumns()
        {
            DropStage();
            Columns(true);
            if (_done) return;
            if (_gathering) BuildParty();
            else { ShowDetail(_picked); Build(FindOptions()); }
        }

        private void DropStage()
        {
            if (_stage == null) return;
            _stage.SetActive(false);
            Destroy(_stage);
            _stage = null;
            _hint = null;
        }

        /// <summary>Три столбца совета — выключить на время шара или вернуть.</summary>
        private void Columns(bool on)
        {
            if (_rows != null) _rows.gameObject.SetActive(on);
            if (_detail != null && _detail.transform.parent != null) _detail.transform.parent.gameObject.SetActive(on);
            if (_quest != null && _quest.transform.parent != null) _quest.transform.parent.gameObject.SetActive(on);
        }

        /// <summary>Чистый лист под заголовком панели, там же, где столбцы.</summary>
        private RectTransform Stage(out float width, out float height)
        {
            DropStage();
            _stage = new GameObject("Совет — шар и место", typeof(RectTransform));   // ключ: имя объекта
            _stage.transform.SetParent(_panel.transform, false);
            var rt = (RectTransform)_stage.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 24f); rt.offsetMax = new Vector2(-20f, -84f);
            // Раскладка ещё не посчитана — размеры панели из сборщика (1280×760).
            width = rt.rect.width > 100f ? rt.rect.width : 1240f;
            height = rt.rect.height > 100f ? rt.rect.height : 652f;
            return rt;
        }

        private Text Words(RectTransform parent, float x, float top, float width, float height,
                           string text, int size, Color colour)
        {
            var go = new GameObject("Текст", typeof(RectTransform));   // ключ: имя объекта
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, top);
            var label = go.AddComponent<Text>();
            label.font = _font; label.fontSize = size; label.text = text; label.color = colour;
            label.supportRichText = true; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.alignment = TextAnchor.UpperLeft;
            return label;
        }

        private void Press(RectTransform parent, float x, float top, float width, float height,
                           string text, System.Action onClick)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, top);
            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.14f, 0.12f, 0.10f, 0.95f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(() => onClick());
            var label = Words(rt, 0f, -16f, width, height - 16f, text, 26, new Color(0.91f, 0.89f, 0.85f));
            label.alignment = TextAnchor.UpperCenter;
        }

        /// <summary>
        /// Средний столбец — до кнопки, шрифт мельче. С 16 октября в нём на строку
        /// «как поведёт отряд» больше, и при прежней высоте (380) обрезалась
        /// последняя строка пророчества — «когда велено отойти», ради которой
        /// совет и есть (замер шрифта и ширины столбца: ~450 при 380). Место
        /// под текстом до кнопки было пустым.
        /// </summary>
        private void FitDetail()
        {
            if (_detail == null || _confirm == null) return;
            var column = _detail.rectTransform.parent as RectTransform;
            var button = _confirm.transform as RectTransform;
            if (column == null || button == null || button.parent != column) return;

            float h = column.rect.height > 100f ? column.rect.height : 652f;
            // Кнопка привязана к середине столбца: верх кнопки — от верха столбца.
            float buttonTop = h * 0.5f - (button.anchoredPosition.y + button.rect.height * 0.5f);
            var rt = _detail.rectTransform;
            float room = buttonTop + rt.anchoredPosition.y - 12f;
            if (room > rt.sizeDelta.y) rt.sizeDelta = new Vector2(rt.sizeDelta.x, room);
            _detail.fontSize = Mathf.Min(_detail.fontSize, 19);
        }

        // ---------- карта в шаре ----------

        /// <summary>Сколько места карта берёт сверху правого столбца, пикселей.</summary>
        private const float MapHeight = 180f;

        /// <summary>Огни отрядов — тот же тёплый свет, что плывёт в шаре (<see cref="CrystalBall"/>).</summary>
        private static readonly Color SquadLight = new(1f, 0.93f, 0.78f);

        /// <summary>Свободное место — уголь: сюда и пойдёт отряд.</summary>
        private static readonly Color FreePlace = new(0.96f, 0.58f, 0.22f);

        private bool _mapBuilt;

        /// <summary>
        /// Верх правого столбца. С 16 октября по 24 ноября здесь была плоская карта (лагерь, три
        /// дороги, огни): до неё «два отряда в пути» жили только огнями в шаре, и игрок не знал,
        /// что это отряды, пока они не гасли на тревоге. Теперь карта — сам шар, первым шагом
        /// совета (<see cref="ShowMap"/>), а здесь — кадр заставы, куда идёт отряд.
        ///
        /// Собирается кодом один раз: сборщик сцены о нём не знает, и сцену пересобирать не нужно.
        /// </summary>
        private void Map()
        {
            if (_mapBuilt || _quest == null) return;
            var column = _quest.rectTransform.parent as RectTransform;
            if (column == null) return;
            _mapBuilt = true;

            // С 24 ноября карта — в шаре, первым шагом совета (ShowMap). Здесь — кадр заставы:
            // куда идёт отряд, и нажатие на него — назад к шару.
            Target(column);
        }

        /// <summary>Кадр заставы сверху правого столбца; нажатие — снова шар.</summary>
        private void Target(RectTransform column)
        {
            float colH = column.rect.height > 100f ? column.rect.height : 652f;
            ReliefMap.Header(column, -12f, MapHeight, Outpost, Loc.T("Брошенная застава"), _font);
            var shot = column.Find("Кадр места") as RectTransform;
            if (shot != null)
            {
                shot.offsetMin = new Vector2(12f, shot.offsetMin.y);
                shot.offsetMax = new Vector2(-12f, shot.offsetMax.y);
                var graphic = shot.GetComponent<Graphic>();
                graphic.raycastTarget = true;
                var button = shot.gameObject.AddComponent<Button>();
                button.targetGraphic = graphic;
                button.onClick.AddListener(ShowMap);
                Caption(shot, Loc.T("← К карте"), new Vector2(12f, MapHeight - 8f), 17, true, 200f, centred: false);
                // Столбец узкий: крупное имя из шапки карточки упиралось в край.
                var title = shot.Find("Имя места")?.GetComponent<Text>();
                if (title != null) title.fontSize = 28;
            }

            var rt = _quest.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -12f - MapHeight - 12f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(120f, colH - MapHeight - 36f));
            _quest.fontSize = Mathf.Min(_quest.fontSize, 18);
        }

        /// <summary>
        /// Подпись на карте. <paramref name="at"/> — верх подписи: левый край
        /// или середина (<paramref name="centred"/>). За край карты не выходит.
        /// </summary>
        private void Caption(RectTransform map, string text, Vector2 at, int size, bool bright,
                             float width, bool centred)
        {
            const float wide = 160f;
            float left = centred ? at.x - wide * 0.5f : at.x;
            left = Mathf.Clamp(left, 4f, Mathf.Max(4f, width - wide - 4f));

            var go = new GameObject("Подпись", typeof(RectTransform));   // ключ: имя объекта
            go.transform.SetParent(map, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(wide, size + 8f);
            rt.anchoredPosition = new Vector2(left, at.y);
            var label = go.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.alignment = centred ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            label.color = bright ? new Color(0.95f, 0.88f, 0.74f) : new Color(0.66f, 0.62f, 0.56f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        }

        /// <summary>Назначить отмеченного. Отдельно от отметки: решение одно.</summary>
        /// <summary>
        /// Кнопка. На первом шаге — к сбору отряда; на втором — отправить,
        /// когда набрано столько, сколько нужно.
        /// </summary>
        private void Appoint()
        {
            if (_done || !_picked.HasValue) return;
            if (!_gathering) { Gather(); return; }
            Choose(_picked.Value.Warrior);
        }

        /// <summary>Скольких старший возьмёт сверх себя: сколько за ним пойдёт, минус он сам.</summary>
        private int Room => Leadership.SquadSize(_picked.Value.Skill) - 1;

        // ---------- второй шаг: кого он возьмёт ----------

        /// <summary>
        /// Открыть сбор с подсказкой — прежним правилом набора
        /// (<see cref="SquadRoster.Suggest"/>): не тронувший ничего получает
        /// то, что получал до 16 октября. Опытные по подсказке остаются:
        /// лагерь не должен пустеть сам. Взять их игрок может.
        /// </summary>
        private void Gather()
        {
            _gathering = true;
            _party.Clear();
            int size = Mathf.Min(_requiredSquad, Room + 1);
            foreach (var name in SquadRoster.Suggest(_picked.Value.Warrior.DisplayName,
                                                     size, keepExperienced: true))
                _party.Add(name);
            BuildParty();
        }

        /// <summary>Назад, к выбору старшего. Собранное забывается: другой старший — другой отряд.</summary>
        private void Ungather()
        {
            _gathering = false;
            _party.Clear();
            ShowDetail(_picked);
            Build(FindOptions());
        }

        private void Toggle(string name)
        {
            if (_party.Contains(name)) _party.Remove(name);
            else if (_party.Count < Room) _party.Add(name);
            else
            {
                var leader = _picked.Value.Warrior;
                BuildParty(Grammar.Pick(leader.Gender,
                    Loc.T("Больше за ним не пойдут. Чтобы взять этого, сперва оставьте кого-нибудь в лагере."),
                    Loc.T("Больше за ней не пойдут. Чтобы взять этого, сперва оставьте кого-нибудь в лагере.")));
                return;
            }
            BuildParty();
        }

        /// <summary>
        /// Список отряда на втором шаге. Каждый — идёт или остаётся с вами;
        /// щелчок меняет. Телохранитель виден и не выбирается — с причиной.
        /// Порядок — состава, а не сцены: одинаковый при каждом запуске.
        /// </summary>
        private void BuildParty(string warning = null)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            var leader = _picked.Value.Warrior;
            if (_title != null)
                _title.text = Loc.F("Кого {0} возьмёт с собой", Loc.Name(leader.ShownName));

            var going = new List<string>();
            var staying = new List<string>();
            var rows = new List<(string Name, string Shown, string Line, bool Can, bool Goes)>();

            foreach (var m in SquadRoster.Members)
            {
                if (m.IsAway || m.Name == leader.DisplayName) continue;

                var w = Find(m.Name);
                if (w == null || w.IsDead) continue;

                bool can = SquadRoster.CanGo(m);
                bool goes = can && _party.Contains(m.Name);
                string shown = Loc.Name(w.ShownName);
                (goes ? going : staying).Add(shown);

                rows.Add((m.Name, shown, PartyLine(m, w, can, goes), can, goes));
            }

            float room = _rows == null ? 0f : _rows.rect.height;
            int count = rows.Count + 1;
            float step = room > 1f ? Mathf.Min(78f, room / count) : 78f;
            float height = Mathf.Max(30f, step - 8f);

            float y = 0f;
            var back = Plate("Назад", y, height, true, Ungather);   // ключ: имя объекта, игрок его не видит
            Label(back, Loc.T("← Выбрать другого старшего"), 22, new Vector2(18f, -8f), 30f, true);
            _spawned.Add(back.gameObject);
            y -= step;

            foreach (var r in rows)
            {
                string name = r.Name;
                var plate = Plate(name, y, height, r.Goes, r.Can ? () => Toggle(name) : (System.Action)null);
                Label(plate, r.Shown, 26, new Vector2(18f, -8f), 32f, r.Goes);
                Label(plate, r.Line, 18, new Vector2(18f, -Mathf.Min(38f, height * 0.55f)), 24f, r.Goes);
                _spawned.Add(plate.gameObject);
                y -= step;
            }

            int total = _party.Count + 1;
            string leaderName = Loc.Name(leader.ShownName);

            if (_detail != null)
            {
                string text = Loc.F("Ведёт {0}: {1}.", leaderName, Leadership.Describe(_picked.Value.Skill)) + Break
                            + (going.Count > 0
                                ? Grammar.Pick(leader.Gender,
                                    Loc.F("С ним пойдут: {0}.", string.Join(", ", going)),
                                    Loc.F("С ней пойдут: {0}.", string.Join(", ", going)))
                                : Grammar.Pick(leader.Gender,
                                    Loc.T("Пойдёт один: с ним никто."),
                                    Loc.T("Пойдёт одна: с ней никто."))) + Break
                            + Loc.F("С вами останутся: {0}.", string.Join(", ", staying));

                // Меньше, чем советовал Карган, — можно, но сказано, чем это грозит.
                if (total < _requiredSquad)
                    text += Break + (total == 1
                        ? Loc.F("Карган советовал послать {0}, а пойдёт один. "
                              + "Охраны от этого меньше не станет.", Leadership.Count(_requiredSquad))
                        : Loc.F("Карган советовал послать {0}, а пойдут {1}. "
                              + "Охраны от этого меньше не станет — рискуют все, кто пойдёт.",
                                Leadership.Count(_requiredSquad), Leadership.Collective(total)));
                if (!string.IsNullOrEmpty(warning)) text += Break + warning;
                _detail.text = text;
            }

            if (_confirm != null) _confirm.interactable = true;
            if (_confirmLabel != null)
                _confirmLabel.text = _party.Count == 0
                    ? Grammar.Pick(leader.Gender,
                        Loc.F("Отправить одного: {0}", leaderName),
                        Loc.F("Отправить одну: {0}", leaderName))
                    : Loc.F("Отправить отряд — ведёт {0}", leaderName);
        }

        /// <summary>
        /// Вторая строка: идёт ли, и что о нём стоит знать, решая, —
        /// главный грех словом, долг, братство. Без чисел.
        /// </summary>
        private static string PartyLine(SquadRoster.Member m, Warrior w, bool can, bool goes)
        {
            if (!can) return Loc.T(m.Unavailable);

            var parts = new List<string> { goes ? Loc.T("идёт") : Loc.T("остаётся с вами") };
            if (w.Soul != null) parts.Add(SoulData.GetSinName(w.Soul.Sin).ToLowerInvariant());
            if (m.UnpaidMissions > 0) parts.Add(Grammar.Pick(m.Gender, Loc.T("ему должны"), Loc.T("ей должны")));
            if (m.Brother) parts.Add(Loc.T("брат по оружию"));
            return string.Join(" · ", parts);
        }

        /// <summary>Воин в сцене по голому имени.</summary>
        private static Warrior Find(string name)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && w.DisplayName == name && w.Team == Team.Player) return w;
            return null;
        }

        private void Choose(Warrior warrior)
        {
            if (_done) return;
            _done = true;
            DropStage();
            Columns(true);

            SquadRoster.ChooseCommander(warrior.DisplayName);

            // Отряд действительно уходит. Без этого выбор старшего ничего
            // не менял бы в лагере, а в эпилоге возвращаться было бы некому:
            // «пятеро уходят, остаются Карган и трое» (пролог §4, сцена 2).
            SquadRoster.SendAway(warrior.DisplayName, _party);

            // Командир обязан остаться один: GetCommander берёт первого
            // попавшегося, и без этой уборки верность считалась бы к тому,
            // кого игрок не выбирал.
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.Team != Team.Player) continue;
                w.SetCommander(w.DisplayName == warrior.DisplayName);

                // Ушедшие исчезают из лагеря на глазах: игрок должен
                // увидеть, что отряд поредел, ещё до прихода Охотников.
                //
                // Удаляются, а не выключаются. Выключенный оставался в списке
                // своих у боевого менеджера и на учёте рамки: рамка там, где
                // он стоял, выделяла невидимого, а с набегом в самом лагере
                // (RaidEvent) охотники шли бы к пустому месту, и гибель
                // отряда не засчитывалась бы — невидимые «живы». Их правда
                // живёт в составе (SquadRoster), им возвращаться в эпилоге.
                if (SquadRoster.TryGet(w.DisplayName, out var m) && m.IsAway)
                    Destroy(w.gameObject);
            }

            Modal.Close(_panel);
            Core.GamePauseController.Instance?.Resume();

            var log = Object.FindFirstObjectByType<BattleLogUI>();
            // 47-TEXTS §1, п. 13: куда и надолго ли. Один — так и сказано.
            string who = Loc.Name(warrior.DisplayName);
            log?.Write(_party.Count == 0
                ? Grammar.Pick(warrior.Gender,
                    Loc.F("{0} ушёл к заставе один. Вернётся не скоро.", who),
                    Loc.F("{0} ушла к заставе одна. Вернётся не скоро.", who))
                : Grammar.Pick(warrior.Gender,
                    Loc.F("{0} повёл отряд к заставе. Вернутся не скоро.", who),
                    Loc.F("{0} повела отряд к заставе. Вернутся не скоро.", who)));
        }
    }
}
