using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.UI
{
    /// <summary>
    /// Пауза. Esc, когда ничего другого не открыто.
    ///
    /// <b>Почему «когда ничего другого».</b> На Esc уже отвечают совет,
    /// карта вылазок, гнёзда сохранений и настройка прозрачности —
    /// каждая закрывает себя. Если бы пауза тоже хватала Esc, она
    /// открывалась бы поверх закрывающейся панели, и игрок нажимал бы
    /// дважды, не понимая, почему.
    ///
    /// Признак простой и уже есть: открытая панель ставит игру на паузу.
    /// Значит «игра не на паузе» и означает «ничего не открыто».
    ///
    /// Пауза ничего не делает сама: она дверь к тому, что уже написано.
    /// Заводить здесь второй список сохранений или вторую настройку
    /// прозрачности значило бы развести их с первыми на первой правке.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Text _title;
        [SerializeField] private Font _font;

        private readonly List<GameObject> _spawned = new();
        private bool _open;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (_open) { Close(); return; }

            // Пауза открывается только на свободный Esc. Игра уже
            // на паузе — значит Esc принадлежит тому, кто её поставил.
            var pause = GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return;

            Open();
        }

        private void Open()
        {
            _open = true;
            if (_panel != null) _panel.SetActive(true);
            GamePauseController.Instance?.Pause();
            Draw();
        }

        private void Close()
        {
            _open = false;

            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_panel != null) _panel.SetActive(false);
            GamePauseController.Instance?.Resume();
        }

        private void Draw()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_title != null)
                _title.text = Commitment.On
                    ? "Пауза.  Игра с обязательством — переиграть нельзя"
                    : "Пауза";

            float y = 0f;

            Add(ref y, "Продолжить", "", Close);

            Add(ref y, "Записать", Where(), () =>
            {
                bool ok = SaveSystem.Write(SaveSystem.Snapshot(), SaveSystem.QuickPath);
                Say(ok ? "Записано." : "Записать не вышло.");
                Draw();
            });

            // Загрузка в ответственной игре не прячется, а объясняется:
            // отсутствие строки читается как забытая возможность,
            // а запрет с причиной — как уговор.
            if (Commitment.CanLoad)
            {
                bool has = SaveSystem.Exists(SaveSystem.QuickPath);

                Add(ref y, "Вернуться к записанному",
                    has ? Where() : "Записи нет",
                    has ? () => Load() : (System.Action)null);
            }
            else
            {
                Add(ref y, "", Commitment.WhyNoLoad, null);
            }

            y -= MenuRows.Height * 0.5f;

            Add(ref y, "Гнёзда сохранений", "F9", null);
            Add(ref y, "Что игра объясняет", "O", null);
            Add(ref y, "Сменить взгляд", "V", null);
        }

        private void Add(ref float y, string title, string second, System.Action act)
        {
            _spawned.Add(MenuRows.Row(_rows, _font, y, title, second, act));
            y -= string.IsNullOrEmpty(second) ? MenuRows.Height : MenuRows.Height * 1.5f;
        }

        private void Load()
        {
            var save = SaveSystem.Read(SaveSystem.QuickPath);

            if (!SaveSystem.Restore(save))
            {
                Say("Эта запись не от нынешней игры.");
                return;
            }

            Say("Вернулись к записанному.");
            Close();
        }

        /// <summary>Что лежит в гнезде, куда пишет пауза.</summary>
        private static string Where()
        {
            var save = SaveSystem.Read(SaveSystem.QuickPath);
            if (save == null) return "Гнездо пустое";

            return string.IsNullOrEmpty(save.Label) ? "Запись" : save.Label;
        }

        private static void Say(string line)
            => Object.FindFirstObjectByType<BattleLogUI>()?.Write(line);
    }
}
