#!/usr/bin/env python3
"""Static integrity checks for multiplayer QA content and the original demo.

No Unity installation is needed. This checks assets as shipped and catches
an accidentally missing/misregistered scene that C# compilation cannot see.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
scenes = ROOT / "Assets/Scenes/Tug"
original = (scenes / "Field.unity").read_bytes()
replica = (scenes / "Multiplayer_Test.unity").read_bytes()

assert original.startswith(b"%YAML 1.1"), "Original Field scene not Unity YAML"
assert replica == original, (
    "Mechanics test scene drifted from Field: review references explicitly"
)
assert b"MapLoader" in replica or b"m_EditorClassIdentifier" in replica, (
    "Expected original scene data missing"
)

meta = (scenes / "Multiplayer_Test.unity.meta").read_text("utf-8")
assert "guid: 82df2b7319bc4ab393db801f19f3b580" in meta
assert "168dd53c90aa7de4aa9eeb0feee559f2" not in meta

build = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text("utf-8")
assert "path: Assets/Scenes/Prologue_Camp.unity" in build
assert "path: Assets/Scenes/Tug/Multiplayer_Test.unity" in build
assert "guid: 82df2b7319bc4ab393db801f19f3b580" in build
enabled = re.findall(r"-\s+enabled:\s+([01])\s+path:\s+(\S+)", build)
assert enabled[0] == ("1", "Assets/Scenes/Prologue_Camp.unity"), (
    "Start scene changed: protect the existing single-player demo"
)
assert ("1", "Assets/Scenes/Tug/Multiplayer_Test.unity") in enabled

loader = (ROOT / "Assets/Scripts/Gameplay/MapLoader.cs").read_text("utf-8")
assert '== "Multiplayer_Test"' in loader
assert "MultiplayerTestMap.Choose()" in loader
entry = (ROOT / "Assets/Scripts/Multiplayer/SinbinderLanBootstrap.cs").read_text("utf-8")
assert "SceneManager.LoadScene(\"Multiplayer_Test\")" in entry
assert '"-mp-test-map"' in entry

altar = (ROOT / "Assets/Scripts/Gameplay/AltarVisualEnhancer.cs").read_text("utf-8")
assert "Object.Destroy(collision)" in altar
assert "collision.enabled = false" in altar
assert "BindingDevice" in altar
assert "Altar" in altar
assert "UnityEditor" not in altar

metas = {
    "Assets/Scripts/Core/MultiplayerTestMap.cs.meta": "04b1ad50829e46bda6e0e8adac13f623",
    "Assets/Scripts/Gameplay/AltarVisualEnhancer.cs.meta": "12b92499cd714343b7a9acf0254d4e31",
    "Assets/Scripts/Multiplayer/MatchAuthority.cs.meta": "1a42d071f99e4e87b44dfc39a47c0278",
    "Assets/Scripts/Multiplayer/NetworkActorIdentity.cs.meta": "6e48768cf44b44d29702b209364ca6ea",
    "Assets/Scripts/Multiplayer/ServerWarriorOrderBridge.cs.meta": "5e2c63ab41e64ec7bc42d209fd2bf358",
    "Assets/Scripts/Multiplayer/ServerWorldProbe.cs.meta": "aab31050a69d4fb5a8df740628e175bd",
    "Assets/Scripts/Multiplayer/LanGameOrderCodec.cs.meta": "78e541dab92d472ebc396de0aa3a67fa",
    "Assets/Scripts/Multiplayer/LanWorldState.cs.meta": "b5d89ad227d54ef4bac99d518ae279fa",
    "Assets/Scripts/Multiplayer/LanLatencyFairness.cs.meta": "82dc47f114754b79a254db154f9dc2b0",
    "Assets/Scripts/Multiplayer/LanRoomDirectory.cs.meta": "ffd062862e4c402b9f14b95fb5461d10",
    "Assets/Scripts/Multiplayer/LanServerBookmarks.cs.meta": "e80acdf90ca34d20ac09acba91f89c08",
    "Assets/Scripts/Multiplayer/LanWorldAutoVerifier.cs.meta": "88c36a74ac8c4faba7ec4ec2d047abc1",
    "Assets/Scenes/Tug/Multiplayer_Test.unity.meta": "82df2b7319bc4ab393db801f19f3b580",
}
all_seen = set()
for name, expected in metas.items():
    actual = re.search(r"guid:\s*([0-9a-f]{32})", (ROOT / name).read_text("utf-8"))
    assert actual and actual.group(1) == expected, f"{name} GUID mismatch"
    assert actual.group(1) not in all_seen, f"{name} GUID duplicated"
    all_seen.add(actual.group(1))

# The multiplayer bridge must preserve the real AOS order API rather
# than bypassing it with a duplicate, client-authoritative AI.
warrior = (ROOT / "Assets/Scripts/Gameplay/Warrior.cs").read_text("utf-8")
bridge = (ROOT / "Assets/Scripts/Multiplayer/ServerWarriorOrderBridge.cs").read_text("utf-8")
authority = (ROOT / "Assets/Scripts/Multiplayer/MatchAuthority.cs").read_text("utf-8")
assert "string lord = null)" in warrior and "IssuePatrol(Vector3 to" in warrior
assert "warrior.IssueCommand(" in bridge and "warrior.IssuePatrol(" in bridge
assert "NetworkActorRole.NeutralWarrior" in authority
assert "AuthenticatedConnectionId" not in authority  # identity comes from socket context
assert "UnityEditor" not in authority
server = (ROOT / "Assets/Scripts/Multiplayer/LanRoomServer.cs").read_text("utf-8")
client = (ROOT / "Assets/Scripts/Multiplayer/LanRoomClient.cs").read_text("utf-8")
probe = (ROOT / "Assets/Scripts/Multiplayer/ServerWorldProbe.cs").read_text("utf-8")
assert '"gameOrder"' in server and "RouteGameOrder(peer.Id, intent)" in server
assert "LanGameOrderCodec.Write(intent)" in client
assert '"-mp-world-host"' in loader and '"-mp-world-host"' in probe
assert "-mp-world-host" in (ROOT / "Test-World-LAN.cmd").read_text("utf-8")
assert "GameOrderWireChecks.Run()" in (ROOT / "Tools/server/UnityLanHarness/Program.cs").read_text("utf-8")
assert "WorldSnapshotChecks.Run()" in (ROOT / "Tools/server/UnityLanHarness/Program.cs").read_text("utf-8")
assert "PrivateWorldActors" in server and "PublishPrivateWorldSnapshot" in server
assert "CanObserveActorOnServer" in probe and "GetPrivateWorldActors" in probe
rival = (ROOT / "Assets/Scripts/Gameplay/RivalLord.cs").read_text("utf-8")
assert "public void SetNetworkControlled(bool controlled)" in rival
assert "if (NetworkControlled) return;" in rival
assert "SetNetworkControl(entry.Warrior, true)" in probe
assert "SetNetworkControl(entry.Warrior, false)" in probe
assert "Scan(UnityEngine.Object.FindObjectsByType<Warrior>" in probe
assert "worldBegin" in client and "worldEnd" in client
assert "LatencyFairnessChecks.Run()" in (ROOT / "Tools/server/UnityLanHarness/Program.cs").read_text("utf-8")
assert "RoomDirectoryChecks.Run()" in (ROOT / "Tools/server/UnityLanHarness/Program.cs").read_text("utf-8")
assert "ServerBookmarksChecks.Run()" in (ROOT / "Tools/server/UnityLanHarness/Program.cs").read_text("utf-8")
assert "RoomJoinStatus" in (ROOT / "Assets/Scripts/Multiplayer/LanRoomDirectory.cs").read_text("utf-8")
assert "Rfc2898DeriveBytes" in (ROOT / "Assets/Scripts/Multiplayer/LanRoomDirectory.cs").read_text("utf-8")
assert "ExtraInputDelayTicks(connectionId)" in probe
assert '"latencyReply"' in server and '"latencyProbe"' in server
assert "LanServerBookmarks.Load()" in entry
assert "mp-ping-balance" in (ROOT / "Test-World-LAN.cmd").read_text("utf-8")
assert "mp-ping-balance" in (ROOT / "Test-World-Auto.ps1").read_text("utf-8")
# Verify that the manual Windows Unity build cannot silently claim success
# after Unity failed or robocopy returned an error. This is only a static
# contract; a real Windows + Unity Player build is still required.
build_script = (ROOT / "Tools/play/build-game.ps1").read_text("utf-8-sig")
assert "if ($p.ExitCode -ne 0)" in build_script
assert "Test-Path -LiteralPath $sourceExe -PathType Leaf" in build_script
assert "Test-Path -LiteralPath $sourceData -PathType Container" in build_script
assert "$copyCode = $LASTEXITCODE" in build_script
assert "if ($copyCode -ge 8)" in build_script  # robocopy: 0..7 nonfatal, >=8 failed
assert "Test-Path -LiteralPath $destExe -PathType Leaf" in build_script
assert "Test-Path -LiteralPath $destData -PathType Container" in build_script
assert build_script.index("if ($copyCode -ge 8)") < build_script.index('Write-Host "Готово за')
print("PASS: Windows author build checks Unity process, output and robocopy before success")
# Regression: Unity can reuse Sinbinder.exe while updating its game data.
# An old executable timestamp is not build provenance. The author script must
# write its marker AFTER validating Unity output and copy success; the LAN
# acceptance script must reject absent markers and later source/scene edits.
auto_test = (ROOT / "Test-World-Auto.ps1").read_text("utf-8-sig")
assert "$buildMarker = Join-Path" in build_script
assert "Remove-Item -LiteralPath $buildMarker" in build_script
assert "WriteAllText($buildMarker" in build_script
assert build_script.index("if ($copyCode -ge 8)") < build_script.index("WriteAllText($buildMarker")
assert "Copy-Item -LiteralPath $buildMarker" in build_script
assert "Test-Path -LiteralPath $marker -PathType Leaf" in auto_test
assert "TryParseExact(" in auto_test
assert "LastWriteTimeUtc -gt $builtAt" in auto_test
assert "'Project input changed after verified Unity build:" in auto_test
assert "Sinbinder_Data" in auto_test
assert "LastWriteTimeUtc -lt $latestScript.LastWriteTimeUtc" not in auto_test
print("PASS: Unity acceptance freshness uses verified author-build marker, not launcher mtime")



# Regression: a non-attack must NOT consume a pending rescue guard. Previously
# Watched.Remove(target) ran before IsRealAttack, so a zero-damage probe could
# disarm the rescue and leave the victim exposed to the next genuine hit.
bodyguard = (ROOT / "Assets/Scripts/Gameplay/Bodyguard.cs").read_text("utf-8-sig")
absorb = bodyguard.split("public static bool Absorb(", 1)[1].split("public static int Blocks", 1)[0]
invalid_gate = "if (w.Rescue && !RescueCreditGate.IsRealAttack(damage, attacker != null)) return false;"
remove_watch = "Watched.Remove(target);"
assert invalid_gate in absorb and remove_watch in absorb, "Rescue guard integrity checks missing"
assert absorb.index(invalid_gate) < absorb.index(remove_watch), (
    "Invalid damage probe consumes rescue watch before a real attack"
)
print("PASS: invalid rescue probes cannot disarm a pending guard")

print("PASS: original demo, map, altar, AOS and real-warrior LAN transport contracts")
