// Assets/Scripts/Gameplay/DreamTutorialChecks.cs
// No UnityEngine dependency: production dream logic is compiled and executed by CI.
using System;
using System.Collections.Generic;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Pure rules shared by the dream and its automated checks. Keep all
    /// coordinates in world units (X/Z), not normalized direction vectors.
    /// </summary>
    public static class DreamTutorialChecks
    {
        public const float SealRadius = 1.35f;
        public const float SealHoldSeconds = 1.2f;

        public static bool OnSeal(float x, float z, float sealX, float sealZ)
        {
            float dx = x - sealX;
            float dz = z - sealZ;
            return dx * dx + dz * dz <= SealRadius * SealRadius;
        }

        /// <summary>
        /// Both living occupants must hold their plates continuously, AFTER
        /// the counterweight releases them. Leaving either plate resets time.
        /// The caller latches a successful result; this helper is not a latch.
        /// </summary>
        public static bool AdvanceSeals(
            ref float heldSeconds, bool mechanismReleased,
            bool warriorOnSeal, bool lordOnSeal, float deltaSeconds)
        {
            if (!mechanismReleased || !warriorOnSeal || !lordOnSeal)
            {
                heldSeconds = 0f;
                return false;
            }

            if (deltaSeconds > 0f && !float.IsNaN(deltaSeconds) && !float.IsInfinity(deltaSeconds))
                heldSeconds += deltaSeconds;

            return heldSeconds >= SealHoldSeconds;
        }

        // ── Пять печатей (№69 §17): что проверяет мир, а не что решит душа ──

        /// <summary>«Выраженный Гнев» (§17): главный спектр — Гнев, на стороне порока, не слабее «тлеет»
        /// (панель воина: ≥25 — «тлеет», ≥60 — «жжёт»). Кротость — Гнев со знаком минус — не подходит.</summary>
        public const float WrathAtLeast = 25f;

        public static bool FitsWrath(bool wrathDominant, float wrath)
            => wrathDominant && wrath >= WrathAtLeast && !float.IsNaN(wrath);

        /// <summary>Двое на печатях братьев: обе заняты, это разные души и у них общий знак родни.
        /// Одинаковое прозвище, перк или ремесло — не родство.</summary>
        public static bool KinPair(string kinA, string idA, string kinB, string idB)
            => !string.IsNullOrEmpty(kinA) && kinA == kinB
               && !string.IsNullOrEmpty(idA) && !string.IsNullOrEmpty(idB) && idA != idB;

        // Four different living people are required, not four instances of
        // souls or four references to the same warrior.
        public static bool DistinctSealPeople(string wrath, string order, string brotherLeft, string brotherRight)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in new[] { wrath, order, brotherLeft, brotherRight })
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) return false;
            return true;
        }

        /// <summary>Печать брата: горит — пара на месте; вполсилы — брат на своей, второго нет
        /// или на второй не тот; тёмная — на ней никого или не брат. 0 — тёмная, 1 — ждёт, 2 — горит.</summary>
        public static int KinSeal(bool occupied, bool hasKin, bool pairStands)
            => pairStands ? 2 : occupied && hasKin ? 1 : 0;

        /// <summary>Physical interaction requires an effective, current order decision.</summary>
        public static bool MayOperateLever(bool alreadyServed, bool currentOrderDecision,
            bool refused, bool hesitated, bool obeyed)
            => !alreadyServed && currentOrderDecision && !refused && !hesitated && obeyed;

        /// <summary>Only a recorded decision for the exact lesson order is evidence.
        /// The order may already have completed/cleared when the dream observes it.</summary>
        public static bool MatchesOrderedDecision(bool contextHasCommand,
            uint expectedRevision, uint recordedRevision)
            => contextHasCommand && expectedRevision == recordedRevision;

        /// <summary>
        /// Only accept a new decision after the expected player order.
        /// Reading LastDecisionDetail alone can accidentally credit a previous
        /// vote as proof that the new lesson was completed.
        /// </summary>
        public static bool FreshOrderedDecision(
            bool correctOrder, float issuedAt, float lessonBeganAt,
            bool newContext, bool contextHasCommand)
        {
            return correctOrder && issuedAt >= lessonBeganAt &&
                !float.IsNaN(issuedAt) && newContext && contextHasCommand;
        }
    }

    internal enum BattleOutcome { Victory, Defeat, Draw }

    internal readonly struct BattleResult
    {
        public readonly BattleOutcome Outcome;
        public readonly int EnemiesKilled;
        public readonly int AlliesLost;
        public readonly string[] FallenAllies;

        public BattleResult(BattleOutcome outcome, int enemiesKilled, int alliesLost, string[] fallenAllies)
        {
            Outcome = outcome;
            EnemiesKilled = enemiesKilled;
            AlliesLost = alliesLost;
            FallenAllies = fallenAllies;
        }
    }

    /// <summary>Tracks casualties and confirmed exits independently of the live team lists.</summary>
    internal sealed class BattleEndGate
    {
        private enum EnemyExit { None, Death, Retreat }

        private bool _active;
        private bool _alliesEliminated;
        private bool _heroFell;
        private EnemyExit _lastEnemyExit;
        private int _enemiesKilled;
        private int _alliesLost;
        private readonly HashSet<string> _fallenAllies = new();

        public bool HasPendingEnd => _heroFell || _alliesEliminated || _lastEnemyExit != EnemyExit.None;

        public bool RosterChanged(bool alliesAlive, bool enemiesAlive)
        {
            if (alliesAlive) _alliesEliminated = false;
            if (enemiesAlive) _lastEnemyExit = EnemyExit.None;

            // Roster changes can cancel an encounter. A final death or confirmed retreat can end it.
            if (alliesAlive && enemiesAlive) _active = true;
            else if ((!alliesAlive && !_alliesEliminated && !_heroFell) ||
                     (!enemiesAlive && _lastEnemyExit == EnemyExit.None)) Reset();
            return _active;
        }

        public void RecordDeath(bool ally, bool sideAlive, string warriorId, bool heroFell)
        {
            if (!_active) return;
            if (ally)
            {
                _alliesLost++;
                _heroFell |= heroFell;
                _alliesEliminated = !sideAlive;
                if (!string.IsNullOrEmpty(warriorId)) _fallenAllies.Add(warriorId);
            }
            else
            {
                _enemiesKilled++;
                _lastEnemyExit = sideAlive ? EnemyExit.None : EnemyExit.Death;
            }
        }

        public void RecordEnemyRetreat(bool enemiesAlive)
        {
            if (_active) _lastEnemyExit = enemiesAlive ? EnemyExit.None : EnemyExit.Retreat;
        }

        public bool TryComplete(out BattleResult result)
        {
            result = default;
            if (!_active || !HasPendingEnd) return false;

            var outcome = _alliesEliminated && _lastEnemyExit == EnemyExit.Death ? BattleOutcome.Draw
                : _alliesEliminated || _heroFell ? BattleOutcome.Defeat : BattleOutcome.Victory;
            var fallen = new string[_fallenAllies.Count];
            _fallenAllies.CopyTo(fallen);
            result = new BattleResult(outcome, _enemiesKilled, _alliesLost, fallen);
            Reset();
            return true;
        }

        public void Reset()
        {
            _active = _alliesEliminated = _heroFell = false;
            _lastEnemyExit = EnemyExit.None;
            _enemiesKilled = _alliesLost = 0;
            _fallenAllies.Clear();
        }
    }

    /// <summary>
    /// No reward for mere intent or approach. Called only after a concrete
    /// Bodyguard.Absorb interception; protects an incident from reward spam.
    /// Kept pure for deterministic CI execution without Unity.
    /// </summary>
    public sealed class RescueCreditGate
    {
        public const float CooldownSeconds = 15f;

        // An intercepted hit must be an actual positive attack. A VFX-only
        // call, zero damage or missing attacker must not create a rescue deed.
        public static bool IsRealAttack(float damage, bool hasAttacker)
            => hasAttacker && damage > 0f && !float.IsNaN(damage) && !float.IsInfinity(damage);

        private readonly Dictionary<string, float> _last = new Dictionary<string, float>();

        public bool TryCredit(string pairKey, float now)
        {
            if (string.IsNullOrEmpty(pairKey) || float.IsNaN(now) || float.IsInfinity(now))
                return false;

            if (_last.TryGetValue(pairKey, out float last) && now - last < CooldownSeconds)
                return false;

            _last[pairKey] = now;
            return true;
        }

        public void Clear() => _last.Clear();
    }
}
