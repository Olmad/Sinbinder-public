// Assets/Scripts/Gameplay/SoulHarvester.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Жатва душ. Сцена 4 пролога: первая волна Охотников — это ещё
    /// и урок о том, что качество души зависит от того, как быстро успел
    /// (docs/09-PROLOGUE.md §4).
    ///
    /// Урок держится на <see cref="SoulDecay"/>: свежая душа сохраняет
    /// характер, память и перки, распавшаяся — почти ничего. Игрок узнаёт
    /// это не из справки, а из того, что подобранная поздно душа названа
    /// иначе, чем подобранная сразу.
    ///
    /// Раньше здесь висел OnGUI с надписью «доступно: N» — то есть игроку
    /// показывалась цифра, чего в этой игре не бывает нигде (00-GDD.md §7).
    /// Причём на каждом своём воине сразу: надписи рисовались одна поверх
    /// другой. Подсказку показывает теперь одна панель на сцену, словами.
    /// </summary>
    public class SoulHarvester : MonoBehaviour
    {
        [SerializeField] private KeyCode _harvestKey = KeyCode.E;
        [SerializeField] private float _harvestCooldown = 2f;

        private float _cooldownTimer;
        private bool _isPlayerUnit;
        private Warrior _warrior;

        void Start()
        {
            // Именно свой, а не любой Warrior: проверка «компонент есть»
            // считала бы своим и охотника, повесь его кто-нибудь на врага.
            _warrior = GetComponent<Warrior>();
            _isPlayerUnit = _warrior != null && _warrior.Team == Team.Player;
        }

        void Update()
        {
            if (!_isPlayerUnit) return;

            // Павший не жнёт: руки есть, а жить в них некому.
            if (_warrior == null || _warrior.IsDead) return;

            _cooldownTimer -= Time.deltaTime;

            if (Input.GetKeyDown(_harvestKey) && _cooldownTimer <= 0f)
                TryHarvest();
        }

        /// <summary>Кадр последней жалобы на полные банки: жнец висит
        /// на каждом своём воине, и без этого жаловались бы все разом.</summary>
        private int _lastFullFrame = -1;

        private void TryHarvest()
        {
            var souls = SoulManager.Instance;

            if (souls == null)
            {
                // Жать нечем — это событие, а не тишина: клавиша нажата,
                // и игрок вправе знать, почему ничего не случилось.
                Debug.LogWarning("[ЖАТВА] SoulManager в сцене нет: "
                               + "собирать души некому.");
                return;
            }

            // Душа живёт в банке, и банок при себе конечное число.
            // Это и есть предел «сколько унесёшь»: раньше его не было
            // вовсе — жатва складывала души в список без счёта.
            int jar = Core.Satchel.FreeJar();
            if (jar < 0)
            {
                if (_lastFullFrame != Time.frameCount)
                {
                    _lastFullFrame = Time.frameCount;

                    // Полка душ стоит в склепе, а не на поле боя, и в демо
                    // её нет вовсе: совет «поставьте на полку» там звал
                    // к тому, чего не найти.
                    Log(Object.FindFirstObjectByType<Crypt.SoulShelf>() != null
                        ? Loc.T("Все банки полны. Поставьте душу на полку или оставьте эту.")
                        : Loc.T("Все банки полны — эту душу унести не в чем."));
                }
                return;
            }

            var soul = souls.TryHarvestSoul(transform.position);

            // Молчим: компонент висит на каждом своём воине, и «слишком
            // далеко» написали бы разом все, кто не дотянулся. Что к душе
            // надо подойти, говорит подсказка — один раз и одна на сцену.
            if (soul == null) return;

            _cooldownTimer = _harvestCooldown;
            souls.RemoveIndicator(soul);

            // Из общего списка — сразу в банку. Иначе о душе было бы
            // две правды: она и в суме, и на полке склепа, которая тот же
            // список и показывает. Полка теперь показывает только то,
            // что игрок поставил на неё руками.
            var kept = souls.TakeHarvested(souls.Harvested.Count - 1);
            if (kept.Soul != null) Core.Satchel.Fill(jar, kept.Soul, kept.Quality);

            // «Перетягивание» (автор, 28.10: «сбор души — анимация и замирание»): присел, замер —
            // в эти полторы секунды сборщик открыт удару. Демо не трогаем: там жатва — урок, а не риск.
            if (Lords.Active) Freeze(this);

            // Тем же наклоном — кошель убитого (автор, 26.10: «я не могу собрать деньги, которые
            // собирают воины с трупов»; решение 27.10: «одним наклоном с душой, без новой кнопки»).
            // Кто быстрее — того и монеты: обобранный воинами труп пуст.
            if (GetComponent<SinbinderPlayer>() != null) TakePurse();

            // Вот и весь урок: одно и то же действие названо по-разному
            // в зависимости от того, насколько игрок промедлил.
            // «Некромант» стоит на DigMostSouls, и писать его было некому.
            // Жнец висит на своём воине — его и заслуга.
            var mine = GetComponent<Warrior>();
            if (mine != null)
            {
                mine.Reputation.Deeds.Add(new AOS.DeedRecord
                    { Type = AOS.DeedType.DigMostSouls, Importance = 1.0f });
                AOS.TitleManager.UpdateTitle(mine);
            }

            Log(Loc.F("Душа собрана: {0}. "
              + "{1}.", Loc.Name(soul.Warrior.DisplayName), SoulDecay.Describe(soul.SoulQuality)));

            if (soul.SoulQuality == SoulQuality.Dissolved)
                Log(Loc.T("От неё осталась одна воля. Такая поднимется зомби."));
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);

        /// <summary>
        /// Сбор души: клип Gather (WarriorAnimation) и ноги стоят, пока он идёт. Зовут жатва игрока и
        /// некромант-ИИ (RivalLord.Necro) — одно правило для обоих.
        /// </summary>
        public static void Freeze(Component who)
        {
            if (who == null) return;
            var body = who.GetComponent<WarriorAnimation>() ?? who.GetComponentInChildren<WarriorAnimation>();
            body?.Gather();
            var legs = who.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (legs == null || !legs.enabled || !legs.isOnNavMesh) return;
            // Корутину ведёт сам позвавший компонент, если он включён, иначе — первый
            // включённый скрипт воина: на выключенном она бы не дошла до Release,
            // и ноги стояли бы до конца боя. Некого попросить — ноги не останавливаем.
            var host = who as MonoBehaviour;
            if (host == null || !host.isActiveAndEnabled)
                host = System.Array.Find(who.GetComponents<MonoBehaviour>(), m => m != null && m.isActiveAndEnabled);
            if (host == null) return;
            legs.isStopped = true;
            legs.ResetPath();
            host.StartCoroutine(Release(legs));
        }

        private static System.Collections.IEnumerator Release(UnityEngine.AI.NavMeshAgent legs)
        {
            yield return new WaitForSeconds(WarriorAnimation.GatherFor);
            if (legs != null && legs.enabled && legs.isOnNavMesh) legs.isStopped = false;
        }

        /// <summary>Кошель ближайшего убитого врага (в двух метрах) — в кошель Греховода.</summary>
        private void TakePurse()
        {
            if (CombatManager.Instance == null || Inventory.PlayerInventory.Instance == null) return;
            HarvestableBody near = null;
            float best = 2f;
            foreach (var body in CombatManager.Instance.BodiesOnField)
            {
                if (body == null || body.IsCollected || !body.Foe || body.GoldValue <= 0) continue;
                float d = CampFocus.GroundDistance(transform.position, body.transform.position);
                if (d < best) { best = d; near = body; }
            }
            if (near == null) return;

            int gold = near.CollectGold();
            if (gold <= 0) return;
            Inventory.PlayerInventory.Instance.AddGold(gold);
            Log(Loc.F("Кошель убитого — в ваш: +{0}.", gold));
        }
    }
}
