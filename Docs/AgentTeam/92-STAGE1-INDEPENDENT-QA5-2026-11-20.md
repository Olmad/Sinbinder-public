# Этап 1: независимый QA №5, контроль BattleNarrator (20.11.2026)

**Статус: NO-GO (промежуточный, не решение автора).** Новый двухэтапный порядок автора имеет приоритет над старым требованием закончить 582 источника до рассмотрения возобновления разработки. Исторический TSV №5 сохраняется без переписывания; другие TSV не изменяются. Отсутствие P0/P1 во всех критических сценариях НЕ доказано.

## Проверенный блок: BattleNarrator, актуальная feature/server-prototype

- Production source: `Assets/Scripts/AOS Engine/BattleNarrator.cs`, Git blob `974273d50701ef0676fb8390cadb72b4c83ff5e8`, метод `Build`, строки 15–62 (нумерация по содержимому source; проверить при следующем механическом экспорте).
- Harness: `Tools/logic/battle-narrator/Program.cs`, blob `e14076647660b87b68e395a0cb8c1d6a4077260e`. Это тестовый Loc.T stub, НЕ production локализатор.
- EN: `Assets/Resources/Lang/en.txt`, blob `2e626a0080b7ed184729f5424360353b44c9cea4`, строки 11–12: `Записей о ходе боя нет.` / `No battle events were recorded.` (соответствие по смыслу подтверждено).
- Исходный baseline `b1a75d7c901f410f2a017924ee20504f5c9a1224`: аудиторы №2–4 зафиксировали ложные выводы о послушании при null/empty/one refusal и о переговорах при пяти произвольных событиях. Это исторический FAIL, не статус нового blob.

### Независимая source-level проверка ветвей

| Вход | Ожидаемое свойство | Что следует из актуального C# | Вердикт |
|---|---|---|---|
| `null` | Не выдумывать послушание | Немедленный `Loc.T("Записей о ходе боя нет.")` | PASS source logic |
| `[]` | То же | Условие `Count == 0` | PASS source logic |
| `["",null,"   "]` | Не выдумывать бой | `hasEvent=false`, нейтральная строка | PASS source logic |
| `["Воин отказался выполнить приказ."]` | Сохранить отказ без ложного исполнения | Заголовок + исходная запись, без утверждения о выполнении | PASS source logic |
| `["Дождь.","Гром.","Ветер.","Туман.","Холод."]` | Не превращать длину журнала в переговоры | Все пять событий переписываются без вывода о переговорах | PASS source logic |
| `["Схватка.","Схватка.","Схватка.",null,"Отступление."]` | Свернуть повтор | Одна строка `Схватка.`, суффикс `И так раз за разом.`, затем `Отступление.` | PASS source logic |

Проверка выполнена **чтением production C# и harness**, не запуском .NET/Unity в данном сеансе. Harness содержит 8 утверждений, включая проверку локализации; само наличие утверждений не считается выполнением тестов. Ветка без структурированных боевых событий не позволяет подтвердить правдивость произвольного входного журнала. Реальный caller, provenance записей, фактическая RU/EN подстановка через production Loc и Unity PlayMode: **UNKNOWN**.

**Вердикт патчу №4: QA_ACCEPTED_SOURCE_SCOPE_ONLY** для устранения трёх известных ложных выводов на перечисленных входах по статическому чтению; **runtime QA UNKNOWN**. Это НЕ полный PASS файла или всего BattleNarrator в игровом цикле. Для окончательной приёмки нужны подтверждённый production caller и исполнение тестов на текущем SHA.

QA2: source=Assets/Scripts/AOS Engine/BattleNarrator.cs; blob=974273d50701ef0676fb8390cadb72b4c83ff5e8; lines=15-62; context=Build(IReadOnlyList<string>) receives unstructured journal, production caller UNKNOWN; negative=one refusal and five unrelated records no longer cause invented compliance/bargaining; verified=1; total=4; RU_EN=no-record key matches EN, other keys and runtime localization UNKNOWN; outcome=UNKNOWN.

## Межаудиторские расхождения (фактически прочитанные реестры)

- №2: baseline BattleNarrator 6 checked / 3 failed; №3: 6 checked / 3 failed; №4: 6 checked / 2 failed. Расхождение **2 против 3** объясняется различной группировкой бездоказательных утверждений (null, short, long), а не обязательно различием source; требуется единая таблица конкретных ключей.
- №1: baseline 5 found / 5 unknown, хотя №2–4 указывают шесть исходных Loc.T; legacy inventory count требует сверки с baseline blob.
- Во всех четырёх строках сохраняется baseline `blob_sha=b1a75d...`; ссылки на актуальный `974273...` содержатся только в evidence. Сравнивать их статус как verdict одного и того же blob без разграничения baseline/current нельзя.

## Матрица этапа 1 (только подтверждённое в этом сеансе)

| Критический сценарий | Независимая приёмка №5 |
|---|---|
| Whisper 7 ветвей | UNKNOWN: новая сквозная проверка не выполнялась |
| SoulCourt / SoulCourtReasons | UNKNOWN |
| BattleNarrator | SOURCE_SCOPE_ACCEPTED для известных ложных выводов; caller/runtime UNKNOWN |
| AOS объяснения приказов | UNKNOWN |
| Экспедиции | UNKNOWN |
| Основные диалоги / лор | UNKNOWN |
| Сон, Tab/V, обучение | UNKNOWN |
| RU/EN critical keys | 1 пара проверена; остальное UNKNOWN |
| Delta changed SHA | BattleNarrator и en.txt отмечены; полный delta-review UNKNOWN |
| Регрессии 13 голосов, сейвов | UNKNOWN |

**GO/NO-GO: NO-GO.** Нельзя документально подтвердить полноту критических сценариев, отсутствие P0/P1 и независимую приёмку остальных патчей №4. Разработка не возобновляется автоматически. Следующий QA-блок: production caller BattleNarrator.Build и provenance журнала, затем SoulCourtReasons/VillageDialogueRules RU/EN и матрица семи грехов. Этап 2 и 582-source TSV остаются исторически отдельными от решения об этапе 1.
