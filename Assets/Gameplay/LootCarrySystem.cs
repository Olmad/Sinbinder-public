using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    [System.Serializable]
    public class CarriedLoot
    {
        public int Gold;
        public List<HarvestableBody> Bodies = new();
        public List<string> Equipment = new();
    }

    public static class LootCarrySystem
    {
        public static CarriedLoot DistributeLoot(List<Warrior> warriors, List<HarvestableBody> bodies)
        {
            var loot = new CarriedLoot();

            foreach (var warrior in warriors)
            {
                if (warrior.IsDead) continue;

                switch (warrior.Soul.Sin)
                {
                    case SinType.Greed:
                        var bodyWithGold = bodies.Find(b => b != null && !b.IsCollected && b.GoldValue > 0);
                        if (bodyWithGold != null)
                        {
                            loot.Gold += bodyWithGold.CollectGold();
                            Debug.Log($"[CARRY] {warrior.DisplayName} (Жадный) собирает золото");
                        }
                        break;

                    case SinType.Pride:
                        var bodyWithEquip = bodies.Find(b => b != null && !b.IsCollected && b.HasEquipment);
                        if (bodyWithEquip != null)
                        {
                            string equip = bodyWithEquip.CollectEquipment();
                            if (equip != null) loot.Equipment.Add(equip);
                            Debug.Log($"[CARRY] {warrior.DisplayName} (Гордый) забирает трофей: {equip}");
                        }
                        break;

                    case SinType.Gluttony:
                        var body = bodies.Find(b => b != null && !b.IsCollected);
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
                        var anyBody = bodies.Find(b => b != null && !b.IsCollected);
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