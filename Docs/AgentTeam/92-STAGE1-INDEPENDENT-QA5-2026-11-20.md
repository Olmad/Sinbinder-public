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

## Независимая проверка QA №5: деревенская тема Envy, 20.11.2026

**Новый source-level FAIL, требует минимальной текстовой правки №4 и повторной QA.** Проверены актуальные blob: `Assets/Scripts/Dialogue/VillageLines.cs` `23dadf30fbdd93cf6b1070dec9a54612de7f4cba` (строки 18–35, 52–55, 89–99); `Assets/Scripts/Dialogue/VillageDialogueRules.cs` `4a690bc08d783562c89f20aa8790c4b3548a48bd` (строки 72–81, 112–116); `Assets/Resources/Lang/en.txt` `01629e8d02602a12b5aa3356cd229cdd3f435bc4` (строки 5943–5956). Harness `Tools/dialogue/Program.cs` blob `57c83c21adc60df95781ea6b95dd811afc4bdafd`, прочитан; .NET/Unity не запускались.

**Контрпример:** два воина с ненулевыми Soul, немой статус отсутствует, `a.Soul.Sin=Envy`, `b.Soul.Sin=Greed`, `TopicIndex(k)=Sin` (либо `Topic=Lord` при `Lords.Player==null`). `VillageLines.Exchange` вызывает `SinOpening(a,b)`: «Видал/Видала, что у соседа новое?», затем `SinAnswer(Envy,Greed)`: «Видал. Если продаёт, узнаю цену.» Нет проверки, что сосед, вещь или наблюдение существуют. При `b.Soul.Sin=Envy` ответ «У него новое, у меня старое. И ладно.» также утверждает неподтверждённые предметы. Это воспроизводимый **source-level дефект фактической обоснованности**, не доказательство конкретного появления в Unity. Для ответов `Envy/Sloth` «Видал. И пусть носит.» проблема аналогична. EN для `Envy/Greed`: «I saw. If it's for sale, I'll ask the price.» тоже утверждает наблюдение, не устраняя дефект перевода; `Envy/Envy` EN «His is new, mine is old. So what.» сохраняет неподтверждённый факт. Для `Envy/Sloth` EN и остальных ответов матрицы не проведена полная независимая сверка.

**QA-приёмка:** шесть ранее изменённых ответов `LordReplies Envy/Distrusted,Undecided` и `SinReplies Envy на Greed/Lust/Gluttony/Sloth` нельзя принять как полный сценарий Envy, поскольку новая проверка обнаружила дефект в соседнем исходном вопросе и некоторых ответах; их точная RU/EN проверка harness не заменяет проверку условий. **QA_REJECTED_SCENARIO_ENVY** (полный сценарий), **QA_UNKNOWN** (приёмка всего патча шести строк до самостоятельного исполнения тестов). Передать №4 минимальную правку только неподтверждённых фактов с сохранением характера персонажей, затем проверить все 7 ответов на вопрос и EN. Статус этапа 1 остаётся **NO-GO**.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Scripts/Dialogue/VillageDialogueRules.cs; blob=23dadf30fbdd93cf6b1070dec9a54612de7f4cba; lines=VillageLines:18-35,52-55,89-99 + VillageDialogueRules:72-81; context=Exchange topic Sin and unmuted pair, a.Sin Envy, b.Sin Greed/Envy/Sloth, no observed-neighbor predicate; negative=neighbor/item never existed but lines assert Vidал or new belongings; verified=0; total=3; RU_EN=Envy/Greed and Envy/Envy EN preserve unsupported assertion; outcome=FAIL. Note: QA2 blob belongs to primary caller VillageLines; related rules blob=4a690bc08d783562c89f20aa8790c4b3548a48bd.


## Дополнительная независимая проверка: все семь ответов Envy, 20.11.2026

Прочитана вся строка `SinReplies[Envy]` (семь столбцов в порядке Greed/Pride/Wrath/Envy/Lust/Gluttony/Sloth), вместе с `VillageLines.SinOpening` и `Exchange`. Текущие blob: `VillageLines.cs` `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`; `VillageDialogueRules.cs` `4a690bc08d783562c89f20aa8790c4b3548a48bd`; `en.txt` `413688e1522d7f0f95ae42fd04806170b157caf5` (изменился после предыдущей записи; точная сверка семи пар выполнена по этому blob). Проверка чтением source-level; Unity/.NET в этом запуске не исполнялись.

Условие: `Exchange(a,b)`, обе Soul существуют, `a` не Mute, `b` не Mute, topic=Sin (либо Lord при `Lords.Player == null`), `a.Soul.Sin=Envy`. Открывающий вопрос зависит от `b.Gender`: «Видал/Видала, что у соседа новое?». Существование соседа, обновки и факт наблюдения в этих ветвях не проверяются. Таблица ниже описывает семь **отдельных** ответов и EN, не семь подтверждённых runtime-событий.

| Ответчик | RU | EN | Source-level QA |
|---|---|---|---|
| Greed | Видал. Если продаёт, узнаю цену. | I saw. If it's for sale, I'll ask the price. | FAIL: утверждает наблюдение без проверки |
| Pride | А своим ты когда-нибудь доволен? | Are you ever pleased with what you have? | UNKNOWN_CONTEXT: ответ не утверждает факт наблюдения; исходный вопрос всё равно предполагает обновку |
| Wrath | Мне до чужого дела нет. | I don't care what the neighbors own. | UNKNOWN_CONTEXT: позиция персонажа, без подтверждённого наблюдения; EN «neighbors own» более предметно, чем RU «чужого дела» |
| Envy | У него новое, у меня старое. И ладно. | His is new, mine is old. So what. | FAIL: утверждает наличие двух предметов без проверки |
| Lust | Я смотрел не на обновку. | I wasn't looking at what he bought. | FAIL: утверждает наблюдение и покупку; RU не утверждает покупку, EN добавляет недоказанное событие |
| Gluttony | У соседа хлеб свежий? Тогда видал. | Fresh bread next door? Then yes, I noticed. | FAIL EN: RU задаёт условный вопрос, EN дополнительно утверждает, что говорящий заметил хлеб |
| Sloth | Видал. И пусть носит. | I saw. Let him enjoy it. | FAIL: утверждает наблюдение без проверки |

**Новый конкретный дефект локализации:** для `Envy/Lust` английское `what he bought` добавляет покупку, которой нет в RU и которая не проверяется в игровом состоянии. Для `Envy/Gluttony` английское `Then yes, I noticed` утверждает наблюдение, тогда как русское `Тогда видал` остаётся условным ответом на вопрос о хлебе. Обе EN-строки требуют минимального согласованного исправления без новых игровых механик. Контрпример: в сцене нет сведений о покупке и о свежем хлебе, но английская реплика сообщает об этих событиях как о фактах.

**Итог ограниченного блока:** 7/7 RU/EN пар прочитаны и сопоставлены; 5/7 имеют доказанный source-level FAIL (Greed, Envy, Lust, Gluttony EN, Sloth), 2/7 UNKNOWN_CONTEXT (Pride, Wrath), 0/7 полный semantic PASS с учётом общего неподтверждённого вступительного вопроса. Это не заменяет проверку полного массива 7×7 и не означает подтверждённого runtime-проявления. `QA_REJECTED_SCENARIO_ENVY` сохраняется; исправление №4 не принято для всего сценария. Проверки 13 голосов, сохранений, суда и Tab/V не запускались и остаются UNKNOWN.

QA2: source=Assets/Scripts/Dialogue/VillageDialogueRules.cs; blob=4a690bc08d783562c89f20aa8790c4b3548a48bd; lines=SinReplies Envy row (7 keys); context=VillageLines.Exchange topic Sin, unmuted pair, a.Sin Envy, b.Sin variable; negative=Envy/Lust EN says bought without purchase evidence and Envy/Gluttony EN claims noticed bread; verified=0; total=7; RU_EN=all seven pairs compared with en.txt blob 413688e1522d7f0f95ae42fd04806170b157caf5; outcome=FAIL.


## QA №5: новая проверка LordReplies / Pride-independent Envy, 20.11.2026

**Новый source-level FAIL, не runtime-утверждение.** Прочитаны `Assets/Scripts/Dialogue/VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`, строки 18–35 и 51–65, и `Assets/Scripts/Dialogue/VillageDialogueRules.cs` blob `4a690bc08d783562c89f20aa8790c4b3548a48bd`, строки 10–24 и 108–116 (строки метода сверить с актуальной нумерацией перед тестом). Конкретный сценарий: `Exchange(a,b)`, у обоих Soul, `a` не Mute, `TopicIndex(k)==Lord`, `Lords.Player!=null`, `Allegiance.Of(a.Id,a.Loyalty,Lords.Player)>=Hostility.Follows`, `b` не Mute, `b.Soul.Sin==Envy`. Тогда `LordTalk` выбирает `VillageLordTone.Respected`, и `LordAnswer(Envy,Respected)` выдаёт «К нему идут охотнее, чем ко мне.» Код проверяет только отношение **a** к Греховоду, но не сравнивает количество/желание подходящих к нему NPC и к **b**. Реплика выдаёт неподтверждённое сравнительное наблюдение за факт. Отрицательный пример: к Греховоду никто не подходил, а к b подходили; условия выбора реплики при этом могут оставаться теми же. Необходима минимальная текстовая правка без новых механик, затем отдельная проверка EN и всех соответствующих ветвей.

**RU/EN:** русский литерал непосредственно проверен; соответствующий ключ в актуальном `en.txt` в этом запуске не извлечён, поэтому RU_EN=UNKNOWN, а не PASS. **Новые SEMANTIC VERIFIED=0; новый source-level FAIL=1; runtime=UNKNOWN.** Независимые .NET/Unity тесты не запускались. Существующие вердикты по Envy/SinReplies не изменяются.

QA2: source=Assets/Scripts/Dialogue/VillageDialogueRules.cs; blob=4a690bc08d783562c89f20aa8790c4b3548a48bd; lines=LordReplies[Respected][Envy]; context=VillageLines.Exchange -> LordTalk with speaker allegiance >= Hostility.Follows and responder sin Envy; negative=no visitors to Lord but visitors to responder, yet comparative claim appears; verified=0; total=1; RU_EN=UNKNOWN; outcome=FAIL.
