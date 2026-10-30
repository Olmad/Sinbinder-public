// Assets/Scripts/Gameplay/CampfireStoke.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Полено в костёр: подошёл к огню — «F — подбросить полено», бросил —
    /// огонь вспыхивает, в небо уходит столб искр, треск густеет, а тот, кто
    /// сидит у огня ближе всех, отзывается по своей примете.
    ///
    /// Автор, 26 октября: «атмосфера, музыка, интерактивность, интересность» —
    /// «лагерь отзывается» (52-RESUME, п. 2). Это не механика, а игрушка —
    /// но игрушка, которая учит: в лагере можно трогать вещи, и люди замечают,
    /// что делает Греховод. Отзыв — через личность: у каждого из девяти своя
    /// строка (<see cref="Lines"/>), один раз за вечер; сказали все, кто рядом, —
    /// огонь просто горит. Немой Гурт отвечает жестом.
    ///
    /// Ставит себя сам на костёр — самый широкий живой огонь
    /// (<see cref="FireFlicker"/>, свет шире 15 м), как <see cref="CampAir"/>.
    /// F ведёт <see cref="UI.GearPanel"/>: полено — после сундука и стола
    /// с картами, до оружия со стойки. На тревоге и в разгроме не предлагается:
    /// там не до дров.
    /// </summary>
    public class CampfireStoke : MonoBehaviour
    {
        /// <summary>Свет костра шире этого, м; у факелов — девять.</summary>
        private const float CampfireRange = 15f;

        /// <summary>С какого расстояния по земле дотянуться до огня, м.</summary>
        private const float Reach = 2.8f;

        /// <summary>Кто сидит у огня, м: дальше — не видел, как бросили.</summary>
        private const float Heard = 8f;

        /// <summary>Не чаще, с: полено должно успеть разгореться.</summary>
        private const float Cooldown = 6f;

        /// <summary>Вспышка: на сколько ярче огонь сразу после полена и сколько гаснет, с.</summary>
        private const float FlareGain = 0.55f;
        private const float FlareSeconds = 5f;

        /// <summary>Поленьев в огне разом — старшее догорает первым.</summary>
        private const int MaxLogs = 3;

        /// <summary>Свежее полено — тёплое дерево, а не белый материал очага.</summary>
        private static readonly Color Fresh = new(0.50f, 0.34f, 0.21f);

        /// <summary>
        /// Что скажет у огня каждый из девяти. Черновик облачной, 26 октября
        /// (28-ORDERS, «Полено в костёр» — таблицей автору). Ключ — имя, как
        /// у <see cref="Signature"/>.
        /// </summary>
        private static readonly Dictionary<string, string> Lines = new()
        {
            [Loc.N("Карган Старый Ворон")] = Loc.N("Хорошо горит. Ворон тоже греется — только виду не подаёт."),
            [Loc.N("Вейн Тихий")] = Loc.N("(шёпотом) Светлее. Теперь и почитать можно."),
            [Loc.N("Марга Копатель")] = Loc.N("Полено-то моё, владыка. Я его нашёл. Ну да ладно — грейтесь."),
            // Котёл — у своего огня, поодаль: у этого костра о нём не говорят (автор, 27.10:
            // «подкидываю дрова в основной костёр, а похлёбка вдалеке быстрее варится»).
            [Loc.N("Брат Хальд")] = Loc.N("Сосна трещит, владыка. Для отвара я беру берёзу — а греться и так сойдёт."),
            [Loc.N("Одноглазый Хорь")] = Loc.N("Ярко. Такой огонь в бору видно далеко. Даже одним глазом."),
            [Loc.N("Толстый Ю")] = Loc.N("На таком жару и хлеб подрумянить можно. Был бы хлеб."),
            [Loc.N("Лиска")] = Loc.N("Я считаю поленья, владыка. На всякий случай."),
            [Loc.N("Немой Гурт")] = Loc.N("(молча подвигается ближе к огню)"),
            [Loc.N("Косой Ждан")] = Loc.N("Дайте я брошу, владыка. Отсюда точно попаду. Наверное."),
        };

        private static CampfireStoke _instance;
        private static readonly HashSet<string> Said = new();

        private Light _light;
        private ParticleSystem _sparks;
        private ParticleSystem _flame;
        private float _flameSize, _flameRate;
        private Material _wood;
        private float _flare;
        private float _ready;
        private int _thrown;
        private readonly Queue<GameObject> _logs = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _instance = null;
            Said.Clear();
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void First() => Install();

        private static void OnScene(Scene scene, LoadSceneMode mode)
        {
            Said.Clear();
            Install();
        }

        private static void Install()
        {
            if (Object.FindFirstObjectByType<CampfireStoke>() != null) return;

            Light fire = null;
            foreach (var f in Object.FindObjectsByType<FireFlicker>(FindObjectsSortMode.InstanceID))
            {
                var l = f.GetComponent<Light>();
                if (l != null && l.range >= CampfireRange && (fire == null || l.range > fire.range)) fire = l;
            }
            if (fire != null) fire.gameObject.AddComponent<CampfireStoke>();
        }

        void Awake()
        {
            _instance = this;
            _light = GetComponent<Light>();

            // Сборщик сцены кладёт рядом со светом искры и модель костра
            // (DemoSceneBuilder.Campfire): искры — те же, только гуще;
            // дерево полена — то же, из чего сложены дрова в огне.
            var root = transform.parent != null ? transform.parent : transform;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name == "Искры" && _sparks == null) _sparks = ps;
                if (ps.name == "Пламя" && _flame == null)
                {
                    _flame = ps;
                    _flameSize = ps.main.startSizeMultiplier;
                    _flameRate = ps.emission.rateOverTimeMultiplier;
                }
            }
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterials.Length > 0 && r.sharedMaterials[0] != null) { _wood = r.sharedMaterials[0]; break; }
        }

        void Start() => Pile();

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Вспышка от полена сейчас: 1 — только что бросили, 0 — догорела. Ей разгораются угли (EmberBed).</summary>
        public static float Flare => _instance != null ? _instance._flare : 0f;

        /// <summary>Костёр, до которого Греховод дотянется сейчас. Нет — null.</summary>
        public static CampfireStoke Nearest()
        {
            if (_instance == null || !SinbinderPlayer.Exists || Inspectable.Telling) return null;
            if (CrystalBall.Raised || RaidEvent.Running) return null;
            if (Time.time < _instance._ready || !Carrying) return null;
            var pause = GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return null;

            var at = _instance.Ground();
            return CampFocus.GroundDistance(SinbinderPlayer.Where, at) <= Reach ? _instance : null;
        }

        public string Prompt => Loc.T("F — подбросить полено");

        /// <summary>Бросить полено: вспышка, искры, треск — и кто-нибудь отзовётся.</summary>
        public void Feed()
        {
            if (Time.time < _ready) return;
            _ready = Time.time + Cooldown;
            if (_held != null) { Destroy(_held); _held = null; }
            StartCoroutine(Throw(_thrown++));
        }

        // ---------- поленница (КТ1 п. 16: «взять полено, подкинуть в костёр») ----------
        // Полено бралось из воздуха: F у огня — и оно падало в костёр. Теперь у огня сложена поленница:
        // F у неё — полено в руках, F у огня — в огонь, G — положить обратно (HandsDrop).

        /// <summary>С какого расстояния по земле дотянуться до поленницы, м.</summary>
        private const float PileReach = 1.8f;

        /// <summary>Поленница от середины огня, м: за кругом брёвен, но в его свете.</summary>
        private const float PileDistance = 3.4f;

        private Transform _pile;
        private GameObject _held;

        /// <summary>Греховод несёт полено от поленницы к огню.</summary>
        public static bool Carrying => _instance != null && _instance._held != null;

        /// <summary>Поленница, до которой Греховод дотянется сейчас, с пустыми руками. Нет — null.</summary>
        public static CampfireStoke PileInReach()
        {
            if (_instance == null || _instance._pile == null || Carrying) return null;
            if (!SinbinderPlayer.Exists || Inspectable.Telling || !HandsFree()) return null;
            if (CrystalBall.Raised || RaidEvent.Running) return null;
            var pause = GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return null;
            return CampFocus.GroundDistance(SinbinderPlayer.Where, _instance._pile.position) <= PileReach ? _instance : null;
        }

        private static bool HandsFree() => RecordHands.Empty && !Remains.Carrying;

        public Vector3 PileCentre => _pile.position + Vector3.up * 0.35f;
        public string PilePrompt => Loc.T("F — взять полено");

        /// <summary>Взять полено: поперёк груди, пока не бросит в огонь или не положит обратно.</summary>
        public void TakeLog()
        {
            var hero = SinbinderPlayer.Instance;
            if (hero == null || _held != null) return;
            _held = Billet("Полено в руках", 0);
            _held.transform.SetParent(hero.transform, false);
            _held.transform.localPosition = new Vector3(0f, 1.05f, 0.42f);
            _held.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Audio.Sfx.Stoke(hero.transform, hero.transform.position);
            Debug.Log("[КОСТЁР] полено из поленницы — в руках");
        }

        /// <summary>G: положить полено обратно на поленницу.</summary>
        public static void PutBack()
        {
            if (_instance == null || _instance._held == null) return;
            Destroy(_instance._held);
            _instance._held = null;
        }

        /// <summary>Что стоит на месте поленницы: вещь не шире восьми метров и не сам костёр. Свободно — null.</summary>
        private Collider Blocker(Vector3 at)
        {
            var fire = transform.parent != null ? transform.parent : transform;
            foreach (var c in Physics.OverlapSphere(at + Vector3.up * 0.6f, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null || c.transform.IsChildOf(fire)) continue;
                var size = c.bounds.size;
                if (size.x > 8f || size.z > 8f) continue;
                return c;
            }
            return null;
        }

        /// <summary>Чурка без коллайдера, тонированная деревом; n — на сколько отличается от соседних.</summary>
        private GameObject Billet(string name, int n)
        {
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = name;
            var col = log.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var renderer = log.GetComponent<MeshRenderer>();
            if (_wood != null) renderer.sharedMaterial = _wood;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tint = Fresh * (0.85f + 0.3f * ((n * 0.618f) % 1f));
            tint.a = 1f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(block);
            log.transform.localScale = new Vector3(0.13f, 0.34f, 0.13f);
            return log;
        }

        /// <summary>
        /// Сложить поленницу: где у огня свободно и дальше всего от брёвен для сидения. Восемь сторон по кругу,
        /// без жребия; занято (палатка, котёл) — следующая. Три ряда: четыре, три, два.
        /// </summary>
        private void Pile()
        {
            var fire = Ground();
            Vector3 best = Vector3.zero;
            float bestScore = -1f;
            for (int i = 0; i < 8; i++)
            {
                float a = (200f + i * 45f) * Mathf.Deg2Rad;
                var at = fire + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * PileDistance;
                // Занято — вещью, а не землёй, холмом или зоной: всё шире восьми метров не мешает
                // (проход w3110e: простая проверка сферой находила занятым каждое место).
                var blocker = Blocker(at);
                if (blocker != null) { Debug.Log($"[КОСТЁР] поленница: сторона {i} занята «{blocker.name}»"); continue; }
                float score = float.MaxValue;
                foreach (var seat in Object.FindObjectsByType<LogSeat>(FindObjectsSortMode.None))
                    score = Mathf.Min(score, CampFocus.GroundDistance(seat.Centre, at));
                if (score > bestScore) { bestScore = score; best = at; }
            }
            if (bestScore < 0f) { Debug.LogWarning("[КОСТЁР] поленнице нет места у огня — все восемь сторон заняты"); return; }

            _pile = new GameObject("Поленница").transform;
            _pile.SetParent(transform.parent != null ? transform.parent : transform, true);
            _pile.position = best;
            var along = fire - best; along.y = 0f;
            _pile.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, along.normalized));

            int n = 0;
            int[] rows = { 4, 3, 2 };
            for (int r = 0; r < rows.Length; r++)
                for (int k = 0; k < rows[r]; k++)
                {
                    var log = Billet("Полено", n++);
                    log.transform.SetParent(_pile, false);
                    log.transform.localPosition = new Vector3((k - (rows[r] - 1) * 0.5f) * 0.135f, 0.065f + r * 0.115f, 0f);
                    log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    log.transform.localScale = new Vector3(0.13f, 0.30f + 0.04f * ((n * 0.382f) % 1f), 0.13f);
                }
        }

        /// <summary>Земля под огнём: свет висит над поленьями на метр с лишним.</summary>
        private Vector3 Ground() => transform.parent != null ? transform.parent.position : transform.position;

        /// <summary>Куда смотреть, чтобы бросить полено: середина огня над поленьями.</summary>
        public Vector3 Centre => Ground() + Vector3.up * 0.4f;

        private IEnumerator Throw(int n)
        {
            var log = Log(n);

            // Полено падает в огонь — четверть секунды, а не возникает в нём.
            var rest = log.transform.position;
            var from = rest + Vector3.up * 0.7f;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                log.transform.position = Vector3.Lerp(from, rest, t / 0.25f * (t / 0.25f));
                yield return null;
            }
            log.transform.position = rest;

            _flare = 1f;
            Burst(n);
            Audio.Sfx.Stoke(transform, rest);
            Debug.Log($"[КОСТЁР] полено №{n + 1}");

            yield return new WaitForSeconds(0.7f);
            Answer();

            StartCoroutine(Burn(log));
        }

        /// <summary>
        /// Полено поперёк огня. Повёрнуто золотым углом от номера: три подряд
        /// ложатся домиком, а не одно в одно.
        /// </summary>
        private GameObject Log(int n)
        {
            while (_logs.Count >= MaxLogs)
            {
                var old = _logs.Dequeue();
                if (old != null) Destroy(old);
            }

            // Первый материал модели костра светлый, как камень очага: полено в огне белело (кадр 31.10).
            // Billet тонирует его в дерево сразу, ещё в падении.
            var log = Billet("Полено", n);

            log.transform.SetParent(transform.parent != null ? transform.parent : transform, true);
            log.transform.position = Ground() + Vector3.up * 0.3f;
            log.transform.rotation = Quaternion.Euler(0f, n * 137.5f, 0f) * Quaternion.Euler(0f, 0f, 90f - 12f);
            log.transform.localScale = new Vector3(0.16f, 0.38f, 0.16f);

            _logs.Enqueue(log);
            return log;
        }

        /// <summary>
        /// Столб искр: шестьдесят разом, вверх и чуть врозь. Скорость и разлёт —
        /// формулой от номера искры, без жребия: одинаковое полено — одинаковый столб.
        /// </summary>
        private void Burst(int n)
        {
            if (_sparks == null) return;

            var at = Ground() + Vector3.up * 0.45f;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 60; i++)
            {
                float a = (i * 137.5f + n * 53f) * Mathf.Deg2Rad;
                float k = (i % 7) / 6f;
                float spread = 0.15f + 0.35f * ((i * 0.618f) % 1f);
                p.position = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.18f * k;
                p.velocity = new Vector3(Mathf.Cos(a) * spread, 2.2f + 1.6f * k, Mathf.Sin(a) * spread);
                p.startLifetime = 1.8f + 1.2f * k;
                p.startSize = 0.06f + 0.05f * (1f - k);
                _sparks.Emit(p, 1);
            }
        }

        /// <summary>Отзывается ближний у огня, кто ещё не говорил сегодня; Гурт — жестом.</summary>
        private void Answer()
        {
            var fire = Ground();
            Warrior best = null;
            float bestDistance = Heard;

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w is SinbinderPlayer || w.IsDead || w.Team != Team.Player) continue;
                if (w.DisplayName == null || !Lines.ContainsKey(w.DisplayName) || Said.Contains(w.DisplayName)) continue;

                float d = CampFocus.GroundDistance(w.transform.position, fire);
                if (d < bestDistance) { best = w; bestDistance = d; }
            }
            if (best == null) return;

            Said.Add(best.DisplayName);
            UI.SpeechBubbles.Say(best, Loc.T(Lines[best.DisplayName]));
            Debug.Log($"[КОСТЁР] отозвался {best.DisplayName}");
        }

        /// <summary>
        /// Полено чернеет за полминуты и прогорает за следующие двадцать секунд:
        /// огонь не копит дрова, и через минуту костёр — снова тот же.
        /// </summary>
        private IEnumerator Burn(GameObject log)
        {
            var renderer = log != null ? log.GetComponent<MeshRenderer>() : null;
            var block = new MaterialPropertyBlock();
            var charred = new Color(0.16f, 0.11f, 0.09f);
            var size = log != null ? log.transform.localScale : Vector3.one;

            for (float t = 0f; t < 50f && log != null; t += Time.deltaTime)
            {
                var c = Color.Lerp(Fresh, charred, Mathf.Clamp01(t / 30f));
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                renderer.SetPropertyBlock(block);

                if (t > 30f) log.transform.localScale = Vector3.Scale(size, new Vector3(1f - (t - 30f) / 20f, 1f, 1f - (t - 30f) / 20f));
                yield return null;
            }
            if (log != null) Destroy(log);
        }

        // Поверх дыхания огня (FireFlicker ставит яркость в Update) — вспышка
        // от полена: сразу ярче и гаснет за пять секунд.
        // И само пламя выше и гуще, а не только свет (КТ1 п. 16: «пламя растёт»).
        void LateUpdate()
        {
            if (_flare <= 0f) return;
            _flare = Mathf.Max(0f, _flare - Time.deltaTime / FlareSeconds);
            float f = _flare * _flare;
            if (_light != null) _light.intensity *= 1f + FlareGain * f;
            if (_flame != null)
            {
                var main = _flame.main;
                main.startSizeMultiplier = _flameSize * (1f + 0.45f * f);
                var emission = _flame.emission;
                emission.rateOverTimeMultiplier = _flameRate * (1f + 0.8f * f);
            }
        }
    }
}
