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
        private static bool _killersShot, _retestShot, _scalesShot, _foreseeShot, _retestIssued, _harvestTold, _done;
        private static bool _risenShot;
        private static string NL => Environment.NewLine;

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
            if (!File.Exists(ScenePath)) { EditorApplication.Exit(1); return; }

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
        }

        private static void Snap(string name)
        {
            try { Utilets.Snapshot.Now(Shots, name); Write("  [КАДР] " + name); }
            catch (Exception e) { Write("  [СНИМОК НЕ ВЫШЕЛ] " + name + ": " + e.Message); }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || _done) return;
            float now = Time.realtimeSinceStartup;
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

            CourtShots(now);
            FlightShot(dream, now);
            AnimateAll(now);

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
                        // С начала дороги — как видит игрок: впереди туман войны, разведанное серым.
                        ShotFrom("сон — дорога к склепу", new Vector3(0f, 9f, -36f), new Vector3(0f, 1.5f, 18f), clear: false);
                        ShotFrom("сон — ворота форта", new Vector3(3f, 2.6f, 3.5f), new Vector3(0f, 3.2f, 15f));
                        ShotFrom("сон — двор форта, чучело", new Vector3(0.5f, 2.3f, 13.6f), new Vector3(4.2f, 1.0f, 17.5f));
                        ShotFrom("сон — двор форта, камень с банками", new Vector3(-0.5f, 2.2f, 16.5f), new Vector3(-4.6f, 0.9f, 20.5f));
                        Warp(dream.Hero, dream.WoundedAt + new Vector3(0f, 0f, -3.2f));
                    }
                    break;
                case "Harvest":
                    if (_step != "жатва") { Go("жатва"); Warp(dream.Hero, dream.WoundedAt + new Vector3(0f, 0f, -1.6f)); }
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
                    if (_step != "поднятие") { Go("поднятие"); Warp(dream.Hero, dream.WoundedAt + new Vector3(0f, 0f, -2f)); }
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
                        // Отступить — назад по дороге, от убийц.
                        dream.Zombie.IssueCommand(CommandKind.FallBack, dream.Zombie.transform.position + new Vector3(0f, 0f, -7f));
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
                        dream.Zombie.IssueCommand(CommandKind.FallBack, dream.Zombie.transform.position + new Vector3(0f, 0f, -6f));
                    }
                    var box = UI.Letterbox.Instance;
                    if (!_retestShot && box != null && box.Shown && box.Typed && now - _stepAt > 1.5f)
                    { _retestShot = true; Snap("сон — тот же приказ без убийц"); }
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
                        Warp(dream.Hero, dream.Gate + new Vector3(0f, 0f, -1f));
                    }
                    if (poke && DreamRoad.CryptFade < 0f) dream.TestPress(KeyCode.F);
                    if (!_gateShot && DreamRoad.CryptFade > 0.15f) { _gateShot = true; Snap("сон — створки расходятся"); }
                    if (!_headShot && DreamRoad.CryptFade > 0.3f) { _headShot = true; Snap("сон — силуэт поднимает голову"); }
                    if (!_deathShot && DreamRoad.CryptFade > 0.45f) { _deathShot = true; Snap("сон — силуэт в склепе"); }
                    if (!_blackShot && DreamRoad.CryptFade >= 1f) { _blackShot = true; Snap("сон — экран погас"); }
                    break;
            }
        }

        private static bool _gateShot, _deathShot, _blackShot, _headShot;
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
                    Warp(dream.Hero, DreamRoad.DummyAt + new Vector3(-1.7f, 0f, -0.2f));
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
        private static void ShotFrom(string name, Vector3 from, Vector3 look, bool clear = true)
        {
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
            if (SoulCourt.Spoken != _spokenSeen) { _spokenSeen = SoulCourt.Spoken; _spokenAt = now; }
            var box = UI.Letterbox.Instance;
            if (_spokenShot == _spokenSeen || box == null || !box.Typed || now - _spokenAt < 1.3f) return;
            _spokenShot = _spokenSeen;
            Snap($"суд {_spokenSeen:00} — {SoulCourt.Step}");
        }

        private static void Warp(SinbinderPlayer hero, Vector3 to)
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
                + $"без убийц — {DreamTutorialDirector.RetestResult ?? "не проверялся"}; ошибок в журнале {_errors}");
            Write("хронометраж сна (FIRST-FIVE-MINUTES, T0–T8):");
            foreach (var mark in DreamTutorialDirector.Timeline) Write("  " + mark);
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Catch;
            UI.Letterbox.Said -= Line;
            EditorApplication.isPlaying = false;
            int code = ok && _errors == 0 ? 0 : 1;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
#endif
