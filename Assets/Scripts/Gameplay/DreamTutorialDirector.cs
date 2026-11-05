// Sinbinder: optional, isolated dream tutorial before Prologue_Camp.
// Does not alter the campaign until a level designer explicitly opts in.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public class DreamTutorialDirector : MonoBehaviour
    {
        public static DreamTutorialDirector Instance { get; private set; }
        public static bool InputLocked => Instance != null && Instance._handBusy;
        public static bool ProtectingHero => Instance != null && Instance._stage == Stage.Battle;

        [SerializeField] private Vector3 _start = new Vector3(0, 0, -27);
        [SerializeField] private Vector3 _woundedAt = new Vector3(0, 0, -2);
        [SerializeField] private Vector3 _allowed = new Vector3(0, 0, 1);
        [SerializeField] private float _backBoundary = -30f;
        [SerializeField] private float _allowedCorridorHalfWidth = 8f;
        [SerializeField] private string _campScene = "Prologue_Camp";

        private enum Stage { Approach, Dying, Harvest, Rebind, Command, Battle, FirstPerson, WarriorMode, DrawWeapon, Practice, Jars, Retreat, Return, Finished }
        private Stage _stage;
        private SinbinderPlayer _hero;
        private Warrior _wounded, _zombie;
        private readonly List<Warrior> _killers = new List<Warrior>();
        private readonly List<Sketch> _killerSketches = new List<Sketch>();
        private bool _handBusy, _battleStarted;
        private int _backAttempts;
        private int _swingsBefore, _blockFramesBefore;
        private bool _squadOrdered, _retreatOrdered, _whisperHint;
        private int _selectedJarBefore;
        private float _commandSince;
        private float _nextPrompt;
        private string _line;
        private DreamHandCursor _hand;
        private int _tutorialSeed;

        // Only intercept commands inside this optional dream. Other scenes are untouched.
        public static bool PermitDestination(Vector3 worldPoint)
        {
            var dream = Instance;
            if (dream == null || dream._stage == Stage.Finished) return true;
            if (dream._handBusy) return false;
            if (worldPoint.z >= dream._backBoundary &&
                (dream._backAttempts < 3 || Mathf.Abs(worldPoint.x) <= dream._allowedCorridorHalfWidth)) return true;
            dream.BackAttempt();
            return false;
        }

        // Used by PlayerWalk when in first-person; never moves the OS cursor.
        public static bool PermitWalking(Vector3 futurePoint)
        {
            var dream = Instance;
            if (dream == null || dream._stage == Stage.Finished) return true;
            if (dream._handBusy) return false;
            if (futurePoint.z >= dream._backBoundary &&
                (dream._backAttempts < 3 || Mathf.Abs(futurePoint.x) <= dream._allowedCorridorHalfWidth)) return true;
            dream.BackAttempt();
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _tutorialSeed = Random.Range(10000, 999999);
        }

        private void OnEnable() { SelectionManager.OnPlayerOrder += TrackOrder; }
        private void OnDisable() { SelectionManager.OnPlayerOrder -= TrackOrder; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void TrackOrder(CommandKind kind, int count)
        {
            if (count <= 0 || SelectionManager.Instance == null) return;
            if (_stage == Stage.Command && SelectionManager.Instance.Selection == SelectionManager.Picked.Squad)
                _squadOrdered = true;
            if (_stage == Stage.Retreat && kind == CommandKind.FallBack
                && SelectionManager.Instance.Selection == SelectionManager.Picked.Squad)
                _retreatOrdered = true;
        }

        private IEnumerator Start()
        {
            // The existing camp is still the real first scene for the public demo.
            // Память душ живёт в MemoryProcessor этой сцены и уходит вместе с ней.
            SquadRoster.Clear();
            Core.Satchel.Forget();
            _hero = SinbinderPlayer.Spawn(_start);
            if (_hero == null) { enabled = false; yield break; }
            _hand = gameObject.AddComponent<DreamHandCursor>();
            _hand.enabled = false;

            var names = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                var k = WarriorGenerator.Make(_tutorialSeed + i + 1, SoulOrigin.Harvest, names);
                _killerSketches.Add(k);
                names.Add(k.Name);
            }
            var dying = WarriorGenerator.Make(_tutorialSeed, SoulOrigin.Harvest, names);
            // Personal memory belongs to this soul, never to a scripted 'revenge zombie'.
            var seed = new MemorySeed {
                Object = _killerSketches[0].Name, Trigger = _killerSketches[0].Name,
                Emotion = "Ненависть", Story = "Эти люди оставили его умирать на дороге."
            };
            var soul = new SoulData(dying.Name, dying.Sin, dying.Moral, 1,
                                    dying.Strength, seed);
            soul.SetTrade(dying.Trade);
            _wounded = CreateLiving(soul, _woundedAt, Team.Neutral);
            if (_wounded == null) { enabled = false; yield break; }
            yield return null;
            var hp = _wounded.GetComponent<Damageable>();
            if (hp != null) hp.Wound(0.12f);
            Set(Stage.Approach, "Укажи Греховоду путь правой кнопкой мыши.");
        }

        private Warrior CreateLiving(SoulData soul, Vector3 at, Team team)
        {
            if (soul == null) return null;
            var go = new GameObject(soul.Name);
            go.transform.position = at;
            var w = go.AddComponent<Warrior>();
            w.Initialize(soul, ShellType.Living, new RelationshipSystem(AOS.MemoryProcessor.Instance), false, team);
            WarriorRig.Attach(go);
            WarriorLook.Build(go, ShellType.Living, 1.25f, 0.52f, 0.7f);
            var setup = FindFirstObjectByType<AOS.AOSSceneSetup>();
            if (setup != null) setup.SetupWarrior(go);
            return w;
        }

        private void Set(Stage stage, string line)
        {
            _stage = stage;
            _line = line;
            _nextPrompt = Time.unscaledTime + 15f;
            Log(line);
        }

        private void Log(string message)
        {
            FindFirstObjectByType<UI.BattleLogUI>()?.Write(message);
            Debug.Log("[СОН] " + message);
        }

        private void BackAttempt()
        {
            if (_handBusy) return;
            _backAttempts++;
            if (_backAttempts >= 3)
            {
                StartCoroutine(HandIntervenes());
            }
        }

        private IEnumerator HandIntervenes()
        {
            if (_handBusy || _hero == null) yield break;
            _handBusy = true;
            if (_hand != null)
            {
                _hand.enabled = true;
                yield return _hand.DragTo(_allowed, 1.05f);
                _hand.enabled = false;
            }
            if (_hero != null)
            {
                var mover = _hero.GetComponent<UnitMover>();
                if (mover != null) mover.CommandMove(_allowed);
            }
            _handBusy = false;
        }

        private void Update()
        {
            if (_hero == null || _stage == Stage.Finished) return;
            if (Time.unscaledTime >= _nextPrompt)
            {
                Log(_line);
                _nextPrompt = Time.unscaledTime + 25f;
            }
            switch (_stage)
            {
                case Stage.Approach:
                    if (_wounded != null && GroundDistance(_hero.transform.position, _woundedAt) <= 4.8f)
                    {
                        Set(Stage.Dying, "Незнакомец пытается что-то сказать.");
                        StartCoroutine(DyingEncounter());
                    }
                    break;
                case Stage.Harvest:
                    // The actual SoulHarvester puts the soul in a jar after E.
                    if (FindJar() >= 0)
                    {
                        Set(Stage.Rebind, "Подойди к телу. Нажми R, чтобы вернуть душу в оболочку.");
                    }
                    break;
                case Stage.Rebind:
                    if (Input.GetKeyDown(KeyCode.R) && GroundDistance(_hero.transform.position, _woundedAt) < 4f)
                        TryRebind();
                    break;
                case Stage.Command:
                    // A real refusal/hesitation exposes the temptation key, but does not force AOS to refuse.
                    if (!_whisperHint && _zombie != null && !_zombie.IsDead && Whisper.Can(_zombie))
                    {
                        _whisperHint = true;
                        Log("Воин колеблется. Z — попытаться искушать его (не гарантирует согласия).");
                    }
                    // The victim's next decision is his own, even if commanded.
                    if (_squadOrdered || Time.unscaledTime - _commandSince > 18f)
                    {
                        if (!_squadOrdered) Log("Можно вернуться к приказам в лагере.");
                        Set(Stage.Battle, "Убийцы возвращаются. Зомби помнит их и принимает решение сам.");
                        StartCoroutine(ReleaseKillers());
                    }
                    break;
                case Stage.Battle:
                    KeepHeroAlive();
                    if (_battleStarted && _killers.Count > 0 && _killers.TrueForAll(k => k == null || k.IsDead))
                        Set(Stage.FirstPerson, "Tab: перейди от тактического вида к первому лицу.");
                    break;
                case Stage.FirstPerson:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam != null && cam.FirstPersonNow)
                        Set(Stage.WarriorMode, "V: переключись в режим Воина. ЛКМ удар, ПКМ блок.");
                    break;
                }
                case Stage.WarriorMode:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    // Оружия со стойки во сне нет — Греховод бьёт посохом, и урока B не нужно.
                    if (_hero.GetComponentInChildren<Armament>(true) != null)
                        Set(Stage.DrawWeapon, "B — достань оружие. Только обнажённым оружием можно атаковать.");
                    else
                        BeginPractice("Посох всегда в руке. Ударь ЛКМ и держи блок ПКМ.");
                    break;
                }
                case Stage.DrawWeapon:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    var arms = _hero.GetComponentInChildren<Armament>(true);
                    if (arms != null && !arms.Drawn) break;
                    BeginPractice("Ударь ЛКМ и держи блок ПКМ. Урок примет только действия боевой системы.");
                    break;
                }
                case Stage.Practice:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    var combat = _hero.GetComponent<FirstPersonCombat>();
                    if (combat != null && combat.AcceptedSwings > _swingsBefore
                        && combat.AcceptedBlockFrames > _blockFramesBefore)
                    {
                        _selectedJarBefore = Satchel.Selected;
                        Set(Stage.Jars, "J: выбери другую банку в суме. Банка остаётся после поднятия.");
                    }
                    break;
                }
                case Stage.Jars:
                    // Confirm that the real SatchelHands changed the selected jar, not merely a J key press.
                    if (Satchel.Jars < 2 || Satchel.Selected != _selectedJarBefore)
                    {
                        if (_zombie != null && !_zombie.IsDead)
                            Set(Stage.Retreat, "Tab — тактика, выбери зомби, прикажи отход. F — пропустить урок.");
                        else
                            Set(Stage.Return, "Первый воин пал. Вернись к воротам и нажми F, чтобы проснуться.");
                    }
                    break;
                case Stage.Retreat:
                    if (_retreatOrdered || _zombie == null || _zombie.IsDead || Input.GetKeyDown(KeyCode.F))
                        Set(Stage.Return, "Вернись к воротам и нажми F, чтобы проснуться.");
                    break;
                case Stage.Return:
                    if (GroundDistance(_hero.transform.position, _allowed) < 5f && Input.GetKeyDown(KeyCode.F))
                        Finish();
                    break;
            }
        }

        private void BeginPractice(string line)
        {
            var combat = _hero.GetComponent<FirstPersonCombat>();
            if (combat == null)
            {
                Debug.LogError("[СОН QA] У Греховода нет FirstPersonCombat (SinbinderPlayer.Spawn).");
                Set(Stage.Return, "Личный бой не подключён. Вернись к воротам и нажми F.");
                return;
            }
            _swingsBefore = combat.AcceptedSwings;
            _blockFramesBefore = combat.AcceptedBlockFrames;
            Set(Stage.Practice, line);
        }

        private IEnumerator DyingEncounter()
        {
            if (_wounded == null || _wounded.IsDead) yield break;
            var lines = new[] { "Они вернулись...", "Я ничего у них не брал...", "Не пускай их к воротам..." };
            Log(_wounded.DisplayName + ": «" + lines[_tutorialSeed % lines.Length] + "»");
            yield return new WaitForSeconds(1.8f);
            if (_wounded == null || _wounded.IsDead) yield break;
            var hp = _wounded.GetComponent<Damageable>();
            if (hp == null) { Log("Нет Damageable у раненого."); yield break; }
            hp.TakeDamage(hp.HP + hp.MaxHP + 1f, null);
            Set(Stage.Harvest, "Подойди к душе и нажми E. Свежая душа сохраняет память.");
        }

        private int FindJar()
        {
            for (int i = 0; i < Satchel.Jars; i++)
                if (Satchel.At(i).FullJar) return i;
            return -1;
        }

        private void TryRebind()
        {
            int jar = FindJar();
            if (jar < 0) { Log("Сначала забери душу в банку."); return; }
            var slot = Satchel.At(jar);
            if (slot.Soul == null) return;
            // A tutorial reanimation is a local, explicit shortcut to Raising.Rise,
            // not a second copy of the binding/shell maths.
            var relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            var risen = Raising.Rise(slot.Soul, ShellType.Zombie,
                _woundedAt + new Vector3(2f, 0f, 0f), Quaternion.identity, relations);
            if (risen == null) { Log("Не удалось поднять тело. Душа осталась в банке."); return; }
            Satchel.TakeSoul(jar); // Only remove after successful creation.
            if (_wounded != null) _wounded.gameObject.SetActive(false);
            _zombie = risen;
            // This is actual AOS memory keyed to the actors, not Attack(killer).
            if (AOS.MemoryProcessor.Instance != null)
                foreach (var killer in _killerSketches)
                    AOS.MemoryProcessor.Instance.CreateMemory(_zombie, "MurderedMe", killer.Name,
                        AOS.EmotionType.Anger, 0.95f);
            _commandSince = Time.unscaledTime;
            Set(Stage.Command, "Выдели зомби и отдай приказ ПКМ. Его воля остаётся его собственной.");
        }

        private IEnumerator ReleaseKillers()
        {
            yield return new WaitForSeconds(1.3f);
            for (int i = 0; i < _killerSketches.Count; i++)
            {
                var k = _killerSketches[i];
                var soul = new SoulData(k.Name, k.Sin, k.Moral, 1, k.Strength, k.Seed);
                soul.SetTrade(k.Trade);
                var enemy = CreateLiving(soul, _woundedAt + new Vector3(-4f + i * 2f, 0f, 9f), Team.Enemy);
                if (enemy == null) continue;
                enemy.Attack = Mathf.Min(enemy.Attack, 2.5f); // Weaker than the hero.
                var hp = enemy.GetComponent<Damageable>();
                if (hp != null) hp.Initialize(9f, 0f);
                _killers.Add(enemy);
                // Link memory to concrete actor identity as well as the old mortal name.
                if (_zombie != null && AOS.MemoryProcessor.Instance != null)
                    AOS.MemoryProcessor.Instance.CreateMemory(_zombie, "MurderedMe", enemy.Id,
                        AOS.EmotionType.Anger, 1f);
            }
            if (_killers.Count == 0)
            {
                Debug.LogError("[СОН QA] No killers spawned. Check dependencies and Unity Console.");
                Set(Stage.Return, "Убийцы не появились. Воспоминание прервано. Вернись к воротам и нажми F.");
                yield break;
            }
            _battleStarted = true;
        }

        private void KeepHeroAlive()
        {
            // Death restores only the Death God's instrument, never the zombie.
            var body = _hero.GetComponent<Damageable>();
            if (body != null && !body.IsDead && body.HP < body.MaxHP * 0.28f)
                body.Heal(body.MaxHP * 0.55f);
        }

        private void Finish()
        {
            if (!Application.CanStreamedLevelBeLoaded(_campScene))
            {
                Log("Сцена лагеря отсутствует в Build Settings: " + _campScene);
                return;
            }
            _stage = Stage.Finished;
            SceneManager.LoadScene(_campScene);
        }

        private static float GroundDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnGUI()
        {
            if (_stage == Stage.Finished || _handBusy) return;
            var style = GUI.skin.box;
            GUI.Box(new Rect(16, Screen.height - 106, Mathf.Min(Screen.width - 32, 720), 80),
                "Воспоминание\n" + _line, style);
        }
    }
}
