using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class WrathModule : IPersonalityModule, IMissionModule
    {
        public string ModuleID => "Wrath";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public WrathModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float sin = soul.Get(SinType.Wrath);

            switch (action)
            {
                case ActionType.Attack:
                    // Врагов видят все, но бить их хотят не все. Прибавка
                    // за каждого противника проходит через Гнев.
                    //
                    // Раньше она была плоской, и это ломало голосование
                    // целиком: при трёх врагах +45 получала любая душа, и
                    // Похоть 30 рвалась в драку наравне с Гневом 90. Гнев
                    // оказывался громче всех у всего отряда, Послушание
                    // не могло выиграть ни при каком поведении игрока —
                    // приказ не исполнял никто и никогда.
                    //
                    // Терпение (отрицательная половина шкалы) прибавки не
                    // получает вовсе; убавку за него даёт следующая строка.
                    score += context.NearbyEnemies * _config.WrathAttackPerEnemy
                             * Mathf.Clamp01(sin / 100f);
                    score += sin * _config.WrathSinMultiplier;
                    if (context.AllyInDanger && sin > 30f) score += _config.WrathAllyInDangerBonus;
                    break;
                case ActionType.Flee:
                    score -= sin * _config.WrathFleeSinMultiplier;
                    if (sin > 60f) score += _config.WrathFleeHighSinPenalty;
                    break;
                case ActionType.Idle:
                    if (sin > 40f) score += _config.WrathIdleHighSinPenalty;
                    break;
            }

            // Гнев не считает силы: усталость его не останавливает,
            // а раззадоривает. Единственный голос, который на неё
            // отвечает прибавкой, а не убавкой.
            if (action == ActionType.Attack && sin > 40f && context.IsExhausted)
                score += _config.WrathAttackFatigueIgnore;

            return score * Weight;
        }

        /// <summary>
        /// Мирная миссия. По таблице гневный убивает: злобный вырезает
        /// всех, прочие ограничиваются путником. Который из двух —
        /// решает Мораль, поэтому здесь оба поднимаются вместе.
        /// </summary>
        public float EvaluateMission(Soul soul, MissionContext context, MissionAction action)
        {
            float sin = soul.Get(SinType.Wrath) * _config.MissionSinScale;

            switch (action)
            {
                // Обычный гнев бьёт одного; вырезать всех — это уже
                // не гнев, а злоба, и добавляет её Мораль.
                case MissionAction.KillTraveler:  return sin;
                case MissionAction.KillEveryone:  return sin * 0.8f;
                case MissionAction.DestroyAltar:  return sin * 0.4f;
                case MissionAction.IgnoreVillage: return -sin;
                case MissionAction.HelpVillage:   return -sin * 0.6f;
                default:                          return 0f;
            }
        }

    }
}