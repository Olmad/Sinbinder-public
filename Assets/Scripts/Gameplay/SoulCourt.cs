// Assets/Scripts/Gameplay/SoulCourt.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Суд души: решение воина, показанное изнутри. Автор, 05.11: «при отказе —
    /// написать „произошёл отказ“, камера влетает в голову воину, и там
    /// в темноте стол и судьи (тьма в плащах), и происходит объяснение»;
    /// судьи — по облику Греховода (<see cref="SinbinderPlayer.Judge"/>).
    ///
    /// <b>Здесь ничего не придумано.</b> Судьи за столом — голоса движка
    /// (<see cref="BehaviourResolver.Ballot"/>), те, что в этом решении сказали
    /// громче прочих. Огонь перед судьёй — сила его голоса; за приказ он или
    /// против — по тому, чему отдал больше. Приговор — само решение, а его
    /// причина — пересчёт «от противного» (<see cref="Counterfactual"/>): то,
    /// без чего воин поступил бы иначе. Другая душа — другие судьи и другой
    /// приговор (автор: «игра должна показать, что она не скриптовая»;
    /// «разные души повышают заметность AOS при переигровке»).
    ///
    /// Зал строится на время суда глубоко под сценой и уходит вместе с ним;
    /// свет мира на это время гаснет, мир стоит на паузе.
    /// </summary>
    public class SoulCourt : MonoBehaviour
    {
        /// <summary>Идёт суд — для прогона и для подсказок, которые прячутся под ним.</summary>
        public static bool Sitting { get; private set; }

        /// <summary>Шаг суда — для прогона: enter, hall, compass, judge, stance, verdict, exit.</summary>
        public static string Step { get; private set; } = "";

        /// <summary>Сколько судей за столом в этот раз.</summary>
        public static int Seated { get; private set; }

        /// <summary>Чем кончился суд: refused, hesitated, obeyed.</summary>
        public static string Outcome { get; private set; }

        /// <summary>Судьи последнего суда словами — для отчёта прогона.</summary>
        public static string Bench { get; private set; } = "";

        /// <summary>Сколько реплик суд сказал с запуска — прогон снимает кадр на каждую.</summary>
        public static int Spoken { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Sitting = false; Step = ""; Seated = 0; Outcome = null; Bench = ""; Spoken = 0; }

        /// <summary>Где стоит зал: под сценой, куда не смотрит ни одна камера.</summary>
        private static readonly Vector3 Hall = new Vector3(0f, -400f, 0f);

        /// <summary>
        /// Судей не больше шести: каждый представляется и говорит, и суд дольше
        /// полуминуты уже не урок, а ожидание. Света на предмет у URP четыре —
        /// судью освещает ближняя к нему свеча, её и хватает.
        /// </summary>
        private const int MaxJudges = 6;

        /// <summary>Тише этого голос в суде не слышен — его и не сажаем.</summary>
        private const float Audible = 2f;

        private const float TableTop = 0.86f;
        private const float Ring = 1.95f;

        /// <summary>Общий план: стол внизу, судьи посередине, компас над ними.</summary>
        private static readonly Vector3 OverviewFrom = new Vector3(0f, 2.8f, -5.0f);
        private static readonly Vector3 OverviewLook = new Vector3(0f, 1.8f, 1.4f);

        /// <summary>
        /// Моральный компас — за судьями, как часы на стене (автор, 05.11:
        /// «за судьями должно быть подобие часов со стрелкой, но только это
        /// моральный компас»). Слева — «за приказ» (там и сидят такие судьи),
        /// справа — «против», вверху — вровень. Стрелка — сумма сказанного.
        /// </summary>
        private static readonly Vector3 DialAt = new Vector3(0f, 2.35f, 3.6f);
        private const float MaxTilt = 70f;
        private static readonly Color ForOrderTone = new Color(0.95f, 0.76f, 0.30f);
        private static readonly Color AgainstTone = new Color(0.82f, 0.22f, 0.20f);

        // Стрелка живёт на зале: у неё вес — идёт к цели, чуть проскакивает, успокаивается.
        private Transform _needle;
        private float _angle, _swing, _aim;

        private void Update()
        {
            if (_needle == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _swing += ((_aim - _angle) * 28f - _swing * 6.5f) * dt;
            _angle += _swing * dt;
            _needle.localRotation = Quaternion.AngleAxis(_angle, Vector3.forward);
        }

        /// <summary>Наклон стрелки: плюс — влево, к «за приказ»; минус — вправо, к «против».</summary>
        private static float Tilt(float against, float reach) => -MaxTilt * Mathf.Clamp(against / reach, -1f, 1f);

        private sealed class Judge
        {
            public string Module;
            public float Obey;
            public float Rival;
            public float Net => Rival - Obey;
            public bool ForOrder => Net < 0f;
            public string Title;
            public Vector3 Seat;
            public Light Fire;
            public Transform Flame;
            public MonoBehaviour Voice;
        }

        /// <summary>
        /// Реплика суда: строка на полосе и голос говорящего (тот же писк, что
        /// у Каргана на наезде, — <see cref="Audio.VoiceGenerator"/>). Щелчок —
        /// дальше (<see cref="UI.Letterbox.Hold"/>).
        /// </summary>
        private static IEnumerator Line(UI.Letterbox box, MonoBehaviour speaker, string name, string words)
        {
            box.Say(name, words);
            Spoken++;
            Debug.Log($"[СУД] {name}: {words}");
            if (speaker != null && speaker.TryGetComponent<Audio.VoiceGenerator>(out var voice))
                voice.Babble(words, Mathf.Min(Herald.Hold(words) - 0.4f, words.Length / 40f * 1.2f + 0.3f));
            yield return UI.Letterbox.Hold(Herald.Hold(words) + 0.5f);
        }

        /// <summary>Голос судьи: ниже человеческого, у каждого свой.</summary>
        private static MonoBehaviour Throat(GameObject who, float pitch)
        {
            var voice = who.AddComponent<Audio.VoiceGenerator>();
            var source = who.GetComponent<AudioSource>();
            if (source != null) { source.pitch = pitch; source.spatialBlend = 0f; }
            return voice;
        }

        /// <summary>
        /// Кто он такой — одной строкой. Имя — то, что видно на панели воина
        /// (у щедрого голосует щедрость, а не жадность), роль — по стороне шкалы.
        /// </summary>
        private static string Introduce(Judge j, Warrior w)
        {
            bool virtue = w.Soul != null && IsVirtue(j.Module, w.Soul);
            switch (j.Module)
            {
                case "Loyalty":  return Loc.F("Я — {0}. Я слышу владыку.", j.Title);
                case "Memory":   return Loc.F("Я — {0}. Я помню всё, что было.", j.Title);
                case "Fear":     return Loc.F("Я — {0}. Я берегу жизнь.", j.Title);
                case "Morality": return Loc.F("Я — {0}. Я знаю, что хорошо и что дурно.", j.Title);
                case "Virtue":   return Loc.F("Я — {0}. Я не прохожу мимо чужой беды.", j.Title);
                case "Patience": return Loc.F("Я — {0}. Я умею ждать.", j.Title);
                case "Wrath":    return virtue ? Loc.F("Я — {0}. Я гашу вспышки.", j.Title) : Loc.F("Я — {0}. Я не прощаю обид.", j.Title);
                case "Pride":    return virtue ? Loc.F("Я — {0}. Я знаю своё место.", j.Title) : Loc.F("Я — {0}. Я не склоняю головы.", j.Title);
                case "Greed":    return virtue ? Loc.F("Я — {0}. Я отдаю, не считая.", j.Title) : Loc.F("Я — {0}. Я считаю, что нам достанется.", j.Title);
                case "Envy":     return virtue ? Loc.F("Я — {0}. Я радуюсь чужому.", j.Title) : Loc.F("Я — {0}. Я вижу, у кого больше.", j.Title);
                case "Lust":     return virtue ? Loc.F("Я — {0}. Я никого не держу.", j.Title) : Loc.F("Я — {0}. Я держусь за тех, кто рядом.", j.Title);
                case "Gluttony": return virtue ? Loc.F("Я — {0}. Мне хватает малого.", j.Title) : Loc.F("Я — {0}. Мне всегда мало.", j.Title);
                case "Sloth":    return virtue ? Loc.F("Я — {0}. Я не бросаю дела.", j.Title) : Loc.F("Я — {0}. Я берегу силы.", j.Title);
                default:         return Loc.F("Я — {0}.", j.Title);
            }
        }

        // ──────────────────────────────────
        // Суд
        // ──────────────────────────────────

        /// <summary>
        /// Провести суд над решением <paramref name="d"/>, принятым в положении
        /// <paramref name="c"/>. Зовёт тот, кто ведёт миг (сон), — и ждёт конца:
        /// мир стоит, пока суд идёт.
        /// </summary>
        public static IEnumerator Convene(Warrior w, Decision d, DecisionContext c)
        {
            if (w == null || c == null || Sitting) yield break;

            var camera = Dialogue.DialogueCameraController.Instance;
            var box = UI.Letterbox.Instance;
            var eye = Camera.main;
            if (camera == null || box == null || eye == null) yield break;

            Sitting = true;
            Step = "enter";

            // Чужой наезд (перебранка перед боем) должен кончиться сам: уходя,
            // он возвращает камеру в мир — прогон 05.11 так выбросило из зала.
            yield return camera.WaitUntilFree(15f);

            var pause = GamePauseController.Instance;
            int mine = -1;
            if (pause != null && !pause.IsPaused) { pause.Pause(); mine = pause.Stamp; }

            // Причина «от противного» — даже если в этой сцене её не взвешивали.
            if (!d.Weighed && (d.RefusedCommand || (d.Hesitated && c.HasCommand)))
                new BehaviourResolver().Weigh(w, c, ref d);

            Outcome = d.RefusedCommand ? "refused" : d.Hesitated ? "hesitated" : "obeyed";
            var rival = Rival(d);
            var judges = Seat(w, c, rival, out float total, out var silent);
            Seated = judges.Count;
            Bench = string.Join(", ", judges.ConvertAll(j => $"{j.Module} {(j.ForOrder ? "+" : "-")} {Mathf.Abs(j.Net):F0}"));
            Debug.Log($"[СУД] {w.DisplayName}: {Outcome}; против приказа — {rival}; судьи: {Bench}; молчат: {string.Join(", ", silent)}");

            var hall = Build(judges, silent, out var host, out var orderFire, out var orderFlame);

            // 1. Отказ — на поле, лицом к воину.
            camera.SaveCameraPosition();
            yield return camera.FocusOn(w.transform);
            box.Say(null, Headline(w, d));
            Spoken++;
            yield return UI.Letterbox.Hold(2.8f);

            // 2. Влёт в голову: к лицу — и в темноту.
            var fader = Fader.Make(box);
            Vector3 head = Head(w);
            Vector3 back = eye.transform.position - head;
            back = back.sqrMagnitude < 0.01f ? -w.transform.forward : back.normalized;
            host.StartCoroutine(fader.To(1f, 0.75f));
            yield return camera.FocusOnPoint(head, head + back * 0.22f, null, push: 0.6f);
            yield return fader.To(1f, 0.2f);

            // 3. Зал: свет мира гаснет, камера над столом.
            Step = "hall";
            var world = Darkness.Fall(eye);
            Vector3 overview = Hall + OverviewFrom, look = Hall + OverviewLook;
            host.StartCoroutine(camera.FocusOnPoint(look, overview, null, push: 20f));
            yield return new WaitForSecondsRealtime(0.35f);
            yield return fader.To(0f, 0.9f);

            // Суд показывается один раз за игру (автор, 05.11) — и потому объясняет
            // себя сам: что за стол, кто за ним, что значит огонь и стрелка.
            // Молчащие тоже в зале — в тени (автор, 05.11: «судей всего 5? у нас
            // только грехов 7»): голосов у души много, говорят те, кого задел вопрос.
            yield return Line(box, host, Loc.T("Суд души"), Grammar.Pick(w.Gender,
                Loc.T("Это суд его души. Голосов в нём много, но за стол сели те, кого задел этот приказ. Огонь перед каждым — сила голоса; кто молчит — стоит в тени."),
                Loc.T("Это суд её души. Голосов в ней много, но за стол сели те, кого задел этот приказ. Огонь перед каждым — сила голоса; кто молчит — стоит в тени.")));

            Step = "compass";
            host.StartCoroutine(camera.FocusOnPoint(Hall + DialAt, Hall + new Vector3(0f, 2.5f, 0.4f), null, push: 12f));
            yield return Line(box, host, Loc.T("Суд души"), Grammar.Pick(w.Gender,
                Loc.T("Позади — его моральный компас. Слева — голоса за приказ, справа — против. Куда склонится стрелка, так он и поступит."),
                Loc.T("Позади — её моральный компас. Слева — голоса за приказ, справа — против. Куда склонится стрелка, так она и поступит.")));

            Step = "hall";
            host.StartCoroutine(camera.FocusOnPoint(look, overview, null, push: 20f));
            yield return Line(box, host, Loc.T("Суд души"),
                Loc.F("На столе — приказ владыки: «{0}».", OrderWords(w.Command.Kind)));

            // 4. Судьи — каждый представится и скажет своё (автор: «познакомить
            // с каждым, чтобы каждый сказал своё мнение»). Сперва за приказ, потом
            // против; громче всех — последним. Каждое «за» и «против» качает стрелку.
            judges.Sort((a, b) => a.ForOrder != b.ForOrder ? (a.ForOrder ? -1 : 1)
                                                            : Mathf.Abs(a.Net).CompareTo(Mathf.Abs(b.Net)));
            float loudest = 1f, running = 0f, reach = Mathf.Abs(total);
            foreach (var j in judges)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(j.Net));
                running += j.Net;
                reach = Mathf.Max(reach, Mathf.Abs(running));
            }
            reach = Mathf.Max(1f, reach * 1.1f);
            running = 0f;

            var spot = Spotlight(host.transform);
            foreach (var j in judges)
            {
                // Кто он — крупно, под светом сверху: говорит тот, на ком свет.
                Step = "judge";
                Vector3 dir = Flat(j.Seat - Hall);
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                // Близко, в лицо: с середины стола в кадр лезли соседи (кадры 05.11).
                host.StartCoroutine(camera.FocusOnPoint(j.Seat + Vector3.up * 1.3f,
                    j.Seat - dir * 1.5f + side * 0.12f + Vector3.up * 1.5f, null, push: 9f));
                host.StartCoroutine(SpotOn(spot, j));

                yield return Line(box, j.Voice, j.Title, Introduce(j, w));

                // Его слово — с общего плана: видно, как огонь разгорелся и стрелка качнулась.
                Step = "stance";
                host.StartCoroutine(camera.FocusOnPoint(look, overview, null, push: 20f));
                host.StartCoroutine(Flare(j, Mathf.Abs(j.Net) / loudest));
                running += j.Net;
                host._aim = Tilt(running, reach);
                string stance = j.ForOrder ? Loc.T("Я — за приказ.") : Loc.T("Я — против.");
                yield return Line(box, j.Voice, j.Title, Words(j, rival, c, w) + " " + stance);
            }

            // 5. Вердикт: стрелка встаёт по решению, сильная сторона разгорается,
            // слабая никнет, свеча приказа гаснет — или вспыхивает.
            Step = "verdict";
            host.StartCoroutine(SpotOff(spot));
            host.StartCoroutine(camera.FocusOnPoint(look, overview, null, push: 20f));
            bool refused = d.RefusedCommand || d.Hesitated;
            host._aim = Final(d, total, reach);
            if (!d.Hesitated)
                foreach (var j in judges) host.StartCoroutine(Settle(j, refused ? !j.ForOrder : j.ForOrder));
            if (orderFire != null)
                host.StartCoroutine(refused ? Die(orderFire, orderFlame) : Blaze(orderFire));

            yield return Line(box, host, Loc.T("Вердикт"), Sentence(w, d, judges));

            string why = Why(w, d, c);
            if (!string.IsNullOrEmpty(why))
                yield return Line(box, host, Loc.T("Вердикт"), why);

            // 6. Назад, в мир.
            Step = "exit";
            yield return fader.To(1f, 0.6f);
            world.Rise();
            camera.StopSway();
            yield return camera.RestoreCamera();
            if (hall != null) Destroy(hall);
            yield return fader.To(0f, 0.6f);
            fader.Drop();

            if (pause != null && pause.IsPaused && pause.Stamp == mine) pause.Resume();
            Sitting = false;
            Step = "";
        }

        // ──────────────────────────────────
        // Зал для просмотра в редакторе
        // ──────────────────────────────────

        /// <summary>Откуда смотрит общий план — для камеры сцены просмотра (зал в нуле).</summary>
        public static Vector3 PreviewFrom => OverviewFrom;
        public static Vector3 PreviewLook => OverviewLook;

        /// <summary>
        /// Зал суда без игры — посмотреть в редакторе и сравнить (автор, 05.11:
        /// «суд в двух вариациях… с 5 судьями и с 13»). Тот же стол, судьи, компас,
        /// что строит суд; раскладка — с прогона сна 05.11 (душа Гордыни при убийцах:
        /// Верность 40 и Страх 3 за приказ, Гордыня 36, Совесть 35, Добродетель 2
        /// против), миг — вердикт, приказ отвергнут. <paramref name="shadows"/> —
        /// ставить ли восемь молчащих голосов в тень за столом (всего тринадцать).
        /// </summary>
        public static GameObject Preview(bool shadows)
        {
            const bool refused = true;
            var judges = new List<Judge>
            {
                new Judge { Module = "Fear", Obey = 3f },
                new Judge { Module = "Loyalty", Obey = 40f },
                new Judge { Module = "Virtue", Rival = 2f },
                new Judge { Module = "Morality", Rival = 35f },
                new Judge { Module = "Pride", Rival = 36f },
            };
            var silent = shadows
                ? new List<string> { "Envy", "Gluttony", "Greed", "Lust", "Memory", "Patience", "Sloth", "Wrath" }
                : new List<string>();
            var hall = Build(judges, silent, out var host, out var orderFire, out var orderFlame);

            // Огни — как к концу вердикта: разгорелись по силе голоса, проигравшие сникли.
            float against = 0f, loudest = 1f;
            foreach (var j in judges) { against += j.Net; loudest = Mathf.Max(loudest, Mathf.Abs(j.Net)); }
            Judge decider = null;
            foreach (var j in judges)
            {
                bool won = refused ? !j.ForOrder : j.ForOrder;
                float share = Mathf.Abs(j.Net) / loudest;
                if (j.Fire != null) j.Fire.intensity = Mathf.Lerp(0.6f, 2.6f, share) * (won ? 1.25f : 0.2f);
                if (j.Flame != null) j.Flame.localScale *= (1f + share * 0.9f) * (won ? 1.15f : 0.45f);
                if (won && (decider == null || Mathf.Abs(j.Net) > Mathf.Abs(decider.Net))) decider = j;
            }

            if (orderFire != null)
            {
                if (refused) { orderFire.intensity = 0f; if (orderFlame != null) orderFlame.gameObject.SetActive(false); }
                else orderFire.intensity *= 2.2f;
            }

            float tilt = Tilt(against, Mathf.Abs(against) * 1.6f);
            host._angle = host._aim = refused ? Mathf.Min(tilt, -8f) : Mathf.Max(tilt, 8f);
            if (host._needle != null) host._needle.localRotation = Quaternion.AngleAxis(host._angle, Vector3.forward);

            var spot = Spotlight(hall.transform);
            if (decider != null) { Aim(spot, decider); spot.intensity = SpotPower; }
            return hall;
        }

        // ──────────────────────────────────
        // Кто за столом
        // ──────────────────────────────────

        /// <summary>Что душа выбрала вместо приказа — или что тянуло её прочь.</summary>
        private static ActionType Rival(Decision d)
        {
            if (d.RefusedCommand) return d.Action;
            if (d.TopContender != ActionType.ObeyCommand) return d.TopContender;
            return d.RunnerUp;
        }

        /// <summary>
        /// Судьи: голоса, что развели приказ и соперника громче прочих. Голос
        /// «за» и голос «против» — по разнице того, что он дал сопернику и что
        /// приказу. Обе стороны, если они есть, за столом представлены.
        /// </summary>
        private static List<Judge> Seat(Warrior w, DecisionContext c, ActionType rival, out float total,
                                        out List<string> silent)
        {
            total = 0f;
            silent = new List<string>();
            var byModule = new Dictionary<string, Judge>();
            foreach (var v in new BehaviourResolver().Ballot(w, c))
            {
                if (v.Action != ActionType.ObeyCommand && v.Action != rival) continue;
                if (!byModule.TryGetValue(v.Module, out var j))
                    byModule[v.Module] = j = new Judge { Module = v.Module };
                if (v.Action == ActionType.ObeyCommand) j.Obey += v.Voice;
                else j.Rival += v.Voice;
            }

            // Стрелка в вердикте — по всем голосам, и по тем, кого за стол не посадили.
            var all = new List<Judge>();
            var ledger = new System.Text.StringBuilder();
            foreach (var j in byModule.Values)
            {
                total += j.Net;
                if (Mathf.Abs(j.Net) >= Audible) all.Add(j);
                ledger.Append($" {j.Module} {j.Obey:F1}/{j.Rival:F1}");
            }
            Debug.Log($"[СУД] Голоса, приказ/соперник:{ledger}");
            all.Sort((a, b) => Mathf.Abs(b.Net).CompareTo(Mathf.Abs(a.Net)));

            var seated = new List<Judge>();
            var forOrder = all.Find(j => j.ForOrder);
            var against = all.Find(j => !j.ForOrder);
            if (forOrder != null) seated.Add(forOrder);
            if (against != null) seated.Add(against);
            foreach (var j in all)
                if (seated.Count < MaxJudges && !seated.Contains(j)) seated.Add(j);

            foreach (var j in seated) j.Title = UI.SelectedUnitPanelUI.VoiceName(j.Module, w.Soul);

            // Остальные голоса души — молчащие: этот вопрос их не задел (или задел тише прочих).
            foreach (var m in byModule.Keys)
                if (!seated.Exists(j => j.Module == m)) silent.Add(m);
            silent.Sort(System.StringComparer.Ordinal);
            return seated;
        }

        // ──────────────────────────────────
        // Слова
        // ──────────────────────────────────

        private static string Headline(Warrior w, Decision d)
        {
            string name = Loc.Name(w.DisplayName);
            if (d.RefusedCommand)
                return Grammar.Pick(w.Gender, Loc.F("Отказ. {0} не исполнил приказ.", name),
                                              Loc.F("Отказ. {0} не исполнила приказ.", name));
            if (d.Hesitated)
                return Grammar.Pick(w.Gender, Loc.F("{0} замер: приказ не исполнен.", name),
                                              Loc.F("{0} замерла: приказ не исполнен.", name));
            return Grammar.Pick(w.Gender, Loc.F("{0} исполнил приказ.", name),
                                          Loc.F("{0} исполнила приказ.", name));
        }

        private static string OrderWords(CommandKind kind)
        {
            switch (kind)
            {
                case CommandKind.FallBack:   return Loc.T("отступить");
                case CommandKind.Move:       return Loc.T("идти, куда велено");
                case CommandKind.Attack:     return Loc.T("бить, кого велено");
                case CommandKind.AttackMove: return Loc.T("идти и бить по дороге");
                case CommandKind.Hold:       return Loc.T("стоять на месте");
                default:                     return Loc.T("исполнить");
            }
        }

        /// <summary>
        /// Слово судьи — по его голосу и по стороне. Числа нет: сила голоса
        /// видна огнём, а не цифрой (00-GDD §7: четвёртая ступень заперта).
        /// </summary>
        private static string Words(Judge j, ActionType rival, DecisionContext c, Warrior w)
        {
            bool virtue = w.Soul != null && IsVirtue(j.Module, w.Soul);

            if (j.ForOrder)
            {
                switch (j.Module)
                {
                    case "Loyalty":  return Loc.T("Владыка велел. Надо идти.");
                    case "Fear":     return Grammar.Pick(w.Gender, Loc.T("Уходи, пока цел."), Loc.T("Уходи, пока цела."));
                    case "Patience": return Loc.T("Не сейчас. Их час ещё придёт.");
                    case "Morality": return Loc.T("Ослушаться — грех.");
                    case "Sloth":    return virtue ? Loc.T("Велено — сделай.") : Loc.T("Уйти проще, чем драться.");
                    case "Greed":    return virtue ? Loc.T("Не о себе думай.") : Loc.T("Даром драться? Уходи.");
                    default:         return Loc.T("Послушай владыку.");
                }
            }

            if (rival == ActionType.Attack)
            {
                switch (j.Module)
                {
                    case "Memory":
                        return c.KnownMurdererInSight ? Loc.T("Это они нас убили. Я их помню.")
                                                      : Loc.T("Помню, чем это кончилось.");
                    case "Wrath":    return virtue ? Loc.T("Стой за своих.") : Loc.T("Бей. Пусть ответят кровью.");
                    case "Pride":    return virtue ? Loc.T("Не мне решать — но я не уйду.") : Loc.T("Бежать от них? Никогда.");
                    case "Envy":     return virtue ? Loc.T("Не уйду.") : Loc.T("Они живы, а мы — нет.");
                    case "Greed":    return virtue ? Loc.T("Не за себя — за правду.") : Loc.T("Своё надо вернуть.");
                    case "Lust":     return Loc.T("Не уйду, пока они здесь.");
                    case "Gluttony": return Loc.T("Мало. Хочу ещё.");
                    case "Fear":     return Loc.T("Спиной к ним не повернусь.");
                    case "Morality": return Loc.T("Убийц нельзя отпускать.");
                    case "Virtue":   return Loc.T("Уйдём — они убьют ещё кого-то.");
                    default:         return Loc.T("Бей!");
                }
            }

            switch (rival)
            {
                case ActionType.Idle:     return j.Module == "Sloth" && !virtue ? Loc.T("Никуда я не пойду.") : Loc.T("Стой, где стоишь.");
                case ActionType.Flee:     return Loc.T("Беги!");
                case ActionType.Loot:     return Loc.T("Сначала добыча.");
                case ActionType.SaveAlly: return Loc.T("Своих не бросают.");
                default:                  return Loc.T("Не уходи.");
            }
        }

        private static bool IsVirtue(string module, SoulData soul)
        {
            switch (module)
            {
                case "Greed":    return soul.Get(SinType.Greed) < 0f;
                case "Pride":    return soul.Get(SinType.Pride) < 0f;
                case "Wrath":    return soul.Get(SinType.Wrath) < 0f;
                case "Envy":     return soul.Get(SinType.Envy) < 0f;
                case "Lust":     return soul.Get(SinType.Lust) < 0f;
                case "Gluttony": return soul.Get(SinType.Gluttony) < 0f;
                case "Sloth":    return soul.Get(SinType.Sloth) < 0f;
                default:         return false;
            }
        }

        /// <summary>
        /// Приговор — по сторонам, а не по одному голосу: голоса складываются.
        /// До 05.11 здесь стояло «гордыня громче, чем верность» — а верность
        /// была громче (40 против 36), проиграла она сумме против.
        /// </summary>
        private static string Sentence(Warrior w, Decision d, List<Judge> judges)
        {
            if (d.Hesitated)
                return Loc.T("Голоса вровень — душа не выбрала ничего.");

            string against = Roll(judges.FindAll(j => !j.ForOrder));
            string pro = Roll(judges.FindAll(j => j.ForOrder));

            if (d.RefusedCommand)
            {
                if (string.IsNullOrEmpty(against)) return Loc.T("Приказ отвергнут.");
                if (string.IsNullOrEmpty(pro)) return Loc.F("Приказ отвергнут. Против — {0}. За приказ — никто.", against);
                return Loc.F("Приказ отвергнут. Против — {0}. За — {1}. Стрелка склонилась к «против».", against, pro);
            }

            if (string.IsNullOrEmpty(pro)) return Loc.T("Приказ исполнен.");
            if (string.IsNullOrEmpty(against)) return Loc.F("Приказ исполнен. За — {0}. Против — никто.", pro);
            return Loc.F("Приказ исполнен. За — {0}. Против — {1}. Стрелка склонилась к «за».", pro, against);
        }

        /// <summary>Имена стороны через запятую, громкий — первым: «гордыня, совесть и добродетель».</summary>
        private static string Roll(List<Judge> side)
        {
            if (side.Count == 0) return "";
            side.Sort((a, b) => Mathf.Abs(b.Net).CompareTo(Mathf.Abs(a.Net)));
            var names = side.ConvertAll(j => Lower(j.Title));
            if (names.Count == 1) return names[0];
            return Loc.F("{0} и {1}", string.Join(", ", names.GetRange(0, names.Count - 1)), names[names.Count - 1]);
        }

        /// <summary>Где встанет стрелка: по сумме всех голосов, но не против самого решения.</summary>
        private static float Final(Decision d, float total, float reach)
        {
            float a = Tilt(total, reach);
            if (d.Hesitated) return Mathf.Clamp(a, -4f, 4f);
            if (d.RefusedCommand) return Mathf.Min(a, -8f);
            return Mathf.Max(a, 8f);
        }

        /// <summary>
        /// Почему — «от противного»: без чего он поступил бы иначе. Это и есть
        /// довод, что решение — его, а не сцены: убери причину — и приговор другой.
        /// </summary>
        private static string Why(Warrior w, Decision d, DecisionContext c)
        {
            string obeyed = Grammar.Pick(w.Gender, Loc.T("он бы послушался"), Loc.T("она бы послушалась"));

            if (!(d.RefusedCommand || d.Hesitated))
                return Loc.T("Другая душа на его месте могла решить иначе.");

            if (d.Weighed && d.Decisive != Counterfactual.Factor.None)
            {
                string because = Counterfactual.Phrase(d.Decisive, c, w.Soul != null ? w.Soul.Sin : (SinType?)null, w.Gender);
                if (d.DecisiveAlso != Counterfactual.Factor.None)
                    because = Loc.F("{0}, и {1}", because,
                        Counterfactual.Phrase(d.DecisiveAlso, c, w.Soul != null ? w.Soul.Sin : (SinType?)null, w.Gender));
                return Loc.F("Решило одно: {0}. Не будь этого — {1}.", because, obeyed);
            }

            if (d.Weighed && !string.IsNullOrEmpty(d.DecisiveVoice))
                return Loc.F("Решил один голос — {0}. Без него {1}.",
                    Lower(UI.SelectedUnitPanelUI.VoiceName(d.DecisiveVoice, w.Soul)), obeyed);

            return Loc.T("Одной причины нет — так решила вся душа разом.");
        }

        private static string Lower(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        // ──────────────────────────────────
        // Зал
        // ──────────────────────────────────

        private static GameObject Build(List<Judge> judges, List<string> silent, out SoulCourt host,
                                        out Light orderFire, out Transform orderFlame)
        {
            var hall = new GameObject("Суд души");
            hall.transform.position = Hall;
            host = hall.AddComponent<SoulCourt>();
            Throat(hall, 0.62f);   // голос самого суда — вступление и вердикт

            var stone = Lit(new Color(0.07f, 0.065f, 0.07f), 0.1f);
            var wood = Lit(new Color(0.16f, 0.10f, 0.06f), 0.25f);
            var wax = Lit(new Color(0.78f, 0.72f, 0.60f), 0.2f);
            var paper = Lit(new Color(0.82f, 0.76f, 0.62f), 0.05f);

            Piece(hall.transform, PrimitiveType.Cylinder, "Пол", Hall + Vector3.up * -0.01f, new Vector3(9f, 0.01f, 9f), stone);
            Piece(hall.transform, PrimitiveType.Cylinder, "Ножка стола", Hall + Vector3.up * (TableTop * 0.5f), new Vector3(0.55f, TableTop * 0.5f, 0.55f), wood);
            Piece(hall.transform, PrimitiveType.Cylinder, "Стол", Hall + Vector3.up * (TableTop - 0.03f), new Vector3(2.7f, 0.03f, 2.7f), wood);

            // Приказ — лист в середине стола и его свеча.
            Piece(hall.transform, PrimitiveType.Cube, "Приказ", Hall + Vector3.up * (TableTop + 0.006f), new Vector3(0.34f, 0.01f, 0.24f), paper);
            orderFire = Candle(hall.transform, Hall + new Vector3(0f, TableTop, 0.2f), 0.22f, new Color(1f, 0.78f, 0.5f), wax, out orderFlame);
            orderFire.intensity = 1.6f;
            orderFire.range = 3.2f;

            // Судьи — дугой по дальней стороне: за приказ — слева, против — справа.
            var left = judges.FindAll(j => j.ForOrder);
            var right = judges.FindAll(j => !j.ForOrder);
            Place(left, 112f, 166f);
            Place(right, 14f, 68f);

            for (int i = 0; i < judges.Count; i++)
            {
                var j = judges[i];
                var root = new GameObject("Судья — " + j.Module);
                root.transform.SetParent(hall.transform, false);
                root.transform.position = j.Seat;
                root.transform.rotation = Quaternion.LookRotation(Flat(Hall - j.Seat));
                j.Voice = Throat(root, 0.72f + 0.07f * i);
                var body = SinbinderPlayer.Judge(root.transform, Glow(j.Module));
                if (body != null)
                    foreach (var a in body.GetComponentsInChildren<Animator>())
                        a.updateMode = AnimatorUpdateMode.UnscaledTime;

                // Свеча — у самого судьи и его цвета: на общем плане видно, чей огонь чей.
                Vector3 dir = Flat(j.Seat - Hall);
                j.Fire = Candle(hall.transform, Hall + dir * 1.12f + Vector3.up * TableTop, 0.13f,
                                Color.Lerp(new Color(1f, 0.72f, 0.42f), Glow(j.Module), 0.75f), wax, out j.Flame, 1f);
                j.Fire.intensity = 0.35f;
            }

            // Молчащие — второй дугой, дальше от стола: без свечи, глаза едва тлеют.
            // Свет до них доходит лишь от компаса — их видно, но не слышно.
            for (int i = 0; i < silent.Count; i++)
            {
                float k = silent.Count == 1 ? 0.5f : i / (float)(silent.Count - 1);
                float a = Mathf.Lerp(24f, 156f, k) * Mathf.Deg2Rad;
                Vector3 at = Hall + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.95f;
                var root = new GameObject("Молчащий — " + silent[i]);
                root.transform.SetParent(hall.transform, false);
                root.transform.position = at;
                root.transform.rotation = Quaternion.LookRotation(Flat(Hall - at));
                var body = SinbinderPlayer.Judge(root.transform, Glow(silent[i]) * 0.35f);
                if (body != null)
                    foreach (var anim in body.GetComponentsInChildren<Animator>())
                        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            host._needle = Dial(hall.transform);
            return hall;
        }

        /// <summary>Компас: тёмный циферблат, двенадцать делений, одна стрелка.</summary>
        private static Transform Dial(Transform parent)
        {
            var root = new GameObject("Моральный компас").transform;
            root.SetParent(parent, false);
            root.position = Hall + DialAt;
            root.rotation = Quaternion.identity;

            var face = Piece(root, PrimitiveType.Cylinder, "Циферблат", root.position + Vector3.forward * 0.03f,
                             new Vector3(1.45f, 0.025f, 1.45f), Lit(new Color(0.09f, 0.085f, 0.095f), 0.35f));
            face.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Деления как у часов: 9–11 — за приказ (там сидят такие судьи), 1–3 — против,
            // 12 — вровень, низ — тёмный: туда стрелка не ходит.
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f;
                Color tone = k == 0 ? new Color(0.95f, 0.93f, 0.88f)
                           : k <= 3 ? AgainstTone
                           : k >= 9 ? ForOrderTone
                           : new Color(0.20f, 0.19f, 0.22f);
                Vector3 at = root.position - Vector3.forward * 0.01f
                           + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad), 0f) * 0.6f;
                var tick = Piece(root, PrimitiveType.Cube, "Деление", at,
                                 new Vector3(0.035f, k % 3 == 0 ? 0.15f : 0.09f, 0.01f), Glowing(tone, 1f));
                tick.transform.rotation = Quaternion.AngleAxis(-a, Vector3.forward);
            }

            var pivot = new GameObject("Стрелка").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0f, 0f, -0.04f);
            var brass = Glowing(new Color(0.95f, 0.88f, 0.70f), 1f);
            Piece(pivot, PrimitiveType.Cube, "Остриё", pivot.position + Vector3.up * 0.26f, new Vector3(0.03f, 0.52f, 0.012f), brass);
            Piece(pivot, PrimitiveType.Cube, "Хвост", pivot.position - Vector3.up * 0.07f, new Vector3(0.05f, 0.14f, 0.012f), brass);
            var hub = Piece(pivot, PrimitiveType.Cylinder, "Ось", pivot.position, new Vector3(0.08f, 0.012f, 0.08f), brass);
            hub.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var go = new GameObject("Свет компаса");
            go.transform.SetParent(root, false);
            go.transform.position = root.position - Vector3.forward * 0.9f + Vector3.up * 0.2f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2.2f;
            light.intensity = 0.7f;
            light.color = new Color(0.75f, 0.78f, 0.95f);
            light.shadows = LightShadows.None;
            return pivot;
        }

        /// <summary>Сила света на говорящем: при 2,6 его на кадрах 05.11 не было видно — до судьи три метра.</summary>
        private const float SpotPower = 14f;

        /// <summary>Свет сверху на говорящего — один на весь суд, переезжает от судьи к судье.</summary>
        private static Light Spotlight(Transform parent)
        {
            var go = new GameObject("Свет на говорящего");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 34f;
            light.innerSpotAngle = 16f;
            light.range = 6f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Поставить свет над судьёй: сверху и чуть спереди, его цвета.</summary>
        private static void Aim(Light spot, Judge j)
        {
            Vector3 dir = Flat(j.Seat - Hall);
            Vector3 at = j.Seat - dir * 0.9f + Vector3.up * 3.1f;
            spot.transform.SetPositionAndRotation(at, Quaternion.LookRotation(j.Seat + Vector3.up * 1.0f - at));
            spot.color = Color.Lerp(new Color(0.78f, 0.80f, 0.92f), Glow(j.Module), 0.35f);
        }

        private static IEnumerator SpotOn(Light spot, Judge j)
        {
            if (spot == null) yield break;
            Aim(spot, j);
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                if (spot == null) yield break;
                spot.intensity = Mathf.Lerp(0f, SpotPower, t / 0.35f);
                yield return null;
            }
            if (spot != null) spot.intensity = SpotPower;
        }

        private static IEnumerator SpotOff(Light spot)
        {
            if (spot == null) yield break;
            float from = spot.intensity;
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                if (spot == null) yield break;
                spot.intensity = Mathf.Lerp(from, 0f, t / 0.5f);
                yield return null;
            }
            if (spot != null) spot.intensity = 0f;
        }

        /// <summary>Вердикт: сильная сторона разгорается, слабая никнет.</summary>
        private static IEnumerator Settle(Judge j, bool won)
        {
            if (j.Fire == null) yield break;
            float from = j.Fire.intensity, to = won ? from * 1.25f : from * 0.2f;
            Vector3 size = j.Flame != null ? j.Flame.localScale : Vector3.one;
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                if (j.Fire == null) yield break;
                float k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                j.Fire.intensity = Mathf.Lerp(from, to, k);
                if (j.Flame != null) j.Flame.localScale = size * Mathf.Lerp(1f, won ? 1.15f : 0.45f, k);
                yield return null;
            }
        }

        private static void Place(List<Judge> side, float from, float to)
        {
            for (int i = 0; i < side.Count; i++)
            {
                float k = side.Count == 1 ? 0.5f : i / (float)(side.Count - 1);
                float a = Mathf.Lerp(from, to, k) * Mathf.Deg2Rad;
                side[i].Seat = Hall + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Ring;
            }
        }

        /// <summary>Свеча: воск, огонёк и свет. Возвращает свет; огонёк — в <paramref name="flame"/>.</summary>
        private static Light Candle(Transform parent, Vector3 at, float tall, Color colour, Material wax, out Transform flame,
                                    float bright = 1.3f)
        {
            Piece(parent, PrimitiveType.Cylinder, "Свеча", at + Vector3.up * (tall * 0.5f), new Vector3(0.055f, tall * 0.5f, 0.055f), wax);

            // Огонёк судьи — не пересвечен: пересвет съедал цвет, и все огни были белыми (кадры 05.11).
            var tip = Piece(parent, PrimitiveType.Sphere, "Огонёк", at + Vector3.up * (tall + 0.03f), new Vector3(0.035f, 0.06f, 0.035f), Glowing(colour, bright));
            flame = tip.transform;

            var go = new GameObject("Свет свечи");
            go.transform.SetParent(parent, false);
            go.transform.position = at + Vector3.up * (tall + 0.1f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.range = 2.6f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static GameObject Piece(Transform parent, PrimitiveType type, string name, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Material Lit(Color colour, float smooth)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { color = colour };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }

        private static Material Glowing(Color colour, float bright = 1.3f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            return new Material(shader) { color = colour * bright };
        }

        /// <summary>Цвет голоса: у греха — его цвет, у прочих — свой.</summary>
        private static Color Glow(string module)
        {
            switch (module)
            {
                case "Greed":    return SinPalette.Of(SinType.Greed);
                case "Pride":    return SinPalette.Of(SinType.Pride);
                case "Wrath":    return SinPalette.Of(SinType.Wrath);
                case "Envy":     return SinPalette.Of(SinType.Envy);
                case "Lust":     return SinPalette.Of(SinType.Lust);
                case "Gluttony": return SinPalette.Of(SinType.Gluttony);
                case "Sloth":    return SinPalette.Of(SinType.Sloth);
                case "Memory":   return new Color(0.40f, 0.62f, 1f);
                case "Loyalty":  return new Color(1f, 0.74f, 0.22f);
                case "Fear":     return new Color(0.45f, 0.80f, 0.50f);
                case "Patience": return new Color(0.35f, 0.52f, 1f);
                case "Morality": return new Color(0.95f, 0.95f, 0.92f);
                case "Virtue":   return new Color(0.35f, 0.88f, 0.85f);
                default:         return new Color(0.9f, 0.85f, 0.75f);
            }
        }

        // ──────────────────────────────────
        // Огни
        // ──────────────────────────────────

        /// <summary>Огонь судьи разгорается до силы его голоса.</summary>
        private static IEnumerator Flare(Judge j, float share)
        {
            if (j.Fire == null) yield break;
            float from = j.Fire.intensity;
            float to = Mathf.Lerp(0.6f, 2.6f, Mathf.Clamp01(share));
            Vector3 size = j.Flame != null ? j.Flame.localScale : Vector3.one;
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.7f);
                if (j.Fire == null) yield break;
                j.Fire.intensity = Mathf.Lerp(from, to, k);
                if (j.Flame != null) j.Flame.localScale = size * Mathf.Lerp(1f, 1f + share * 0.9f, k);
                yield return null;
            }
        }

        /// <summary>Свеча приказа гаснет — как огни отрядов в шаре: вспышка и провал.</summary>
        private static IEnumerator Die(Light fire, Transform flame)
        {
            float start = fire.intensity;
            const float seconds = 1.4f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (fire == null) yield break;
                float k = t / seconds;
                fire.intensity = start * (k < 0.25f ? Mathf.Lerp(1f, 2.2f, k / 0.25f) : Mathf.Lerp(2.2f, 0f, (k - 0.25f) / 0.75f));
                yield return null;
            }
            if (fire != null) fire.intensity = 0f;
            if (flame != null) flame.gameObject.SetActive(false);
        }

        private static IEnumerator Blaze(Light fire)
        {
            float start = fire.intensity;
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                if (fire == null) yield break;
                fire.intensity = Mathf.Lerp(start, start * 2.2f, Mathf.SmoothStep(0f, 1f, t / 0.9f));
                yield return null;
            }
        }

        // ──────────────────────────────────
        // Мелочи
        // ──────────────────────────────────

        private static Vector3 Head(Warrior w)
        {
            float scale = Dialogue.DialogueCameraController.Scale(w.transform, out float ground);
            return new Vector3(w.transform.position.x, ground, w.transform.position.z)
                 + Vector3.up * (1.62f * scale);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        /// <summary>Свет мира на время суда: гаснет и возвращается тем же, что был.</summary>
        private sealed class Darkness
        {
            private Camera _eye;
            private CameraClearFlags _clear;
            private Color _background;
            private UnityEngine.Rendering.AmbientMode _ambientMode;
            private Color _ambient, _sky, _equator, _groundColor;
            private float _ambientIntensity, _reflections;
            private bool _fog;
            private readonly List<(Light light, bool on)> _suns = new();

            public static Darkness Fall(Camera eye)
            {
                var d = new Darkness { _eye = eye };
                if (eye != null) { d._clear = eye.clearFlags; d._background = eye.backgroundColor; }
                d._ambientMode = RenderSettings.ambientMode;
                d._ambient = RenderSettings.ambientLight;
                d._sky = RenderSettings.ambientSkyColor;
                d._equator = RenderSettings.ambientEquatorColor;
                d._groundColor = RenderSettings.ambientGroundColor;
                d._ambientIntensity = RenderSettings.ambientIntensity;
                d._reflections = RenderSettings.reflectionIntensity;
                d._fog = RenderSettings.fog;

                if (eye != null) { eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = Color.black; }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.018f, 0.016f, 0.024f);
                RenderSettings.reflectionIntensity = 0f;
                RenderSettings.fog = false;
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l != null && l.type == LightType.Directional) { d._suns.Add((l, l.enabled)); l.enabled = false; }
                return d;
            }

            public void Rise()
            {
                if (_eye != null) { _eye.clearFlags = _clear; _eye.backgroundColor = _background; }
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambient;
                RenderSettings.ambientSkyColor = _sky;
                RenderSettings.ambientEquatorColor = _equator;
                RenderSettings.ambientGroundColor = _groundColor;
                RenderSettings.ambientIntensity = _ambientIntensity;
                RenderSettings.reflectionIntensity = _reflections;
                RenderSettings.fog = _fog;
                foreach (var (light, on) in _suns) if (light != null) light.enabled = on;
            }
        }

        /// <summary>Темнота поверх мира, но под полосами: влёт в голову и выход из неё.</summary>
        private sealed class Fader
        {
            private GameObject _root;
            private Image _black;

            public static Fader Make(UI.Letterbox box)
            {
                var f = new Fader();
                f._root = new GameObject("Суд души — темнота", typeof(Canvas));
                var canvas = f._root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var under = box != null ? box.GetComponentInParent<Canvas>() : null;
                canvas.sortingOrder = under != null ? under.rootCanvas.sortingOrder - 1 : 90;

                var go = new GameObject("Тьма", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(f._root.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                f._black = go.GetComponent<Image>();
                f._black.color = new Color(0f, 0f, 0f, 0f);
                f._black.raycastTarget = false;
                return f;
            }

            public IEnumerator To(float alpha, float seconds)
            {
                if (_black == null) yield break;
                float from = _black.color.a;
                for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                {
                    if (_black == null) yield break;
                    _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, alpha, Mathf.SmoothStep(0f, 1f, t / seconds)));
                    yield return null;
                }
                if (_black != null) _black.color = new Color(0f, 0f, 0f, alpha);
            }

            public void Drop()
            {
                if (_root != null) Object.Destroy(_root);
            }
        }
    }
}
