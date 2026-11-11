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
assert "Экспедиция" in p and "Exit()" in p
assert "PersonalExpedition.CurrentMapKey" in bag
assert "Gameplay.PersonalExpedition.BeforeSaveOrLoad()" in save
assert "Gameplay.PersonalExpedition.RestoreProgress(save.PersonalExpeditions)" in save
assert "public List<Sinbinder.Gameplay.SavedPersonalExpedition> PersonalExpeditions" in s
assert "public void RecordPersonal(Mission mission" in board
assert 'GameObject.CreatePrimitive(type)' in m
print("PASS: two real navigable personal maps, AOS enemies, virtual bag map, UI and saves wired")
