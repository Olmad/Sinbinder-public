using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class BattlefieldManager : MonoBehaviour
    {
        public static BattlefieldManager Instance { get; private set; }

        private List<BattlefieldData> _battlefields = new();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            for (int i = _battlefields.Count - 1; i >= 0; i--)
            {
                var bf = _battlefields[i];
                if (bf.IsCollected) continue;

                bf.RemainingTime -= Time.deltaTime;
                if (bf.RemainingTime <= 0f)
                {
                    _battlefields.RemoveAt(i);
                }
            }
        }

        public void RegisterBattlefield(BattlefieldData data)
        {
            _battlefields.Add(data);
        }

        public List<BattlefieldData> GetAvailableBattlefields()
        {
            return _battlefields.FindAll(b => !b.IsCollected && b.RemainingTime > 0f);
        }

        public BattlefieldData GetBattlefield(string id)
        {
            return _battlefields.Find(b => b.Id == id);
        }

        public void MarkCollected(string id)
        {
            var bf = GetBattlefield(id);
            if (bf != null)
                bf.IsCollected = true;
        }
    }
}