// Assets/Scripts/UI/PlateSight.cs
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// На что нацелен игрок — и чья подпись поэтому видна.
    ///
    /// Один на сцену, а не по компоненту на табличку: луч пускается
    /// один раз за кадр, а не по разу на каждый предмет в комнате.
    ///
    /// Показывается подпись строкой у нижней панели, а не над самим
    /// предметом. Так она всегда в одном месте экрана: её не надо искать
    /// взглядом, она не наезжает на панель выделенного воина и не пляшет
    /// вместе с предметом, к которому подходишь.
    ///
    /// Куда целятся, зависит от взгляда, и это не мелочь:
    /// в первом лице курсора нет вовсе, целится середина экрана —
    /// то есть голова. В тактическом целится мышь. Спрашивать
    /// <c>Input.mousePosition</c> в обоих случаях было бы неверно:
    /// при захваченном курсоре он стоит там, где его заперли.
    ///
    /// Видна ровно одна подпись. Две разом — это уже та самая стена
    /// текста, от которой всё и затевалось.
    /// </summary>
    public class PlateSight : MonoBehaviour
    {
        [Tooltip("Дальше этого надписи не читаются: подпись, которую видно "
               + "через всю комнату, всё равно что подпись всегда.")]
        [SerializeField] private float _range = 9f;

        [Tooltip("Сколько уровней вверх искать хозяина таблички от того, "
               + "во что попал луч. Коллайдер обычно на самом предмете "
               + "или на его примитиве, глубже трёх не бывает.")]
        [SerializeField] private int _levels = 4;

        [Tooltip("Строка, в которой показывается подпись. Стоит над панелью "
               + "выделенного воина: подпись всегда в одном месте экрана, "
               + "её не надо искать и она ни на что не наезжает.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _line;

        private Camera _cam;
        private WorldPlate _lit;

        void Start()
        {
            _cam = Camera.main;

            if (_panel != null) _panel.SetActive(false);

            if (_line == null)
                Debug.LogWarning("[ТАБЛИЧКИ] Строка подписи не связана в сцене: "
                               + "показывать надписи будет негде. Пересоберите сцены.");

            if (_cam == null)
            {
                Debug.LogWarning("[ТАБЛИЧКИ] Камеры в сцене нет: целиться нечем, "
                               + "надписи останутся погашенными.");
                enabled = false;
            }
        }

        void Update()
        {
            if (_cam == null) return;

            var found = Aimed();

            if (found == _lit) return;

            _lit = found;
            Draw();
        }

        private void Draw()
        {
            string what = _lit != null ? _lit.Text : string.Empty;
            bool show = !string.IsNullOrWhiteSpace(what);

            if (_line != null) _line.text = what;
            if (_panel != null) _panel.SetActive(show);
        }

        void OnDisable()
        {
            // Гаснем за собой: компонент выключают вместе со сценой,
            // и оставленная строка пережила бы её.
            _lit = null;
            if (_panel != null) _panel.SetActive(false);
        }

        private WorldPlate Aimed()
        {
            var view = _cam.GetComponent<Gameplay.RTS_Camera>();
            bool firstPerson = view != null && view.FirstPersonNow;

            Vector3 from = firstPerson
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : Input.mousePosition;

            var ray = _cam.ScreenPointToRay(from);

            return Physics.Raycast(ray, out var hit, _range)
                ? PlateOf(hit.collider.transform)
                : null;
        }

        /// <summary>
        /// Чья это табличка. Луч попадает в коллайдер предмета, а надпись
        /// висит у предмета в детях, — поэтому идём вверх, пока не найдём
        /// того, у кого она есть.
        /// </summary>
        private WorldPlate PlateOf(Transform hit)
        {
            var t = hit;

            for (int i = 0; i < _levels && t != null; i++, t = t.parent)
            {
                var plate = t.GetComponentInChildren<WorldPlate>(true);
                if (plate != null) return plate;
            }

            return null;
        }
    }
}
