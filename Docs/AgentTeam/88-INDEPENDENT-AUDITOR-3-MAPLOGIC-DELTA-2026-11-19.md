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
