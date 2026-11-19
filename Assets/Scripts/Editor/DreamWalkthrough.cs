#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Прогон сна Бога Смерти (05.11): сцена до сих пор не запускалась ни разу,
    /// а на ней теперь стоит главный показ игры — отказ и суд души. Прогон
    /// собирает сцену, если её нет, входит в Play и ведёт сон за игрока:
    /// подойти, собрать душу, поднять, приказать, велеть отступить при убийцах —
    /// и снимает каждую реплику суда. Потом бой, тот же приказ без убийц,
    /// ворота, пробуждение. Отчёт — Logs/dream-walkthrough.txt, кадры —
    /// Docs/Образцы/сон. Выход с кодом: 0 — прошло, 1 — нет.
    ///
    /// Запуск: Unity -batchmode -executeMethod Sinbinder.EditorTools.DreamWalkthrough.Run
    /// (без -quit: прогон выходит сам).
    /// </summary>
    [InitializeOnLoad]
    public static class DreamWalkthrough
    {
        private const string Active = "Sinbinder.DreamWalkthrough.Active";
        private const string ReportPath = "Logs/dream-walkthrough.txt";
        private const string Shots = "Docs/Образцы/сон";
        private const string ScenePath = "Assets/Scenes/Prologue_Dream.unity";
        private const float Limit = 540f;   // суд с 06.11 — тринадцать судей, знакомство и слово каждого

        private static float _t0 = -1f, _stepAt, _lastPoke;
        private static string _step = "";
        private static int _errors, _spokenShot = -1, _spokenSeen = -1;
        private static float _spokenAt, _diveAt = -1f, _aimAt = -1f;
        private static int _diveShots;
        private static float _riseAt = -1f;
        private static bool _riseShot;
        private static float _walkAt = -1f;
        private static bool _walkShot, _seatShot;
        private static float _skipAt = -1f;
        private static bool _killersShot, _retestShot, _scalesShot, _foreseeShot, _retestIssued, _harvestTold, _done;
        private static bool _risenShot, _bridgeUpShot, _leverOrdered, _leverByRun, _leverHintShot;
        private static string _lastAim = "";
        private static bool _whisperTried, _whisperShot;
        private static float _legsAt = -10f, _leverOrderAt = -1f, _lowSince = -1f;
        private static string NL => Environment.NewLine;
        private static float _pausedAt = -1f;

        /// <summary>Шаг прогона — для верстака (Workbench: «ping», «стоп на шаге»).</summary>
        public static string Step => _step;

        static DreamWalkthrough()
        {
            if (!SessionState.GetBool(Active, false)) return;
            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;
            UI.Letterbox.Said -= Line;
            UI.Letterbox.Said += Line;
        }

        public static void Run()
        {
            EditorUtility.audioMasterMute = true;
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, "");

            if (!File.Exists(ScenePath))
            {
                Utilets.DemoSceneBuilder.BuildDreamTutorial();
                Write(File.Exists(ScenePath) ? "сцена сна собрана: " + ScenePath : "СЦЕНА СНА НЕ СОБРАЛАСЬ");
            }
            if (!File.Exists(ScenePath)) { if (Workbench.Hosting) Workbench.DreamEnded(); else EditorApplication.Exit(1); return; }

            SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;
        }

        private static void Write(string line)
            => File.AppendAllText(ReportPath, "[" + Time.realtimeSinceStartup.ToString("F0") + "] " + line + NL);

        private static string _lastLine;

        /// <summary>Реплика с полосы — в отчёт: сон целиком для вычитки (58-ROADMAP, КТ1 п. 12).</summary>
        private static void Line(string who, string line, bool thought)
        {
            if (string.IsNullOrEmpty(line) || line == _lastLine) return;
            _lastLine = line;
            string text = System.Text.RegularExpressions.Regex.Replace(line, "<[^>]+>", "").Replace('\n', '/');
            Write("    [РЕПЛИКА] " + (thought ? "(мысль) " : string.IsNullOrEmpty(who) ? "" : who + ": ") + text);
        }

        private static void Catch(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (type == LogType.Exception || type == LogType.Error)
            {
                _errors++;
                Write("  [ОШИБКА] " + message + (string.IsNullOrEmpty(stack) ? "" : " @ " + stack.Split('\n')[0]));
                return;
            }
            if (message.StartsWith("[СУД]") || message.StartsWith("[СОН] Душа") || message.StartsWith("[СОН] Решение")
                || message.StartsWith("[СОН] Тот же") || message.StartsWith("[СОН QA]") || message.StartsWith("[СОН-СТЕНД]"))
                Write("  " + message);
        }

        private static void Go(string step)
        {
            _step = step;
            _stepAt = Time.realtimeSinceStartup;
            Write("шаг: " + step);
            if (Workbench.StopAt(step)) Write("  [ВЕРСТАК] стоп на шаге — пауза");
        }

        private static void Snap(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            try { Utilets.Snapshot.Now(Shots, name); Write("  [КАДР] " + name); }
            catch (Exception e) { Write("  [СНИМОК НЕ ВЫШЕЛ] " + name + ": " + e.Message); }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || _done) return;
            float now = Time.realtimeSinceStartup;
            // Пауза верстака (стоп на шаге, осмотр): часы прогона стоят, иначе после неё сработают сроки шагов.
            if (EditorApplication.isPaused) { if (_pausedAt < 0f) _pausedAt = now; return; }
            if (_pausedAt >= 0f) { float held = now - _pausedAt; _pausedAt = -1f; if (_t0 >= 0f) _t0 += held; _stepAt += held; _lastPoke += held; }
            if (_t0 < 0f) { _t0 = now; Go("ждём сон"); }

            // Экран пакетного прогона — 640×480, кадр (Snapshot) — 1600×900: подписи суда
            // считались под 4:3, а мир рисовался под 16:9, и на кадрах подписи разъезжались
            // шире фигур (06.11). В игре экран и кадр одни — здесь делаем так же.
            var eye = Camera.main;
            if (eye != null && Mathf.Abs(eye.aspect - 16f / 9f) > 0.01f) eye.aspect = 16f / 9f;

            if (now - _t0 > Limit) { End(false, $"ЗАСТРЯЛО на шаге «{_step}», стадия сна {DreamTutorialDirector.StageName}"); return; }

            if (SceneManager.GetActiveScene().name == "Prologue_Camp")
            {
                End(true, "сон кончился, лагерь загружен");
                return;
            }

            var dream = DreamTutorialDirector.Instance;
            if (dream == null || dream.Hero == null) return;
            string stage = DreamTutorialDirector.StageName;
            bool poke = now - _lastPoke > 0.5f;
            if (poke) _lastPoke = now;

            // Стрелка к месту шага (19.11): куда она нацелена — в отчёт при каждой смене.
            if (UI.TaskList.Aimed != _lastAim) { _lastAim = UI.TaskList.Aimed; Write("  [СТРЕЛКА] " + (_lastAim == "" ? "нет" : _lastAim)); }
            CourtShots(now);
            FlightShot(dream, now);
            AnimateAll(now);

            // Только мост (отладка этапа): суд проматывается, конец — когда поднятый опустил мост.
            bool untilBridge = Environment.GetEnvironmentVariable("SINBINDER_UNTIL") == "bridge";
            if (untilBridge && SoulCourt.Sitting && poke) UI.Letterbox.Instance?.SkipAll();
            if (untilBridge && (_leverOrdered || _leverByRun) && !DreamRoad.BridgeRaised)
            {
                // Лёг — и по настилу снова можно пройти (вырез навмеша снят). Ждём, пока ляжет целиком:
                // под судом мир стоит, и настил замирает на полпути.
                if (!DreamRoad.BridgeFullyDown) return;
                if (_lowSince < 0f) _lowSince = now;
                if (now - _lowSince < 1f) return;
                var across = new UnityEngine.AI.NavMeshPath();
                UnityEngine.AI.NavMesh.SamplePosition(DreamRoad.BridgeWestEnd + new Vector3(-2f, 0f, 0f), out var wa, 3f, UnityEngine.AI.NavMesh.AllAreas);
                UnityEngine.AI.NavMesh.SamplePosition(DreamRoad.LeverStand, out var ea, 3f, UnityEngine.AI.NavMesh.AllAreas);
                UnityEngine.AI.NavMesh.CalculatePath(wa.position, ea.position, UnityEngine.AI.NavMesh.AllAreas, across);
                float len = 0f;
                for (int i = 0; i + 1 < across.corners.Length; i++) len += Vector3.Distance(across.corners[i], across.corners[i + 1]);
                End(across.status == UnityEngine.AI.NavMeshPathStatus.PathComplete && len < 30f,
                    $"мост опустил {(_leverByRun ? "прогон (душа не пошла; рычаг ей не поддался — верно)" : "поднятый")} (стадия {stage}); "
                    + $"путь по мосту: {across.status}, {len:F0} м");
                return;
            }
            if (untilBridge && (stage == "Jars" || stage == "Return"))
            {
                if (_step != "ждём мост")
                {
                    // Ещё раз к рычагу — как игрок после проверки: провожание кончается, решает душа.
                    Go("ждём мост");
                    _leverOrderAt = now + 0.6f;   // провожание к камню начинается кадром позже смены шага
                }
                var z = dream.Zombie;
                if (_leverOrderAt > 0f && now > _leverOrderAt && z != null && !z.IsDead)
                {
                    _leverOrderAt = -1f;
                    z.IssueCommand(CommandKind.Move, DreamRoad.LeverStand);
                    Write("  приказ к рычагу: " + DreamTutorialDirector.Legs(z));
                }
                {
                    // Отказ души — не поломка рычага: поломка — когда решил исполнить, а мост лежит поднятым.
                    var mind = z != null ? z.GetComponent<Sinbinder.AOS.AOSWarriorWrapper>() : null;
                    var d = mind != null && mind.LastContext != null ? mind.LastDecisionDetail : (Sinbinder.AOS.Decision?)null;
                    bool obeys = d.HasValue && !d.Value.RefusedCommand && !d.Value.Hesitated && d.Value.Action == Sinbinder.AOS.ActionType.ObeyCommand;
                    // Не пошла за 12 с — рычаг тянет прогон: проверить сам мост (лёг ли, открылся ли путь).
                    if (!obeys && !_leverByRun && now - _stepAt > 12f && DreamRoad.Instance != null)
                    {
                        _leverByRun = true;
                        Write($"  душа не пошла ({(d.HasValue ? d.Value.Action.ToString() : "нет решения")}) — рычаг тянет прогон");
                        DreamRoad.Instance.PullLever("прогон");
                    }
                    if (now - _stepAt > 45f)
                    {
                        End(false, $"мост не опустили за 45 с; решение души: {(d.HasValue ? d.Value.Action.ToString() : "нет")}"
                            + (obeys ? " — РЫЧАГ НЕ СРАБОТАЛ" : "") + "; " + DreamTutorialDirector.Legs(z));
                        return;
                    }
                }
                return;
            }
            switch (stage)
            {
                case "Approach":
                    if (_step != "подход")
                    {
                        Go("подход");
                        Snap("сон — начало");
                        // Дорога целиком (docs/64-DREAM-ROAD.md): от начала до склепа. Свечи
                        // загораются у идущего — для кадров издалека зажигаем все.
                        DreamRoad.Instance?.LightAll();
                        // Карта обучения (docs/68): проходима ли и куда ведёт; сверху — разметка целиком.
                        MapCheck();
                        MapShot("сон — карта обучения сверху", new Vector3(-62f, 0f, 2f), 150f);
                        ShotFrom("сон — опушка, путь на восток", DreamRoad.StarAt + new Vector3(-2.5f, 2.4f, -1.2f), DreamRoad.DyingAt + Vector3.up * 1.0f, clear: false);
                        ShotFrom("сон — стоянка убийц, чучело", new Vector3(-89f, 2.2f, 27.6f), DreamRoad.DummyAt + Vector3.up * 1.1f);
                        ShotFrom("сон — мост с рычагом", new Vector3(-69f, 2.6f, 26.8f), new Vector3(-53f, 1.0f, 29.6f));
                        ShotFrom("сон — засада, валуны", new Vector3(-31f, 3.0f, 21f), DreamRoad.AmbushAt + Vector3.up * 0.8f);
                        ShotFrom("сон — бревно через расщелину", new Vector3(-31.5f, 3.2f, -20.5f), new Vector3(-14f, 1.0f, -24f));
                        ShotFrom("сон — колонны, камень с банками", new Vector3(-17.5f, 2.3f, -26.6f), DreamRoad.ShrineAt + Vector3.up * 0.9f);
                        // С площади — по привычной дороге к склепу.
                        ShotFrom("сон — дорога к склепу", new Vector3(-6f, 9f, -30f), new Vector3(0f, 1.5f, 18f), clear: false);
                        ShotFrom("сон — ворота форта", new Vector3(3f, 2.6f, 3.5f), new Vector3(0f, 3.2f, 15f));
                        // Только карта (разметка, дыры) — без сна целиком: минута вместо пяти.
                        if (Environment.GetEnvironmentVariable("SINBINDER_MAP_ONLY") == "1") { End(true, "только карта"); return; }
                        Warp(dream.Hero, DreamRoad.Behind(dream.WoundedAt, 3.2f));
                    }
                    break;
                case "Harvest":
                    if (_step != "жатва") { Go("жатва"); Warp(dream.Hero, DreamRoad.Behind(dream.WoundedAt, 1.6f)); }
                    // Душа над телом (план 66, сцена 1; SoulGlow 16.11) — до жатвы, сбоку и сверху.
                    if (!_soulOverShot)
                    {
                        if (now - _stepAt < 0.8f) break;
                        _soulOverShot = true;
                        ShotFrom("сон — душа над телом", dream.WoundedAt + new Vector3(2.6f, 1.9f, -2.6f), dream.WoundedAt + Vector3.up * 0.9f);
                    }
                    if (poke)
                    {
                        var reaper = dream.Hero.GetComponent<SoulHarvester>();
                        if (reaper != null) reaper.HarvestNow();
                        else if (now - _stepAt > 3f) { End(false, "у Греховода нет SoulHarvester — жать нечем"); return; }
                    }
                    // Прогон 05.11 (сон №3) встал здесь: «SoulManager в сцене нет». Где он?
                    if (!_harvestTold && now - _stepAt > 4f)
                    {
                        _harvestTold = true;
                        var all = UnityEngine.Object.FindObjectsByType<SoulManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        Write($"  [ЖАТВА?] Instance {(SoulManager.Instance != null ? "есть" : "НЕТ")}; менеджеров душ найдено {all.Length}");
                        foreach (var m in all)
                            Write($"    {m.gameObject.name}: активен {m.gameObject.activeInHierarchy}, включён {m.enabled}, сцена {m.gameObject.scene.name}");
                    }
                    break;
                case "Rebind":
                    if (_step != "поднятие") { Go("поднятие"); Warp(dream.Hero, DreamRoad.Behind(dream.WoundedAt, 2f)); }
                    if (poke) dream.TestPress(KeyCode.R);
                    break;
                case "Command":
                    if (_step != "приказ") { Go("приказ"); }
                    // Поднятый из свежего тела — тот же человек, а не гниль (автор, 08.11).
                    // Камера на миг к нему, кадр, камера обратно: Snapshot рисует сразу.
                    if (!_risenShot && dream.Zombie != null && now - _stepAt > 0.6f && Camera.main != null)
                    {
                        _risenShot = true;
                        var cam = Camera.main.transform;
                        var keepAt = cam.position; var keepRot = cam.rotation; float keepFov = Camera.main.fieldOfView;
                        var who = dream.Zombie.transform;
                        var face = who.position + Vector3.up * 1.15f;
                        cam.position = face + who.forward * 2.3f + who.right * 0.8f + Vector3.up * 0.25f;
                        cam.rotation = Quaternion.LookRotation(face - cam.position, Vector3.up);
                        Camera.main.fieldOfView = 45f;
                        Snap("сон — поднятый, свежее тело");
                        cam.position = keepAt; cam.rotation = keepRot; Camera.main.fieldOfView = keepFov;
                    }
                    if (dream.Zombie != null && !dream.Zombie.HasCommand && now - _stepAt > 1f)
                        dream.Zombie.IssueCommand(CommandKind.Move, dream.Zombie.transform.position + new Vector3(-1.5f, 0f, -1.5f));
                    break;
                case "Standoff":
                    if (_step != "убийцы") Go("убийцы");
                    if (dream.Killers.Count >= 2 && !_killersShot && now - _stepAt > 1.5f && dream.Zombie != null)
                    {
                        _killersShot = true;
                        Snap("сон — убийцы вернулись");
                        Write($"  поднятый: {dream.Zombie.DisplayName} ({dream.Zombie.Soul.Sin}); убийцы в "
                            + $"{Vector3.Distance(dream.Zombie.transform.position, dream.Killers[0].transform.position):F1} м");
                    }
                    break;
                case "Chase":
                    // Убийцы бегут к мосту (docs/68, этап 2–3); Греховод идёт следом до настила.
                    if (_step != "погоня")
                    {
                        Go("погоня");
                        dream.Hero.GetComponent<UnitMover>()?.CommandMove(DreamRoad.BridgeWestEnd + new Vector3(-2f, 0f, 0f));
                    }
                    if (now - _legsAt > 3f)
                    {
                        _legsAt = now;
                        Write("    погоня: " + DreamTutorialDirector.Legs(dream.Killers.Count > 0 ? dream.Killers[0] : null)
                            + " | поднятый " + DreamTutorialDirector.Legs(dream.Zombie));
                    }
                    if (!_bridgeUpShot && DreamRoad.BridgeRaised && DreamRoad.BridgeFullyUp)
                    {
                        _bridgeUpShot = true;
                        ShotFrom("сон — мост подняли", new Vector3(-73f, 8.5f, 41f), new Vector3(-53f, 2.4f, 28.5f), clear: false);
                    }
                    break;
                case "Bridge":
                    if (_step != "мост")
                    {
                        Go("мост");
                        Write($"  поднятый за рекой: {(dream.Zombie != null ? $"{dream.Zombie.transform.position.x:F1}; {dream.Zombie.transform.position.z:F1}" : "нет")}");
                        if (!_bridgeUpShot)
                        {
                            _bridgeUpShot = true;
                            ShotFrom("сон — мост подняли", new Vector3(-73f, 8.5f, 41f), new Vector3(-53f, 2.4f, 28.5f), clear: false);
                        }
                    }
                    // Как видит игрок: урок у поднятого и стрелка на рычаг (19.11) — до приказа.
                    if (!_leverHintShot && now - _stepAt > 0.7f) { _leverHintShot = true; Snap("сон — мост поднят, стрелка на рычаг"); Write($"  метка: нацелена «{UI.TaskList.Aimed}», видна «{UI.TaskList.Marked}»"); }
                    // Приказ к рычагу — как игрок: ПКМ по рычагу. Через секунду: сон ждёт приказа после отметки.
                    if (!_leverOrdered && now - _stepAt > 1f && dream.Zombie != null)
                    {
                        _leverOrdered = true;
                        dream.Zombie.IssueCommand(CommandKind.Move, DreamRoad.LeverStand);
                    }
                    break;
                case "Deciding":
                    if (_step != "решение") Go("решение");
                    if (!_scalesShot && DreamTutorialDirector.RefusalResult != null
                        && !SoulCourt.Sitting && now - _stepAt > 0.9f)
                    { _scalesShot = true; Snap("сон — весы над головой"); }
                    break;
                case "Battle":
                    if (_step != "бой")
                    {
                        Go("бой");
                        Write($"  итог мига: {DreamTutorialDirector.RefusalResult}; суд: {SoulCourt.Outcome}, судей {SoulCourt.Seated}: {SoulCourt.Bench}");
                    }
                    // Бой прогону не урок: через пятнадцать секунд убийц добивает сам прогон.
                    if (now - _stepAt > 15f)
                        foreach (var k in dream.Killers)
                        {
                            var hp = k != null ? k.GetComponent<Damageable>() : null;
                            if (hp != null && !hp.IsDead) hp.TakeDamage(hp.HP + 1f, dream.Hero.gameObject);
                        }
                    break;
                case "Retest":
                    if (_step != "тот же приказ") Go("тот же приказ");
                    // Весы до приказа (63-TEACHING, п. 7): выделить, прицелиться отступлением,
                    // как игрок по X, — и кадр, пока весы над ним отвечают до щелчка.
                    if (!_foreseeShot && now - _stepAt > 0.5f && dream.Zombie != null)
                    {
                        var manager = SelectionManager.Instance;
                        var unit = dream.Zombie.GetComponent<SelectionComponent>();
                        if (manager == null || unit == null) _foreseeShot = true;
                        else if (manager.Aiming != CommandKind.FallBack)
                        {
                            typeof(SelectionManager).GetMethod("SelectUnit",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                ?.Invoke(manager, new object[] { unit });
                            manager.Aim(CommandKind.FallBack);
                            _aimAt = now;
                            if (manager.Aiming != CommandKind.FallBack) { _foreseeShot = true; Write("  [ВЕСЫ ДО ПРИКАЗА] прицелиться не вышло"); }
                        }
                        else if (now - _aimAt > 0.9f)
                        {
                            _foreseeShot = true;
                            Snap("сон — весы до приказа");
                            manager.StopAiming();
                        }
                    }
                    // Через секунду, а не в кадр смены стадии: сон ждёт приказа «позже» своей отметки.
                    if (!_retestIssued && _foreseeShot && now - _stepAt > 1f && dream.Zombie != null)
                    {
                        _retestIssued = true;
                        dream.Zombie.IssueCommand(CommandKind.Move, DreamRoad.LeverStand);
                    }
                    var box = UI.Letterbox.Instance;
                    if (!_retestShot && box != null && box.Shown && box.Typed && now - _stepAt > 1.5f)
                    {
                        _retestShot = true;
                        Snap("сон — тот же приказ без убийц");
                        // Кнопка «пропустить» (17.11): Esc на этой строке — сцена должна уйти сразу.
                        _skipAt = now;
                        box.SkipAll();
                        Write("  [ПРОПУСК] Esc на строке повтора");
                    }
                    break;
                case "Whisper":
                    // «Снова нет» — шёпот (docs/68, вариант «Шёпот»): как игрок — подойти к отказавшему и Z.
                    if (_step != "шёпот") Go("шёпот");
                    if (!_whisperTried && now - _stepAt > 1f && dream.Zombie != null && dream.Hero != null)
                    {
                        _whisperTried = true;
                        var zp = dream.Zombie.transform.position;
                        var away = dream.Hero.transform.position - zp; away.y = 0f;
                        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                        if (away.magnitude > 5f) Warp(dream.Hero, zp + away.normalized * 4f);
                        bool started = Whisper.TryAny();
                        Write($"  Z — шепнуть: {(started ? "начат" : "НЕКОМУ")}; {DreamTutorialDirector.Legs(dream.Zombie)}");
                    }
                    // Итог — когда строка допечатана (первый кадр 19.11 поймал одну букву).
                    var said = UI.Letterbox.Instance;
                    if (!_whisperShot && Whisper.ShowingOutcome && said != null && said.Typed)
                    { _whisperShot = true; Snap("сон — шёпот после «снова нет»"); }
                    break;
                case "FirstPerson":
                case "WarriorMode":
                case "DrawWeapon":
                case "Practice":
                case "Jars":
                    if (_step != "управление")
                    {
                        Go("управление");
                        Write($"  тот же приказ без убийц: {DreamTutorialDirector.RetestResult ?? "не проверялся"}");
                        Write($"  мост: {(DreamRoad.BridgeRaised ? "поднят" : "опущен")}");
                        if (_skipAt > 0f)
                        {
                            Write($"  [ПРОПУСК] сцена ушла за {now - _skipAt:F2} с; полосы {(UI.Letterbox.Instance != null && UI.Letterbox.Instance.Covering ? "ещё на экране" : "ушли")}");
                            if (now - _skipAt > 2f) Write("  [ПРОПУСК] ДОЛГО: пропуск не сработал");
                            _skipAt = -1f;
                        }
                    }
                    if (EyesShots(dream, now)) dream.TestSkipControls();
                    break;
                case "Return":
                    if (_step != "пробуждение")
                    {
                        Go("пробуждение");
                        ShotFrom("сон — ворота склепа", new Vector3(2.5f, 2.2f, DreamRoad.GateZ - 8.5f), new Vector3(0f, 3.4f, DreamRoad.GateZ));
                        // Площадь печатей (12.11): сверху, с входа глазами, алтарь, арка с противовесом, печати.
                        ShotFrom("сон — площадь печатей сверху", new Vector3(0f, 16f, DreamRoad.SquareFrom - 6f), new Vector3(0f, 0f, (DreamRoad.SquareFrom + DreamRoad.SquareTo) * 0.5f));
                        ShotFrom("сон — площадь, глазами с входа", new Vector3(0.4f, 1.65f, DreamRoad.SquareFrom - 1f), new Vector3(0f, 2.5f, DreamRoad.GateZ));
                        ShotFrom("сон — алтарь лука", DreamRoad.AltarAt + new Vector3(2.8f, 1.9f, -2.6f), DreamRoad.AltarAt + Vector3.up * 0.9f);
                        ShotFrom("сон — арка противовеса", new Vector3(-3.5f, 2.0f, DreamRoad.ArchZ.z - 9f), new Vector3(0f, 4.0f, DreamRoad.ArchZ.z));
                        ShotFrom("сон — две печати у ворот", new Vector3(0f, 3.2f, DreamRoad.WarriorSealAt.z - 5f), new Vector3(0f, 0.3f, DreamRoad.WarriorSealAt.z));
                        // Печати — замок ворот (17.11): Греховод — на свою, поднятому — приказ на свою.
                        Warp(dream.Hero, DreamRoad.LordSealAt);
                        if (dream.Zombie != null && !dream.Zombie.IsDead)
                        {
                            Warp(dream.Zombie, DreamRoad.WarriorSealAt + new Vector3(0f, 0f, -4f));
                            _sealOrderAt = now + 1.5f;
                        }
                        else _raiseAt = now + 1.5f;
                    }
                    if (_sealOrderAt > 0f && now > _sealOrderAt)
                    {
                        _sealOrderAt = -1f;
                        if (dream.Zombie != null && !dream.Zombie.IsDead)
                        {
                            // Отказ — нарочно: ум поднятого выключен, приказ записан, а исполнять некому.
                            // Послушание у печати видно в прогонах 17.11 («встал поднятый у дороги»);
                            // здесь проверяется другое — отказ, подсказка и подъём стража.
                            var mind = dream.Zombie.GetComponent<Sinbinder.AOS.AutoAttackAOS>();
                            if (mind != null) mind.enabled = false;
                            dream.Zombie.IssueCommand(CommandKind.Move, DreamRoad.WarriorSealAt);
                            Write("  [ПЕЧАТИ] приказ поднятому: на печать с мечом (ум выключен — проверка отказа)");
                            _sealCheckAt = now + 12f;
                        }
                    }
                    // Не встал — половины нет (17.11): ворота ждут, поднимаем павшего стража (E у тела).
                    if (_sealCheckAt > 0f && now > _sealCheckAt)
                    {
                        _sealCheckAt = -1f;
                        if (!dream.SealsOpen)
                        {
                            Write($"  [ПЕЧАТИ] поднятый не встал за 12 с; задача: {UI.TaskList.Current}; плашка: {Lesson.Hinted}");
                            _raiseAt = now;
                        }
                    }
                    if (_raiseAt > 0f && now > _raiseAt && !dream.SealsOpen)
                    {
                        _raiseAt = -1f;
                        if (dream.Fallen.Count == 0) Debug.LogError("[ПЕЧАТИ] у печатей нет павших стражей — поднимать некого");
                        else
                        {
                            var body = dream.Fallen[dream.Fallen.Count - 1];
                            Warp(dream.Hero, body.transform.position + new Vector3(0f, 0f, -1.4f));
                            ShotFrom("сон — павший страж у печатей", body.transform.position + new Vector3(3.2f, 3.4f, -4.2f), body.transform.position);
                            _raisePressAt = now + 0.6f;
                        }
                    }
                    if (_raisePressAt > 0f && now > _raisePressAt)
                    {
                        _raisePressAt = -1f;
                        dream.TestPress(KeyCode.E);
                        _raiseOrderAt = now + 1.2f;
                    }
                    if (_raiseOrderAt > 0f && now > _raiseOrderAt)
                    {
                        _raiseOrderAt = -1f;
                        var risen = dream.LastRaised;
                        if (risen == null) Debug.LogError("[ПЕЧАТИ] E у павшего стража — никто не поднялся");
                        else
                        {
                            Write($"  [ПЕЧАТИ] поднят страж {risen.DisplayName}; плашка: {Lesson.Hinted}");
                            Warp(dream.Hero, DreamRoad.LordSealAt);
                            risen.IssueCommand(CommandKind.Move, DreamRoad.WarriorSealAt);
                        }
                    }
                    if (_sealShotAt < 0f && dream.SealsOpen) _sealShotAt = now + 0.6f;
                    if (!_sealShot && _sealShotAt > 0f && now > _sealShotAt)
                    {
                        _sealShot = true;
                        Write($"  [ПЕЧАТИ] {DreamTutorialDirector.SealsResult}; задача: {UI.TaskList.Current}; плашка: {Lesson.Hinted}");
                        ShotFrom("сон — печати горят", new Vector3(3.6f, 5.2f, DreamRoad.WarriorSealAt.z - 6.5f),
                                 new Vector3(0f, 0.3f, DreamRoad.WarriorSealAt.z + 0.4f));
                    }
                    if (poke && _sealShot && DreamRoad.CryptFade < 0f) dream.TestPress(KeyCode.F);
                    if (!_gateShot && DreamRoad.CryptFade > 0.15f) { _gateShot = true; Snap("сон — створки расходятся"); }
                    if (!_headShot && DreamRoad.CryptFade > 0.3f) { _headShot = true; Snap("сон — силуэт поднимает голову"); }
                    if (!_deathShot && DreamRoad.CryptFade > 0.45f) { _deathShot = true; Snap("сон — силуэт в склепе"); }
                    if (!_blackShot && DreamRoad.CryptFade >= 1f) { _blackShot = true; Snap("сон — экран погас"); }
                    break;
            }
        }

        private static bool _gateShot, _deathShot, _blackShot, _headShot, _sealShot;
        private static float _sealOrderAt = -1f, _sealShotAt = -1f, _sealCheckAt = -1f, _raiseAt = -1f, _raisePressAt = -1f, _raiseOrderAt = -1f;
        private static int _fpStep;
        private static float _fpAt;

        /// <summary>
        /// Глазами у чучела (автор, 08.11): оружие в руке справа; удар — чучело отшатывается
        /// по ходу удара. true — сняли, можно дальше.
        /// </summary>
        private static void EyeAtDummy(Camera cam, DreamTutorialDirector dream)
        {
            var eye = dream.Hero.transform.position + Vector3.up * 1.65f;
            var aim = DreamRoad.DummyAt + Vector3.up * 1.4f;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
            cam.fieldOfView = 60f;
        }

        private static float _animatedAt = -10f;

        /// <summary>
        /// В пакетном прогоне камера между кадрами не рисует, и Unity считает тела невидимыми: аниматор
        /// с отсечением стоит, и на кадрах убитый стоял в рост с огоньком души в животе (кадр
        /// «душа над телом», 16.11), хотя в игре он лежит. Раз в секунду — всем «анимировать всегда»,
        /// как лагерный прогон делает с Греховодом у костра (DemoWalkthrough).
        /// </summary>
        private static void AnimateAll(float now)
        {
            if (now - _animatedAt < 1f) return;
            _animatedAt = now;
            foreach (var a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (a.cullingMode != AnimatorCullingMode.AlwaysAnimate) a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private static bool _soulOverShot, _flightShot;
        private static float _flightSeen = -1f;

        /// <summary>Душа летит в банку (SoulFlight): кадр на трети полёта, с той же точки, что «душа над телом».</summary>
        private static void FlightShot(DreamTutorialDirector dream, float now)
        {
            if (_flightShot || Sinbinder.Gameplay.SoulFlight.InAir == 0) return;
            if (_flightSeen < 0f) { _flightSeen = now; Write("  душа в полёте к банке"); return; }
            if (now - _flightSeen < 0.3f) return;
            _flightShot = true;
            ShotFrom("сон — душа летит в банку", dream.WoundedAt + new Vector3(2.6f, 1.9f, -2.6f), dream.WoundedAt + new Vector3(0f, 1.3f, -0.9f));
        }

        private static void EyeAlongRoad(Camera cam, DreamTutorialDirector dream)
        {
            var eye = dream.Hero.transform.position + Vector3.up * 1.65f;
            var aim = new Vector3(0f, 1.2f, DreamRoad.GateZ);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
            cam.fieldOfView = 60f;
        }

        private static bool EyesShots(DreamTutorialDirector dream, float now)
        {
            var cam = Camera.main;
            var view = cam != null ? cam.GetComponent<RTS_Camera>() : null;
            if (view == null || dream.Hero == null) return true;
            switch (_fpStep)
            {
                case 0:
                    Warp(dream.Hero, DreamRoad.DummyFront(1.7f));
                    if (!view.FirstPersonNow) view.Switch();
                    _fpStep = 1;
                    _fpAt = now;
                    return false;
                case 1:
                    // Идёт сценка под полосами (зомби «Грабит», w0811d) — камера не в глазах:
                    // ждать её конца, иначе кадр и удар — из камеры сценки.
                    var talk = Sinbinder.Dialogue.DialogueCameraController.Instance;
                    if ((talk != null && (talk.InDialogue || talk.Homebound))
                        || (Sinbinder.UI.Letterbox.Instance != null && Sinbinder.UI.Letterbox.Instance.Covering))
                    {
                        if (now - _fpAt > 25f) { Write("  глазами: сценка не кончилась за 25 с — кадр пропущен"); _fpStep = 6; return true; }
                        return false;
                    }
                    if (!view.FirstPersonNow) return now - _fpAt > 3f;
                    if (!view.WarriorModeNow) view.SwitchRole();
                    view.Face(DreamRoad.DummyAt);
                    // В пакетном запуске у окна нет фокуса, и RTS_Camera глаза не ставит (кадр w0811e —
                    // вид сверху и посох перед ним): глаза — отсюда, как у кадра луны в прогоне лагеря.
                    EyeAtDummy(cam, dream);
                    if (now - _fpAt < 1.2f) return false;
                    Snap("сон — глазами, оружие в руке, чучело");
                    dream.Hero.GetComponent<FirstPersonCombat>()?.TestStrike();
                    _fpStep = 2;
                    _fpAt = now;
                    return false;
                case 2:
                    EyeAtDummy(cam, dream);
                    if (now - _fpAt < 0.2f) return false;
                    Snap("сон — удар, чучело отшатнулось");
                    Write($"  попаданий по чучелу: {DreamRoad.DummyHits}");
                    _fpStep = 3;
                    _fpAt = now;
                    return false;
                case 3:
                    // Ответ чучела (8e93245): через 0,5 с после удара руки идут вверх — кадр посреди замаха.
                    EyeAtDummy(cam, dream);
                    if (now - _fpAt < 0.55f) return false;
                    Snap("сон — чучело отвечает замахом");
                    Write($"  ответов чучела: {DreamRoad.DummyRipostes}, встречено блоком: {DreamRoad.DummyBlocks}");
                    // Душа в руке (16.11, SoulInHandView): у камня с банками, глазами на дорогу к воротам.
                    Warp(dream.Hero, DreamRoad.ShrineAt + new Vector3(-1.3f, 0f, -0.8f));
                    Sinbinder.Crypt.CryptHands.Drop();
                    Sinbinder.Crypt.CryptHands.TakeSoul(new Sinbinder.Core.SoulData("Путник у дороги", Sinbinder.Core.SinType.Sloth,
                        Sinbinder.Core.MoralType.Neutral, 1, 35f), Sinbinder.Core.SoulQuality.Acceptance);
                    _fpStep = 4;
                    _fpAt = now;
                    return false;
                case 4:
                    EyeAlongRoad(cam, dream);
                    if (now - _fpAt < 0.8f) return false;
                    Snap("сон — душа в руке");
                    Sinbinder.Crypt.CryptHands.Drop();
                    _fpStep = 5;
                    _fpAt = now;
                    return false;
                case 5:
                    if (now - _fpAt < 0.3f) return false;
                    if (view.FirstPersonNow) view.Switch();
                    // Назад к чучелу: убийцы встают у стоянки, погоня — к мосту (кадр души в руке — у колонн).
                    Warp(dream.Hero, DreamRoad.DummyFront(1.7f));
                    _fpStep = 6;
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>Кадр с отдельной точки: камера сна на миг встаёт туда и возвращается.</summary>
        /// <summary>
        /// Кадр с точки. clear — без тумана войны: место, каким его видит игрок, когда
        /// Греховод там (вблизи туман рассеян); иначе дальний план чёрный — не разведан.
        /// </summary>
        /// <summary>
        /// Карта обучения: проходимость по навмешу. Главный путь — по участкам, с проверкой, что идёт через мост
        /// и бревно (вода и расщелина не пропускают мимо), и лесная ветка — в обход реки к бревну.
        /// </summary>
        private static void MapCheck()
        {
            var path = new UnityEngine.AI.NavMeshPath();
            Vector3 OnMesh(Vector3 p) => UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position : p;
            void Leg(string what, Vector3 a, Vector3 b, Vector3? via, string viaName)
            {
                UnityEngine.AI.NavMesh.CalculatePath(OnMesh(a), OnMesh(b), UnityEngine.AI.NavMesh.AllAreas, path);
                float len = 0f;
                bool passes = !via.HasValue;
                var c = path.corners;
                for (int i = 0; i + 1 < c.Length; i++)
                {
                    len += Vector3.Distance(c[i], c[i + 1]);
                    if (via.HasValue && SegmentNear(c[i], c[i + 1], via.Value, 3f)) passes = true;
                }
                if (via.HasValue && c.Length == 1) passes = SegmentNear(c[0], c[0], via.Value, 3f);
                Write($"  [КАРТА] {what}: {path.status}, {len:F0} м, углов {c.Length}"
                      + (via.HasValue ? (passes ? $", через {viaName}" : $", МИМО: {viaName}") : ""));
                if (via.HasValue && !passes)
                    Write("    путь: " + string.Join(" ", System.Array.ConvertAll(c, p => $"({p.x:F0};{p.z:F0})")));
            }
            // Чего быть не должно: сквозь лес, в озеро — путь неполный; на воде и в расщелине навмеша нет.
            void Shut(string what, Vector3 a, Vector3 b)
            {
                UnityEngine.AI.NavMesh.CalculatePath(OnMesh(a), OnMesh(b), UnityEngine.AI.NavMesh.AllAreas, path);
                bool open = path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
                Write($"  [КАРТА] {what}: " + (open ? "ПРОХОДИМО — ДЫРА" : "закрыто"));
                if (open) Write("    путь: " + string.Join(" ", System.Array.ConvertAll(path.corners, p => $"({p.x:F1};{p.z:F1};{p.y:F2})")));
            }
            // Навмеш целиком — в файл: Tools/… рисует его сверху (где дыры в буреломе).
            var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            var dump = new System.Text.StringBuilder();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var v in tri.vertices) dump.Append(v.x.ToString("F2", inv)).Append(' ').Append(v.z.ToString("F2", inv)).Append(' ').Append(v.y.ToString("F2", inv)).Append('\n');
            dump.Append("#\n");
            for (int i = 0; i + 2 < tri.indices.Length; i += 3) dump.Append(tri.indices[i]).Append(' ').Append(tri.indices[i + 1]).Append(' ').Append(tri.indices[i + 2]).Append('\n');
            File.WriteAllText("Logs/dream-navmesh.txt", dump.ToString());
            void Ground(string what, Vector3 p, bool walkable)
            {
                bool has = UnityEngine.AI.NavMesh.SamplePosition(p, out _, 0.6f, UnityEngine.AI.NavMesh.AllAreas);
                Write($"  [КАРТА] {what}: " + (has == walkable ? (has ? "проход есть" : "прохода нет") : (has ? "ПРОХОД ЕСТЬ — ДЫРА" : "ПРОХОДА НЕТ")));
            }
            Shut("с лесной тропы в чащу", new Vector3(-100f, 0f, -17f), new Vector3(-100f, 0f, -8f));
            Shut("с дороги к засаде — в чащу у реки", new Vector3(-31f, 0f, 5f), new Vector3(-44f, 0f, 5f));
            Shut("с дороги — к озеру", new Vector3(-31f, 0f, -17f), new Vector3(-40f, 0f, -17.7f));
            Ground("река у моста", new Vector3(BridgeX, 0f, 33f), false);
            Ground("настил моста", new Vector3(BridgeX, 0.16f, 29f), true);
            Ground("расщелина", new Vector3(DreamRoad.ChasmX, 0f, -10f), false);
            Ground("расщелина у бревна", new Vector3(DreamRoad.ChasmX, 0f, DreamRoad.LogAt.z - DreamRoad.LogHalf - 0.6f), false);
            Ground("бревно", DreamRoad.LogAt + Vector3.up * 0.25f, true);
            var beyond = new Vector3(-45f, 0f, 29f);
            var plaza = new Vector3(-12f, 0f, -24f);
            Leg("опушка → умирающий", DreamRoad.StarAt, DreamRoad.DyingAt, null, null);
            Leg("умирающий → стоянка", DreamRoad.DyingAt, DreamRoad.DummyFront(1.7f), null, null);
            Leg("стоянка → за реку", DreamRoad.CampAt, beyond, DreamRoad.BridgeAt, "мост");
            Leg("за рекой → площадь", beyond, plaza, DreamRoad.LogAt, "бревно");
            Leg("площадь → камень с банками", plaza, DreamRoad.ShrineAt + new Vector3(0f, 0f, -1.2f), null, null);
            Leg("площадь → ворота склепа", plaza, DreamRoad.GateFront, null, null);
            Leg("лес → к бревну", new Vector3(-124f, 0f, -8f), new Vector3(-31f, 0f, -33f), new Vector3(-67f, 0f, -25f), "лесную тропу");
            Leg("вся дорога: опушка → ворота", DreamRoad.StarAt, DreamRoad.GateFront, DreamRoad.LogAt, "бревно");
        }

        private static float BridgeX => DreamRoad.BridgeAt.x;

        private static bool SegmentNear(Vector3 a, Vector3 b, Vector3 p, float r)
        {
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            var ap = new Vector2(p.x - a.x, p.z - a.z);
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            return (ap - ab * t).magnitude <= r;
        }

        /// <summary>Карта сверху — без тумана и с ровным светом: проверка разметки, не кадр игры.</summary>
        private static void MapShot(string name, Vector3 center, float height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var cam = Camera.main;
            if (cam == null) return;
            bool fog = RenderSettings.fog;
            var mode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            float far = cam.farClipPlane, fov = cam.fieldOfView;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);
            cam.farClipPlane = Mathf.Max(far, height + 60f);
            cam.fieldOfView = 50f;
            ShotFrom(name, center + Vector3.up * height, center + new Vector3(0f, 0f, 4f));
            cam.farClipPlane = far;
            cam.fieldOfView = fov;
            RenderSettings.fog = fog;
            RenderSettings.ambientMode = mode;
            RenderSettings.ambientLight = ambient;
        }

        private static void ShotFrom(string name, Vector3 from, Vector3 look, bool clear = true)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var rts = cam.GetComponent<RTS_Camera>();
            bool was = rts != null && rts.enabled;
            if (rts != null) rts.enabled = false;
            var pos = cam.transform.position;
            var rot = cam.transform.rotation;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from));
            if (clear) FogOfWar.ShotClear(true);
            Snap(name);
            if (clear) FogOfWar.ShotClear(false);
            cam.transform.SetPositionAndRotation(pos, rot);
            if (rts != null) rts.enabled = was;
        }

        /// <summary>Кадр на каждую реплику суда — когда она допечатана и камера доехала.</summary>
        private static void CourtShots(float now)
        {
            if (!SoulCourt.Sitting || SoulCourt.Spoken == 0) return;
            // Влёт в голову (автор 06.11: в голове была прозрачность) — два кадра посреди влёта.
            if (SoulCourt.Step == "dive")
            {
                if (_diveAt < 0f) _diveAt = now;
                if (_diveShots == 0 && now - _diveAt > 0.3f) { _diveShots = 1; Snap("суд 00а — влёт в голову"); }
                else if (_diveShots == 1 && now - _diveAt > 0.6f) { _diveShots = 2; Snap("суд 00б — влёт в голову"); }
            }
            // Зал снизу (автор 06.11: «не хватает грандиозности») — проявился, ещё не поднялись.
            if (SoulCourt.Step == "rise")
            {
                if (_riseAt < 0f) _riseAt = now;
                if (!_riseShot && now - _riseAt > 2.3f) { _riseShot = true; Snap("суд 01б — зал снизу"); }
            }
            // «Вы — судья»: на ходу к месту приказа и уже на нём — видна ли фигура во тьме, в кадре ли место.
            if (SoulCourt.Step == "walk")
            {
                if (_walkAt < 0f) _walkAt = now;
                if (!_walkShot && now - _walkAt > 2.8f) { _walkShot = true; Snap("суд 01в — идёте к месту приказа"); }
                if (!_seatShot && !string.IsNullOrEmpty(SoulCourt.TookSeat)) { _seatShot = true; Snap("суд 01г — на месте приказа"); }
            }
            if (SoulCourt.Spoken != _spokenSeen) { _spokenSeen = SoulCourt.Spoken; _spokenAt = now; }
            var box = UI.Letterbox.Instance;
            if (_spokenShot == _spokenSeen || box == null || !box.Typed || now - _spokenAt < 1.3f) return;
            _spokenShot = _spokenSeen;
            Snap($"суд {_spokenSeen:00} — {SoulCourt.Step}");
        }

        private static void Warp(Component hero, Vector3 to)
        {
            if (hero == null) return;
            var legs = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (legs != null && legs.enabled && legs.isOnNavMesh) legs.Warp(to);
            else hero.transform.position = to;
        }

        private static void End(bool ok, string why)
        {
            _done = true;
            Write((ok ? "=== СОН ПРОЙДЕН: " : "=== СОН НЕ ПРОЙДЕН: ") + why);
            Write($"итог: миг при убийцах — {DreamTutorialDirector.RefusalResult ?? "не было"}; суд — {SoulCourt.Outcome ?? "не было"}; "
                + $"без убийц — {DreamTutorialDirector.RetestResult ?? "не проверялся"}; шёпот — {DreamTutorialDirector.WhisperResult ?? "не шептали"}; "
                + $"место приказа в суде — {(string.IsNullOrEmpty(SoulCourt.TookSeat) ? "не ходили" : SoulCourt.TookSeat)}; "
                + $"ошибок в журнале {_errors}");
            Write("хронометраж сна (FIRST-FIVE-MINUTES, T0–T8):");
            foreach (var mark in DreamTutorialDirector.Timeline) Write("  " + mark);
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Catch;
            UI.Letterbox.Said -= Line;
            EditorApplication.isPlaying = false;
            int code = ok && _errors == 0 ? 0 : 1;
            // Под верстаком Unity не закрывается: отчёт — ждущей команде, дальше новые команды.
            if (Workbench.Hosting) { Workbench.DreamEnded(); return; }
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
#endif
