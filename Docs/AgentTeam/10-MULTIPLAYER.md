# Sinbinder — подробный рабочий план сессии МУЛЬТИПЛЕЕР
Владелец: ежечасная сессия «Проверка мультиплеера Sinbinder», запуск около :11.
Обязательный общий регламент: 00-TEAM-CHARTER.md. Главный ранее утверждённый handoff: Assets/Scripts/docs/57-MULTIPLAYER-PRIORITIES-HANDOFF.md; GitHub issue #2.
Это backlog и сценарии проверок, не список уже реализованного.

## 1. Конечная цель и граница ответственности
Два человека на разных компьютерах видят один настоящий мир Sinbinder и могут независимо управлять двумя Греховодами. На сервере живут настоящие Warrior, AOS, Damageable, SoulManager, BindingDevice, экономика, туман и условия победы. Клиент передаёт намерение, а не результат урона, решения воина или скрытые данные. После сетевой партии одиночное демо остаётся прежним.
Основная зона: Assets/Scripts/Multiplayer, TCP/room/service и тонкие согласованные gameplay bridges. Gameplay/AOS/клиентская визуализация являются смежными зонами, работать с ответственными сессиями через issue #3.

## 2. Честный baseline
В коде уже есть LanRoomServer/Client, LanMessage, LanWire, MatchAuthority, NetworkActorIdentity, ServerWarriorOrderBridge, ServerWorldProbe, LanWorldState, LanLatencyFairness, LanRoomDirectory, LanServerBookmarks, ограниченные snapshots позиции/HP, gameOrder/gameAccepted/gameResolved; есть отдельная арена капсул и test world probe. RoomDirectory — не рабочее защищённое публичное лобби. TCP test с Unity-shim не равен общей Unity-карте. Гарантий Linux dedicated и двух физических клиентов нет. Не повторять фундамент без конкретной причины.

## 3. Рабочие фазы и контрольные точки
### M0. Реальная Unity-сборка и сквозной тест (P0, S–M)
M0.1: Проверить HEAD/CI и исходное положение PR №1. Если CI красный из-за внешнего tutorial, задокументировать, не лгать, будто все workflow зелёные. Исправлять общий workflow только вместе с QA и владельцем.
M0.2: Проверить сборочный путь «Собрать игру.cmd» → Tools/play/build-game.ps1 → Unity 6000.3.22f1 Build, список сцен, multiplayer test scene, Packages/manifest.json, .meta, asmdef, используемые API. Провал импорта/компиляции — блокер всех следующих основных функций.
M0.3: Test-World-Auto.cmd: глава сервера в headless, два реальных Unity Player, gameMatch, личные ID, MatchAuthority, реальное NavMesh движение и HP; в логах единый match ID, host/server tick, запрет чужих команд, нормальный exit code. Не достаточно нарисованных капсул.
M0.4: Проверить Tab/V и одиночный вход/выход; возврат камер/SelectionManager и управление в Field, не оставлять живой server world после закрытия матча.
Приёмка M0: L3+L5 доказаны логами на одном SHA; QA независимо сверил результаты. При отсутствии Unity сделать только preflight, пометить блокер и не объявлять M0 завершённым.

### M1. Общая реальная карта и два игрока (P1, L–XL)
M1.1: Явно разделить server world и client view; одна выбранная MapData/seed, карта из проверенного источника, одинаковые координаты и геометрия. Не создавать на клиентах второго самостоятельного авторитетного MapLoader с RivalLord AI/уроном.
M1.2: Два Греховода и два безопасных seat→LordId. RivalLord.NetworkControlled подавляет только стратегический ИИ на серверном хосте и восстанавливается при завершении; при respawn переслать новый ID. Не ломать соперника в соло.
M1.3: Actor lifecycle: authoritative GUID/ServerId, role/side/owner, create/despawn, HP, смерть, новое тело и rebind. Регистрация не может зависеть только от периодического сканирования, если важное событие рождает актёра между snapshots.
M1.4: Снимки содержат настоящий игровой мир, а не 24 случайно выбранных воина навсегда; снять диагностический лимит только после видимости, bounds, bytes, batching/area interest management. Стабильные tick/sequence/snapshotId, атомарное применение, устранение ghost-actors.
M1.5: Client interpolation визуальных позиций отдельно от логики, без клиентской симуляции AOS, здоровья и NavMesh. Клиппинг и телепорты проверить при jitter/пропуске snapshot.
M1.6: Два клиента получают видимые реальные Warrior и NPC, собственный Lord выбран, второй управляем, нет двойных AI приказов, повторного spawn или скрытых позиций.
Приёмка M1: реальные два Unity окна с одинаковой картой, собственными сторонами, валидированными актёрами и отсутствием скрытых противников в трафике; L5, затем L6.

### M2. Команды и AOS как игровое действие (P1–P2, L)
M2.1: Tab — тактика/первое лицо, V — Командир/Воин. Командир: select LMB, order RMB; Воин: attack LMB, block RMB. Клиент передаёт intent, сервер проверяет slot/Lord/actor/range/current state.
M2.2: Move, Attack, Patrol, Hold и профильные приказы перевести на существующий ServerWarriorOrderBridge; у каждой команды один серверный CommandId/ExecuteTick и ответ только адресату.
M2.3: Акустика Voice: server-side расстояние/приглушение, некорректный голос/невидимая цель отклоняются. Нельзя верить client x/y/z цели атаки. Neutral не должен становиться собственностью последнего говорившего.
M2.4: Разделить gameAccepted (прошла авторизация), gameResolved (передана в Warrior) и AOSVerdict (согласие/отказ/причины). Для нескольких Греховодов сохранить суд настоящих 13 голосов, память, отношения, fear и доверие.
M2.5: Запрет повтора/replay, серверный sequence cursor на reconnect, отмена устаревших/невалидных команд после смерти/смены стороны; тесты race: target died between command admission and tick.
Приёмка M2: два Греховода по очереди и одновременно обращаются к одному нейтральному, сервер подтверждает намерение и реальный AOS результат, клиент правдиво показывает отказ и последствия.

### M3. Бой и игровые состояния (P2–P3, XL)
M3.1: Серверный CombatManager/UnitMover/Damageable: проверка hit origin, дистанции, коллайдеров и окон блокирования. Не принимать client «урон N» и client timestamps как истину.
M3.2: По одному уникальному ID на событие атаки/блока/урона/смерти; клиент только показывает VFX/звук; не посылать скрытую информацию.
M3.3: HP, stamina/эффекты и смерть имеют последовательность и авторитет; убийство и revive не дублируются при потере packets/повторном подключении.
M3.4: SoulManager, Remains, BindingDevice, BindingSocket, трон, Satchel: действия и расход ресурсов — атомарные серверные транзакции, недопустимо использовать одну душу/тело дважды.
M3.5: Правило победы/поражения, Туг-матч, таймеры волн и нейтральные события — единственный серверный источник и только серверная остановка/завершение.
Приёмка M3: законченный PvP матч с отрядом и AOS, смертью, возвратом души, алтарём и победителем; воспроизводимый сценарий и логи.

### M4. Server Fog/безопасность (P3–P4, L–XL)
M4.1: Серверный отдельный per-side FOW (не UI FogOfWar.Hides) и проверка видимости в командах, snapshots, combat events, HP и идентификаторах hidden actors.
M4.2: Проверить прямой просмотр TCP payload модифицированным клиентом, не только «невидимость в UI». Скрытый актёр не появляется в данных клиента.
M4.3: Разделить публичную и приватную информацию о карте, прогрессе, талантах, AOS и позиции воина; события, которые слышны, не всегда дают точный ActorId/координату.
M4.4: Invalid bytes/message sizes/NaN, replay, rate limit, password/tokens: запрет на выполнение/показ без серверной валидации.
Приёмка M4: последовательные privacy tests и adversarial TCP tests; все тайные данные остаются на сервере.

### M5. Серверы, комнаты, reconnect (P4, XL)
M5.1: Защищённая сессия/TLS и аутентификация ДО room passwords. Отделить сетевой IO thread от Unity gameplay thread; password KDF проверять вне main thread; rate limit по IP/пользователю/комнате.
M5.2: Встроить имеющийся LanRoomDirectory в настоящий API создания/списка/входа/выхода. Комната: имя, публичная/закрытая, пароль, владелец, slots, карта, ready, правила, mode, старт, остановка; права владельца проверяет сервер.
M5.3: Не обещать «до четырёх игроков», пока сервер реально рассчитан на 2 Lords. 3–4 стороны только после отдельного расширения сетевой модели и геймплейной карты.
M5.4: Избранные IP в LanServerBookmarks оставить локально, добавить LAN discovery без раскрытия приватных комнат; HTTPS server directory и invitation codes — позже, с auth/security.
M5.5: Resume по настоящей сессионной идентичности, не по TCP slot; grace period, timeout, защита от похищения чужого Lord, full snapshot + event cursor, контролируемый AI takeover.
M5.6: Linux/Windows Dedicated Server, конфиг порта/каталога/логов, сигнал shutdown, watchdog, VPS firewall и безопасный update.
Приёмка M5: пользователь создаёт закрытую комнату и безопасно подключает друга; wrong password/подмена отклоняются; reconnect сохраняет сторону; VPS закрыт от неавторизованного клиента.

### M6. Задержка и оптимизация (P4–P5, L)
M6.1: Off/Conservative/Balanced существующей LanLatencyFairness измерить на реальных 20/80/150/250 ms RTT и jitter. Сервер измеряет RTT, но игрок может задерживать ответы на ping probes: тестировать exploit.
M6.2: Не менять скорость серверной AOS или simulation tick ради fairness; дополнительные 100/150 ms только к разрешённым входным намерениям. Для attack/block проверить отзывчивость и unfair advantage.
M6.3: Производительность: 20Hz server tick, snapshot bandwidth, сетевые очереди, garbage, 20/50/100+ Warrior, поведение при Slow client; bounded backlog/backpressure, нет hidden state leaks.
M6.4: 30–60 min real match, завершение/дисконнект во всех состояниях, быстрый повторный матч без memory/socket leak.
Приёмка M6: измеренные результаты CPU/RAM/RTT/packet loss, без искусственных обещаний одинакового пинга.

## 4. Тестовый набор (не пропускать)
- Authorization: чужой LordId, neutral Warrior, выдача перед смертью, reconnect seat replay.
- AOS: два конкурирующих Lord приказа, реальный verdict, memory/reputation effect, страх/спасение.
- State: spawn/despawn, kill/revive/ownership, empty snapshot, partial frame, stale frame, fog revoke.
- Combat: duplicate hit, simultaneous block/attack, target dead, displaced target, ping jitter.
- Resources: две стороны и одна душа/тело/алтарь, no duplicate.
- Network: malformed, oversize, reconnect, slow read, heartbeat, idle timeout, graceful shutdown, TLS auth.
- Solo: Field, Tab/V, LMB/RMB, AOS unchanged after LAN.

## 5. Порядок проверки каждого этапа
Сначала L0/L1/L2 + реальные CI на новом SHA. Для GameObjects, NavMesh, UI, Combat, Audio нужен L3–L5; для сетевой цели как минимум L6. В отчёте каждый раз явно назвать, что недоступно. В PR указать коды, результаты и список того, что НЕ работает. По P0 при отсутствии Unity не объявлять игру готовой; можно делать безопасные подготовительные контракты.

## 6. Границы и передачи
- Визуалу: пакет client-visible событий (spawn/death/attack) с точной видимостью и требованиями к VFX/anim, не просить его решать авторитетность.
- Логике: инварианты AOS, soul accounting, fairness, сценарии конкуренции.
- UX: gameAccepted/gameResolved/AOSVerdict, сообщения отказа, reconnect UI и понятность задержек.
- QA: протокол теста L3–L6, все обнаруженные Unity API несовместимости и блокеры.
Передавать только подтверждённые факты через issue #3. Пожелания и необязательные фичи — после полноценного матча.
