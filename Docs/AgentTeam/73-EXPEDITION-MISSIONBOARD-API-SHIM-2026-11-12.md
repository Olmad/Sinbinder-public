# Sinbinder: проверка реальной MissionBoard через Unity API shim

**Дата:** 12.11.2026  
**Ветка:** `feature/server-prototype`  
**Статус:** production-код компилируется и выполняется в headless API-shim стенде; 7 GitHub Actions успешны. Unity Editor/PlayMode **не запускались**.

## Зачем

В предыдущих итерациях `MissionBoard` проверялась преимущественно статическими правилами: «существует вызов», «есть поле снимка». А `AosSessionChecks.csproj` компилировал настоящие `AutoBattleResolver` и `Expedition`, но не сам компонент-посредник `MissionBoard`. У сложного жизненного цикла «отправка → переход → бой → сохранение → возвращение → награда» могли остаться невидимые статическим проверкам ошибки.

По запросу пользователя интеграция расширена до API shim без необходимости установленного Unity Editor.

## Что действительно компилируется

`Tools/expeditions/BoardShimChecks.csproj` с `BOARD_SHIM` собирает **неизменённые production-файлы**:
- `Assets/Scripts/Crypt/MissionBoard.cs`, `MissionCatalog.cs`, `JunctionCatalog.cs`;
- `Assets/Scripts/AOS Engine/AutoBattleResolver.cs`, `BattleRecord.cs`;
- `Assets/Scripts/Gameplay/Expedition.cs`, `ExpeditionBattleCheckpoint.cs`, `ExpeditionJourneyTiming.cs`, `ExpeditionHealthRules.cs`;
- `ExpeditionObjectiveRules.cs`, `ExpeditionAutoLoot.cs`, `ExpeditionCombatFacts.cs`, `ExpeditionReplayData.cs`, `ExpeditionReplayArchive.cs`.

`BoardShimStubs.cs` подменяет только неизбежные для headless запуска внешние объекты: упрощённую `UnityEngine` (`MonoBehaviour`, `Object`, `Mathf`, `Time`, `GameObject`), реестр, инвентарь, сохранение, интерфейсные текстовые сервисы и **`BehaviourResolver`**. Это важное ограничение: 13 реальных голосующих модулей в BoardShim не исполняются.

`BoardShimTests.cs` вызывает **реальные** методы `MissionBoard.Send`, `MissionBoard.AdvanceJourneys`, `MissionBoard.SnapshotJourneys`, `MissionBoard.RestoreJourneys`. Для имитации дискового снимка сериализует публичные поля через `System.Text.Json` с `IncludeFields=true`, затем восстанавливает; это *не* родной Unity `JsonUtility`.

## Проверенные сценарии

1. Отправка отряда резервирует настоящих участников и создаёт один сохранённый поход; отправка не вызывает мгновенный бой.
2. Через 15 игровых секунд начинается `Phase=3`, и `BattleCheckpoint.CompletedRounds=0`, событий боя нет.
3. Следующий тик, равный `BattleRoundSeconds`, выполняет **один** боевой ход; запись содержит только фактически совершённые действия.
4. JSON-снимок после первого хода можно восстановить через production `MissionBoard.RestoreJourneys`; ID оригинальных бойцов и завершённый номер хода сохраняются. Следующее событие записывается как второй ход.
5. Бой доигрывается до результата, `BattleCheckpoint` очищается лишь после закреплённого итогового `EncounterResolved=true`, появляется `Phase=2`.
6. В другой сцене истекший таймер возвращения не выдаёт золото, не меняет физический состав и не архивирует законченный реплей.
7. После прибытия в склеп одно возвращение выдаёт добычу, архивирует полную запись и освобождает состав. Повторный `AdvanceJourneys` не повторяет выплату или реплей.
8. Дублированный ID внутри JSON-снимка вызывает `CombatBlocked`; отправленный отряд остаётся в очереди, награда не начисляется, испорченный ход не переисполняется.
9. Во время `PersonalExpedition.Active` автоматическое автосохранение не прерывает лично посещаемую карту.
10. **Два параллельных отряда** в разных миссиях имеют различные `JourneyId`, отдельные HP/ID и последовательности AOS. Снимок JSON и восстановление сохраняют обе боевые истории; обе поездки завершаются после возвращения в склеп без двойной выплаты.

В GitHub Actions для коммита `b28f015678f4c27309ac6c01e9c3558210e7affd` выведены:
- `PASS: 32 real MissionBoard Unity API shim scenarios, JSON roundtrips and single payout`.
- `PASS: 47 real AOS session, one-round progression, and legacy equivalence checks`.
- Подтверждены также остальные наборы .NET и Python в `Expedition collection integrity`, AOS order, LAN smoke и прочие CI (7/7).

## Запуск в доступной среде

```bash
dotnet run --project Tools/expeditions/BoardShimChecks.csproj -c Release
dotnet run --project Tools/expeditions/AosSessionChecks.csproj -c Release
```

Первый тест входит в `.github/workflows/expedition-integrity.yml` и выполняется при PR-изменениях.

## Границы и следующий уровень проверки

- **Не проверяется полный AOS**: вместо всех 13 реальных модулей в тестовом `BehaviourResolver` заранее определённый ответ. Приоритет дальнейшей интеграции: компилировать оригинальный резолвер и подключать настоящие модули, не теряя независимость от Unity.
- **Не проверяются Unity-сцены**: физика, навигация, анимации, `MonoBehaviour` Awake/OnDestroy/Update и реальный `DontDestroyOnLoad` исполняются только в Unity. Для них нужен PlayMode.
- **Не проверяется реальный Unity JSON**: `System.Text.Json` проверяет поля и логику восстановления, а не точное поведение `JsonUtility` (особенно вложенных классов/полей).
- **Не доказана транзакционность при аварийном прерывании посреди выплаты**: тест гарантирует отсутствие повторов при нормальном продолжении времени и восстановлении до оплаты. Для полной гарантии нужна идентифицируемая запись уже применённого `JourneyId` и атомарное сохранение счета/реестра/архива, либо иной проверенный протокол коммита.
- **Не проверен серверный авторитет мультиплеера**: несколько клиентов не должны самостоятельно выполнять одни и те же боевые ходы.
- Повреждение чужих сохранений, блокировка человека на карте и загрузка прерванного Unity процесса всё ещё требуют ручных сценариев.

## Рекомендации QA

Отдельно в Unity PlayMode проверить: переход в другую сцену во время `Phase=3`; перезапуск на 1-м/5-м/последнем боевом ходе; два одновременно идущих похода; возвращение с нулём выживших; личную карту Греховода; душу/секрет только с личным присутствием; физический инвентарь; LAN host/client, Tab/V и 3D-сцены двух карт.

**Не считать автоматические .NET проверки заменой полной Unity-интеграции.**
