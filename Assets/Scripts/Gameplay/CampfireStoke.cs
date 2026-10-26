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
            [Loc.N("Брат Хальд")] = Loc.N("Не так близко к котелку, владыка. Жар портит отвар."),
            [Loc.N("Одноглазый Хорь")] = Loc.N("Ярко. Такой огонь в бору видно далеко. Даже одним глазом."),
            [Loc.N("Толстый Ю")] = Loc.N("Вот и похлёбка оживёт. Спасибо, владыка."),
            [Loc.N("Лиска")] = Loc.N("Я считаю поленья, владыка. На всякий случай."),
            [Loc.N("Немой Гурт")] = Loc.N("(молча подвигается ближе к огню)"),
            [Loc.N("Косой Ждан")] = Loc.N("Дайте я брошу, владыка. Отсюда точно попаду. Наверное."),
        };

        private static CampfireStoke _instance;
        private static readonly HashSet<string> Said = new();

        private Light _light;
        private ParticleSystem _sparks;
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
                if (ps.name == "Искры") { _sparks = ps; break; }
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterials.Length > 0 && r.sharedMaterials[0] != null) { _wood = r.sharedMaterials[0]; break; }
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Костёр, до которого Греховод дотянется сейчас. Нет — null.</summary>
        public static CampfireStoke Nearest()
        {
            if (_instance == null || !SinbinderPlayer.Exists || Inspectable.Telling) return null;
            if (CrystalBall.Raised || RaidEvent.Running) return null;
            if (Time.time < _instance._ready) return null;
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
            StartCoroutine(Throw(_thrown++));
        }

        /// <summary>Земля под огнём: свет висит над поленьями на метр с лишним.</summary>
        private Vector3 Ground() => transform.parent != null ? transform.parent.position : transform.position;

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

            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "Полено";
            var col = log.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var renderer = log.GetComponent<MeshRenderer>();
            if (_wood != null) renderer.sharedMaterial = _wood;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

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
                var c = Color.Lerp(Color.white, charred, Mathf.Clamp01(t / 30f));
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
        void LateUpdate()
        {
            if (_flare <= 0f || _light == null) return;
            _flare = Mathf.Max(0f, _flare - Time.deltaTime / FlareSeconds);
            _light.intensity *= 1f + FlareGain * _flare * _flare;
        }
    }
}
