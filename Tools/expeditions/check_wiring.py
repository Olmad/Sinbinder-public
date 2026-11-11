from pathlib import Path
r = Path(__file__).resolve().parents[2]
m = (r/"Assets/Scripts/Crypt/MissionBoard.cs").read_text(encoding="utf-8")
h = (r/"Assets/Scripts/Gameplay/SoulHarvester.cs").read_text(encoding="utf-8")
ev = (r/"Assets/Scripts/Gameplay/MapEvents.cs").read_text(encoding="utf-8")
catalog = (r/"Assets/Scripts/Crypt/MissionCatalog.cs").read_text(encoding="utf-8")
assert "BringSoul(" not in m, "automatic missions must not award synthetic souls"
assert "CryptUpgrades.Bring(" not in m, "auto-squads cannot bring hidden secret upgrades"
assert "GetComponent<SinbinderPlayer>()" in h, "non-player warrior must be rejected"
assert "_warrior is SinbinderPlayer" in h
assert "bool present = me && SinbinderPlayer.Exists" in ev
assert "ExpeditionFind.Soul, present" in ev
assert "ExpeditionFind.Secret, present" in ev
assert "На месте есть души, но нынешние автоматические отряды" in catalog
print("PASS: automatic missions cannot collect souls/secrets, player harvest gate wired")

rival=(r/"Assets/Scripts/Gameplay/RivalLord.cs").read_text(encoding="utf-8")
auto=(r/"Assets/Scripts/Gameplay/ExpeditionAutoLoot.cs").read_text(encoding="utf-8")
assert "TryHarvestSoul(" not in rival, "rival lords must not harvest souls while forbidden"
assert "ExpeditionAutoLoot.Allocate(coin,witnesses)" in m
assert "shares.AddRange(split.PersonalCoins)" in m
assert "SquadRoster.CreditPocket(share.Name,share.Coins)" in m
assert "ExpeditionGreed.Greed" in m and "ExpeditionGreed.Envy" in m
assert "ReturnedCoins" in auto and "PersonalCoins" in auto
