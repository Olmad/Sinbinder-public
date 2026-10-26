// Assets/Scripts/Gameplay/SignatureDeaths.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Смерть помнит примету: свой пал — строка журнала из <see cref="Signature.Death"/>.
    /// Ставит себя сам в сцену, где есть бой (<see cref="CombatManager"/>).
    /// </summary>
    public class SignatureDeaths : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<SignatureDeaths>() != null) return;
            var go = new GameObject("Приметы — гибель");
            DontDestroyOnLoad(go);
            go.AddComponent<SignatureDeaths>();
        }

        private CombatManager _subscribed;

        void Update()
        {
            var combat = CombatManager.Instance;
            if (combat == null || combat == _subscribed) return;
            if (_subscribed != null) _subscribed.OnAnyDeath -= OnDeath;
            combat.OnAnyDeath += OnDeath;
            _subscribed = combat;
        }

        void OnDestroy()
        {
            if (_subscribed != null) _subscribed.OnAnyDeath -= OnDeath;
        }

        private static void OnDeath(Damageable killed, GameObject killer)
        {
            var w = killed != null ? killed.Warrior : null;
            if (w == null || w.Team != Team.Player || w is SinbinderPlayer) return;
            string line = Signature.Death(w);
            if (string.IsNullOrEmpty(line)) return;
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
            Debug.Log($"[ПРИМЕТА] гибель {w.DisplayName}: {line}");
        }
    }
}
