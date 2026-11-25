// Assets/Scripts/Gameplay/AutoAttack.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    [RequireComponent(typeof(Damageable))]
    public class AutoAttack : MonoBehaviour
    {
        [SerializeField] private float _attackDamage = 5f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackCooldown = 1f;

        private Damageable _self;
        private Damageable _currentTarget;
        private float _cooldownTimer;
        private bool _isPlayerControlled;

        /// <summary>
        /// Сила удара. С 24 сентября — удар воина (оболочка плюс вещи
        /// в руках, <see cref="Warrior.Attack"/>), пока включён
        /// <see cref="CombatMath"/>; выключен — прежнее число компонента.
        /// </summary>
        public float AttackDamage
        {
            get
            {
                if (!CombatMath.Enabled) return _attackDamage;

                // Лениво: удар могли повесить раньше, чем душу.
                if (_warrior == null) _warrior = GetComponent<Warrior>();
                return _warrior != null ? _warrior.Attack : _attackDamage;
            }
        }

        private Warrior _warrior;
        public float AttackRange => _attackRange;
        public Damageable CurrentTarget => _currentTarget;

        void Awake()
        {
            _self = GetComponent<Damageable>();
            _warrior = GetComponent<Warrior>();
        }

        void Update()
        {
            if (_self.IsDead) return;

            // Мир стоит — пауза или урок, — и руки стоят с ним. Иначе
            // готовый к удару бил бы в замершем мире один раз, а удар
            // на уроке жатвы мог бы положить ещё одного.
            if (Time.deltaTime <= 0f) return;

            _cooldownTimer -= Time.deltaTime;

            // Греховод в роли Воина бьёт только по клику (FirstPersonCombat):
            // сам цель не ищет и по прежней не добивает.
            if (_manualOnly) return;

            if (_currentTarget != null)
            {
                if (_currentTarget.IsDead) { _currentTarget = null; return; }
                float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);
                if (dist <= Reach && _cooldownTimer <= 0f)
                    Attack(_currentTarget);
            }
            else if (!_isPlayerControlled)
            {
                FindClosestEnemy();
            }
        }


        /// <summary>
        /// Через сколько после начала взмаха клинок доходит до цели, секунд.
        /// До 22 октября урон приходил в миг решения, и взмах начинался вместе
        /// с ним: враг вздрагивал раньше, чем его задевали (отзыв автора 21.10,
        /// п. 8: «атаки выглядят странно»).
        /// </summary>
        private const float HitDelay = 0.3f;

        /// <summary>Удар по приказу — не чаще раза в столько секунд.</summary>
        private const float ForcedPace = 0.55f;

        /// <summary>
        /// Когда начат последний взмах — по часам игры. Темп удара по приказу
        /// считается по нему, а не по <c>_cooldownTimer</c>: у воинов отряда
        /// этот компонент выключен (AOSSceneSetup — бьёт AutoAttackAOS), его
        /// Update не идёт, и таймер, взведённый первым ударом, стоял вечно —
        /// каждый бил по приказу один раз за бой (23 октября: убежавших раненых
        /// охотников первой волны никто не мог добить).
        /// </summary>
        private float _lastSwing = float.NegativeInfinity;

        private void Attack(Damageable target, bool credit = false)
        {
            bool bow = IsArcher;
            _cooldownTimer = bow ? Mathf.Max(_attackCooldown, Pace) : _attackCooldown;
            _lastSwing = Time.time;

            // Удар стоит сил, а выдохшийся бьёт вполсилы.
            float damage = AttackDamage;
            var fatigue = GetComponent<Fatigue>();
            if (fatigue != null)
            {
                damage *= fatigue.Effectiveness;
                fatigue.SpendForAttack();
            }

            // Лицом к цели, если она сильно сбоку: бил туда, куда смотрел, —
            // бывало, и боком. Чуть вбок — не трогаем: в свалке спина врага
            // открыта соседу (Facing, удар сзади ×1,5), и выравнивать всех
            // лицом к лицу значило бы срезать урон боя.
            var to = target.transform.position - transform.position;
            to.y = 0f;
            // Лучник целится — лицом к цели всегда.
            if (to.sqrMagnitude > 1e-4f && Vector3.Angle(transform.forward, to) > (bow ? 10f : 45f))
                transform.rotation = Quaternion.LookRotation(to);

            // Взмах — сейчас, урон — когда клинок дойдёт (или долетит стрела).
            if (TryGetComponent<WarriorAnimation>(out var arm)) arm.Struck();
            StartCoroutine(!bow ? Land(target, damage, credit) : SkyBow ? Sky(target, damage, credit) : Loose(target, damage, credit));
        }

        // ── Лучник ──────────────────────────────────────────────────────────
        // Автор, 25.11: «как мы можем тестировать фрагмент с лучниками — без лучников? нам нужны лучники!»
        // До того ремесло лучника было только видом: лук за спиной, кинжал в руке, удар на 2 м.
        // Теперь в бою отряда он стреляет. Стрела летит по-настоящему — дугой, в точку, где цель
        // будет (упреждение по скорости), без самонаведения: увернулся — промах, стрела в земле.
        // Урон — когда долетела, тому, в кого попала первым — своему тоже. Свой на линии выстрела —
        // лучник бьёт навесом, над его головой; опасно, только если свой шагнёт под стрелу или стоит вплотную к врагу.
        // Личный бой (Греховод в теле лучника) и осада ворот — по-прежнему рукой (AttackRange).

        /// <summary>С какой дали бьёт лучник, м.</summary>
        public const float BowRange = 11f;
        /// <summary>Выстрел — не чаще раза в столько секунд.</summary>
        public const float BowPace = 1.6f;
        private const float ArrowSpeed = 24f;     // м/с по прямой; дуга видна глазом
        private const float ArrowFall = 24f;      // «тяжесть» стрелы, м/с² — на 11 м дуга ~0,6 м
        private const float DrawDelay = 0.12f;    // натянуть тетиву — стрела уходит в середине взмаха

        public bool IsArcher
        {
            get
            {
                if (_manualOnly) return false;
                if (_warrior == null) _warrior = GetComponent<Warrior>();
                return _warrior != null && _warrior.Soul != null && _warrior.Soul.Trade == Core.Trade.Archer;
            }
        }

        /// <summary>Досягаемость в бою отряда: у лучника — выстрел, у прочих — рука.</summary>
        public float Reach => IsArcher ? (SkyBow ? SkyRange : BowRange) : _attackRange;

        // ── «Небесный лук» — проба (автор, 25.11: «лук с повышенной дальностью и самонаводящимися
        // стрелами — стрела при пуске летит вверх, подлетает к противнику не снижая высоты и летит
        // ровно вниз на противника»). Откуда он у воина — вещь, титул, умение — решит автор; пока —
        // пометка SkyBow (ставит стрельбище Editor/ArcherRange). Не записывается.
        [System.NonSerialized] public bool SkyBow;
        public const float SkyRange = 22f;
        public const float SkyPace = 2.2f;
        private const float SkySpeed = 22f;
        private const float SkyTurn = 12f;        // рад/с: поворот дугой ~1,8 м, без углов
        private const float SkyHeight = 6f;       // над головой цели

        /// <summary>Сколько выпущено и куда пришлось — для стрельбища.</summary>
        [System.NonSerialized] public int Shots, Hits, FriendlyHits, Misses;

        private float Pace => SkyBow ? SkyPace : BowPace;

        private static Quaternion Face(Vector3 dir, Vector3 side)
            => Mathf.Abs(dir.normalized.y) > 0.98f ? Quaternion.LookRotation(dir, side) : Quaternion.LookRotation(dir);

        private System.Collections.IEnumerator Sky(Damageable target, float damage, bool credit)
        {
            yield return new WaitForSeconds(DrawDelay);
            if (_self.IsDead || target == null || target.IsDead) yield break;
            Shots++;

            Sides(out var foes, out var ours);
            Vector3 p = transform.position + Vector3.up * 1.4f;
            Vector3 vel = (Vector3.up * 3f + transform.forward).normalized * SkySpeed;   // почти отвесно вверх
            Vector3 side = transform.forward;
            var arrow = MakeArrow();
            Destroy(arrow, 8f);
            arrow.transform.SetPositionAndRotation(p, Face(vel, side));

            int phase = 0;   // 0 — взлёт, 1 — на высоте к цели, 2 — вниз на цель, 3 — цели нет: падает
            Vector3 lastSeen = target.transform.position;
            Damageable hit = null;
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                yield return null;
                if (arrow == null) yield break;

                // Цель пала в полёте — ближайший враг рядом с ней; никого — стрела падает.
                if (phase < 3 && (target == null || target.IsDead))
                {
                    target = null;
                    float best = 6f;
                    foreach (var f in foes)
                    {
                        if (f == null || f.IsDead) continue;
                        float d = Vector3.Distance(f.transform.position, lastSeen);
                        if (d < best) { best = d; target = f; }
                    }
                    if (target == null) phase = 3;
                }
                Vector3 chest = target != null ? target.transform.position + Vector3.up * 1.1f : lastSeen;
                if (target != null) lastSeen = target.transform.position;

                float cruise = lastSeen.y + 1.1f + SkyHeight;
                Vector3 flatTo = new Vector3(chest.x - p.x, 0f, chest.z - p.z);
                if (flatTo.sqrMagnitude > 1e-4f) side = flatTo.normalized;
                if (phase == 0 && p.y >= cruise - 0.5f) phase = 1;
                if (phase == 1 && flatTo.magnitude <= SkySpeed / SkyTurn) phase = 2;   // радиус поворота — и вниз

                Vector3 want = phase == 0 ? Vector3.up * 3f + side
                             : phase == 1 ? side + Vector3.up * Mathf.Clamp(cruise - p.y, -1f, 1f) * 0.5f
                             : phase == 2 ? chest - p
                             : Vector3.down;
                vel = Vector3.RotateTowards(vel, want.normalized * SkySpeed, SkyTurn * Time.deltaTime, 0f);
                Vector3 next = p + vel * Time.deltaTime;
                arrow.transform.SetPositionAndRotation(next, Face(vel, side));

                if (phase >= 2)
                {
                    hit = FirstOnPath(foes, p, next);
                    if (hit == null) hit = FirstOnPath(ours, p, next);
                }
                p = next;
                if (hit != null) break;
                if (p.y <= lastSeen.y + 0.02f) break;   // в землю
            }
            ArrowLands(arrow, hit, foes, damage, credit);
        }

        private void ArrowLands(GameObject arrow, Damageable hit, System.Collections.Generic.List<Damageable> foes, float damage, bool credit)
        {
            if (arrow == null) return;
            if (arrow.TryGetComponent<TrailRenderer>(out var trail)) trail.emitting = false;
            if (hit == null) { Misses++; Destroy(arrow, 3f); return; }   // промах — торчит из земли

            arrow.transform.SetParent(hit.transform, true);   // торчит из того, в кого попала
            Destroy(arrow, 2f);
            bool foe = foes.Contains(hit);
            if (foe) Hits++;
            else { FriendlyHits++; Debug.Log("[ЛУК] " + name + " попал в своего: " + hit.name); }
            hit.TakeArrow(damage, gameObject);
            if (foe && credit && hit.IsDead && TryGetComponent<AOS.AOSWarriorWrapper>(out var mind))
                mind.OnKilledEnemy(hit.Warrior);
        }

        private System.Collections.IEnumerator Loose(Damageable target, float damage, bool credit)
        {
            yield return new WaitForSeconds(DrawDelay);
            if (_self.IsDead || target == null || target.IsDead) yield break;

            Vector3 from = transform.position + Vector3.up * 1.4f + transform.forward * 0.35f;
            Vector3 aim = target.transform.position + Vector3.up * 1.1f;
            float flat = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(aim.x, 0f, aim.z));
            float T = Mathf.Clamp(flat / ArrowSpeed, 0.12f, 0.6f);
            // Свой на линии выстрела — навесом: дуга ~3 м проходит над головой и падает на врага
            // (автор, 25.11: «каков шанс попасть по союзнику, если он стоит перед лучником и бьётся?»).
            Sides(out var foes, out var ours);
            if (LineHasMate(ours, from, aim)) T = Mathf.Max(T, LobTime);
            // Упреждение: туда, где цель будет, если не свернёт.
            if (target.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var legs) && legs.isOnNavMesh)
                aim += new Vector3(legs.velocity.x, 0f, legs.velocity.z) * T;
            var fall = new Vector3(0f, -ArrowFall, 0f);
            Vector3 v0 = (aim - from) / T - 0.5f * T * fall;

            float groundFrom = transform.position.y, groundTo = target.transform.position.y;

            Shots++;
            var arrow = MakeArrow();
            Destroy(arrow, 6f);   // страховка: лучника не стало посреди полёта
            arrow.transform.SetPositionAndRotation(from, Quaternion.LookRotation(v0));
            Vector3 last = from;
            Damageable hit = null;
            for (float t = 0f; t < 2f; )
            {
                yield return null;
                if (arrow == null) yield break;
                t += Time.deltaTime;
                Vector3 p = from + v0 * t + 0.5f * t * t * fall;
                Vector3 vel = v0 + fall * t;
                arrow.transform.SetPositionAndRotation(p, Quaternion.LookRotation(vel));

                hit = FirstOnPath(foes, last, p);
                if (hit == null) hit = FirstOnPath(ours, last, p);   // свой под стрелой — его и ранит
                last = p;
                if (hit != null) break;
                if (p.y <= Mathf.Lerp(groundFrom, groundTo, Mathf.Clamp01(t / T)) + 0.02f) break;   // в землю
            }

            ArrowLands(arrow, hit, foes, damage, credit);
        }

        /// <summary>
        /// Кто враг и кто свой для стрелы. Полигон (Crypt_Test) живёт без менеджера боя — тогда по командам воинов:
        /// ближний бой обходился без менеджера, и стрела без него не вылетала вовсе (стрельбище, 25.11).
        /// </summary>
        private void Sides(out System.Collections.Generic.List<Damageable> foes, out System.Collections.Generic.List<Damageable> ours)
        {
            var cm = CombatManager.Instance;
            if (cm != null) { foes = cm.GetEnemies(gameObject); ours = cm.GetAllies(gameObject); return; }
            foes = new System.Collections.Generic.List<Damageable>();
            ours = new System.Collections.Generic.List<Damageable>();
            if (_warrior == null) _warrior = GetComponent<Warrior>();
            var me = _warrior != null ? _warrior.Team : Team.Player;
            foreach (var w in FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || !w.TryGetComponent<Damageable>(out var d)) continue;
                if (w.Team == me) ours.Add(d);
                else if (me != Team.Neutral && w.Team != Team.Neutral) foes.Add(d);
            }
        }

        /// <summary>
        /// Время полёта навесом: при «тяжести» 24 м/с² вершина дуги ≈ 3 м над прямой. Свой в 1,5 м перед
        /// врагом — стрела над его головой (≈2,4 м); вплотную к врагу (до ~0,7 м) — заденет: стрелять в свалку опасно.
        /// </summary>
        private const float LobTime = 1.0f;

        /// <summary>Стоит ли свой на прямой от лучника к цели (ближе 0,6 м к ней, между ними).</summary>
        private bool LineHasMate(System.Collections.Generic.List<Damageable> ours, Vector3 from, Vector3 aim)
        {
            Vector2 a = new Vector2(from.x, from.z), b = new Vector2(aim.x, aim.z), ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-4f) return false;
            foreach (var d in ours)
            {
                if (d == null || d.IsDead || d == _self) continue;
                Vector2 q = new Vector2(d.transform.position.x, d.transform.position.z);
                float s = Vector2.Dot(q - a, ab) / len2;
                if (s > 0f && s < 1f && (a + ab * s - q).sqrMagnitude < 0.6f * 0.6f) return true;
            }
            return false;
        }

        /// <summary>Первое тело, которое задевает отрезок полёта: стоячая капсула 0,42 м, от колен до макушки.</summary>
        private Damageable FirstOnPath(System.Collections.Generic.List<Damageable> bodies, Vector3 a, Vector3 b)
        {
            Damageable best = null;
            float bestT = 2f;
            foreach (var d in bodies)
            {
                if (d == null || d.IsDead || d == _self) continue;
                Vector3 feet = d.transform.position;
                for (int k = 1; k <= 3; k++)
                {
                    float s = k / 3f;
                    if (s >= bestT) break;
                    Vector3 q = Vector3.Lerp(a, b, s);
                    float dx = q.x - feet.x, dz = q.z - feet.z;
                    if (dx * dx + dz * dz <= 0.42f * 0.42f && q.y >= feet.y + 0.15f && q.y <= feet.y + 1.9f)
                    { best = d; bestT = s; break; }
                }
            }
            return best;
        }

        private static Material _shaftMat, _featherMat, _tipMat, _trailMat;

        private static GameObject MakeArrow()
        {
            if (_shaftMat == null)
            {
                _shaftMat = Lit(new Color(0.36f, 0.26f, 0.16f));
                _featherMat = Lit(new Color(0.86f, 0.84f, 0.78f));
                _tipMat = Lit(new Color(0.55f, 0.56f, 0.6f));
                var sprite = Shader.Find("Sprites/Default");
                if (sprite != null) _trailMat = new Material(sprite);
            }
            var root = new GameObject("Стрела");
            ArrowPart(root, _shaftMat, Vector3.zero, new Vector3(0.028f, 0.028f, 0.75f));
            ArrowPart(root, _tipMat, new Vector3(0f, 0f, 0.39f), new Vector3(0.055f, 0.055f, 0.07f));
            ArrowPart(root, _featherMat, new Vector3(0f, 0f, -0.31f), new Vector3(0.12f, 0.012f, 0.13f));
            ArrowPart(root, _featherMat, new Vector3(0f, 0f, -0.31f), new Vector3(0.012f, 0.12f, 0.13f));
            if (_trailMat != null)
            {
                var trail = root.AddComponent<TrailRenderer>();
                trail.sharedMaterial = _trailMat;
                trail.time = 0.14f;
                trail.startWidth = 0.05f;
                trail.endWidth = 0f;
                trail.minVertexDistance = 0.05f;
                trail.startColor = new Color(1f, 0.95f, 0.85f, 0.6f);
                trail.endColor = new Color(1f, 0.95f, 0.85f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
            }
            return root;
        }

        private static void ArrowPart(GameObject root, Material mat, Vector3 at, Vector3 size)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            var r = part.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material Lit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        private System.Collections.IEnumerator Land(Damageable target, float damage, bool credit)
        {
            yield return new WaitForSeconds(HitDelay);
            if (_self.IsDead || target == null || target.IsDead) yield break;
            // Отскочил за время взмаха — промах.
            if (Vector3.Distance(transform.position, target.transform.position) > _attackRange + 0.6f) yield break;

            target.TakeDamage(damage, gameObject);

            // Убийство по приказу — деяние (AOSWarriorWrapper, как и до задержки).
            if (credit && target.IsDead && TryGetComponent<AOS.AOSWarriorWrapper>(out var mind))
                mind.OnKilledEnemy(target.Warrior);
        }

        private void FindClosestEnemy()
        {
            if (CombatManager.Instance == null) return;
            var enemy = CombatManager.Instance.GetClosestEnemy(transform.position, Reach + _attackRange, gameObject);
            if (enemy != null && !enemy.IsDead)
                _currentTarget = enemy;
        }

        public void Initialize(float damage, float range, float cooldown)
        {
            _attackDamage = damage;
            _attackRange = range;
            _attackCooldown = cooldown;
        }

        private bool _manualOnly;

        /// <summary>
        /// Личный бой (<see cref="FirstPersonCombat"/>): включено — удар только
        /// по <see cref="ForceAttack"/>, одним взмахом за клик. Переключение
        /// забывает цель, чтобы ни одна роль не доигрывала чужой бой.
        /// </summary>
        public void SetManualOnly(bool on)
        {
            if (_manualOnly == on) return;
            _manualOnly = on;
            _currentTarget = null;
            _isPlayerControlled = false;
        }

        public void ForceAttack(Damageable target)
        {
            if (target != null && !target.IsDead)
            {
                _currentTarget = target;
                _isPlayerControlled = true;
                // Решение приходит каждый такт, и до 22 октября каждый такт
                // начинал удар заново, не докончив прежний (п. 8 отзыва 21.10).
                // Темп боя по приказу при этом был около двух ударов в секунду —
                // на нём держится бой демо. Его и оставляем, но взмах доигрывается.
                if (Time.time - _lastSwing >= (IsArcher ? Pace : ForcedPace)) Attack(target, credit: true);
            }
        }
    }
}