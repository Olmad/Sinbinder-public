// Assets/Scripts/Editor/DemoWalkthrough.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Прохождение демо целиком, без человека: от палатки до склепа.
    ///
    /// <see cref="DemoSmoke"/> ловит то, что падает само, но ничего не
    /// нажимает — а пролог с 13 сентября ведётся действиями игрока
    /// (14-HANDOFF §25), и сорок секунд простоя его не проверяют вовсе:
    /// лагерь честно ждёт, пока Греховод выйдет из палатки.
    ///
    /// Здесь игрок действует. Каждый шаг — то, что сделал бы человек,
    /// и условие, по которому видно, что игра ответила. Шаг, не
    /// дождавшийся ответа, пишется как провал с тем, чего ждали, и
    /// прохождение идёт дальше: одна застрявшая доля не должна прятать
    /// поломки в следующих.
    ///
    /// <b>Где автопилот срезает.</b> Героя он переносит, а не ведёт
    /// (<c>NavMeshAgent.Warp</c>), клавиши заменяет вызовами, а кнопки
    /// совета нажимает их же обработчиками. Это проверка того, что
    /// пролог отвечает на действия, а не того, удобно ли до них дойти:
    /// путь ногами проверяет только человек.
    ///
    /// Запуск: <c>-executeMethod Sinbinder.EditorTools.DemoWalkthrough.Run</c>
    /// без <c>-quit</c>. Итог — <c>Logs/demo-walkthrough.txt</c>.
    ///
    /// <b>Со всеми выключателями</b> — <c>RunAll</c> (24 сентября): те же
    /// шаги, но после входа в Play включено всё, что ждёт прогона (голос,
    /// причина, удар, добыча, лагерь, склад). Итог — отдельным файлом,
    /// <c>Logs/demo-walkthrough-all.txt</c>, чтобы сравнить с обычным.
    /// Провал здесь — не обязательно поломка: воин, не расслышавший приказ
    /// издали, так и задуман. Читать, а не верить приговору.
    /// </summary>
    [InitializeOnLoad]
    public static class DemoWalkthrough
    {
        private const string Active = "Sinbinder.DemoWalkthrough.Active";
        private const string AllOn = "Sinbinder.DemoWalkthrough.AllOn";

        private static string Report => SessionState.GetBool(AllOn, false)
            ? "Logs/demo-walkthrough-all.txt" : "Logs/demo-walkthrough.txt";

        private static readonly string NL = Environment.NewLine;

        private sealed class Step
        {
            public string Name;
            public Action Do;
            public Func<bool> Done;
            public float Limit;
        }

        private static List<Step> _steps;
        private static int _index;
        private static bool _entered;
        private static float _startedAt;
        private static int _failed;

        /// <summary>Ошибок и исключений за прогон. Роняют приговор.</summary>
        private static int _errors;

        /// <summary>
        /// Сработавших страховок: шаг прошёл, но не потому, что игра
        /// ответила, а потому, что истёк срок (<see cref="Gameplay.Beat"/>).
        /// Это третье состояние — не провал и не чистота.
        /// </summary>
        private static int _late;

        static DemoWalkthrough()
        {
            if (!SessionState.GetBool(Active, false)) return;

            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;
        }

        public static void Run() => Begin(false);

        /// <summary>То же прохождение со всеми выключателями дня.</summary>
        public static void RunAll() => Begin(true);

        private static void Begin(bool all)
        {
            SessionState.SetBool(AllOn, all);

            // Проверки идут молча. Прогон и дымовая проверка входят
            // в Play и включают весь звук игры — рог отхода, голоса,
            // бой, — а идут они по десять минут и в любое время суток.
            // Автор 20 сентября попросил не шуметь: он в это время
            // занят другим, и рог из-за спины пугает.
            AudioListener.volume = 0f;
            File.WriteAllText(Report, "");
            SessionState.SetBool(Active, true);

            EditorSceneManager.OpenScene("Assets/Scenes/Prologue_Camp.unity");
            EditorApplication.isPlaying = true;

            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;
        }

        private static void Write(string line)
            => File.AppendAllText(Report, "[" + Time.realtimeSinceStartup.ToString("F0") + "] " + line + NL);

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (!EditorApplication.isPlaying) return;

            if (_steps == null)
            {
                _steps = Build();
                _index = 0;
                _entered = false;
                _failed = 0;
                _errors = 0;
                _late = 0;

                // Выключатели сбрасываются при входе в Play
                // (SubsystemRegistration), поэтому включаются здесь,
                // уже в игре, а не в Run.
                if (SessionState.GetBool(AllOn, false))
                {
                    Gameplay.Voice.Enabled = true;
                    AOS.Counterfactual.Enabled = true;
                    Gameplay.CombatMath.Enabled = true;
                    Gameplay.LootChain.Enabled = true;
                    Gameplay.CampLife.Enabled = true;
                    Gameplay.TrophyChest.Store = true;
                    Write("=== ПРОХОЖДЕНИЕ СО ВСЕМИ ВЫКЛЮЧАТЕЛЯМИ: голос, причина, удар, добыча, лагерь, склад ===");
                }
                else Write("=== ПРОХОЖДЕНИЕ ===");
            }

            if (_index >= _steps.Count)
            {
                Finish();
                return;
            }

            var step = _steps[_index];

            if (!_entered)
            {
                _entered = true;
                _startedAt = Time.realtimeSinceStartup;

                try { step.Do?.Invoke(); }
                catch (Exception e) { Write("  [ОШИБКА ШАГА] " + step.Name + ": " + e.Message); }
            }

            bool done;
            try { done = step.Done == null || step.Done(); }
            catch (Exception e) { Write("  [ОШИБКА ПРОВЕРКИ] " + step.Name + ": " + e.Message); done = false; }

            float spent = Time.realtimeSinceStartup - _startedAt;

            if (done)
            {
                Write("  [ГОТОВО] " + step.Name + " — за " + spent.ToString("F0") + " с");
                Shot(step.Name);
                Next();
                return;
            }

            if (spent >= step.Limit)
            {
                _failed++;
                Write("  [ЗАСТРЯЛО] " + step.Name + " — не дождались за " + step.Limit.ToString("F0") + " с");
                Shot("ЗАСТРЯЛО " + step.Name);
                Dump();
                Next();
            }
        }

        private static void Next()
        {
            _index++;
            _entered = false;
        }

        private static void Finish()
        {
            // Приговор обязан считать то, что отчёт записал. До 17 сентября
            // _failed рос только на истёкшем шаге, а ошибки и сработавшие
            // страховки уходили в отчёт и на приговор не влияли: прогон,
            // в котором каждый кадр летит NullReferenceException, печатал
            // «ПРОЙДЕНО ЦЕЛИКОМ» и выходил с нулём. Для инструмента,
            // который проходит демо **без человека**, приговор — всё,
            // что видно (14-HANDOFF.md §26).
            //
            // Состояний три, а не два. Страховка — не провал: шаг прошёл,
            // игра не развалилась. Но и не чистота: игрок на этом месте
            // не сделал того, ради чего шаг существует, и назвать это
            // «пройдено целиком» — соврать.
            if (_failed == 0 && _errors == 0 && _late == 0)
                Write("=== ПРОЙДЕНО ЦЕЛИКОМ ===");
            else if (_failed == 0 && _errors == 0)
                Write("=== ПРОШЛО НА СТРАХОВКАХ: сработало — " + _late
                    + ". Демо не разваливается, но эти шаги игра "
                    + "не отработала. Искать [ПРОЛОГ] выше ===");
            else
                Write("=== КОНЕЦ: застряло шагов — " + _failed
                    + ", ошибок — " + _errors
                    + ", страховок — " + _late + " ===");

            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(_failed == 0 && _errors == 0 ? 0 : 1);
        }

        // ─────────────────────────────── шаги ───────────────────────────────

        private static List<Step> Build()
        {
            return new List<Step>
            {
                // ── Лагерь ──
                // Вопрос о сохранении — первое, что видит игрок, и первое,
                // на что отвечает прогон. Нажимаем каждый кадр, пока он
                // висит: сцена грузится не мгновенно, и одного нажатия
                // в начале шага могло бы не хватить.
                S("ответили на вопрос о сохранении", null,
                  () => { Sinbinder.UI.StartPanel.ChooseFresh();
                          return !Sinbinder.UI.StartPanel.Waiting; }, 30f),

                S("лагерь загрузился, заставка ушла", null,
                  () => Scene("Prologue_Camp") && SinbinderPlayer.Exists
                     && !Sinbinder.UI.PrologueTitleUI.Showing && !Paused(), 30f),

                // Проверяем то, что должно было случиться, а не то, что
                // мы попросили: Done = () => true означал шаг, который
                // не может провалиться, — а таких в списке проверок
                // быть не должно вовсе.
                S("Греховод вышел из палатки", () => StepHero(4.5f),
                  () => SinbinderPlayer.Exists && !Paused(), 4f),

                // ── Мышь ──
                // До 24 сентября прогон не касался мыши вовсе, и двенадцать
                // дней без коллайдеров у воинов (13-DRIFT, восьмая) прошли
                // сквозь все его зелёные отчёты. Нажатий ввод Unity в прогоне
                // не принимает, поэтому проверяется путь, по которому идёт
                // щелчок: луч из камеры попадает в воина, и выделение тем же
                // методом, что зовёт щелчок, даёт круг у ног.
                S("мышь: луч от камеры попадает в воина", null,
                  () => RayFinds(w => w.Team == Team.Player && !(w is SinbinderPlayer)), 10f),

                S("мышь: выделенный получает круг", SelectOneOwn,
                  () => SelectedWithRing(), 5f),

                S("провожатый дошёл, лагерь пошёл дальше", null,
                  () => CampOpening.EscortArrived, 40f),

                S("Карган позвал к шару", null,
                  () => Summoned(), 15f),

                S("Греховод у стола", () => HeroTo(Ball()?.transform, 1.5f),
                  () => Ball() != null && Ball().PlayerIsClose(), 5f),

                S("совет открыт", () => Council()?.Open(),
                  () => CouncilPanelOpen(), 5f),

                S("старший назначен", ChooseCommander,
                  () => !string.IsNullOrEmpty(SquadRoster.CommanderName), 5f),

                S("Греховод у сундука", () => HeroTo(Chest()?.transform, 1.2f),
                  () => TrophyChest.Looted, 15f),

                // Сундук-склад (выключатель «склад»): экран сундука обязан
                // открыться сам и держит паузу — автопилот его закрывает.
                // Без склада шаг проходит сразу.
                S("склад: экран сундука открылся", null, () =>
                {
                    if (!TrophyChest.Store) return true;
                    if (!UI.GearPanel.Open) return false;
                    UI.GearPanel.Dismiss();
                    return true;
                }, 10f),

                // ── Вещи и прогноз (24 сентября): экраны, которые без рук
                // автора не открывал никто. Автопилот зовёт их напрямую,
                // клавиш и мыши у него нет. ──
                S("вещи: I у выделенного — экран открылся", () =>
                {
                    SelectOneOwn();
                    UI.GearPanel.Toggle();
                }, () =>
                {
                    if (!UI.GearPanel.Open) return false;
                    UI.GearPanel.Dismiss();
                    SelectionManager.Instance?.Drop(_clicked);
                    return true;
                }, 5f),

                S("вещи: разговор вблизи и обмен", TalkAndHand, () =>
                {
                    if (!UI.GearPanel.Open) return false;
                    UI.GearPanel.Dismiss();
                    return true;
                }, 5f),

                S("прогноз на панели приказов", ForecastSquad, () => _forecastOk, 5f),

                S("тревога после сундука", null,
                  () => Ball() != null && Ball().IsAlarmed, 30f),

                // С 24 сентября разгром — событие лагеря, а не новая сцена
                // (RaidEvent); отдельной сцены набега нет вовсе.
                S("Греховод у горящего шара", () => HeroTo(Ball()?.transform, 1.5f),
                  () => RaidEvent.Running, 60f),

                // ── Набег ──
                S("набег: охотники вышли", null,
                  () => Enemies() > 0, 30f),

                // Приказ «бить» — луч в охотника. Только в видимого: того,
                // кто в тумане, щелчком не достать, и так задумано (§67).
                S("мышь: луч попадает в видимого охотника", null,
                  () => RayFinds(w => w.Team == Team.Enemy && !FogOfWar.Hides(w)), 60f),

                S("набег: первая волна положена", AttackAll,
                  () => Enemies() == 0 && FirstWaveOver(), 150f),

                // SoulManager.Instance == null раньше засчитывался за успех:
                // нет жнеца в сцене — значит души собраны. Это «ноль вместо
                // события», против которого весь проект. Нет его — шаг
                // обязан застрять, и в отчёте будет видно почему.
                S("набег: души собраны", HarvestAll,
                  () => SoulManager.Instance != null
                        && (SoulManager.Instance.FadingCount == 0
                            || Core.Satchel.FreeJar() < 0), 90f),

                S("набег: подкрепление вышло и край открыт", null,
                  () => EscapeZone.Active != null && EscapeZone.Active.Open, 20f),

                S("набег: отряд у края", SquadToEscape,
                  () => Scene("Crypt_Entrance"), 45f),

                // ── Склеп ──
                S("склеп: спросили о плате", null,
                  () => SalaryOpen(), 20f),

                S("склеп: заплатили", PaySalary,
                  () => UI.SalaryPanelUI.Answered, 5f),

                // С 24 сентября эпилог ждёт шага: Греховод входит в зал.
                // Раньше он приходил по часам, и прогон просто ждал.
                S("склеп: Греховод у алтаря", () => HeroTo(Altar(), 1.5f),
                  () => SinbinderPlayer.Exists && Altar() != null
                     && CampFocus.GroundDistance(SinbinderPlayer.Where,
                                                 Altar().position) <= 3.5f, 5f),

                S("склеп: конец демо показан", null,
                  () => DemoEndShown(), 60f),
            };
        }

        private static Step S(string name, Action action, Func<bool> done, float limit)
            => new Step { Name = name, Do = action, Done = done, Limit = limit };

        // ─────────────────────────────── действия ───────────────────────────────

        private static void StepHero(float metres)
        {
            var hero = SinbinderPlayer.Instance;
            if (hero == null) return;

            var target = hero.transform.position + hero.transform.forward * metres;
            Warp(hero.gameObject, target);
        }

        private static void HeroTo(Transform what, float gap)
        {
            var hero = SinbinderPlayer.Instance;
            if (hero == null || what == null) return;

            var from = hero.transform.position;
            var to = what.position;
            var dir = from - to;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;

            Warp(hero.gameObject, to + dir.normalized * gap);
        }

        private static void Warp(GameObject go, Vector3 to)
        {
            var agent = go.GetComponent<NavMeshAgent>();

            if (agent != null && agent.isOnNavMesh
                && NavMesh.SamplePosition(to, out var hit, 4f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                return;
            }

            go.transform.position = to;
        }

        private static void ChooseCommander()
        {
            var council = Council();
            if (council == null) return;

            var rows = Field<RectTransform>(council, "_rows");
            var confirm = Field<Button>(council, "_confirm");

            if (rows != null)
                foreach (var button in rows.GetComponentsInChildren<Button>(true))
                {
                    button.onClick.Invoke();
                    break;
                }

            confirm?.onClick.Invoke();
        }

        /// <summary>
        /// Приказ «в атаку» всему отряду — ровно то, что сделал бы игрок
        /// рамкой и правой кнопкой. Приказ голосуется, как и у человека:
        /// кто-то откажется, и это не провал прохождения, а игра.
        /// Повторяется, пока на поле есть враги: исполненный приказ гаснет,
        /// а новый охотник мог выйти.
        /// </summary>
        private static void AttackAll()
        {
            _sawFirstWave = true;
            _attackNext = 0f;
            EditorApplication.update -= Charge;
            EditorApplication.update += Charge;
        }

        private static float _attackNext;

        private static void Charge()
        {
            if (!EditorApplication.isPlaying || Enemies() == 0) { EditorApplication.update -= Charge; return; }
            if (Time.realtimeSinceStartup < _attackNext) return;
            _attackNext = Time.realtimeSinceStartup + 4f;

            var enemies = CombatManager.Instance.GetAliveEnemies();
            if (enemies == null || enemies.Count == 0) return;

            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer) continue;
                if (w.HasCommand) continue;

                Damageable nearest = null;
                float best = float.MaxValue;
                foreach (var e in enemies)
                {
                    if (e == null || e.IsDead) continue;
                    float d = (e.transform.position - w.transform.position).sqrMagnitude;
                    if (d < best) { best = d; nearest = e; }
                }

                if (nearest != null)
                    w.IssueCommand(CommandKind.Attack, nearest.transform.position, nearest.gameObject);
            }
        }

        private static bool _sawFirstWave;
        private static float _harvestNext;

        private static bool FirstWaveOver() => _sawFirstWave;

        private static void HarvestAll()
        {
            _harvestNext = 0f;
            EditorApplication.update -= Reap;
            EditorApplication.update += Reap;
        }

        /// <summary>
        /// Собирать души, пока они есть: перенестись к ближайшей и нажать
        /// жатву тем же методом, что вызывает клавиша.
        /// </summary>
        private static void Reap()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Reap; return; }
            if (Time.realtimeSinceStartup < _harvestNext) return;
            _harvestNext = Time.realtimeSinceStartup + 0.6f;

            var souls = SoulManager.Instance;
            var hero = SinbinderPlayer.Instance;
            if (souls == null || hero == null || souls.FadingCount == 0)
            {
                EditorApplication.update -= Reap;
                return;
            }

            var all = souls.GetAllFadingSouls();
            if (all == null || all.Count == 0) return;

            Warp(hero.gameObject, all[0].Position + Vector3.back * 0.5f);

            var harvester = hero.GetComponent<SoulHarvester>();
            if (harvester == null) return;

            // Банки кончились — сбор окончен, как у игрока: нести не во что.
            if (Core.Satchel.FreeJar() < 0) { EditorApplication.update -= Reap; return; }

            var m = typeof(SoulHarvester).GetMethod("TryHarvest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            m?.Invoke(harvester, null);
        }

        private static void SquadToEscape()
        {
            var zone = EscapeZone.Active;
            if (zone == null) return;

            int i = 0;
            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;

                var offset = new Vector3(Mathf.Cos(i) * 1.5f, 0f, Mathf.Sin(i) * 1.5f);
                Warp(w.gameObject, zone.transform.position + offset);
                i++;
            }
        }

        /// <summary>
        /// Что было на поле в миг, когда шаг не дождался. Без снимка
        /// «застряло» говорит только где, а не почему: первый прогон
        /// так и оставил загадкой бой, в котором за полторы минуты
        /// не погиб никто.
        /// </summary>
        /// <summary>
        /// Снимок шага. Прогон и так печатает, что случилось, — но
        /// написанное «отряд у края» и увиденное «отряд у края» это,
        /// как выяснилось 18 сентября, две разные вещи: неделю все
        /// предметы стояли в сто раз меньше, и ни одна строка отчёта
        /// об этом не сказала.
        ///
        /// Заодно это единственные наши кадры с людьми: воины рождаются
        /// в игре, и в собранной сцене их нет вовсе.
        /// </summary>
        private static void Shot(string step)
        {
            _shot++;

            // Имя файла — из имени шага, без двоеточий и косых: иначе
            // Windows молча откажет в записи посреди прогона.
            var clean = new System.Text.StringBuilder();
            foreach (var c in step)
                clean.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? ' ' : c);

            try
            {
                Sinbinder.Utilets.Snapshot.Now("Docs/Образцы/прохождение",
                    _shot.ToString("00") + " " + clean.ToString().Trim());

                // Два места, где стоит подойти вплотную: лагерь, где
                // свои, и набег, где охотники. Гардероб виден только так.
                if (step == "старший назначен") Portraits("лагерь");
                if (step == "набег: первая волна положена") Portraits("набег");
            }
            catch (Exception e)
            {
                Write("  [СНИМОК НЕ ВЫШЕЛ] " + e.Message);
            }
        }

        private static int _shot;

        /// <summary>
        /// Портреты: по одному от каждой стороны. Не все подряд — двадцать
        /// пять кадров одинаковых скелетов никто смотреть не станет,
        /// а различить оболочку и снаряжение хватает и двух.
        /// </summary>
        private static void Portraits(string where)
        {
            // Сперва сам Греховод: он не воин и в общий перебор не попадает,
            // а показывать в первую очередь надо именно его.
            var hero = SinbinderPlayer.Instance;
            if (hero != null)
                Sinbinder.Utilets.Snapshot.Portrait("Docs/Образцы/облик в игре",
                    where + " — Греховод", hero.transform);

            var seen = new HashSet<Team>();

            foreach (var warrior in UnityEngine.Object.FindObjectsByType<Warrior>(
                         FindObjectsSortMode.None))
            {
                if (warrior == null || !warrior.isActiveAndEnabled) continue;

                // Только стоящие. Павший лежит, и камера, поставленная
                // «перед лицом», ложится вместе с ним на землю: первый
                // же портрет вышел изнутри трупа.
                var body = warrior.GetComponentInChildren<SkinnedMeshRenderer>();
                if (body == null) continue;

                var size = body.bounds.size;
                if (size.y < Mathf.Max(size.x, size.z)) continue;

                if (!seen.Add(warrior.Team)) continue;

                Sinbinder.Utilets.Snapshot.Portrait("Docs/Образцы/облик в игре",
                    where + " — " + warrior.Team + " — " + warrior.DisplayName,
                    warrior.transform);
            }
        }

        private static void Dump()
        {
            var pause = Core.GamePauseController.Instance;
            Write("    · сцена " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                  + ", timeScale " + Time.timeScale.ToString("F2")
                  + ", пауза " + (pause != null && pause.IsPaused)
                  + ", разговор " + (Dialogue.DialogueCameraController.Instance != null
                                     && Dialogue.DialogueCameraController.Instance.InDialogue));

            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null) continue;

                var mind = w.GetComponent<AOS.AOSWarriorWrapper>();
                var agent = w.GetComponent<NavMeshAgent>();
                var dmg = w.GetComponent<Damageable>();

                Write("    · " + w.Team + " " + w.DisplayName
                      + (w.IsDead ? " [мёртв]" : "")
                      + " @" + w.transform.position.ToString("F0")
                      + " решение " + (mind != null ? mind.LastDecision.ToString() : "нет ума")
                      + " навмеш " + (agent != null && agent.isOnNavMesh)
                      + " жизнь " + (dmg != null ? dmg.HP.ToString("F0") : "?"));
            }
        }

        // ─────────────────────────────── вопросы ───────────────────────────────

        private static bool Scene(string name)
            => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == name;

        private static bool Paused()
            => Core.GamePauseController.Instance != null && Core.GamePauseController.Instance.IsPaused;

        private static CrystalBall Ball() => UnityEngine.Object.FindFirstObjectByType<CrystalBall>();

        private static TrophyChest Chest() => UnityEngine.Object.FindFirstObjectByType<TrophyChest>();

        /// <summary>
        /// Попадает ли луч из камеры, пущенный в экранную точку воина,
        /// в его коллайдер. Ровно это делают щелчок, правый клик и подсказка
        /// при наведении. Все лучи, а не первый: заслонить воина палаткой —
        /// не поломка, а вот пройти сквозь него — поломка.
        /// </summary>
        private static bool RayFinds(Func<Warrior, bool> which)
        {
            var cam = Camera.main;
            if (cam == null) return false;

            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || !which(w)) continue;

                var p = cam.WorldToScreenPoint(w.transform.position + Vector3.up * 0.9f);
                if (p.z <= 0f || p.x < 0f || p.y < 0f || p.x > Screen.width || p.y > Screen.height)
                    continue;

                foreach (var hit in Physics.RaycastAll(cam.ScreenPointToRay(p), 200f))
                    if (hit.collider != null && hit.collider.GetComponentInParent<Warrior>() == w)
                        return true;
            }

            return false;
        }

        private static SelectionComponent _clicked;

        /// <summary>
        /// Выделить своего тем же методом, что зовёт щелчок
        /// (<c>SelectionManager.SelectUnit</c>): нажатие в прогоне не подделать,
        /// а путь после него — можно.
        /// </summary>
        private static void SelectOneOwn()
        {
            _clicked = null;

            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer) continue;
                _clicked = w.GetComponent<SelectionComponent>();
                if (_clicked != null) break;
            }

            var manager = SelectionManager.Instance;
            if (manager == null || _clicked == null) return;

            typeof(SelectionManager)
                .GetMethod("SelectUnit", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(manager, new object[] { _clicked });
        }

        /// <summary>
        /// Подойти к своему, заговорить, отдать первую вещь мешка. Воин может
        /// не взять — это ответ души, а не провал; в отчёт идёт, что сказал.
        /// </summary>
        private static void TalkAndHand()
        {
            Warrior w = null;
            foreach (var x in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (x != null && !x.IsDead && x.Team == Team.Player && !(x is SinbinderPlayer)) { w = x; break; }
            if (w == null) return;

            HeroTo(w.transform, 1.5f);
            UI.GearPanel.TalkTo(w);

            var bag = Inventory.PlayerInventory.Instance;
            if (bag == null) return;
            foreach (var item in new List<Inventory.InventoryItem>(bag.GetAllItems()))
            {
                if (item == null || item.Slot == Inventory.GearSlot.None) continue;
                bool took = SquadGear.Hand(w, item, bag, out string word);
                Write($"  [ОБМЕН] {w.DisplayName} — {item.Name}: {(took ? "взял" : "не взял")} ({word})");
                break;
            }
            UI.GearPanel.Refresh();
        }

        private static bool _forecastOk;

        /// <summary>
        /// Прогноз «кто пойдёт» на атаку для всего отряда. Без выключателя
        /// «причина» прогноза нет, и шаг проходит сразу. Прогноз обязан
        /// быть непустым и без цифр.
        /// </summary>
        private static void ForecastSquad()
        {
            _forecastOk = false;
            if (!AOS.Counterfactual.Enabled) { _forecastOk = true; return; }

            var manager = SelectionManager.Instance;
            if (manager == null) return;
            var select = typeof(SelectionManager).GetMethod("SelectUnit", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer) continue;
                var unit = w.GetComponent<SelectionComponent>();
                if (unit != null) select?.Invoke(manager, new object[] { unit });
            }

            string text = UI.CommandPanel.Predict(CommandKind.Attack);
            Write("  [ПРОГНОЗ] " + text.Replace("\n", " | "));
            _forecastOk = !string.IsNullOrEmpty(text) && !System.Text.RegularExpressions.Regex.IsMatch(text, "[0-9]");

            foreach (var unit in new List<SelectionComponent>(manager.GetSelectedUnits())) manager.Drop(unit);
        }

        /// <summary>Выделен ли он и горит ли у ног круг. Проверив — снять выделение.</summary>
        private static bool SelectedWithRing()
        {
            var manager = SelectionManager.Instance;
            if (_clicked == null || manager == null) return false;

            bool selected = manager.GetSelectedUnits().Contains(_clicked);
            var ring = _clicked.transform.Find("Круг выбора");
            bool shown = ring != null && ring.gameObject.activeSelf
                      && ring.GetComponent<LineRenderer>() != null;

            if (selected && shown) manager.Drop(_clicked);
            return selected && shown;
        }

        /// <summary>
        /// Алтарь зала склепа — туда входит Греховод перед эпилогом.
        /// Нет алтаря — зал целиком: тем же порядком ищет PrologueDirector.
        /// </summary>
        private static Transform Altar()
        {
            var altar = GameObject.Find("Altar");
            if (altar == null) altar = GameObject.Find("Зал");
            return altar != null ? altar.transform : null;
        }

        private static UI.CommanderCouncilUI Council()
            => UnityEngine.Object.FindFirstObjectByType<UI.CommanderCouncilUI>();

        private static bool Summoned()
        {
            var council = Council();
            return council != null && Field<bool>(council, "_summoned");
        }

        private static bool CouncilPanelOpen()
        {
            var council = Council();
            var panel = council != null ? Field<GameObject>(council, "_panel") : null;
            return panel != null && panel.activeSelf;
        }

        private static int Enemies()
            => CombatManager.Instance != null ? CombatManager.Instance.GetAliveEnemyCount() : 0;

        private static bool SalaryOpen()
        {
            var salary = UnityEngine.Object.FindFirstObjectByType<UI.SalaryPanelUI>();
            var panel = salary != null ? Field<GameObject>(salary, "_panel") : null;
            return panel != null && panel.activeSelf;
        }

        private static void PaySalary()
        {
            var salary = UnityEngine.Object.FindFirstObjectByType<UI.SalaryPanelUI>();
            if (salary != null) Field<Button>(salary, "_payButton")?.onClick.Invoke();
        }

        private static bool DemoEndShown()
        {
            var end = UnityEngine.Object.FindFirstObjectByType<UI.DemoEndUI>();
            var panel = end != null ? Field<GameObject>(end, "_panel") : null;
            return panel != null && panel.activeSelf;
        }

        private static T Field<T>(object owner, string name)
        {
            var f = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return f != null && f.GetValue(owner) is T value ? value : default;
        }

        private static void Catch(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (type == LogType.Log) return;

            string first = (message ?? "").Split('\n')[0];

            if (type == LogType.Warning)
            {
                // Страховка Beat пишет единообразно, с приставкой [ПРОЛОГ].
                // По ней и отличаем «шаг не случился, но срок вышел»
                // от прочих предупреждений, которых в игре хватает.
                if (first.StartsWith("[ПРОЛОГ]")) _late++;

                Write("    [ПРЕДУПРЕЖДЕНИЕ] " + first);
                return;
            }

            _errors++;
            Write("    [ОШИБКА] " + first);
            Write("        " + (stack ?? "").Split('\n')[0]);
        }
    }
}
