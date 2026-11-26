// Assets/Scripts/UI/AOSDecisionAdvice.cs
using Sinbinder.AOS;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.UI
{
    /// <summary>
    /// Shared advice for the selected-warrior panel and hover explanation.
    /// The counterfactual is authoritative when present. Otherwise this
    /// is a grounded suggestion, not a promise that an order will pass.
    /// </summary>
    public static class AOSDecisionAdvice
    {
        private static string Factor(Decision decision, DecisionContext context)
        {
            if (decision.Weighed)
                return decision.Decisive != Counterfactual.Factor.None
                    ? decision.Decisive.ToString() : null;

            // When counterfactual analysis is unavailable, suggest a
            // visible obstacle. Never override a weighed decisive voice.
            if (context.CommandVolume < 0.75f) return "Distance";
            if (context.UnpaidMissions > 0) return "Debt";
            if (context.KnownMurdererInSight) return "Murderer";
            if (context.AllyInDanger) return "AllyInDanger";
            if (context.Surrounded) return "Surrounded";
            if (context.IsExhausted || context.Fatigue > 0.6f) return "Fatigue";
            if (context.MaxHP > 0f && context.CurrentHP < context.MaxHP * 0.4f) return "Wounds";
            return null;
        }

        private static bool NeedsHelp(Decision d, DecisionContext c)
            => c != null && c.HasCommand && (d.RefusedCommand || d.Hesitated);

        private static string Voice(Decision decision)
            => decision.Weighed ? decision.DecisiveVoice : decision.TopModule;

        private static bool HasPair(Decision decision)
            => decision.Weighed && decision.Decisive != Counterfactual.Factor.None
                && decision.DecisiveAlso != Counterfactual.Factor.None;

        /// <summary>Решила особенность и у неё есть свой рычаг (Traits.Advice) — он; иначе null.</summary>
        private static string TraitLever(Decision decision)
            => decision.Weighed && decision.Decisive == Counterfactual.Factor.None
               && !string.IsNullOrEmpty(decision.DecisiveTrait)
                ? Traits.AdviceFor(decision.DecisiveTrait) : null;

        public static string Next(Decision decision, DecisionContext context)
        {
            if (!NeedsHelp(decision, context)) return "";
            var lever = TraitLever(decision);
            if (lever != null) return lever;
            if (HasPair(decision))
                return OrderAdvice.NextPair(decision.Decisive.ToString(),
                    decision.DecisiveAlso.ToString(), Loc.T);
            return Loc.T(OrderAdvice.Next(Factor(decision, context), Voice(decision)));
        }

        public static string NextShort(Decision decision, DecisionContext context)
        {
            if (!NeedsHelp(decision, context)) return "";
            var lever = TraitLever(decision);
            if (lever != null) return lever;
            if (HasPair(decision)) return Loc.T(OrderAdvice.PairShort);
            return Loc.T(OrderAdvice.Short(Factor(decision, context), Voice(decision)));
        }

        public static bool Fulfilled(Decision decision, DecisionContext context)
            => context != null && context.HasCommand && !decision.Hesitated
                && !decision.RefusedCommand && context.SatisfiedBy(decision.Action);
    }
}
