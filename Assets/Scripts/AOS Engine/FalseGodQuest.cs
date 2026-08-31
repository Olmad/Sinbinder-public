using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    [CreateAssetMenu(fileName = "FalseGodQuest", menuName = "Sinbinder/Quests/False God")]
    public class FalseGodQuest : Quest
    {
        private void OnEnable()
        {
            questName = "Ложный Бог";
            description = "В деревне путешественник выдал себя за божество. Крестьяне забросили поля и молятся ложному идолу. Нужно навести порядок.";

            playerOptions = new List<QuestOption>
            {
                new QuestOption
                {
                    text = "«Твоя шутка зашла слишком далеко. Иди и сознайся».",
                    outcome = MissionOutcome.VillageSaved,
                    respectChange = 10,
                    fearChange = -5
                },
                new QuestOption
                {
                    text = "«Продолжай. Это забавно. Но плати мне долю».",
                    outcome = MissionOutcome.NewCultEstablished,
                    goldChange = 50,
                    moralityChange = -1
                },
                new QuestOption
                {
                    text = "«Ты прав. Бог действительно здесь. Я — его посланник».",
                    outcome = MissionOutcome.NewCultEstablished,
                    respectChange = 20,
                    moralityChange = 1
                },
                new QuestOption
                {
                    text = "«Убей его» (без слов).",
                    outcome = MissionOutcome.TravelerKilled,
                    fearChange = 15
                }
            };

            commanderOutcomes = new List<CommanderOutcome>
            {
                new CommanderOutcome { sin = SinType.Wrath, moral = MoralType.Vicious, action = MissionAction.KillEveryone },
                new CommanderOutcome { sin = SinType.Wrath, moral = MoralType.Neutral, action = MissionAction.KillTraveler },
                new CommanderOutcome { sin = SinType.Wrath, moral = MoralType.Pious, action = MissionAction.KillTraveler },
                new CommanderOutcome { sin = SinType.Pride, moral = MoralType.Vicious, action = MissionAction.DestroyAltar },
                new CommanderOutcome { sin = SinType.Pride, moral = MoralType.Neutral, action = MissionAction.SanctifyAltar },
                new CommanderOutcome { sin = SinType.Pride, moral = MoralType.Pious, action = MissionAction.SanctifyAltar },
                new CommanderOutcome { sin = SinType.Greed, moral = MoralType.Vicious, action = MissionAction.TaxVillage },
                new CommanderOutcome { sin = SinType.Greed, moral = MoralType.Neutral, action = MissionAction.TaxVillage },
                new CommanderOutcome { sin = SinType.Greed, moral = MoralType.Pious, action = MissionAction.TaxVillage },
                new CommanderOutcome { sin = SinType.Sloth, moral = MoralType.Vicious, action = MissionAction.IgnoreVillage },
                new CommanderOutcome { sin = SinType.Sloth, moral = MoralType.Neutral, action = MissionAction.IgnoreVillage },
                new CommanderOutcome { sin = SinType.Sloth, moral = MoralType.Pious, action = MissionAction.IgnoreVillage },
                new CommanderOutcome { sin = SinType.Envy, moral = MoralType.Vicious, action = MissionAction.EnslaveVillage },
                new CommanderOutcome { sin = SinType.Envy, moral = MoralType.Neutral, action = MissionAction.EnslaveVillage },
                new CommanderOutcome { sin = SinType.Envy, moral = MoralType.Pious, action = MissionAction.HelpVillage },
            };
        }

        public override List<MissionAction> GetAvailableCommanderActions()
        {
            return new List<MissionAction>
            {
                MissionAction.KillEveryone,
                MissionAction.KillTraveler,
                MissionAction.DestroyAltar,
                MissionAction.SanctifyAltar,
                MissionAction.TaxVillage,
                MissionAction.IgnoreVillage,
                MissionAction.EnslaveVillage,
                MissionAction.HelpVillage
            };
        }

        protected override MissionContext CreateContext()
        {
            return new MissionContext
            {
                MissionID = MissionID.FalseGod,
                HasInnocentVictims = true,
                HasGuiltyParty = true,
                HasTreasure = true,
                HasAltar = true,
                IsVillageIntact = true,
                Traveler = new NPCData { Id = "traveler", DisplayName = "Бродяга" },
                VillageElder = new NPCData { Id = "village_elder", DisplayName = "Староста" }
            };
        }

        protected override MissionOutcome ApplyOutcome(MissionAction action, Warrior commander)
        {
            switch (action)
            {
                case MissionAction.KillEveryone:
                    return MissionOutcome.VillageDestroyed;

                case MissionAction.KillTraveler:
                    return MissionOutcome.VillageSaved;

                case MissionAction.DestroyAltar:
                    return MissionOutcome.VillageAbandoned;

                case MissionAction.SanctifyAltar:
                    return MissionOutcome.NewCultEstablished;

                case MissionAction.TaxVillage:
                    return MissionOutcome.VillageSaved;

                case MissionAction.IgnoreVillage:
                    return MissionOutcome.None;

                case MissionAction.EnslaveVillage:
                    return MissionOutcome.VillageDestroyed;

                case MissionAction.HelpVillage:
                    return MissionOutcome.VillageSaved;

                default:
                    return MissionOutcome.None;
            }
        }

        public override void ResolvePlayerChoice(QuestOption chosenOption)
        {
            if (chosenOption == null) return;

            // Применяем изменения к игроку (через SinbinderPlayer или GameManager)
            var player = FindObjectOfType<SinbinderPlayer>();
            if (player != null)
            {
                player.Reputation.Respect += chosenOption.respectChange;
                player.Reputation.Fear += chosenOption.fearChange;
                // Золото живёт в инвентаре — второго счёта на игроке заводить
                // нельзя, иначе про одни и те же монеты будет две правды.
                Inventory.PlayerInventory.Instance?.AddGold(chosenOption.goldChange);
                // Мораль намеренно не меняется. Она — постоянная черта души,
                // а не шкала, которую двигает исход задания: добрый может
                // пойти на злое дело по весомой причине (верность, прошлое),
                // но своего мнения о добре и зле при этом не меняет.
                // Поэтому цена выбора против морали ложится на верность,
                // репутацию и память, а не на саму мораль.
            }

            isCompleted = true;
        }
    }
}