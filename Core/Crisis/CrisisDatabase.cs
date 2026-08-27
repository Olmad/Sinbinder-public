using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    [CreateAssetMenu(fileName = "CrisisDatabase", menuName = "Sinbinder/Crisis Database")]
    public class CrisisDatabase : ScriptableObject
    {
        public List<CrisisData> Crises;
    }
}