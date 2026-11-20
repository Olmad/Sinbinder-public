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
