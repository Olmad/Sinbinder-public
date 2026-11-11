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
assert "ExpeditionMapBuilder.Build(plan)" in p
assert "new GameObject" in m and "surface.BuildNavMesh()" in m
assert "WarriorRig.Attach(go)" in p and "sceneSetup?.SetupWarrior(go)" in p
assert "ExpeditionMapPlan.ForName(mission.Name)" in p
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
assert "ready > 0 || personalPossible" in ui
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
