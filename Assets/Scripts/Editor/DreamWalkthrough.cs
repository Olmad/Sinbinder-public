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
                        ShotFrom("сон — дорога к склепу", new Vector3(0f, 9f, -36f), new Vector3(0f, 1.5f, 18f));
                        ShotFrom("сон — ворота форта", new Vector3(3f, 2.6f, 3.5f), new Vector3(0f, 3.2f, 15f));
                        ShotFrom("сон — двор форта, чучело и камень с банками", new Vector3(0.4f, 3.4f, 14.6f), new Vector3(0f, 1.0f, 21f));
                        Warp(dream.Hero, dream.WoundedAt + new Vector3(0f, 0f, -3.2f));
                    }
                    break;
                case "Harvest":
                    if (_step != "жатва") { Go("жатва"); Warp(dream.Hero, dream.WoundedAt + new Vector3(0f, 0f, -1.6f)); }
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
                case "Temptation":
                    if (_step != "искушение")
                    {
                        Go("искушение");
                        if (dream.Zombie != null)
                            Warp(dream.Hero, dream.Zombie.transform.position + new Vector3(0f, 0f, -2f));
                    }
                    if (poke && now - _stepAt > 0.8f)
                        Whisper.TryAny();  // настоящий шёпот через AOS, либо тайм-аут сцены
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
                case "CommanderPractice":
                    if (_step != "управление")
                    {
                        Go("управление");
                        Write($"  тот же приказ без убийц: {DreamTutorialDirector.RetestResult ?? "не проверялся"}");
                    }
                    dream.TestSkipControls(); // только кнопочный блок; новые загадки проверяются отдельно
                    break;
                case "BowFind":
                    if (_step != "лук — возвышение")
                    {
                        Go("лук — возвышение");
                        Warp(dream.Hero, DreamRoad.BowStandAt + new Vector3(0f, 0f, -1.5f));
                        Snap("сон — лук-искуситель на возвышении");
                    }
                    if (poke) dream.TestPress(KeyCode.F);
                    break;
                case "BowEquip":
                    if (_step != "лук — снаряжение")
                    {
                        Go("лук — снаряжение");
                        var bag = Sinbinder.Inventory.PlayerInventory.Instance;
                        var ally = dream.SealCompanion;
                        var item = bag?.GetAllItems().Find(i => i != null && i.Name == DreamBowTarget.BowName);
                        if (ally == null || item == null || !SquadGear.Hand(ally, item, bag, out string reason))
                        {
                            End(false, "не удалось экипировать учебный лук реальной системой SquadGear");
                            return;
                        }
                        Write("  передача лука через SquadGear.Hand: есть; голос искушения в вещи " +
                              ally.Worn(Sinbinder.Inventory.GearSlot.Weapon)?.TemptationSin);
                        Snap("сон — лук в снаряжении союзника");
                    }
                    break;
                case "BowShot":
                    if (_step != "лук — выстрел")
                    {
                        Go("лук — выстрел");
                        // Инвариант: до настоящего попадания механизм ещё заперт.
                        if (DreamRoad.Instance == null || DreamRoad.Instance.MechanismReleased)
                        {
                            End(false, "противовес отпёр засовы до попадания стрелы");
                            return;
                        }
                        Write("  [МЕХАНИЗМ] до выстрела засовы закрыты");
                        var target = DreamRoad.Instance != null ? DreamRoad.Instance.BowTarget : null;
                        if (dream.SealCompanion == null || target == null)
                        {
                            End(false, "отсутствует соратник или подвесная мишень");
                            return;
                        }
                        // В первую очередь: подлинный приказ и голосование. Отказ не является
                        // багом, поэтому через 12 секунд принудительно проверим только
                        // механику полёта стрелы, явно записав обход AOS в отчёт.
                        dream.SealCompanion.IssueCommand(CommandKind.Attack, target.transform.position, target.gameObject);
                        Snap("сон — высокая мишень, настоящий приказ");
                    }
                    if (now - _stepAt > 12f && DreamRoad.Instance != null && !DreamRoad.Instance.BowTargetBroken)
                    {
                        var target = DreamRoad.Instance.BowTarget?.GetComponent<DreamBowTarget>();
                        if (target != null && dream.SealCompanion != null)
                        {
                            Write("  [QA ОБХОД] За 12 с AOS не исполнила приказ, выстрел проверяется прямым вызовом.");
                            target.ExecuteShot(dream.SealCompanion);
                        }
                    }
                    break;
                case "BowTemper":
                    if (_step != "лук — искушение в деле")
                    {
                        Go("лук — искушение в деле");
                        // Падение мишени без разблокировки плит больше не считается успехом.
                        if (DreamRoad.Instance == null || !DreamRoad.Instance.MechanismReleased)
                        {
                            End(false, "стрела сбила противовес, но засовы печатей не открылись");
                            return;
                        }
                        Write("  [МЕХАНИЗМ] противовес освободил две напольные печати");
                        var archer = dream.SealCompanion;
                        if (archer != null)
                        {
                            archer.IssueCommand(CommandKind.FallBack,
                                archer.transform.position + new Vector3(0f, 0f, -5f));
                            Write("  попытка отступления с луком: решение принимает AOS");
                        }
                        Snap("сон — испытание гордыни с луком");
                    }
                    break;
                case "Rescue":
                    if (_step != "Последний щит")
                    {
                        Go("Последний щит");
                        Write("  [AOS] наблюдаем настоящие решения защитника и реальный перехват входящего урона");
                        Snap("сон — последний щит, AOS спасение");
                    }
                    // Не вызываем GuardForRescue и OnAllySaved из прогона:
                    // иначе нарисуем победу там, где души на деле не спасали.
                    break;
                case "CompareSouls":
                    if (_step != "две души — один приказ")
                    {
                        Go("две души — один приказ");
                        var mercy = dream.ContrastMerciful;
                        var pride = dream.ContrastProud;
                        if (mercy == null || pride == null)
                        {
                            End(false, "учебные души AOS не созданы");
                            return;
                        }
                        // Эмуляция только клика игрока: оба ПРИКАЗА настоящие.
                        // AOS обязана проголосовать самостоятельно в своём обычном такте.
                        mercy.IssueCommand(CommandKind.Move, DreamTutorialDirector.ContrastPoint);
                        pride.IssueCommand(CommandKind.Move, DreamTutorialDirector.ContrastPoint);
                        Write("  [QA ВВОД] отправлен одинаковый Move обеим душам; голосование не подменяется");
                        Snap("сон — два характера");
                    }
                    break;
                case "EmotionShift":
                    if (_step != "одна душа — разный страх")
                    {
                        Go("одна душа — разный страх");
                        if (!dream.CompareSoulsCompleted)
                        {
                            End(false, "урок двух душ пропущен или оба голосования не записаны");
                            return;
                        }
                        var mercy = dream.ContrastMerciful;
                        if (mercy == null)
                        {
                            End(false, "нет стража для повторной проверки эмоции");
                            return;
                        }
                        mercy.IssueCommand(CommandKind.Move, DreamTutorialDirector.ContrastPoint);
                        Write("  [QA ВВОД] повторён Move после настоящего EmotionSystem.TriggerEmotion");
                        Snap("сон — изменение страха");
                    }
                    break;
                case "Puzzle":
                    if (!dream.CompareSoulsCompleted || !dream.EmotionShiftCompleted)
                    {
                        End(false, "не показаны два характера или влияние страха на голосование");
                        return;
                    }
                    if (dream.RescueResult != "спасён")
                    {
                        End(false, "сцену спасения прошли без настоящего защитного удара: " + dream.RescueResult);
                        return;
                    }
                    if (_step != "две печати")
                    {
                        Go("две печати");
                        if (DreamRoad.Instance == null || !DreamRoad.Instance.MechanismReleased)
                        {
                            End(false, "достигли финальной загадки, не разблокировав плиты выстрелом");
                            return;
                        }
                        Snap("сон — две печати склепа");
                    }
                    var companion = dream.SealCompanion;
                    if (companion == null || companion.IsDead)
                    {
                        End(false, "соратник не дожил до двух печатей");
                        return;
                    }
                    companion.ClearCommand();
                    // Regression: «стою на печати» — расстояние, не
                    // нормализованное направление. Испытываем 120 тактов
                    // неверной комбинации, затем уже ставим обоих правильно.
                    var road = DreamRoad.Instance;
                    if (road == null) { End(false, "дорога пропала"); return; }
                    var far = new Vector3(-5f, 0f, 17f);
                    if (UnityEngine.Vector3.Distance(companion.transform.position, DreamRoad.WarriorSealAt) <= 1.35f)
                        companion.transform.position = far;
                    for (int i = 0; i < 120; i++)
                        if (road.CheckSeals(companion, DreamRoad.LordSealAt))
                        {
                            End(false, "плиты сработали с союзником вне своей печати");
                            return;
                        }
                    Write("  [ПЛИТЫ] 120 проверок вне круга не открывают ворота");
                    var agent = companion.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null && agent.enabled && agent.isOnNavMesh)
                        agent.Warp(DreamRoad.WarriorSealAt);
                    else companion.transform.position = DreamRoad.WarriorSealAt;
                    Warp(dream.Hero, DreamRoad.LordSealAt);
                    break;
                case "Return":
                    if (_step != "пробуждение")
                    {
                        Go("пробуждение");
                        ShotFrom("сон — ворота склепа", new Vector3(2.5f, 2.2f, 21.5f), new Vector3(0f, 3.4f, 30f));
                        Warp(dream.Hero, dream.Gate + new Vector3(0f, 0f, -1f));
                    }
                    if (poke && DreamRoad.CryptFade < 0f) dream.TestPress(KeyCode.F);
                    if (!_gateShot && DreamRoad.CryptFade > 0.15f) { _gateShot = true; Snap("сон — створки расходятся"); }
                    if (!_deathShot && DreamRoad.CryptFade > 0.45f) { _deathShot = true; Snap("сон — силуэт в склепе"); }
                    if (!_blackShot && DreamRoad.CryptFade >= 1f) { _blackShot = true; Snap("сон — экран погас"); }
                    break;
            }
        }

        private static bool _gateShot, _deathShot, _blackShot;

        /// <summary>Кадр с отдельной точки: камера сна на миг встаёт туда и возвращается.</summary>
        private static void ShotFrom(string name, Vector3 from, Vector3 look)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rts = cam.GetComponent<RTS_Camera>();
            bool was = rts != null && rts.enabled;
            if (rts != null) rts.enabled = false;
            var pos = cam.transform.position;
            var rot = cam.transform.rotation;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from));
            Snap(name);
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
