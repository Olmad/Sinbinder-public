// Assets/Scripts/Gameplay/SceneDoctor.cs
using System.Collections;
using System.Text;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Проверяет, что сцена собрана целиком, — и говорит об этом вслух.
    ///
    /// Заведён после того, как за три дня в одном и том же месте нашлось
    /// пять разрывов подряд: движение, выделение, удар, значки намерений,
    /// огоньки душ. Все одного рода — система написана, правило верное,
    /// стендом проверено, а звена между ней и сценой нет. И все находились
    /// перебором вручную, по одному.
    ///
    /// Пока их ищут наугад, конца не будет: проверенных цепочек пять,
    /// рваных — пять. Поэтому ищем не поломки, а способ их находить.
    ///
    /// Ходит по цепочкам «кто решает → чем исполняет → есть ли это чем»
    /// и печатает недостающее одним списком. Не чинит: чинить молча —
    /// значит прятать причину. Дело врача — поставить диагноз.
    ///
    /// Работает только в редакторе и только при открытой трассировке:
    /// игроку это видеть незачем, а на сборку не должно стоить ничего.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class SceneDoctor : MonoBehaviour
    {
        [Tooltip("Сколько подождать, прежде чем осматривать. Спавнеры "
               + "создают воинов в своих Start, и до них смотреть не на что.")]
        [SerializeField] private float _afterSeconds = 1.5f;

        void Start()
        {
            if (!Core.Transparency.Shows(Core.Clarity.Trace)) { enabled = false; return; }
            StartCoroutine(Examine());
        }

        private IEnumerator Examine()
        {
            yield return new WaitForSecondsRealtime(_afterSeconds);

            var report = new StringBuilder();
            int broken = 0;

            broken += CheckManagers(report);
            broken += CheckWarriors(report);
            broken += CheckNavigation(report);

            if (broken == 0)
            {
                Debug.Log("[ОСМОТР] Сцена собрана целиком: все цепочки сходятся.");
                yield break;
            }

            Debug.LogWarning($"[ОСМОТР] Разрывов: {broken}\n{report}");
        }

        /// <summary>Кто должен стоять на Managers, чтобы слои жили.</summary>
        private int CheckManagers(StringBuilder report)
        {
            int broken = 0;

            broken += Need<CombatManager>(report, "боем некому распоряжаться");
            broken += Need<SelectionManager>(report, "игрок никого не выделит");
            broken += Need<SoulManager>(report, "души не начнут угасать — жать будет нечего");
            broken += Need<AOS.AOSEventHub>(report,
                "отказ не поднимет ни журнал, ни тишину");
            broken += Need<Core.GamePauseController>(report,
                "панели не остановят бой под собой");
            broken += Need<Inventory.PlayerInventory>(report,
                "плату и трофеи класть некуда");
            broken += Need<Core.TransparencySettings>(report,
                "ступени прозрачности не настроены");

            return broken;
        }

        /// <summary>Из чего собран воин. Здесь ломалось чаще всего.</summary>
        private int CheckWarriors(StringBuilder report)
        {
            var warriors = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID);

            if (warriors.Length == 0)
            {
                report.AppendLine("  воинов в сцене нет вовсе");
                return 1;
            }

            int broken = 0;

            // Смотрим одного: спавнеры собирают всех одинаково, и разрыв
            // у одного значит разрыв у всех.
            //
            // Но не Греховода: он единственный, кто по замыслу без
            // AOSWarriorWrapper — тело есть, бюллетеня нет. Попадись он
            // в образец, осмотр каждый раз кричал бы «он ничего не решает»
            // про того, кому решать и не положено.
            var w = warriors[0];
            for (int i = 0; i < warriors.Length; i++)
            {
                if (warriors[i] is SinbinderPlayer) continue;
                w = warriors[i];
                break;
            }

            if (w is SinbinderPlayer)
            {
                report.AppendLine("  в сцене только Греховод, отряда нет");
                return 1;
            }

            string who = w.DisplayName;

            broken += NeedOn<Damageable>(w, report, who, "его нельзя ранить");
            broken += NeedOn<UnityEngine.AI.NavMeshAgent>(w, report, who,
                "он не сделает ни шага");
            broken += NeedOn<UnitMover>(w, report, who, "ему нечем исполнять приказ идти");
            broken += NeedOn<AutoAttack>(w, report, who, "он не сможет ударить");
            broken += NeedOn<SelectionComponent>(w, report, who, "его нельзя выделить");
            broken += NeedOn<AOS.AOSWarriorWrapper>(w, report, who, "он ничего не решает");
            broken += NeedOn<AOS.RefusalPresenter>(w, report, who,
                "его отказа не будет видно");

            if (w.GetComponentInChildren<UI.OverheadUI>() == null)
            {
                report.AppendLine($"  {who}: нет надголовного — первая ступень "
                                + "прозрачности не работает, и главный кадр игры не снять");
                broken++;
            }

            // Умения. Не у всякого воина они есть — у Жадности, Гордыни
            // и Зависти наборов не написано, — но если каталог для его
            // шкалы что-то знает, а компонента нет, воину нечем сделать
            // ничего своего. Именно так шесть наборов пролежали в проекте
            // никем не повешенные, и заметить это удалось только чтением
            // кода, а не игрой.
            if (w.Soul != null
                && AOS.SkillCatalog.Dominant(AOS.Soul.FromWarrior(w)).Count > 0
                && w.GetComponent<AOS.ISkillSet>() == null)
            {
                report.AppendLine($"  {who}: умения его шкалы написаны, но набор "
                                + "не повешен — он не сделает ничего своего");
                broken++;
            }

            return broken;
        }

        /// <summary>Есть ли по чему ходить.</summary>
        private int CheckNavigation(StringBuilder report)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position,
                    out _, 25f, UnityEngine.AI.NavMesh.AllAreas))
                return 0;

            report.AppendLine("  навмеша нет: агенты есть, а ходить им негде");
            return 1;
        }

        private int Need<T>(StringBuilder report, string cost) where T : Object
        {
            if (Object.FindFirstObjectByType<T>() != null) return 0;

            report.AppendLine($"  нет {typeof(T).Name} — {cost}");
            return 1;
        }

        private int NeedOn<T>(Warrior w, StringBuilder report, string who, string cost)
            where T : Component
        {
            if (w.GetComponent<T>() != null) return 0;

            report.AppendLine($"  {who}: нет {typeof(T).Name} — {cost}");
            return 1;
        }
    }
}
