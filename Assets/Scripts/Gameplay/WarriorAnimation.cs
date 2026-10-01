// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Говорит телу то, что решила душа.
    ///
    /// Движок уже называет действие — <c>Flee</c>, <c>Attack</c>,
    /// <c>Loot</c>. Не хватало одного: сказать это аниматору.
    /// <c>DialogueAnimator</c> был написан месяцы назад и не сыграл
    /// ни разу, потому что <c>Animator</c>а в проекте не было ни одного —
    /// восьмой случай «написано, звена нет».
    ///
    /// <b>18 сентября аниматоры появились</b>: контроллеры пересобраны,
    /// <c>Talk</c> и <c>Attack</c> перестали быть покоем. Проводка,
    /// написанная заранее, тем и хороша — вспоминать её в тот вечер
    /// не пришлось.
    ///
    /// Тогда же <c>DialogueAnimator</c> снят совсем: он остался никем
    /// не повешенным, а повесить его рядом значило бы завести **второго
    /// хозяина одного `Animator`** — тот же вид беды, что утром того же
    /// дня развели у камеры. Хозяин тела один, и это здесь.
    ///
    /// Состояние меняем только когда оно сменилось: <c>Animator.Play</c>
    /// каждый кадр перезапускает анимацию с нуля, и воин дёргается
    /// на месте вместо того, чтобы идти.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class WarriorAnimation : MonoBehaviour
    {
        private Animator _animator;
        private Warrior _warrior;
        private Damageable _self;
        private AOSWarriorWrapper _mind;

        private string _now;
        private bool _fell;
        private bool _talking;
        private string _posed;
        private AnimatorUpdateMode _wasMode;

        /// <summary>Поза, заданная съёмкой, или null — тело слушает душу.</summary>
        public string Posed => _posed;

        /// <summary>
        /// Поставить позу из <see cref="BodyMotion"/> поверх решения души.
        /// Зовёт только съёмка из консоли (<c>Dev.Shooting</c>): для кадра
        /// нужен воин, который говорит, пока его снимают, а не тогда,
        /// когда так решил. null — отпустить.
        ///
        /// Поставленная поза идёт и на замершем мире: аниматор переводится
        /// на реальное время и возвращается, когда позу снимают. Так
        /// буквы могут брать интервью в застывшем бою.
        /// </summary>
        public void Pose(string state)
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            if (_animator != null)
            {
                if (_posed == null && state != null)
                {
                    _wasMode = _animator.updateMode;
                    _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                }
                else if (_posed != null && state == null)
                {
                    _animator.updateMode = _wasMode;
                }
            }

            _posed = state;
        }

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
            _self = GetComponent<Damageable>();
            _mind = GetComponent<AOSWarriorWrapper>();
        }

        void Update()
        {
            // Аниматор ищем каждый раз, пока не нашли: модель может
            // появиться позже нас — воина собирают телом вперёд,
            // а бывает и наоборот.
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            HushIfSilent();
            Arms();
            Play(State());
        }

        private string State()
        {
            // Упал один раз и навсегда: мёртвый не идёт и не бьёт,
            // а перезапуск падения выглядит как судорога.
            if (_self != null && _self.IsDead) { _fell = true; return BodyMotion.Die; }
            if (_fell) return BodyMotion.Die;

            if (_posed != null) return _posed;

            // Достаёт или убирает оружие — стоя; клип короткий и доигрывается.
            if (_armsClip != null) return _armsClip;

            if (_talking) return BodyMotion.Talk;

            // Ноги важнее решения: воин, решивший «бить», но ещё идущий
            // к цели, на экране идёт. Решение станет ударом, когда он
            // дойдёт, и спорить с ногами здесь нельзя — игрок смотрит
            // на ноги, а не на бюллетень.
            if (Walking()) return BodyMotion.Walk;

            // Реплика лагеря над головой — и рот при ней (TalkFor).
            if (Time.time < _talkUntil) return BodyMotion.Talk;

            return _mind != null ? BodyMotion.For(_mind.LastDecision) : BodyMotion.Idle;
        }

        private bool Walking()
        {
            // На уроке агент стоит вместе с миром, а Греховода ведёт урок:
            // скорости у агента нет, а ноги идут.
            if (_warrior is SinbinderPlayer && Lesson.HeroWalking) return true;

            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null || !agent.isOnNavMesh) return false;

            return agent.velocity.sqrMagnitude > 0.04f;
        }

        /// <summary>
        /// Говорит ли <b>этот</b> воин прямо сейчас.
        ///
        /// Здесь стояло «идёт ли разговор вообще»: условие спрашивало
        /// <c>InDialogue</c> дважды и **ни разу — кто говорящий**.
        /// На совете это значило, что рот шевелят все девятеро разом,
        /// а разговор в прологе — его хребет.
        ///
        /// Ставит флаг тот, кто заставляет говорить: <c>DialogueUI</c>
        /// гасит его у всех и зажигает у говорящего — эта проводка была
        /// верной с самого начала, только указывала на
        /// <c>DialogueAnimator</c>, которого никто не вешал и который
        /// дрался бы с нами за один <c>Animator</c>. Хозяин тела один,
        /// и это мы.
        /// </summary>
        public void Talk(bool on) => _talking = on;

        private float _talkUntil = -1f;

        /// <summary>
        /// Говорить столько-то секунд вне разговора: строка лагеря над головой
        /// (<see cref="CampTalk"/>). До 26 сентября реплики висели над теми,
        /// кто стоял истуканом, — автор: «живой лагерь всё ещё не живой».
        /// Своё время, а не флаг: разговору его гасит страховка, которой
        /// у строки лагеря нет.
        /// </summary>
        public void TalkFor(float seconds) => _talkUntil = Time.time + seconds;

        /// <summary>
        /// Страховка от застрявшего рта: разговор мог оборваться
        /// смертью говорящего или сменой сцены, и гасить флаг стало бы
        /// некому.
        /// </summary>
        private void HushIfSilent()
        {
            if (!_talking) return;

            var camera = Dialogue.DialogueCameraController.Instance;
            if (camera == null || !camera.InDialogue) _talking = false;
        }

        /// <summary>
        /// Переход между состояниями — смешиванием, а не скачком. Клипы
        /// «достать» и «убрать» перенесены с Mixamo и начинаются не с нашей
        /// позы покоя: скачком тело дёргалось бы на входе и на выходе.
        /// Сотня с лишним миллисекунд — меньше шага, глазу не заметно.
        /// </summary>
        private const float Blend = 0.12f;

        private void Play(string state)
        {
            if (state == _now) return;

            _now = state;
            _animator.CrossFadeInFixedTime(state, Blend);
        }

        // ---------------------------------------------------------- оружие

        /// <summary>Длина клипов — 25 и 39 кадров при 30 в секунду (motion/*.json).</summary>
        private const float DrawLength = 0.80f;
        private const float SheatheLength = 1.27f;

        /// <summary>
        /// На этом кадре «убрать» кисть отпускает рукоять (31-й из 39, `settle`):
        /// там оружие в руке и в ножнах стоят в одном месте, и подмена не видна.
        /// </summary>
        private const float SheatheRelease = 1.03f;

        /// <summary>Враг ближе этого — оружие наголо.</summary>
        private const float NearEnemy = 12f;

        /// <summary>Сколько секунд без врага рядом, прежде чем убрать оружие.</summary>
        private const float CalmBeforeSheathe = 6f;

        private Armament _arms;
        private string _armsClip;
        private float _armsStart;
        private bool _armsSwapped;
        private float _calmSince = -1f;
        private float _nextLook;
        private bool _enemyNear;

        /// <summary>Слово игрока: true — наголо, false — убрать, null — как велит бой.</summary>
        private bool? _ordered;

        /// <summary>
        /// Приказ о мече — клавиша B у Греховода (<see cref="Arms"/>). Бой
        /// сильнее приказа «убрать»: при враге рядом оружие всё равно наголо.
        /// </summary>
        public void OrderArms(bool drawn) => _ordered = drawn;

        /// <summary>
        /// Доставать и убирать оружие. Автор, 29 сентября: «сделай
        /// возможность доставать и убирать оружие». Рядом враг — наголо;
        /// шесть секунд спокойно — в ножны; в лагере в начале — в ножнах
        /// (<see cref="Wardrobe.Dress"/>). Стоя — клипом, на ходу — сменой:
        /// клип играет всё тело, и ноги на ходу скользили бы.
        /// </summary>
        private void Arms()
        {
            if (_warrior is SinbinderPlayer) ArmsKey();

            if (_arms == null) _arms = GetComponentInChildren<Armament>();
            if (_arms == null || !_arms.CanSheathe) return;
            if (_self != null && _self.IsDead) { _armsClip = null; return; }

            if (_armsClip != null)
            {
                float t = Time.time - _armsStart;
                bool drawing = _armsClip == BodyMotion.Draw;

                // «Достать» — когда смешивание кончилось и кисть уже у ножен;
                // раньше меч мелькнул бы у руки, ещё стоящей в покое.
                if (!_armsSwapped && t >= (drawing ? Blend + 0.02f : SheatheRelease))
                {
                    _arms.Show(drawing);
                    _armsSwapped = true;
                }

                if (t >= (drawing ? DrawLength : SheatheLength) || Walking())
                {
                    if (!_armsSwapped) _arms.Show(drawing);
                    _armsClip = null;
                }
                return;
            }

            bool want = WantsDrawn();
            if (want == _arms.Drawn) return;

            if (Walking() || _talking || _posed != null)
            {
                _arms.Show(want);
                return;
            }

            _armsClip = want ? BodyMotion.Draw : BodyMotion.Sheathe;
            _armsStart = Time.time;
            _armsSwapped = false;
        }

        private bool WantsDrawn()
        {
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + 0.5f;
                _enemyNear = EnemyNear();
            }

            if (_enemyNear)
            {
                _calmSince = -1f;
                return true;
            }

            if (_ordered.HasValue) return _ordered.Value;
            if (!_arms.Drawn) return false;

            if (_calmSince < 0f) _calmSince = Time.time;
            return Time.time - _calmSince < CalmBeforeSheathe;
        }

        private bool EnemyNear()
        {
            if (_warrior == null) return false;
            var at = transform.position;
            foreach (var other in Everyone())
            {
                if (other == null || other.IsDead || other.Team == _warrior.Team) continue;
                if ((other.transform.position - at).sqrMagnitude < NearEnemy * NearEnemy) return true;
            }
            return false;
        }

        /// <summary>
        /// Все воины сцены — общим списком на полсекунды: искать их каждому
        /// заново значило бы двадцать поисков по сцене дважды в секунду.
        /// </summary>
        private static Warrior[] Everyone()
        {
            if (Time.time - _everyoneAt > 0.5f || _everyoneAt < 0f)
            {
                _everyone = FindObjectsByType<Warrior>(FindObjectsSortMode.None);
                _everyoneAt = Time.time;
            }
            return _everyone;
        }

        private static Warrior[] _everyone = new Warrior[0];
        private static float _everyoneAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _everyone = new Warrior[0];
            _everyoneAt = -1f;
        }

        /// <summary>
        /// Клавиша B — меч наголо или в ножны: Греховоду и выделенным своим.
        /// B свободна (Z и Q заняты съёмкой, `Dev/Shooting`). В разговоре
        /// и на паузе руки заняты, как у сумы (<see cref="SatchelHands"/>).
        /// </summary>
        private void ArmsKey()
        {
            if (!Input.GetKeyDown(KeyCode.B)) return;
            if (Dialogue.DialogueCameraController.Instance != null
                && Dialogue.DialogueCameraController.Instance.InDialogue) return;
            if (Core.GamePauseController.Instance != null && Core.GamePauseController.Instance.IsPaused) return;

            var own = GetComponentInChildren<Armament>();
            bool drawn = !(own != null && own.Drawn);
            OrderArms(drawn);

            var selection = SelectionManager.Instance;
            if (selection == null) return;
            foreach (var unit in selection.GetSelectedUnits())
            {
                var body = unit != null ? unit.GetComponent<WarriorAnimation>() : null;
                if (body != null && body != this) body.OrderArms(drawn);
            }
        }
    }
}
