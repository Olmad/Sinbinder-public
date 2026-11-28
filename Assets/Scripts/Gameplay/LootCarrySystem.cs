// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    [System.Serializable]
    public class CarriedLoot
    {
        public int Gold;
        public List<HarvestableBody> Bodies = new();
        public List<string> Equipment = new();

        /// <summary>Кто какой трофей забрал — чтобы вложить его в руки ему, а не отряду.</summary>
        public List<(Warrior Who, string What, bool Equipped, string Refusal, InventoryItem Old)> Trophies = new();

        /// <summary>Кто подобрал золото и сколько — для строки журнала.</summary>
        public List<(Warrior Who, int Gold, int Kept)> GoldBy = new();
    }

    public static class LootCarrySystem
    {
        public static CarriedLoot DistributeLoot(List<Warrior> warriors, List<HarvestableBody> bodies)
        {
            var loot = new CarriedLoot();

            if (warriors == null || bodies == null) return loot;
            foreach (var warrior in warriors)
            {
                if (warrior == null || warrior.IsDead || warrior.Soul == null ||
                    warrior.Team != Team.Player || warrior is SinbinderPlayer) continue;

                switch (warrior.Soul.Sin)
                {
                    case SinType.Greed:
                        var bodyWithGold = bodies.Find(b => b != null && b.Foe && b.GoldValue > 0 && b.CanLoot(warrior));
                        if (bodyWithGold != null)
                        {
                            var (gold, _) = bodyWithGold.GatherSelectedLoot(warrior, true, false, out _, out _, out int kept);
                            if (gold > 0)
                            {
                                loot.Gold += gold;
                                loot.GoldBy.Add((warrior, gold, kept));
                                Debug.Log($"[CARRY] {warrior.DisplayName} (Жадный) собирает золото");
                            }
                        }
                        break;

                    case SinType.Pride:
                        var bodyWithEquip = bodies.Find(b => b != null && b.Foe && b.HasEquipment &&
                            !string.IsNullOrWhiteSpace(b.EquipmentName) && b.CanLoot(warrior));
                        if (bodyWithEquip != null)
                        {
                            SquadGear.Place(warrior, LootChain.Trophy(bodyWithEquip.EquipmentName), out _, out var old);
                            var (_, equip) = bodyWithEquip.GatherSelectedLoot(warrior, false, true,
                                out bool equipped, out string refusal, out _);
                            if (equip != null)
                            {
                                loot.Equipment.Add(equip);
                                loot.Trophies.Add((warrior, equip, equipped, refusal, old));
                                Debug.Log($"[CARRY] {warrior.DisplayName} (Гордый) собирает трофей: {equip}");
                            }
                        }
                        break;

                    case SinType.Gluttony:
                        var body = bodies.Find(b => b != null && b.Foe && b.CanCarryRemains);
                        if (body != null)
                        {
                            body.MarkCollected();
                            loot.Bodies.Add(body);
                            Debug.Log($"[CARRY] {warrior.DisplayName} (Чревоугодный) тащит труп");
                        }
                        break;

                    case SinType.Sloth:
                        Debug.Log($"[CARRY] {warrior.DisplayName} (Унылый) отказывается нести что-либо");
                        break;

                    case SinType.Wrath:
                        Debug.Log($"[CARRY] {warrior.DisplayName} (Гневный) не носит вещи");
                        break;

                    default:
                        var anyBody = bodies.Find(b => b != null && b.Foe && b.CanCarryRemains);
                        if (anyBody != null)
                        {
                            anyBody.MarkCollected();
                            loot.Bodies.Add(anyBody);
                            Debug.Log($"[CARRY] {warrior.DisplayName} берёт труп");
                        }
                        break;
                }
            }

            return loot;
        }
    }
}