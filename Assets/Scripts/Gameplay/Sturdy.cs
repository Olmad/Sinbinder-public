// Assets/Scripts/Gameplay/Sturdy.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Прочность вещи склепа «Перетягивания» — трона и двери (54-TUG.md: «конец партии — смерть
    /// вражеских Греховодов или разрушение их тронов»; автор, 27.10: «нельзя посмотреть трон на
    /// количество здоровья, то же самое с дверью склепа»).
    ///
    /// Не <see cref="Damageable"/>: тот — тело воина, его считает бой (CombatManager), и трон
    /// встал бы в строй рядом с отрядом. Здесь — только запас и удар; ломать его будут охотники и
    /// чужие отряды (шаги 3–4).
    ///
    /// Что видно при наведении (<see cref="Describe"/>, UI.WarriorTooltipUI): своё — числом, им
    /// Греховод владеет (как кошелём); чужое — словом, без цифр.
    /// </summary>
    public class Sturdy : MonoBehaviour
    {
        [SerializeField] private string _title = "";
        [SerializeField] private float _max = 1000f;
        [SerializeField] private float _hp = -1f;

        /// <summary>Чей склеп — номер стороны карты (места в лобби, MapSession.Seats); −1 — ничей.</summary>
        [SerializeField] private int _side = -1;

        public string Title => _title;
        public float Max => _max;
        public float HP => _hp < 0f ? _max : _hp;
        public int Side => _side;
        public bool Broken => HP <= 0f;

        /// <summary>Своё ли — склеп стороны, за которую играет игрок.</summary>
        public bool Mine => _side >= 0 && _side < MapSession.Seats.Count
                            && MapSession.Seats[_side] == MapSession.Seat.Player;

        /// <summary>Разрушено: трон — сторона проиграла, дверь — склеп открыт настежь.</summary>
        public static event System.Action<Sturdy> Broke;

        public void Configure(string title, float max, int side)
        {
            _title = title;
            _max = Mathf.Max(1f, max);
            _hp = _max;
            _side = side;
        }

        public void TakeDamage(float damage)
        {
            if (Broken || damage <= 0f) return;
            _hp = Mathf.Max(0f, HP - damage);
            if (!Broken) return;
            Debug.Log($"[СКЛЕП] разрушено: {_title}");
            Broke?.Invoke(this);
        }

        /// <summary>Подпись при наведении: что это, чьё и сколько в нём осталось.</summary>
        public string Describe()
        {
            string whose = _side < 0 ? "" : Mine ? Loc.T("ваш склеп") : Loc.F("склеп стороны {0}", _side + 1);
            // Имя уже переведено: его даёт MapLoader через Loc.F.
            string head = string.IsNullOrEmpty(whose) ? _title : Loc.F("{0} — {1}", _title, whose);
            if (Mine) return head + "\n" + Loc.F("Прочность: {0} из {1}", Mathf.CeilToInt(HP), Mathf.RoundToInt(_max));
            return head + "\n" + State();
        }

        /// <summary>Чужое — словом: сколько осталось, видно и так, а цифра — не наша.</summary>
        private string State()
        {
            float k = HP / _max;
            if (k <= 0f) return Loc.T("Разрушено");
            if (k < 0.25f) return Loc.T("Еле держится");
            if (k < 0.6f) return Loc.T("В трещинах");
            if (k < 0.95f) return Loc.T("Побито");
            return Loc.T("Цело");
        }
    }
}
