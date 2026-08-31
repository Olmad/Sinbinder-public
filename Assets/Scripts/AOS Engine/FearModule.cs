using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    /// <summary>
    /// Страх — голос тела, а не характера, и своей шкалы у него нет.
    /// Трусость не восьмой грех: она не отдельная сущность, а отсутствие
    /// того, что человеку важнее страха. Семь шкал, а не восемь.
    ///
    /// Но бояться одинаково все не должны. До появления <see cref="Nerve"/>
    /// этот модуль — самый тяжёлый из тринадцати, вес 2.0 — не обращался
    /// к душе ни разу: параметр soul в Evaluate просто не использовался.
    /// Гордец с Гордыней 90 паниковал ровно как унылый, и в тесной сцене
    /// весь отряд бежал одним голосом. Тринадцать характеров сходились
    /// в один ровно там, где решается судьба боя.
    /// </summary>
    public class FearModule : IPersonalityModule
    {
        public string ModuleID => "Fear";
        public float Weight => 2.0f;

        private AOSConfig _config;

        public FearModule()
        {
            _config = AOSConfig.Load();
        }

        /// <summary>
        /// Насколько душе есть чем пересилить страх, множителем к его голосу.
        ///
        /// Гордый не покажет слабости, гневный не считает сил, унылому
        /// нечем себя пересилить. Всё это уже есть на семи шкалах —
        /// новой сущности не заводим, только читаем имеющиеся.
        ///
        /// Меньше единицы — храбрее среднего, больше — пугливее.
        /// </summary>
        private float Nerve(Soul soul)
        {
            float hold = soul.Get(SinType.Pride) * _config.NervePrideShare
                       + soul.Get(SinType.Wrath) * _config.NerveWrathShare
                       - soul.Get(SinType.Sloth) * _config.NerveSlothShare;

            return Mathf.Clamp(1f - hold / 100f,
                _config.NerveFloor, _config.NerveCeiling);
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;

            if (context.CurrentHP < context.MaxHP * 0.4f)
            {
                if (action == ActionType.Flee) score += _config.FearFleeLowHpBonus;
                if (action == ActionType.Attack) score += _config.FearAttackLowHpPenalty;
            }

            if (context.DangerLevel > 0.6f)
            {
                if (action == ActionType.Flee) score += context.DangerLevel * _config.FearFleeDangerMultiplier;
                if (action == ActionType.Idle) score += _config.FearIdleDangerPenalty;
            }

            // Окружённый боится не врага, а того, что уйти уже нельзя.
            if (context.Surrounded && action == ActionType.Flee)
                score += _config.FearFleeSurroundedBonus;

            if (action == ActionType.Attack)
                score += _config.FearAttackGlobalPenalty * 10f; // небольшая общая нерешительность

            // Через характер проходит весь голос разом, а не каждое слагаемое
            // по отдельности: страх — одно чувство, и пересиливают его целиком.
            return score * Nerve(soul) * Weight;
        }
    }
}
