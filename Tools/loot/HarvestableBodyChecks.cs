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
        PostBattleShare(check);
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

    // Production LootChain + LootCarrySystem + HarvestableBody + PlayerInventory;
    // only the fighter/gear/Unity and text boundaries are shims.
    private static void PostBattleShare(Action<bool, string> check)
    {
        ResetInventory();
        var greed = new Warrior { Team = Team.Player, Soul = new SoulData { Sin = SinType.Greed }, Kept = 3 };
        var pride = new Warrior { Team = Team.Player, Soul = new SoulData { Sin = SinType.Pride } };
        var glutton = new Warrior { Team = Team.Player, Soul = new SoulData { Sin = SinType.Gluttony } };
        var body = Body(10, "Sword", foe: true);
        var bodies = new System.Collections.Generic.List<HarvestableBody> { body };
        var squad = new System.Collections.Generic.List<Damageable> {
            new Damageable { Warrior = greed }, new Damageable { Warrior = pride }, new Damageable { Warrior = glutton }
        };
        LootChain.Enabled = true;
        try
        {
            LootChain.Share(squad, bodies);
            check(body.GoldValue == 10 && body.HasEquipment && !body.IsCollected &&
                  greed.PocketGold == 0 && pride.Equipped == null,
                "missing wallet and rejected trophy survive complete post-battle distribution including remains choice");

            var store = OpenInventory();
            for (int i = 0; i < store.MaxSlots; i++)
                if (!store.AddItem(new InventoryItem("Filler " + i, "", ItemType.Equipment)))
                    throw new InvalidOperationException("fill failed");
            LootChain.Share(squad, bodies);
            check(body.GoldValue == 0 && greed.PocketGold == 3 && store.Gold == 7 &&
                  body.HasEquipment && !body.IsCollected && store.Count == store.MaxSlots,
                "full bag consumes only accepted gold; refused trophy remains recoverable");

            LootChain.Share(squad, bodies);
            check(greed.PocketGold == 3 && store.Gold == 7 && body.HasEquipment,
                "repeat while full neither duplicates gold nor consumes rejected equipment");
            store.RemoveItem(store.GetAllItems()[0].Id);
            LootChain.Share(squad, bodies);
            check(!body.HasEquipment && store.GetAllItems().FindAll(i => i.Name == "Sword").Count == 1 &&
                  store.Gold == 7 && greed.PocketGold == 3,
                "retry accepts exactly the remaining trophy without second gold transfer");
            LootChain.Share(squad, bodies);
            check(store.GetAllItems().FindAll(i => i.Name == "Sword").Count == 1 && store.Gold == 7,
                "repeat after full delivery does not duplicate trophy");

            var selective = Body(10, "Dagger", foe: true);
            var greedOnly = new System.Collections.Generic.List<Warrior> { greed };
            var result = LootCarrySystem.DistributeLoot(greedOnly,
                new System.Collections.Generic.List<HarvestableBody> { selective });
            check(result.Gold == 10 && result.GoldBy.Count == 1 && selective.HasEquipment && result.Equipment.Count == 0,
                "greed does not consume equipment through shared transfer path");
            var prideOnly = new System.Collections.Generic.List<Warrior> { pride };
            var equipBody = Body(10, "Knife", foe: true);
            pride.EquipmentSlotFree = true;
            result = LootCarrySystem.DistributeLoot(prideOnly,
                new System.Collections.Generic.List<HarvestableBody> { equipBody });
            check(result.Equipment.Count == 1 && pride.Equipped?.Name == "Knife" &&
                  equipBody.GoldValue == 10 && !equipBody.HasEquipment,
                "pride equips only the trophy without consuming gold");
        }
        finally { LootChain.Enabled = false; ResetInventory(); }
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
    internal enum RuntimeInitializeLoadType { SubsystemRegistration }
    internal sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType mode) { }
    }
    internal static class Object
    {
        public static T FindFirstObjectByType<T>() where T : class => null;
    }
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
    public sealed class SoulData { public SinType Sin = SinType.Wrath; }
    internal static class Loc
    {
        public static string T(string text) => text;
        public static string N(string text) => text;
        public static string Name(string text) => text;
        public static string F(string text, params object[] args) => string.Format(text, args);
    }
    internal static class Grammar
    {
        public static string For(bool gender, string text) => text;
        public static string Pick(bool gender, string male, string female) => gender ? female : male;
    }
}

namespace Sinbinder.Gameplay
{
    public enum Team { Player, Enemy, Neutral }

    public class Warrior
    {
        public string DisplayName => "Fixture";
        public bool Gender => false;
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

    public sealed class SinbinderPlayer : Warrior { }
    public sealed class Damageable
    {
        public Warrior Warrior;
        public bool IsDead => Warrior == null || Warrior.IsDead;
    }

    internal static class SquadGear
    {
        public static bool Place(Warrior warrior, InventoryItem item, out GearSlot slot, out InventoryItem old)
        {
            slot = GearSlot.Weapon;
            old = warrior.Equipped;
            return true;
        }
        public static string GoldWord(int gold) => gold.ToString();
        internal static PlayerInventory LastStore { get; private set; }
        public static int Kept(Warrior warrior, int gold) => warrior.Kept;

        public static bool Pick(Warrior warrior, InventoryItem item, PlayerInventory store, out string word)
        {
            LastStore = store;
            word = "";
            return warrior.Give(item);
        }
    }

    internal static class BodyWorth
    {
        public static int Gold(ShellType shell, SoulData soul) => 0;
        public static bool HasEquipment(ShellType shell, SoulData soul) => false;
        public static string Equipment(ShellType shell, SoulData soul) => null;
    }
}

namespace Sinbinder.UI
{
    internal sealed class BattleLogUI { public void Write(string text) { } }
}
