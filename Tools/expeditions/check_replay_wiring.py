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

# Round number is the genuine AOS turn, not fabricated spatial timing.
resolver=(r/"Assets/Scripts/AOS Engine/AutoBattleResolver.cs").read_text(encoding="utf-8")
battle=(r/"Assets/Scripts/AOS Engine/BattleRecord.cs").read_text(encoding="utf-8")
archive=(r/"Assets/Scripts/Gameplay/ExpeditionReplayArchive.cs").read_text(encoding="utf-8")
assert "public int Round;" in battle and "Round = round" in resolver
assert "public int Round;" in data
assert "ev.HealDealt," in board and "ev.Round,ev.TargetName,true,ev.Resolved" in board
assert "frame.Damage,frame.Healing,frame.Round" in board
assert "Round = frame.Round" in archive
assert 'frame.Round>0 ? "\\nХод AOS: "+frame.Round' in viewer
print("PASS: factual AOS round provenance retained through save and replay")

# An AOS BattleEvent's attempted attack power is not the actual HP removed.
# Replay remembers source GUID AND human-readable target, the actual HP loss
# after protection, execution status and a genuinely observed killing blow.
facts=(r/"Assets/Scripts/Gameplay/ExpeditionCombatFacts.cs").read_text(encoding="utf-8")
assert "public string TargetName;" in battle
assert "public bool TargetKilled;" in battle
assert "public float TargetHealthAfter;" in battle
assert "float before=target.HP;" in resolver
assert "target.TakeDamage(warrior.Attack);" in resolver
assert "ExpeditionCombatFacts.ActualDamage(before,target.HP)" in resolver
assert "ExpeditionCombatFacts.ActualHealing(before,ally.HP)" in resolver
assert "evt.TargetName=target.DisplayName;" in resolver
assert "evt.TargetKilled=wasAlive && target.IsDead;" in resolver
assert "Math.Max(0f, beforeHp-afterHp)" in facts
assert "Math.Max(0f, afterHp-beforeHp)" in facts
assert "ev.TargetName,true,ev.Resolved" in board
assert "frame.TargetName,frame.HasOutcome,frame.Resolved" in board
assert "public bool HasOutcome;" in data
assert "public bool TargetKilled;" in data
assert "public float TargetHealthAfter;" in data
assert "TargetName = frame.TargetName" in archive
assert "HasOutcome = frame.HasOutcome" in archive
assert "TargetKilled = frame.TargetKilled" in archive
assert "ObservedEffect(frame)" in viewer
assert "if(frame.TargetKilled)" in viewer
assert "frame.HasOutcome" in viewer
assert "ev.DamageDealt.ToString" in retelling
assert "ev.HealDealt.ToString" in retelling
print("PASS: actual HP deltas, target names and killing blows persisted through AOS and replay")
