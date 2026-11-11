// Assets/Scripts/Gameplay/CardTable.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// A genuine 36-card game of ochko (21). The existing AOS camp vote
    /// decides who comes to the table; each participant then takes real
    /// cards, decides whether to take another, and can win, lose or bust.
    /// F opens the table, including solo practice if nobody has arrived.
    /// No invented cash wagers: results produce only a small honest memory
    /// for opponents who actually played against the Sinbinder.
    /// </summary>
    public class CardTable : MonoBehaviour
    {
        /// <summary>С какого расстояния можно подсесть, по земле.</summary>
        public const float Reach = 2.2f;

        /// <summary>Как далеко от стола игрок ещё «за картами».</summary>
        private const float Seat = 2.0f;

        /// <summary>Задержка между физическими ходами NPC и раздачами.</summary>
        private const float TurnBeat = 1.45f;
        private const float NextDeal = 5.5f;

        public static CardTable Instance { get; private set; }

        /// <summary>Греховод сидит за картами — камера его.</summary>
        public static bool Watching { get; private set; }

        /// <summary>Греховод у стола: F — его, а не разговора.</summary>
        public static bool Near =>
            Instance != null && SinbinderPlayer.Exists
            && CampFocus.GroundDistance(SinbinderPlayer.Where, Instance.transform.position) <= Reach;

        private CampCardRound _round;
        private CampCardPot _pot;
        private readonly List<Warrior> _roundWarriors = new();
        private float _nextMoveAt;
        private float _nextDealAt;
        private int _deal;
        private bool _playedByHuman;
        private bool _joinNext;
        private bool _recorded;
        private string _status = "";
        private GUIStyle _headingStyle;
        private GUIStyle _rowStyle;
        private static int _escapeClosedFrame = -1;

        // Esc belongs to the card window even if another Update reads it after
        // the window has closed during the same Unity frame.
        public static bool OwnsEscape => Watching || _escapeClosedFrame == Time.frameCount;


        void Awake()
        {
            Instance = this;
            Watching = false;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            AbortRound(); // scene transition never burns escrowed coins
            if (Watching) StandUp();
            Instance = null;
            Watching = false;
        }

        /// <summary>Кто сейчас за картами: свои, которых туда привела душа и которые дошли.</summary>
        public List<Warrior> Players()
        {
            var list = new List<Warrior>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer
                    || w.HasCommand || w.Shell == ShellType.Ghost) continue;
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
            var to = transform.position - pos;
            to.y = 0f;
            var rot = to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to) : w.transform.rotation;

            var body = w.GetComponent<WarriorAnimation>();
            if (body != null) body.Seated = true;
            _seated[w] = seat;
            StartCoroutine(Settle(w, pos, rot));   // после записи: Settle проверяет, что он всё ещё сидит
        }

        /// <summary>
        /// Сесть за полсекунды, а не рывком (КТ1 п. 6: «подойти, развернуться, сесть — без телепорта»): воин
        /// встаёт у сиденья в шаге от него, и перенос на сиденье читался прыжком. Плавно, как Греховод на бревне.
        /// </summary>
        private IEnumerator Settle(Warrior w, Vector3 pos, Quaternion rot)
        {
            var from = w.transform.position;
            var fromRot = w.transform.rotation;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (w == null || !_seated.ContainsKey(w)) yield break;
                float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                w.transform.position = Vector3.Lerp(from, pos, k);
                w.transform.rotation = Quaternion.Slerp(fromRot, rot, k);
                yield return null;
            }
            if (w == null || !_seated.ContainsKey(w)) yield break;
            w.transform.position = pos;
            w.transform.rotation = rot;
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

        private sealed class WarriorWallet : ICampCardWallet
        {
            private readonly Warrior _warrior;
            public WarriorWallet(Warrior warrior) { _warrior = warrior; }
            public int Balance => _warrior != null ? _warrior.PocketGold : 0;
            public bool Spend(int amount) => _warrior != null && _warrior.TrySpendPocket(amount);
            public void Credit(int amount) { if (_warrior != null) _warrior.Pocket(amount); }
        }

        private sealed class PlayerWallet : ICampCardWallet
        {
            private readonly Inventory.PlayerInventory _inventory;
            public PlayerWallet(Inventory.PlayerInventory inventory) { _inventory = inventory; }
            public int Balance => _inventory != null ? _inventory.Gold : 0;
            public bool Spend(int amount) => _inventory != null && amount > 0 &&
                                              _inventory.SpendGold(amount);
            public void Credit(int amount) { if (_inventory != null && amount > 0) _inventory.AddGold(amount); }
        }

        /// <summary>
        /// Cancel immediately before the corpse's purse becomes ground loot;
        /// a refund on the next Update would come too late and lose coins.
        /// </summary>
        public void CancelForDeath(Warrior warrior)
        {
            if (warrior == null || _round == null || _round.Finished) return;
            if (warrior == SinbinderPlayer.Instance || _roundWarriors.Contains(warrior))
            {
                AbortRound();
                _status = Loc.T("Партия остановлена: участник погиб. Ставки возвращены.");
            }
        }

        private void AbortRound()
        {
            _pot?.Refund();
            _pot = null;
            _round = null;
            _roundWarriors.Clear();
            _playedByHuman = false;
            _joinNext = false;
        }

        void Update()
        {
            Seating();

            // Camp scenes, battle events and another modal take precedence.
            if (!CampLife.Enabled || RaidEvent.Running || OpeningReveal.Busy)
            {
                if (Watching) StandUp();
                AbortRound();
                return;
            }

            if (Watching)
            {
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape) ||
                    !SinbinderPlayer.Exists ||
                    CampFocus.GroundDistance(SinbinderPlayer.Where, transform.position) > Reach + 1.5f)
                {
                    StandUp();
                    return;
                }
                if (_round != null && !_round.Finished && _round.Current.Human)
                {
                    if (Input.GetKeyDown(KeyCode.Space)) HumanMove(true);
                    else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        HumanMove(false);
                }
                if (Input.GetKeyDown(KeyCode.N) && (_round == null || _round.Finished))
                    NewDeal(true);
            }
            else if (Input.GetKeyDown(KeyCode.F) && Near && !UI.Modal.Any &&
                     !UI.GearPanel.Open && !UI.PauseMenu.IsOpen)
            {
                var pause = GamePauseController.Instance;
                var camera = Dialogue.DialogueCameraController.Instance;
                if ((pause == null || !pause.IsPaused) && camera != null && !camera.InDialogue)
                    SitDown();
            }

            if (GamePauseController.Instance != null && GamePauseController.Instance.IsPaused)
                return;
            TickGame();
        }

        private void TickGame()
        {
            var available = Players();
            available.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));

            if (_round != null && !_round.Finished)
            {
                foreach (var participant in _roundWarriors)
                    if (participant == null || participant.IsDead || participant.HasCommand ||
                        !available.Contains(participant))
                    {
                        AbortRound(); // refund escrow; no phantom stakes on exit
                        _nextDealAt = Time.unscaledTime + NextDeal;
                        _status = Loc.T("Партия прервана: один из воинов отошёл от стола.");
                        return;
                    }

                if (_round.Current.Human) return; // actual player input, never scripted

                if (Time.unscaledTime < _nextMoveAt) return;
                var hand = _round.Current;
                var npc = _roundWarriors[_round.Turn];
                bool take = CampCardRound.WantsCard(hand.Sin, hand.Score, RecentCardBias(npc));
                _round.Play(take);
                _nextMoveAt = Time.unscaledTime + TurnBeat;
                if (npc != null)
                {
                    string action = hand.Bust ? Loc.T("Перебор.") :
                                    take ? Loc.T("Беру карту.") : Loc.T("Хватит.");
                    UI.SpeechBubbles.Say(npc, action, 2.2f);
                    npc.GetComponent<WarriorAnimation>()?.TalkFor(1.2f);
                }
                if (_round.Finished) FinishGame();
                return;
            }

            // An observer may ask to join the *next* fair deal, without
            // erasing the hand the NPCs are currently playing.
            if (Watching && _joinNext && Time.unscaledTime >= _nextDealAt)
            {
                BeginRound(available, true);
                return;
            }
            // In the background, NPCs really play the next hand rather
            // than repeating two pre-recorded phrases forever.
            if (!Watching && Time.unscaledTime >= _nextDealAt && available.Count >= 2)
                BeginRound(available, false);
        }

        /// <summary>
        /// An actual previous victory makes the warrior a little more
        /// cautious; a loss can make the next round riskier. Only the
        /// most recent vivid game memory against this Sinbinder counts.
        /// This changes a game decision, never the AOS command verdict.
        /// </summary>
        private static int RecentCardBias(Warrior who)
        {
            if (who == null || !SinbinderPlayer.Exists ||
                MemoryProcessor.Instance == null) return 0;

            MemoryRecord recent = null;
            foreach (var memory in MemoryProcessor.Instance.GetMemories(who))
            {
                if (memory == null || memory.TargetID != SinbinderPlayer.Instance.Id ||
                    (memory.EventType != "CardsLostToSinbinder" &&
                     memory.EventType != "CardsWonAgainstSinbinder")) continue;
                if (recent == null || memory.CreationTime > recent.CreationTime)
                    recent = memory;
            }
            return recent == null ? 0
                : recent.EventType == "CardsLostToSinbinder" ? 1 : -1;
        }

        private void BeginRound(List<Warrior> available, bool includeHuman, int requestedStake = 0)
        {
            // A participant who was invited may leave before their first
            // turn; taking only physically present, eligible souls is crucial.
            _roundWarriors.Clear();
            var seats = new List<(string Name, SinType Sin, bool Human)>();
            for (int i = 0; i < available.Count && seats.Count < (includeHuman ? 3 : 4); i++)
            {
                var w = available[i];
                if (w == null || w.Soul == null) continue;
                _roundWarriors.Add(w);
                seats.Add((Loc.Name(w.DisplayName), w.Soul.Sin, false));
            }
            if (includeHuman)
                seats.Add((Loc.T("Вы"), SinType.Pride, true));

            if (seats.Count == 0) return;

            // In unattended camp life, souls may choose a modest coin
            // wager when they possess money and their personalities allow it.
            if (!includeHuman && seats.Count > 1)
            {
                requestedStake = 1;
                foreach (var w in _roundWarriors)
                    if (CampCardRound.MaxStake(w.Soul.Sin, w.PocketGold) < 1)
                        requestedStake = 0;
            }
            if (requestedStake > 0)
            {
                if (seats.Count < 2)
                {
                    _status = Loc.T("Для денежной ставки нужен соперник.");
                    return;
                }
                var wallets = new List<ICampCardWallet>();
                foreach (var warrior in _roundWarriors)
                {
                    if (CampCardRound.MaxStake(warrior.Soul.Sin, warrior.PocketGold)
                        < requestedStake)
                    {
                        _status = Loc.T("Воин отказался ставить столько монет.");
                        return;
                    }
                    wallets.Add(new WarriorWallet(warrior));
                }
                if (includeHuman)
                {
                    var inventory = Inventory.PlayerInventory.Instance;
                    if (inventory == null || inventory.Gold < requestedStake)
                    {
                        _status = Loc.T("В вашем кошеле не хватает монет.");
                        return;
                    }
                    wallets.Add(new PlayerWallet(inventory));
                }
                if (!CampCardPot.TryOpen(wallets, requestedStake, out var pot))
                {
                    _status = Loc.T("Не удалось собрать ставки: ничьи деньги не списаны.");
                    return;
                }
                _pot = pot;
            }
            else
                _pot = null;
            _deal++;
            // A new shuffled deck for every deal, with seed specific to this
            // camp session. This is not predetermined win/lose for the player.
            int seed = unchecked(System.Environment.TickCount ^ (int)Time.unscaledTime ^
                                 (_deal * 7919));
            _round = new CampCardRound(seats, seed);
            _playedByHuman = includeHuman;
            _joinNext = false;
            _recorded = false;
            _nextMoveAt = Time.unscaledTime + TurnBeat;
            _nextDealAt = Time.unscaledTime + NextDeal;
            _status = seats.Count == 1 ? Loc.T("Тренировка: наберите от 17 до 21.")
                : _pot != null ? Loc.F("На кону {0} монет. Побеждает лучший результат до 21.", _pot.Total)
                : Loc.T("Новая раздача. Цель — ближе всех к 21, не перебрав.");
        }

        private void NewDeal(bool join, int stake = 0)
        {
            if (_round != null && !_round.Finished) return;
            var available = Players();
            available.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            BeginRound(available, join, stake);
        }

        private void HumanMove(bool take)
        {
            if (!Watching || _round == null || _round.Finished ||
                !_round.Current.Human) return;

            _round.Play(take);
            _status = _round.Hands[_round.Turn - (_round.Finished ? 1 : 0)].Human
                ? Loc.T("Ваш ход записан.") : Loc.T("Ход сделан.");
            _nextMoveAt = Time.unscaledTime + TurnBeat;
            if (_round.Finished) FinishGame();
        }

        private void FinishGame()
        {
            if (_recorded || _round == null || !_round.Finished) return;
            _recorded = true;
            var winners = _round.Winners();
            if (_pot != null)
            {
                // The exact winners decide the pot. All-bust refunds;
                // no negative balance, repeat payout or fictitious stake.
                _pot.Settle(winners);
                _pot = null;
            }
            if (winners.Count == 0)
            {
                _status = Loc.T("Никто не набрал достаточно очков.");
            }
            else
            {
                var names = new List<string>();
                foreach (int winner in winners) names.Add(_round.Hands[winner].Name);
                _status = Loc.F("Партия окончена. Выиграли: {0}.", string.Join(", ", names));
                int winnerNpc = winners[0];
                if (winnerNpc < _roundWarriors.Count && _roundWarriors[winnerNpc] != null)
                    UI.SpeechBubbles.Say(_roundWarriors[winnerNpc], Loc.T("Эта партия моя."), 2.6f);
            }

            // Playing cards creates a modest actual memory, not fake cash
            // or raw loyalty. Existing MemoryProcessor applies its usual
            // decay and protects against identical reward farming.
            if (_playedByHuman && _round.Hands.Count > 1 &&
                SinbinderPlayer.Exists && MemoryProcessor.Instance != null)
            {
                int hero = _round.Hands.Count - 1;
                bool heroWon = winners.Contains(hero);
                for (int i = 0; i < _roundWarriors.Count; i++)
                {
                    var warrior = _roundWarriors[i];
                    if (warrior == null || warrior.IsDead) continue;
                    bool npcWon = winners.Contains(i);
                    string eventType;
                    EmotionType emotion;
                    float importance;
                    if (heroWon && !npcWon)
                    {
                        eventType = "CardsLostToSinbinder";
                        emotion = warrior.Soul != null && (warrior.Soul.Sin == SinType.Greed ||
                            warrior.Soul.Sin == SinType.Pride) ? EmotionType.Anger : EmotionType.Surprise;
                        importance = 0.10f;
                    }
                    else if (npcWon && !heroWon)
                    {
                        eventType = "CardsWonAgainstSinbinder";
                        emotion = EmotionType.Joy;
                        importance = 0.10f;
                    }
                    else
                    {
                        eventType = "CardsDrewWithSinbinder";
                        emotion = EmotionType.Calm;
                        importance = 0.06f;
                    }
                    MemoryProcessor.Instance.CreateMemory(warrior, eventType,
                        SinbinderPlayer.Instance.Id, emotion, importance);
                }
            }
            _nextDealAt = Time.unscaledTime + NextDeal;
            // Ordinary camp life should not dump a match result into the
            // battle log every few seconds when the player is elsewhere.
            if (Watching) Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(_status);
        }

        /// <summary>F at the table: watch a live round or play yourself.</summary>
        public void SitDown()
        {
            if (Watching || !SinbinderPlayer.Exists) return;
            var camera = Dialogue.DialogueCameraController.Instance;
            var eye = Camera.main;
            if (camera == null || eye == null || camera.InDialogue) return;

            Watching = true;
            _joinNext = false;
            _status = Loc.T("Наблюдайте за ходами или нажмите «Сыграть».");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Vector3 table = transform.position + Vector3.up * 0.65f;
            Vector3 from = SinbinderPlayer.Where - table;
            from.y = 0f;
            from = from.sqrMagnitude < 0.01f ? -eye.transform.forward : from.normalized;
            from = table + from * 2.1f + Vector3.up * 1.1f;
            camera.SaveCameraPosition();
            camera.StartCoroutine(camera.FocusOnPoint(table, from,
                Loc.T("Карты у костра"), push: 12f, fov: 52f));
        }

        public void StandUp()
        {
            if (!Watching) return;
            Watching = false;
            _joinNext = false;
            _escapeClosedFrame = Time.frameCount;
            // Leaving a live player hand is abandonment, not a win or
            // an event that grants AOS memory. NPCs can deal again later.
            if (_playedByHuman && _round != null && !_round.Finished)
            {
                AbortRound(); // player walked away: return every escrowed coin
                _nextDealAt = Time.unscaledTime + NextDeal;
            }
            var camera = Dialogue.DialogueCameraController.Instance;
            if (camera != null)
            {
                camera.StopSway();
                camera.StartCoroutine(camera.RestoreCamera());
            }
        }

        private void OnGUI()
        {
            if (!Watching) return;
            float width = Mathf.Clamp(Screen.width - 24f, 280f, 710f);
            float height = Mathf.Clamp(Screen.height - 35f, 290f, 500f);
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;
            GUI.Box(new Rect(x, y, width, height), "");
            if (_headingStyle == null)
            {
                _headingStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
                _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            }

            GUILayout.BeginArea(new Rect(x + 16f, y + 10f, width - 32f, height - 20f));
            GUILayout.Label(Loc.T("КАРТЫ У КОСТРА · ОЧКО (21)"), _headingStyle);
            GUILayout.Label(Loc.T("36 карт: 6–10 по номиналу, В=2, Д=3, К=4, Т=11."), _rowStyle);
            if (_pot != null)
                GUILayout.Label(Loc.F("Ставка: {0} с игрока; в банке: {1}.",
                    _pot.Stake, _pot.Total), _rowStyle);
            var purse = Inventory.PlayerInventory.Instance;
            if (purse != null)
                GUILayout.Label(Loc.F("Ваш кошель: {0} монет.", purse.Gold), _rowStyle);
            GUILayout.Space(9f);
            if (_round != null)
            {
                for (int i = 0; i < _round.Hands.Count; i++)
                {
                    var hand = _round.Hands[i];
                    string cards = "";
                    foreach (var c in hand.Cards)
                        cards += (cards.Length == 0 ? "" : "    ") + c.Face;
                    string score = hand.Bust ? Loc.T("ПЕРЕБОР") : hand.Score.ToString();
                    string state = !_round.Finished && _round.Turn == i ? Loc.T(" ← ход") :
                                   hand.Standing ? Loc.T(" · закончил") : "";
                    GUILayout.Label(Loc.F("{0}: {1}  [{2}]{3}", hand.Name, cards, score, state), _rowStyle);
                    GUILayout.Space(4f);
                }
            }
            else
            {
                GUILayout.Label(Loc.T("За столом пока нет начатой партии."), _rowStyle);
            }
            GUILayout.Space(12f);
            GUILayout.Label(_status, _rowStyle);
            if (_round != null && !_round.Finished && _round.Current.Human)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Loc.T("Взять карту (Пробел)"), GUILayout.Height(42f)))
                    HumanMove(true);
                if (GUILayout.Button(Loc.T("Хватит (Enter)"), GUILayout.Height(42f)))
                    HumanMove(false);
                GUILayout.EndHorizontal();
            }
            else if (_round == null || _round.Finished)
            {
                if (GUILayout.Button(Loc.T("Бесплатная партия (N)"), GUILayout.Height(35f)))
                    NewDeal(true);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Loc.T("Поставить 1"), GUILayout.Height(33f)))
                    NewDeal(true, 1);
                if (GUILayout.Button(Loc.T("Поставить 5"), GUILayout.Height(33f)))
                    NewDeal(true, 5);
                if (GUILayout.Button(Loc.T("Поставить 10"), GUILayout.Height(33f)))
                    NewDeal(true, 10);
                GUILayout.EndHorizontal();
                if (Players().Count < 2)
                    GUILayout.Label(Loc.T("Нет соперников: доступна тренировка или игра с одним воином."),
                        _rowStyle);
            }
            else
            {
                GUILayout.Label(Loc.T("Сейчас ход воина. Решение зависит от его характера."),
                    _rowStyle);
                if (!_playedByHuman && !_joinNext &&
                    GUILayout.Button(Loc.T("Сыграть после этой раздачи"), GUILayout.Height(38f)))
                {
                    _joinNext = true;
                    _status = Loc.T("Вы присоединитесь к следующей раздаче.");
                }
                else if (_joinNext)
                    GUILayout.Label(Loc.T("Следующая раздача будет с вашим участием."), _rowStyle);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Loc.T("Встать из-за стола (F / Esc)"), GUILayout.Height(35f)))
                StandUp();
            GUILayout.EndArea();
        }
    }
}
