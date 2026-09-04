using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

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
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Font _font;

        [Tooltip("Через сколько секунд игрового времени собирается совет. "
               + "По хронометражу пролога доля 2 идёт 1:00–1:30, совет — "
               + "с 1:30. Время здесь заглушка: правильный повод открыть "
               + "совет — три исполненных приказа подряд, но события "
               + "«приказ исполнен» в движке пока нет. Метод Open публичный "
               + "именно для того, чтобы его позвал будущий сценарий доли 2.")]
        [SerializeField] private float _openAfterSeconds = 30f;

        [Tooltip("Сколько человек требует миссия доли 3. Карган объясняет "
               + "это вслух: «там довольно опасно, нужно пятеро». Тот, чей "
               + "навык командования столько не уводит, в списке виден, но "
               + "не выбирается — и рядом написано почему.")]
        [SerializeField] private int _requiredSquad = 5;

        private readonly List<GameObject> _spawned = new();
        private bool _done;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);

            // Совет бывает один раз за пролог. Если игрок уже выбрал —
            // сцена перезапущена или это следующая доля, панель не нужна.
            if (!string.IsNullOrEmpty(SquadRoster.CommanderName)) { _done = true; return; }

            Invoke(nameof(Open), _openAfterSeconds);
        }

        public void Open()
        {
            if (_done || _panel == null) return;

            var options = FindOptions();
            if (options.Count == 0) return;

            Build(options);

            _panel.SetActive(true);
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

                float skill = 0f;
                string blocked = "";

                if (SquadRoster.TryGet(w.DisplayName, out var member))
                {
                    skill = member.Leadership;
                    blocked = member.Unavailable;
                }

                // Навыка не хватает на этот отряд — виден, но не выбирается.
                // Именно здесь рядовой упирается в требование доли 3, и это
                // единственное объяснение, зачем нужен опытный, которое
                // игроку не придётся читать в справке.
                if (string.IsNullOrEmpty(blocked) && !Leadership.CanLead(skill, _requiredSquad))
                    blocked = Leadership.Shortfall(skill, _requiredSquad);

                options.Add(new Option(w, skill, blocked));
            }

            // Если порог не проходит никто — совет запер бы игру насмерть:
            // панель ставит игру на паузу, а PrologueDirector ждёт выбранного
            // старшего, чтобы вывести отряд из лагеря. Такое случится, если
            // опытные не дожили. Тогда требование отступает, но запрет из
            // состава — нет: Карган телохранитель при любом раскладе.
            bool anyone = false;
            foreach (var o in options) if (o.CanChoose) { anyone = true; break; }

            if (!anyone)
            {
                Debug.LogWarning("[СОВЕТ] Никто не уводит отряд нужного размера. "
                               + "Требование снято, иначе пролог не двинется.");

                for (int i = 0; i < options.Count; i++)
                {
                    var o = options[i];
                    bool blockedByRoster =
                        SquadRoster.TryGet(o.Warrior.DisplayName, out var m)
                        && !string.IsNullOrEmpty(m.Unavailable);

                    if (!blockedByRoster) options[i] = new Option(o.Warrior, o.Skill, "");
                }
            }

            // Порядок обхода сцены не гарантирован, а список обязан
            // выглядеть одинаково при каждом запуске: ничего случайного.
            // Сперва те, кого выбрать можно, среди них — по навыку.
            options.Sort((a, b) =>
            {
                if (a.CanChoose != b.CanChoose) return a.CanChoose ? -1 : 1;
                if (!Mathf.Approximately(a.Skill, b.Skill)) return b.Skill.CompareTo(a.Skill);
                return string.CompareOrdinal(a.Warrior.DisplayName, b.Warrior.DisplayName);
            });

            return options;
        }

        private void Build(List<Option> options)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_title != null) _title.text = "Кого поставить старшим";

            float y = 0f;
            foreach (var option in options)
            {
                var row = Row(option, y);
                _spawned.Add(row);
                y -= 178f;
            }
        }

        private GameObject Row(Option option, float y)
        {
            var warrior = option.Warrior;

            var go = new GameObject(warrior.DisplayName, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 168f);
            rt.anchoredPosition = new Vector2(0f, y);

            var plate = go.AddComponent<Image>();
            plate.color = option.CanChoose
                ? new Color(0.10f, 0.09f, 0.08f, 0.92f)
                : new Color(0.07f, 0.06f, 0.06f, 0.80f);

            // Кнопка вешается только на тех, кого можно выбрать. Строка,
            // которая не сработает, не должна и нажиматься: игра про
            // настоящие решения не имеет права соврать на первом же.
            if (option.CanChoose)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = plate;
                button.onClick.AddListener(() => Choose(warrior));
            }

            Label(rt, warrior.DisplayName, 30, new Vector2(18f, -10f), 40f,
                option.CanChoose);

            // Навык командования словами, без шкалы: цифры игроку не
            // показываются нигде (docs/00-GDD.md §7). Для невыбираемого
            // здесь стоит причина — и она же объясняет, чего ему не хватает.
            Label(rt, option.CanChoose
                    ? Leadership.Describe(option.Skill)
                    : option.Blocked,
                20, new Vector2(18f, -48f), 26f, option.CanChoose);

            // Четыре строки настоящего пророчества. Последняя из них —
            // и есть завязка доли 6.
            Label(rt, TemperamentPredictor.Describe(warrior), 21,
                new Vector2(18f, -76f), 84f, option.CanChoose);

            return go;
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

        private void Choose(Warrior warrior)
        {
            if (_done) return;
            _done = true;

            SquadRoster.ChooseCommander(warrior.DisplayName);

            // Отряд действительно уходит. Без этого выбор старшего ничего
            // не менял бы в лагере, а в эпилоге возвращаться было бы некому:
            // «пятеро уходят, остаются Карган и трое» (пролог §4, сцена 2).
            SquadRoster.SendAway(warrior.DisplayName, _requiredSquad);

            // Командир обязан остаться один: GetCommander берёт первого
            // попавшегося, и без этой уборки верность считалась бы к тому,
            // кого игрок не выбирал.
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.Team != Team.Player) continue;
                w.SetCommander(w.DisplayName == warrior.DisplayName);

                // Ушедшие исчезают из лагеря на глазах: игрок должен
                // увидеть, что отряд поредел, ещё до прихода Охотников.
                if (SquadRoster.TryGet(w.DisplayName, out var m) && m.IsAway)
                    w.gameObject.SetActive(false);
            }

            if (_panel != null) _panel.SetActive(false);
            Core.GamePauseController.Instance?.Resume();

            var log = Object.FindFirstObjectByType<BattleLogUI>();
            log?.Write($"{warrior.DisplayName} принял отряд.");
        }
    }
}
