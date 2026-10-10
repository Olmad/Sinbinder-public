// Перевод: текст через Loc
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

using Sinbinder.Core;
namespace Sinbinder.UI
{
    /// <summary>
    /// Конец демо: кто вернулся.
    ///
    /// Последняя строка сценария 00-GDD.md §8 — «возвращение выжившего
    /// отряда, состав которого зависит от выбора командира в начале».
    /// Экран не подводит итог в очках и не ставит оценку: он просто
    /// называет тех, кто дошёл, и того, кого игрок поставил старшим.
    ///
    /// Морального счётчика в игре нет (05-BOUNDS), поэтому и здесь
    /// нет ни «хорошей», ни «плохой» концовки — есть список имён.
    /// </summary>
    public class DemoEndUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Text _body;

        /// <summary>
        /// «Начать сначала» и «Выйти из игры». До 26 сентября эпилог был
        /// без единой кнопки, а меню паузы на Esc не открывается, пока игра
        /// стоит: из собранной игры после «Демо окончено» выходили Alt+F4.
        /// Кнопки те же, что у экрана «Греховод пал», и ведут туда же.
        /// </summary>
        [SerializeField] private Button _again;
        [SerializeField] private Button _quit;

        /// <summary>
        /// «Остаться в склепе» — песочница после истории (docs/37-DEMO.md §0,
        /// автор 10 октября: «нужны вылазки после основной игры»). Есть,
        /// только когда отряд вернулся и стол вылазок включён (выключатель
        /// «вылазки»); иначе эпилог — конец демо, как прежде.
        /// </summary>
        [SerializeField] private Button _stay;

        /// <summary>Кто вернулся с вылазки из лагеря: им входить в склеп, если игрок останется.</summary>
        private readonly List<string> _returned = new();

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_again != null) _again.onClick.AddListener(GameOverUI.NewGame);
            if (_quit != null) _quit.onClick.AddListener(Application.Quit);
            if (_stay != null) _stay.onClick.AddListener(Stay);
        }

        private static void Caption(Button button, string text)
        {
            var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label != null) label.text = text;
        }

        public void Show(bool wiped)
        {
            if (_panel == null) return;

            if (_title != null)
                _title.text = wiped ? Loc.T("Отряд не вернулся.") : Loc.T("Отряд вернулся.");

            bool sandbox = !wiped && _stay != null && Crypt.CryptMap.Switch;
            if (_stay != null) _stay.gameObject.SetActive(sandbox);

            if (_body != null)
                _body.text = wiped ? Epitaph()
                    : (Roll() + Comeback()).TrimEnd() + "\n\n"
                      + (sandbox ? Loc.T("Здесь история демо кончается. Склеп остаётся вашим: "
                                       + "можно остаться и посылать отряды на вылазки.")
                                 : Loc.T("Демо окончено."));

            // Подписи кнопок вписал сборщик сцены — по-русски, на миг сборки.
            // Язык могли сменить; подписываем при показе.
            Caption(_again, Loc.T("Начать сначала"));
            Caption(_quit, Loc.T("Выйти из игры"));
            Caption(_stay, Loc.T("Остаться в склепе"));

            Fit();
            Modal.Open(_panel);

            // Отряд вернулся — «Возвращение лорда», главная тема: ей и место
            // в конце. Не вернулся — тишина.
            if (wiped) Audio.Music.Stop(3f);
            else Audio.Music.Play(Audio.Track.Epilogue, 3f);

            // Конец, а не пауза: обычную снял бы конец любого разговора
            // или «Продолжить» в меню паузы, и мир пошёл бы дальше под
            // эпилогом. Держит так же, как экран «Греховод пал»; снимает
            // только новая игра или загрузка.
            Object.FindFirstObjectByType<DialogueUI>()?.Cut();
            Core.GamePauseController.Instance?.Halt();
        }

        /// <summary>Поле под текстом — то же, что над заголовком, с запасом.</summary>
        private const float Bottom = 40f;

        /// <summary>Ряд кнопок снизу с полями: кнопка 84 на высоте 70 от края и зазор над ней.</summary>
        private const float Buttons = 112f + 32f;

        /// <summary>Ниже этого панель читается полоской, а не экраном.</summary>
        private const float Shortest = 260f;

        /// <summary>
        /// Панель по тексту. Состав отряда бывает от трёх строк до десятка,
        /// и панель, собранная под самый длинный случай, стояла наполовину
        /// пустой: снимки прохождения 25 сентября показали пустую нижнюю
        /// половину на обоих кадрах эпилога.
        /// </summary>
        private void Fit()
        {
            if (_body == null || !(_panel.transform is RectTransform panel)) return;

            var body = _body.rectTransform;
            float height = Mathf.Ceil(_body.preferredHeight);
            body.sizeDelta = new Vector2(body.sizeDelta.x, height);

            // Текст стоит от верха панели на своём месте, под заголовком;
            // под ним — ряд кнопок, если сцена их собрала.
            float top = -body.anchoredPosition.y;
            float below = _again != null || _quit != null ? Buttons : Bottom;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x,
                Mathf.Max(Shortest, top + height + below));
        }

        /// <summary>Кто дошёл и кто их вёл.</summary>
        private string Roll()
        {
            var sb = new StringBuilder();

            string commander = SquadRoster.CommanderName;
            if (!string.IsNullOrEmpty(commander))
                sb.AppendLine(Loc.F("Отряд вёл {0}.", Loc.Name(commander))).AppendLine();

            foreach (var m in SquadRoster.Members)
            {
                if (m.IsAway) continue;   // они возвращаются ниже, отдельно

                sb.Append(Loc.Name(m.Name));

                // Долг — единственное, что отряд уносит с собой к следующей
                // вылазке. Числа игрок не видит, только факт. «Ему» — по роду:
                // Женщине (основная игра) должны так же.
                if (m.UnpaidMissions > 0)
                    sb.Append(Grammar.Pick(m.Gender, Loc.T(" — ему всё ещё должны"), Loc.T(" — ей всё ещё должны")));
                else if (m.IsCommander) sb.Append(Loc.T(" — старший"));

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Возвращение отряда, ушедшего на доле 2, — закрытие демо.
        ///
        /// Игрок выбрал старшего полчаса назад, прочитав пророчество.
        /// Здесь ему возвращают счёт, и состав зависит от того самого
        /// выбора (00-GDD.md §8).
        ///
        /// Считает не эта панель и больше не таблица: вылазку проводит
        /// <see cref="Expedition"/> настоящим боем через тот же движок,
        /// что решает всё остальное. Правило пролога §2 — «ни одна
        /// постановочная сцена не показывает того, чего движок не мог бы
        /// решить сам» — выполняется теперь и здесь.
        /// </summary>
        private string Comeback()
        {
            var away = new List<SquadRoster.Member>();
            foreach (var m in SquadRoster.Away) away.Add(m);

            _returned.Clear();
            if (away.Count == 0) return "";

            // Командир идёт первым: он вернулся, если вернулся хоть кто-то.
            away.Sort((a, b) =>
            {
                if (a.IsCommander != b.IsCommander) return a.IsCommander ? -1 : 1;
                return string.CompareOrdinal(a.Name, b.Name);
            });

            var leader = away[0];
            var survivors = Expedition.Resolve(away);

            // Вернувшиеся — снова в составе: им платить и их посылать, если
            // игрок останется в склепе. Не вернувшиеся из него вычеркнуты;
            // старший вылазки больше не старший — вылазка кончилась.
            SquadRoster.ComeBack(survivors);
            _returned.AddRange(survivors);

            var sb = new StringBuilder();

            // Не вернулся никто — это законный исход настоящего боя,
            // а не сбой. Таблица такого не допускала: она всегда
            // возвращала хотя бы одного, и это было обещание,
            // которого движок не давал.
            if (survivors.Count == 0)
            {
                sb.AppendLine().AppendLine(Loc.T("Из ушедших не вернулся никто."));
                sb.AppendLine();
                sb.Append(Homecoming.Story(leader.Sin, leader.Gender));
                return sb.ToString();
            }

            sb.AppendLine().AppendLine(Loc.T("В склеп входит отряд, ушедший из лагеря."));
            sb.AppendLine();

            foreach (var name in survivors)
            {
                sb.Append(Loc.Name(name));
                if (name == leader.Name && leader.IsCommander)
                    sb.Append(Grammar.Pick(leader.Gender, Loc.T(" — вёл их"), Loc.T(" — вела их")));
                sb.AppendLine();
            }

            if (survivors.Count < away.Count)
            {
                sb.AppendLine();
                sb.Append(Homecoming.Story(leader.Sin, leader.Gender));
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Остаться в склепе: эпилог уходит, мир идёт, вернувшиеся входят,
        /// стол вылазок встаёт. Состав снимается со сцены заново — поднятые
        /// в мастерской попадают в него только так, а без этого их нельзя
        /// было бы послать.
        /// </summary>
        private void Stay()
        {
            Modal.Close(_panel);
            Core.GamePauseController.Instance?.Unhalt();
            Audio.Music.Play(Audio.Track.Crypt, 3f);

            Object.FindFirstObjectByType<PrologueCampSpawner>()?.Arrive(_returned);
            SquadRoster.Remember(Crypt.MissionBoard.Squad());
            Crypt.CryptMap.Open();

            Object.FindFirstObjectByType<BattleLogUI>()?.Write(Loc.T(
                "Склеп ваш. Шар у правой стены показывает дороги. Первая ведёт "
              + "к соляному обозу: охраны при нём двое, а везут серебро."));
        }

        private string Epitaph()
            => Loc.T("Никто не дошёл до склепа.\n\nДуши разойдутся Некроэфиром,"
             + " и помнить о них будет некому.\n\nДемо окончено.");
    }
}
