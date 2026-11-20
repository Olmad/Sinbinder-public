# Auditor 5 — independent delta QA: MapLogicRunner (2026-11-19)

Branch: `feature/server-prototype`. This is a delta-only review, NOT part of the immutable 582-row baseline. No production change and no semantic PASS for the whole file.

## Source checkpoint
- `Assets/Scripts/Gameplay/MapLogicRunner.cs`, Git blob `f7c502940337c7df33c1bf3f9adbc37c71163c2f`; complete source inspected (136 physical lines, subject to terminal newline counting).
- `Assets/Resources/Lang/en.txt`, Git blob `1c98dacabcef8eeea623a81cb71fc1efd31c462a`; localization pair around lines 14582–14583 independently read.
- Scope: two player-facing output sites, not every runtime-generated message or JSON trigger.

## QA2 findings
1. `OfferInteractions`: `Loc.T("F — взаимодействовать")`, near source line 83. Condition: existing runner and map; player exists; enabled `OnInteractObject` trigger with target, unused once-trigger, allowed condition, registered target within 3 units; offers through existing interaction priority callback. RU = `F — взаимодействовать`; EN = `F — interact` (en.txt 14582–14583). Source-level RU/EN **PASS** for this exact static label; end-to-end focus selection/runtime **UNKNOWN**. Negative case: target farther than 3 units yields no offer.
2. `Apply` / `ShowMessage`: `Herald.Line(Loc.T(t.Message))`, near source line 119. Condition: enabled matching trigger, valid condition, `ShowMessage` action, non-whitespace message length <=240. **UNKNOWN_CONTEXT**: text is supplied by MapData/JSON, not a fixed literal; no per-message inventory or RU/EN/effect proven by reading this file. Negative case: nonempty 241-character message is suppressed; valid 240-character message may be displayed even if untranslated or narratively false. A translator fallback must be inspected before asserting exact output.
3. `Fire`: `OnStart` ignores region/target filter; `OnEnterRegion` and `OnInteractObject` require either `RegionId` OR `TargetId` equal to target. This is source-level behavior, not proof of a player-facing text defect. Potential wrong-target scenario remains **UNKNOWN** pending trigger schema and data inventory.

Counters for this bounded review: 2 output sites examined; 1 static RU/EN pair verified; 1 dynamic output site UNKNOWN; 0 confirmed source-level FAIL; full-file semantic PASS **not awarded**. Trigger JSON messages, caller effects and runtime remain UNKNOWN. The 582-row personal TSV is deliberately unchanged because this new file is outside the frozen baseline.

## BattleNarrator QA status
Prior static review removed two source-level false assertions. Independent Unity/runtime integration and all actual battle callers remain UNKNOWN. Overall production QA **NOT ACCEPTED YET**, not a regression claim.

UX remains frozen. No five-auditor completion claim.
