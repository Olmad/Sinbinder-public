using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class MoralityModule : IPersonalityModule, IMissionModule
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

        /// <summary>
        /// Мирная миссия. Мораль не выбирает поступок, а выбирает между
        /// жестоким и мягким вариантом того, к чему уже тянет грех.
        /// Отсюда в таблице квеста и берутся расхождения: гневный
        /// злобный режет всех, гневный благочестивый — одного;
        /// завистливый благочестивый вовсе помогает.
        /// </summary>
        public float EvaluateMission(Soul soul, MissionContext context, MissionAction action)
        {
            float w = _config.MissionMoralWeight;

            // Совесть запрещает громко, а предлагает тихо. Запреты идут
            // в полный вес, поощрения — в долю от него, и порок устроен
            // так же: он разрешает худшее, но не выдумывает своих целей.
            // Поэтому жадный мерзавец всё-таки обкладывает деревню данью,
            // а не угоняет её в рабство: грех выбирает поступок, мораль —
            // его меру.
            if (soul.Morality == MoralityType.Pious)
            {
                switch (action)
                {
                    case MissionAction.HelpVillage:    return w * 0.7f;
                    case MissionAction.SanctifyAltar:  return w * 0.1f;
                    case MissionAction.KillEveryone:   return -w * 2f;
                    case MissionAction.EnslaveVillage: return -w * 1.6f;
                    case MissionAction.DestroyAltar:   return -w * 1.4f;
                    case MissionAction.KillTraveler:   return -w * 0.6f;
                    case MissionAction.TaxVillage:     return -w * 0.5f;
                }
            }
            else if (soul.Morality == MoralityType.Vicious)
            {
                switch (action)
                {
                    case MissionAction.KillEveryone:   return w * 0.4f;
                    case MissionAction.DestroyAltar:   return w * 0.35f;
                    case MissionAction.EnslaveVillage: return w * 0.3f;
                    case MissionAction.TaxVillage:     return w * 0.2f;
                    case MissionAction.HelpVillage:    return -w;
                    case MissionAction.SanctifyAltar:  return -w * 0.8f;
                }
            }

            return 0f;
        }

    }
}