// Assets/Scripts/Gameplay/SoulCourtVerdict.cs
namespace Sinbinder.Gameplay
{
    /// <summary>
    /// The Court describes the AOS action chosen at the time of a decision,
    /// not a potentially stale refusal flag. The real command semantics live
    /// in DecisionContext.SatisfiedBy; the caller passes that result through.
    /// This rule is free of Unity state so it can be tested independently.
    /// </summary>
    public enum SoulCourtVerdictKind { Obeyed, Refused, Hesitated }

    public static class SoulCourtVerdict
    {
        public static SoulCourtVerdictKind Classify(
            bool hasCommand, bool actionSatisfiesCommand,
            bool hesitated, bool reportedRefusal)
        {
            // Hesitation is no completed action, even when the held-position
            // command would otherwise accept Idle.
            if (hesitated) return SoulCourtVerdictKind.Hesitated;

            // Snapshot of the ORIGINAL command, never the warrior's current one.
            if (hasCommand)
                return actionSatisfiesCommand
                    ? SoulCourtVerdictKind.Obeyed
                    : SoulCourtVerdictKind.Refused;

            // Backward-compatible no-command diagnostics. Normal Courts have
            // HasCommand; never infer execution from a command not in evidence.
            return reportedRefusal
                ? SoulCourtVerdictKind.Refused
                : SoulCourtVerdictKind.Obeyed;
        }
    }
}
