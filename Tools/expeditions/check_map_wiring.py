from pathlib import Path
root=Path(__file__).resolve().parents[2]
def src(path): return (root/path).read_text(encoding="utf-8")
p=src("Assets/Scripts/Gameplay/PersonalExpedition.cs")
m=src("Assets/Scripts/Gameplay/ExpeditionMapBuilder.cs")
ui=src("Assets/Scripts/UI/MissionMapUI.cs")
bag=src("Assets/Scripts/Gameplay/FallenBag.cs")
save=src("Assets/Scripts/Core/SaveSystem.cs")
s=src("Assets/Scripts/Core/SaveGame.cs")
board=src("Assets/Scripts/Crypt/MissionBoard.cs")
# A resolved peaceful caravan must not spawn its wagons on re-entry.
# Assert the actual state predicate and the builder's selective removal,
# not the obsolete single-argument spelling.
assert (
    "ExpeditionMapBuilder.Build(plan, "
    "plan.Kind == ExpeditionMapKind.SaltRoad && visit._state.CaravanPassed)"
) in " ".join(p.split())
assert "Build(ExpeditionMapPlan plan, bool caravanGone = false)" in m
assert "if (caravanGone && p.Kind == ExpeditionPropKind.Wagon)" in m
assert "new GameObject" in m and "surface.BuildNavMesh()" in m
assert "NavMesh.SamplePosition(entrance, out _, 3f, NavMesh.AllAreas)" in m
assert "surface.RemoveData();" in m
assert "if (NavMesh.SamplePosition(point, out var safe, 3f, NavMesh.AllAreas))" in p
assert "WarriorRig.Attach(go)" in p and "sceneSetup?.SetupWarrior(go)" in p
assert "ExpeditionMapPlan.ForName(mission.Name)" in p
# Personal caravan junction must persist a non-loot peaceful result and
# never award the wagon cargo after the encounter has been resolved.
assert "public bool CaravanPassed;" in p
assert "CaravanPassed=state.CaravanPassed" in p
assert "_state.CaravanPassed=true;" in p
assert "_state.CargoTaken=true;" in p
assert "_state.EnemiesRemaining=0;" in p
assert "_state.CaravanAssaulted=true;" in p
assert "CaravanAssaulted=state.CaravanAssaulted" in p
assert "private void OpenCaravanChoice()" in p
# 26.11 (автор: «выбор во время диалога, как в Ведьмаке 3»): окно с двумя кнопками заменено
# разговором со старшим охраны (CaravanStories): решение — ответом, исход — когда разговор закрыт.
assert "private void EndTalk()" in p and "private void LetCaravanGo(int coins)" in p
# Agreed peaceful departure is a resolved narrative outcome, not a player order.
# Disable the scheduler that calls Decide/Execute, then invalidate any old command
# and move using the existing NavMesh pipeline. Combat/ordinary AOS stay untouched.
departure = p.split("private void LetCaravanGo(int coins)", 1)[1].split("public static string TalkNow", 1)[0]
assert "guard.IssueCommand(" not in departure
assert "_state.CaravanPassed=true;" in departure and "if (!guard.IsDead)" in departure
assert "guard.GetComponent<AOS.AutoAttackAOS>()" in departure
assert departure.index("_state.CaravanPassed=true;") < departure.index("tick.enabled = false;")
assert departure.index("tick.enabled = false;") < departure.index("guard.ClearCommand();") < departure.index("guard.GetComponent<UnitMover>()?.CommandMove(")
assert "UnityEngine.Object.Destroy(guard.gameObject,14f);" in departure
assert "CartRoll.Send(part.gameObject,away)" in departure
tick = src("Assets/Scripts/AOS Engine/AutoAttackAOS.cs")
assert "void OnDisable()" in tick and "StopAllCoroutines();" in tick
assert "_wrapper.Decide()" in tick and "_wrapper.Execute(action)" in tick
warrior = src("Assets/Scripts/Gameplay/Warrior.cs")
clear = warrior.split("public void ClearCommand()", 1)[1].split("/// <summary>", 1)[0]
assert "CommandRevision++" in clear and "_command = default;" in clear
print("PASS: peaceful departure is not a player order; scheduler and stale orders are stopped before movement")
assert "UI.TalkView.Show(" in p and "CaravanStories.Get(" in p
# The caravan is a PLAYABLE choice, not a fake two-button dialogue
# while hostile guards are already attacking the player. A returning hunter
# has a separate reason to be hostile, even on a peaceful caravan route.
assert "bool hunter=was!=null && was.Reason==Survivors.Hunt;" in p
assert (
    "Team disposition=!hunter && _plan.Kind==ExpeditionMapKind.SaltRoad && "
    "!_state.CaravanAssaulted && !_state.CaravanPassed ? Team.Neutral : Team.Enemy;"
) in " ".join(p.split())
assert "w.Initialize(soul,shell,relations,false,disposition," in p
assert "private void ProvokeCaravan()" in p
assert "visit._state.EnemiesRemaining<mission.Guards" in p
assert "Вы напали на обоз: охрана теперь враждебна" in p
assert "Охрана не нападёт первой" in p
assert "guard.Team=Team.Enemy;" in p
assert "CombatManager.Instance.UpdateTeam(body,Team.Enemy);" in p
assert "public static void OnGuardAttacked(Warrior victim, GameObject attacker)" in p
assert "attacker.GetComponentInParent<Warrior>()" in p
damage=src("Assets/Scripts/Gameplay/Damageable.cs")
combat=src("Assets/Scripts/Gameplay/CombatManager.cs")
assert "PersonalExpedition.OnGuardAttacked(_warrior,attacker);" in damage
assert "else RegisterEnemyUnit(unit);" in combat
assert "public void RegisterEnemyUnit(Damageable unit) => Register(unit, _enemyUnits);" in combat
assert "_state.CaravanPassed=true;" in p and "_state.CaravanAssaulted=true;" in p
assert "expedition.OpenCaravanChoice" in p
assert "(_state.CargoTaken && !_state.CaravanPassed)" in p
assert "if(_state.CaravanPassed)" in p
assert "PersonalExpedition.Enter(_chosen" in ui
assert "viewport.gameObject.AddComponent<RectMask2D>()" in ui
assert "_missionScroll = viewport.gameObject.AddComponent<ScrollRect>()" in ui
assert "_rows.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, required)" in ui
assert "_missionScroll.verticalNormalizedPosition = 1f" in ui
gear=src("Assets/Scripts/UI/GearPanel.cs")
assert "PersonalExpedition.OfferInteractions(Offer);" in gear
assert "public static void OfferInteractions(" in p
assert "FallenBag.Near()" not in p
assert "Input.GetKeyDown(KeyCode.F)" not in p
availability = ui.split("private bool CanGo(Mission m)", 1)[1].split("private void ShowCard", 1)[0]
assert (
    "bool personalPossible = ExpeditionMapPlan.ForName(m.Name) != null && "
    "SinbinderPlayer.Exists && !SinbinderPlayer.Instance.IsDead; "
    "return _board.Ready() > 0 || personalPossible;"
) in " ".join(availability.split())
assert 'bool can = CanGo(m);' in ui and '(Loc.T("Продолжить"), can, () => PickCommander(m))' in ui
assert "SquadRoster.CanGo(member)" in p
assert "seen.Count + 1 > Mathf.Max(1, mission.Squad)" in p
assert "Экспедиция" in p and "Exit()" in p
assert "PersonalExpedition.CurrentMapKey" in bag
assert "_active.Exit(commitResult:false)" in p
assert "if (commitResult)" in p
assert "private void Exit(bool commitResult)" in p
assert "if (commitResult && _state != null)" in p
assert "Gameplay.PersonalExpedition.BeforeSaveOrLoad()" in save
assert "Gameplay.PersonalExpedition.RestoreProgress(save.PersonalExpeditions)" in save
assert "public List<Sinbinder.Gameplay.SavedPersonalExpedition> PersonalExpeditions" in s
assert "public void RecordPersonal(Mission mission" in board
assert 'GameObject.CreatePrimitive(type)' in m
print("PASS: two real navigable personal maps, AOS enemies, virtual bag map, UI and saves wired")
