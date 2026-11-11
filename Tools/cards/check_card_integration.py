#!/usr/bin/env python3
from pathlib import Path
r = Path(__file__).resolve().parents[2]
table = (r/"Assets/Scripts/Gameplay/CardTable.cs").read_text(encoding="utf-8")
rules = (r/"Assets/Scripts/Gameplay/CampCardRules.cs").read_text(encoding="utf-8")
gear = (r/"Assets/Scripts/UI/GearPanel.cs").read_text(encoding="utf-8")
pause = (r/"Assets/Scripts/UI/PauseMenu.cs").read_text(encoding="utf-8")
assert "new CampCardRound(seats, seed)" in table
assert "CampCardRound.WantsCard(hand.Sin, hand.Score)" in table
assert "if (Input.GetKeyDown(KeyCode.Space)) HumanMove(true);" in table
assert "HumanMove(false)" in table
assert "GUI.Box(" in table and "GUILayout.Button" in table
assert "MemoryProcessor.Instance.CreateMemory" in table
assert "if (_playedByHuman && _round.Hands.Count > 1" in table
assert "F — карты: смотреть или сыграть" in gear
assert "Gameplay.CardTable.OwnsEscape" in pause
assert 'Loc.T("Ставлю медяк' not in table
assert "foreach (var hand in _hands) hand.Cards.Add(_deck[_drawn++])" in rules
print("PASS: cards wired to physical NPC turns, real player actions, UI, AOS memory and Escape capture")
