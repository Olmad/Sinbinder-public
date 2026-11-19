# Independent auditor 3 — delta evidence: MapLogicRunner (2026-11-19)

Scope: **DELTA ONLY**, outside immutable 582-source baseline. No baseline TSV row added. No production changes. Source-level review, not Unity runtime verification.

## Source identities
- `Assets/Scripts/Gameplay/MapLogicRunner.cs`, blob `f7c502940337c7df33c1bf3f9adbc37c71163c2f`, full file read.
- `Assets/Scripts/Core/Loc.cs`, blob `0865eeb9310031767ae5f4f726e6e7715dd239aa`, full file read.
- `Assets/Resources/Lang/en.txt`, blob `1c98dacabcef8eeea623a81cb71fc1efd31c462a`, relevant key at lines 14581–14583 inspected.

## 1. Fixed interaction prompt — SOURCE-LEVEL PASS, runtime UNKNOWN
MapLogicRunner `OfferInteractions` (approx. lines 66–88) calls `Loc.T("F — взаимодействовать")` only for an enabled `OnInteractObject` trigger whose target is registered, within `UseRange=3f`, whose `Once`/condition constraints pass. The offer is routed into the existing priority system. EN catalog has exact `msgid "F — взаимодействовать"` with `msgstr "F — interact"`. RU/EN source key pair confirmed (1/1). Counterexample considered: absent target or >3f => no offer; unknown condition => false. Runtime prompt ordering and actual object visibility are UNKNOWN.

QA2: source=Assets/Scripts/Gameplay/MapLogicRunner.cs + Assets/Resources/Lang/en.txt; blob=f7c502940337c7df33c1bf3f9adbc37c71163c2f; lines=OfferInteractions approx 66-88 + en.txt 14581-14583; context=enabled OnInteractObject, registered target, within 3f, condition passes; negative=unregistered target or distance over 3f suppresses offer; verified=1; total=1; RU_EN=F — взаимодействовать / F — interact; outcome=PASS

## 2. User-authored ShowMessage — CONFIRMED SOURCE-LEVEL LOCALIZATION GAP
`Fire` iterates enabled matching triggers, checks `AllowedCondition`, then `Apply`. `Apply` accepts `ShowMessage` with nonempty `t.Message` of <=240 UTF-16 code units and invokes `Herald.Line(Loc.T(t.Message))`. `Loc.T` looks up the exact Russian string in a finite static table, otherwise returns its input unchanged; `Loc.Name` transliteration is not used. Thus no guarantee of English output for arbitrary map JSON messages.

Reproducible source-level counterexample (NOT an executed Unity test): a map with an enabled `OnStart` `ShowMessage` trigger, `Condition="Always"`, `Message="Секретная записка"`, and a locale catalog without that exact key. `Start` calls `Fire("OnStart","")`, `AllowedCondition` returns true, `Apply` calls `Loc.T`; with `Language="en"` and no matching translation, the result is the original Russian message. This is a demonstrable missing guarantee, not evidence that any shipped map actually contains this message. If user enters an English message, Russian-language players likewise see that English text. No input-language metadata or per-map translation table is read by this runner. Whether other validation/editor paths constrain or supply translations is UNKNOWN.

QA2: source=Assets/Scripts/Gameplay/MapLogicRunner.cs + Assets/Scripts/Core/Loc.cs; blob=f7c502940337c7df33c1bf3f9adbc37c71163c2f; lines=Start, Fire, AllowedCondition, Apply ShowMessage + Loc.T; context=OnStart or matching region/object trigger and valid condition; negative=English locale + arbitrary Russian map message absent from static catalog => Russian output; verified=1; total=1; RU_EN=UNKNOWN for arbitrary JSON; outcome=FAIL

## Limits / checkpoint
Source assertions checked: 2; fixed prompt RU/EN pair: 1 verified; dynamic message translation guarantee: 1 FAIL (source-level), not a verified existing player-facing translation defect; actual JSON message corpus, MapData serializer, prefab/scene references, Herald implementation, map-editor validation, and Unity runtime: UNKNOWN. No semantic PASS claimed for arbitrary authored messages. Baseline ledger remains unchanged because this source is outside its 582 rows. No test was executed in Unity or CI; counterexample is traced through source branches only.

## 3. Additional completed provenance check: editor language leaks into saved map messages

Source identities: `Assets/Scripts/Gameplay/MapEditor.cs` blob `9810ddaa9f502353dcf0b69e535bcbfa9eb4158d`; `Assets/Scripts/Gameplay/MapLogicRunner.cs` blob `f7c502940337c7df33c1bf3f9adbc37c71163c2f`; `Assets/Scripts/Core/Loc.cs` blob `0865eeb9310031767ae5f4f726e6e7715dd239aa`. Branch HEAD before write: `9c380a949fe4893d88fc8e5d4eaa0ec8df914b3f`.

**Confirmed source-level FAIL (separate from arbitrary-user-text gap):** MapEditor creates a new region rule with `Message = Loc.T("Вы вошли в область.")` at lines 1355–1365, and an object interaction rule with `Message = Loc.T("Вы осмотрели предмет.")` at lines 1596–1609. The editor then exposes `TextField("Сообщение игроку", t.Message, value => t.Message = value, true)` at lines 1641–1650, and validation at 1734–1738 checks only presence and maximum length, not canonical source language or bilingual alternatives. MapLogicRunner.Apply at lines 119–124 calls `Herald.Line(Loc.T(t.Message))`. Loc.T at lines 49–53 immediately returns its input when `Language == "ru"`; it only looks up the exact stored key otherwise.

**Reproducible conditional counterexample:** when the editor is opened with English selected and the corresponding fixed prompt has an EN translation, the default `Message` is stored in English (e.g. `"You entered the area."`, conditional on that exact catalog translation). Saving that map, then opening it with Russian selected, leads to the English stored string being returned unchanged by `Loc.T` and shown by Herald. This is a language provenance error in the built-in editor's default content, not merely hypothetical arbitrary JSON. The exact English spelling and shipped catalog presence for these two default messages were not rechecked in this increment; therefore their individual EN pairs remain **UNKNOWN**, and the observed manifestation is conditional on a nonempty translation. The code path and loss of original RU key are verified.

Independent second counterexample, not dependent on EN catalog: enter an English custom message in the editor field, save, then run the map in Russian. The validator accepts any nonempty message <=240 code units, and Loc.T returns it unchanged in RU. This establishes that the editor does not enforce RU source-language messages. No Unity execution or saved fixture performed.

QA2: source=Assets/Scripts/Gameplay/MapEditor.cs + Assets/Scripts/Gameplay/MapLogicRunner.cs + Assets/Scripts/Core/Loc.cs; blob=9810ddaa9f502353dcf0b69e535bcbfa9eb4158d; lines=MapEditor.cs 1355-1365,1596-1609,1641-1650,1734-1738 + MapLogicRunner.cs 119-124 + Loc.cs 49-53; context=editor constructs rule and saves message then runner displays rule in another locale; negative=custom English message accepted by editor and displayed unchanged in Russian locale; verified=1; total=2; RU_EN=UNKNOWN for editor-generated defaults, verified RU fallback behavior; outcome=FAIL

Checkpoint: one additional source-level provenance failure documented; no additional semantic PASS for shipped RU/EN strings; one conditional default-message translation case UNKNOWN. Production, 582-row TSV, 13 voices, Tab/V and saves untouched. CI/Unity not executed.


## 4. Independent EN-catalog verification of two built-in editor messages (checkpoint 2026-11-19)

Exact source blobs: MapEditor.cs `9810ddaa9f502353dcf0b69e535bcbfa9eb4158d`; MapLogicRunner.cs `f7c502940337c7df33c1bf3f9adbc37c71163c2f`; Loc.cs `0865eeb9310031767ae5f4f726e6e7715dd239aa`; Assets/Resources/Lang/en.txt `1c98dacabcef8eeea623a81cb71fc1efd31c462a`. Source-level review only, no Unity runtime claim.

**Confirmed P1 localization provenance defect (1 fixed default):** MapEditor.cs:1359–1365 creates a trigger with `Message = Loc.T("Вы вошли в область.")`. en.txt:14434–14435 has `msgid "Вы вошли в область."` / `msgstr "You entered the region."`. In English, MapEditor stores the *translated* value `You entered the region.` in the trigger. When this saved map is subsequently run in Russian, MapLogicRunner.cs:119–124 sends it through `Loc.T`, which returns the input unchanged in RU (Loc.cs:49–53). Thus the built-in editor-generated message is displayed in English in Russian mode. Counterexample requires a new editor rule created in EN and saved, then replayed in RU; no custom user text or hypothetical translation needed. Actual persistence/runtime is not executed, so the end-to-end UI observation remains UNKNOWN.

**Confirmed missing EN catalog key (1 fixed default):** MapEditor.cs:1598–1608 constructs an enabled `OnInteractObject` / `ShowMessage` rule with `Message = Loc.T("Вы осмотрели предмет.")`. An exact search of all 14,656 lines of the en.txt blob found **no** `Вы осмотрели предмет.` key. Under EN, `Loc.T` falls back to the Russian text. With the registered object within 3m and condition Always, interaction offers F and `Fire` displays that Russian message (MapLogicRunner.cs:66–88, 92–124). Source-level FAIL for missing translation; actual interaction rendering and resource availability UNKNOWN.

QA2: source=Assets/Scripts/Gameplay/MapEditor.cs + Assets/Scripts/Gameplay/MapLogicRunner.cs + Assets/Scripts/Core/Loc.cs + Assets/Resources/Lang/en.txt; blob=9810ddaa9f502353dcf0b69e535bcbfa9eb4158d; lines=MapEditor.cs:1359-1365,1598-1608; MapLogicRunner.cs:66-124; Loc.cs:49-53; en.txt:14434-14435 and exact full-file search; context=editor generates built-in trigger in EN, map later runs in RU; or EN object interaction with target registered within 3m; negative=EN editor stores You entered the region. then RU Loc.T returns it unchanged; missing EN key for Вы осмотрели предмет. returns Russian under EN; verified=2; total=2; RU_EN=Вы вошли в область. / You entered the region. (catalog PASS, storage provenance FAIL); Вы осмотрели предмет. / EN MISSING; outcome=FAIL

Verified in this increment: **2 source-level text paths with concrete counterexamples; 0 complete runtime confirmations; 1 EN pair matched, 1 EN translation missing.** No production changes. Baseline 582-row TSV deliberately untouched.


## 5. Event/target provenance collision in dynamic map messages (independent checkpoint, 2026-11-19)

Source: `Assets/Scripts/Gameplay/MapLogicRunner.cs`, blob `f7c502940337c7df33c1bf3f9adbc37c71163c2f`, lines 44–62 and 90–124. Complete 139-line file reread in this pass. `Update` fires `OnEnterRegion` with the entered `region.Id`; `Fire` checks `eventName` but accepts a trigger when **either** `t.RegionId == target` **or** `t.TargetId == target`. This is not an event-specific field check.

**Reproducible source-level counterexample:** a valid map contains a region with `Id="zoneA"` and a trigger `Event="OnEnterRegion", RegionId="zoneB", TargetId="zoneA", Enabled=true, Condition="Always", Action="ShowMessage", Message="Вы вошли в область Б."`. On entry to `zoneA`, `Fire("OnEnterRegion","zoneA")` accepts the trigger because `TargetId` matches, even though `RegionId` points to zoneB. `Apply` displays a statement asserting entry to zoneB. This is a **confirmed source-level provenance/condition defect**, not evidence that a shipped map contains this configuration. The same OR matching permits an `OnInteractObject` trigger to pass via a matching `RegionId` rather than `TargetId` if invoked with that target; availability of such invocation requires separate caller/resource review.

The code does not validate that the referenced zoneB exists, nor that its geometry contains the player. The actual map loader's validation, built-in maps, scene references, and Unity execution remain **UNKNOWN**. RU/EN for the custom `Message` is **UNKNOWN** because arbitrary JSON text is not a guaranteed Russian catalog key. No gameplay changes authorized or made.

QA2: source=Assets/Scripts/Gameplay/MapLogicRunner.cs; blob=f7c502940337c7df33c1bf3f9adbc37c71163c2f; lines=44-62,90-124; context=Update OnEnterRegion passes region.Id to Fire, Fire accepts either RegionId or TargetId; negative=OnEnterRegion RegionId zoneB TargetId zoneA displays zoneB claim on entry to zoneA; verified=1; total=1; RU_EN=UNKNOWN for user-authored JSON message; outcome=FAIL

Checkpoint: 1 new source-level FAIL in event-to-message provenance, 0 fully RU/EN semantic PASS, 1 custom-message language UNKNOWN. Baseline 582-file ledger unchanged because MapLogicRunner is delta. Unity and CI not executed; production untouched.
