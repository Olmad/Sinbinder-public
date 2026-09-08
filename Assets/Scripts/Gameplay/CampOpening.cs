// Assets/Scripts/Gameplay/CampOpening.cs
using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Первые полминуты лагеря: Греховод вышел из палатки.
    ///
    /// Здесь закрываются две дыры, найденные на разборе демо.
    ///
    /// <b>Первая: долгу нужна предпосылка.</b> Строка, которой игра
    /// продаётся дословно — «ему не платили третью вылазку подряд», —
    /// рождается только при долге больше двух. Долг теперь есть с самого
    /// начала (лагерь жил до того, как игрок открыл глаза), но свалиться
    /// на игрока молча он не может: тогда позднейший отказ прочтётся
    /// как несправедливость. Карган говорит о нём вслух и заранее —
    /// это и есть третье требование к демо: «отказ можно было
    /// предотвратить, и игрок это видит» (00-GDD.md §8).
    ///
    /// <b>Вторая: до совета игроку некому приказывать.</b> Продукт демо —
    /// отказ, а отказ работает только на фоне послушания. Полторы минуты
    /// лагеря игрок до сих пор просто ходил. Теперь с ним вызывается
    /// пройтись рядовой — и вызывается по характеру, а не по воле
    /// сценариста: гордому важно, чтобы видели, с кем он ходит.
    /// Его можно выделить и им можно командовать, и он послушается.
    ///
    /// Никаких имён в коде: и должник, и провожатый находятся по составу.
    /// Канон имён не закрепляет, а зашитое имя пережило бы переименование
    /// и стало бы ссылкой в пустоту.
    /// </summary>
    public class CampOpening : MonoBehaviour
    {
        [Tooltip("Сколько подождать после заставки, прежде чем заговорить.")]
        [SerializeField] private float _afterTitle = 5f;

        [Tooltip("Пауза между провожатым и предупреждением Каргана.")]
        [SerializeField] private float _between = 7f;

        [Tooltip("Насколько близко провожатый подходит к игроку.")]
        [SerializeField] private float _escortDistance = 3f;

        void Start() => StartCoroutine(Routine());

        private IEnumerator Routine()
        {
            // Реальное время: заставка доли 0 держит игру на паузе,
            // и отсчитывать под ней игровое было бы нечестно.
            yield return new WaitForSecondsRealtime(_afterTitle);

            Escort();

            yield return new WaitForSecondsRealtime(_between);

            WarnAboutDebt();
        }

        /// <summary>
        /// Провожатый: гордый рядовой напрашивается пройтись рядом.
        ///
        /// Ищем по составу: рядовой (навыка командования нет), с грехом
        /// Гордыни, живой и не ушедший. Гордыня здесь не украшение —
        /// она и есть причина, по которой он вызвался.
        /// </summary>
        private void Escort()
        {
            Warrior best = null;

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;
                if (!SquadRoster.TryGet(w.DisplayName, out var m)) continue;

                if (m.IsAway) continue;
                if (!string.IsNullOrEmpty(m.Unavailable)) continue;   // телохранитель занят
                if (Leadership.IsExperienced(m.Leadership)) continue; // опытные не напрашиваются
                if (m.Sin != Core.SinType.Pride) continue;

                // Порядок обхода сцены не гарантирован, а вызваться должен
                // каждый раз один и тот же: сравниваем по имени.
                if (best == null
                    || string.CompareOrdinal(w.DisplayName, best.DisplayName) < 0)
                    best = w;
            }

            if (best == null)
            {
                // Гордого рядового в составе нет — сцена не ломается,
                // но и урока послушания не будет. Молчать об этом нельзя.
                Debug.LogWarning("[ЛАГЕРЬ] Провожатого не нашлось: "
                               + "некому напроситься в спутники.");
                return;
            }

            Log($"{best.DisplayName}: «Владыка, позвольте пройтись с вами. "
              + "Пусть видят, с кем я хожу».");

            // Подходит сам, настоящим приказом через настоящий конвейер:
            // это первое, что игрок видит исполненным, и подделывать его
            // нельзя (docs/09-PROLOGUE.md §2).
            var cam = Camera.main;
            if (cam == null) return;

            var to = cam.transform.position;
            to.y = best.transform.position.y;

            var from = best.transform.position;
            var step = (to - from);
            step.y = 0f;

            if (step.sqrMagnitude > 0.01f)
                to = from + step.normalized * Mathf.Max(0f, step.magnitude - _escortDistance);

            best.IssueCommand(CommandKind.Move, to);
        }

        /// <summary>
        /// Предупреждение о долге. Говорится только если долг правда есть:
        /// реплика про «затянувшееся наказание» при нулевом долге была бы
        /// ложью, а игра, которая продаёт честные причины, врать не может
        /// даже в мелочи.
        /// </summary>
        private void WarnAboutDebt()
        {
            SquadRoster.Member debtor = default;
            bool found = false;

            foreach (var m in SquadRoster.Members)
            {
                // Порога, за которым Жадность вдруг ставит счёты выше
                // приказа, больше нет: долг стал шкалой. Предупреждаем
                // с двух невыплат — с них замер стенда впервые слышит
                // разницу в исходе (docs/12-BALANCE.md, «Долг»).
                if (m.UnpaidMissions < 2) continue;

                if (!found || m.UnpaidMissions > debtor.UnpaidMissions
                    || (m.UnpaidMissions == debtor.UnpaidMissions
                        && string.CompareOrdinal(m.Name, debtor.Name) < 0))
                {
                    debtor = m;
                    found = true;
                }
            }

            if (!found) return;

            var bodyguard = Bodyguard();
            string who = bodyguard.HasValue ? bodyguard.Value.Name : "Карган Старый Ворон";

            Log($"{who}: «Владыка, ваше наказание {Possessive(debtor.Name)} "
              + "затянулось. Подумайте о последствиях».");
        }

        private static SquadRoster.Member? Bodyguard()
        {
            foreach (var m in SquadRoster.Members)
                if (!string.IsNullOrEmpty(m.Unavailable)) return m;

            return null;
        }

        /// <summary>
        /// «Марга Копатель» → «Марги». Грубо, по первому слову: имён
        /// в демо девять, и склонять их полноценно незачем.
        /// </summary>
        private static string Possessive(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            int space = name.IndexOf(' ');
            string first = space > 0 ? name.Substring(0, space) : name;

            if (first.EndsWith("а")) return first.Substring(0, first.Length - 1) + "и";
            if (first.EndsWith("я")) return first.Substring(0, first.Length - 1) + "и";

            return first + "а";
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
