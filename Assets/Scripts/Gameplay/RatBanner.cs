// Assets/Scripts/Gameplay/RatBanner.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.UI;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Знамя «Крыс» — имя отряда (docs/32-CAMP.md §12). Автор, 23 октября:
    /// музыке «Крыс» не хватает «явного образа» — «кто-то из лагеря стоял
    /// со знаменем, где изображена голова крысы, и предложил Игроку назваться
    /// Крысами»; «если он погибает, можно подбежать и забрать знамя. Тогда оно
    /// будет в склепе»; «если Хорь выживет — то пускай сам и носится со своим
    /// знаменем».
    ///
    /// Три вещи:
    /// 1. <b>Сцена у огня.</b> Провожатый дошёл, в лагере стихло — Хорь подходит
    ///    сам и предлагает имя. Ответ — отношение, а не исход: имя их, а не его,
    ///    и «нет» его не отменяет (приказ — один голос, docs/02-AOS.md).
    /// 2. <b>Знамя носят.</b> Вещь гардероба на спине того, кто несёт
    ///    (<c>wear.py</c> <c>rat_banner</c>), и идёт с ним само — в набег,
    ///    в бегство, в склеп. Носящий пал — знамя остаётся воткнутым там, где он
    ///    упал; Греховод подошёл — поднял и несёт дальше сам.
    /// 3. <b>Кто донёс — с тем и в склепе.</b> Носящий — имя из состава
    ///    (<see cref="SquadRoster"/> переносит отряд между сценами по именам);
    ///    спавнер надевает знамя, когда тело встало (<see cref="Dress"/>).
    ///
    /// Ставит себя спавнер отряда (<see cref="PrologueCampSpawner"/>) — в лагере
    /// и в склепе; сцену пересобирать не нужно.
    /// </summary>
    public class RatBanner : MonoBehaviour
    {
        /// <summary>Вещь гардероба (<see cref="Wardrobe"/>, кость Chest).</summary>
        public const string Item = "RatBanner";

        /// <summary>Знаменосец — вор, знамя из краденой ткани Ордена (32-CAMP §12, решение автора).</summary>
        public static readonly string Bearer = Loc.N("Одноглазый Хорь");

        /// <summary>Кто несёт — имя из состава. Пусто — знамя у Греховода или потеряно.</summary>
        public static string Holder { get; private set; } = Bearer;

        /// <summary>Несёт Греховод: поднял за павшим.</summary>
        public static bool BySinbinder { get; private set; }

        /// <summary>Сцена у огня прошла (или пропущена — Хоря в лагере не было).</summary>
        public static bool Named { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Reset();

        /// <summary>Новая игра: знамя снова у Хоря, имени ещё нет.</summary>
        public static void Reset()
        {
            Holder = Bearer;
            BySinbinder = false;
            Named = false;
            _worn = null;
            _dropped = null;
        }

        /// <summary>Носит ли знамя этот: Греховод — если поднял, воин — если он и есть носящий.</summary>
        public static bool Carries(Warrior w)
        {
            if (w == null) return false;
            if (w is SinbinderPlayer) return BySinbinder;
            return !BySinbinder && !string.IsNullOrEmpty(Holder) && w.DisplayName == Holder;
        }

        /// <summary>
        /// Носящий пал: знамя больше ничьё и лежит там, где он упал.
        /// Не он — ничего; Греховода не касается: его гибель кончает демо.
        /// </summary>
        public static bool Fell(string name)
        {
            if (BySinbinder || string.IsNullOrEmpty(Holder) || name != Holder) return false;
            Holder = null;
            return true;
        }

        /// <summary>Греховод поднял знамя павшего.</summary>
        public static void PickedUp()
        {
            Holder = null;
            BySinbinder = true;
        }

        // Вещь на теле и вещь на земле. Живут в своей сцене; при смене сцены
        // ссылки становятся пустыми, и знамя надевается заново (Dress).
        private static GameObject _worn;
        private static GameObject _dropped;
        private static Vector3 _droppedAt;

        /// <summary>
        /// Надеть знамя, если этот — носящий. Зовёт спавнер, когда тело
        /// встало: в лагере, в склепе и для вернувшихся с вылазки.
        /// Модели ещё нет (не собран <c>wear.py</c>) — молча без неё:
        /// об отсутствии один раз скажет <see cref="Wardrobe"/>.
        /// </summary>
        public static void Dress(Warrior w)
        {
            if (!Carries(w)) return;
            if (_worn != null && _worn.transform.IsChildOf(w.transform)) return;
            _worn = Wardrobe.Wear(w.gameObject, Item);
        }

        private CombatManager _subscribed;
        private float _nextHeal;

        // ---------- числа ----------

        private const float QuietFor = 4f;       // сколько секунд в лагере должно быть тихо, прежде чем Хорь пойдёт
        private const float TalkReach = 3.5f;    // с какого расстояния он заговаривает, метров
        private const float StepBack = 1.6f;     // где встаёт перед Греховодом, метров
        private const float PickReach = 1.4f;    // с какого расстояния Греховод поднимает знамя, метров
        private const float PlantDepth = 0.25f;  // насколько древко ушло в землю, метров
        private const float PlantLean = 9f;      // наклон воткнутого древка, градусов

        // Строки — 47-TEXTS.md §9 (автор: «да», 23.10).
        private static readonly string Offer = Loc.N("Владыка, мы тут с Гуртом подумали. Раз уж мы всё бегаем да прячемся по углам — пусть хоть знамя будет честное.");
        private static readonly string Show = Loc.N("Крысы. А что? Живучие.");
        private static readonly string Yes = Loc.N("Крысы так крысы.");
        private static readonly string No = Loc.N("Мы не крысы.");
        private static readonly string OnYes = Loc.N("Вот и я говорю. Гурт, слыхал? Мы теперь Крысы.");
        private static readonly string OnNo = Loc.N("Вы — нет, Владыка. А мы — да.");
        private static readonly string Thought = Loc.N("Крысы. Хуже имени для того, кто ушёл от Смерти, не придумать. И точнее — тоже.");

        void Start()
        {
            // Сцена — только в лагере: там начинается пролог (CampOpening).
            if (!Named && FindFirstObjectByType<CampOpening>() != null)
                StartCoroutine(Naming());
        }

        void OnDestroy()
        {
            if (_subscribed != null) _subscribed.OnAnyDeath -= OnDeath;
        }

        void Update()
        {
            // Менеджер боя ставит сцена; подписываемся, когда он есть.
            var combat = CombatManager.Instance;
            if (combat != null && combat != _subscribed)
            {
                if (_subscribed != null) _subscribed.OnAnyDeath -= OnDeath;
                combat.OnAnyDeath += OnDeath;
                _subscribed = combat;
            }

            PickUp();

            // Модель тела пересобирают (титул, оболочка) — знамя с ней уходит.
            // Раз в секунду: носящий жив, а знамени на нём нет — надеть снова.
            if (Time.unscaledTime >= _nextHeal)
            {
                _nextHeal = Time.unscaledTime + 1f;
                Heal();
            }
        }

        private static void Heal()
        {
            if (_dropped != null) return;
            if (BySinbinder)
            {
                if (SinbinderPlayer.Exists) Dress(SinbinderPlayer.Instance);
                return;
            }
            var holder = Find(Holder);
            if (holder != null) Dress(holder);
        }

        // ---------- пал и поднял ----------

        private void OnDeath(Damageable killed, GameObject killer)
        {
            var w = killed != null ? killed.Warrior : null;
            if (w == null || w is SinbinderPlayer || !Fell(w.DisplayName)) return;

            Plant(w.transform.position);
            Log(Loc.F("Знамя «Крыс» осталось там, где пал {0}.", Loc.Name(w.DisplayName)));
        }

        /// <summary>
        /// Воткнуть знамя там, где упал носящий. Стоймя, а не лёжа: сверху
        /// древко с тряпкой видно издали — это метка места, к которой бегут.
        /// </summary>
        private static void Plant(Vector3 at)
        {
            if (_worn == null) return;

            var t = _worn.transform;
            t.SetParent(null, true);

            var look = _worn.GetComponentInChildren<Renderer>();
            if (look != null)
            {
                // Нижний конец древка — на PlantDepth под землёй.
                float lift = look.bounds.min.y - (at.y - PlantDepth);
                t.position -= Vector3.up * lift;
                var foot = new Vector3(look.bounds.center.x, at.y, look.bounds.center.z);
                t.RotateAround(foot, Vector3.right, PlantLean);
                _droppedAt = foot;
            }
            else
            {
                _droppedAt = at;
            }

            _dropped = _worn;
            _worn = null;
        }

        private static void PickUp()
        {
            if (_dropped == null || !SinbinderPlayer.Exists) return;
            if (CampFocus.GroundDistance(SinbinderPlayer.Where, _droppedAt) > PickReach) return;

            Destroy(_dropped);
            _dropped = null;
            PickedUp();
            Dress(SinbinderPlayer.Instance);
            Log(Loc.T("Греховод поднял знамя «Крыс»."));
        }

        // ---------- сцена у огня ----------

        private IEnumerator Naming()
        {
            // Провожатый дошёл (CampOpening) — и подождать, пока в лагере
            // стихнет: Карган зовёт к столу, идёт открытие, кто-то говорит.
            while (!CampOpening.EscortArrived) yield return null;

            float quiet = 0f;
            while (quiet < QuietFor)
            {
                quiet = Busy() ? 0f : quiet + Time.unscaledDeltaTime;
                yield return null;
            }

            var bearer = Find(Bearer);
            if (bearer == null)
            {
                Debug.Log("[ЗНАМЯ] Хоря в лагере нет — сцены у огня не будет.");
                Named = true;
                yield break;
            }

            // Подходит сам, как провожатый. Приказ — через настоящий конвейер:
            // не дошёл с первого раза — просим снова, пока не окажется рядом.
            float nextAsk = 0f;
            while (bearer != null && !bearer.IsDead && !Near(bearer))
            {
                if (Time.unscaledTime >= nextAsk && !Busy())
                {
                    nextAsk = Time.unscaledTime + 2f;
                    bearer.IssueCommand(CommandKind.Move, Before(bearer));
                }
                yield return null;
            }

            while (Busy()) yield return null;
            if (bearer == null || bearer.IsDead) yield break;

            yield return Talk(bearer);
        }

        private static bool Busy()
        {
            var pause = Core.GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return true;
            if (Letterbox.Instance != null && Letterbox.Instance.Shown) return true;
            if (TalkView.Shown || OpeningReveal.Busy) return true;
            var cam = Dialogue.DialogueCameraController.Instance;
            return cam != null && cam.InDialogue;
        }

        private static bool Near(Warrior w)
            => !SinbinderPlayer.Exists
            || CampFocus.GroundDistance(w.transform.position, SinbinderPlayer.Where) <= TalkReach;

        /// <summary>Место перед Греховодом со стороны подходящего.</summary>
        private static Vector3 Before(Warrior w)
        {
            var to = SinbinderPlayer.Where;
            var from = w.transform.position;
            var step = to - from;
            step.y = 0f;
            if (step.sqrMagnitude < 0.01f) return from;
            var at = from + step.normalized * Mathf.Max(0f, step.magnitude - StepBack);
            at.y = from.y;
            return at;
        }

        private IEnumerator Talk(Warrior bearer)
        {
            var pause = Core.GamePauseController.Instance;
            int stamp = 0;
            if (pause != null) { pause.Pause(); stamp = pause.Stamp; }

            Face(bearer, SinbinderPlayer.Exists ? SinbinderPlayer.Where : bearer.transform.position + bearer.transform.forward);

            // Наезд на лицо, как в разговоре (GearPanel): полосы поднимает камера.
            var cam = Dialogue.DialogueCameraController.Instance;
            bool framed = false;
            if (cam != null && !cam.InDialogue)
            {
                cam.SaveCameraPosition();
                cam.StartCoroutine(cam.FocusOn(bearer.transform));
                framed = true;
            }
            Letterbox.Instance?.Show();

            string name = Loc.Name(bearer.DisplayName);
            var none = new List<TalkView.Option>();

            // «Мы тут с Гуртом подумали…»
            TalkView.Show(name, Loc.T(Offer), none, null);
            yield return Hold(Loc.T(Offer));

            // Поворачивается спиной — камера стоит перед ним, и в кадр входит
            // знамя: на краденой тряпке Ордена — голова крысы углём.
            Face(bearer, bearer.transform.position - bearer.transform.forward);
            int answer = 0;
            var options = new List<TalkView.Option>
            {
                new TalkView.Option { Text = "1. " + Loc.T(Yes), Act = () => answer = 1 },
                new TalkView.Option { Text = "2. " + Loc.T(No),  Act = () => answer = 2 },
            };
            TalkView.Show(name, Loc.T(Show), options, Loc.T("Выберите ответ — цифрой или мышью."));

            float auto = Time.unscaledTime + 3f;
            while (answer == 0)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) answer = 1;
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) answer = 2;
                else if (OpeningReveal.Autopilot && Time.unscaledTime >= auto) answer = 1;
                yield return null;
            }

            // Обернулся обратно — отвечает. Имя остаётся при обоих ответах.
            Face(bearer, SinbinderPlayer.Exists ? SinbinderPlayer.Where : bearer.transform.position - bearer.transform.forward);
            string reply = Loc.T(answer == 1 ? OnYes : OnNo);
            TalkView.Show(name, reply, none, null);
            yield return Hold(reply);

            // Последнее слово — мысль Греховода: про него, а не про ответ.
            TalkView.Hide();
            Letterbox.Instance?.Think(Loc.T(Thought));
            yield return Hold(Loc.T(Thought));

            Named = true;
            Log(Loc.T("Отряд назвал себя Крысами."));
            Debug.Log($"[ЗНАМЯ] Отряд назвался Крысами; ответ Греховода — {(answer == 1 ? "«крысы так крысы»" : "«мы не крысы»")}.");

            if (framed && cam != null) cam.StartCoroutine(cam.RestoreCamera());
            else Letterbox.Instance?.Hide();
            if (pause != null && pause.Stamp == stamp) pause.Resume();
        }

        /// <summary>
        /// Дать строке допечататься и прочитаться: по букве идёт она сама
        /// (Letterbox), здесь — срок по длине. F, пробел, Enter или щелчок — дальше.
        /// </summary>
        private static IEnumerator Hold(string line)
        {
            float until = Time.unscaledTime + 1.5f + (line?.Length ?? 0) * 0.045f;
            if (OpeningReveal.Autopilot) until = Time.unscaledTime + 1.5f;
            yield return null;   // нажатие, которым ответили, — не «дальше»
            while (Time.unscaledTime < until)
            {
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space)
                    || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)) yield break;
                yield return null;
            }
        }

        private static void Face(Warrior w, Vector3 point)
        {
            var d = point - w.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) w.transform.rotation = Quaternion.LookRotation(d);
        }

        // ---------- общее ----------

        /// <summary>Живой свой воин с этим именем — тот, кто в сцене.</summary>
        private static Warrior Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var w in FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.Team == Team.Player && !(w is SinbinderPlayer)
                    && w.DisplayName == name)
                    return w;
            return null;
        }

        private static void Log(string line)
            => FindFirstObjectByType<BattleLogUI>()?.Write(line);
    }
}
