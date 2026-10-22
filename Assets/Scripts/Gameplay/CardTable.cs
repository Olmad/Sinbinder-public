// Assets/Scripts/Gameplay/CardTable.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Стол с картами у огня. Автор, 2 октября: «Не хватает стола с картами,
    /// чтобы воины хоть как-то развлекались, и возможность к ним подсесть —
    /// понаблюдать или поучаствовать».
    ///
    /// <b>Кто играет</b> — решают души, тем же голосованием, что и прочие
    /// места лагеря (<see cref="CampSpot.Cards"/>): жадность — на кон,
    /// уныние — убить время, похоть — ради компании, зависть — смотреть,
    /// как везёт другим; гордыне и гневу за картами не сидится. Стоят
    /// вокруг ящика, лицом к нему (<see cref="CampLife"/>).
    ///
    /// <b>Подсесть</b> — F у стола: камера смотрит на игру, игроки
    /// перебрасываются словами — каждый по своему греху, по очереди,
    /// без жребия; F, Esc или отойти — встать. Пока играет — мир идёт:
    /// это не сцена, а вечер у огня.
    ///
    /// <b>Сыграть самому</b> — ждёт решения автора: что ставят и что игра
    /// меняет (верность, память, долг). Здесь — только смотреть.
    /// </summary>
    public class CardTable : MonoBehaviour
    {
        /// <summary>С какого расстояния можно подсесть, по земле.</summary>
        public const float Reach = 2.2f;

        /// <summary>Как далеко от стола игрок ещё «за картами».</summary>
        private const float Seat = 2.0f;

        /// <summary>Как часто звучит реплика за столом, секунд.</summary>
        private const float Beat = 3.4f;

        public static CardTable Instance { get; private set; }

        /// <summary>Греховод сидит за картами — камера его.</summary>
        public static bool Watching { get; private set; }

        /// <summary>Греховод у стола: F — его, а не разговора.</summary>
        public static bool Near =>
            Instance != null && SinbinderPlayer.Exists
            && CampFocus.GroundDistance(SinbinderPlayer.Where, Instance.transform.position) <= Reach;

        private float _nextLine;
        private int _turn;

        void Awake()
        {
            Instance = this;
            Watching = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Watching = false;
        }

        /// <summary>Кто сейчас за картами: свои, которых туда привела душа и которые дошли.</summary>
        public List<Warrior> Players()
        {
            var list = new List<Warrior>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer) continue;
                if (!CampLife.SpotOf(w, out var spot) || spot != CampSpot.Cards) continue;
                if (CampFocus.GroundDistance(w.transform.position, transform.position) > Seat) continue;
                list.Add(w);
            }
            return list;
        }

        // ── Сиденья ─────────────────────────────────────────────────────
        // Автор, 21 октября: «я так ни разу и не увидел, чтобы воины сидели
        // за картами». Сиденья — пни и ящик вокруг стола (сборщик: «Сиденье»).
        // Подошёл к свободному и стоит — садится: навигация на время выключена,
        // тело — на сиденье лицом к столу, поза Sit (WarriorAnimation.Seated).
        // Душа увела в другое место, приказ, набег, смерть — встаёт.

        private readonly Dictionary<Warrior, Transform> _seated = new();
        private readonly Dictionary<Warrior, float> _hipHeight = new();
        private float _nextSeat;

        /// <summary>Таз сидящего — на 41 % ниже стоячего (клип SitIdle, motion/SitIdle.json).</summary>
        private const float SeatedHips = 0.591f;

        /// <summary>Сколько сидит сейчас. Спрашивает прогон.</summary>
        public int SeatedCount => _seated.Count;

        /// <summary>Кто-нибудь из сидящих или null. Прогону — для портрета.</summary>
        public Warrior AnySeated()
        {
            foreach (var w in _seated.Keys) if (w != null) return w;
            return null;
        }

        private void Seating()
        {
            if (Time.unscaledTime < _nextSeat) return;
            _nextSeat = Time.unscaledTime + 0.3f;

            var players = Players();

            // Встать тем, кто больше не за картами.
            var leave = new List<Warrior>();
            foreach (var pair in _seated)
            {
                var w = pair.Key;
                if (w == null || w.IsDead || !players.Contains(w) || w.HasCommand || RaidEvent.Running)
                    leave.Add(w);
            }
            foreach (var w in leave) StandUp(w);

            if (RaidEvent.Running) return;

            // Сесть тем, кто дошёл и стоит у свободного сиденья.
            foreach (var w in players)
            {
                if (_seated.ContainsKey(w) || w.HasCommand) continue;
                // Призрак не садится: ног нет, в позе «сидя» он врастал в пень
                // (портрет прогона 22.10). Бесплотный стоит у стола.
                if (w.Shell == ShellType.Ghost) continue;
                var agent = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent == null || !agent.enabled || agent.velocity.sqrMagnitude > 0.04f) continue;

                Transform best = null;
                float bestD = 1.4f;
                foreach (Transform seat in transform)
                {
                    if (seat.name != "Сиденье" || _seated.ContainsValue(seat)) continue;
                    float d = CampFocus.GroundDistance(w.transform.position, seat.position);
                    if (d < bestD) { bestD = d; best = seat; }
                }
                if (best != null) SitOn(w, best, agent);
            }
        }

        private void SitOn(Warrior w, Transform seat, UnityEngine.AI.NavMeshAgent agent)
        {
            // Высота таза стоя — меряем, пока стоит.
            float hip = 0.55f;
            var hips = Find(w.transform, "Hips");
            if (hips != null) hip = Mathf.Max(0.2f, hips.position.y - w.transform.position.y);
            _hipHeight[w] = hip;

            agent.ResetPath();
            agent.enabled = false;

            // Маркер «Сиденье» сборщик ставит на верх пня или ящика.
            float top = seat.position.y;
            var pos = seat.position;
            pos.y = top + 0.04f - hip * SeatedHips;
            w.transform.position = pos;

            var to = transform.position - pos;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f) w.transform.rotation = Quaternion.LookRotation(to);

            var body = w.GetComponent<WarriorAnimation>();
            if (body != null) body.Seated = true;
            _seated[w] = seat;
        }

        private void StandUp(Warrior w)
        {
            if (w == null) { CleanNulls(); return; }
            _seated.Remove(w);
            var body = w.GetComponent<WarriorAnimation>();
            if (body != null) body.Seated = false;

            var pos = w.transform.position;
            pos.y = transform.position.y;
            var agent = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && !agent.enabled)
            {
                w.transform.position = pos;
                agent.enabled = true;
                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }
        }

        private void CleanNulls()
        {
            var dead = new List<Warrior>();
            foreach (var k in _seated.Keys) if (k == null) dead.Add(k);
            foreach (var k in dead) _seated.Remove(k);
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var hit = Find(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

        void Update()
        {
            Seating();

            if (!Watching)
            {
                // Подсесть — F у стола. Мир стоит под панелью — не наше.
                if (!Input.GetKeyDown(KeyCode.F) || !Near) return;
                var pause = GamePauseController.Instance;
                if (pause != null && pause.IsPaused) return;
                SitDown();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape)
                || !SinbinderPlayer.Exists
                || CampFocus.GroundDistance(SinbinderPlayer.Where, transform.position) > Reach + 1.5f)
            {
                StandUp();
                return;
            }

            if (Time.unscaledTime >= _nextLine)
            {
                _nextLine = Time.unscaledTime + Beat;
                Talk();
            }
        }

        /// <summary>Подсесть к игре. Никого — строка в журнал, кадра нет.</summary>
        public void SitDown()
        {
            var players = Players();
            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            if (players.Count == 0)
            {
                log?.Write(Loc.T("За картами никого: играть не с кем."));
                return;
            }

            var camera = Dialogue.DialogueCameraController.Instance;
            var eye = Camera.main;
            if (camera == null || eye == null || camera.InDialogue) return;

            Watching = true;
            _turn = 0;
            _nextLine = Time.unscaledTime + 1.2f;

            var names = new List<string>();
            foreach (var p in players) names.Add(Loc.Name(p.DisplayName));
            string caption = Loc.F("За картами: {0}", string.Join(", ", names));
            log?.Write(caption);

            // Со стороны Греховода, чуть сверху: видно ящик, карты и лица.
            Vector3 table = transform.position + Vector3.up * 0.65f;
            Vector3 from = SinbinderPlayer.Where - table;
            from.y = 0f;
            from = from.sqrMagnitude < 0.01f ? -eye.transform.forward : from.normalized;
            from = table + from * 2.1f + Vector3.up * 1.1f;

            camera.SaveCameraPosition();
            camera.StartCoroutine(camera.FocusOnPoint(table, from, caption, push: 12f));

            // Первым — кто-то из игроков встречает.
            UI.SpeechBubbles.Say(players[0], Loc.T("Владыка? Садитесь. Смотреть — не возбраняется."), 3f);
        }

        /// <summary>Встать из-за стола: камера — домой.</summary>
        public void StandUp()
        {
            if (!Watching) return;
            Watching = false;

            var camera = Dialogue.DialogueCameraController.Instance;
            if (camera == null) return;
            camera.StopSway();
            camera.StartCoroutine(camera.RestoreCamera());
        }

        /// <summary>Следующий игрок говорит свою строку — по кругу.</summary>
        private void Talk()
        {
            var players = Players();
            if (players.Count == 0) { StandUp(); return; }

            var who = players[_turn % players.Count];
            UI.SpeechBubbles.Say(who, Line(who, _turn / players.Count), 3f);
            _turn++;
        }

        /// <summary>
        /// Слово за картами — по греху, который в нём сейчас громче всех.
        /// Строк по две на грех, по очереди: без жребия.
        /// </summary>
        private static string Line(Warrior w, int round)
        {
            var soul = w.Soul;
            string[] lines;
            switch (soul != null ? soul.Sin : SinType.Pride)
            {
                case SinType.Greed:    lines = Greed;    break;
                case SinType.Envy:     lines = Envy;     break;
                case SinType.Sloth:    lines = Sloth;    break;
                case SinType.Lust:     lines = Lust;     break;
                case SinType.Gluttony: lines = Gluttony; break;
                case SinType.Wrath:    lines = Wrath;    break;
                default:               lines = Plain;    break;
            }
            return Loc.T(lines[round % lines.Length]);
        }

        private static readonly string[] Greed =
        {
            Loc.N("Ставлю медяк. Нет — два."),
            Loc.N("Не трогай кон, это моё."),
        };

        private static readonly string[] Envy =
        {
            Loc.N("Опять ему везёт."),
            Loc.N("Покажи руку. Не верю я твоим тузам."),
        };

        private static readonly string[] Sloth =
        {
            Loc.N("Сдавай. Я подожду."),
            Loc.N("Мой ход? Пропущу."),
        };

        private static readonly string[] Lust =
        {
            Loc.N("Кто проиграл — тот наливает."),
            Loc.N("Садись ближе, веселее будет."),
        };

        private static readonly string[] Gluttony =
        {
            Loc.N("На кон — вяленое мясо."),
            Loc.N("Играем на последний сухарь."),
        };

        private static readonly string[] Wrath =
        {
            Loc.N("Ещё раз туз — переверну ящик."),
            Loc.N("Кто сдавал? Ты сдавал?!"),
        };

        private static readonly string[] Plain =
        {
            Loc.N("Твой ход."),
            Loc.N("Пас."),
        };
    }
}
