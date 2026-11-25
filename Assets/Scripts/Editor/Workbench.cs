#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Верстак — постоянная скрытая Unity для Claude (автор, 19.11: «можем ли мы как-то программно ускорить
    /// разработку? может сделать конструктор для тебя?»).
    ///
    /// Прогон сна идёт в настоящем времени, и ради одного кадра у моста его приходилось играть с начала.
    /// Верстак поднимается один раз и ждёт команд: сон до нужного шага и стоп на паузе, кадры с любой точки,
    /// поиск и перенос предметов, чтение и запись полей, вызов методов, перекомпиляция без перезапуска.
    ///
    /// Команда — файл Logs/workbench/in/&lt;id&gt;.cmd (одна строка), ответ — Logs/workbench/out/&lt;id&gt;.txt.
    /// Клиент — D:\Claude\runs\wb.ps1 (несколько команд через « ;; », рецепты — «wb.ps1 run &lt;имя&gt;»).
    /// Простоял 45 минут без команд — выходит сам: ОЗУ на ПК автора мало.
    /// Пока верстак держит проект, другая Unity его не откроет: перед «Собрать игру.cmd» — «wb quit».
    ///
    /// Запускать только на скрытом рабочем столе (D:\Claude\runs\HiddenDesk.cs, это делает «wb.ps1 start»):
    /// через пару секунд после снятия игры с паузы пакетная Unity открывает окно «Game» — 19.11 оно
    /// всплыло на рабочем ПК автора, где окна запрещены.
    /// </summary>
    [InitializeOnLoad]
    public static class Workbench
    {
        private const string Key = "Sinbinder.Workbench.";
        private const string DreamActive = "Sinbinder.DreamWalkthrough.Active";
        private const string Root = "Logs/workbench";
        private const string Shots = Root + "/shots";
        // 24.11: было 20 мин — холодный подъём стоит до трёх минут, а перерыв на чтение кадров дольше двадцати.
        private const float IdleQuit = 45f * 60f;

        private const string Help =
            "ping — жив ли, что открыто\n" +
            "refresh — перечитать файлы и перекомпилировать (ответ — ошибки компиляции)\n" +
            "open <Assets/...unity> — открыть сцену; play / stop / pause / resume; time <масштаб>\n" +
            "wait <с> — подождать в игре\n" +
            "dream [until=bridge] [map] [nochase] [late] [stop=<шаг>] — прогон сна; nochase — поднятый сам не гонится (ветка\n" +
            "    «прикажите догнать»); late — пал в засаде до прихода (E, R — снова в тело); stop= — пауза в начале шага\n" +
            "dreamwait — снять паузу и досмотреть сон до отчёта\n" +
            "shot <имя> [откуда x,y,z] [куда x,y,z] — кадр в Logs/workbench/shots\n" +
            "cam [x,y,z [x,y,z]] | cam off — поставить камеру (игровая отключается) / вернуть\n" +
            "find <часть имени> — предметы и где стоят; move <имя> x,y,z — перенести (агента — Warp)\n" +
            "get Тип.Член | Тип#член — значение (# — у первого такого в сцене); set Тип.Член <знач>\n" +
            "call Тип.Метод [арг…] | Тип#Метод — вызвать; числа через точку, вектор x,y,z, enum по имени\n" +
            "buttons — видимые кнопки UI; click <надпись или имя> — нажать, как игрок; hover <надпись> — навести\n" +
            "film лагерь | поле | 1 3 — вертикальные Shorts 1080×1920 (Editor/TrailerShorts) в D:\\Claude\\Shorts\\кадры\n" +
            "log [n] — последние строки журнала Unity; quit — выйти";

        private static readonly List<string> Recent = new();
        private static float _polledAt;

        public static bool Hosting => SessionState.GetBool(Key + "Active", false);

        // Команда, которая ждёт: перекомпиляции, входа в игру, конца сна, времени. В SessionState —
        // переживает перезагрузку домена (вход в игру и перекомпиляция её делают).
        private static string PendingId { get => SessionState.GetString(Key + "Id", ""); set => SessionState.SetString(Key + "Id", value); }
        private static string PendingKind { get => SessionState.GetString(Key + "Kind", ""); set => SessionState.SetString(Key + "Kind", value); }
        private static float PendingSince { get => SessionState.GetFloat(Key + "Since", 0f); set => SessionState.SetFloat(Key + "Since", value); }
        private static float PendingFor { get => SessionState.GetFloat(Key + "For", 0f); set => SessionState.SetFloat(Key + "For", value); }
        private static float LastCommand { get => SessionState.GetFloat(Key + "Last", 0f); set => SessionState.SetFloat(Key + "Last", value); }
        private static float Now => (float)EditorApplication.timeSinceStartup;

        static Workbench()
        {
            if (Hosting) Hook();
        }

        /// <summary>Точка входа: Unity -batchmode -executeMethod Sinbinder.EditorTools.Workbench.Run (без -quit).</summary>
        public static void Run()
        {
            EditorUtility.audioMasterMute = true;
            Directory.CreateDirectory(Root + "/in");
            Directory.CreateDirectory(Root + "/out");
            Directory.CreateDirectory(Shots);
            foreach (var f in Directory.GetFiles(Root + "/in")) File.Delete(f);
            foreach (var f in Directory.GetFiles(Root + "/out")) File.Delete(f);
            SessionState.SetBool(Key + "Active", true);
            PendingId = ""; PendingKind = "";
            LastCommand = Now;
            Hook();
            File.WriteAllText(Root + "/ready.txt",
                System.Diagnostics.Process.GetCurrentProcess().Id + "\n" + DateTime.Now.ToString("HH:mm:ss"));
        }

        private static void Hook()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Application.logMessageReceived -= Keep;
            Application.logMessageReceived += Keep;
            CompilationPipeline.assemblyCompilationFinished -= Compiled;
            CompilationPipeline.assemblyCompilationFinished += Compiled;
        }

        private static void Keep(string message, string stack, LogType type)
        {
            string tag = type == LogType.Log ? "" : type == LogType.Warning ? "[!] " : "[ОШИБКА] ";
            Recent.Add(tag + (message ?? "").Split('\n')[0]);
            if (Recent.Count > 400) Recent.RemoveRange(0, 100);
        }

        private static void Compiled(string assembly, CompilerMessage[] messages)
        {
            var errors = messages.Where(m => m.type == CompilerMessageType.Error)
                                 .Select(m => $"{Path.GetFileName(m.file)}({m.line}): {m.message}").ToList();
            if (errors.Count == 0) return;
            string was = SessionState.GetString(Key + "Errors", "");
            SessionState.SetString(Key + "Errors", was + string.Join("\n", errors) + "\n");
        }

        // ──────────────────────────────────
        // Очередь
        // ──────────────────────────────────

        private static void Poll()
        {
            if (!Hosting) return;
            float now = Now;
            if (now - _polledAt < 0.2f) return;
            _polledAt = now;

            if (PendingKind != "") { Wait(now); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            string inbox = Root + "/in";
            string next = Directory.Exists(inbox) ? Directory.GetFiles(inbox, "*.cmd").OrderBy(f => f).FirstOrDefault() : null;
            if (next == null)
            {
                if (now - LastCommand > IdleQuit) Quit("простой " + (IdleQuit / 60f).ToString("0") + " мин");
                return;
            }

            string id = Path.GetFileNameWithoutExtension(next), line;
            try { line = File.ReadAllText(next, Encoding.UTF8).Trim(); File.Delete(next); }
            catch { return; }   // клиент ещё пишет — в следующий раз
            LastCommand = now;

            string answer;
            try { answer = Do(id, line); }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                answer = "ОШИБКА: " + inner.GetType().Name + ": " + inner.Message;
                if (PendingId == id) { PendingKind = ""; PendingId = ""; }
            }
            if (answer != null) Reply(id, answer);   // null — ответ придёт, когда дождёмся
        }

        private static void Wait(float now)
        {
            float since = now - PendingSince;
            switch (PendingKind)
            {
                case "refresh":
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating || since < 2f) return;
                    string errors = SessionState.GetString(Key + "Errors", "");
                    Done(errors == "" ? $"собрано, {since:0} с" : "ОШИБКИ КОМПИЛЯЦИИ:\n" + errors.TrimEnd());
                    return;
                case "play":
                    if (EditorApplication.isPlaying && since > 1f) Done($"играет ({since:0} с); " + State());
                    else if (since > 120f) Done("ОШИБКА: игра не запустилась за 120 с");
                    return;
                case "stop":
                    if (!EditorApplication.isPlaying) Done("остановлено");
                    return;
                case "wait":
                    if (since >= PendingFor) Done($"прошло {PendingFor:0.#} с; " + State());
                    return;
                case "film":
                    if (!Sinbinder.Utilets.TrailerShoot.ShortsBusy) Done(Sinbinder.Utilets.TrailerShoot.ShortsReport);
                    return;
                case "dream":
                    // Отвечают DreamEnded и StopAt; это — страховка, если сон кончился без них.
                    if (since > 5f && !EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(DreamActive, false))
                        Done("сон кончился без отчёта\n" + DreamSummary());
                    return;
                default:
                    Done("ОШИБКА: неизвестное ожидание «" + PendingKind + "»");
                    return;
            }
        }

        private static void Pend(string id, string kind)
        {
            PendingId = id;
            PendingKind = kind;
            PendingSince = Now;
        }

        private static void Done(string answer)
        {
            string id = PendingId;
            PendingKind = "";
            PendingId = "";
            if (id != "") Reply(id, answer);
        }

        private static void Reply(string id, string answer)
        {
            string file = Root + "/out/" + id + ".txt", tmp = file + ".tmp";
            Directory.CreateDirectory(Root + "/out");
            File.WriteAllText(tmp, answer, new UTF8Encoding(false));
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        private static void Quit(string why)
        {
            SessionState.SetBool(Key + "Active", false);
            try { File.WriteAllText(Root + "/ready.txt", "0\n" + why); } catch { }
            EditorApplication.update -= Poll;
            EditorApplication.Exit(0);
        }

        // ──────────────────────────────────
        // Команды
        // ──────────────────────────────────

        private static string Do(string id, string line)
        {
            var a = Split(line);
            if (a.Count == 0) return "пустая команда";
            switch (a[0].ToLowerInvariant())
            {
                case "help": return Help;
                case "ping": return "живой; " + State();
                case "refresh":
                    if (EditorApplication.isPlaying) return "ОШИБКА: сначала stop";
                    SessionState.SetString(Key + "Errors", "");
                    Pend(id, "refresh");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    return null;
                case "open":
                    if (EditorApplication.isPlaying) return "ОШИБКА: сначала stop";
                    string scene = ScenePath(Arg(a, 1));
                    EditorSceneManager.OpenScene(scene);
                    return "открыта " + scene;
                case "play":
                    if (EditorApplication.isPlaying) return "уже играет";
                    Pend(id, "play");
                    EditorApplication.isPlaying = true;
                    return null;
                case "stop":
                    SessionState.SetBool(DreamActive, false);
                    if (!EditorApplication.isPlaying) return "не играет";
                    EditorApplication.isPaused = false;
                    Pend(id, "stop");
                    EditorApplication.isPlaying = false;
                    return null;
                case "pause": EditorApplication.isPaused = true; return "пауза; " + State();
                case "resume": EditorApplication.isPaused = false; return "дальше; " + State();
                case "time": Time.timeScale = F(Arg(a, 1)); return "timeScale " + Time.timeScale.ToString(CultureInfo.InvariantCulture);
                case "wait": PendingFor = F(Arg(a, 1)); Pend(id, "wait"); return null;
                case "dream": return Dream(id, a);
                case "dreamwait":
                    if (!SessionState.GetBool(DreamActive, false) || !EditorApplication.isPlaying) return "сон не идёт";
                    EditorApplication.isPaused = false;
                    Pend(id, "dream");
                    return null;
                case "shot": return Shot(a);
                case "cam": return Cam(a);
                case "find": return Find(Arg(a, 1));
                case "move": return Move(Arg(a, 1), V(Arg(a, 2)));
                case "get": return Show(Member(Arg(a, 1), null, false));
                case "set": Member(Arg(a, 1), Arg(a, 2), true); return "ок: " + Show(Member(Arg(a, 1), null, false));
                case "call": return Show(Call(Arg(a, 1), a.Skip(2).ToArray()));
                case "buttons":
                    var all = Buttons().Select(b => (b.interactable ? "" : "(неактивна) ") + Name(b)).ToList();
                    return all.Count == 0 ? "видимых кнопок нет" : string.Join("\n", all);
                case "click": return Click(string.Join(" ", a.Skip(1)));
                case "hover": return Hover(string.Join(" ", a.Skip(1)));
                case "film":
                    Pend(id, "film");
                    Sinbinder.Utilets.TrailerShoot.Film(string.Join(" ", a.Skip(1)));
                    return null;
                case "log":
                    int n = a.Count > 1 ? int.Parse(a[1]) : 30;
                    return Recent.Count == 0 ? "(журнал пуст с последней перезагрузки)" : string.Join("\n", Recent.Skip(Math.Max(0, Recent.Count - n)));
                case "quit": Reply(id, "выхожу"); Quit("по команде"); return null;
                default: return "не знаю команду «" + a[0] + "». help — список";
            }
        }

        private static string State()
        {
            string mode = !EditorApplication.isPlaying ? "правка" : EditorApplication.isPaused ? "пауза" : "играет";
            string s = $"сцена {SceneManager.GetActiveScene().name}, {mode}";
            if (EditorApplication.isPlaying) s += $", t {Time.timeSinceLevelLoad:0} с";
            if (SessionState.GetBool(DreamActive, false)) s += $", сон: шаг «{DreamWalkthrough.Step}», стадия {Gameplay.DreamTutorialDirector.StageName}";
            return s;
        }

        private static string Arg(List<string> a, int i)
            => i < a.Count ? a[i] : throw new ArgumentException($"не хватает аргумента №{i}");

        /// <summary>Сцена по пути или по имени («Prologue_Camp»).</summary>
        private static string ScenePath(string name)
        {
            if (name.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return name;
            return AssetDatabase.FindAssets("t:Scene " + name).Select(AssetDatabase.GUIDToAssetPath)
                       .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name)
                   ?? throw new ArgumentException("нет сцены «" + name + "»");
        }

        // ── кнопки UI: нажать, как игрок ──

        private static IEnumerable<UnityEngine.UI.Button> Buttons()
            => UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID)
                   .Where(b => b.isActiveAndEnabled);

        /// <summary>Надпись кнопки (первый текст внутри) или имя объекта.</summary>
        private static string Name(Component c)
        {
            var t = c.GetComponentInChildren<UnityEngine.UI.Text>(false);
            return t != null && !string.IsNullOrWhiteSpace(t.text) ? t.text.Split('\n')[0].Trim() : c.name;
        }

        /// <summary>Сперва точное совпадение надписи или имени, потом — часть.</summary>
        private static T Pick<T>(IEnumerable<T> items, string what, out int count) where T : Component
        {
            var list = items.ToList();
            var hits = list.Where(c => Name(c) == what || c.name == what).ToList();
            if (hits.Count == 0)
                hits = list.Where(c => Name(c).IndexOf(what, StringComparison.OrdinalIgnoreCase) >= 0
                                    || c.name.IndexOf(what, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            count = hits.Count;
            return hits.FirstOrDefault();
        }

        private static string Click(string what)
        {
            var b = Pick(Buttons(), what, out int count);
            if (b == null) return "ОШИБКА: нет кнопки «" + what + "»; buttons — список";
            if (!b.interactable) return "ОШИБКА: кнопка «" + Name(b) + "» неактивна";
            b.onClick.Invoke();
            return "нажата «" + Name(b) + "»" + (count > 1 ? $" (подходит {count}, взята первая)" : "");
        }

        private static string Hover(string what)
        {
            var triggers = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventTrigger>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID).Where(t => t.isActiveAndEnabled);
            var t = Pick(triggers, what, out int count);
            if (t == null) return "ОШИБКА: не на что навести «" + what + "»";
            UnityEngine.EventSystems.ExecuteEvents.Execute(t.gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            return "наведено на «" + Name(t) + "»" + (count > 1 ? $" (подходит {count}, взято первое)" : "");
        }

        // ── сон ──

        private static string Dream(string id, List<string> a)
        {
            if (EditorApplication.isPlaying) return "ОШИБКА: сначала stop";
            string until = null, map = null, noChase = null, late = null, stop = "";
            foreach (var w in a.Skip(1))
            {
                if (w.StartsWith("until=")) until = w.Substring(6);
                else if (w == "map") map = "1";
                else if (w == "nochase") noChase = "1";
                else if (w == "late") late = "1";
                else if (w.StartsWith("stop=")) stop = w.Substring(5);
                else return "ОШИБКА: непонятно «" + w + "»";
            }
            // Прогон читает режимы из окружения процесса; ставим заново каждый раз — иначе прошлый режим залипнет.
            Environment.SetEnvironmentVariable("SINBINDER_UNTIL", until);
            Environment.SetEnvironmentVariable("SINBINDER_MAP_ONLY", map);
            Environment.SetEnvironmentVariable("SINBINDER_NOCHASE", noChase);
            Environment.SetEnvironmentVariable("SINBINDER_LATE", late);
            SessionState.SetString(Key + "StopAt", stop);
            Pend(id, "dream");
            DreamWalkthrough.Run();
            return null;
        }

        /// <summary>Прогон сна начал шаг: не стоп ли здесь? Да — игра на паузу, ждущей команде ответ.</summary>
        public static bool StopAt(string step)
        {
            if (!Hosting) return false;
            string at = SessionState.GetString(Key + "StopAt", "");
            if (at == "" || !step.StartsWith(at, StringComparison.OrdinalIgnoreCase)) return false;
            SessionState.SetString(Key + "StopAt", "");
            EditorApplication.isPaused = true;
            if (PendingKind == "dream")
                Done($"стоп на шаге «{step}», игра на паузе ({State()}). dreamwait — досмотреть, stop — бросить.");
            return true;
        }

        /// <summary>Прогон сна кончился (DreamWalkthrough.End): отчёт ждущей команде вместо выхода из Unity.</summary>
        public static void DreamEnded()
        {
            if (PendingKind == "dream") Done(DreamSummary());
        }

        private static string DreamSummary()
        {
            const string report = "Logs/dream-walkthrough.txt";
            if (!File.Exists(report)) return "отчёта нет";
            var lines = File.ReadAllLines(report, Encoding.UTF8);
            var keep = lines.Where(l => l.Contains("===") || l.Contains("итог:") || l.Contains("[ОШИБКА]")
                                     || l.Contains("СНИМОК НЕ ВЫШЕЛ") || l.Contains("ЗАСТРЯЛО")).Take(30).ToList();
            int shots = lines.Count(l => l.Contains("[КАДР]"));
            keep.Add($"кадров {shots}; отчёт: {Path.GetFullPath(report)}");
            return string.Join("\n", keep);
        }

        // ── камера и кадры ──

        private static string Shot(List<string> a)
        {
            var cam = Camera.main;
            if (cam == null) return "ОШИБКА: в сцене нет Camera.main";
            string name = a.Count > 1 ? a[1] : DateTime.Now.ToString("HHmmss");
            var t = cam.transform;
            Vector3 was = t.position;
            Quaternion wasRot = t.rotation;
            if (a.Count > 2) t.position = V(a[2]);
            if (a.Count > 3) t.LookAt(V(a[3]));
            string where = $"камера {t.position:F1}, взгляд {t.forward:F2}";
            try { Utilets.Snapshot.Now(Shots, name); }
            finally { if (a.Count > 2) t.SetPositionAndRotation(was, wasRot); }
            string file = Path.GetFullPath($"{Shots}/{name}.png");
            return (File.Exists(file) ? file : "КАДР НЕ ЛЁГ: " + file) + "; " + where;
        }

        private static string Cam(List<string> a)
        {
            var cam = Camera.main;
            if (cam == null) return "ОШИБКА: в сцене нет Camera.main";
            var rig = cam.GetComponent<Gameplay.RTS_Camera>();
            if (a.Count > 1 && a[1] == "off")
            {
                if (rig != null) rig.enabled = true;
                return "камера снова игровая";
            }
            if (a.Count > 1)
            {
                if (rig != null) rig.enabled = false;
                cam.transform.position = V(a[1]);
                if (a.Count > 2) cam.transform.LookAt(V(a[2]));
            }
            return $"камера {cam.transform.position:F1}, взгляд {cam.transform.forward:F2}, игровая {(rig != null && rig.enabled ? "да" : "нет")}";
        }

        // ── предметы ──

        private static string Find(string part)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++n > 40) { sb.AppendLine("…и ещё"); break; }
                sb.AppendLine($"{PathOf(t)}  {t.position:F1}{(t.gameObject.activeInHierarchy ? "" : " (выкл)")}");
            }
            return n == 0 ? "не нашёл «" + part + "»" : sb.ToString().TrimEnd();
        }

        private static string PathOf(Transform t)
        {
            string s = t.name;
            for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
            return t.parent != null ? "…/" + s : s;
        }

        private static string Move(string name, Vector3 to)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var t = all.FirstOrDefault(x => x.name == name)
                 ?? all.FirstOrDefault(x => x.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            if (t == null) return "не нашёл «" + name + "»";
            var legs = t.GetComponent<NavMeshAgent>();
            if (legs != null && legs.enabled && legs.isOnNavMesh) legs.Warp(to);
            else t.position = to;
            return $"{t.name} → {t.position:F1}";
        }

        // ── отражение ──

        private static object Member(string target, string value, bool set)
        {
            var (type, name, obj, flags) = Target(target);
            var f = type.GetField(name, flags);
            if (f != null)
            {
                if (set) f.SetValue(obj, As(value, f.FieldType));
                return f.GetValue(obj);
            }
            var p = type.GetProperty(name, flags);
            if (p != null)
            {
                if (set) p.SetValue(obj, As(value, p.PropertyType));
                return p.GetValue(obj);
            }
            throw new MissingMemberException($"у {type.Name} нет поля или свойства {name}");
        }

        private static object Call(string target, string[] args)
        {
            var (type, name, obj, flags) = Target(target);
            foreach (var m in type.GetMethods(flags).Where(m => m.Name == name && !m.ContainsGenericParameters))
            {
                var ps = m.GetParameters();
                if (args.Length > ps.Length || ps.Skip(args.Length).Any(p => !p.IsOptional)) continue;
                var vals = new object[ps.Length];
                try
                {
                    for (int i = 0; i < ps.Length; i++)
                        vals[i] = i < args.Length ? As(args[i], ps[i].ParameterType) : ps[i].DefaultValue;
                }
                catch { continue; }
                object r = m.Invoke(obj, vals);
                return m.ReturnType == typeof(void) ? "(готово)" : r;
            }
            throw new MissingMethodException($"нет подходящего {type.Name}.{name} на {args.Length} арг.");
        }

        private static (Type type, string name, object obj, BindingFlags flags) Target(string target)
        {
            bool inst = target.Contains("#");
            int cut = inst ? target.LastIndexOf('#') : target.LastIndexOf('.');
            if (cut <= 0) throw new ArgumentException("нужно Тип.Член или Тип#член: " + target);
            string typeName = target.Substring(0, cut);
            var type = FindType(typeName) ?? throw new TypeLoadException("нет типа " + typeName);
            object obj = null;
            if (inst)
            {
                obj = UnityEngine.Object.FindFirstObjectByType(type);
                if (obj == null) throw new MissingReferenceException("в сцене нет " + type.Name);
            }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy
                      | (inst ? BindingFlags.Instance : BindingFlags.Static);
            return (type, target.Substring(cut + 1), obj, flags);
        }

        private static Type FindType(string name)
        {
            Type loose = null;
            var asms = AppDomain.CurrentDomain.GetAssemblies()
                .OrderBy(x => x.GetName().Name.StartsWith("Assembly-CSharp") ? 0 : 1);
            foreach (var asm in asms)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t.FullName == name) return t;
                    if (loose == null && (t.Name == name || (t.FullName != null && t.FullName.EndsWith("." + name)))) loose = t;
                }
            }
            return loose;
        }

        private static object As(string s, Type t)
        {
            if (s == "null") return null;
            var u = Nullable.GetUnderlyingType(t) ?? t;
            if (u == typeof(string)) return s;
            if (u == typeof(Vector3)) return V(s);
            if (u.IsEnum) return Enum.Parse(u, s, true);
            if (u == typeof(bool)) return s == "1" || s == "да" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (u == typeof(float)) return F(s);
            return Convert.ChangeType(s, u, CultureInfo.InvariantCulture);
        }

        private static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static Vector3 V(string s)
        {
            var p = s.Split(',', ';');
            if (p.Length != 3) throw new FormatException("вектор пишется x,y,z (дроби через точку): " + s);
            return new Vector3(F(p[0]), F(p[1]), F(p[2]));
        }

        private static string Show(object o)
        {
            switch (o)
            {
                case null: return "null";
                case string s: return s;
                case Vector3 v: return v.ToString("F2");
                case UnityEngine.Object u:
                    if (!u) return "null (уничтожен)";
                    return $"{u.GetType().Name} «{u.name}»" + (u is Component c ? " " + c.transform.position.ToString("F1") : "");
                case IEnumerable e:
                    var items = e.Cast<object>().Take(30).Select(Show).ToList();
                    return $"[{items.Count}] " + string.Join("; ", items);
                default: return Convert.ToString(o, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Слова через пробел; "в кавычках" и «ёлочках» — одно слово.</summary>
        private static List<string> Split(string line)
        {
            var r = new List<string>();
            var sb = new StringBuilder();
            char close = '\0';
            foreach (char ch in line)
            {
                if (close != '\0')
                {
                    if (ch == close) close = '\0';
                    else sb.Append(ch);
                    continue;
                }
                if (ch == '"') { close = '"'; continue; }
                if (ch == '«') { close = '»'; continue; }
                if (char.IsWhiteSpace(ch))
                {
                    if (sb.Length > 0) { r.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(ch);
            }
            if (sb.Length > 0) r.Add(sb.ToString());
            return r;
        }
    }
}
#endif
