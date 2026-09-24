using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class LoyaltyModule : IPersonalityModule, IMissionModule, ICampModule
    {
        public string ModuleID => "Loyalty";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public LoyaltyModule()
        {
            _config = AOSConfig.Load();
        }

        /// <summary>
        /// Голос за предложение игрока на развилке.
        ///
        /// До этого верность на уровне задания не голосовала вовсе:
        /// семь модулей спорили о совести и жадности, а игрока в этом
        /// споре не было — он мог только смотреть. Теперь он в нём есть
        /// ровно на тех же правах, что и в бою: один голос, который
        /// можно перекричать.
        /// </summary>
        public float EvaluateMission(Soul soul, MissionContext context, MissionAction action)
        {
            if (!context.HasSuggestion) return 0f;
            if (action != context.SuggestedAction) return 0f;
            return soul.Loyalty * _config.MissionLoyaltyWeight;
        }

        /// <summary>Куда тянет в лагере без приказа (docs/32-CAMP.md).</summary>
        public float EvaluateSpot(Soul soul, CampSpot spot)
        {
            // Верный держится рядом с Греховодом; неверный — подальше.
            // Точка безразличия та же, что в бою (LoyaltyIndifferent).
            if (spot != CampSpot.Sinbinder) return 0f;
            return 6f * (soul.Loyalty - _config.LoyaltyIndifferent) / 100f;
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (!context.HasCommand) return 0f;

            float score = 0f;

            if (action == ActionType.ObeyCommand)
            {
                // Голос Греховода (docs/31-VOICE.md): далёкий приказ тише,
                // но верного он догоняет почти целиком. Чем выше верность,
                // тем меньше она теряет с расстоянием: верный идёт и на тихий
                // приказ, неверный — только на громкий.
                float heard = context.CommandVolume
                            + (1f - context.CommandVolume) * Mathf.Clamp01(soul.Loyalty / 100f);
                score += soul.Loyalty * _config.LoyaltyObeySinMultiplier * heard;

                // У приказа появилась цена. Уйти из ближнего боя — значит
                // подставиться под удар вслед, и «отойди» перестаёт быть
                // бесплатным. Именно это делает отказ иногда правильным,
                // а голосование — спором по существу, а не капризом.
                if (context.IsEngaged && context.CommandLeavesFight)
                    score += _config.LoyaltyObeyEngagedPenalty
                             * (context.Surrounded ? 1.5f : 1f);
            }
            else if (!context.SatisfiedBy(action))
            {
                // Верность голосовала ровно за одно действие — и потому
                // молчала везде, где приказ и без неё не был первым.
                // Замер чувствительности нашёл на ней мёртвую зону
                // в пятьдесят единиц подряд: от 21 до 70 верность
                // не меняла ни одного решения из двухсот.
                //
                // Причина не в том, что голос тих, а в том, что он
                // звучал не там. Верность — не награда за приказ,
                // а цена за то, чтобы им пренебречь: пока приказ стоит,
                // верному тяжело заняться своим. И наоборот — неверному
                // легче: ниже точки безразличия помеха превращается
                // в поблажку, и своеволие становится ему по средствам.
                //
                // Действия, которые приказ и так исполняют, не трогаем:
                // «отходи» исполняется Бегством, и мешать Бегству значило
                // бы наказывать за послушание.
                score -= (soul.Loyalty - _config.LoyaltyIndifferent)
                         * _config.LoyaltyDisobeyDrag;
            }

            return score * Weight;
        }
    }
}