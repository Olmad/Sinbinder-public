// Assets/Scripts/Gameplay/SoulHarvester.cs
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

        void Start()
        {
            // Именно свой, а не любой Warrior: проверка «компонент есть»
            // считала бы своим и охотника, повесь его кто-нибудь на врага.
            var warrior = GetComponent<Warrior>();
            _isPlayerUnit = warrior != null && warrior.Team == Team.Player;
        }

        void Update()
        {
            if (!_isPlayerUnit) return;

            _cooldownTimer -= Time.deltaTime;

            if (Input.GetKeyDown(_harvestKey) && _cooldownTimer <= 0f)
                TryHarvest();
        }

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

            var soul = souls.TryHarvestSoul(transform.position);

            // Молчим: компонент висит на каждом своём воине, и «слишком
            // далеко» написали бы разом все, кто не дотянулся. Что к душе
            // надо подойти, говорит подсказка — один раз и одна на сцену.
            if (soul == null) return;

            _cooldownTimer = _harvestCooldown;
            souls.RemoveIndicator(soul);

            // Вот и весь урок: одно и то же действие названо по-разному
            // в зависимости от того, насколько игрок промедлил.
            Log($"Душа собрана: {soul.Warrior.DisplayName}. "
              + $"{SoulDecay.Describe(soul.SoulQuality)}.");

            if (soul.SoulQuality == SoulQuality.Dissolved)
                Log("От неё осталась одна воля. Такая поднимется зомби.");
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
