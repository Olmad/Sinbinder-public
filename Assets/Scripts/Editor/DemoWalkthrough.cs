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
            // в Play со всем звуком игры — рог, голоса, бой, — идут
            // по десять минут и в любое время суток; автор попросил
            // не шуметь дважды.
            //
            // <b>Глушим не громкость слушателя, а звук редактора.</b>
            // `AudioListener.volume`, выставленный здесь, не доживает
            // до игры: вход в Play перезагружает домен и сбрасывает
            // статику к значению из настроек проекта. Ровно поэтому
            // первая правка 20 сентября не сработала, и звук пугал
            // автора снова. `EditorUtility.audioMasterMute` — та самая
            // кнопка «Mute Audio» в редакторе, она Play переживает.
            EditorUtility.audioMasterMute = true;
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
                ResetHandChecks();

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
            // Со всеми выключателями — ещё и то, что план 25 сентября
            // оставлял рукам (28-ORDERS, этап 2): лагерь живёт, приказ
            // издали с прогнозом, «Мародёр» до экрана сундука, смерть
            // и «С начала доли». В обычном прогоне этих шагов нет: он
            // остаётся тем, на который ссылается ПОКАЗ.md.
            bool all = SessionState.GetBool(AllOn, false);

            var steps = new List<Step>
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
            };

            // Лагерь живёт только до совета: назначен старший — шар ведёт
            // сцену (CrystalBall.Leading) до самого разгрома, и CampLife молчит.
            // Значит, пять минут в лагере — здесь, пока Карган ждёт у стола.
            if (all)
            {
                steps.Add(S("лагерь: пять минут игры — ходят и говорят", WatchCamp, CampDone, 400f));
                steps.Add(S("приказ издали: прогноз, отказы, причины", OrderFromAfar, FarOrderDone, 60f));
            }

            steps.AddRange(new[]
            {
                S("Греховод у стола", () => HeroTo(Ball()?.transform, 1.5f),
                  () => Ball() != null && Ball().PlayerIsClose(), 5f),

                S("совет открыт", () => Council()?.Open(),
                  () => CouncilPanelOpen(), 5f),

                S("старший назначен", ChooseCommander,
                  () => !string.IsNullOrEmpty(SquadRoster.CommanderName), 5f),

                S("Греховод у сундука", () => HeroTo(Chest()?.transform, 1.2f),
                  () => TrophyChest.Looted, 15f),

                // Сундук-склад (выключатель «склад»): экран сундука обязан
                // открыться сам и держит паузу — автопилот берёт одну вещь
                // и закрывает его. Без вещи в мешке обмену ниже нечего было
                // отдать, и шаг обмена при складе проходил впустую
                // (14-HANDOFF §105.4). Без склада шаг проходит сразу.
                S("склад: экран сундука открылся", null, () =>
                {
                    if (!TrophyChest.Store) return true;
                    if (!UI.GearPanel.Open) return false;
                    ChestOpened();
                    UI.GearPanel.Dismiss();
                    return true;
                }, 10f),
            });

            // Стык §93: «Мародёр» за клад ставит свою паузу, и экран сундука
            // обязан ждать её конца, а не лечь поверх.
            if (all)
                steps.Add(S("склад: «Мародёр» — до экрана сундука, не поверх", null, CeremonyVerdict, 30f));

            steps.AddRange(new[]
            {

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
            });

            // Смерть Греховода посреди разгрома — и «С начала доли» (§101):
            // разгром заново, отряд с теми же вещами. Дальше прогон идёт
            // по перезапущенному разгрому, как игрок после кнопки.
            if (all)
            {
                steps.Add(S("смерть: Греховод пал — экран конца", KillHero,
                            () => UI.GameOverUI.Shown, 20f));
                steps.Add(S("смерть: «С начала доли» — разгром заново, вещи те же", AgainPart,
                            PartRestarted, 90f));
            }

            steps.AddRange(new[]
            {
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
            });

            return steps;
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

        // ──────────── то, что план оставлял рукам (только со всеми выключателями) ────────────
        //
        // 28-ORDERS, 25 сентября, этап 2 — ручной проход. Здесь то из него,
        // что проверяется без глаз: ходят ли и говорят ли в лагере, что
        // отвечают на приказ издали и сходится ли это с прогнозом, ждёт ли
        // сундук церемонию, возвращает ли «С начала доли» те же вещи.
        // Глазам остаётся своё: живо это или суетливо, читается ли отказ
        // как решение, а не как сломанный ИИ, не проседают ли кадры.

        /// <summary>Свои живые, без Греховода.</summary>
        private static List<Warrior> Own()
        {
            var own = new List<Warrior>();
            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.Team == Team.Player && !(w is SinbinderPlayer)) own.Add(w);
            return own;
        }

        /// <summary>Кадр вне шагов — реплики, отказ. Туда же, куда кадры шагов.</summary>
        private static void Snap(string name)
        {
            try { Sinbinder.Utilets.Snapshot.Now("Docs/Образцы/прохождение", name); }
            catch (Exception e) { Write("  [СНИМОК НЕ ВЫШЕЛ] " + e.Message); }
        }

        // ── Лагерь живёт: пять минут игры ──

        /// <summary>Сколько минут игры стоять в лагере — столько, сколько просил план.</summary>
        private const float CampMinutes = 5f;

        /// <summary>
        /// Во сколько раз быстрее идёт игра, пока стоим. Места и разговоры
        /// идут по игровым часам, а пять настоящих минут — больше, чем
        /// весь остальной прогон.
        /// </summary>
        private const float CampFast = 6f;

        private static float _campFrom;
        private static readonly Dictionary<Warrior, string> _campWhere = new();
        private static readonly HashSet<string> _campMovers = new();
        private static readonly HashSet<string> _heardLines = new();
        private static int _campMoves, _campLines, _campShots;

        private static void WatchCamp()
        {
            _campFrom = Time.time;
            EditorApplication.update -= Camp;
            EditorApplication.update += Camp;
        }

        /// <summary>Каждый кадр: кто сменил место, кто что сказал.</summary>
        private static void Camp()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Camp; return; }
            if (!Paused() && Time.timeScale > 0f && Time.timeScale < CampFast) Time.timeScale = CampFast;

            foreach (var w in Own())
            {
                string now = CampLife.Now(w);
                if (string.IsNullOrEmpty(now)) continue;

                if (!_campWhere.TryGetValue(w, out string was))
                {
                    _campWhere[w] = now;
                    Write($"  [МЕСТО] {CampClock()} {w.DisplayName} — {now}");
                    continue;
                }
                if (was == now) continue;

                _campWhere[w] = now;
                _campMoves++;
                _campMovers.Add(w.DisplayName);
                Write($"  [МЕСТО] {CampClock()} {w.DisplayName}: {was} → {now}");
            }

            var live = Bubbles();
            bool fresh = false;
            foreach (var (who, line, until) in live)
            {
                if (!_heardLines.Add(who + "|" + line + "|" + until.ToString("F1"))) continue;
                fresh = true;
                _campLines++;
                Write($"  [РЕПЛИКА] {CampClock()} {who}: «{line}»");
            }

            // Кадр, когда над головами двое: читается ли строка с высоты
            // камеры, решают глаза, а не отчёт.
            if (fresh && live.Count >= 2 && _campShots < 3)
            {
                _campShots++;
                Snap("лагерь — реплики " + _campShots);
            }
        }

        private static bool CampDone()
        {
            if (Time.time - _campFrom < CampMinutes * 60f) return false;

            EditorApplication.update -= Camp;
            if (Mathf.Approximately(Time.timeScale, CampFast)) Time.timeScale = 1f;

            Write($"  [ЛАГЕРЬ] за {CampMinutes:0} минут игры: переходов {_campMoves} "
                  + $"(ходили {_campMovers.Count} из {_campWhere.Count}), реплик {_campLines}");

            // Ни шага или ни слова за пять минут — лагерь не живёт, а ради
            // этого он и за выключателем (14-HANDOFF §102–103).
            if (_campMoves == 0 || _campLines == 0)
            {
                _failed++;
                Write("  [ЛАГЕРЬ НЕ ЖИВЁТ] " + (_campMoves == 0 ? "никто не сменил места. " : "")
                      + (_campLines == 0 ? "Никто не заговорил." : ""));
            }
            return true;
        }

        private static string CampClock()
        {
            float t = Mathf.Max(0f, Time.time - _campFrom);
            return $"{(int)(t / 60f)}:{(int)(t % 60f):00}";
        }

        /// <summary>
        /// Строки, что сейчас над головами. У <see cref="UI.SpeechBubbles"/>
        /// нет события — и ради прогона не нужно: список читается так же,
        /// как прогон читает совет.
        /// </summary>
        private static List<(string Who, string Line, float Until)> Bubbles()
        {
            var found = new List<(string, string, float)>();

            var instance = typeof(UI.SpeechBubbles)
                .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            if (instance == null) return found;

            if (!(instance.GetType().GetField("_live", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(instance) is System.Collections.IList live)) return found;

            foreach (var bubble in live)
            {
                if (bubble == null) continue;
                var type = bubble.GetType();
                var who = type.GetField("Who")?.GetValue(bubble) as Warrior;
                var text = type.GetField("Line")?.GetValue(bubble) as Text;
                float until = type.GetField("Until")?.GetValue(bubble) is float u ? u : 0f;
                if (who == null || text == null || string.IsNullOrEmpty(text.text)) continue;
                found.Add((who.DisplayName, text.text, until));
            }
            return found;
        }

        // ── Приказ издали: прогноз против того, что вышло ──

        private const string Unheard = "не слышит";

        private static readonly List<string> _refusedNames = new();
        private static readonly Dictionary<string, string> _earshot = new();
        private static float _orderAt = -1f;
        private static string _farForecast = "";
        private static float _refusalAt = -1f;
        private static bool _refusalShot;

        /// <summary>
        /// Греховод отходит от костра, выделяет отряд, смотрит прогноз
        /// на «иди» — и отдаёт приказ тем же путём, что ПКМ по земле
        /// (<c>SelectionManager.OrderAt</c>): с голосом и его приглушённостью.
        /// Остальные приказы прогона звучат в полную силу — голосу там
        /// нечего проверять.
        /// </summary>
        private static void OrderFromAfar()
        {
            var hero = SinbinderPlayer.Instance;
            var fire = UnityEngine.Object.FindFirstObjectByType<CampOpening>();
            var manager = SelectionManager.Instance;
            if (hero == null || fire == null || manager == null)
            {
                Write("  [ПРИКАЗ ИЗДАЛИ] нет Греховода, костра или выделения — проверять нечем");
                return;
            }

            // Одиннадцать метров от костра: у огня приказ слышен тихо (голос
            // гаснет с половины зрения Греховода), дальние места не слышат.
            Warp(hero.gameObject, fire.transform.position + Vector3.right * 11f);

            var select = typeof(SelectionManager).GetMethod("SelectUnit", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var w in Own())
            {
                var unit = w.GetComponent<SelectionComponent>();
                if (unit != null) select?.Invoke(manager, new object[] { unit });

                float muffle = Voice.MuffleFor(w);
                string heard = !Voice.Heard(muffle) ? Unheard : muffle > 0f ? "слышит тихо" : "слышит";
                _earshot[w.DisplayName] = heard;
                Write($"  [ГОЛОС] {w.DisplayName}: "
                      + $"{CampFocus.GroundDistance(SinbinderPlayer.Where, w.transform.position):0} м — {heard}");
            }

            _farForecast = UI.CommandPanel.Predict(CommandKind.Move) ?? "";
            Write("  [ПРОГНОЗ ИЗДАЛИ] " + _farForecast.Replace("\n", " | "));

            var hub = AOS.AOSEventHub.Instance;
            if (hub != null) { hub.OnRefusal -= Refused; hub.OnRefusal += Refused; }

            var toward = fire.transform.position - hero.transform.position;
            toward.y = 0f;
            if (!Ground(hero.transform.position + toward.normalized * 2f, out var ground))
            {
                Write("  [ПРИКАЗ ИЗДАЛИ] под точкой приказа нет земли");
                return;
            }

            typeof(SelectionManager).GetMethod("OrderAt", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(manager, new object[] { ground, false });
            _orderAt = Time.time;
            Write("  [ПРИКАЗ ИЗДАЛИ] «иди» — всему отряду, к Греховоду");
        }

        /// <summary>Земля под точкой — то, во что попал бы щелчок: не воин.</summary>
        private static bool Ground(Vector3 at, out RaycastHit ground)
        {
            ground = default;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(new Ray(at + Vector3.up * 30f, Vector3.down), 60f))
            {
                if (hit.collider == null || hit.collider.GetComponentInParent<Warrior>() != null) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                ground = hit;
            }
            return best < float.MaxValue;
        }

        private static void Refused(Warrior w, AOS.Decision d, AOS.DecisionContext c)
        {
            if (w == null || _refusedNames.Contains(w.DisplayName)) return;
            _refusedNames.Add(w.DisplayName);

            string why;
            try { why = AOS.PhraseGenerator.Reason(w, c, d); }
            catch (Exception e) { why = "(причина не собралась: " + e.Message + ")"; }

            Write($"  [ОТКАЗ] {w.DisplayName}: {(string.IsNullOrEmpty(why) ? "(без причины)" : why)}"
                  + $" · решило: {d.Decisive}"
                  + (d.DecisiveAlso != AOS.Counterfactual.Factor.None ? " и " + d.DecisiveAlso : "")
                  + (string.IsNullOrEmpty(d.DecisiveVoice) ? "" : ", голос " + d.DecisiveVoice));

            if (_refusalAt < 0f) _refusalAt = Time.realtimeSinceStartup;
        }

        private static bool FarOrderDone()
        {
            if (_orderAt < 0f) return true;   // проверять было нечем — сказано выше

            // Кадр отказа — чуть позже самого отказа: подпись и наезд
            // встают не в тот же кадр.
            if (!_refusalShot && _refusalAt > 0f && Time.realtimeSinceStartup - _refusalAt > 0.8f)
            {
                _refusalShot = true;
                Snap("приказ издали — отказ");
            }

            if (Time.time - _orderAt < 8f) return false;

            var hub = AOS.AOSEventHub.Instance;
            if (hub != null) hub.OnRefusal -= Refused;

            int doubtAt = _farForecast.IndexOf("Вряд ли", StringComparison.Ordinal);
            string doubtful = doubtAt >= 0 ? _farForecast.Substring(doubtAt) : "";
            int agree = 0, differ = 0;

            foreach (var w in Own())
            {
                string name = w.DisplayName;
                if (_earshot.TryGetValue(name, out string heard) && heard == Unheard)
                {
                    Write($"  [СВЕРКА] {name}: не слышал — приказа не было");
                    continue;
                }

                bool doubted = doubtful.Contains(name);
                bool refused = _refusedNames.Contains(name);
                if (doubted == refused) agree++; else differ++;
                Write($"  [СВЕРКА] {name}: прогноз — {(doubted ? "вряд ли" : "пойдёт")}, "
                      + $"вышло — {(refused ? "отказал" : "послушался")}"
                      + (doubted == refused ? "" : "   ← разошлось"));
            }

            Write($"  [ПРИКАЗ ИЗДАЛИ] прогноз сошёлся у {agree}, разошёлся у {differ}; отказов {_refusedNames.Count}");

            // Назад, к лагерю: приказ снят, выделение тоже.
            foreach (var w in Own()) w.ClearCommand();
            var manager = SelectionManager.Instance;
            if (manager != null)
                foreach (var unit in new List<SelectionComponent>(manager.GetSelectedUnits())) manager.Drop(unit);

            return true;
        }

        // ── Сундук и «Мародёр» ──

        private static readonly List<(float At, string Line)> _ceremonies = new();
        private static float _chestOpenedAt = -1f;
        private static bool _ceremonyAtOpen;

        /// <summary>
        /// Экран сундука открылся: запомнить миг и шла ли церемония; взять
        /// одну вещь с местом на теле — «взять часть, остальное оставить».
        /// </summary>
        private static void ChestOpened()
        {
            _chestOpenedAt = Time.realtimeSinceStartup;
            _ceremonyAtOpen = CeremonyPlaying();
            Write("  [СУНДУК] экран открылся" + (_ceremonyAtOpen ? " — а церемония ещё идёт" : ""));

            var chest = Chest();
            var bag = Inventory.PlayerInventory.Instance;
            if (chest == null || bag == null) return;

            var left = TrophyChest.Remaining();
            foreach (var item in left)
            {
                if (item == null || item.Slot == Inventory.GearSlot.None) continue;
                bool took = chest.Take(item, bag, out string word);
                Write($"  [СКЛАД] из сундука: {item.Name} — {(took ? word : "не взял: " + word)}; "
                      + $"в сундуке осталось {TrophyChest.Remaining().Count} из {left.Count}");
                return;
            }
            Write("  [СКЛАД] в сундуке нет вещи, которую можно надеть");
        }

        private static bool CeremonyPlaying()
        {
            var ceremony = UnityEngine.Object.FindFirstObjectByType<AOS.TitleCeremonyBehaviour>();
            return ceremony != null && Field<bool>(ceremony, "_playing");
        }

        /// <summary>
        /// Приговор стыку: церемония прозвучала до экрана сундука, и ни одна
        /// не началась после — иначе она легла бы поверх открытого экрана.
        /// Ждём, пока церемоний в очереди нет и прошло восемь секунд.
        /// </summary>
        private static bool CeremonyVerdict()
        {
            if (_chestOpenedAt < 0f)
            {
                Write("  [СУНДУК] экран сундука не открывался — сверять не с чем");
                return true;
            }
            if (CeremonyPlaying() || Time.realtimeSinceStartup - _chestOpenedAt < 8f) return false;

            int near = 0, after = 0;
            foreach (var (at, line) in _ceremonies)
            {
                float gap = at - _chestOpenedAt;
                if (gap > 0f) after++;
                else if (gap > -30f) near++;
                Write($"  [ЦЕРЕМОНИЯ] {(gap > 0f ? "через" : "за")} {Mathf.Abs(gap):0} с "
                      + $"{(gap > 0f ? "ПОСЛЕ экрана сундука" : "до экрана")}: {line}");
            }

            if (_ceremonyAtOpen || after > 0)
            {
                _failed++;
                Write("  [СУНДУК ПОВЕРХ ЦЕРЕМОНИИ] экран сундука открылся, пока церемония шла или ждала камеры");
            }
            else if (near == 0)
                Write("  [СУНДУК] церемонии у сундука не было — «Мародёр» не вручён, стык не проверен");
            else
                Write("  [СУНДУК] экран — после церемонии, как задумано");
            return true;
        }

        // ── Смерть и «С начала доли» ──

        private static Dictionary<string, string> _gearBefore;
        private static SinbinderPlayer _fallenHero;

        /// <summary>
        /// Сперва падает один из отряда, потом Греховод — от ближайшего
        /// охотника, тем же уроном, что в бою (<see cref="Damageable.TakeDamage"/>);
        /// конец игры Греховод замечает сам. Так и бывает в разгроме: свои
        /// гибнут раньше. Отметка начала доли помнит павшего живым, и
        /// «С начала доли» обязана его вернуть (SaveSystem.RestartPart:
        /// «иначе мёртвые остались бы мёртвыми»). Состав, вещи, мешок,
        /// сундук и ушедшие запоминаются до обеих смертей.
        /// </summary>
        private static void KillHero()
        {
            _gearBefore = GearNow();
            foreach (var pair in _gearBefore)
                Write($"  [ДОЛЯ] до смерти — {pair.Key}: {(string.IsNullOrEmpty(pair.Value) ? "пусто" : pair.Value)}");

            var hero = SinbinderPlayer.Instance;
            if (hero == null) return;
            _fallenHero = hero;

            var own = Own();
            if (own.Count > 0)
            {
                Write($"  [ДОЛЯ] перед Греховодом пал {own[0].DisplayName}");
                Strike(own[0].gameObject);
            }

            Strike(hero.gameObject);
        }

        /// <summary>Смертельный удар от ближайшего живого охотника.</summary>
        private static void Strike(GameObject victim)
        {
            GameObject killer = null;
            float best = float.MaxValue;
            var enemies = CombatManager.Instance != null ? CombatManager.Instance.GetAliveEnemies() : null;
            if (enemies != null)
                foreach (var e in enemies)
                {
                    if (e == null || e.IsDead) continue;
                    float d = (e.transform.position - victim.transform.position).sqrMagnitude;
                    if (d < best) { best = d; killer = e.gameObject; }
                }

            var body = victim.GetComponent<Damageable>();
            if (body != null) body.TakeDamage(body.MaxHP * 100f + 1000f, killer);
        }

        /// <summary>Кнопка «С начала доли» — её же обработчиком.</summary>
        private static void AgainPart()
        {
            Write("  [ДОЛЯ] экран конца: " + (Core.SaveSystem.CanRestartPart
                ? "кнопка «С начала доли» есть"
                : "КНОПКИ «С начала доли» НЕТ — будет полный перезапуск"));
            typeof(UI.GameOverUI).GetMethod("AgainPart", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, null);
        }

        /// <summary>
        /// Разгром заново: новый Греховод жив, экрана конца нет, охотники
        /// на поле. И вещи те же, что были до смерти.
        /// </summary>
        private static bool PartRestarted()
        {
            var hero = SinbinderPlayer.Instance;
            if (UI.GameOverUI.Shown || hero == null || hero == _fallenHero || hero.IsDead) return false;
            if (!RaidEvent.Running || Enemies() == 0) return false;
            if (_gearBefore == null) return true;

            var now = GearNow();
            int differ = 0;
            foreach (var pair in _gearBefore)
            {
                now.TryGetValue(pair.Key, out string after);
                if (after == pair.Value) continue;
                differ++;
                Write($"  [ДОЛЯ РАЗОШЛАСЬ] {pair.Key}: было «{pair.Value}», стало «{after ?? "(его нет)"}»");
            }
            foreach (var pair in now)
                if (!_gearBefore.ContainsKey(pair.Key))
                    Write($"  [ДОЛЯ] снова в строю: {pair.Key} — {pair.Value}");

            if (differ > 0) _failed++;
            else Write("  [ДОЛЯ] разгром заново; вещи отряда, мешок и сундук — те же, что до смерти");
            return true;
        }

        /// <summary>Что на ком, что в мешке и в сундуке — словами, для сравнения.</summary>
        private static Dictionary<string, string> GearNow()
        {
            var gear = new Dictionary<string, string>();
            foreach (var w in Own()) gear[w.DisplayName] = SquadGear.Summary(w);

            var bag = new List<string>();
            var inventory = Inventory.PlayerInventory.Instance;
            if (inventory != null)
                foreach (var item in inventory.GetAllItems())
                    if (item != null) bag.Add(item.Name);
            bag.Sort(StringComparer.Ordinal);
            gear["(мешок)"] = string.Join(", ", bag);

            var chest = new List<string>();
            foreach (var item in TrophyChest.Remaining())
                if (item != null) chest.Add(item.Name);
            chest.Sort(StringComparer.Ordinal);
            gear["(сундук)"] = string.Join(", ", chest);

            // Ушедших на вылазку нет ни в одной сцене — их правда живёт
            // только в составе, и возвращаться им в эпилоге.
            var away = new List<string>();
            foreach (var m in SquadRoster.Away) away.Add(m.Name);
            away.Sort(StringComparer.Ordinal);
            gear["(ушли с вылазкой)"] = string.Join(", ", away);
            gear["(старший)"] = SquadRoster.CommanderName;

            return gear;
        }

        /// <summary>Сбросить память проверок этапа 2 — в начале прогона.</summary>
        private static void ResetHandChecks()
        {
            _campWhere.Clear();
            _campMovers.Clear();
            _heardLines.Clear();
            _campMoves = _campLines = _campShots = 0;
            _refusedNames.Clear();
            _earshot.Clear();
            _orderAt = -1f;
            _farForecast = "";
            _refusalAt = -1f;
            _refusalShot = false;
            _ceremonies.Clear();
            _chestOpenedAt = -1f;
            _ceremonyAtOpen = false;
            _gearBefore = null;
            _fallenHero = null;
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

            string first = (message ?? "").Split('\n')[0];

            // Церемония титула пишет обычной строкой; стыку со сундуком
            // нужно, когда именно она прозвучала.
            if (first.StartsWith("[TITLE CEREMONY]"))
                _ceremonies.Add((Time.realtimeSinceStartup, first));

            if (type == LogType.Log) return;

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
