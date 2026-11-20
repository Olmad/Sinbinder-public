# Э0 — сведение работ Sinbinder в единую ветку

**Дата:** 20.11.2026. **Единственный действующий план:** [69 — Единый план](69-PLAN.md).
**Это журнал интеграции, не отдельная дорожная карта.** Подтверждение в GitHub не заменяет Unity, кадры или прохождение автором.

## Встроено в GitHub-ветку `feature/server-prototype`

| Источник | Результат | Проверка / граница доказательства |
|---|---|---|
| Загруженный автором `69-PLAN.md` | **Встроено без смысловых изменений**: [Docs/69-PLAN.md](69-PLAN.md), commit `554ddc6fc286a0abe5063b848680e1db3910b3ee` | Source-истина. Старые №80/81/82 — архив |
| PR [№6](https://github.com/Olmad/Sinbinder-public/pull/6), terrain + visibility fix | **Уже был merged 19.11** в `feature/server-prototype`; повторно не применять | Код в ветке, не считать новый Unity PASS |
| PR [№7](https://github.com/Olmad/Sinbinder-public/pull/7), WorldForge best improvements | **Merged 20.11**, commit [`6e06c6b`](https://github.com/Olmad/Sinbinder-public/commit/6e06c6b56abb23e6ea0bd7524815e0d0b540c90c). Перенос выделенных объектов, snap, Undo, фокус камеры, копия карты, диагностика, шаблоны групп | [Map editor compile and ECA source checks PASS](https://github.com/Olmad/Sinbinder-public/actions/runs/44697235552). **НЕ Unity** |
| PR №8 (layout), №9 (templates), №10 (rotation preview), №11 (mesh ghost) | **Все уже merged** в цепочку PR №7 19.11 и попали в общую ветку через №7; не выполнять повторные cherry-pick | Их содержание входит в merged tree; нужен осмотр и тест в настоящем редакторе |
| `fix/worldforge-fog-2026-11-19` (отдельный невстроенный коммит) | **Минимальный эквивалент перенесён в рабочий `FogOfWar.Install()`**: [код `787e1e2`](https://github.com/Olmad/Sinbinder-public/commit/787e1e294b41441da1611567ba9a9345634402e0), [регрессия `95a9508`](https://github.com/Olmad/Sinbinder-public/commit/95a9508c4bd408697e3e5b6414025d8434e5697d), [CI-триггер `a983bf2`](https://github.com/Olmad/Sinbinder-public/commit/a983bf21879faa3146407930b17e11515978cf14). Это **перенос изменения**, не буквальный merge ветки | [Map editor visibility hotfix guard PASS](https://github.com/Olmad/Sinbinder-public/actions/runs/44698209822): в редакторе FOW отключён до поиска GroundNavMesh; обычные сцены сохраняют установку FOW. **Unity визуально НЕ ПОДТВЕРЖДЕНА** |
| LAN после интеграции | [`44697235554` PASS](https://github.com/Olmad/Sinbinder-public/actions/runs/44697235554) | .NET сервер, test-stubs + TCP; не 2 Unity-клиента |
| Unity cloud preflight | [`44697235557` PASS по shape checks](https://github.com/Olmad/Sinbinder-public/actions/runs/44697235557) | **Реальный Unity 6000.3.22f1 import/EditMode job SKIPPED**, значит Э0 ступень 2 НЕ ДОСТИГНУТА |

## Остаётся разрозненным или недоступным

| Ресурс | Наблюдаемая ситуация | Решение/следующий безопасный шаг |
|---|---|---|
| GitHub `main` | На 20.11 `feature/server-prototype` была впереди `main` на 2135 коммитов без отставания; `main` на `3612cf83`. **Это не подтверждает** синхронизацию локальной основной ветки автора | **НЕ делать массовый merge в удалённый main** без контрольной копии и сохранения канонического сна из документа 68 |
| Авторская ветка `claude/3d-horror-survival-game-ckrslc` / PR №1 | `feature/server-prototype` и авторская ветка разошлись (перед интеграцией: +1246/−28 коммитов относительно авторской). Авторский сон **не считается защищённым автоматическим merge** | PR №1 оставить draft; при локальном слиянии сравнить/сохранить канонические `DreamTutorialDirector`, `DreamWalkthrough` и связанные карты/сцены по документу 68; перенести лишь полезные доказанные правки |
| Прочие удалённые ветки WorldForge (`integration/worldforge-unified`, `feature/worldforge-*`) | На 20.11 `integration/worldforge-unified` и ветки best/layout/templates/rotation/mesh-ghost **являются предками HEAD** (сравнение GitHub: ahead=0). `feature/map-editor-terrain` и `fix/map-editor-black-screen` формально diverged, но их результат ранее включён через PR №6; не выполнять повторное слепое слияние | Вердикт по известным изменениям: **ALREADY INTEGRATED / SUPERSEDED**; при точечном сравнении текущих файлов не возвращать старые сцены и настройки |
| Локальная `codex/worldforge-map-validation` | **Не найдена среди удалённых GitHub branches** на момент проверки. По №69 содержит валидацию карт, Undo до 200 шагов и восстановление черновика | Нужен доступ к локальной ветке (push или patch); **нельзя** объявить слитой по названию |
| Локальный stash `console-wc3-wip-29.09` | GitHub API не имеет доступа к локальному stash | На ПК автора сравнить панель команд с текущей и принять/отклонить изменения с причинами |
| `D:\\Claude\\Sinbinder_v303_codex_fix.zip`, 10 карт с рельефом и форты | Файл не предоставлен в данной рабочей среде; №69 относит импорт карт к **Э4**, форты и гарнизоны — после демо | Не добавлять вторую систему рельефа; когда архив будет доступен, конвертировать карты в WorldForge |
| Крысы (306) и сон (304–305) | **По авторскому плану №69** уже встроены 05.11; в этом проходе отдельные игровые кадры и runtime не получены | Считать заявленным ранее состоянием, не новым проверенным результатом |
| Пять независимых текстовых аудитов | 20.11 [коммит `2e529f3`](https://github.com/Olmad/Sinbinder-public/commit/2e529f37b023ef387bd284fa9dcdb42069579dfa): последняя строка №3 `ProjectSettings/XRSettings.asset` восстановлена до 10 столбцов добавлением **одного** пустого `evidence`-поля. 582 baseline-пути и оценки сохранены. [CI `44698498207` FAIL](https://github.com/Olmad/Sinbinder-public/actions/runs/44698498207): проверка `HEAD^` читает ещё повреждённый предыдущий TSV и ошибочно воспринимает старые строки №3 как новые без QA2 | **Формат текущего файла восстановлен; CI ещё НЕ ЗЕЛЁНЫЙ**. Нельзя добавлять фиктивные QA2 или объявлять аудит закрытым; при следующем содержательном изменении QA3 на чистом сравнении проверить заново |

## Дополнение Э0: контроль достоверности пяти аудитов, 20.11

Восстановлена корректность валидатора исторических TSV, **без послаблений для неподтверждённого PASS**:

- [`df7bf5a`](https://github.com/Olmad/Sinbinder-public/commit/df7bf5acfbe00f8e1689566e424c2e143ba498a2): поддержан **только** старый случай потери последней пустой ячейки evidence в UNINSPECTED-строке; произвольная порча остаётся ошибкой.
- [`65bdd99`](https://github.com/Olmad/Sinbinder-public/commit/65bdd9969104f021db92ba972168a44250791682): отсутствие старого реестра не отключает требование QA2 у новых записей.
- [`5fdedca`](https://github.com/Olmad/Sinbinder-public/commit/5fdedca70311079be70a704fa9a55eb84b7457a6): пять отрицательных/положительных тестов для исторического восстановления и защиты QA2.
- [`f95a52d`](https://github.com/Olmad/Sinbinder-public/commit/f95a52d1b8b564db1d3b02d7983d59c407f670e6): статус файла `UNKNOWN_CONTEXT` / `NEEDS_CONTEXT` логически эквивалентен `UNKNOWN` последней порции; при наличии уже подтверждённых `failed>0` в общей строке FAIL/PARTIAL_FAIL очередная порция UNKNOWN не стирает старую ошибку. `PASS` по-прежнему требует полного доказательства.
- [`09fbbea`](https://github.com/Olmad/Sinbinder-public/commit/09fbbea25f2f51051165232bdc5c1b8ae0f6bf90): ещё четыре теста на эти соглашения, в том числе запрет скрыть FAIL или фальсифицировать PASS.

**Итог CI:** [44699738516](https://github.com/Olmad/Sinbinder-public/actions/runs/44699738516): **32/32 контрактных теста валидатора PASS**, но общий шаг `Validate five independent ledgers` **FAIL** с двумя оставшимися настоящими ошибками:

1. Аудитор №2: `Assets/Scripts/Gameplay/MapEditor.cs` = `UNINSPECTED`, но поле evidence содержит посторонний блок QA2 по `MapLogicRunner.cs`; нельзя считать MapEditor проверенным. Передано персональной независимой сессии №2, с требованием сохранить delta-доказательство в отдельном файле.
2. Аудитор №5: `Assets/Scripts/AOS Engine/BattleNarrator.cs` имеет `QA2 source=Tools/logic/battle-narrator/Program.cs` вместо принадлежности исходному BattleNarrator; доказательства тестового harness и действительный SHA кода нужно развести. Передано персональной сессии №5 без фиктивной приёмки.

**Не считать** устранение двух ошибок равным полной смысловой проверке пяти независимых проходов. Unity всё ещё не выполнена в этой облачной сессии. Перенос в авторскую ветку/локальные файлы/Unity-приёмку — зона локального Codex по текущему распоряжению автора.

---

## Критерий закрытия Э0

1. Свести локальный авторский `main` и GitHub-изменения с явным сохранением сна по №68, без потери GUID/.meta и сейвов.
2. `codex/worldforge-map-validation` и stash: иметь вердикт **MERGED** или **REJECTED** с конкретным основанием.
3. Компиляция на целевой Unity, чистый прогон **одного** авторского сна; лог, SHA и желательно кадр. Не подменять это GitHub CI.

**Распределение ответственности 20.11:** пользователь поручил локальному Codex слияние авторской основы, локальные ветки/stash и финальную Unity-проверку. Этот проход работает только с удалёнными GitHub-ветками и source-тестами, не вмешиваясь в локальный сон и `main`.

**Статус Э0:** **PARTIAL / NOT ACCEPTED**. Пакет WorldForge встроен в удалённую рабочую ветку; канонический сон, локальные изменения и реальный Unity-прогон ещё не приняты. При выполнении Э0 не переносить автоматически на Э1 или Э2.
