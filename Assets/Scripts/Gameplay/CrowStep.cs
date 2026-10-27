// Assets/Scripts/Gameplay/CrowStep.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// «Вороньим следом» — навык Каргана Старого Ворона. Автор, 26 октября:
    /// «не должно ли у Каргана быть особый навык, связанный с тем, что он
    /// Старый Ворон… он распадается на перья и в другом месте с облачком
    /// материализуется». Когда — автор выбрал «сам + по приказу „ко мне“»;
    /// и общее правило: навыки — «в полусвободном формате» (28-ORDERS, 26.10).
    ///
    /// Карган — телохранитель («не отходит от вас», состав отряда). Поэтому
    /// навык о Греховоде:
    /// <list type="bullet">
    /// <item><b>сам</b> — Греховоду грозит беда (враг ближе <see cref="Danger"/>
    ///   или по нему только что ударили), Карган дальше <see cref="Far"/> и верен
    ///   (верность не ниже <see cref="Faithful"/>): неверный не придёт — и это видно;</item>
    /// <item><b>по зову</b> — приказ идти к Греховоду издалека, и Карган его
    ///   принял (решение «исполнить»): зов голосуется, как любой приказ.</item>
    /// </list>
    /// Рассыпается перьями, пропадает, собирается из перьев рядом с Греховодом —
    /// между ним и ближайшим врагом. Откат <see cref="Cooldown"/>, стоит сил.
    /// </summary>
    [RequireComponent(typeof(Warrior))]
    public class CrowStep : MonoBehaviour, IAskableSkill
    {
        // ---------- просьба с панели приказов (IAskableSkill) ----------

        public string Word => Loc.T("Вороньим следом");

        public string Tip => Loc.T("Рассыпается перьями и встаёт рядом с Греховодом: принимает удар "
                                 + "на себя и зовёт врага. Это просьба — может и не прийти.");

        public bool Ready
        {
            get
            {
                if (_warrior == null || _warrior.IsDead || _stepping || Time.time < _readyAt) return false;
                var f = GetComponent<Fatigue>();
                return f == null || f.Current >= Cost;
            }
        }

        public bool Fits(out string why)
        {
            why = "";
            if (!SinbinderPlayer.Exists) { why = Loc.T("Некого прикрывать."); return false; }

            // Прикрывает отход — пост не бросает (28-ORDERS, 26.10: «CrowStep ломает
            // арьергард»): развилка «подошёл или нет» — его сцена, и она одна.
            if (_warrior != null && EscapeZone.Rearguard == _warrior.DisplayName)
            {
                why = Loc.T("Я обещал их задержать.");
                return false;
            }
            if (CampFocus.GroundDistance(transform.position, SinbinderPlayer.Where) <= Far)
            {
                why = Loc.T("Я и так рядом, Владыка.");
                return false;
            }

            // Свою почти выигранную драку ради слабого у Греховода не бросает (облачная, 26.10).
            var own = NearestEnemy(transform.position, out float ownAt);
            var theirs = NearestEnemy(SinbinderPlayer.Where, out _);
            if (own != null && ownAt <= 2.5f && own.HP <= own.MaxHP * 0.25f
                && theirs != null && theirs != own && theirs.HP <= theirs.MaxHP * 0.35f)
            {
                why = Loc.T("Сначала добью этого.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Кнопка — после того, как он хоть раз перелетел сам (28-ORDERS, 26.10:
        /// «игрок сперва видит, потом узнаёт, что можно попросить»).
        /// </summary>
        public bool Known => _selfUsed;

        private bool _selfUsed;

        /// <summary>
        /// Попросить — значит позвать к Греховоду издалека: приказ идти к нему,
        /// приглушённый расстоянием и голосуемый. Принял — <see cref="Called"/>
        /// увидит это и пойдёт вороньим следом.
        /// </summary>
        public void Ask()
        {
            if (!Fits(out string why)) { UI.SpeechBubbles.Say(_warrior, why); return; }
            _warrior.IssueCommand(CommandKind.Move, SinbinderPlayer.Where, null, Voice.MuffleFor(_warrior));
            Debug.Log($"[ВОРОН] {_warrior.DisplayName}: попросили «вороньим следом».");
        }

        private const float Far = 10f;          // дальше этого — идёт пешком нельзя успеть, метров
        private const float Danger = 5f;        // враг ближе этого к Греховоду — беда, метров
        private const float Faithful = 50f;     // верность, с которой приходит сам (AOSConfig.LoyaltyIndifferent)
        private const float Cooldown = 40f;     // секунд игры
        private const float Cost = 25f;         // сил
        private const float Gone = 0.35f;       // сколько его нет между облаками, секунд
        private const float Beside = 1.4f;      // как близко встаёт к Греховоду, метров

        // Строки — черновик D; облаку на сверку (47-TEXTS, приметы).
        private static readonly string SelfLine = Loc.N("{0} рассыпался перьями — и встал рядом с Греховодом.");
        private static readonly string CalledLine = Loc.N("{0} пришёл на зов вороньим следом.");

        private static readonly Color Feather = new Color(0.11f, 0.125f, 0.185f, 1f);   // перо, как у плаща (wear.py FEATHER)

        private Warrior _warrior;
        private float _readyAt;
        private float _heroHp = -1f;
        private bool _stepping;

        /// <summary>Сколько раз за игру — для прогона.</summary>
        public static int Used { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Used = 0;

        /// <summary>Новая игра — счёт перелётов с нуля.</summary>
        public static void Forget() => Used = 0;

        /// <summary>Тот ли это, кому навык положен: Старый Ворон, по прозвищу.</summary>
        public static bool Raven(Warrior w)
            => w != null && w.DisplayName != null && w.DisplayName.Contains("Ворон") && !(w is SinbinderPlayer);

        void Awake() => _warrior = GetComponent<Warrior>();

        void Update()
        {
            if (_stepping || _warrior == null || _warrior.IsDead || Time.time < _readyAt) return;
            if (!SinbinderPlayer.Exists || Time.timeScale <= 0f) return;

            var hero = SinbinderPlayer.Instance;
            float far = CampFocus.GroundDistance(transform.position, SinbinderPlayer.Where);

            // По Греховоду ударили — с прошлого кадра здоровья стало меньше.
            var heroBody = hero.GetComponent<Damageable>();
            bool hit = false;
            if (heroBody != null)
            {
                if (_heroHp >= 0f && heroBody.HP < _heroHp - 0.01f) hit = true;
                _heroHp = heroBody.HP;
            }
            if (far <= Far) return;
            // Уместность — та же, что у просьбы: арьергард, своя драка (Fits).
            if (!Fits(out _)) return;

            var enemy = NearestEnemy(SinbinderPlayer.Where, out float enemyAt);
            bool danger = hit || (enemy != null && enemyAt <= Danger);

            bool self = danger && _warrior.Loyalty >= Faithful;
            bool called = Called();
            if (!self && !called) return;

            var fatigue = GetComponent<Fatigue>();
            if (fatigue != null && fatigue.Current < Cost) return;

            StartCoroutine(Step(enemy, called && !self));
        }

        /// <summary>Позван: приказ идти к Греховоду, и решение — исполнить.</summary>
        private bool Called()
        {
            var cmd = _warrior.Command;
            if (!cmd.IsSet || cmd.Kind != CommandKind.Move) return false;
            if (CampFocus.GroundDistance(cmd.Point, SinbinderPlayer.Where) > 3f) return false;
            var mind = GetComponent<AOSWarriorWrapper>();
            return mind != null && mind.LastDecision == ActionType.ObeyCommand;
        }

        private Damageable NearestEnemy(Vector3 at, out float distance)
        {
            distance = float.MaxValue;
            Damageable best = null;
            if (CombatManager.Instance == null) return null;
            foreach (var e in CombatManager.Instance.GetEnemies(gameObject))
            {
                if (e == null || e.IsDead) continue;
                float d = CampFocus.GroundDistance(e.transform.position, at);
                if (d < distance) { distance = d; best = e; }
            }
            return best;
        }

        private System.Collections.IEnumerator Step(Damageable enemy, bool byCall)
        {
            _stepping = true;
            _readyAt = Time.time + Cooldown;
            GetComponent<Fatigue>()?.Spend(Cost);

            Feathers(transform.position + Vector3.up * 0.9f);
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = false;

            yield return new WaitForSeconds(Gone);

            // Встать между Греховодом и врагом — телохранитель, а не зритель.
            var heroAt = SinbinderPlayer.Where;
            var dir = enemy != null ? enemy.transform.position - heroAt : transform.position - heroAt;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            var to = heroAt + dir.normalized * Beside;

            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (UnityEngine.AI.NavMesh.SamplePosition(to, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) to = hit.position;
            if (agent != null && agent.isOnNavMesh) agent.Warp(to); else transform.position = to;
            if (enemy != null)
            {
                var look = enemy.transform.position - to; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
            }
            if (byCall) _warrior.ClearCommand();

            foreach (var r in renderers) if (r != null) r.enabled = true;
            Feathers(to + Vector3.up * 0.9f);

            Used++;
            if (!byCall) _selfUsed = true;
            string line = Loc.F(byCall ? CalledLine : SelfLine, Loc.Name(_warrior.DisplayName));
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
            Debug.Log($"[ВОРОН] {_warrior.DisplayName} вороньим следом: {(byCall ? "по зову" : "сам")}");

            // Встал — и первый удар по Греховоду его (автор, 26.10: «блокировал первую
            // атаку по Греховоду»). Звать на себя после перелёта — нет: «Карган разгружен —
            // одно действие» (28-ORDERS, облачная 26.10), провокация — только в арьергарде.
            var heroBody = SinbinderPlayer.Exists ? SinbinderPlayer.Instance.GetComponent<Damageable>() : null;
            Bodyguard.Guard(heroBody, _warrior, GuardFor);
            yield return new WaitForSeconds(GuardFor);
            _stepping = false;
        }

        private const float GuardFor = 4f;     // сколько сторожит первый удар по Греховоду, секунд

        private static Material _featherMat;

        /// <summary>Облако перьев: тёмные лепестки разлетаются и падают. Само уходит.</summary>
        private static void Feathers(Vector3 at)
        {
            var go = new GameObject("Перья");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Feather;
            main.gravityModifier = 0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.2f;

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Feather, 0f), new GradientColorKey(Feather, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            if (_featherMat == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _featherMat = new Material(shader);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (_featherMat != null) r.sharedMaterial = _featherMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
            ps.Emit(45);   // разом, как облако — не струйкой
        }
    }
}
