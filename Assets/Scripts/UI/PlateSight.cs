// Assets/Scripts/UI/PlateSight.cs
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// На что нацелен игрок — и чья надпись поэтому горит.
    ///
    /// Один на сцену, а не по компоненту на табличку: луч пускается
    /// один раз за кадр, а не по разу на каждый предмет в комнате.
    ///
    /// Куда целятся, зависит от взгляда, и это не мелочь:
    /// в первом лице курсора нет вовсе, целится середина экрана —
    /// то есть голова. В тактическом целится мышь. Спрашивать
    /// <c>Input.mousePosition</c> в обоих случаях было бы неверно:
    /// при захваченном курсоре он стоит там, где его заперли.
    ///
    /// Горит ровно одна табличка. Две подписи разом — это уже та самая
    /// стена текста, от которой всё и затевалось.
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

        private Camera _cam;
        private WorldPlate _lit;

        void Start()
        {
            _cam = Camera.main;

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

            if (_lit != null) _lit.Show(false);
            _lit = found;
            if (_lit != null) _lit.Show(true);
        }

        void OnDisable()
        {
            // Гаснем за собой: компонент выключают вместе со сценой,
            // и оставленная гореть надпись пережила бы её.
            if (_lit != null) _lit.Show(false);
            _lit = null;
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
