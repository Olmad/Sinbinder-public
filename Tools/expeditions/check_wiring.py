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
assert "if (journey.Phase==0 || journey.Phase==1)" in m
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

# Combat wounds must not disappear at return, save/load or the next
# simulation, and old saves must not interpret their missing field as death.
roster=(r/"Assets/Scripts/Gameplay/SquadRoster.cs").read_text(encoding="utf-8")
fight=(r/"Assets/Scripts/Gameplay/Expedition.cs").read_text(encoding="utf-8")
body=(r/"Assets/Scripts/Gameplay/Damageable.cs").read_text(encoding="utf-8")
warrior=(r/"Assets/Scripts/Gameplay/Warrior.cs").read_text(encoding="utf-8")
rules=(r/"Assets/Scripts/Gameplay/ExpeditionHealthRules.cs").read_text(encoding="utf-8")
assert "public float HealthFraction;" in roster
assert "m.HealthFraction=ExpeditionHealthRules.Capture(w.HP,w.MaxHP)" in roster
assert "public static void ApplyExpeditionHealth(string name, float fraction)" in roster
assert "journey.SurvivorHealth=CopyHealth(fight.Health);" in m
assert "SquadRoster.ApplyExpeditionHealth(condition.Name" in m
assert "w.SetHealthFraction(ExpeditionHealthRules.RestoreFraction(m.HealthFraction));" in m
assert "w.SetHealthFraction(ExpeditionHealthRules.RestoreFraction(m.HealthFraction));" in fight
assert "public void SetHealthFraction(float fraction)" in warrior
assert "public void SetHealthFraction(float fraction)" in body
assert "HealthFraction = m.HealthFraction" in save
assert "public float HealthFraction;" in savegame
assert "if(float.IsNaN(stored)" in rules
print("PASS: AOS injuries persist through game save and subsequent expedition")

# Optional advice must not hold the entire world indefinitely:
# leaders independently resolve via the same AOS after a finite timer.
timing=(r/"Assets/Scripts/Gameplay/ExpeditionJourneyTiming.cs").read_text(encoding="utf-8")
cryptmap=(r/"Assets/Scripts/Crypt/CryptMap.cs").read_text(encoding="utf-8")
assert "public const float AdviceSeconds = 20f;" in timing
assert "public void AdvanceJourneys(float gameDelta, bool canDeliver = true)" in m
assert "AdvanceJourneys(Time.deltaTime);" in m
assert "journey.SecondsRemaining=ExpeditionJourneyTiming.Advance(" in m
assert "journey.SecondsRemaining=ExpeditionJourneyTiming.AdviceSeconds;" in m
assert "Core.SaveSystem.AutoSave();" in m
assert "if (journey.Phase==1) continue;" not in m
assert "if (journey.Phase==0 || journey.Phase==1)" in m
assert "if (!ResolveEncounter(journey)) continue;" in m
assert "_board != null && !_board.isActiveAndEnabled" in cryptmap
assert "_board.AdvanceJourneys(Time.deltaTime);" in cryptmap
assert "journey.Phase==1" in ui
assert "Старший решит сам через" in ui
print("PASS: finite commander autonomy, paused-game time, hidden table and multiple independent journeys")

# Remote world-time continuity is not a UI/crypt-only timer. One persistent
# owner drives the same real MissionBoard/AOS transitions, including while
# the player visits a different scene. Physical payout is crypt-only.
world=(r/"Assets/Scripts/Crypt/ExpeditionWorldClock.cs").read_text(encoding="utf-8")
timing=(r/"Assets/Scripts/Gameplay/ExpeditionJourneyTiming.cs").read_text(encoding="utf-8")
crypt=(r/"Assets/Scripts/Crypt/CryptMap.cs").read_text(encoding="utf-8")
assert "[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]" in world
assert "DontDestroyOnLoad(go);" in world
assert "_offscreenBoard = gameObject.AddComponent<MissionBoard>();" in world
assert "MissionBoard.Journeys.Count == 0" in world
assert "SceneManager.GetActiveScene()" in world
assert "resolver.AdvanceJourneys(Time.deltaTime," in world
assert "canDeliver: deliveryBoard != null" in world
assert "if (!ExpeditionWorldClock.IsRunning)" in m
assert "if (!ExpeditionWorldClock.IsRunning &&" in crypt
assert "public void AdvanceJourneys(float gameDelta, bool canDeliver = true)" in m
assert "ExpeditionJourneyTiming.CanSettle(canDeliver," in m
assert "if (!CanCompleteJourney(journey))" in m
assert "ActiveJourneys.RemoveAt(i);" in m
assert "return journey.Recalled || Inventory.PlayerInventory.Instance!=null;" in m
assert "public static bool CanSettle(bool inCrypt, bool inventoryReady, bool recalled)" in timing
assert "public static string SnapshotReport()" in m
assert "public static void RestoreReport(string report)" in m
assert "save.AutoExpeditionReport = MissionBoard.SnapshotReport();" in save
assert "MissionBoard.RestoreReport(save.AutoExpeditionReport);" in save
assert "public string AutoExpeditionReport;" in savegame
print("PASS: one persistent cross-scene expedition clock, crypt-only settlement, saved reports")

# Personal expeditions run inside the crypt scene as a temporary 3D map.
# Calling SaveSystem.AutoSave during them forcibly calls
# PersonalExpedition.BeforeSaveOrLoad() => Exit(). Remote auto-squads
# must neither abort personal play nor materialize soldiers on this map.
personal=(r/"Assets/Scripts/Gameplay/PersonalExpedition.cs").read_text(encoding="utf-8")
assert "if (Active) _active.Exit();" in personal
assert "Gameplay.PersonalExpedition.Active" in world
assert "if (PersonalExpedition.Active) canDeliver=false;" in m
assert "private static void SaveExpeditionProgress()" in m
assert "if (!PersonalExpedition.Active &&" in m
assert "Inventory.PlayerInventory.Instance != null)" in m
assert "!Gameplay.SinbinderPlayer.Exists && CryptMap.Board == null" in world
assert "Core.SaveSystem.AutoSave();" in m
assert "SaveExpeditionProgress();" in m
assert "Core.SaveSystem.AutoSave();" in m.split("private static void SaveExpeditionProgress()",1)[1]
assert "Core.SaveSystem.AutoSave();" not in m.split("private static void SaveExpeditionProgress()",1)[0]
print("PASS: remote journeys do not force-close personal expedition maps through autosave or delivery")

# Invalid party data cannot silently lose a completed expedition, spawn
# missing warriors, or credit rewards. The UI must explain blocked return.
assert "public bool ReturnBlocked;" in m
assert "ReturnBlocked=item.ReturnBlocked" in m
assert "journey.ReturnBlocked=true;" in m
assert "if (!CanCompleteJourney(journey))" in m
assert "Возврат и выдача добычи заблокированы" in m
assert "journey.ReturnBlocked" in ui
assert "Награды не выданы" in ui
print("PASS: invalid squad data preserves trip and exposes recovery-required status")
