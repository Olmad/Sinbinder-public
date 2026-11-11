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
        public static string Next(Decision decision, DecisionContext context)
        {
            if (context == null || !context.HasCommand ||
                (!decision.RefusedCommand && !decision.Hesitated))
                return "";

            string factor = null;
            if (decision.Weighed && decision.Decisive != Counterfactual.Factor.None)
                factor = decision.Decisive.ToString();

            // Without a decisive counterfactual, try a visible, actionable
            // circumstance before falling back to the personality voice.
            if (factor == null)
            {
                if (context.CommandVolume < 0.75f) factor = "Distance";
                else if (context.UnpaidMissions > 0) factor = "Debt";
                else if (context.KnownMurdererInSight) factor = "Murderer";
                else if (context.AllyInDanger) factor = "AllyInDanger";
                else if (context.Surrounded) factor = "Surrounded";
                else if (context.IsExhausted || context.Fatigue > 0.6f) factor = "Fatigue";
                else if (context.MaxHP > 0f && context.CurrentHP < context.MaxHP * 0.4f) factor = "Wounds";
            }

            string voice = !string.IsNullOrEmpty(decision.DecisiveVoice)
                ? decision.DecisiveVoice : decision.TopModule;
            return Loc.T(OrderAdvice.Next(factor, voice));
        }

        public static bool Fulfilled(Decision decision, DecisionContext context)
            => context != null && context.HasCommand && !decision.Hesitated
                && !decision.RefusedCommand && context.SatisfiedBy(decision.Action);
    }
}
