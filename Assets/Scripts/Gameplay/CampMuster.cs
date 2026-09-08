// Assets/Scripts/Gameplay/CampMuster.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Сбор отряда к столу перед советом.
    ///
    /// Заведён под задачу «мелкие дешёвые отказы в лагере». Продукт демо —
    /// отказ, но отказ читается характером только на фоне послушания:
    /// первый же неисполненный приказ, если он единственный, читается
    /// как сломанный ИИ. До сих пор игрок за весь лагерь отдавал один
    /// приказ провожатому, а следующий — уже в бою, где отказ стоит крови.
    ///
    /// Теперь у него есть повод отдать полдюжины приказов подряд там,
    /// где отказ не стоит ничего. Карган просит покликать отряд к столу;
    /// игрок выделяет и посылает, кого хочет; кто-то идёт, кто-то нет.
    /// По замеру стенда в спокойном лагере отказывают четверо из девяти
    /// (docs/12-BALANCE.md, «Лагерь под приказом»), то есть несколько
    /// отказов игрок увидит почти наверняка — и ни один ему не навредит.
    ///
    /// <b>Совет не заперт за сбором.</b> Ни при каких условиях: шар
    /// открывается по подходу игрока, как и раньше. Условие «сначала
    /// приведи N воинов» превратило бы упрямство отряда в тупик,
    /// а на этом проект уже стоял намертво в сцене побега.
    /// Сбор — необязательный повод, а не ворота.
    ///
    /// Единственное, что он делает помимо повода, — <b>называет итог</b>.
    /// Когда совет открывается, Карган говорит, кто подошёл, а кто нет.
    /// Это вторая ступень прозрачности, приложенная к тому, что игрок
    /// только что сделал руками.
    /// </summary>
    public class CampMuster : MonoBehaviour
    {
        [Tooltip("Через сколько секунд после выхода из палатки просить сбор.")]
        [SerializeField] private float _asksAfter = 14f;

        [Tooltip("Насколько близко к столу нужно подойти, чтобы считаться пришедшим.")]
        [SerializeField] private float _gathered = 6f;

        [Tooltip("Стол совета. Пусто — найдём по хрустальному шару.")]
        [SerializeField] private Transform _table;

        private bool _asked;
        private bool _reported;

        /// <summary>
        /// Кто где стоял в тот миг, когда попросили собраться.
        ///
        /// Без этого снимка итог был бы неправдой: воины расставлены
        /// кругом у костра, и кто-то оказывается у стола сам по себе.
        /// Считать его «пришедшим» значит приписать игроку послушание,
        /// которого он не добивался, — а игра, которая продаёт честные
        /// причины, врать не может даже в похвале.
        /// </summary>
        private readonly Dictionary<Warrior, bool> _wasFar = new Dictionary<Warrior, bool>();

        /// <summary>
        /// Кому игрок действительно отдал приказ после просьбы.
        ///
        /// По одному положению воинов этого не узнать, а разница
        /// существенная: «никто не подошёл, потому что вы никого
        /// не звали» и «никто не подошёл, хотя вы звали всех» —
        /// две разные строки, и вторая и есть продукт демо.
        /// </summary>
        private readonly HashSet<Warrior> _ordered = new HashSet<Warrior>();

        void Start() => StartCoroutine(Routine());

        private IEnumerator Routine()
        {
            yield return new WaitForSecondsRealtime(_asksAfter);
            Ask();
        }

        void Update()
        {
            if (_reported || !_asked) return;

            // Приказ живёт недолго: воин его исполняет или отбрасывает,
            // и к моменту совета от него не остаётся следа. Поэтому
            // замечаем его, пока он есть.
            foreach (var pair in _wasFar)
                if (pair.Key != null && pair.Key.HasCommand)
                    _ordered.Add(pair.Key);

            // Итог подводится один раз, в тот момент, когда совет открылся:
            // раньше — рано, позже — игрок уже смотрит в шар.
            if (!CrystalBall.Raised) return;

            _reported = true;
            Report();
        }

        private void Ask()
        {
            _asked = true;

            var table = Table();
            if (table != null)
                foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                {
                    if (w == null || w.IsDead || w.Team != Team.Player) continue;

                    _wasFar[w] = CampFocus.GroundDistance(w.transform.position,
                                                          table.position) > _gathered;
                }

            var keeper = Bodyguard();
            string who = keeper != null ? keeper : "Карган Старый Ворон";

            Log($"{who}: «Владыка, покличьте их к столу. Совет любит, "
              + "когда все на виду».");
        }

        /// <summary>
        /// Кто подошёл, а кто нет. Имён в коде нет: и телохранитель,
        /// и остальные находятся по составу отряда.
        /// </summary>
        private void Report()
        {
            var table = Table();
            if (table == null) return;

            var came = new List<string>();
            var stayed = new List<string>();

            foreach (var pair in _wasFar)
            {
                var w = pair.Key;
                if (w == null || w.IsDead) continue;

                // Кто и так стоял у стола, в счёт не идёт: его никто
                // никуда не звал.
                if (!pair.Value) continue;

                float d = CampFocus.GroundDistance(w.transform.position, table.position);
                if (d <= _gathered) { came.Add(w.DisplayName); continue; }

                // Остался — но упрекать за это можно только того, кого
                // звали. Прочие просто стояли, где стояли.
                if (_ordered.Contains(w)) stayed.Add(w.DisplayName);
            }

            // Никого не звали и никто не пришёл — молчим. Отчёт о том,
            // чего игрок не делал, прозвучал бы упрёком ни за что.
            if (came.Count == 0 && stayed.Count == 0) return;

            var keeper = Bodyguard();
            string who = keeper != null ? keeper : "Карган Старый Ворон";

            if (stayed.Count == 0)
            {
                Log($"{who}: «Все пришли, владыка. Это редкость».");
                return;
            }

            if (came.Count == 0)
            {
                Log($"{who}: «Никто не подошёл, владыка. Они вас слышали».");
                return;
            }

            Log($"{who}: «{Join(stayed)} остались на месте. "
              + "Они вас слышали, владыка».");
        }

        private Transform Table()
        {
            if (_table != null) return _table;

            var ball = Object.FindFirstObjectByType<CrystalBall>();
            if (ball != null) _table = ball.transform;

            return _table;
        }

        private static string Bodyguard()
        {
            // Телохранитель — тот, кого нельзя отправить в вылазку.
            foreach (var m in SquadRoster.Members)
                if (!string.IsNullOrEmpty(m.Unavailable)) return m.Name;

            return null;
        }

        private static string Join(List<string> names)
        {
            if (names.Count == 1) return names[0];

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(i == names.Count - 1 ? " и " : ", ");
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
