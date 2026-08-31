// Assets/AOS Engine/Modules/PrideModule.cs
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    /// <summary>
    /// Гордыня — самый интересный голос совета, потому что она единственная
    /// заставляет воина платить за собственную добродетель.
    ///
    /// Гордый не отступает, когда отступить разумно. Гордый не подчиняется
    /// приказу, который его унижает. И гордый не бьёт в спину — теряя на
    /// этом урон. Смирение (отрицательная половина шкалы) делает обратное:
    /// слушается охотнее и спасает чаще.
    /// </summary>
    public class PrideModule : IPersonalityModule
    {
        public string ModuleID => "Pride";
        public float Weight => 1.1f;

        private AOSConfig _config;

        public PrideModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float pride = soul.Get(SinType.Pride);

            switch (action)
            {
                case ActionType.Attack:
                    score += pride * _config.PrideAttackSinMultiplier;

                    // Не бьёт в спину — и теряет на этом урон.
                    // Добродетель, за которую платят.
                    if (pride > 40f && context.TargetBackExposed)
                        score += _config.PrideRearStrikeRefusal;
                    break;

                case ActionType.Flee:
                    // Отступление — публичное признание слабости.
                    score -= pride * _config.PrideFleeSinMultiplier;
                    if (pride > 60f) score += _config.PrideFleeHighSinPenalty;
                    break;

                case ActionType.ObeyCommand:
                    // Смирение слушается охотно, гордыня — через силу.
                    score -= pride * _config.PrideObeySinMultiplier;
                    break;

                case ActionType.SaveAlly:
                    // Спасать — значит признать, что кто-то важнее тебя.
                    score -= pride * _config.PrideSaveAllySinMultiplier;
                    break;

                case ActionType.Idle:
                    // «Я не устал». Гордый отказывается признать, что
                    // выдохся, и продолжает, пока не свалится.
                    if (pride > 30f && context.Fatigue > 0.3f)
                        score += _config.PrideIdleFatigueRefusal;
                    break;

                case ActionType.LastStand:
                    // Последний живой и гордый — это и есть Последний рубеж.
                    if (context.LastAlive && pride > 40f)
                        score += _config.PrideLastAliveBonus + pride * 0.3f;
                    break;

                case ActionType.DuelChallenge:
                    if (pride > 50f && context.NearbyEnemies > 0)
                        score += pride * 0.5f;
                    break;

                case ActionType.HeroicPose:
                case ActionType.Inspiration:
                    if (pride > 30f && context.NearbyAllies > 0)
                        score += pride * 0.25f;
                    break;
            }

            return score * Weight;
        }
    }
}
