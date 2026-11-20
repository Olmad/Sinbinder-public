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
