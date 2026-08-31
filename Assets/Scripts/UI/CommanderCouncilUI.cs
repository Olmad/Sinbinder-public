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

            var candidates = FindCandidates();
            if (candidates.Count == 0) return;

            Build(candidates);

            _panel.SetActive(true);
            Core.GamePauseController.Instance?.Pause();
        }

        /// <summary>
        /// Кандидаты — живые свои, помеченные в составе отряда.
        /// Ищем среди воинов сцены, а не в списке: пророчество надо
        /// спрашивать у настоящей души со всеми смещениями оболочки.
        /// </summary>
        private List<Warrior> FindCandidates()
        {
            var found = new List<Warrior>();
            var names = new HashSet<string>();

            foreach (var m in SquadRoster.Members)
                if (m.IsCandidate) names.Add(m.Name);

            foreach (var w in Object.FindObjectsOfType<Warrior>())
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;
                if (!names.Contains(w.DisplayName)) continue;
                found.Add(w);
            }

            // Порядок обхода сцены не гарантирован, а список обязан
            // выглядеть одинаково при каждом запуске: ничего случайного.
            found.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return found;
        }

        private void Build(List<Warrior> candidates)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_title != null) _title.text = "Кого поставить старшим";

            float y = 0f;
            foreach (var warrior in candidates)
            {
                var row = Row(warrior, y);
                _spawned.Add(row);
                y -= 150f;
            }
        }

        private GameObject Row(Warrior warrior, float y)
        {
            var go = new GameObject(warrior.DisplayName, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 140f);
            rt.anchoredPosition = new Vector2(0f, y);

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.10f, 0.09f, 0.08f, 0.92f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(() => Choose(warrior));

            Label(rt, warrior.DisplayName, 30, new Vector2(18f, -10f), 40f);

            // Четыре строки настоящего пророчества. Последняя из них —
            // и есть завязка доли 6.
            Label(rt, TemperamentPredictor.Describe(warrior), 21,
                new Vector2(18f, -52f), 84f);

            return go;
        }

        private void Label(RectTransform parent, string text, int size, Vector2 offset, float height)
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
            label.color = new Color(0.91f, 0.89f, 0.85f);
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

            // Командир обязан остаться один: GetCommander берёт первого
            // попавшегося, и без этой уборки верность считалась бы к тому,
            // кого игрок не выбирал.
            foreach (var w in Object.FindObjectsOfType<Warrior>())
            {
                if (w == null || w.Team != Team.Player) continue;
                w.SetCommander(w.DisplayName == warrior.DisplayName);
            }

            if (_panel != null) _panel.SetActive(false);
            Core.GamePauseController.Instance?.Resume();

            var log = Object.FindObjectOfType<BattleLogUI>();
            log?.Write($"{warrior.DisplayName} принял отряд.");
        }
    }
}
