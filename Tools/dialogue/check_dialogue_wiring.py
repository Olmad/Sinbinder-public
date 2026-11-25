#!/usr/bin/env python3
"""Structural assertions for contextual dialogue wiring; not a Unity PlayMode test."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
v = (root / "Assets/Scripts/Dialogue/VillageLines.cs").read_text(encoding="utf-8")
t = (root / "Assets/Scripts/Dialogue/DialogueTrigger.cs").read_text(encoding="utf-8")
c = (root / "Assets/Scripts/Dialogue/CampLines.cs").read_text(encoding="utf-8")


def method_body(source, name):
    declaration = re.search(rf"^        (?:private|public|internal) [^\n]+\b{name}\(", source, re.MULTILINE)
    assert declaration is not None, f"Missing dialogue method: {name}"
    start = declaration.end()
    return source[start:source.index("\n        }", start)]


assert "VillageDialogueRules.SinAnswer(a.Soul.Sin, b.Soul.Sin)" in v
assert "VillageDialogueRules.LordAnswer(b.Soul.Sin, tone)" in v
assert "VillageLordTone.Undecided" in v
assert "Греховод заходил. Звал за собой." not in v
assert "Опять этот Греховод тут ходит" not in v
assert "SinOpening(a, b)" in v
assert "Grammar.Pick(b.Gender" in v
assert "VillageDialogueRules.TopicIndex(k)" in v
assert "CampLines.Mute(a)" in v and v.count("CampLines.Mute(b)") >= 4
assert "SinAnswer(Warrior b)" not in v
assert 'if (situation == "JOIN_01"' in t
method = method_body(t, "GetDialogueLine")
assert method.index('if (situation == "JOIN_01"') < method.index("EncounterLines.Say(")
assert 'public static string Acknowledge(Warrior w)' in c
assert 'Grammar.Pick(w.Gender' in c
assert 'CampLines.Acknowledge(b)' in v
print("PASS: source wiring uses contextual village responses, mute gestures and JOIN_01")

ui = (root / "Assets/Scripts/UI/DialogueUI.cs").read_text(encoding="utf-8")
expedition = (root / "Assets/Scripts/Gameplay/PersonalExpedition.cs").read_text(encoding="utf-8")
assert re.search(r"\binternal\s+(?:Gameplay\.)?PersonalExpedition\s+Expedition\b", t), (
    "Dialogue lines must carry their expedition visit"
)
assert "DialogueVisit" in expedition and "DialogueVisit" in t, (
    "Trigger must capture the current expedition visit when it starts a conversation"
)
for name, situation in (("StartDialogue", "PRE_01"), ("AddJoiner", "JOIN_01")):
    body = method_body(t, name)
    assert situation in body and re.search(r"\bExpedition\s*=\s*\w+", body), (
        f"{situation} lines must be stamped before they are published"
    )
joiners = method_body(t, "CheckForJoiners")
assert "ReferenceEquals(expedition, PersonalExpedition.DialogueVisit)" in joiners, (
    "Delayed replies must retain the captured expedition visit and validate it"
)

for name in ("OnDialogueStart", "OnLineAdded"):
    body = method_body(ui, name)
    assert "QueueAmbient(" in body and "Expedition" in body, (
        f"{name} must route expedition lines away from the cinematic queue"
    )
ambient = method_body(ui, "ShowAmbientDialogue")
enqueue = method_body(ui, "QueueAmbient")
assert "SpeechBubbles.SayTracked(" in ambient and "BattleLogUI" in ambient and ".Write(" in ambient
assert ("ReferenceEquals(visit, PersonalExpedition.DialogueVisit)" in ambient and
        "ReferenceEquals(line.Expedition, PersonalExpedition.DialogueVisit)" in enqueue), (
    "Ambient dialogue must discard lines from an earlier or exiting visit"
)
assert ambient.index("FogOfWar.Hides(speaker)") < ambient.index("SpeechBubbles.SayTracked(")
assert "SpeechBubbles.Withdraw(_ambientBubble)" in ambient, (
    "The visible expedition reply must be removed when its visit ends"
)
for call in ("SaveCameraPosition(", "FocusConversation(", "RestoreCamera(", "Pause(", "Resume("):
    assert call not in ambient and call not in enqueue, f"Ambient dialogue must not call {call}"

cinematic = method_body(ui, "ShowDialogue")
assert (cinematic.index("SaveCameraPosition(") < cinematic.index("Pause(") <
        cinematic.index("FocusConversation(") < cinematic.index("RestoreCamera(") <
        cinematic.index("Resume(")), "Regular dialogue must preserve the cinematic lifecycle"
cut = method_body(ui, "Cut")
assert "CutAmbient();" in cut[:cut.index("if (!_isShowing)")], (
    "Cut must cancel ambient dialogue even when a cinematic dialogue is absent"
)
cut_ambient = method_body(ui, "CutAmbient")
assert "_ambientQueue.Clear()" in cut_ambient and "SpeechBubbles.Withdraw(_ambientBubble)" in cut_ambient
print("PASS: expedition dialogue uses visit-bound speech and preserves regular cinematic dialogue")
