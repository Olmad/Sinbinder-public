# Auditor 5: stage 1 independent QA checkpoint (2026-11-20)

Branch: `feature/server-prototype`. Observed HEAD before writing: `93044b6f84a04f9cca2cf6c5315f97b8893fbbb3`. **Stage 1: NO-GO / not accepted.** This is an evidence checkpoint, not a completed audit or Unity runtime validation. The historic 582-source TSV remains unchanged.

## New independent source review: delta MapLogicRunner.cs
Source: `Assets/Scripts/Gameplay/MapLogicRunner.cs`; blob `f7c502940337c7df33c1bf3f9adbc37c71163c2f`. Read full file. Player-facing paths:
- `OfferInteractions`: enabled `OnInteractObject` trigger, nonempty TargetId, target registered and within 3 units, condition allowed, not fired when Once; offers `Loc.T("F — взаимодействовать")` through shared interaction callback. The message is *not* unconditional; competing interaction priorities and actual rendering are runtime UNKNOWN.
- `Fire` from `Start` (`OnStart`), `Update` region entry (`OnEnterRegion`), or F callback (`OnInteractObject`), subject to Enabled, matching IDs, Once and `AllowedCondition`; `Apply` `ShowMessage` accepts nonblank `t.Message` length <=240 and calls `Herald.Line(Loc.T(t.Message))`. Thus user-authored JSON can become player-facing dialogue without any fixed source literal or guaranteed EN translation. The actual JSON triggers, author, facts, NPC, grammatical context and EN key coverage have **not** been enumerated. Classifying this file EXCLUDED_TECH would be incorrect.
- Negative/source-level counterexamples: empty `t.Message` or length 241 is rejected; unknown condition returns false; `OnInteractObject` with missing registered object cannot offer interaction. Conversely `Condition=Always`, `Action=ShowMessage`, nonempty message and matching event/target can emit an arbitrary map-authored phrase. No unconditional claim about actual gameplay is warranted.
- QA2: source=Assets/Scripts/Gameplay/MapLogicRunner.cs; blob=f7c502940337c7df33c1bf3f9adbc37c71163c2f; lines=OfferInteractions,Fire,AllowedCondition,Apply; context=OnStart/OnEnterRegion/OnInteractObject plus trigger conditions; negative=blank or >240-char message rejected and unknown condition rejected; verified=0; total=2; RU_EN=F prompt source identified, dynamic JSON messages UNKNOWN; outcome=UNKNOWN.
- Verdict: **UNKNOWN_CONTEXT** for the two text-emission pathways, **not PASS**. New source READ: 1; new semantic verified: 0; source-level runtime unknown: 2 pathways. Current baseline-582 counts not recalculated.

## BattleNarrator reinspection
Source: `Assets/Scripts/AOS Engine/BattleNarrator.cs`, blob `974273d50701ef0676fb8390cadb72b4c83ff5e8`; entire `Build` method read. Harness: `Tools/logic/battle-narrator/Program.cs`, blob `e14076647660b87b68e395a0cb8c1d6a4077260e`; actual production localization: `Assets/Resources/Lang/en.txt`, blob `413688e1522d7f0f95ae42fd04806170b157caf5`. Confirmed source branch: null/empty/all-blank return `Loc.T("Записей о ходе боя нет.")`; nonblank journal entries are reproduced without inference of obedience or negotiation; adjacent identical entries are compressed, with whitespace-sensitive equality. Current en.txt has the explicit pair `Записей о ходе боя нет.` / `No battle events were recorded.`. This pair is semantically appropriate for an empty journal but does not prove the real combat caller supplies a complete journal.

Independent executable C# harness was **not run** here; its 8 assertions were read, not independently executed. Prior CI 44499180002 is historical evidence only. **QA_ACCEPTED_SOURCE withheld / QA_UNKNOWN** pending independent executable run and tracing actual combat caller. Unity runtime UNKNOWN. This is not a new semantic PASS.

## Stage 1 acceptance matrix
| Critical scenario | Independent QA status | Missing evidence |
|---|---|---|
| Whisper, all branches | NOT ACCEPTED | Seven branches, actual triggers, RU/EN, unresolved historical P1 |
| SoulCourt and SoulCourtReasons | NOT ACCEPTED | Complete call/condition/Grammar.For and patch recheck |
| BattleNarrator | QA_UNKNOWN | Independent executable assertions, real combat caller, Unity |
| AOS order/decision explanations | NOT ACCEPTED | Caller, truth of cause, actor and memory |
| Expeditions | NOT ACCEPTED | Full outcomes/replay dialogue and RU/EN |
| Village/core dialogue and lore | NOT ACCEPTED | 7x7 matrix and independent patch #4 tests |
| Dream/tutorial/Tab/V | NOT ACCEPTED | Full hints, voice/conditions, regression tests |
| Localization and changed-source delta | NOT ACCEPTED | Full key manifest and JSON trigger texts |
| 13 voices, saves and runtime regressions | UNKNOWN | No Unity execution in this checkpoint |

No QA acceptance of developer #4 patches, no resolution of disagreements among auditors #1-3, and no 100% critical scenario coverage are claimed. Stage 1 GO requires independent evidence and explicit author decision; development remains paused pending that decision.
