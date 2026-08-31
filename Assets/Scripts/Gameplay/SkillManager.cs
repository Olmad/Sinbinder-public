using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.AOS
{
    public class SkillManager : MonoBehaviour
    {
        public List<SkillData> skills = new();

        public void AddSkill(SkillData skill)
        {
            if (!skills.Contains(skill))
                skills.Add(skill);
        }
    }
}