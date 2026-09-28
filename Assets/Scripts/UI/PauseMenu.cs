// Assets/Scripts/UI/PauseMenu.cs
// Перевод: текст через Loc
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

            // Этот Esc закрыл консоль (Dev.CheatConsole) — меню он не открывает.
            if (Dev.CheatConsole.OwnsEscape) return;

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
                    ? Loc.T("Пауза.  Игра с обязательством — переиграть нельзя")
                    : Loc.T("Пауза");

            float y = 0f;

            Add(ref y, Loc.T("Продолжить"), "", Close);

            Add(ref y, Loc.T("Записать"), Where(), () =>
            {
                bool ok = SaveSystem.Write(SaveSystem.Snapshot(), SaveSystem.QuickPath);
                Say(ok ? Loc.T("Записано.") : Loc.T("Записать не вышло."));
                Draw();
            });

            // Загрузка в ответственной игре не прячется, а объясняется:
            // отсутствие строки читается как забытая возможность,
            // а запрет с причиной — как уговор.
            if (Commitment.CanLoad)
            {
                bool has = SaveSystem.Exists(SaveSystem.QuickPath);

                Add(ref y, Loc.T("Вернуться к записанному"),
                    has ? Where() : Loc.T("Записи нет"),
                    has ? () => Load() : (System.Action)null);
            }
            else
            {
                Add(ref y, "", Commitment.WhyNoLoad, null);
            }

            y -= MenuRows.Height * 0.5f;

            Add(ref y, Loc.T("Гнёзда сохранений"), "F9", null);
            Add(ref y, Loc.T("Что игра объясняет"), "O", null);
            Add(ref y, Loc.T("Сменить взгляд"), "V", null);

            // Настройки — только те, которых больше нигде нет. Ясность
            // живёт своей панелью (строка выше — дверь к ней), сохранения
            // своими гнёздами. Громкости нет потому, что нет звука:
            // рычаг для несуществующего был бы мишурой.
            y -= MenuRows.Height * 0.5f;

            Add(ref y, Loc.T("Экран"), Core.Preferences.ScreenName(), () =>
            {
                Core.Preferences.ToggleScreen();
                Draw();
            });

            Add(ref y, Loc.T("Поворот взгляда"), Core.Preferences.SensitivityName(), () =>
            {
                Core.Preferences.CycleSensitivity();
                Draw();
            });

            // Язык — названием на самом языке (LocSetup.Languages): включивший
            // чужой по ошибке узнает свой, не читая чужого.
            Add(ref y, Loc.T("Язык"), Core.LocSetup.CurrentName(), () =>
            {
                Core.LocSetup.Cycle();
                Draw();
            });

            // Выйти из игры было нечем: Application.Quit не звался
            // во всём проекте ни разу. В редакторе это незаметно — там
            // выход всегда есть кнопкой Stop, — а человеку, которому
            // прислали сборку, оставался Alt+F4. Меню без выхода
            // читается как незаконченное, и читается верно.
            //
            // Стоит последней и отделена пустой строкой: случайно
            // нажать выход — потерять проход.
            y -= MenuRows.Height * 0.5f;
            Add(ref y, Loc.T("Выйти из игры"), Commitment.On
                    ? Loc.T("Игра с обязательством — записывайте перед выходом")
                    : "", Quit);
        }

        /// <summary>
        /// Закрыть игру. В редакторе ничего не делает — так задумано
        /// самим Unity, и подменять это чем-то своим не нужно: проверять
        /// выход всё равно надо на сборке.
        /// </summary>
        private static void Quit() => Application.Quit();

        private void Add(ref float y, string title, string second, System.Action act)
        {
            _spawned.Add(MenuRows.Row(_rows, _font, y, title, second, act));
            y -= string.IsNullOrEmpty(second) ? MenuRows.Height : MenuRows.Height * 1.5f;
        }

        private void Load()
        {
            var save = SaveSystem.Read(SaveSystem.QuickPath);

            if (!SaveSystem.ReturnTo(save))
            {
                Say(Loc.T("Эта запись не от нынешней игры."));
                return;
            }

            Say(Loc.T("Вернулись к записанному."));
            Close();
        }

        /// <summary>Что лежит в гнезде, куда пишет пауза.</summary>
        private static string Where()
        {
            var save = SaveSystem.Read(SaveSystem.QuickPath);
            if (save == null) return Loc.T("Гнездо пустое");

            return string.IsNullOrEmpty(save.Label) ? Loc.T("Запись") : save.Label;
        }

        private static void Say(string line)
            => Object.FindFirstObjectByType<BattleLogUI>()?.Write(line);
    }
}
