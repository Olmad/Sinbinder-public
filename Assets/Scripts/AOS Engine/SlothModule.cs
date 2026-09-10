using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class SlothModule : IPersonalityModule, IMissionModule
    {
        public string ModuleID => "Sloth";
        public float Weight => 1.2f;

        private AOSConfig _config;

        public SlothModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float sin = soul.Get(SinType.Sloth);

            if (action == ActionType.Idle)
            {
                score += sin * _config.SlothIdleSinMultiplier;

                // Покой в тишине соблазняет унылого, а не всех подряд.
                // Прибавка была плоской, и в спокойном лагере «постоять»
                // получала любая душа, включая деятельную. Против неё
                // стоял один голос Верности — поэтому первый приказ
                // пролога проигрывал буквально ничему: ни врагу,
                // ни добыче, ни характеру, а просто тишине.
                if (context.DangerLevel < 0.3f)
                    score += _config.SlothIdleLowDangerBonus * Mathf.Clamp01(sin / 100f);

                // Усталость — главный союзник уныния. Чем меньше сил,
                // тем громче голос «постоять».
                score += context.Fatigue * _config.SlothIdleFatigueMultiplier;
            }

            if (action == ActionType.Attack && context.IsExhausted)
                score += _config.SlothAttackFatiguePenalty;

            if (context.DangerLevel > 0.6f || context.CurrentHP < context.MaxHP * 0.4f)
            {
                if (action == ActionType.Flee) score += _config.SlothFleeDangerThreshold;
                if (action == ActionType.Attack) score += _config.SlothAttackDangerPenalty;
            }

            if (action == ActionType.Attack)
                score -= sin * _config.SlothAttackSinMultiplier;

            if (action == ActionType.SaveAlly)
                score -= sin * _config.SlothSaveAllySinMultiplier;

            // ---------- собственные умения ----------
            //
            // Пороги стоят в разных местах шкалы намеренно. Замер
            // чувствительности (docs/12-BALANCE.md) показал, что шкалы
            // работают плато: одна единица не значит почти нигде. Порог —
            // единственное место, где она значит всё сразу, и умения дают
            // их по нескольку на каждую шкалу.
            //
            // Оба полюса: Усердие — это Уныние со знаком минус, и умения
            // у него свои. Одна шкала, два набора, восемь порогов.
            float diligence = -sin;

            switch (action)
            {
                case ActionType.LazyHeal:
                    if (sin > 25f && context.CurrentHP < context.MaxHP * 0.6f)
                        score += 30f + sin * 0.3f;
                    break;

                case ActionType.Yawn:
                    if (sin > 45f && context.NearbyEnemies > 0)
                        score += 35f + sin * 0.2f;
                    break;

                case ActionType.AuraOfApathy:
                    if (sin > 65f && context.NearbyEnemies >= 2)
                        score += 45f + sin * 0.25f;
                    break;

                case ActionType.EternalSleep:
                    // Крайнее средство унылого: он не бежит и не дерётся,
                    // он выходит из происходящего.
                    if (sin > 85f && context.IsExhausted)
                        score += 55f + sin * 0.3f;
                    break;

                case ActionType.WorkSurge:
                    if (diligence > 35f && !context.IsExhausted)
                        score += 30f + diligence * 0.25f;
                    break;

                case ActionType.Tireless:
                    if (diligence > 60f && context.Fatigue > 0.5f)
                        score += 40f + diligence * 0.3f;
                    break;

                case ActionType.WorkInspiration:
                    if (diligence > 75f && context.NearbyAllies > 0)
                        score += 45f + diligence * 0.25f;
                    break;
            }

            return score * Weight;
        }

        /// <summary>
        /// Мирная миссия. По таблице унылый проходит мимо при любой
        /// морали: всё остальное — работа.
        /// </summary>
        public float EvaluateMission(Soul soul, MissionContext context, MissionAction action)
        {
            float sin = soul.Get(SinType.Sloth) * _config.MissionSinScale;

            if (action == MissionAction.IgnoreVillage) return sin;

            // Обоз. Здесь у Уныния впервые есть степени: не всякая работа
            // одинаково тяжела. Раньше всё, кроме «пройти мимо», стоило
            // ровно -0.4 — и на развилке обоза это сделало бы «уйти»
            // хуже безделья, что прямо наоборот.
            switch (action)
            {
                case MissionAction.LetThemPass:          return sin;
                case MissionAction.TakeGoodsSparePeople: return -sin * 0.2f;
                case MissionAction.TakeEverything:       return -sin * 0.8f;
                case MissionAction.TakePeople:           return -sin;
            }

            return -sin * 0.4f;
        }

    }
}