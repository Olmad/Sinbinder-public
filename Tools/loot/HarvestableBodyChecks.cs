using System;
using System.Reflection;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.Inventory;

internal static class HarvestableBodyChecks
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerLoot(check);
        ReceiverFailure(check);
        OtherTeams(check);
        InvalidOrUnavailable(check);
        ReentrantInventoryChange(check);
        ResetInventory();
    }

    private static void PlayerLoot(Action<bool, string> check)
    {
        var store = OpenInventory();
        store.SetGold(20);
        var warrior = new Warrior { Team = Team.Player, Kept = 4, EquipmentSlotFree = true };
        warrior.Pocket(6);
        var body = Body(13, "Sword", foe: true);
        check(body.CanLoot(warrior) && body.HasLoot, "AOS player warrior can target a resource-bearing enemy corpse");

        var loot = body.GatherLoot(warrior);
        check(loot.gold == 13 && warrior.PocketGold == 10 && store.Gold == 29 && body.GoldValue == 0,
            "accepted player gold is conserved across corpse, split pocket and wallet");
        check(loot.equipment == "Sword" && warrior.Equipped?.Name == "Sword" && !body.HasEquipment && store.Count == 0,
            "accepted trophy is equipped once and does not create an inventory copy");
        check(!body.HasLoot && !body.CanLoot(warrior) && !body.IsCollected,
            "depleted resource body stops attracting AOS while remaining available to Remains");

        loot = body.GatherLoot(warrior);
        check(loot.gold == 0 && loot.equipment == null && body.CollectGold() == 0 && body.CollectEquipment() == null &&
              warrior.PocketGold == 10 && store.Gold == 29 && store.Count == 0,
            "a second resource action cannot duplicate money or equipment");

        var ally = Body(7, "Allied shield", foe: false);
        loot = ally.GatherLoot(warrior);
        check(!ally.CanLoot(warrior) && loot.gold == 0 && loot.equipment == null && ally.HasLoot,
            "player squad cannot consume the resources of a friendly corpse");
    }

    private static void ReceiverFailure(Action<bool, string> check)
    {
        var full = OpenInventory();
        for (int i = 0; i < full.MaxSlots; i++)
            if (!full.AddItem(new InventoryItem("Stored gear " + i, "", ItemType.Equipment)))
                throw new InvalidOperationException("Failed to fill the production inventory");

        var warrior = new Warrior { Team = Team.Player, Kept = 2 };
        var body = Body(10, "Helmet", foe: true);
        var loot = body.GatherLoot(warrior);
        check(loot.gold == 10 && warrior.PocketGold == 2 && full.Gold == 8 &&
              loot.equipment == null && body.HasEquipment && body.HasLoot && !body.IsCollected && full.Count == full.MaxSlots,
            "full pack retains refused equipment after successful gold transfer");

        full.RemoveItem(full.GetAllItems()[0].Id);
        loot = body.GatherLoot(warrior);
        check(loot.gold == 0 && loot.equipment == "Helmet" && !body.HasEquipment &&
              full.GetAllItems().Exists(item => item.Name == "Helmet") && full.Count == full.MaxSlots,
            "a newly free pack slot receives only the remaining trophy");

        ResetInventory();
        var noStore = new Warrior { Team = Team.Player, Kept = 3, EquipmentSlotFree = true };
        body = Body(10, "Knife", foe: true);
        loot = body.GatherLoot(noStore);
        check(loot.gold == 0 && body.GoldValue == 10 && noStore.PocketGold == 0 &&
              loot.equipment == "Knife" && noStore.Equipped?.Name == "Knife" && !body.HasEquipment,
            "missing wallet leaves the full split on the corpse but allows a free equipment slot");
        check(body.HasLoot && !body.CanLoot(noStore),
            "player avoids repeatedly targeting gold the missing wallet cannot receive");

        body = Body(5, "Shield", foe: true);
        loot = body.GatherLoot(noStore);
        check(loot.gold == 0 && loot.equipment == null && body.GoldValue == 5 && body.HasEquipment,
            "missing wallet and unavailable equipment slot preserve both recoverable resources");
    }

    private static void OtherTeams(Action<bool, string> check)
    {
        var store = OpenInventory();
        store.SetGold(15);
        var neutral = new Warrior { Team = Team.Neutral, Kept = 2 };
        var body = Body(8, "Relic", foe: false);
        var loot = body.GatherLoot(neutral);
        check(loot.gold == 8 && neutral.PocketGold == 8 && store.Gold == 15 && body.GoldValue == 0,
            "neutral keeps its gold in its own pocket without funding the protagonist");
        check(loot.equipment == null && body.HasEquipment && store.Count == 0 && SquadGear.LastStore == null,
            "neutral has no gear fallback to the protagonist pack");

        var enemy = new Warrior { Team = Team.Enemy, EquipmentSlotFree = true };
        body = Body(6, "Dagger", foe: false);
        loot = body.GatherLoot(enemy);
        check(loot.gold == 6 && enemy.PocketGold == 6 && loot.equipment == "Dagger" &&
              enemy.Equipped?.Name == "Dagger" && store.Gold == 15 && store.Count == 0 && !body.IsCollected,
            "enemy can accept both resources on its own while preserving remains");
    }

    private static void InvalidOrUnavailable(Action<bool, string> check)
    {
        var store = OpenInventory();
        var warrior = new Warrior { Team = Team.Player, Kept = 3, EquipmentSlotFree = true };
        var body = Body(10, null, foe: true);
        store.SetGold(int.MaxValue - 1);
        var loot = body.GatherLoot(warrior);
        check(loot.gold == 0 && warrior.PocketGold == 0 && store.Gold == int.MaxValue - 1 && body.GoldValue == 10,
            "full numeric wallet preserves the entire gold split");

        store.SetGold(0);
        warrior.Pocket(int.MaxValue - 1);
        loot = body.GatherLoot(warrior);
        check(loot.gold == 0 && warrior.PocketGold == int.MaxValue - 1 && store.Gold == 0 && body.GoldValue == 10,
            "full numeric pocket preserves the entire gold split");

        var neutral = new Warrior { Team = Team.Neutral };
        neutral.Pocket(int.MaxValue);
        loot = body.GatherLoot(neutral);
        check(loot.gold == 0 && neutral.PocketGold == int.MaxValue && body.GoldValue == 10 && store.Gold == 0,
            "NPC pocket cannot overflow or transfer its balance to the protagonist");

        var invalidSplit = new Warrior { Team = Team.Player, Kept = 11 };
        loot = body.GatherLoot(invalidSplit);
        check(loot.gold == 0 && invalidSplit.PocketGold == 0 && body.GoldValue == 10 && store.Gold == 0,
            "a split outside the resource quantity transfers nothing");

        var blocked = Body(10, "Hauberk", foe: true);
        blocked.MarkCollected();
        loot = blocked.GatherLoot(warrior);
        check(!blocked.HasLoot && !blocked.CanLoot(warrior) && loot.gold == 0 && loot.equipment == null,
            "a corpse already taken for remains cannot yield resources afterward");

        var corrupt = new HarvestableBody { Foe = true };
        corrupt.Initialize(ShellType.Living, -1, true, "");
        loot = corrupt.GatherLoot(warrior);
        check(!corrupt.HasLoot && !corrupt.CanLoot(warrior) && loot.gold == 0 && loot.equipment == null && store.Count == 0,
            "negative coins and nameless equipment never enter a pocket or pack");

        body = Body(4, "Trophy", foe: true);
        var dead = new Warrior { Team = Team.Player, IsDead = true };
        var uninitialized = new Warrior { Team = Team.Player, Soul = null };
        check(!body.CanLoot(null) && !body.CanLoot(dead) && !body.CanLoot(uninitialized) &&
              body.GatherLoot(dead).gold == 0 && body.GatherLoot(uninitialized).equipment == null && body.GoldValue == 4,
            "missing, dead and uninitialized collectors do not consume resources");
    }

    private static void ReentrantInventoryChange(Action<bool, string> check)
    {
        var store = OpenInventory();
        var warrior = new Warrior { Team = Team.Player, Kept = 3 };
        var body = Body(11, "Sword", foe: true);
        int calls = 0;
        bool reentryBlocked = true;
        store.OnInventoryChanged = () =>
        {
            calls++;
            var nested = body.GatherLoot(warrior);
            reentryBlocked &= nested.gold == 0 && nested.equipment == null &&
                              body.CollectGold() == 0 && body.CollectEquipment() == null;
        };

        var loot = body.GatherLoot(warrior);
        check(calls > 0 && reentryBlocked && loot.gold == 11 && loot.equipment == "Sword" &&
              store.Gold == 8 && warrior.PocketGold == 3 && store.Count == 1 && !body.HasLoot,
            "inventory callbacks cannot reenter the same corpse through either resource API");
    }

    private static HarvestableBody Body(int gold, string equipment, bool foe)
    {
        var body = new HarvestableBody { Foe = foe };
        body.Initialize(ShellType.Living, gold, equipment != null, equipment);
        return body;
    }

    private static PlayerInventory OpenInventory()
    {
        ResetInventory();
        var store = new PlayerInventory();
        typeof(PlayerInventory).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(store, null);
        return store;
    }

    private static void ResetInventory()
        => typeof(PlayerInventory).GetProperty(nameof(PlayerInventory.Instance)).SetValue(null, null);
}

// The tests link production body and inventory code. Only Unity and the fighter,
// equipment and valuation boundaries are replaced to make reception explicit.
namespace UnityEngine
{
    internal sealed class SerializeField : Attribute { }
    public class MonoBehaviour
    {
        protected object gameObject { get; } = new();
        protected void DontDestroyOnLoad(object value) { }
        protected void Destroy(object value) { }
    }

    internal static class Debug { public static void Log(string text) { } }
    internal static class Mathf { public static int Min(int a, int b) => Math.Min(a, b); }
}

namespace Sinbinder.Core
{
    public sealed class SoulData { }
}

namespace Sinbinder.Gameplay
{
    public enum Team { Player, Enemy, Neutral }

    public sealed class Warrior
    {
        public Team Team;
        public bool IsDead;
        public SoulData Soul = new();
        public int Kept;
        public bool EquipmentSlotFree;
        public int PocketGold { get; private set; }
        public InventoryItem Equipped { get; private set; }

        public void Pocket(int gold) => PocketGold = checked(PocketGold + gold);

        public bool Give(InventoryItem item)
        {
            if (!EquipmentSlotFree || Equipped != null) return false;
            Equipped = item;
            return true;
        }
    }

    internal static class SquadGear
    {
        internal static PlayerInventory LastStore { get; private set; }
        public static int Kept(Warrior warrior, int gold) => warrior.Kept;

        public static bool Pick(Warrior warrior, InventoryItem item, PlayerInventory store, out string word)
        {
            LastStore = store;
            word = "";
            return warrior.Give(item);
        }
    }

    internal static class LootChain
    {
        public static InventoryItem Trophy(string name) => new InventoryItem(name, "", ItemType.Equipment);
    }

    internal static class BodyWorth
    {
        public static int Gold(ShellType shell, SoulData soul) => 0;
        public static bool HasEquipment(ShellType shell, SoulData soul) => false;
        public static string Equipment(ShellType shell, SoulData soul) => null;
    }
}
