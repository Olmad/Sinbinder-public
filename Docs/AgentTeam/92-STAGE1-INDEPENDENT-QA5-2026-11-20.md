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


## QA №5: новый блок SinReplies[Gluttony], 20.11.2026

Независимо прочитаны семь ответов `VillageDialogueRules.SinReplies[Gluttony]` (строки 92–100), caller `VillageLines.Exchange` (18–35) и открывающая реплика `SinOpening` (89–99). Production blobs: `VillageDialogueRules.cs` `4a690bc08d783562c89f20aa8790c4b3548a48bd`, `VillageLines.cs` `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`, `en.txt` `413688e1522d7f0f95ae42fd04806170b157caf5` (локализация примерно строки 5998–6025; фрагмент 5970–6020 прочитан). Новых source-level исполняемых тестов и Unity не было.

Условия: оба участника с Soul, `a.Soul.Sin=Gluttony`, `b.Soul.Sin` определяет столбец; `TopicIndex(k)==Sin` либо `Lord` без `Lords.Player`; `b` не Mute. Вступление: «Что-то есть хочется. Опять.» Это субъективное состояние говорящего, не утверждение о существовании еды. `SinAnswer` не проверяет инвентарь отвечающего.

- Greed: «На чужой ужин не рассчитывай.» / «Don't count on someone else's supper.» — UNKNOWN_CONTEXT, запрет/совет без подтверждённого события.
- Pride: «Тебе только еда на уме?» / «Is food all you think about?» — UNKNOWN_CONTEXT, вопрос, согласуется с вступлением.
- Wrath: «Не ной. Лучше дров подкинь.» / «Stop moaning. Put another log on.» — UNKNOWN_CONTEXT, совет предполагает доступные дрова; инвентарь не проверен.
- Envy: «Тебе хочется, а мне что достанется?» / «You're hungry, but what will I get?» — UNKNOWN_CONTEXT, вопрос о доле.
- Lust: «Разделим хлеб, коли найдётся.» / «We'll share some bread if we find any.» — UNKNOWN_CONTEXT, корректное условное обещание.
- Gluttony: «У меня горбушка осталась. Пополам?» / «I've got a heel of bread left. Half each?» — **FAIL source-level**: обе версии утверждают наличие хлеба у `b` без проверки его вещей.
- Sloth: «Мне бы тоже, да вставать лень.» / EN **UNKNOWN**: в извлечённом фрагменте перевода нет этой пары; не считать проверенной.

**Воспроизводимый отрицательный сценарий:** `a.Sin=Gluttony`, `b.Sin=Gluttony`, обе Soul есть, нет Mute, `Topic=Sin`, у `b` нет хлеба. Вызов `SinAnswer(Gluttony,Gluttony)` возвращает утверждение о наличии горбушки, поскольку метод индексирует только два греха. Это дефект фактической обоснованности текста, не доказательство конкретного игрового показа. **Итог блока: 7 READ, 0 SEMANTIC VERIFIED, 1 FAIL, 6 UNKNOWN; 6/7 RU/EN пар сопоставлены, последняя UNKNOWN.** Минимальная правка: заменить утверждение о собственном хлебе на условное предложение, не менять механики.

QA2: source=Assets/Scripts/Dialogue/VillageDialogueRules.cs; blob=4a690bc08d783562c89f20aa8790c4b3548a48bd; lines=92-100; context=VillageLines.Exchange topic Sin and speaker Gluttony, responder Gluttony, no food inventory predicate; negative=responder owns no bread but claims to have a heel; verified=0; total=7; RU_EN=6 compared with en.txt blob 413688e1522d7f0f95ae42fd04806170b157caf5, Sloth UNKNOWN; outcome=FAIL.

**Этап 1 остаётся NO-GO.** Исторический TSV не менялся; runtime и независимая приёмка исправления UNKNOWN.


## QA №5: закрытие ранее неизвестной EN-пары Gluttony/Sloth, 20.11.2026

Новая конкретная проверка (не повторное начисление READ): в актуальном `Assets/Resources/Lang/en.txt` Git blob `961bf2ac4e9eb09b37dbf169ef8c0630152519ef`, строки 6023–6024, найдено точное соответствие ключу из `Assets/Scripts/Dialogue/VillageDialogueRules.cs` blob `4a690bc08d783562c89f20aa8790c4b3548a48bd`, `SinReplies[Gluttony][Sloth]`: RU «Мне бы тоже, да вставать лень.» / EN «I'd like some too, but I can't be bothered to get up.»

Caller проверен по `Assets/Scripts/Dialogue/VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`, строки 18–35 и 89–99: `Exchange(a,b)`, оба `Soul != null`, `a.Sin=Gluttony`, `b.Sin=Sloth`, `Topic=Sin` (либо `Lord` при отсутствии `Lords.Player`), отвечающий не Mute. Вступление «Что-то есть хочется. Опять.»; ответ сообщает о желании и лени, но не утверждает наличие еды, предмета или свидетеля. Отрицательный пример: у обоих нет еды; обе реплики остаются логически совместимы с этим состоянием. EN передаёт желание и нежелание вставать, без дополнительного факта. **Вердикт для этой ограниченной пары: PASS_SOURCE_SEMANTICS; runtime UNKNOWN** (реальный выбор темы и отображение в Unity не проверялись). Ранее указанное `Sloth EN UNKNOWN` исправлено: для ряда Gluttony теперь **7/7 пар RU/EN сопоставлены**, но весь ряд по-прежнему имеет `FAIL` на Gluttony/Gluttony и не получает полного PASS.

QA2: source=Assets/Scripts/Dialogue/VillageDialogueRules.cs; blob=4a690bc08d783562c89f20aa8790c4b3548a48bd; lines=SinReplies[Gluttony][Sloth]; context=VillageLines.Exchange -> SinOpening Gluttony -> SinAnswer(Gluttony,Sloth), responder not Mute; negative=no bread in inventory, but the answer asserts only desire and laziness; verified=1; total=1; RU_EN=exact match en.txt blob 961bf2ac4e9eb09b37dbf169ef8c0630152519ef lines 6023-6024; outcome=PASS_SOURCE_SEMANTICS; runtime=UNKNOWN.

Новый инкремент: READ=0 (повторно открытый блок), SEMANTIC VERIFIED source-level=1 пара, FAIL=0, закрыт один прежний EN UNKNOWN. Исполняемые .NET, Unity и CI в этом сеансе не запускались. Общий статус этапа 1 **NO-GO**. Исторический TSV №5 не изменён.


## QA №5: VillageLines Order/Event EN coverage, 20.11.2026

Новый ограниченный source-level блок: прочитаны `Assets/Scripts/Dialogue/VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`, строки 18–35, 68–86; `Assets/Resources/Lang/en.txt` blob `278672b5b86a49357e301411a03f945ddb1c3579`, строки 6054–6118. Проверено 13 отдельных ответов: семь `OrderAnswer` (Wrath, Pride, Sloth, Greed, Envy, Lust, default Gluttony) и шесть `EventAnswer` (Greed, Wrath, Sloth, Pride, Envy, default Wrath/Lust/Gluttony). У всех 13 точных `msgid` имеется `msgstr ""`, то есть нет содержательного английского перевода. Дополнительно `LordTalk` при Respected использует `Наш-то Греховод — не худший из них.`, у которого `msgstr ""`. Это 14 проверенных пустых EN-значений, не 14 доказанных показов русского текста в Unity.

**Условия:** `Exchange(a,b)` требует `a.Soul,b.Soul != null`, говорящий не Mute; `TopicIndex(k)==Order` выбирает `OrderAnswer(b)` при немом=false; `TopicIndex(k)==Event` и `Nearest(a)!=null` выбирает `EventAnswer(b)` при немом=false. Если события рядом нет, Event переводится в Sin. Для Lord/Respected нужны `Lords.Player!=null`, `Allegiance.Of(a.Id,a.Loyalty,Lords.Player)>=Hostility.Follows`. Пустые EN значения доказаны по файлу, но поведение `Loc.T` при пустом переводе и runtime язык остаются UNKNOWN до чтения реализации Loc и Unity.

**Контрпример:** английский язык выбран, воин Wrath отвечает на тему Order: `Loc.T("Пусть приходят. Топор наточен.")` имеет пустой `msgstr`; следовательно, полноценный EN-эквивалент отсутствует в проверенном каталоге. Отдельный смысловой риск: `Поговаривают, Орден снова на тропах` оформлено как слух, а не доказанный факт, поэтому не классифицируется автоматически как ложное событие. `Топор наточен` утверждает состояние оружия без проверки экипировки: это потенциальный source-level factual FAIL, но связь оружия и текстового утверждения требует отдельной проверки.

**Вердикт:** `FAIL_EN_COVERAGE_SOURCE` для 14 точных ключей; `UNKNOWN_RUNTIME` для фактического fallback/display; `SEMANTIC VERIFIED=0` для контекста реальных сцен. Проверка не является полным PASS для Order/Event/Lord, не изменяет ранее проверенные строки. Исполняемые .NET, CI и Unity в этом запуске не запускались. Минимальный следующий шаг для разработчика №4: заполнить EN для указанных ключей без изменения механик, затем независимый QA переводов и `Loc.T` fallback.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs; blob=23dadf30fbdd93cf6b1070dec9a54612de7f4cba; lines=18-35,68-86 and LordTalk; context=Exchange -> OrderAnswer/EventAnswer/LordTalk gated by TopicIndex, Nearest, Mute, Allegiance; negative=Order Wrath msgid has empty EN msgstr and Event Greed msgid has empty EN msgstr; verified=0; total=14; RU_EN=14 empty msgstr in en.txt blob 278672b5b86a49357e301411a03f945ddb1c3579 lines 6062-6118; outcome=FAIL_EN_COVERAGE_SOURCE.

Этап 1: **NO-GO**. Исторический TSV №5 и другие реестры не менялись.


## QA №5: независимая проверка SinOpening RU/EN, HEAD 2f3f5d95 (20.11.2026)

Прочитан законченный блок всех 7 ветвей `VillageLines.SinOpening` и соответствующие ключи `en.txt` (локализация строки 6110–6150 приблизительно; диапазон проверен через GitHub line-slice 6055–6160). Источники: `Assets/Scripts/Dialogue/VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`; `Assets/Resources/Lang/en.txt` blob `a8e5a95cd3d5b9fc6cf337d6ba3935a8835a2255`. Caller: `VillageLines.Exchange` при наличии обеих Soul, немой говорящий исключён; тема Sin либо Lord без Lords.Player. Ветка Envy выбирает мужскую/женскую формулировку по `b.Gender`.

Проверка семи грехов: Greed `Сколько у тебя припрятано? Только честно.` EN пуст; Pride `В этой деревне только я чего-то стою.` EN заполнен; Wrath `Ещё день без дела — и я кого-нибудь пришибу.` EN пуст; Sloth `Хорошо сидим. Не вставай.` EN пуст; Envy мужская `Видал, что у соседа новое?` EN пуст, женская `Видала, что у соседа новое?` EN заполнен; Lust `У очага теплее, когда не один.` EN пуст; Gluttony `Что-то есть хочется. Опять.` EN пуст. Итого **6 пустых EN из 8 гендерно-различимых ключей** (семь ветвей); 2 ключа имеют непустой перевод. Это новый проверенный блок, не повтор предыдущих 14 Order/Event/Lord ключей. Все 6 пустых ключей являются **FAIL_EN_COVERAGE_SOURCE**, но отображение в Unity и fallback `Loc.T` остаются UNKNOWN. Дополнительно Envy opening задаёт вопрос о неподтверждённой обновке, уже известный семантический FAIL; его не считаем новым дефектом.

Контрпример: `a.Soul.Sin=Greed`, `b` не немой, `Topic=Sin`; открывающий текст выбирается, но `en.txt` содержит пустой `msgstr` для точного русского ключа. Другой пример: Envy + мужской `b.Gender` выбирает пустой перевод, тогда как женская версия заполнена. Это доказывает несогласованность локализации двух гендерных путей без утверждения о конкретном Unity-выводе.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Resources/Lang/en.txt; blob=23dadf30fbdd93cf6b1070dec9a54612de7f4cba; lines=SinOpening Greed/Pride/Wrath/Sloth/Envy male+female/Lust/Gluttony; context=Exchange topic Sin or Lord without player Lord and speaker not mute; negative=Greed opening and male Envy opening have empty EN msgstr while female Envy has translation; verified=0; total=8; RU_EN=6 empty EN, 2 nonempty, localization blob a8e5a95cd3d5b9fc6cf337d6ba3935a8835a2255; outcome=FAIL.

Новых source-level FAIL-ключей EN: 6; новых полностью SEMANTIC VERIFIED: 0; новых runtime проверок: 0. Предыдущие 14 отсутствующих переводов сохраняются отдельным набором: суммарно по двум независимо указанным блокам 20 пустых ключей, без заявления о полном количестве по всей локализации. **Этап 1 NO-GO**. .NET, Unity и CI в этом запуске не запускались. Необходимо заполнить шесть EN значений и отдельно проверить фактологию Envy и последствия реплик.


## QA №5: новый доказанный FAIL, пол говорящего в SinOpening/Envy (20.11.2026)

Независимо прочитаны **три** актуальных источника: `Assets/Scripts/Dialogue/VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba` (строки 18–37, 89–100); `Assets/Scripts/Core/Grammar.cs` blob `1b8a1a099e9474ae251b6d4314d97827bd2b159c` (строки 6–9, 35–36); `Assets/Resources/Lang/en.txt` blob `03d46e460af2f5e616f9635de0e570958969502c` (строки 6143–6148). Сверка выполнена по текущему содержимому ветки `feature/server-prototype`, не по старым SHA локализации.

**Воспроизводимый source-level контрпример:** `Exchange(a,b)` возвращает `SinOpening(a,b)` как **First**, то есть произносимую `a` реплику. Для `a.Soul.Sin=Envy`, `a.Gender=Female`, `b.Gender=Male`, немого `a` нет, тема Sin, `SinOpening` использует `Grammar.Pick(b.Gender, "Видал...", "Видала...")`. `Grammar.Pick` выбирает мужской текст для Male и женский для Female. **Фактически:** женщина `a` произносит «Видал, что у соседа новое?», вместо женского «Видала...». Обратный контрпример: мужчина `a`, женщина `b` → говорящий мужчина произносит «Видала...». При теме Lord и отсутствии `Lords.Player` тот же `SinOpening(a,b)` также достижим. Это **новый FAIL_GENDER_SPEAKER_SOURCE**, не повтор прежнего FAIL_EN_COVERAGE.

**RU/EN:** для мужского ключа `msgstr ""` (en.txt 6143–6144), для женского `msgstr "Have you seen what the neighbor got?"` (6147–6148). При ошибочно выбранном мужском ключе дополнительно срабатывает ранее зарегистрированный дефект пустого EN. Поведение `Loc.T` при пустом переводе и Unity не проверялись: runtime UNKNOWN.

**Минимальное исправление для №4:** заменить аргумент `b.Gender` на `a.Gender` **только в выборе формы речи SinOpening/Envy**; отдельно заполнить мужской EN ключ, затем проверить четыре комбинации `a.Gender × b.Gender` и две ветки caller (Sin / Lord fallback). Не изменять AOS, управление, сейвы, голоса и другие диалоги без доказательств.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Scripts/Core/Grammar.cs; blob=23dadf30fbdd93cf6b1070dec9a54612de7f4cba; lines=VillageLines.cs:18-37,89-100;Grammar.cs:35-36; context=Exchange(a,b)->SinOpening(a,b) as First, a.Soul.Sin=Envy, b.Gender used instead of a.Gender; negative=a.Gender=Female,b.Gender=Male yields masculine «Видал», and inverse yields feminine «Видала»; verified=0; total=2; RU_EN=en.txt blob 03d46e460af2f5e616f9635de0e570958969502c lines 6143-6148, male msgstr empty, female nonempty; outcome=FAIL.

**Новый результат:** 1 доказанный source-level FAIL в 2 гендерных комбинациях; 0 новых SEMANTIC VERIFIED; Unity/.NET/CI не запускались. Этап 1 остаётся **NO-GO**, независимая приёмка патча после исправления обязательна.


## QA №5: независимая трассировка происхождения тона LordTalk (21.11.2026)

HEAD при начале проверки: `3abb613c7def7faebb9193d7cb160353cc0166db`. Повторно прочитаны production-файлы `Assets/Scripts/Dialogue/VillageLines.cs` (blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`, строки 52–65) и `Assets/Scripts/Dialogue/VillageDialogueRules.cs` (blob `4a690bc08d783562c89f20aa8790c4b3548a48bd`, таблица `LordReplies`, метод `LordAnswer`). Это проверка происхождения контекста, не новый полный проход всех строк.

**Трасса:** `Exchange(a,b)` при теме Lord вызывает `LordTalk(a,b)`. `Allegiance.Of(a.Id, a.Loyalty, Lords.Player)` определяет и `first` говорящего `a`, и `VillageLordTone`, который передаётся в `LordAnswer(b.Soul.Sin, tone)` для отвечающего `b`. `Allegiance.Of(b.Id,b.Loyalty,Lords.Player)` здесь не вызывается. Поэтому тон ответа `b` определяется отношением **a**, а не **b**. Например, `a` уважает Греховода, `b` презирает: `b` получает строку из ряда `Respected`, хотя её собственное отношение другое. Это воспроизводимое **расхождение источника состояния**, но не автоматическое доказательство ложности каждой строки: часть ответов условна, часть может быть реакцией на чужое мнение.

**Вердикт: UNKNOWN_CONTEXT / QA_REVIEW_REQUIRED**, не `SEMANTIC VERIFIED` и не подтверждённый P1 без проверки семантики всех 21 комбинации `tone × b.Soul.Sin`. Не следует механически заменять `a` на `b`: это изменит смысл диалога. Проверить авторское намерение (ответ на мнение `a` или собственная позиция `b`), независимые контрпримеры для расходящихся `a/b` и RU/EN каждого ответа. Поскольку Unity и исполняемые тесты не запускались, runtime остаётся UNKNOWN. Патчи №4 по этому месту не принимались.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Scripts/Dialogue/VillageDialogueRules.cs; blob=23dadf30fbdd93cf6b1070dec9a54612de7f4cba,4a690bc08d783562c89f20aa8790c4b3548a48bd; lines=VillageLines.cs:52-65; context=Exchange->LordTalk->Allegiance.Of(a)->LordAnswer(b.Sin,tone(a)); negative=a respects Lord,b despises Lord, responder gets Respected row; verified=0; total=21 response-context combinations awaiting independent semantic acceptance; RU_EN=UNKNOWN for this block; outcome=UNKNOWN_CONTEXT. **Этап 1: NO-GO.**


## QA №5: отдельный контрпример фактической предпосылки в LordReplies (21.11.2026)

Проверены текущие source blobs: VillageLines.cs 23dadf30fbdd93cf6b1070dec9a54612de7f4cba; VillageDialogueRules.cs 4a690bc08d783562c89f20aa8790c4b3548a48bd. Путь вызова: Exchange(a,b) -> LordTalk(a,b) -> Allegiance.Of(a.Id,a.Loyalty,Lords.Player) -> LordAnswer(b.Soul.Sin,tone). Для tone=Distrusted и b.Soul.Sin=Lust выбирается «Не нравится мне, как он смотрит на людей.». Это высказывание от лица b предполагает наблюдение b за тем, как Греховод смотрит на людей; в рассмотренной цепочке нет проверки встречи, видимости, памяти либо наблюдения b. Контрпример входа: a относится к Греховоду отрицательно; b имеет грех Lust, но никогда не встречал Греховода и не наблюдал его взгляд. Ответ всё равно выбирается из таблицы. Отрицательное отношение a не доказывает наблюдения b. **Вердикт UNKNOWN_CONTEXT / FACT_SOURCE_UNPROVEN**, не semantic PASS; требуется проверить более широкую модель памяти/свидетельств, RU/EN и реальную достижимость контрпримера в Unity до присвоения severity. Не подменять отсутствие локальной проверки доказательством невозможности наблюдения во всей игре.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Scripts/Dialogue/VillageDialogueRules.cs; blob=23dadf30fbdd93cf6b1070dec9a54612de7f4cba,4a690bc08d783562c89f20aa8790c4b3548a48bd; context=LordTalk tone(a)=Distrusted, responder b.Sin=Lust, LordReplies[1][4]; negative=b has no recorded observation of Lord, phrase presupposes observed gaze; verified=0; total=1; RU_EN=UNKNOWN; runtime=UNKNOWN; outcome=UNKNOWN_CONTEXT. Исправления №4 не принимались. Этап 1 NO-GO.


## QA №5, 21.11.2026: независимая повторная сверка четырёх EN-ключей

Current `Assets/Resources/Lang/en.txt` blob `2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df` (GitHub fetch). `VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`; `VillageDialogueRules.cs` blob `4a690bc08d783562c89f20aa8790c4b3548a48bd`.

- «Видал, что у соседа новое?» → empty `msgstr`: FAIL_EN_COVERAGE_SOURCE.
- «Видала, что у соседа новое?» → `Have you seen what the neighbor got?`: EN present; gender selector `b.Gender` in speaker `a` line remains wrong.
- «К нему идут охотнее, чем ко мне.» → `They go to him more readily than to me.`: unsupported visit comparison persists in EN.
- «У меня горбушка осталась. Пополам?» → `I've got a heel of bread left. Half each?`: unsupported possession of bread persists in EN.

QA2: four exact entries checked against current EN blob; source-level only; Unity/.NET NOT_RUN; patch №4 not accepted; full changed-source delta UNKNOWN; Stage 1 NO-GO. Historical TSV unchanged.


## QA №5, 22.11.2026: актуализация evidence без ложного PASS

Независимая сверка `19b9f6210462f6b93cef7a42e0233ce9c703633b...feature/server-prototype`: ahead=31, behind=0; GitHub compare перечисляет только `88-INDEPENDENT-AUDITOR-1-PROGRESS.tsv` (4 изменения), №2 (24), №3 (22). Это сравнение не доказывает отсутствие исправлений до base SHA. Production patch №4 в указанном delta не выявлен.

Непосредственно перечитаны текущие source blobs: `VillageLines.cs=23dadf30fbdd93cf6b1070dec9a54612de7f4cba` (Exchange, строки 18–35); `VillageDialogueRules.cs=4a690bc08d783562c89f20aa8790c4b3548a48bd` (Gluttony/Gluttony, строка 99); `BattleNarrator.cs=974273d50701ef0676fb8390cadb72b4c83ff5e8` (Build); `en.txt=2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df`. Проверены точные RU/EN пары: `Видал, что у соседа новое?` → пустой `msgstr` (en.txt 6143–6144), `У меня горбушка осталась. Пополам?` → `I've got a heel of bread left. Half each?` (6019–6020), `Записей о ходе боя нет.` → `No battle events were recorded.` (11–12). Это повторное подтверждение ранее зарегистрированных проблем, **не новые дефекты и не дополнительные semantic PASS**.

Контрпример Gluttony/Gluttony сохраняется: Exchange выбирает SinAnswer по грехам без проверки хлеба в инвентаре, а реплика утверждает владение хлебом. EN сохраняет ту же неподтверждённую фактичность. BattleNarrator.Build для пустого/whitespace журнала не выдумывает послушание, но production caller и Unity не проверены. `verified=0`; RU_EN=FAIL для пустого перевода и FACT_SOURCE_UNPROVEN для хлеба; runtime=UNKNOWN. Патчи №4 не приняты. Исторический TSV №5 не переписывался. **Stage 1 NO-GO**, авторское разрешение на UX/механики отсутствует.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Scripts/Dialogue/VillageDialogueRules.cs + Assets/Resources/Lang/en.txt + Assets/Scripts/AOS Engine/BattleNarrator.cs; blobs=23dadf30fbdd93cf6b1070dec9a54612de7f4cba,4a690bc08d783562c89f20aa8790c4b3548a48bd,2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df,974273d50701ef0676fb8390cadb72b4c83ff5e8; context=Exchange topic Sin, Gluttony responder; negative=no bread yet asserts possession, and untranslated male Envy opening; verified=0; runtime=UNKNOWN; outcome=FAIL_SOURCE_LEVEL/NO_GO.


## QA №5, 22.11.2026: закрытие пробела EN для Gluttony/Sloth (source-level)

Сверены production `VillageDialogueRules.cs` blob `4a690bc08d783562c89f20aa8790c4b3548a48bd`, `VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba` и актуальный `en.txt` blob `2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df` (EN 5970–6035). Для `SinReplies[Gluttony][Sloth]` RU «Мне бы тоже, да вставать лень.» соответствует EN «I'd like some too, but I can't be bothered to get up.»: смысл совпадает, без утверждения о наличии пищи. Это **проверенная пара переводов**, но не semantic PASS сценария: контекст и runtime остаются UNKNOWN. В предыдущем блоке EN для этой пары был UNKNOWN из-за ограниченного диапазона чтения; теперь именно этот пробел устранён. Остальные семь ответов Gluttony не получают автоматического PASS; Gluttony/Gluttony с неподтверждённым хлебом остаётся source-level FAIL.

Контроль delta `19b9f6210462f6b93cef7a42e0233ce9c703633b...feature/server-prototype`: ahead=35, behind=0; затронуты только реестры №1–3 и этот QA-отчёт, production source в этом сравнении не изменился. Это не заменяет проверку исправлений №4 относительно их фактических parent SHA. Проверка выполнена чтением GitHub, .NET/Unity не запускались; 13 голосов, суд, Whisper, Tab/V, экспедиции, сейвы: runtime UNKNOWN. Исторический TSV №5 сохранён. Этап 1: **NO-GO**.

QA2: source=Assets/Scripts/Dialogue/VillageDialogueRules.cs + Assets/Resources/Lang/en.txt; blobs=4a690bc08d783562c89f20aa8790c4b3548a48bd,2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df; context=VillageLines.Exchange -> SinAnswer(Gluttony,Sloth), both souls present and unmuted, topic Sin; negative=no food or no standing action required for subjective reluctance; verified=0 full scenario; RU_EN=PAIR_MATCHED_SOURCE; runtime=UNKNOWN; outcome=UNKNOWN_CONTEXT.


## QA №5, 22.11.2026: дополнительный пропущенный EN-ключ Sloth (source-level)

Сверка base `19b9f6210462f6b93cef7a42e0233ce9c703633b` с `feature/server-prototype` перед записью: ahead=38, behind=0, delta содержит только реестры №1–3 и этот QA-отчёт, production-файлов нет. Независимо прочитаны `Assets/Scripts/Dialogue/VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba` (строки 89–100), `Assets/Resources/Lang/en.txt` blob `2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df` (строки 6138–6150), `VillageDialogueRules.cs` blob `4a690bc08d783562c89f20aa8790c4b3548a48bd` (строки 90–105).

**Новый отдельный FAIL_EN_COVERAGE_SOURCE:** `SinOpening` при `a.Soul.Sin=Sloth` вызывает `Loc.T("Хорошо сидим. Не вставай.")`, а соответствующая запись `en.txt` имеет `msgstr ""` (непосредственно перед ранее известным пустым мужским ключом Envy). Таким образом, выявлен **ещё один** пустой перевод критического деревенского диалога. Смысл реплики не утверждает проверяемый факт о сидении с уверенностью: контекст совместного пребывания/позы и поведение production `Loc.T` при пустом `msgstr` не исследованы, поэтому полному сценарию PASS не присваивается. Минимальная задача №4: заполнить EN перевод `Хорошо сидим. Не вставай.` с проверкой ключа, не меняя механику; затем независимая повторная приёмка и проверка fallback локализатора. Ранее известный пустой `Видал, что у соседа новое?` остаётся отдельным FAIL. Runtime Unity/.NET UNKNOWN.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Resources/Lang/en.txt; blobs=23dadf30fbdd93cf6b1070dec9a54612de7f4cba,2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df; lines=VillageLines:89-100;en.txt:6138-6150; context=SinOpening(a,b), a.Soul.Sin=Sloth; negative=reachable Sloth opening has empty English msgstr; verified=0; total=1; RU_EN=FAIL_EN_COVERAGE_SOURCE; runtime=UNKNOWN; outcome=FAIL_SOURCE_LEVEL. Исторический TSV №5 не изменён. Этап 1 NO-GO.


## QA №5: локализация VillageLines, независимая сверка 22.11.2026

Текущие source blobs: `VillageLines.cs` = `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`; `VillageDialogueRules.cs` = `4a690bc08d783562c89f20aa8790c4b3548a48bd`; `en.txt` = `2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df`. Сверен вызов `Exchange -> SinOpening` и локализация (фрагмент en.txt 6120–6160). При двух существующих Soul, немом статусе говорящего=false и выбранной теме Sin либо Lord без игрока соответствующие вступления достигаются по `a.Soul.Sin`. Конкретное появление в Unity не проверялось.

Подтверждены **пять пустых EN-переводов вступлений**, не один: Greed `Сколько у тебя припрятано? Только честно.`; Wrath `Ещё день без дела — и я кого-нибудь пришибу.`; Sloth `Хорошо сидим. Не вставай.`; Envy (мужская форма) `Видал, что у соседа новое?`; Lust `У очага теплее, когда не один.`; дополнительно Gluttony `Что-то есть хочется. Опять.` также пустой. Итого **шесть** пустых EN msgstr из семи ветвей `SinOpening` с учётом двух гендерных форм Envy (всего восемь RU-ключей, два непустых: Pride и Envy женская форма). `Loc.T` fallback/runtime поведение UNKNOWN; пустой msgstr не доказывает пустую строку на экране. Все шесть: `FAIL_EN_COVERAGE_SOURCE`, semantic PASS=0.

Отдельный риск: Envy male/female RU формулировки зависят от `b.Gender`; EN женская форма имеется, мужская пуста. Это может давать разный язык отображения в зависимости от пола адресата. Для обеих форм сохраняется ранее установленная проблема неподтверждённого факта обновки соседа. Исправление должно исключить выдуманное наблюдение, не просто заполнить перевод.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Resources/Lang/en.txt; blobs=23dadf30fbdd93cf6b1070dec9a54612de7f4cba,2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df; lines=VillageLines.SinOpening, en.txt 6120-6160; context=Exchange topic Sin or Lord fallback, a.Soul.Sin, Envy b.Gender; negative=6 msgstr empty; verified=0; total=8; RU_EN=FAIL six keys, two nonempty but full semantic context UNKNOWN; outcome=FAIL_EN_COVERAGE_SOURCE; runtime=UNKNOWN.

№4: требуется минимальное заполнение шести EN-ключей и устранение недоказанных фактов Envy с повторной независимой проверкой. До патча QA_ACCEPTED=NO. Полный этап 1 остаётся NO-GO. Исторический TSV №5 не менялся.


## QA №5: новый независимый контрпример Greed/SinReplies, 22.11.2026

**Область:** только два ответа на вопрос о припрятанном, не полный проход 7×7. Повторно получены production blobs: `VillageLines.cs` `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`; `VillageDialogueRules.cs` `4a690bc08d783562c89f20aa8790c4b3548a48bd`. EN-каталог `en.txt` на HEAD имеет blob `2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df`, но точные EN-пары этих двух ответов в этом проходе не извлечены: RU_EN=UNKNOWN.

**Вход:** `VillageLines.Exchange(a,b)`; обе `Soul != null`, оба не Mute; `TopicIndex(k)==Sin` (либо Lord при `Lords.Player==null`); `a.Soul.Sin=Greed`. Вступление: «Сколько у тебя припрятано? Только честно.»; `SinAnswer` выбирает ответ исключительно по `a.Soul.Sin` и `b.Soul.Sin`, не проверяя инвентарь или экипировку `b`.

1. `b.Soul.Sin=Wrath`: «Припрятал топор. Хочешь проверить?» утверждает наличие спрятанного топора. Контрпример: у отвечающего нет топора и отсутствует факт его сокрытия, но текст выбирается тем же индексом.
2. `b.Soul.Sin=Gluttony`: «Горбушка хлеба. До вечера не доживёт.» в контексте вопроса о запасах утверждает наличие хлеба. Контрпример: у отвечающего нет хлеба, но текст неизменен.

**Вердикт:** два новых `FAIL_SOURCE_FACT_PROVENANCE` в конкретных ветвях, без доказательства runtime-показа. Это отдельные случаи от ранее выявленного Gluttony/Gluttony. Минимальная правка №4: заменить безусловное утверждение о принадлежащих предметах на условное/гипотетическое высказывание, не меняя механику, затем сверить EN и повторно принять контрпримеры. Не считать утверждения о вещах фактом только потому, что они произнесены NPC. Исполняемые тесты и Unity не запускались.

QA2: source=Assets/Scripts/Dialogue/VillageDialogueRules.cs; blob=4a690bc08d783562c89f20aa8790c4b3548a48bd; lines=SinReplies Greed row; caller=VillageLines.Exchange blob 23dadf30fbdd93cf6b1070dec9a54612de7f4cba; context=topic Sin, speaker Greed, responder Wrath or Gluttony, no inventory/equipment predicate; negative=no axe and no bread yet respective possession claims; verified=0; total=2; RU_EN=UNKNOWN (EN catalog blob 2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df); outcome=FAIL_SOURCE_FACT_PROVENANCE; runtime=UNKNOWN.

**Stage 1: NO-GO.** Исторический TSV №5 не изменён; регрессии 13 голосов, суда, Whisper, Tab/V, экспедиций и сохранений не проверялись в Unity.


## QA №5: независимая EN-сверка Greed/Wrath и Greed/Gluttony, 22.11.2026

Контроль GitHub compare: base `19b9f6210462f6b93cef7a42e0233ce9c703633b` → `feature/server-prototype`: ahead=47, behind=0; changed files: только реестры аудиторов №1–3 и этот отчёт, production delta в данном диапазоне отсутствует. Сверены актуальные source blobs: `VillageDialogueRules.cs=4a690bc08d783562c89f20aa8790c4b3548a48bd`, `VillageLines.cs=23dadf30fbdd93cf6b1070dec9a54612de7f4cba`, `en.txt=2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df`.

Уточнение прежнего QA2 с RU_EN=UNKNOWN: `SinReplies[Greed][Wrath]` RU «Припрятал топор. Хочешь проверить?» ↔ EN `I've hidden an axe. Want to check?`; `SinReplies[Greed][Gluttony]` RU «Горбушка хлеба. До вечера не доживёт.» ↔ EN `A heel of bread. It won't last until evening.`. Обе EN-пары непустые, смысл утверждений о сокрытом топоре и наличии хлеба сохранён. Значит, **RU_EN=PAIR_MATCHED_SOURCE** для этих двух пар, но **FACT_SOURCE=FAIL** остаётся: `Exchange` выбирает ответы по грехам без проверки инвентаря/факта сокрытия. Никакого semantic PASS сценария не присваивается.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Scripts/Dialogue/VillageDialogueRules.cs + Assets/Resources/Lang/en.txt; blobs=23dadf30fbdd93cf6b1070dec9a54612de7f4cba,4a690bc08d783562c89f20aa8790c4b3548a48bd,2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df; caller=Exchange -> SinAnswer; context=topic Sin, a.Sin=Greed, b.Sin=Wrath/Gluttony, speaker not Mute; negative=no axe/no bread; verified=0; RU_EN=PAIR_MATCHED_SOURCE; FACT_SOURCE=FAIL; runtime=UNKNOWN; patch4_acceptance=NO; stage1=NO-GO. Исторический TSV №5 не изменён. Unity/.NET NOT_RUN.


## QA №5: независимый контрпример OrderAnswer/Wrath, 22.11.2026

Проверена актуальная ветка `feature/server-prototype` против `19b9f6210462f6b93cef7a42e0233ce9c703633b`: ahead=50, behind=0, delta только реестры аудиторов №1–3 и отчёт №5; production delta после этой базы отсутствует (не доказательство отсутствия более ранних изменений). Прочитан production `Assets/Scripts/Dialogue/VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba`, строки 18–35 и 63–78, и `VillageDialogueRules.cs` blob `4a690bc08d783562c89f20aa8790c4b3548a48bd` для сопоставления ранее зарегистрированного Greed/Wrath. `en.txt` blob `2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df` получен, но EN-пара новой реплики не проверена.

**Новый самостоятельный FAIL_SOURCE_FACT_PROVENANCE:** `VillageLines.OrderAnswer(b)` при `b.Soul.Sin=Wrath` безусловно выдаёт `Loc.T("Пусть приходят. Топор наточен.")`. Путь: `Exchange(a,b)`, оба `Soul != null`, говорящий `!CampLines.Mute(a)`, тема `Topic.Order`, отвечающий `!CampLines.Mute(b)`, грех Wrath. В `OrderAnswer` нет проверки оружия, заточки или памяти о подготовке. Контрпример: у Wrath-ответчика отсутствует топор, но текст заявляет о наточенном топоре. Это **отдельная ветвь** от уже зарегистрированного `SinReplies[Greed][Wrath]` «Припрятал топор». Не считать повторным дефектом той же строки. Доказана безусловность выбора текста на уровне исходника, не фактический runtime-показ. Исправление №4: заменить на реплику без утверждения о существующем наточенном оружии либо добавить доказанное условие; проверить EN и оба контрпримера независимо. Пока QA_ACCEPTED=NO, Unity runtime=UNKNOWN.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs; blob=23dadf30fbdd93cf6b1070dec9a54612de7f4cba; caller=Exchange -> Topic.Order -> OrderAnswer(b); context=b.Soul.Sin=Wrath, !Mute(a), !Mute(b); negative=no axe / no sharpening evidence; verified=0; total=1; RU_EN=UNKNOWN; outcome=FAIL_SOURCE_FACT_PROVENANCE; runtime=UNKNOWN; patch4_acceptance=NO; stage1=NO-GO. Исторический TSV №5 не изменён. Регрессии 13 голосов, SoulCourt, Whisper, Tab/V, экспедиций и сейвов в Unity не проверены.


## QA №5: независимая проверка OrderAnswer RU/EN, 22.11.2026 (source-level)

Контроль перед записью: base `19b9f6210462f6b93cef7a42e0233ce9c703633b` → `feature/server-prototype`: ahead=54, behind=0. Delta: только TSV №1–3 и отчёт №5; production-файлы в данном сравнении не изменены. Прочитаны `Assets/Scripts/Dialogue/VillageLines.cs` blob `23dadf30fbdd93cf6b1070dec9a54612de7f4cba` и `Assets/Resources/Lang/en.txt` blob `2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df`.

**Новый независимый результат: FAIL_EN_COVERAGE_SOURCE для всех семи ветвей OrderAnswer.** В `Exchange` при `Topic.Order` и `!CampLines.Mute(b)` вызывается `OrderAnswer(b)`, где ответ выбирается по `b.Soul.Sin`. Все семь RU msgid имеют пустые EN msgstr: Wrath «Пусть приходят. Топор наточен.»; Pride «Мы им не дичь.»; Sloth «Придут — тогда и встанем.»; Greed «С охотника снять есть что.»; Envy «К соседям бы сперва зашли.»; Lust «Лишь бы не к нашим очагам.»; default/Gluttony «Значит, спать вполглаза.». Отдельно подтверждены шесть пустых EN msgstr для `EventAnswer` в том же соседнем блоке en.txt: «Пойдёт тот, кому заплатят.»; «Я бы пошёл. Позвал бы кто.»; «Без меня сходят.»; «Позовут лучших — пойду.»; «Сходят другие — и всё им достанется.»; «Не наше это дело. Пока.». Для EventAnswer вызывающий путь в этой проверке не исследован до конца, поэтому отдельный scenario PASS/FAIL по достижимости не назначен. Вся группа требует минимального исправления EN и повторной независимой проверки №4.

Уточнение предыдущего QA2 по `OrderAnswer/Wrath`: RU_EN больше не UNKNOWN, а **FAIL_EN_COVERAGE_SOURCE**; факт о наточенном топоре остаётся самостоятельным `FAIL_SOURCE_FACT_PROVENANCE`. Пустой `msgstr` не доказывает пустой текст на экране: `Loc.T` fallback и Unity runtime UNKNOWN. Никаких новых semantic PASS, никакой приёмки патча №4. Исторический `88-INDEPENDENT-AUDITOR-5-PROGRESS.tsv` не изменять.

QA2: source=Assets/Scripts/Dialogue/VillageLines.cs + Assets/Resources/Lang/en.txt; blobs=23dadf30fbdd93cf6b1070dec9a54612de7f4cba,2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df; caller=Exchange -> Topic.Order -> OrderAnswer(b); context=both Soul present, !Mute(a), !Mute(b), TopicIndex=Order; negative=seven reachable sin branches have empty EN msgstr; verified_semantic=0; total_order_branches=7; RU_EN=FAIL_EN_COVERAGE_SOURCE; runtime=UNKNOWN; patch4_acceptance=NO; stage1=NO-GO.


## QA №5: новый независимый EN-gap кнопки пробуждения, 23.11.2026 (source-level)

Контроль `b0df08b9f00340db4c1d4dd4aa44159546a3ed86` → `feature/server-prototype` перед записью: ahead=9, behind=0, changed files только исторические TSV №1–3; production delta в этом диапазоне отсутствует. Прочитаны актуальные исходники `Assets/Scripts/UI/PauseMenu.cs` blob `f86e820b6f09e74ac1b2c003d4a6fe802084d31c`, `Assets/Resources/Lang/en.txt` blob `2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df`; дополнительный контекст `Assets/Scripts/Editor/DoorCheck.cs` blob `427ac91fc054b6d4cd57444f16cc8751d02c8030`.

**Новый FAIL_EN_COVERAGE_SOURCE, критический сон/обучение:** `PauseMenu.Draw` при `SceneManager.GetActiveScene().name == StartPanel.Dream` добавляет пункт `Loc.T("Проснуться")` с действием `Field("Prologue_Camp")`. В `en.txt` существует ключ `msgid "Проснуться"`, но `msgstr ""` (непосредственно после `Начать заново`, перед `Чужой клинок`). Это воспроизводимый source-level пробел локализации кнопки выхода из сна. Проверка полного поведения `Loc.T`, фактической доступности/отображения меню, перехода сцены и Unity runtime не выполнялась; не утверждать, что кнопка визуально пустая. `DoorCheck.cs` описывает сценарий «меню паузы → Проснуться → лагерь», но комментарий/описание теста не является доказательством успешного runtime.

QA2: source=Assets/Scripts/UI/PauseMenu.cs + Assets/Resources/Lang/en.txt; blobs=f86e820b6f09e74ac1b2c003d4a6fe802084d31c,2d7a907e150a0efaebc20ecc6b3c2b16cb4fa6df; context=active scene StartPanel.Dream, pause menu Draw; key=Проснуться; negative=EN msgstr empty; verified_semantic=0; total=1; RU_EN=FAIL_EN_COVERAGE_SOURCE; runtime=UNKNOWN; patch4_acceptance=NO; stage1=NO-GO. Минимальная задача №4: заполнить EN перевод ключа, проверить отсутствие конфликтующих ключей, затем независимая повторная приёмка вместе с переходом Dream→Prologue_Camp. Исторический TSV №5 не менять. Никаких semantic PASS или разрешений разработки.
