using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class MoralityModule : IPersonalityModule
    {
        public string ModuleID => "Morality";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public MoralityModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;

            if (soul.Morality == MoralityType.Pious)
            {
                if (action == ActionType.SaveAlly) score += _config.MoralityPiousSaveAlly;
                if (action == ActionType.Loot) score += _config.MoralityPiousLootPenalty;
                if (action == ActionType.Flee) score += _config.MoralityPiousFleePenalty;

                // Второй голос за Послушание — и единственный, идущий
                // не от верности командиру, а от самого воина. Благочестивый
                // слушается не потому, что вам доверяет, а потому, что данное
                // слово держат. Верность можно потерять, это — нет.
                //
                // До него «за» приказ говорила одна Верность, а против —
                // Гордыня, Жадность, Зависть и Похоть. Послушание проигрывало
                // всегда, и доля 2 пролога («приказ исполнен мгновенно,
                // трижды подряд») была движку недостижима.
                if (action == ActionType.ObeyCommand) score += _config.MoralityPiousObey;
            }
            else if (soul.Morality == MoralityType.Vicious)
            {
                if (action == ActionType.Attack) score += _config.MoralityViciousAttack;
                if (action == ActionType.Loot) score += _config.MoralityViciousLoot;
                if (action == ActionType.SaveAlly) score += _config.MoralityViciousSaveAllyPenalty;

                // Порочный слышит в приказе не дело, а чужую волю над собой.
                if (action == ActionType.ObeyCommand) score += _config.MoralityViciousObeyPenalty;
            }
            return score * Weight;
        }
    }
}