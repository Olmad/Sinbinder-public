using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class SoulHarvester : MonoBehaviour
    {
        [SerializeField] private KeyCode _harvestKey = KeyCode.E;
        [SerializeField] private float _harvestCooldown = 2f;

        private float _cooldownTimer;
        private bool _isPlayerUnit;

        void Start()
        {
            var warrior = GetComponent<Warrior>();
            if (warrior != null)
            {
                _isPlayerUnit = true;
                Debug.Log($"[HARVESTER] SoulHarvester активирован на {warrior.DisplayName}");
            }
        }

        void Update()
        {
            if (!_isPlayerUnit) return;
            _cooldownTimer -= Time.deltaTime;

            if (Input.GetKeyDown(_harvestKey) && _cooldownTimer <= 0f)
            {
                TryHarvest();
            }
        }

        private void TryHarvest()
        {
            if (SoulManager.Instance == null)
            {
                Debug.Log("[HARVESTER] SoulManager.Instance == null");
                return;
            }

            Debug.Log($"[HARVESTER] Попытка жатвы. Угасающих душ: {SoulManager.Instance.FadingCount}");

            var soul = SoulManager.Instance.TryHarvestSoul(transform.position);

            if (soul != null)
            {
                _cooldownTimer = _harvestCooldown;
                SoulManager.Instance.RemoveIndicator(soul);
                Debug.Log($"[HARVESTER] Душа собрана: {soul.Warrior.DisplayName}");
            }
            else
            {
                Debug.Log("[HARVESTER] Нет душ в радиусе");
            }
        }

        void OnGUI()
        {
            if (!_isPlayerUnit) return;
            int count = SoulManager.Instance != null ? SoulManager.Instance.FadingCount : 0;
            if (count > 0)
            {
                GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height - 50, 250, 30),
                    $"Нажмите E для Жатвы душ (доступно: {count})");
            }
        }
    }
}