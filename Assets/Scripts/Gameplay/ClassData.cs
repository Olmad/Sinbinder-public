using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Core
{
    [CreateAssetMenu(fileName = "ClassData", menuName = "Sinbinder/Classes/New Class")]
    public class ClassData : ScriptableObject
    {
        public string className;
        public ShellData requiredShell;           // какая оболочка нужна
        public List<SkillData> classSkills;       // навыки, которые получает класс
        public float attackModifier;
        public float defenseModifier;
        // Дополнительные параметры класса
    }
}