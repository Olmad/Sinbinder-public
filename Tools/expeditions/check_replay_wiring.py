from pathlib import Path
r=Path(__file__).resolve().parents[2]
board=(r/"Assets/Scripts/Crypt/MissionBoard.cs").read_text(encoding="utf-8")
retelling=(r/"Assets/Scripts/Crypt/Retelling.cs").read_text(encoding="utf-8")
ui=(r/"Assets/Scripts/UI/MissionMapUI.cs").read_text(encoding="utf-8")
viewer=(r/"Assets/Scripts/UI/ExpeditionReplayViewer.cs").read_text(encoding="utf-8")
data=(r/"Assets/Scripts/Gameplay/ExpeditionReplayData.cs").read_text(encoding="utf-8")
assert 'foreach(var ev in fight.Record.Events)' in board
assert 'journey.EncounterTrace=events;' in board
assert 'journey.EncounterResolved=true;' in board
assert 'events.Append(ExpeditionReplayKind.CombatAction' in board
assert 'replay.Append(frame.Kind,frame.Actor,frame.Description,frame.Target,' in board
assert 'LastReplay = BuildReplay(' in board
assert 'SavedAutoExpedition outcome, List<string> survivors' in board
assert 'public static string Describe(BattleEvent ev)' in retelling
assert 'ExpeditionReplayViewer.Open(replay)' in ui
assert 'ExpeditionReplayArchive.Add(LastReplay)' in board
assert 'ExpeditionReplayArchive.History' in ui
assert 'private int _replayPage;' in ui
assert 'int last = history.Count - 1 - _replayPage*2;' in ui
save=(r/"Assets/Scripts/Core/SaveSystem.cs").read_text(encoding="utf-8")
assert 'save.ExpeditionReplays = Gameplay.ExpeditionReplayArchive.Snapshot()' in save
assert 'Gameplay.ExpeditionReplayArchive.Restore(save.ExpeditionReplays)' in save
assert 'ExpeditionReplays' in (r/"Assets/Scripts/Core/SaveGame.cs").read_text(encoding="utf-8")
assert 'public readonly List<ExpeditionReplayFrame>' not in data
assert 'ExpeditionReplayViewer.OwnsEscape' in ui
assert 'Time.unscaledDeltaTime' in viewer
assert 'Схематическая реконструкция' in viewer
assert 'foreach(var frame in Frames) yield return frame' in data
assert 'Frames.Count' in viewer and 'GUI.BeginScrollView' in viewer
print("PASS: automatic mission replay retains all source events and opens timeline viewer")

# AOS decision != executed effect: a fighter who actually flees leaves the
# encounter, and a converted enemy must not take a turn for the old side.
resolver=(r/"Assets/Scripts/AOS Engine/AutoBattleResolver.cs").read_text(encoding="utf-8")
battle=(r/"Assets/Scripts/AOS Engine/BattleRecord.cs").read_text(encoding="utf-8")
expedition=(r/"Assets/Scripts/Gameplay/Expedition.cs").read_text(encoding="utf-8")
assert 'public bool Resolved;' in battle
assert 'evt.Resolved = allies.Remove(warrior);' in resolver
assert 'if (enemies.Contains(enemy) && !enemy.IsDead)' in resolver
assert 'if (squad.Contains(warrior) && !warrior.IsDead)' in resolver
assert 'evt.Resolved = true;' in resolver
assert 'case ActionType.Flee:' in retelling
assert 'return ev.Resolved' in retelling
assert 'case ActionType.BribeEnemy:' in retelling
assert 'case ActionType.Loot:' in retelling
assert 'case ActionType.SaveAlly:' in retelling
assert 'var originalSquad=new List<Warrior>(squad);' in expedition
assert 'i < originalSquad.Count' in expedition
assert 'AutoBattleResolver.SquadGold=0;' in expedition
assert 'AutoBattleResolver.SquadGold=previousGold;' in expedition
assert 'var w = Summon(holder, m, relations);' in expedition
print("PASS: AOS retreat/bribe effects and expedition outcomes are distinguished from intent")
