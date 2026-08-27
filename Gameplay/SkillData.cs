using UnityEngine;
using System;

namespace Sinbinder.AOS
{
    [CreateAssetMenu(fileName = "SkillData", menuName = "Sinbinder/Skills/New Skill")]
    public class SkillData : ScriptableObject
    {
        public string skillName;
        public ActionType actionType;      // к какому ActionType привязан
        public int cooldownTurns;
        public int manaCost;
        public SkillEffect effectType;     // тип эффекта (урон, лечение, бафф)
        public float effectValue;          // числовое значение эффекта

        // Ссылка на реализацию эффекта (может быть наследником SkillEffect)
        public SkillEffectHandler effectHandler;
    }

    // Абстрактный обработчик эффекта навыка
    public abstract class SkillEffectHandler : ScriptableObject
    {
        public abstract void Execute(Warrior user, Warrior target, float value);
    }

    public enum SkillEffect
    {
        Damage,
        Heal,
        Buff,
        Debuff,
        Summon,
        Teleport
    }
}