# Исправления текстов, владелец №4

## 2026-11-20 — SoulCourtReasons / Greed: выдуманная невыплата

- **Основание:** независимая QA №5, `Docs/AgentTeam/88-INDEPENDENT-AUDITOR-5-PROGRESS.tsv`, запись `Assets/Scripts/Gameplay/SoulCourtReasons.cs`, QA2 `lines=94-95,124-125`, исходный `blob=fe8256054d45109df7001dbc26b789ea7429d23b`. Контрпример: воину полностью заплатили, но отрицательный голос Greed всё равно утверждает, что за прошлые дела не заплатили.
- **Актуальный source перед патчем:** `ff551effda310e08982f84435bba755078d73f28`; `Assets/Resources/Lang/en.txt` `2e626a0080b7ed184729f5424360353b44c9cea4`.
- **Условие:** `SoulCourtReasons.Order`, `case "Greed"`, `likes=false`. Ветка не получает историю выплат. Новая строка описывает мотив жадности без выдуманного события.
- **RU:** «Ему не заплатили за прошлые дела. Жадность ведёт счёт.» → «Жадность ставит выгоду выше приказа.»
- **EN:** “He was not paid for his past work. Greed keeps count.” → “Greed puts profit before the order.”
- **Коммиты:** [C# 6924ab5](https://github.com/Olmad/Sinbinder-public/commit/6924ab5f35b71624574884311f7e6694f149de37), [EN e03bcbf](https://github.com/Olmad/Sinbinder-public/commit/e03bcbf118f535246cd1478072002b3ca08ba03f).
- **Новые blob:** C# `2d08089ba8a6005d52ee925aecce2367f4877c35`; EN `01629e8d02602a12b5aa3356cd229cdd3f435bc4`.
- **Проверка source-level:** 5/5 assertions PASS: правильная ветка Greed/likes=false, отсутствие старого выдуманного факта в C#, наличие точной RU/EN пары, отсутствие старого msgid, единственность нового msgid. Эти проверки строковые и не являются компиляцией C# или Unity runtime.
- **QA2:** source=Assets/Scripts/Gameplay/SoulCourtReasons.cs; blob=2d08089ba8a6005d52ee925aecce2367f4877c35; lines=Order case Greed; context=likes=false, history of payments unavailable; negative=fully paid warrior must not be described as unpaid; verified=1; total=1; RU_EN=matched en.txt blob 01629e8d02602a12b5aa3356cd229cdd3f435bc4; outcome=FIX_PENDING_QA5.
- **Статус:** исправлено на уровне исходников; независимая приёмка QA №5 **ожидается**, Unity **UNKNOWN**. Исторические записи пяти аудиторов не переписаны. Этап 1 не закрыт.

## 2026-11-20 — SoulHarvester: пустой EN для добытого кошеля

- **Основание:** аудитор №1, `88-INDEPENDENT-AUDITOR-1-PROGRESS.tsv`, строка `Assets/Scripts/Gameplay/SoulHarvester.cs`, source-level FAIL на строках 182–198; `Loc.F` выводит русский msgid при пустом английском `msgstr`.
- **Source:** `Assets/Scripts/Gameplay/SoulHarvester.cs` blob `11b476a52146964b8609d79ec30bb3873c858a8f`, строки 183–197; `TakePurse()` требует доступные CombatManager/PlayerInventory, ближайшее тело врага до 2 м, `GoldValue>0`, `CollectGold()>0`, затем `AddGold(gold)` и `Log(Loc.F(..., gold))`.
- **Контрпример:** при выбранном английском языке и успешно собранном кошеле лог не имеет английского перевода. Новый EN: `The fallen enemy’s purse is yours: +{0}.`; RU не изменён: `Кошель убитого — в ваш: +{0}.`.
- **До:** EN blob `01629e8d02602a12b5aa3356cd229cdd3f435bc4`. **После:** `413688e1522d7f0f95ae42fd04806170b157caf5`. [Коммит ea8376f](https://github.com/Olmad/Sinbinder-public/commit/ea8376f060440411e2209f8c45b3d6d86c9920e8).
- **Source-level assertions 6/6:** ключ единственный, `Loc.F` использует тот же ключ, `gold<=0` исключён, `AddGold` предшествует журналу, placeholder `{0}` присутствует в RU и EN. Проверки JS по реальным исходникам, **не компилируемый C# test**. Unity/CI не запускались.
- **QA2:** source=Assets/Scripts/Gameplay/SoulHarvester.cs; blob=11b476a52146964b8609d79ec30bb3873c858a8f; lines=182-198; context=TakePurse CollectGold positive then AddGold and Loc.F; negative=English locale previously had empty msgstr for positive purse reward; verified=1; total=1; RU_EN=en.txt blob 413688e1522d7f0f95ae42fd04806170b157caf5 placeholder matched; outcome=FIX_PENDING_QA5.
- **Статус:** source-level localization fix; независимая приёмка №5 PENDING, runtime UNKNOWN. Исторический TSV №4 и механики не изменены.

## 2026-11-20 — Whisper: пустой итог при исчезновении воина

- **Основание:** QA №5, `88-INDEPENDENT-AUDITOR-5-PROGRESS.tsv`, строка `Assets/Scripts/Gameplay/Whisper.cs`, QA2 `lines=209-251`; исходный baseline blob `6b527416ec8a6536f3cbb7f25f50a1d4ba7fff8a`. Повторно воспроизведено в текущем исходнике.
- **Контрпример:** режим SpeechBubbles, воин уничтожен до `t>=1.0f`; старый guard `w != null` пропускал `ResolveBargain()`, поэтому `outcome=""` попадал в `BattleLogUI.Write`.
- **Минимальное исправление:** на `t>=1.0f` всегда вызвать `ResolveBargain()`; показ пузыря, голоса и Flare оставить под `w != null`. Внутренний `ResolveBargain` уже отказывает при отсутствующем воине, не тратит золото и формирует нейтральный результат. Не менять голоса, AOS, Суд, сейвы или Tab/V.
- **Source SHA:** before `92a86ee8b9000c4020187e8195c50aba8ed453a1`, after `65f8aceaa74f82eedac74b514ffef31796b4e310`. [Production commit a68f6ed](https://github.com/Olmad/Sinbinder-public/commit/a68f6ed55220aab73b9d1f3ed1aeb6d2103c1590).
- **Source-level checks:** exact old block unique; null/dead guard in ResolveBargain exists; neutral RU outcome exists; English translation in `en.txt` blob `413688e1522d7f0f95ae42fd04806170b157caf5` exists (4/4 checks). **Не компилируемый C# test**. Runtime/Unity UNKNOWN. Нужен независимый QA №5, включая случай уничтожения воина во время корутины.
- **QA2:** source=Assets/Scripts/Gameplay/Whisper.cs; blob=65f8aceaa74f82eedac74b514ffef31796b4e310; lines=230-243; context=SpeechBubbles fallback, t>=1.0f, warrior destroyed; negative=previous guard skipped resolution and emitted empty battle log; verified=1; total=1; RU_EN=neutral Loc.T outcome and EN translation present; outcome=FIX_PENDING_QA5.

## 2026-11-20 — ExpeditionReplayViewer: replay controls RU/EN

- Основание: аудитор №2, hardcoded Russian GUI labels in English locale; source before 3b4468fc57067a2d6557e60f890a27ca18284456.
- Контрпример: with EN selected and nonempty replay, header and Close/Back/Pause/Play/Forward controls remained Russian. Existing «Пауза» translates as «Paused», so action key «Приостановить» maps to «Pause».
- QA2: source=Assets/Scripts/UI/ExpeditionReplayViewer.cs; blob=bc75e8fa7caad015fc1e36754847ce446399c306; lines=OnGUI header and controls; context=nonempty replay in English locale; negative=previously displayed hardcoded Russian buttons; verified=6; total=6; RU_EN=en.txt blob 961bf2ac4e9eb09b37dbf169ef8c0630152519ef six exact matches; outcome=FIX_PENDING_QA5.
- Source-level RU/EN assertions: 6/6 PASS. Compiled C# NOT RUN; Unity UNKNOWN. Other replay captions still hardcoded Russian; entire viewer NOT PASS.
- Commits: 23c9d08d6614f2b9149e57f58fbebd48a996efdf; 3c140ca408aa68a8e5450b41e877470ebb3f28a9; b59e9e4a43b2cb207a2f58bfd0b2d30b861e810f; b83f8dadf6cf97bee6a134a6198f8761d5d855c3.
- Independent QA #5 pending; stage 1 remains open.

## 2026-11-20 — ExpeditionReplayViewer: подписи схемы маршрута

- Основание: ранее подтверждённая аудитором №2 незавершённая EN-локализация реплея; текущий source blob до изменения `bc75e8fa7caad015fc1e36754847ce446399c306`.
- Три оставшиеся русские player-facing подписи в `DrawSchematicMap`: «СХЕМА МАРШРУТА», «Цель», «Выход / возврат». При английской локали ранее показывались по-русски.
- Изменение: только `Loc.T` для трёх литералов и три точные пары RU/EN: ROUTE SCHEMATIC; Objective; Departure / return. Обработчики, схема и данные не менялись.
- QA2: source=Assets/Scripts/UI/ExpeditionReplayViewer.cs; blob=f0d36ad782883556f8cf31e14ca9a94efde32c5f; lines=DrawSchematicMap GUI.Box/GUI.Label; context=nonempty expedition replay and schematic map visible; negative=EN locale previously displayed three Russian labels; verified=3; total=3; RU_EN=en.txt blob 278672b5b86a49357e301411a03f945ddb1c3579 matched 3 pairs; outcome=FIX_PENDING_QA5.
- JS source-level проверки: 6/6 (каждый Loc.T вызов и точный RU/EN ключ). Компилируемый C# и Unity не запускались, runtime UNKNOWN; весь файл не PASS.
- Коммиты: [C# cb1da0d](https://github.com/Olmad/Sinbinder-public/commit/cb1da0d5b78f44f75843ffadd30c9e4e5aeaef2d), [EN 717f50c](https://github.com/Olmad/Sinbinder-public/commit/717f50cee67afcc21303c07e9bc337bd4245f77e). Независимая QA №5 ожидается.

## 2026-11-20 — ExpeditionReplayViewer: счётчик события и ход AOS

- Основание: подтверждённый source-level FAIL непереведённых русских подписей в `ExpeditionReplayViewer.OnGUI()`, строки блока справа от карты, исходный blob `f0d36ad782883556f8cf31e14ca9a94efde32c5f`.
- Контрпример: английская локаль, открытый непустой реплей; `"Событие "+(_index+1)+" из "+Frames.Count` и `"Ход AOS: "+frame.Round` оставались русскими.
- Минимальная правка: `Loc.F("Событие {0} из {1}",_index+1,_data.Frames.Count)` и `frame.Round>0 ? "\\n"+Loc.F("Ход AOS: {0}",frame.Round) : ""`; условие показа раунда и параметры не менялись. EN: `Event {0} of {1}`, `AOS turn: {0}`.
- QA2: source=Assets/Scripts/UI/ExpeditionReplayViewer.cs; blob=1a69c2aa000d68df3cb31cd9a1d461993d02243d; lines=OnGUI side panel event and round; context=nonempty expedition replay, frame.Round>0 for round; negative=English locale previously displayed Russian counter and round; verified=2; total=2; RU_EN=en.txt blob a8e5a95cd3d5b9fc6cf337d6ba3935a8835a2255 placeholders matched; outcome=FIX_PENDING_QA5.
- Source-level JS assertions 8/8 PASS: exact callers, round guard, two translations, uniqueness, absence of previous hardcoded forms. **Compiled C# test NOT RUN; Unity UNKNOWN; independent QA #5 PENDING.** Remaining replay labels are not covered; entire file NOT PASS.
- Commits: [C# d114e06](https://github.com/Olmad/Sinbinder-public/commit/d114e067e348c87a5366733a43422c29ed22009b), [EN 7114598](https://github.com/Olmad/Sinbinder-public/commit/7114598fef3393edd7485cdc2e8a76fa069d00f6).

## 2026-11-20 — ExpeditionReplayViewer: предупреждение о схематической реконструкции

- Основание: source-level контрпример: английская локаль и открытый реплей показывали русскую строку-предупреждение без Loc.T в OnGUI. До C# blob 1a69c2aa000d68df3cb31cd9a1d461993d02243d; EN blob a8e5a95cd3d5b9fc6cf337d6ba3935a8835a2255.
- Исправлено: единственный литерал предупреждения об отсутствии точных координат обёрнут в Loc.T, добавлен EN «Schematic reconstruction: combat decisions come from the engine; exact coordinates are absent from the legacy record.». Значение факта о старых записях не менялось.
- QA2: source=Assets/Scripts/UI/ExpeditionReplayViewer.cs; blob=a964c80f2ca29ca5fc98cc0175b1e6c1ad94eef5; lines=OnGUI schematic reconstruction GUI.Label; context=nonempty replay, schematic caption visible; negative=English locale previously showed Russian disclaimer; verified=1; total=1; RU_EN=en.txt blob 03d46e460af2f5e616f9635de0e570958969502c, exact key and meaning matched; outcome=FIX_PENDING_QA5.
- Source-level JS checks 6/6 PASS: Loc.T caller unique, exact translation unique, nonempty replay guard and schematic absence-of-position contract retained. Компилируемый C# тест НЕ ЗАПУСКАЛСЯ, Unity UNKNOWN, независимая QA №5 PENDING; весь файл не PASS.
- Commits: [C# 0da66d5](https://github.com/Olmad/Sinbinder-public/commit/0da66d58f00fd06e89df2b36656f1c5760eb79bd), [EN 7f9b1e9](https://github.com/Olmad/Sinbinder-public/commit/7f9b1e93d79c7ca401f29710458d80ed4a484e97).

## 2026-11-21 — ExpeditionReplayViewer: локализация строк списка событий

- Основание: ранее подтверждённый аудитором №2 source-level FAIL русского текста в английской локали реплея. До патча C# blob `a964c80f2ca29ca5fc98cc0175b1e6c1ad94eef5`, EN blob `03d46e460af2f5e616f9635de0e570958969502c`.
- Контрпример: английская локаль, непустой реплей; при `step.Round>0` отображалось «Ход», при `step.HasOutcome` отображалось «исполнено» либо «без результата» без перевода.
- Минимальная правка: список событий использует `Loc.F("Ход {0}",step.Round)` и `Loc.T("исполнено")` / `Loc.T("без результата")`. Переводы: Turn {0}; completed; no confirmed outcome. Ветвления `Round>0`, `HasOutcome`, `Resolved` и данные не изменены.
- QA2: source=Assets/Scripts/UI/ExpeditionReplayViewer.cs; blob=3e3f575f0192d04903b6ef8b6905557633796c94; lines=OnGUI event list string line; context=nonempty replay with Round>0 and HasOutcome; negative=EN locale previously displayed untranslated Russian labels; verified=3; total=3; RU_EN=en.txt blob 6d06d895213146d068b20e29a0f246a9eed10838; outcome=FIX_PENDING_QA5.
- Source-level string checks: three unique replacements, three new EN keys, existing round/outcome guards retained. **Compiled C# test NOT RUN; Unity runtime UNKNOWN; independent QA #5 PENDING.** Whole viewer NOT PASS.
- Commits: [C# 664549d](https://github.com/Olmad/Sinbinder-public/commit/664549d11f31f6689e542b676f99744f50b018f7); [EN eb67ff8](https://github.com/Olmad/Sinbinder-public/commit/eb67ff8e32ef8c1b2b7d0a6eaf79fe8e023f6654).


## 2026-11-21 — ExpeditionReplayViewer: фактические результаты боевых действий

- Основание: подтверждённая аудитором №2 неполная EN локализация реплея; source-level контрпример: EN locale, CombatAction frame with HasOutcome=true showed Russian outcome/target/damage/healing/kill lines from ObservedEffect().
- Условия: ObservedEffect returns empty for null, non-CombatAction or !HasOutcome; Resolved selects one of two result messages; TargetName nonempty, Damage>0, Healing>0, TargetKilled gate optional lines. Guards preserved; no fabricated effects introduced.
- Минимальный патч: six strings moved to Loc.T/Loc.F; EN six matching msgids and placeholders. Old source blob 3e3f575f0192d04903b6ef8b6905557633796c94, new source blob c25e3aeb703e8a62f0aa718d829096adb62164df; old EN blob 6d06d895213146d068b20e29a0f246a9eed10838, new EN blob 2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df.
- QA2: source=Assets/Scripts/UI/ExpeditionReplayViewer.cs; blob=c25e3aeb703e8a62f0aa718d829096adb62164df; lines=ObservedEffect; context=CombatAction and HasOutcome, optional actual outcome facts; negative=English locale previously displayed hardcoded Russian result lines; verified=6; total=6; RU_EN=en.txt blob 2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df all six keys present; outcome=FIX_PENDING_QA5.
- Source-level checks: 6/6 caller+EN existence assertions on post-commit source PASS; compiled C# test NOT RUN, Unity runtime UNKNOWN, independent QA #5 PENDING. This is not full file PASS. Stage 1 open.
- Commits: [C# 935bece](https://github.com/Olmad/Sinbinder-public/commit/935bece993149c16de0542dd9d7efe654db1f0e5); [EN 8629263](https://github.com/Olmad/Sinbinder-public/commit/86292638c47f8db0091d28c2be3b334a002f6d57).
