# Independent QA #5 — delta MapLogicRunner, 2026-11-19

Branch: feature/server-prototype. Source blob: `Assets/Scripts/Gameplay/MapLogicRunner.cs` `f7c502940337c7df33c1bf3f9adbc37c71163c2f`. This is a NEW source outside the immutable 582-source baseline. It MUST NOT be marked EXCLUDED_TECH.

## Completed bounded source-level check: F interaction label
`OfferInteractions` (lines 64–88): only enabled `OnInteractObject` triggers with nonempty target IDs, valid conditions, a registered object within `UseRange=3f`, and no previously fired once-trigger can offer `Loc.T("F — взаимодействовать")`. The label is supplied to the existing F priority offer, not a new input binding. RU/EN checked: `Assets/Resources/Lang/en.txt` blob `911b9202b1e8fb5d252e4f6eb1d05b5f51cb7a13`, lines 14582–14583, `F — interact`. **Translation PASS for this one literal**, but contextual visibility/priority and UI effects UNKNOWN (the offer callback and F priority dispatcher were not inspected). This is not a PASS for the file.

## Completed bounded source-level check: trigger message dispatch
`Fire` (lines 90–103), `AllowedCondition` (105–114), `Apply` (116–136): `ShowMessage` requires a nonblank `t.Message` of length at most 240, then executes `Herald.Line(Loc.T(t.Message))`. Conditions are `Always`, `FlagIsTrue`, `FlagIsFalse`. Unknown condition/action fails closed. A triggered message is player-facing even though its exact text is DATA, not a literal in this file. **UNKNOWN_CONTEXT**, not PASS. Reproducible counterexample to treating the source as technical: configure an enabled `OnStart` / `Always` / `ShowMessage` trigger with `Message="Untranslated map author sentence"`; on `Start`, `Fire("OnStart","")` reaches `Loc.T(t.Message)` and `Herald.Line`. Whether localization falls back or a real scene presents it correctly remains UNKNOWN; the example is a source-level execution path, not an observed runtime defect. The concrete map JSON messages, locale lookup behavior, and caller wiring remain unreviewed.

## Accounting and verdict
Two bounded pathways reviewed; one static RU/EN pair compared. SEMANTIC VERIFIED across complete source: 0. Dynamic message contents: UNKNOWN, count not established. Source file status: **NEEDS_CONTEXT**. Production changes: none. Baseline 582-path TSV untouched because this is a new delta source. QA ACCEPTED for whole file: NO. Unity tests: not run.
