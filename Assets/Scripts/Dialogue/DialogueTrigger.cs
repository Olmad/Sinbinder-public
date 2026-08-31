// Assets/_Project/Scripts/Dialogue/DialogueTrigger.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Dialogue
{
    [System.Serializable]
    public class DialogueLine
    {
        public string SpeakerId;
        public string SpeakerName;
        public string Text;
        public float Duration;
    }

    public class DialogueTrigger : MonoBehaviour
    {
        [SerializeField] private DialogueDatabase _dialogueDatabase;
        [SerializeField] private float _triggerRadius = 15f;
        [SerializeField] private float _joinRadius = 50f;
        [SerializeField] private float _dialogueCooldown = 15f;
        [SerializeField] private int _maxParticipants = 4;

        private float _lastDialogueTime = -15f;
        private List<Warrior> _activeSpeakers = new();
        private bool _dialogueInProgress = false;
        private bool _battleDialogueTriggered = false;

        public System.Action<List<DialogueLine>> OnDialogueStart;
        public System.Action<DialogueLine> OnLineAdded;

        void Awake()
        {
            if (_dialogueDatabase != null)
                DialogueLoader.LoadDatabase(_dialogueDatabase);
        }

        void Start()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnUnitsChanged += OnUnitsChanged;
            }
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged -= OnUnitsChanged;
        }

        private void OnUnitsChanged()
        {
            if (_battleDialogueTriggered) return;
            if (Time.time - _lastDialogueTime < _dialogueCooldown) return;

            var allies = CombatManager.Instance?.GetAliveAllies() ?? new List<Damageable>();
            var enemies = CombatManager.Instance?.GetAliveEnemies() ?? new List<Damageable>();

            var player = SinbinderPlayer.Instance;
            if (player != null && !player.IsDead && !allies.Contains(player.GetComponent<Damageable>()))
                allies.Insert(0, player.GetComponent<Damageable>());

            foreach (var allyDmg in allies)
            {
                if (allyDmg == null || allyDmg.IsDead) continue;
                var ally = allyDmg.Warrior;
                if (ally == null) continue;

                foreach (var enemyDmg in enemies)
                {
                    if (enemyDmg == null || enemyDmg.IsDead) continue;
                    var enemy = enemyDmg.Warrior;
                    if (enemy == null) continue;

                    if (ally.HasSpokenWith(enemy.Id)) continue;

                    float dist = Vector3.Distance(ally.transform.position, enemy.transform.position);
                    if (dist <= _triggerRadius && !_dialogueInProgress)
                    {
                        _battleDialogueTriggered = true;
                        StartDialogue(allies, enemies);
                        return;
                    }
                }
            }
        }

        public void ResetAllBattleDialogues()
        {
            foreach (var w in CombatManager.Instance.GetAllWarriors())
                w.ResetBattleDialogue();
            _battleDialogueTriggered = false;
        }

        private void StartDialogue(List<Damageable> allies, List<Damageable> enemies)
        {
            _dialogueInProgress = true;
            _lastDialogueTime = Time.time;
            _activeSpeakers.Clear();

            var lines = new List<DialogueLine>();
            allies.Sort((a, b) => GetPriority(b.Warrior).CompareTo(GetPriority(a.Warrior)));
            enemies.Sort((a, b) => GetPriority(b.Warrior).CompareTo(GetPriority(a.Warrior)));

            var firstSpeaker = allies[0].Warrior;
            var firstTarget = enemies[0].Warrior;

            lines.Add(new DialogueLine { SpeakerId = firstSpeaker.Id, SpeakerName = firstSpeaker.DisplayName, Text = GetDialogueLine(firstSpeaker, firstTarget, "PRE_01"), Duration = 3f });
            lines.Add(new DialogueLine { SpeakerId = firstTarget.Id, SpeakerName = firstTarget.DisplayName, Text = GetDialogueLine(firstTarget, firstSpeaker, "PRE_01"), Duration = 3f });

            firstSpeaker.MarkSpokenWith(firstTarget.Id);
            firstTarget.MarkSpokenWith(firstSpeaker.Id);
            _activeSpeakers.Add(firstSpeaker);
            _activeSpeakers.Add(firstTarget);

            OnDialogueStart?.Invoke(lines);
            StartCoroutine(CheckForJoiners(allies, enemies));
        }

        private System.Collections.IEnumerator CheckForJoiners(List<Damageable> allies, List<Damageable> enemies)
        {
            yield return new WaitForSeconds(1.5f);

            foreach (var allyDmg in allies)
            {
                if (_activeSpeakers.Count >= _maxParticipants) break;
                var ally = allyDmg.Warrior;
                if (ally == null || _activeSpeakers.Contains(ally)) continue;

                foreach (var enemyDmg in enemies)
                {
                    if (_activeSpeakers.Count >= _maxParticipants) break;
                    var enemy = enemyDmg.Warrior;
                    if (enemy == null || _activeSpeakers.Contains(enemy)) continue;
                    if (ally.HasSpokenWith(enemy.Id)) continue;

                    float dist = Vector3.Distance(ally.transform.position, enemy.transform.position);
                    if (dist <= _joinRadius)
                    {
                        AddJoiner(ally, enemy);
                        yield return new WaitForSeconds(1.5f);
                        break;
                    }
                }
            }

            foreach (var enemyDmg in enemies)
            {
                if (_activeSpeakers.Count >= _maxParticipants) break;
                var enemy = enemyDmg.Warrior;
                if (enemy == null || _activeSpeakers.Contains(enemy)) continue;

                foreach (var allyDmg in allies)
                {
                    if (_activeSpeakers.Count >= _maxParticipants) break;
                    var ally = allyDmg.Warrior;
                    if (ally == null || _activeSpeakers.Contains(ally)) continue;
                    if (enemy.HasSpokenWith(ally.Id)) continue;

                    float dist = Vector3.Distance(enemy.transform.position, ally.transform.position);
                    if (dist <= _joinRadius)
                    {
                        AddJoiner(enemy, ally);
                        yield return new WaitForSeconds(1.5f);
                        break;
                    }
                }
            }

            yield return new WaitForSeconds(3f);
            _dialogueInProgress = false;
            _activeSpeakers.Clear();
        }

        private void AddJoiner(Warrior speaker, Warrior target)
        {
            var line1 = new DialogueLine
            {
                SpeakerId = speaker.Id,
                SpeakerName = speaker.DisplayName,
                Text = GetDialogueLine(speaker, target, "JOIN_01"),
                Duration = 2.5f
            };
            speaker.MarkSpokenWith(target.Id);
            target.MarkSpokenWith(speaker.Id);
            _activeSpeakers.Add(speaker);
            _activeSpeakers.Add(target);
            OnLineAdded?.Invoke(line1);

            if (_activeSpeakers.Count <= _maxParticipants)
            {
                var line2 = new DialogueLine
                {
                    SpeakerId = target.Id,
                    SpeakerName = target.DisplayName,
                    Text = GetDialogueLine(target, speaker, "JOIN_01"),
                    Duration = 2.5f
                };
                OnLineAdded?.Invoke(line2);
            }
        }

        private float GetPriority(Warrior w)
        {
            if (w == null) return 0f;
            var player = SinbinderPlayer.Instance;
            if (player != null && w.Id == player.Id) return 999f;
            float baseP = w.Soul.Sin switch { SinType.Pride => 100, SinType.Wrath => 90, SinType.Envy => 70, SinType.Lust => 60, SinType.Greed => 50, SinType.Gluttony => 40, SinType.Sloth => 10, _ => 0 };
            baseP += w.Soul.Moral switch { MoralType.Vicious => 20, MoralType.Neutral => 0, MoralType.Pious => -10, _ => 0 };
            return baseP;
        }

        private string GetDialogueLine(Warrior speaker, Warrior target, string situation)
        {
            // TryGetLine принимает говорящего и ситуацию; собеседник ей не нужен.
            // Если реплики должны обращаться к target по имени — нужна перегрузка загрузчика.
            if (DialogueLoader.TryGetLine(speaker, situation, out string text)) return text;
            return $"[{speaker.DisplayName}]: ...";
        }
    }
}