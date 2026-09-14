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
    /// </summary>
    [InitializeOnLoad]
    public static class DemoWalkthrough
    {
        private const string Active = "Sinbinder.DemoWalkthrough.Active";
        private const string Report = "Logs/demo-walkthrough.txt";

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

        static DemoWalkthrough()
        {
            if (!SessionState.GetBool(Active, false)) return;

            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
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
                Write("=== ПРОХОЖДЕНИЕ ===");
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
                Next();
                return;
            }

            if (spent >= step.Limit)
            {
                _failed++;
                Write("  [ЗАСТРЯЛО] " + step.Name + " — не дождались за " + step.Limit.ToString("F0") + " с");
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
            Write(_failed == 0
                ? "=== ПРОЙДЕНО ЦЕЛИКОМ ==="
                : "=== КОНЕЦ: застряло шагов — " + _failed + " ===");

            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(_failed == 0 ? 0 : 1);
        }

        // ─────────────────────────────── шаги ───────────────────────────────

        private static List<Step> Build()
        {
            return new List<Step>
            {
                // ── Лагерь ──
                S("лагерь загрузился, заставка ушла", null,
                  () => Scene("Prologue_Camp") && SinbinderPlayer.Exists && !Paused(), 30f),

                S("Греховод вышел из палатки", () => StepHero(4.5f),
                  () => true, 2f),

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

                S("тревога после сундука", null,
                  () => Ball() != null && Ball().IsAlarmed, 10f),

                S("Греховод у горящего шара", () => HeroTo(Ball()?.transform, 1.5f),
                  () => Scene("Prologue_Raid"), 60f),

                // ── Набег ──
                S("набег: охотники вышли", null,
                  () => Enemies() > 0, 30f),

                S("набег: первая волна положена", AttackAll,
                  () => Enemies() == 0 && FirstWaveOver(), 150f),

                S("набег: души собраны", HarvestAll,
                  () => SoulManager.Instance == null || SoulManager.Instance.FadingCount == 0
                        || Core.Satchel.FreeJar() < 0, 90f),

                S("набег: подкрепление вышло и край открыт", null,
                  () => EscapeZone.Active != null && EscapeZone.Active.Open, 20f),

                S("набег: отряд у края", SquadToEscape,
                  () => Scene("Crypt_Entrance"), 45f),

                // ── Склеп ──
                S("склеп: спросили о плате", null,
                  () => SalaryOpen(), 20f),

                S("склеп: заплатили", PaySalary,
                  () => UI.SalaryPanelUI.Answered, 5f),

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
                Write("    [ПРЕДУПРЕЖДЕНИЕ] " + first);
                return;
            }

            Write("    [ОШИБКА] " + first);
            Write("        " + (stack ?? "").Split('\n')[0]);
        }
    }
}
