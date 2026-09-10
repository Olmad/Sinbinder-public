// Assets/Scripts/AOS Engine/Modules/GreedModule.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class GreedModule : IPersonalityModule, IMissionModule
    {
        public string ModuleID => "Greed";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public GreedModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float sin = soul.Get(SinType.Greed);

            switch (action)
            {
                case ActionType.Loot:
                    // Проверять здесь, есть ли добыча, не нужно: без неё
                    // Добыча не попадает в бюллетень (BehaviourResolver.BuildCandidates).
                    score += context.NearbyLoot * _config.GreedLootPerItem;
                    score += sin * _config.GreedSinMultiplier;
                    // Предметы-искусители считает TemptationResolver, один раз на решение.
                    break;

                case ActionType.SaveAlly:
                    score -= sin * _config.GreedSaveAllySinMultiplier;
                    if (sin > 50f) score += _config.GreedSaveAllyHighSinPenalty;
                    break;

                case ActionType.Attack:
                    if (context.NearbyLoot > 0) score += _config.GreedAttackPenaltyWhenLoot;
                    if (sin < -50f) score += _config.GreedAttackGoodVirtueBonus;
                    break;

                case ActionType.ObeyCommand:
                    // Долг был выключателем: до трёх невыплат — ничего,
                    // на третьей — сразу всё. Игрок не мог заметить, что
                    // задолжал, пока не становилось поздно, и не мог
                    // облегчить положение частичной уплатой. Теперь
                    // каждая невыплата слышна, и слышна по-разному:
                    // жадный ведёт счёт, щедрый машет рукой.
                    if (context.UnpaidMissions > 0)
                        score += _config.GreedObeyUnpaidPenalty
                               * UnpaidWeight(context.UnpaidMissions)
                               * UnpaidCounts(sin);
                    break;
            }
            return score * Weight;
        }

        /// <summary>
        /// Вес долга: какая доля полного счёта уже предъявлена.
        /// Полный счёт — это <c>GreedObeyUnpaidPenalty</c> из конфига,
        /// и набирается он к пятой невыплате подряд.
        ///
        /// Ровные ступени были бы неправдой: первую вылазку без платы
        /// терпят, к четвёртой уже считают, сколько всего должны.
        /// Поэтому каждая следующая невыплата дороже предыдущей
        /// на <c>GreedUnpaidGrowth</c>.
        /// </summary>
        private float UnpaidWeight(int unpaid)
        {
            float owed = Owed(unpaid);
            float reference = Owed(FullCount);
            return reference <= 0f ? 0f : owed / reference;
        }

        /// <summary>
        /// На какой невыплате счёт предъявлен полностью. Всё, что дальше,
        /// уже не меняет решения — воин и так считает себя обманутым.
        /// </summary>
        private const int FullCount = 5;

        private float Owed(int unpaid)
            => unpaid * (1f + (unpaid - 1) * _config.GreedUnpaidGrowth);

        /// <summary>
        /// Насколько этот воин вообще ведёт счёт. Долг — единственный
        /// рычаг игрока, который до сих пор действовал на всех одинаково,
        /// то есть ничего не говорил о воине.
        /// </summary>
        private float UnpaidCounts(float sin)
            => Mathf.Clamp(0.4f + sin * _config.GreedUnpaidSinShare, 0.2f, 1.2f);

        /// <summary>
        /// Мирная миссия. По таблице квеста жадный при любой морали
        /// облагает деревню данью — деньги есть деньги.
        /// </summary>
        public float EvaluateMission(Soul soul, MissionContext context, MissionAction action)
        {
            float sin = soul.Get(SinType.Greed) * _config.MissionSinScale;

            switch (action)
            {
                case MissionAction.TaxVillage:     return sin;
                case MissionAction.EnslaveVillage: return sin * 0.35f;  // доход, но хлопотный
                case MissionAction.HelpVillage:    return -sin * 0.5f;  // даром не работает
                case MissionAction.IgnoreVillage:  return -sin * 0.3f;  // мимо денег не проходит

                // Обоз. Товар без драки — лучшее, что бывает; всё
                // остальное это тот же товар, только с возн­ёй.
                case MissionAction.TakeGoodsSparePeople: return sin;
                case MissionAction.TakeEverything:       return sin * 0.9f;
                case MissionAction.TakePeople:           return sin * 0.5f;
                case MissionAction.LetThemPass:          return -sin;

                default:                           return 0f;
            }
        }

    }
}