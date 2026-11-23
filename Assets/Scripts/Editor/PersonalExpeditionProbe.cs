// Assets/Scripts/Editor/PersonalExpeditionProbe.cs
// Личная экспедиция в настоящей Unity: Греховод сам идёт с отрядом на проходимую карту.
// Проверяет то, на чём стоит игра: карта построена, по ней ходят, приказ «напасть» доходит до воинов,
// их ум AOS решает, стража отвечает, есть урон и смерти. Путь — как у игрока: лагерь → склеп → карта.
//
// Верстак: open Assets/Scenes/Prologue_Camp.unity → play → call Sinbinder.EditorTools.PersonalExpeditionProbe.Begin
// Журнал: Logs/personal-expedition-probe.txt. Карта остаётся открытой для кадров; выход — ...PersonalExpeditionProbe.Leave
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class PersonalExpeditionProbe
    {
        private const string LogPath = "Logs/personal-expedition-probe.txt";
        private enum Step { Camp, Crypt, Enter, Look, Walk, Order, Fight, Goal, Gate, Back, Done }

        private static Step _step;
        private static double _since, _next;
        private static Vector3 _walkFrom, _walkTo;
        private static readonly List<Warrior> Party = new List<Warrior>();
        private static readonly List<Warrior> Guards = new List<Warrior>();

        public static string Result { get; private set; } = "не запускалась";
        /// <summary>Откуда и куда снимать карту сверху (для shot верстака).</summary>
        public static string Overview { get; private set; } = "";

        public static string Begin()
        {
            if (!EditorApplication.isPlaying) return "нужен режим игры (play)";
            File.WriteAllText(LogPath, "", new UTF8Encoding(false));
            Party.Clear(); Guards.Clear();
            _retreat = false;
            Result = "идёт";
            Next(Step.Camp);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Write($"=== ЛИЧНАЯ ЭКСПЕДИЦИЯ В UNITY, сцена {SceneManager.GetActiveScene().name}");
            return "запущена";
        }

        /// <summary>
        /// Второй вход в часовню после прогона: те ли стражи ждут (Survivors), с теми ли ранами,
        /// что сказано в журнале. Греховод ставится в 10 м от первого выжившего — узнать его.
        /// </summary>
        public static string Revisit()
        {
            if (!EditorApplication.isPlaying || CryptMap.Board == null) return "нужен склеп в игре";
            if (PersonalExpedition.Active) return "экспедиция уже идёт";
            var memory = new StringBuilder("в памяти: ");
            foreach (var s in Survivors.All)
                memory.Append($"{s.Name} при {s.Place}, {s.Health:P0}, встреч {s.Meetings}{(string.IsNullOrEmpty(s.Killed) ? "" : ", убил " + s.Killed)}{(s.Fled ? ", бежал" : "")}; ");
            if (Survivors.All.Count == 0) memory.Append("никого");
            Write("=== ВТОРОЙ ВХОД: " + memory);
            Mission chapel = default; bool found = false;
            foreach (var m in CryptMap.Board.Missions) if (m.Name == "Придорожная часовня") { chapel = m; found = true; }
            if (!found) return "нет часовни";
            var names = new List<string>();
            foreach (var m in SquadRoster.Members) if (names.Count < chapel.Squad - 1 && SquadRoster.CanGo(m)) names.Add(m.Name);
            if (!PersonalExpedition.Enter(chapel, names, out var error)) { Write("  вход отказан: " + error); return "вход отказан: " + error; }
            Warrior first = null;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !w.IsDead && w.Team == Team.Enemy)
                {
                    Write($"  страж {w.DisplayName}: {w.HP:F0}/{w.MaxHP:F0}, грех {w.Soul?.Sin}");
                    if (first == null) first = w;
                }
            if (first != null && SinbinderPlayer.Exists)
            {
                var hero = SinbinderPlayer.Instance;
                // 13 м: в шаге от узнавания (14 м), но не в бою. Кадр — сразу, и сразу Leave():
                // 23.11 Греховод, оставленный в 10 м на время разговора в склепе, пал.
                var at = first.transform.position + (hero.transform.position - first.transform.position).normalized * 13f;
                var agent = hero.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) agent.Warp(at); else hero.transform.position = at;
                var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                if (cam != null)
                {
                    var to = first.transform.position - at; to.y = 0f;
                    cam.Yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; cam.Pitch = 0f;
                }
                Write($"  Греховод поставлен в 13 м от {first.DisplayName}, лицом к нему");
            }
            return "второй вход: " + memory;
        }

        public static string Leave()
        {
            if (!PersonalExpedition.Active) return "экспедиции нет";
            Object.FindFirstObjectByType<PersonalExpedition>()?.Exit();
            Write($"выход: экспедиция {(PersonalExpedition.Active ? "ВСЁ ЕЩЁ идёт" : "закрыта")}, сцена {SceneManager.GetActiveScene().name}; "
                + "отряд: " + string.Join(", ", Party.ConvertAll(w => w == null ? "—" : $"{w.DisplayName} {(w.IsDead ? "мёртв" : $"{w.HP:F0}/{w.MaxHP:F0}")} у {Flat(w.transform.position)}")));
            return "вышли";
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish("НЕ ПРОЙДЕНО: игра остановлена посреди проверки"); return; }
            double now = EditorApplication.timeSinceStartup, spent = now - _since;
            DismissSalary(spent);
            var hero = SinbinderPlayer.Exists ? SinbinderPlayer.Instance : null;
            if (AnswerLesson(now, hero)) return;
            switch (_step)
            {
                case Step.Camp:
                    // Стартовое окно ждёт ответа, как ждёт человека (прогон демо отвечает так же).
                    // 20.11 без ответа окно держало экран чёрным до конца проверки.
                    if (UI.StartPanel.Waiting) { UI.StartPanel.ChooseFresh(); Write("стартовое окно: «начать заново»"); _since = now; return; }
                    if (spent < 3) return;
                    if (!SquadRoster.HasSquad) { if (spent > 90) Finish("НЕ ПРОЙДЕНО: отряд не завёлся"); return; }
                    if (SceneManager.GetActiveScene().name != "Crypt_Entrance") SceneManager.LoadScene("Crypt_Entrance");
                    Next(Step.Crypt);
                    return;

                case Step.Crypt:
                    if (SceneManager.GetActiveScene().name != "Crypt_Entrance" || CryptMap.Board == null || hero == null)
                    { if (spent > 90) Finish("НЕ ПРОЙДЕНО: склеп, доска или Греховод не появились за 90 с"); return; }
                    // Как игрок: сперва встреча в склепе (Карган о потерях) и плата отряду,
                    // потом карта. 20.11 вход через 4 с — и встреча с окном платы догнали Греховода на карте.
                    if (CryptArrival.Running || !UI.SalaryPanelUI.Answered)
                    {
                        if (UI.Letterbox.CanSkip && !UI.Letterbox.Skipping) UI.Letterbox.Instance.SkipAll();
                        if (spent > 120) { Finish("НЕ ПРОЙДЕНО: встреча в склепе не кончилась за 120 с"); return; }
                        return;
                    }
                    if (spent < 2) return;
                    Write($"склеп: встреча кончилась, плата отвечена ({spent:F0} с)");
                    Next(Step.Enter);
                    return;

                case Step.Enter:
                {
                    Mission chapel = default; bool found = false;
                    foreach (var m in CryptMap.Board.Missions) if (m.Name == "Придорожная часовня") { chapel = m; found = true; }
                    if (!found) { Finish("НЕ ПРОЙДЕНО: в каталоге нет «Придорожной часовни»"); return; }
                    var names = new List<string>();
                    foreach (var m in SquadRoster.Members)
                        if (names.Count < chapel.Squad - 1 && SquadRoster.CanGo(m)) names.Add(m.Name);
                    Write($"вход: «{chapel.Name}», стража {chapel.Guards}, с Греховодом идут {string.Join(", ", names)}");
                    bool ok = PersonalExpedition.Enter(chapel, names, out var error);
                    Write($"  PersonalExpedition.Enter → {(ok ? "да" : "НЕТ: " + error)}");
                    if (!ok) { Finish("НЕ ПРОЙДЕНО: вход в личную экспедицию отказан"); return; }
                    foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                        if (w != null && w.Team == Team.Player && !(w is SinbinderPlayer) && names.Contains(w.DisplayName)) Party.Add(w);
                    Next(Step.Look);
                    return;
                }

                case Step.Look:
                {
                    if (spent < 2.5) return;
                    Guards.Clear();
                    foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                        if (w != null && !w.IsDead && w.Team == Team.Enemy) Guards.Add(w);
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    bool onMesh = NavMesh.SamplePosition(hero.transform.position, out var hit, 1f, NavMesh.AllAreas);
                    Write($"на карте: Греховод у {Flat(hero.transform.position)}, на навмеше {(onMesh ? "да" : "НЕТ")}, "
                        + $"вид {(cam == null ? "камеры нет" : cam.FirstPersonNow ? "глазами" : cam.WarriorModeNow ? "Воин" : "сверху")}");
                    foreach (var w in Party)
                        Write($"  свой {w.DisplayName}: {w.HP:F0}/{w.MaxHP:F0}, у {Flat(w.transform.position)}, ум AOS {(w.GetComponent<AOS.AOSWarriorWrapper>() != null ? "есть" : "НЕТ")}, "
                            + $"ноги {(w.GetComponent<UnitMover>() != null ? "есть" : "НЕТ")}");
                    foreach (var g in Guards)
                        Write($"  страж {g.DisplayName}: {g.Shell}, {g.HP:F0}/{g.MaxHP:F0}, у {Flat(g.transform.position)}, ум AOS {(g.GetComponent<AOS.AOSWarriorWrapper>() != null ? "есть" : "НЕТ")}");
                    if (Guards.Count == 0) { Finish("НЕ ПРОЙДЕНО: на карте нет стражи"); return; }
                    var mid = Vector3.zero; foreach (var g in Guards) mid += g.transform.position; mid /= Guards.Count;
                    var c = (mid + hero.transform.position) * 0.5f;
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    Overview = string.Format(inv, "{0:F1},{1:F1},{2:F1} {0:F1},{3:F1},{4:F1}", c.x, c.y + 38f, c.z - 22f, c.y, c.z);
                    Write("  кадр сверху: shot <имя> " + Overview);
                    // Ход — как ПКМ по земле в виде сверху: Греховод идёт к страже и встаёт в 12 м.
                    _walkFrom = hero.transform.position;
                    // 24 м, а не 12: 20.11 без Каргана (он больше не перелетает на чужую вылазку)
                    // двое скелетов окружили стоящего в 12 м Греховода — «Греховод пал», конец игры.
                    _walkTo = Vector3.MoveTowards(mid, hero.transform.position, 24f);
                    var legs = hero.GetComponent<UnitMover>();
                    if (legs == null) { Finish("НЕ ПРОЙДЕНО: у Греховода нет ног (UnitMover)"); return; }
                    legs.CommandMove(_walkTo);
                    foreach (var w in Party) w.IssueCommand(CommandKind.Move, _walkTo + (w.transform.position - hero.transform.position) * 0.3f);
                    Next(Step.Walk);
                    return;
                }

                case Step.Walk:
                {
                    if (spent < 10) return;
                    float before = Vector3.Distance(_walkFrom, _walkTo), left = Vector3.Distance(hero.transform.position, _walkTo);
                    Write($"ход: Греховод прошёл {before - left:F1} м из {before:F1}, до точки {left:F1} м");
                    foreach (var w in Party) Write($"  {w.DisplayName}: до точки {Vector3.Distance(w.transform.position, _walkTo):F1} м");
                    // Приказ — как ПКМ по врагу: каждому свой ближний страж.
                    foreach (var w in Party)
                    {
                        Warrior target = null;
                        foreach (var g in Guards) if (g != null && !g.IsDead && (target == null
                            || (g.transform.position - w.transform.position).sqrMagnitude < (target.transform.position - w.transform.position).sqrMagnitude)) target = g;
                        if (target == null) continue;
                        w.IssueCommand(CommandKind.Attack, target.transform.position, target.gameObject);
                        Write($"приказ: {w.DisplayName} → напасть на {target.DisplayName} ({Vector3.Distance(w.transform.position, target.transform.position):F1} м)");
                    }
                    _fightFrom = Time.time;
                    Next(Step.Fight);
                    return;
                }

                case Step.Fight:
                {
                    if (now < _next) return;
                    _next = now + 5;
                    float game = Time.time - _fightFrom;
                    // Свой страж пал — приказ исполнен и снят. Как игрок: новый приказ на ближнего живого.
                    foreach (var w in Party)
                    {
                        if (w == null || w.IsDead || w.HasCommand) continue;
                        Warrior next = null;
                        foreach (var g in Guards) if (g != null && !g.IsDead && (next == null
                            || (g.transform.position - w.transform.position).sqrMagnitude < (next.transform.position - w.transform.position).sqrMagnitude)) next = g;
                        if (next == null) continue;
                        w.IssueCommand(CommandKind.Attack, next.transform.position, next.gameObject);
                        Write($"новый приказ: {w.DisplayName} → напасть на {next.DisplayName} ({Vector3.Distance(w.transform.position, next.transform.position):F1} м)");
                    }
                    var sb = new StringBuilder($"+{game:F0} с игры ");
                    foreach (var w in Party)
                    {
                        if (w != null && !w.IsDead && w.HasCommand && w.Command.Target != null)
                        {
                            var legs = w.GetComponent<NavMeshAgent>();
                            sb.Append($"| {w.DisplayName} до цели {Vector3.Distance(w.transform.position, w.Command.Target.transform.position):F1} м, ход {(legs != null && legs.enabled ? legs.velocity.magnitude : 0f):F1} м/с ");
                        }
                        var mind = w.GetComponent<AOS.AOSWarriorWrapper>();
                        string said = "—";
                        if (mind != null)
                        {
                            var d = mind.LastDecisionDetail;
                            said = $"{d.Action}{(d.RefusedCommand ? " (ОТКАЗ)" : d.Hesitated ? " (колеблется)" : "")}";
                        }
                        sb.Append($"| {w.DisplayName} {(w.IsDead ? "МЁРТВ" : $"{w.HP:F0}")}, решил {said}, приказ {(w.HasCommand ? w.Command.Kind.ToString() : "нет")} ");
                    }
                    sb.Append("|| стража: ");
                    foreach (var g in Guards) sb.Append(g == null ? "— " : g.IsDead ? "пал; " : $"{g.HP:F0}/{g.MaxHP:F0}; ");
                    Write(sb.ToString());
                    if (hero == null || hero.IsDead) { Finish("НЕ ПРОЙДЕНО: Греховод пал в бою"); return; }
                    bool guardsDown = Guards.TrueForAll(g => g == null || g.IsDead);
                    bool partyDown = Party.TrueForAll(w => w == null || w.IsDead);
                    // Страж у самого Греховода. 20.11 и 23.11 сбежавший от Вейна скелет
                    // натыкался на стоящего столбом Греховода и убивал его — игрок бы ушёл.
                    Warrior close = null;
                    foreach (var g in Guards)
                        if (g != null && !g.IsDead && Flat(g.transform.position, hero.transform.position) < 7f) close = g;
                    if (guardsDown || partyDown || close != null || game > 150f || spent > 500)
                    {
                        int hit = 0; foreach (var g in Guards) if (g != null && (g.IsDead || g.HP < g.MaxHP - 0.01f)) hit++;
                        int hurt = 0; foreach (var w in Party) if (w != null && (w.IsDead || w.HP < w.MaxHP - 0.01f)) hurt++;
                        string why = guardsDown ? "стража пала" : partyDown ? "отряд пал"
                            : close != null ? $"{close.DisplayName} в {Flat(close.transform.position, hero.transform.position):F1} м от Греховода"
                            : "за 150 с игры бой не кончился";
                        string fight = $"{why} ({game:F0} с игры); задеты стражей {hit} из {Guards.Count}, своих {hurt} из {Party.Count}";
                        if (!guardsDown)
                        {
                            // Как игрок, когда бой проигран: назад к бирюзовому знаку и F — вернуться.
                            // Так проверяется и выход из вылазки без победы.
                            Write("бой: " + fight + " — отступаю к знаку, как игрок");
                            _retreat = true;
                            hero.GetComponent<UnitMover>().CommandMove(Vector3.MoveTowards(Spot("Gate"), hero.transform.position, 1.5f));
                            Next(Step.Gate);
                            return;
                        }
                        Write("бой: " + fight);
                        // Цель — как игрок: дойти к янтарному огню и нажать F.
                        hero.GetComponent<UnitMover>().CommandMove(Vector3.MoveTowards(Spot("Objective"), hero.transform.position, 1.5f));
                        Next(Step.Goal);
                    }
                    return;
                }

                case Step.Goal:
                case Step.Gate:
                {
                    var spot = Spot(_step == Step.Goal ? "Objective" : "Gate");
                    float left = Flat(hero.transform.position, spot);
                    if (now >= _next) { _next = now + 10; Write($"  +{spent:F0} с: до точки {left:F1} м{(_wasd ? " (шагом)" : "")}. " + Route().Replace("\n", " | ")); }
                    var agent = hero.GetComponent<NavMeshAgent>();
                    // Урок жатвы уводит Греховода к душе (мир стоит, пока не соберёт).
                    // После урока игрок снова щёлкает к своей точке — так и здесь.
                    // 23.11 без этого Греховод стоял у души в 58 м от знака.
                    if (!_wasd && left > 3.5f && agent != null && agent.isOnNavMesh && Flat(agent.destination, spot) > 3f)
                    {
                        hero.GetComponent<UnitMover>().CommandMove(Vector3.MoveTowards(spot, hero.transform.position, 1.5f));
                        Write($"  снова к {(_step == Step.Goal ? "цели" : "знаку")}: приказ стоял не туда (после урока)");
                        _since = now;
                    }
                    // Приказ мышью встал — дальше так, как ходит игрок глазами: W шагает agent.Move (PlayerWalk).
                    if (left > 3.5f && spent > 8 && !_wasd && agent != null && agent.velocity.sqrMagnitude < 0.01f)
                    { _wasd = true; Write($"  приказ мышью встал в {left:F1} м от точки — дальше шаг как с клавиш (agent.Move, как PlayerWalk)"); }
                    if (_wasd && agent != null && agent.isOnNavMesh && left > 3.5f)
                    {
                        var path = new NavMeshPath();
                        NavMesh.CalculatePath(hero.transform.position, spot, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }, path);
                        var corner = path.corners.Length > 1 ? path.corners[1] : spot;
                        var dir = corner - hero.transform.position; dir.y = 0f;
                        if (dir.sqrMagnitude < 0.01f) { dir = spot - hero.transform.position; dir.y = 0f; }
                        agent.Move(dir.normalized * 3.5f * (float)System.Math.Min(0.1, now - _lastTick));
                    }
                    _lastTick = now;
                    if (left > 3.5f && spent < 90) return;
                    Write($"{(_step == Step.Goal ? "цель" : "знак возврата")}: дошёл за {spent:F0} с, до точки {left:F1} м; F предлагает: "
                        + string.Join(" / ", Offers().ConvertAll(o => o.label)));
                    var want = _step == Step.Goal ? "исследовать" : "вернуться";
                    var act = Offers().Find(o => o.label.Contains(want)).act;
                    if (act == null) { Finish($"НЕ ПРОЙДЕНО: у {(_step == Step.Goal ? "цели" : "знака")} нет «F — {want}»"); return; }
                    act();
                    if (_step == Step.Goal)
                    {
                        Write("  после F: " + Notice());
                        hero.GetComponent<UnitMover>().CommandMove(Vector3.MoveTowards(Spot("Gate"), hero.transform.position, 1.5f));
                        Next(Step.Gate);
                    }
                    else Next(Step.Back);
                    return;
                }

                case Step.Back:
                {
                    if (spent < 3) return;
                    Write($"после возврата: экспедиция {(PersonalExpedition.Active ? "ВСЁ ЕЩЁ идёт" : "закрыта")}, сцена {SceneManager.GetActiveScene().name}, Греховод у {Flat(hero.transform.position)}");
                    foreach (var w in Party)
                    {
                        string roster = SquadRoster.TryGet(w.DisplayName, out var m) ? $"{m.HealthFraction:P0}" : "нет в отряде";
                        Write($"  {w.DisplayName}: у {Flat(w.transform.position)}, {(w.IsDead ? "мёртв" : $"{w.HP:F0}/{w.MaxHP:F0}")}, в списке отряда {roster}");
                    }
                    var inv = Inventory.PlayerInventory.Instance;
                    bool letter = false;
                    if (inv != null) foreach (var it in inv.GetAllItems()) if (it != null && it.Name.Contains("письмо")) letter = true;
                    Finish($"{(PersonalExpedition.Active ? "НЕ ПРОЙДЕНО: из экспедиции не вышли" : hero == null || hero.IsDead ? "НЕ ПРОЙДЕНО: Греховод пал" : _retreat ? "ПРОЙДЕНО С ОТСТУПЛЕНИЕМ: вошли, дошли, приказали, бой не выигран — ушли к знаку и вернулись" : "ПРОЙДЕНО: вошли, дошли, приказали, стража пала, цель взята, вернулись")}"
                         + $"; письмо в суме {(letter ? "есть" : "НЕТ")}");
                    return;
                }
            }
        }

        private static float _fightFrom;
        private static bool _retreat;
        private static string _lessonSeen;
        private static double _lessonNext;

        /// <summary>
        /// Урок стоп-кадром (мир стоит, ждёт игрока) — ответ, как у прогона демо
        /// (DemoWalkthrough): отказ — навести подсказку, спина — послать обходящего
        /// за спину врага, прочие — Греховод идёт по кольцу. 20.11: стража стала видна,
        /// и урок «спина» остановил бой — проверка стояла, пока мир ждал щелчка.
        /// </summary>
        private static bool AnswerLesson(double now, SinbinderPlayer hero)
        {
            if (!Lesson.Holding) { _lessonSeen = null; return false; }
            string kind = Lesson.Now.ToString();
            if (kind != _lessonSeen) { _lessonSeen = kind; Write($"урок «{kind}»: мир стоит и ждёт игрока — отвечаю, как прогон демо"); }
            if (now < _lessonNext) return true;
            _lessonNext = now + 0.5;
            if (Lesson.Now == LessonKind.Refusal) { UI.WarriorTooltipUI.Pinned = Lesson.Partner; return true; }
            if (Lesson.Now == LessonKind.Backstab)
            {
                var flanker = Lesson.Flanker;
                if (flanker != null) flanker.IssueCommand(CommandKind.Move, Lesson.Mark);
                return true;
            }
            hero?.GetComponent<UnitMover>()?.CommandMove(Lesson.Target);
            return true;
        }

        private static void DismissSalary(double spent)
        {
            // Суд души над отказом ставит мир на паузу — как игрок, жмём Esc «пропустить».
            if (SoulCourt.Sitting && UI.Letterbox.CanSkip && !UI.Letterbox.Skipping)
            {
                UI.Letterbox.Instance.SkipAll();
                Write("суд души поставил игру на паузу — проверка пропустила его (Esc)");
            }
            var salary = Object.FindFirstObjectByType<UI.SalaryPanelUI>();
            if (salary == null || !UI.Modal.Any || Core.GamePauseController.Instance == null || !Core.GamePauseController.Instance.IsPaused) return;
            typeof(UI.SalaryPanelUI).GetMethod("Withhold", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(salary, null);
            Write("окно платы отряду поставило игру на паузу — проверка выбрала «Не платить никому»");
        }

        private static string Flat(Vector3 p) => $"({p.x:F1}; {p.z:F1})";
        private static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        private const System.Reflection.BindingFlags Hidden = System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
        private static object Live => typeof(PersonalExpedition).GetField("_active", Hidden)?.GetValue(null);

        /// <summary>Точки карты (цель, знак возврата) — те же, по которым игра решает, что предложить на F.</summary>
        private static Vector3 Spot(string name)
        {
            var live = Live;
            var p = typeof(PersonalExpedition).GetProperty(name, Hidden);
            return live != null && p != null ? (Vector3)p.GetValue(live) : Vector3.zero;
        }

        /// <summary>Путь по навмешу от Греховода к цели и к знаку возврата — для верстака (call ...Route).</summary>
        public static string Route()
        {
            if (!SinbinderPlayer.Exists) return "Греховода нет";
            var hero = SinbinderPlayer.Instance;
            var legs = hero.GetComponent<NavMeshAgent>();
            var sb = new StringBuilder($"Греховод у {hero.transform.position}, агент {(legs == null ? "нет" : legs.enabled ? (legs.isOnNavMesh ? "на сетке" : "НЕ на сетке") : "выключен")}"
                + (legs != null && legs.enabled && legs.isOnNavMesh ? $", цель агента {legs.destination}, остановлен {legs.isStopped}, путь {legs.pathStatus}" : ""));
            // Препятствия рядом: тела, мешки, стены — кто режет сетку вокруг Греховода.
            foreach (var o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None))
                if (o != null && o.isActiveAndEnabled && Flat(o.transform.position, hero.transform.position) < 6f)
                    sb.Append($"\nпрепятствие «{o.name}» в {Flat(o.transform.position, hero.transform.position):F1} м, вырезает {o.carving}, размер {o.size}, радиус {o.radius}");
            var filter = new NavMeshQueryFilter { agentTypeID = legs != null ? legs.agentTypeID : 0, areaMask = legs != null ? legs.areaMask : NavMesh.AllAreas };
            foreach (var name in new[] { "Objective", "Gate" })
            {
                var to = Spot(name);
                bool near = NavMesh.SamplePosition(to, out var hit, 3f, filter);
                var path = new NavMeshPath();
                NavMesh.CalculatePath(hero.transform.position, near ? hit.position : to, filter, path);
                var end = path.corners.Length > 0 ? path.corners[path.corners.Length - 1] : hero.transform.position;
                sb.Append($"\n{name} {to}: сетка рядом {(near ? $"да ({Flat(hit.position, to):F1} м)" : "НЕТ")}, путь {path.status}, углов {path.corners.Length}, конец пути в {Flat(end, to):F1} м от точки");
            }
            return sb.ToString();
        }

        private static string Notice() => typeof(PersonalExpedition).GetField("_notice", Hidden)?.GetValue(Live) as string ?? "—";

        /// <summary>Что игра сейчас предлагает на F — через тот же общий выбор, что и у игрока.</summary>
        private static List<(string label, System.Action act)> Offers()
        {
            var list = new List<(string label, System.Action act)>();
            PersonalExpedition.OfferInteractions((gaze, label, act) => list.Add((label, act)));
            return list;
        }
        private static bool _wasd;
        private static double _lastTick;
        private static void Next(Step s) { _step = s; _since = _lastTick = EditorApplication.timeSinceStartup; _next = 0; _wasd = false; }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            _step = Step.Done;
            Result = result;
            Write("=== " + result);
        }

        private static void Write(string line)
        {
            Debug.Log("[ЛИЧНАЯ] " + line);
            File.AppendAllText(LogPath, line + "\n", new UTF8Encoding(false));
        }
    }
}
