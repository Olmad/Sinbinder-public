// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    public class HarvestableBody : MonoBehaviour
    {
        internal const float LootSearchRadius = 8f;

        [SerializeField] private ShellType _shell;
        [SerializeField] private int _goldValue;
        [SerializeField] private bool _hasEquipment;
        [SerializeField] private string _equipmentName;

        private bool _collected = false;
        private bool _gatheringLoot;

        public ShellType Shell => _shell;
        public int GoldValue => _goldValue;
        public bool HasEquipment => _hasEquipment;
        public string EquipmentName => _equipmentName;
        public bool IsCollected => _collected;
        internal bool HasLoot => !_collected && !_gatheringLoot && (_goldValue > 0 || HasLootEquipment);

        private bool HasLootEquipment => _hasEquipment && !string.IsNullOrWhiteSpace(_equipmentName);

        /// <summary>
        /// Враг ли лежит. Добычу отряд берёт с врагов (<see cref="LootChain"/>);
        /// обирать павших товарищей — другой поступок, и он пока не написан.
        /// </summary>
        public bool Foe { get; set; }

        /// <summary>
        /// Собрать труп по покойнику.
        ///
        /// Единственная правда о том, чего он стоит, лежит в
        /// <see cref="BodyWorth"/>: вызывающим её не пересчитывать
        /// и не подменять. Раньше золото приходило сюда числом
        /// с места вызова, и это число было случайным.
        /// </summary>
        public void Initialize(ShellType shell, SoulData soul)
        {
            _shell = shell;
            _goldValue = BodyWorth.Gold(shell, soul);
            _hasEquipment = BodyWorth.HasEquipment(shell, soul);
            _equipmentName = BodyWorth.Equipment(shell, soul);
        }

        /// <summary>
        /// Реквизит без покойника: сундук на полигоне, подложенный узел.
        /// Для трупа этот путь не годится — у трупа есть душа.
        /// </summary>
        public void Initialize(ShellType shell, int gold, bool equipment, string equipName)
        {
            _shell = shell;
            _goldValue = gold;
            _hasEquipment = equipment;
            _equipmentName = equipName;
        }

        public int CollectGold()
        {
            if (_collected || _gatheringLoot) return 0;
            int gold = _goldValue;
            _goldValue = 0;
            Debug.Log($"[LOOT] +{gold} золота");
            return gold;
        }

        public string CollectEquipment()
        {
            if (_collected || _gatheringLoot || !_hasEquipment) return null;
            _hasEquipment = false;
            Debug.Log($"[LOOT] +{_equipmentName}");
            return _equipmentName;
        }

        internal bool CanLoot(Warrior collector) => HasLoot && collector != null &&
            collector.Soul != null && !collector.IsDead &&
            (collector.Team != Team.Player || (Foe && (HasLootEquipment || PlayerInventory.Instance != null)));

        /// <summary>Переносит имущество. Само тело остаётся доступно для других действий.</summary>
        internal (int gold, string equipment) GatherLoot(Warrior collector)
        {
            if (!CanLoot(collector)) return (0, null);

            // Inventory callbacks can observe the corpse during a transfer.
            _gatheringLoot = true;
            try
            {
                bool sharesWithPlayer = collector.Team == Team.Player;
                var store = sharesWithPlayer ? PlayerInventory.Instance : null;
                int gold = TransferGold(collector, sharesWithPlayer, store);
                if (!HasLootEquipment) return (gold, null);

                var trophy = LootChain.Trophy(_equipmentName);
                if (!SquadGear.Pick(collector, trophy, store, out _) &&
                    (store == null || !store.AddItem(trophy))) return (gold, null);

                _hasEquipment = false;
                return (gold, _equipmentName);
            }
            finally
            {
                _gatheringLoot = false;
            }
        }

        private int TransferGold(Warrior collector, bool sharesWithPlayer, PlayerInventory store)
        {
            int gold = _goldValue;
            if (gold <= 0 || (sharesWithPlayer && store == null)) return 0;

            int kept = sharesWithPlayer ? SquadGear.Kept(collector, gold) : gold;
            if (kept < 0 || kept > gold) return 0;

            int toPurse = gold - kept;
            if ((long)collector.PocketGold + kept > int.MaxValue ||
                (toPurse > 0 && (long)store.Gold + toPurse > int.MaxValue)) return 0;

            _goldValue = 0;
            collector.Pocket(kept);
            if (toPurse > 0) store.AddGold(toPurse);
            return gold;
        }

        public void MarkCollected()
        {
            _collected = true;
        }
    }
}
