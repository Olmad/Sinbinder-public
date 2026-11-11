// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.UI
{
    /// <summary>
    /// Гнёзда сохранений. Открывается на F9.
    ///
    /// В спокойной игре гнёзд несколько: записать, вернуться, выбрать
    /// из чего. В игре с последствиями гнездо одно, оно обновляется,
    /// и вернуться нельзя — весь список сводится к одной строке и
    /// объяснению, почему её нельзя загрузить.
    ///
    /// <b>Гнёзда называются тем, что в них лежит</b>, а не номером:
    /// «Склеп · девятеро · трое в долгу». Номер гнезда — цифра, а цифр
    /// игрок не видит нигде, включая меню.
    /// </summary>
    public class SaveSlotsPanel : MonoBehaviour
    {
        [SerializeField] private KeyCode _key = KeyCode.F9;
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Text _title;
        [SerializeField] private Font _font;

        private readonly List<GameObject> _spawned = new();
        private bool _open;
        private bool _loading;
        private string _status;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        void Update()
        {
            // Конец игры: гнёзда — через «Начать сначала» (QuickSave, там же почему).
            if (GamePauseController.Instance != null && GamePauseController.Instance.Halted)
            {
                if (Input.GetKeyDown(_key)) Say(Loc.T("Игра окончена. Начните сначала — там и запись."));
                return;
            }

            if (Input.GetKeyDown(_key)) Toggle();
            else if (_open && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void Toggle()
        {
            if (_open) Close(); else Open();
        }

        private void Open()
        {
            _open = true;
            _loading = false;
            _status = null;
            if (_panel != null) _panel.SetActive(true);
            GamePauseController.Instance?.Pause();
            Draw();
        }

        private void Close()
        {
            _open = false;
            _status = null;
            if (_panel != null) _panel.SetActive(false);
            GamePauseController.Instance?.Resume();
        }

        private void Draw()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_title != null)
                _title.text = !string.IsNullOrEmpty(_status)
                    ? _status
                    : (_loading
                        ? Loc.T("Вернуться к записанному.  F9 или Esc — закрыть")
                        : Loc.T("Куда записать.  F9 или Esc — закрыть"));

            float y = 0f;

            // Переключатель. В игре с последствиями его нет: возвращаться
            // некуда, и предлагать это — обманывать.
            if (Commitment.CanLoad)
            {
                bool toLoad = !_loading;
                _spawned.Add(Row(y, toLoad ? Loc.T("Вернуться к записанному") : Loc.T("Записать"),
                    "", () => { _loading = toLoad; _status = null; Draw(); }));
                y -= RowHeight * 1.4f;
            }
            else
            {
                _spawned.Add(Row(y, Commitment.WhyNoLoad, "", null));
                y -= RowHeight * 1.4f;
            }

            foreach (var slot in SaveSystem.Available())
            {
                var name = slot;
                var save = SaveSystem.Read(SaveSystem.PathOf(slot));
                bool has = save != null;
                // An existing but unreadable file is not an empty slot.
                // Never invite the player to load it as if it were valid.
                bool unreadable = !has && SaveSystem.Exists(SaveSystem.PathOf(slot));

                string what = has
                    ? (string.IsNullOrEmpty(save.Label) ? Loc.T("Запись") : save.Label)
                    : (unreadable ? Loc.T("Запись не читается") : Loc.T("Пусто"));

                // Загружать нечего — строка есть, но не нажимается:
                // пустое гнездо должно быть видно, чтобы в него записали.
                System.Action act = _loading
                    ? (has ? () => { if (Load(name)) Close(); else Draw(); } : (System.Action)null)
                    // Do not silently overwrite an unreadable existing save.
                    // It may be the player's only recoverable copy.
                    : (unreadable ? (System.Action)null : () => { _status = Save(name); Draw(); });

                _spawned.Add(Row(y, Loc.T(name), what, act));   // имя гнезда — ключ файла, показ — переводом
                y -= RowHeight * 1.6f;
            }
        }

        private static string Save(string slot)
        {
            bool ok = SaveSystem.Write(SaveSystem.Snapshot(), SaveSystem.PathOf(slot));
            string message = ok ? Loc.F("Записано в «{0}».", Loc.T(slot)) : Loc.T("Записать не вышло.");
            Say(message);
            return message;
        }

        private bool Load(string slot)
        {
            if (!Commitment.CanLoad) { _status = Commitment.WhyNoLoad; Say(_status); return false; }

            var save = SaveSystem.Read(SaveSystem.PathOf(slot));
            if (save == null)
            {
                _status = Loc.T("Запись не прочиталась. Выберите другую запись или начните заново.");
                Say(_status);
                return false;
            }

            bool loaded = SaveSystem.ReturnTo(save);
            _status = loaded
                ? Loc.F("Вернулись к «{0}».", Loc.T(slot))
                : Loc.T("Не удалось восстановить запись. Выберите другую запись или начните заново.");
            Say(_status);
            return loaded;
        }

        private static void Say(string line)
            => Object.FindFirstObjectByType<BattleLogUI>()?.Write(line);

        private const float RowHeight = 54f;

        private GameObject Row(float y, string title, string second, System.Action onClick)
        {
            var go = new GameObject(title, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, RowHeight * 1.4f);
            rt.anchoredPosition = new Vector2(0f, y);

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.10f, 0.09f, 0.08f, onClick == null ? 0.35f : 0.88f);

            if (onClick != null)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = plate;
                button.onClick.AddListener(() => onClick());
            }

            Label(rt, title, 26, -8f, onClick != null);
            if (!string.IsNullOrEmpty(second)) Label(rt, second, 20, -40f, false);

            return go;
        }

        private void Label(RectTransform parent, string text, int size, float y, bool bright)
        {
            var go = new GameObject("Строка", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(18f, 0f);
            rt.offsetMax = new Vector2(-18f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 34f);
            rt.anchoredPosition = new Vector2(18f, y);

            var label = go.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.alignment = TextAnchor.UpperLeft;
            label.color = bright
                ? new Color(0.91f, 0.89f, 0.85f)
                : new Color(0.62f, 0.59f, 0.55f);
            label.raycastTarget = false;
            label.text = text;
        }
    }
}
