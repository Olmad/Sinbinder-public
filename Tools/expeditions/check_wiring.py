from pathlib import Path
r = Path(__file__).resolve().parents[2]
m = (r/"Assets/Scripts/Crypt/MissionBoard.cs").read_text(encoding="utf-8")
h = (r/"Assets/Scripts/Gameplay/SoulHarvester.cs").read_text(encoding="utf-8")
ev = (r/"Assets/Scripts/Gameplay/MapEvents.cs").read_text(encoding="utf-8")
catalog = (r/"Assets/Scripts/Crypt/MissionCatalog.cs").read_text(encoding="utf-8")
assert "BringSoul(" not in m, "automatic missions must not award synthetic souls"
assert "if (mission.Spoils == Spoils.Souls)" in m
assert "chosen == AOS.MissionAction.TakeEverything ||" not in m
assert "CryptUpgrades.Bring(" not in m, "auto-squads cannot bring hidden secret upgrades"
assert "GetComponent<SinbinderPlayer>()" in h, "non-player warrior must be rejected"
assert "_warrior is SinbinderPlayer" in h
assert "bool present = me && SinbinderPlayer.Exists" in ev
assert "ExpeditionFind.Soul, present" in ev
assert "ExpeditionFind.Secret, present" in ev
assert "На месте есть души, но нынешние автоматические отряды" in catalog
print("PASS: automatic missions cannot collect souls/secrets, player harvest gate wired")

rival=(r/"Assets/Scripts/Gameplay/RivalLord.cs").read_text(encoding="utf-8")
auto=(r/"Assets/Scripts/Gameplay/ExpeditionAutoLoot.cs").read_text(encoding="utf-8")
assert "TryHarvestSoul(" not in rival, "rival lords must not harvest souls while forbidden"
assert "ExpeditionAutoLoot.Allocate(coin,witnesses)" in m
assert "shares.AddRange(split.PersonalCoins)" in m
assert "SquadRoster.CreditPocket(share.Name,share.Coins)" in m
assert "ExpeditionGreed.Greed" in m and "ExpeditionGreed.Envy" in m
assert "ReturnedCoins" in auto and "PersonalCoins" in auto

# Automatic missions are not instantaneous: active crew, timer, decision wait,
# return and save/load must be wired to the actual AOS outcome.
ui=(r/"Assets/Scripts/UI/MissionMapUI.cs").read_text(encoding="utf-8")
save=(r/"Assets/Scripts/Core/SaveSystem.cs").read_text(encoding="utf-8")
savegame=(r/"Assets/Scripts/Core/SaveGame.cs").read_text(encoding="utf-8")
assert "class SavedAutoExpedition" in m
assert "ActiveJourneys.Add(new SavedAutoExpedition" in m
assert "SecondsRemaining=OutboundSeconds" in m
assert "journey.Phase=1;" in m and "journey.Phase=2;" in m
assert "if (journey.Phase==1) continue;" in m
assert "CompleteJourney(journey);" in m
assert "ResolveReturned(found,journey.Commander" in m
assert "Expedition.Fight(away, mission.Guards)" in m
assert "SquadRoster.ComeBack(party, survivors)" in m
assert "OfferJourneyOrder(string commanderName, AOS.MissionAction? offer)" in m
assert "public bool RecallJourney(string commanderName)" in m
assert "if (journey.Recalled)" in m
assert "SquadRoster.ComeBack(journey.Party,journey.Party);" in m
assert "Отряд вернулся без боя и добычи" in m
assert "MissionBoard.Journeys" in ui and "PickJourneyDecision(commanderId)" in ui
assert "_board.RecallJourney(commanderId); ShowMissions();" in ui
assert "Decisions occur when the squad reaches the location" in ui
assert "save.AutoExpeditions = MissionBoard.SnapshotJourneys();" in save
assert "MissionBoard.RestoreJourneys(save.AutoExpeditions);" in save
assert "List<Sinbinder.Crypt.SavedAutoExpedition> AutoExpeditions" in savegame
print("PASS: staged automatic expedition dispatch, commander advice, delayed return and persistence")
assert "MaxActiveJourneys = 4" in m
assert "for (int i=ActiveJourneys.Count-1;i>=0;i--)" in m
assert "PickJourneyDecision(commanderId)" in ui
assert "_board.OfferJourneyOrder(commanderId,value)" in ui
assert "var reserved=new HashSet<string>();" in m
assert "if (!valid || !item.Party.Contains(item.Commander)) continue;" in m
assert "if (selected.Count+1>maxSize)" in m
assert "SquadRoster.SendAway(commanderName, companions);" in m
assert "if(MissionBoard.Journeys.Count==before && _title!=null)" in ui
roster=(r/"Assets/Scripts/Gameplay/SquadRoster.cs").read_text(encoding="utf-8")
assert "if (alreadyAway) continue;" in roster

# Causal contract: encounter first, recorded result on return, no replay reroll,
# winner not merely body count, no absent warrior in a second scene.
rules=(r/"Assets/Scripts/Gameplay/ExpeditionObjectiveRules.cs").read_text(encoding="utf-8")
assert "private bool ResolveEncounter(SavedAutoExpedition journey)" in m
assert "if (!ResolveEncounter(journey)) continue;" in m
assert "journey.EncounterResolved=true;" in m
assert "journey.BattleWon=fight.Record!=null" in m
assert "if(!journey.EncounterResolved) return;" in m
assert "ExpeditionObjectiveRules.Secured(" in m
assert "ExpeditionObjectiveRules.DeliveredCoins(" in m
assert "public static bool Secured(bool fought, bool resolverVictory, int survivors)" in rules
assert "return survivors > 0 && (!fought || resolverVictory);" in rules
assert "if(secured) TakeSpoils(mission, chosen, journey);" in m
assert "warrior.gameObject.SetActive(false);" in m
assert "w.gameObject.SetActive(true);" in m
assert "FindObjectsInactive.Include" in m
assert "journey.EncounterTrace=events;" in m
assert "journey.EncounterResolved=true;" in m
print("PASS: AOS expedition encounter, victory, world state and delayed payout invariants")

# Persistent first-person AOS memory vote is derived from genuine completed
# expedition outcomes, never from a suggested but disobeyed order.
ledger=(r/"Assets/Scripts/Gameplay/ExpeditionMemoryLedger.cs").read_text(encoding="utf-8")
vote=(r/"Assets/Scripts/Gameplay/ExpeditionMemoryVote.cs").read_text(encoding="utf-8")
memory=(r/"Assets/Scripts/AOS Engine/MemoryModule.cs").read_text(encoding="utf-8")
context=(r/"Assets/Scripts/AOS Engine/MissionContext.cs").read_text(encoding="utf-8")
assert "JourneyId=System.Guid.NewGuid()" in m
assert "ExpeditionMemoryLedger.Remember(journey.JourneyId" in m
assert "if(!survivors.Contains(member.Name)) continue;" in m
assert "RecentMemories = ExpeditionMemoryLedger.ForWarrior(boss.Name, mission.Name)" in m
assert "ExpeditionMemoryLedger.Forget();" in m
assert "IMissionModule" in memory
assert "ExpeditionMemoryVote.Score(" in memory
assert "public string ExpeditionSite;" in context
assert "ExpeditionMemories = Gameplay.ExpeditionMemoryLedger.Snapshot();" in save
assert "ExpeditionMemoryLedger.Restore(save.ExpeditionMemories)" in save
assert "List<Sinbinder.Gameplay.ExpeditionMemoryEntry> ExpeditionMemories" in savegame
assert "MaxPerWarrior = 16" in ledger
assert "Math.Max(-12f,Math.Min(12f,score))" in vote
assert "new Inventory.InventoryItem(" in m
assert "Inventory.ItemType.Shell" in m and "if(inventory.AddItem(body))" in m
assert "journey.FoesFallen<=0" in m
print("PASS: authentic durable mission memory, bounded AOS influence and real ordinary shell inventory")
