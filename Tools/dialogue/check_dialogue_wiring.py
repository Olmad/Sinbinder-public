#!/usr/bin/env python3
"""Structural assertions for contextual dialogue wiring; not a Unity PlayMode test."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
v = (root / "Assets/Scripts/Dialogue/VillageLines.cs").read_text(encoding="utf-8")
t = (root / "Assets/Scripts/Dialogue/DialogueTrigger.cs").read_text(encoding="utf-8")
c = (root / "Assets/Scripts/Dialogue/CampLines.cs").read_text(encoding="utf-8")
assert "VillageDialogueRules.SinAnswer(a.Soul.Sin, b.Soul.Sin)" in v
assert "VillageDialogueRules.LordAnswer(b.Soul.Sin, tone)" in v
assert "VillageDialogueRules.TopicIndex(k)" in v
assert "CampLines.Mute(a)" in v and v.count("CampLines.Mute(b)") >= 4
assert "SinAnswer(Warrior b)" not in v
assert 'if (situation == "JOIN_01"' in t
begin = t.index("private string GetDialogueLine(")
end = t.index("\n        }", begin)
method = t[begin:end]
assert method.index('if (situation == "JOIN_01"') < method.index("EncounterLines.Say(")
assert 'Loc.T("Понял.")' in c
print("PASS: source wiring uses contextual village responses, mute gestures and JOIN_01")
