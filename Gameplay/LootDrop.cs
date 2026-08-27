using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class LootDrop : MonoBehaviour
    {
        public string ItemName;
        public bool IsCollected { get; private set; }

        void Start()
        {
            // Автоуничтожение через 2 минуты
            Destroy(gameObject, 120f);
        }

        public void Collect()
        {
            IsCollected = true;
            Destroy(gameObject, 0.2f);
        }
    }
}