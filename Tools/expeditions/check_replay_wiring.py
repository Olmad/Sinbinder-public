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
