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
        /// Судьи — все голоса души, тринадцать (автор, 05.11: «голосов очень
        /// мало», «суд выглядит как сбор секты»). Кого вопрос не задел — тот
        /// воздерживается, но за кафедрой сидит и слово говорит.
        /// </summary>
        private static readonly string[] Voices =
        {
            "Loyalty", "Fear", "Memory", "Morality", "Virtue", "Patience",
            "Greed", "Pride", "Wrath", "Envy", "Lust", "Gluttony", "Sloth",
        };

        /// <summary>Тише этого голос в суде — воздержался.</summary>
        private const float Audible = 2f;

        /// <summary>
        /// Зал — суд, а не круг у стола: кафедра дугой, за ней судьи в высоких
        /// креслах, перед ней — подсудимый за оградкой; на стене весы, над ними
        /// моральный компас.
        /// </summary>
        private const float BenchRadius = 3.6f;
        private const float BenchFrom = 162f, BenchTo = 18f;
        private const float DeskTop = 1.12f;
        private const float Dais = 0.18f;
        private const float WallZ = 4.9f;

        /// <summary>Общий план: из-за спины подсудимого — кафедра, весы, компас.</summary>
        private static readonly Vector3 OverviewFrom = new Vector3(0f, 2.4f, -3.9f);
        private static readonly Vector3 OverviewLook = new Vector3(0f, 2.7f, 3.0f);

        /// <summary>
        /// Весы — то, что 05.11 было «компасом»: часы со стрелкой (автор: «идея
        /// с часами как признаком согласия или нет — гениально… назовём весами»).
        /// Слева — «за приказ» (там и сидят такие судьи), справа — «против».
        /// Стрелка — сумма сказанного.
        /// </summary>
        private static readonly Vector3 ScalesAt = new Vector3(0f, 3.35f, WallZ - 0.2f);
        private static readonly Vector3 ScalesFrom = new Vector3(0f, 3.0f, 1.7f);
        private const float MaxTilt = 70f;
        private static readonly Color ForOrderTone = new Color(0.95f, 0.76f, 0.30f);
        private static readonly Color AgainstTone = new Color(0.82f, 0.22f, 0.20f);

        /// <summary>
        /// Моральный компас — выше весов (автор, 05.11: «я говорил про моральный
        /// компас персонажа: добрый, нейтральный, злой. Компас можешь сделать
        /// выше весов»). Слева — добро, вверху — середина, справа — зло.
        /// </summary>
        private static readonly Vector3 CompassAt = new Vector3(0f, 5.05f, WallZ - 0.2f);
        private static readonly Vector3 CompassFrom = new Vector3(0f, 4.5f, 2.0f);
        private static readonly Color PiousTone = new Color(0.98f, 0.90f, 0.62f);
        private static readonly Color NeutralTone = new Color(0.55f, 0.56f, 0.62f);
        private static readonly Color ViciousTone = new Color(0.85f, 0.10f, 0.08f);

        /// <summary>Куда смотрит стрелка компаса: плюс — влево (добро), минус — вправо (зло).</summary>
        private static float MoralAngle(MoralType m)
            => m == MoralType.Pious ? 60f : m == MoralType.Vicious ? -60f : 0f;

        // Стрелки живут на зале: у них вес — идут к цели, чуть проскакивают, успокаиваются.
        private Transform _needle, _compass;
        private float _angle, _swing, _aim;
        private float _cAngle, _cSwing, _cAim;

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_needle != null)
            {
                _swing += ((_aim - _angle) * 28f - _swing * 6.5f) * dt;
                _angle += _swing * dt;
                _needle.localRotation = Quaternion.AngleAxis(_angle, Vector3.forward);
            }
            if (_compass != null)
            {
                // Компас ищет своё, как настоящий: крутится, проскакивает, встаёт.
                _cSwing += ((_cAim - _cAngle) * 9f - _cSwing * 1.6f) * dt;
                _cAngle += _cSwing * dt;
                _compass.localRotation = Quaternion.AngleAxis(_cAngle, Vector3.forward);
            }
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
            public bool Abstains => Mathf.Abs(Net) < Audible;
            public string Title;
            public Vector3 Seat;
            public Light Fire;
            public Transform Flame;
            public MonoBehaviour Voice;
            /// <summary>Шарнир в поясе: поклон, кивок, откидывание — весь верх тела.</summary>
            public Transform Waist;
            public Animator[] Anims;
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

            // Сон, открытый из лагеря (меню паузы): Managers лагеря переживает
            // смену сцен, а Managers сна гибнет как двойник — и Instance мог
            // остаться на погибшем. Сборка 05.11 так молча пропустила суд.
            var camera = Dialogue.DialogueCameraController.Instance;
            if (camera == null) camera = Object.FindFirstObjectByType<Dialogue.DialogueCameraController>();
            var box = UI.Letterbox.Instance;
            if (box == null) box = Object.FindFirstObjectByType<UI.Letterbox>(FindObjectsInactive.Include);
            var eye = Camera.main;
            if (camera == null || box == null || eye == null)
            {
                Debug.LogWarning($"[СУД] не собрался: камера наезда {(camera != null)}, полосы {(box != null)}, главная камера {(eye != null)}");
                yield break;
            }

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
            var judges = Seat(w, c, rival, out float total);
            Seated = judges.Count;
            var voting = judges.FindAll(j => !j.Abstains);
            Bench = string.Join(", ", voting.ConvertAll(j => $"{j.Module} {(j.ForOrder ? "+" : "-")} {Mathf.Abs(j.Net):F0}"));
            Debug.Log($"[СУД] {w.DisplayName}: {Outcome}; против приказа — {rival}; голосуют: {Bench}; "
                    + $"воздержались: {string.Join(", ", judges.FindAll(j => j.Abstains).ConvertAll(j => j.Module))}");

            var moral = w.Soul != null ? w.Soul.Moral : MoralType.Neutral;
            var hall = Build(judges, w.Shell, out var host, out var orderFire, out var orderFlame);

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
            // себя сам: кто судьи, кто перед ними, что значат компас, весы и огонь.
            string court = Loc.T("Суд души");
            yield return Line(box, host, court, Grammar.Pick(w.Gender,
                Loc.T("Это суд его души. Судьи — его голоса, все тринадцать. Перед ними — он сам."),
                Loc.T("Это суд её души. Судьи — её голоса, все тринадцать. Перед ними — она сама.")));

            // Компас — кто он по натуре: крутится и встаёт на своё.
            Step = "compass";
            host._cSwing = 540f;
            host._cAim = MoralAngle(moral);
            host.StartCoroutine(camera.FocusOnPoint(Hall + CompassAt, Hall + CompassFrom, null, push: 12f));
            yield return Line(box, host, court, CompassWords(w, moral));

            // Весы — как он поступит сейчас.
            Step = "scales";
            host.StartCoroutine(camera.FocusOnPoint(Hall + ScalesAt, Hall + ScalesFrom, null, push: 12f));
            yield return Line(box, host, court, Grammar.Pick(w.Gender,
                Loc.T("Ниже — весы. Слева — голоса за приказ, справа — против. Куда склонятся весы, так он и поступит."),
                Loc.T("Ниже — весы. Слева — голоса за приказ, справа — против. Куда склонятся весы, так она и поступит.")));

            Step = "hall";
            host.StartCoroutine(camera.FocusOnPoint(look, overview, null, push: 20f));
            yield return Line(box, host, court,
                Loc.F("Перед судом — приказ владыки: «{0}». Судьи, встаньте и назовитесь.", OrderWords(w.Command.Kind)));

            // 4. Знакомство (автор, 05.11: «сначала познакомить с персонажами,
            // чтобы при знакомстве проиграли приветственные анимации, желательно
            // разные»): слева направо, каждый — крупно, под светом, своим жестом.
            var spot = Spotlight(host.transform);
            foreach (var j in judges)
            {
                Step = "judge";
                Vector3 dir = Flat(j.Seat - Hall);
                host.StartCoroutine(camera.FocusOnPoint(j.Seat + Vector3.up * (Dais + 1.35f),
                    j.Seat - dir * 1.25f + Vector3.up * (Dais + 1.55f), null, push: 9f));
                host.StartCoroutine(SpotOn(spot, j));
                host.StartCoroutine(Greet(j));
                yield return Line(box, j.Voice, j.Title, Introduce(j, w));
            }

            // 5. Слово каждого (автор: «а после каждый высказался»): тише — первыми,
            // громче — последними. Каждое «за» и «против» качает весы.
            Step = "stance";
            host.StartCoroutine(camera.FocusOnPoint(look, overview, null, push: 20f));
            yield return Line(box, host, court, Loc.T("Судьи, ваше слово. Тише — первыми, громче — последними."));

            var order = new List<Judge>(judges);
            order.Sort((a, b) => Mathf.Abs(a.Net).CompareTo(Mathf.Abs(b.Net)));
            float loudest = 1f, running = 0f, reach = Mathf.Abs(total);
            foreach (var j in order)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(j.Net));
                running += j.Net;
                reach = Mathf.Max(reach, Mathf.Abs(running));
            }
            reach = Mathf.Max(1f, reach * 1.1f);
            running = 0f;

            foreach (var j in order)
            {
                // Камера с общего плана чуть поворачивает к говорящему: весы — в кадре.
                host.StartCoroutine(camera.FocusOnPoint(Vector3.Lerp(look, j.Seat + Vector3.up * 1.6f, 0.3f),
                                                        overview, null, push: 14f));
                host.StartCoroutine(SpotOn(spot, j));
                host.StartCoroutine(Flare(j, Mathf.Abs(j.Net) / loudest));
                Play(j, j.Abstains ? "Idle" : "Talk");
                running += j.Net;
                host._aim = Tilt(running, reach);
                string stance = j.Abstains ? Loc.T("Воздерживаюсь.")
                              : j.ForOrder ? Loc.T("Я — за приказ.") : Loc.T("Я — против.");
                yield return Line(box, j.Voice, j.Title, Words(j, rival, c, w) + " " + stance);
                Play(j, "Idle");
            }

            // 6. Вердикт: весы встают по решению, сильная сторона разгорается,
            // слабая никнет, свеча приказа гаснет — или вспыхивает.
            Step = "verdict";
            host.StartCoroutine(SpotOff(spot));
            host.StartCoroutine(camera.FocusOnPoint(look, overview, null, push: 20f));
            bool refused = d.RefusedCommand || d.Hesitated;
            host._aim = Final(d, total, reach);
            if (!d.Hesitated)
                foreach (var j in voting) host.StartCoroutine(Settle(j, refused ? !j.ForOrder : j.ForOrder));
            if (orderFire != null)
                host.StartCoroutine(refused ? Die(orderFire, orderFlame) : Blaze(orderFire));

            yield return Line(box, host, Loc.T("Вердикт"), Sentence(w, d, voting));

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

        /// <summary>Кадры для просмотра (зал в нуле): общий, компас, весы и крупно — судья <paramref name="seat"/> из 13.</summary>
        public static (string name, Vector3 from, Vector3 look)[] PreviewShots(int seat)
        {
            float a = Mathf.Lerp(BenchFrom, BenchTo, seat / 12f) * Mathf.Deg2Rad;
            var at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * BenchRadius;
            var dir = Flat(at);
            return new[]
            {
                ("общий", OverviewFrom, OverviewLook),
                ("компас", CompassFrom, CompassAt),
                ("весы", ScalesFrom, ScalesAt),
                ("судья", at - dir * 1.25f + Vector3.up * (Dais + 1.55f), at + Vector3.up * (Dais + 1.35f)),
            };
        }

        /// <summary>
        /// Зал суда без игры — посмотреть в редакторе. Та же кафедра, судьи, весы
        /// и компас, что строит суд; раскладка — с прогона сна 05.11 (душа Гордыни
        /// при убийцах, злобная: Верность 40 и Страх 3 за приказ, Гордыня 36,
        /// Совесть 35, Добродетель 2 против, прочие воздержались), миг — вердикт,
        /// приказ отвергнут. <paramref name="shadows"/> оставлен для старого меню.
        /// </summary>
        public static GameObject Preview(bool shadows)
        {
            const bool refused = true;
            var votes = new Dictionary<string, (float obey, float rival)>
            {
                ["Fear"] = (3f, 0f), ["Loyalty"] = (40f, 0f), ["Virtue"] = (0f, 2f),
                ["Morality"] = (0f, 35f), ["Pride"] = (0f, 36f),
            };
            var all = new List<Judge>();
            foreach (var m in Voices)
            {
                votes.TryGetValue(m, out var v);
                all.Add(new Judge { Module = m, Obey = v.obey, Rival = v.rival });
            }
            var judges = Arrange(all);
            var hall = Build(judges, ShellType.Zombie, out var host, out var orderFire, out var orderFlame);
            host._cAngle = host._cAim = MoralAngle(MoralType.Vicious);
            if (host._compass != null) host._compass.localRotation = Quaternion.AngleAxis(host._cAngle, Vector3.forward);

            // Огни — как к концу вердикта: разгорелись по силе голоса, проигравшие сникли.
            float against = 0f, loudest = 1f;
            foreach (var j in judges) { against += j.Net; loudest = Mathf.Max(loudest, Mathf.Abs(j.Net)); }
            Judge decider = null;
            foreach (var j in judges)
            {
                if (j.Abstains) continue;
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
        /// Судьи — все голоса души. Голос «за» и голос «против» — по разнице того,
        /// что он дал сопернику и что приказу; тише <see cref="Audible"/> — воздержался.
        /// </summary>
        private static List<Judge> Seat(Warrior w, DecisionContext c, ActionType rival, out float total)
        {
            total = 0f;
            var byModule = new Dictionary<string, Judge>();
            foreach (var m in Voices) byModule[m] = new Judge { Module = m };
            foreach (var v in new BehaviourResolver().Ballot(w, c))
            {
                if (v.Action != ActionType.ObeyCommand && v.Action != rival) continue;
                if (!byModule.TryGetValue(v.Module, out var j))
                    byModule[v.Module] = j = new Judge { Module = v.Module };
                if (v.Action == ActionType.ObeyCommand) j.Obey += v.Voice;
                else j.Rival += v.Voice;
            }

            var all = new List<Judge>(byModule.Values);
            var ledger = new System.Text.StringBuilder();
            foreach (var j in all)
            {
                total += j.Net;
                j.Title = UI.SelectedUnitPanelUI.VoiceName(j.Module, w.Soul);
                ledger.Append($" {j.Module} {j.Obey:F1}/{j.Rival:F1}");
            }
            Debug.Log($"[СУД] Голоса, приказ/соперник:{ledger}");
            return Arrange(all);
        }

        /// <summary>
        /// Места за кафедрой слева направо: за приказ — слева (громче — ближе
        /// к середине), воздержавшиеся — посередине, под весами; против — справа.
        /// Кафедра сама — итог: видно, сколько на какой стороне.
        /// </summary>
        private static List<Judge> Arrange(List<Judge> all)
        {
            var pro = all.FindAll(j => !j.Abstains && j.ForOrder);
            var mid = all.FindAll(j => j.Abstains);
            var con = all.FindAll(j => !j.Abstains && !j.ForOrder);
            pro.Sort((a, b) => Mathf.Abs(a.Net).CompareTo(Mathf.Abs(b.Net)));
            con.Sort((a, b) => Mathf.Abs(b.Net).CompareTo(Mathf.Abs(a.Net)));
            mid.Sort((a, b) => System.Array.IndexOf(Voices, a.Module).CompareTo(System.Array.IndexOf(Voices, b.Module)));

            var row = new List<Judge>(pro);
            row.AddRange(mid);
            row.AddRange(con);
            for (int i = 0; i < row.Count; i++)
            {
                float k = row.Count == 1 ? 0.5f : i / (float)(row.Count - 1);
                float a = Mathf.Lerp(BenchFrom, BenchTo, k) * Mathf.Deg2Rad;
                row[i].Seat = Hall + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * BenchRadius;
                if (string.IsNullOrEmpty(row[i].Title)) row[i].Title = UI.SelectedUnitPanelUI.VoiceName(row[i].Module, null);
            }
            return row;
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

            if (j.Abstains)
            {
                switch (System.Array.IndexOf(Voices, j.Module) % 4)
                {
                    case 0:  return Loc.T("Меня это не касается.");
                    case 1:  return Loc.T("Мне нечего сказать.");
                    case 2:  return Loc.T("Пусть решают другие.");
                    default: return Loc.T("Это не мой вопрос.");
                }
            }

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
                return Loc.F("Приказ отвергнут. Против — {0}. За — {1}. Весы склонились к «против».", against, pro);
            }

            if (string.IsNullOrEmpty(pro)) return Loc.T("Приказ исполнен.");
            if (string.IsNullOrEmpty(against)) return Loc.F("Приказ исполнен. За — {0}. Против — никто.", pro);
            return Loc.F("Приказ исполнен. За — {0}. Против — {1}. Весы склонились к «за».", pro, against);
        }

        /// <summary>Что показал компас — натура души, а не это решение.</summary>
        private static string CompassWords(Warrior w, MoralType m)
        {
            switch (m)
            {
                case MoralType.Pious:
                    return Grammar.Pick(w.Gender,
                        Loc.T("Над всем — его моральный компас: слева добро, вверху середина, справа зло. Стрелка встала на добро: он благочестив."),
                        Loc.T("Над всем — её моральный компас: слева добро, вверху середина, справа зло. Стрелка встала на добро: она благочестива."));
                case MoralType.Vicious:
                    return Grammar.Pick(w.Gender,
                        Loc.T("Над всем — его моральный компас: слева добро, вверху середина, справа зло. Стрелка встала на зло: он злобен."),
                        Loc.T("Над всем — её моральный компас: слева добро, вверху середина, справа зло. Стрелка встала на зло: она злобна."));
                default:
                    return Grammar.Pick(w.Gender,
                        Loc.T("Над всем — его моральный компас: слева добро, вверху середина, справа зло. Стрелка встала посередине: он ни добр, ни зол."),
                        Loc.T("Над всем — её моральный компас: слева добро, вверху середина, справа зло. Стрелка встала посередине: она ни добра, ни зла."));
            }
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

        private static GameObject Build(List<Judge> judges, ShellType accused, out SoulCourt host,
                                        out Light orderFire, out Transform orderFlame)
        {
            var hall = new GameObject("Суд души");
            hall.transform.position = Hall;
            host = hall.AddComponent<SoulCourt>();
            Throat(hall, 0.62f);   // голос самого суда — вступление и вердикт

            var stone = Lit(new Color(0.075f, 0.07f, 0.075f), 0.1f);
            var wall = Lit(new Color(0.10f, 0.09f, 0.085f), 0.05f);
            var wood = Lit(new Color(0.20f, 0.12f, 0.07f), 0.3f);
            var dark = Lit(new Color(0.11f, 0.07f, 0.045f), 0.35f);
            var cloth = Lit(new Color(0.22f, 0.04f, 0.04f), 0.05f);
            var chair = Lit(new Color(0.045f, 0.028f, 0.022f), 0.05f);
            var wax = Lit(new Color(0.78f, 0.72f, 0.60f), 0.2f);
            var paper = Lit(new Color(0.82f, 0.76f, 0.62f), 0.05f);
            var t = hall.transform;

            // Пол и стена за кафедрой.
            Piece(t, PrimitiveType.Cube, "Пол", Hall + new Vector3(0f, -0.05f, 1.2f), new Vector3(12f, 0.1f, 9f), stone);
            Piece(t, PrimitiveType.Cube, "Стена", Hall + new Vector3(0f, 3.6f, WallZ + 0.15f), new Vector3(12f, 7.2f, 0.3f), wall);
            // Полотнище под весами и компасом — как герб над судом.
            Piece(t, PrimitiveType.Cube, "Полотнище", Hall + new Vector3(0f, 4.15f, WallZ - 0.02f), new Vector3(1.9f, 3.9f, 0.04f), cloth);
            foreach (float x in new[] { -4.6f, 4.6f })
                Piece(t, PrimitiveType.Cube, "Колонна", Hall + new Vector3(x, 3.4f, WallZ - 0.4f), new Vector3(0.6f, 6.8f, 0.6f), wall);

            // Кафедра: на каждого судью — помост, своя доска стойки и высокое кресло за спиной.
            for (int i = 0; i < judges.Count; i++)
            {
                var j = judges[i];
                Vector3 dir = Flat(j.Seat - Hall);
                var face = Quaternion.LookRotation(-dir);
                Piece(t, PrimitiveType.Cube, "Помост", j.Seat + dir * 0.05f + Vector3.up * (Dais * 0.5f),
                      new Vector3(0.84f, Dais, 1.2f), dark).transform.rotation = face;
                Piece(t, PrimitiveType.Cube, "Кафедра", j.Seat - dir * 0.5f + Vector3.up * (DeskTop * 0.5f),
                      new Vector3(0.80f, DeskTop, 0.22f), wood).transform.rotation = face;
                Piece(t, PrimitiveType.Cube, "Кафедра, верх", j.Seat - dir * 0.44f + Vector3.up * (DeskTop + 0.02f),
                      new Vector3(0.84f, 0.04f, 0.42f), dark).transform.rotation = face;
                // Спинка тёмная и матовая: светлые спинки на кадре 05.11 стояли забором.
                Piece(t, PrimitiveType.Cube, "Спинка кресла", j.Seat + dir * 0.42f + Vector3.up * (Dais + 1.05f),
                      new Vector3(0.56f, 2.1f, 0.08f), chair).transform.rotation = face;
                Piece(t, PrimitiveType.Cube, "Навершие", j.Seat + dir * 0.42f + Vector3.up * (Dais + 2.15f),
                      new Vector3(0.16f, 0.16f, 0.10f), wood).transform.rotation = face * Quaternion.Euler(0f, 0f, 45f);

                var root = new GameObject("Судья — " + j.Module);
                root.transform.SetParent(t, false);
                root.transform.position = j.Seat + Vector3.up * Dais;
                root.transform.rotation = Quaternion.LookRotation(Flat(Hall - j.Seat));
                j.Voice = Throat(root, 0.70f + 0.04f * i);

                // Шарнир в поясе: верх тела кланяется, ноги за кафедрой.
                j.Waist = new GameObject("Пояс").transform;
                j.Waist.SetParent(root.transform, false);
                j.Waist.localPosition = Vector3.up * 0.95f;
                var holder = new GameObject("Тело судьи").transform;
                holder.SetParent(j.Waist, false);
                holder.localPosition = Vector3.down * 0.95f;
                var body = SinbinderPlayer.Judge(holder, Glow(j.Module) * (j.Abstains ? 0.55f : 1f));
                if (body != null)
                {
                    j.Anims = body.GetComponentsInChildren<Animator>();
                    foreach (var a in j.Anims) a.updateMode = AnimatorUpdateMode.UnscaledTime;
                }

                // Свеча — на кафедре перед ним, его цвета: на общем плане видно, чей огонь чей.
                j.Fire = Candle(t, j.Seat - dir * 0.36f + Vector3.up * (DeskTop + 0.04f), 0.13f,
                                Color.Lerp(new Color(1f, 0.72f, 0.42f), Glow(j.Module), 0.75f), wax, out j.Flame, 1f);
                j.Fire.intensity = 0.35f;
                j.Fire.range = 2.2f;
            }

            // Подсудимый — он сам, за оградкой посреди зала, лицом к суду.
            var dock = new GameObject("Скамья подсудимого").transform;
            dock.SetParent(t, false);
            dock.position = Hall;
            foreach (var (x, z) in new[] { (-0.6f, -0.55f), (0.6f, -0.55f), (-0.6f, 0.55f), (0.6f, 0.55f) })
                Piece(dock, PrimitiveType.Cube, "Столбик", Hall + new Vector3(x, 0.5f, z), new Vector3(0.08f, 1.0f, 0.08f), wood);
            Piece(dock, PrimitiveType.Cube, "Перила", Hall + new Vector3(0f, 0.98f, 0.55f), new Vector3(1.28f, 0.06f, 0.08f), dark);
            Piece(dock, PrimitiveType.Cube, "Перила", Hall + new Vector3(-0.6f, 0.98f, 0f), new Vector3(0.08f, 0.06f, 1.18f), dark);
            Piece(dock, PrimitiveType.Cube, "Перила", Hall + new Vector3(0.6f, 0.98f, 0f), new Vector3(0.08f, 0.06f, 1.18f), dark);
            var who = new GameObject("Подсудимый");
            who.transform.SetParent(dock, false);
            who.transform.position = Hall;
            who.transform.rotation = Quaternion.identity;
            var self = WarriorLook.Build(who, accused, 1.72f, 1f, 1.72f * 0.62f);
            if (self != null)
                foreach (var a in self.GetComponentsInChildren<Animator>()) a.updateMode = AnimatorUpdateMode.UnscaledTime;

            // Приказ — лист на аналое между подсудимым и судом, и его свеча.
            Piece(t, PrimitiveType.Cube, "Аналой", Hall + new Vector3(0f, 0.45f, 1.45f), new Vector3(0.36f, 0.9f, 0.3f), wood);
            Piece(t, PrimitiveType.Cube, "Приказ", Hall + new Vector3(0f, 0.91f, 1.45f), new Vector3(0.30f, 0.01f, 0.22f), paper);
            orderFire = Candle(t, Hall + new Vector3(0.12f, 0.9f, 1.52f), 0.2f, new Color(1f, 0.78f, 0.5f), wax, out orderFlame);
            orderFire.intensity = 1.6f;
            orderFire.range = 3.2f;

            // Подсудимый — в луче сверху: в суде на него смотрят все (на кадре 05.11 он тонул во тьме).
            var beam = new GameObject("Свет на подсудимого").AddComponent<Light>();
            beam.transform.SetParent(t, false);
            beam.transform.position = Hall + new Vector3(0f, 5.2f, -1.1f);
            beam.transform.rotation = Quaternion.LookRotation(Hall + new Vector3(0f, 0.9f, 0.3f) - beam.transform.position);
            beam.type = LightType.Spot;
            beam.spotAngle = 36f;
            beam.innerSpotAngle = 18f;
            beam.range = 8f;
            beam.intensity = 7f;
            beam.color = new Color(1f, 0.92f, 0.80f);
            beam.shadows = LightShadows.None;

            // Холодный верхний свет на кафедру — чтобы зал читался залом, а не кругом в темноте.
            var fill = new GameObject("Свет над кафедрой").AddComponent<Light>();
            fill.transform.SetParent(t, false);
            fill.transform.position = Hall + new Vector3(0f, 6.2f, 1.0f);
            fill.type = LightType.Point;
            fill.range = 10f;
            fill.intensity = 0.6f;
            fill.color = new Color(0.55f, 0.60f, 0.80f);
            fill.shadows = LightShadows.None;

            host._needle = Dial(t);
            host._compass = Compass(t);
            return hall;
        }

        /// <summary>
        /// Моральный компас: круг, три сектора — добро слева, середина вверху,
        /// зло справа — и стрелка с острием. Встаёт на натуру души.
        /// </summary>
        private static Transform Compass(Transform parent)
        {
            var root = new GameObject("Моральный компас").transform;
            root.SetParent(parent, false);
            root.position = Hall + CompassAt;
            root.rotation = Quaternion.identity;

            var rim = Piece(root, PrimitiveType.Cylinder, "Обод", root.position + Vector3.forward * 0.05f,
                            new Vector3(1.36f, 0.02f, 1.36f), Lit(new Color(0.42f, 0.33f, 0.16f), 0.6f));
            rim.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var face = Piece(root, PrimitiveType.Cylinder, "Круг", root.position + Vector3.forward * 0.03f,
                             new Vector3(1.22f, 0.025f, 1.22f), Lit(new Color(0.06f, 0.055f, 0.065f), 0.35f));
            face.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Сектора: дуга из делений и самоцвет на краю. Угол — по часовой от верха.
            foreach (var (centre, tone) in new[] { (-60f, PiousTone), (0f, NeutralTone), (60f, ViciousTone) })
            {
                for (int k = -2; k <= 2; k++)
                {
                    float a = centre + k * 9f;
                    var at = root.position - Vector3.forward * 0.01f
                           + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad), 0f) * 0.47f;
                    var tick = Piece(root, PrimitiveType.Cube, "Деление", at,
                                     new Vector3(0.05f, k == 0 ? 0.16f : 0.09f, 0.01f), Glowing(tone, 1f));
                    tick.transform.rotation = Quaternion.AngleAxis(-a, Vector3.forward);
                }
                var gem = root.position - Vector3.forward * 0.02f
                        + new Vector3(Mathf.Sin(centre * Mathf.Deg2Rad), Mathf.Cos(centre * Mathf.Deg2Rad), 0f) * 0.64f;
                Piece(root, PrimitiveType.Sphere, "Самоцвет", gem, Vector3.one * 0.11f, Glowing(tone, 1.2f));
            }

            var pivot = new GameObject("Стрелка компаса").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0f, 0f, -0.05f);
            var steel = Glowing(new Color(0.85f, 0.87f, 0.92f), 1f);
            Piece(pivot, PrimitiveType.Cube, "Остриё", pivot.position + Vector3.up * 0.22f, new Vector3(0.045f, 0.44f, 0.012f), steel)
                .transform.rotation = Quaternion.identity;
            var tip = Piece(pivot, PrimitiveType.Cube, "Наконечник", pivot.position + Vector3.up * 0.44f, new Vector3(0.075f, 0.075f, 0.012f), steel);
            tip.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Piece(pivot, PrimitiveType.Cube, "Хвост", pivot.position - Vector3.up * 0.1f, new Vector3(0.07f, 0.2f, 0.012f),
                  Glowing(new Color(0.3f, 0.3f, 0.34f), 1f));
            var hub = Piece(pivot, PrimitiveType.Cylinder, "Ось", pivot.position, new Vector3(0.09f, 0.012f, 0.09f), steel);
            hub.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var go = new GameObject("Свет компаса");
            go.transform.SetParent(root, false);
            go.transform.position = root.position - Vector3.forward * 0.9f + Vector3.up * 0.2f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2.0f;
            light.intensity = 0.6f;
            light.color = new Color(0.85f, 0.80f, 0.70f);
            light.shadows = LightShadows.None;
            return pivot;
        }

        /// <summary>Весы: тёмный циферблат, двенадцать делений, одна стрелка.</summary>
        private static Transform Dial(Transform parent)
        {
            var root = new GameObject("Весы").transform;
            root.SetParent(parent, false);
            root.position = Hall + ScalesAt;
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

        // ──────────────────────────────────
        // Жесты
        // ──────────────────────────────────

        /// <summary>Движение тела судьи, если у его модели такое есть.</summary>
        private static void Play(Judge j, string state)
        {
            if (j.Anims == null) return;
            int hash = Animator.StringToHash(state);
            foreach (var a in j.Anims)
                if (a != null && a.runtimeAnimatorController != null && a.HasState(0, hash))
                    a.CrossFadeInFixedTime(hash, 0.25f);
        }

        /// <summary>
        /// Приветствие — у каждого голоса своё (автор, 05.11: «приветственные
        /// анимации, желательно разные»): движение тела и жест корпусом в поясе.
        /// Верность и Совесть кланяются, Гордыня откидывается, Гнев подаётся
        /// вперёд, Зависть косится, Лень садится.
        /// </summary>
        private static IEnumerator Greet(Judge j)
        {
            string state = "Idle";
            float pitch = 0f, roll = 0f, seconds = 1.6f;
            int beats = 1;
            switch (j.Module)
            {
                case "Loyalty":  pitch = 32f; seconds = 1.7f; break;                    // поклон владыке
                case "Morality": pitch = 42f; seconds = 2.0f; break;                    // глубокий поклон
                case "Patience": pitch = 20f; seconds = 2.6f; break;                    // медленный поклон
                case "Fear":     pitch = -9f; seconds = 0.5f; beats = 2; break;         // вздрагивает
                case "Memory":   state = "Talk"; pitch = 9f; break;                     // кивает, вспоминая
                case "Virtue":   state = "Gather"; pitch = 14f; break;                  // протягивает руку
                case "Greed":    state = "Gather"; pitch = 6f; seconds = 1.2f; break;   // сгребает
                case "Pride":    state = "Challenge"; pitch = -11f; seconds = 1.9f; break; // нос кверху
                case "Wrath":    state = "Draw"; pitch = 14f; seconds = 0.9f; break;    // рывок вперёд
                case "Envy":     state = "Talk"; roll = 11f; seconds = 1.8f; break;     // косится по сторонам
                case "Lust":     state = "Strum"; roll = 7f; seconds = 2.0f; beats = 2; break; // покачивается
                case "Gluttony": state = "Dig"; pitch = 16f; break;                     // тянется к блюду
                case "Sloth":    state = "Sit"; seconds = 1.8f; break;                  // садится — лень вставать
            }
            Play(j, state);
            for (int b = 0; b < beats; b++)
            {
                for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                {
                    if (j.Waist == null) yield break;
                    float k = t / seconds;
                    // Туда — задержка — обратно; покачивание — синусом.
                    float lean = k < 0.3f ? Mathf.SmoothStep(0f, 1f, k / 0.3f)
                               : k < 0.65f ? 1f : Mathf.SmoothStep(1f, 0f, (k - 0.65f) / 0.35f);
                    float sway = Mathf.Sin(k * Mathf.PI * 2f);
                    j.Waist.localRotation = Quaternion.Euler(pitch * lean, 0f, roll * sway);
                    yield return null;
                }
            }
            if (j.Waist != null) j.Waist.localRotation = Quaternion.identity;
            yield return new WaitForSecondsRealtime(0.6f);
            Play(j, "Idle");
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
