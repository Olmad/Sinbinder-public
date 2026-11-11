#!/usr/bin/env python3
from pathlib import Path
r = Path(__file__).resolve().parents[2]
table = (r/"Assets/Scripts/Gameplay/CardTable.cs").read_text(encoding="utf-8")
rules = (r/"Assets/Scripts/Gameplay/CampCardRules.cs").read_text(encoding="utf-8")
gear = (r/"Assets/Scripts/UI/GearPanel.cs").read_text(encoding="utf-8")
pause = (r/"Assets/Scripts/UI/PauseMenu.cs").read_text(encoding="utf-8")
assert "new CampCardRound(seats, seed)" in table
assert "CampCardRound.WantsCard(hand.Sin, hand.Score, RecentCardBias(npc))" in table
assert 'memory.EventType != "CardsLostToSinbinder"' in table
assert "if (Input.GetKeyDown(KeyCode.Space)) HumanMove(true);" in table
assert "HumanMove(false)" in table
assert "GUI.Box(" in table and "GUILayout.Button" in table
assert "MemoryProcessor.Instance.CreateMemory" in table
assert "CampCardPot.TryOpen(wallets, requestedStake, out var pot)" in table
assert "_pot.Settle(winners)" in table
assert "_pot?.Refund()" in table
assert "public void CancelForDeath(Warrior warrior)" in table
assert "public static void BeforeSaveOrLoad()" in table
assert "SquadRoster.CreditPocket(_name, amount)" in table
assert "CampCardRound.MaxStake(warrior.Soul.Sin, warrior.PocketGold)" in table
assert "if (Watching) Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(_status);" in table
assert "_joinNext" in table and "Сыграть после этой раздачи" in table
assert "if (_playedByHuman && _round.Hands.Count > 1" in table
assert "F — карты: смотреть или сыграть" in gear
assert "Gameplay.CardTable.OwnsEscape" in pause
assert 'Loc.T("Ставлю медяк' not in table
assert "foreach (var hand in _hands) hand.Cards.Add(_deck[_drawn++])" in rules
print("PASS: cards wired to physical NPC turns, real player actions, UI, AOS memory and Escape capture")
