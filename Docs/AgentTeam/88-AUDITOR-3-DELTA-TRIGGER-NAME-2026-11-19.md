# Auditor 3 — delta evidence: localized trigger names persisted as data (2026-11-19)

Scope: **feature/server-prototype** only. This is a delta outside the frozen 582-file baseline, **not** a new TSV row. No production changes. Source-level review; Unity NOT RUN.

## Finding D3-NAME-01 — FAIL (source-level, editor-facing language provenance)

- **Source:** `Assets/Scripts/Gameplay/MapEditor.cs`, blob `9810ddaa9f502353dcf0b69e535bcbfa9eb4158d`.
- **Relevant lines:** 1355–1376 (new trigger, stored Name, direct label rendering); 1596–1609 (object-interaction Name); 1810–1813 (JSON snapshot and copy before save).
- **Localization dependency:** `Assets/Scripts/Core/Loc.cs`, blob `0865eeb9310031767ae5f4f726e6e7715dd239aa`, lines 49–54: `Loc.T` returns its argument when source language is active, otherwise looks up a translation and falls back to the argument.
- **Actual flow:** Clicking `+ Новый триггер` writes `Name = Loc.T("Новое правило")` into the mutable map. The trigger list later displays `t.Name ?? t.Id` directly, without `Loc.T`. `AddObjectInteraction` similarly stores `Loc.T("Взаимодействие: ") + thing.Prop` in the Name field.
- **Reproducible negative case:** Create a new trigger with RU active; its stored Name is `Новое правило`. Change interface language to EN without renaming the trigger. The list still uses the stored Russian Name, not a translated label. Conversely, when EN translation is present at creation, the stored English Name remains after switching to RU. This is deterministic from assignment + direct rendering, independent of Unity timing. The exact English translation for this key has **not** been verified here.
- **Impact:** editor-facing default trigger labels do not track language changes; language-specific UI text is being persisted as map data. This is a distinct data-provenance issue from `Trigger.Message` being shown by `MapLogicRunner`. It does not establish an in-game dialogue defect.
- **RU/EN:** RU literal `Новое правило`; EN catalogue entry UNKNOWN. For `Взаимодействие: `, EN UNKNOWN; `thing.Prop` may be a technical asset identifier.
- **Semantic verification:** 1 stored-label path and 1 direct-display path inspected; 0 fully RU/EN-verified player-facing strings; 1 confirmed source-level path defect; 1 additional object-label path UNKNOWN pending full language/resource review.
- **QA2:** source=Assets/Scripts/Gameplay/MapEditor.cs; blob=9810ddaa9f502353dcf0b69e535bcbfa9eb4158d; lines=1355-1376,1596-1609,1810-1813; context=click new trigger -> assign Loc.T result to Name -> render t.Name directly; negative=create in RU then switch to EN, existing Name stays RU; verified=0; total=2; RU_EN=RU Новое правило / EN UNKNOWN; outcome=FAIL

## Checks and boundaries

Checked actual source assignments, list rendering and JSON snapshot. `MapLogicRunner.cs` blob `f7c502940337c7df33c1bf3f9adbc37c71163c2f` was re-read for the separate `Trigger.Message` flow: `Herald.Line(Loc.T(t.Message))` is a different player-facing issue already reported, **not counted as a new defect** here. No claims about prefab/scene visibility or runtime behavior. No modifications to baseline TSV, gameplay, saves, voices, dream, or Tab/V. Gate CI not run. Overall audit INCOMPLETE.
