using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class BodyComponent : MonoBehaviour
    {
        public bool IsCollected { get; private set; }

        public void Collect()
        {
            IsCollected = true;
            Destroy(gameObject, 0.2f);
        }
    }
}