// One lifetime owner for expedition world-time across Unity scenes.
// Does NOT grant rewards in remote scenes or bypass the game's pause.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Drives active journeys even when the crypt expedition table has
    /// been unloaded. Stage transitions and real AOS combat can happen
    /// remotely; homecoming is held until the actual crypt board and
    /// player inventory exist. A returning party never materializes in
    /// the wrong scene and never gets loot without a recipient.
    /// </summary>
    public sealed class ExpeditionWorldClock : MonoBehaviour
    {
        private static ExpeditionWorldClock _instance;
        private MissionBoard _offscreenBoard;

        public static bool IsRunning => _instance != null &&
            _instance.isActiveAndEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Expedition world clock");
            DontDestroyOnLoad(go);
            go.AddComponent<ExpeditionWorldClock>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            // A real MissionBoard has all the existing AOS resolver paths.
            // This component is deliberately not tied to a panel/canvas.
            _offscreenBoard = gameObject.AddComponent<MissionBoard>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (MissionBoard.Journeys.Count == 0 || Time.deltaTime <= 0f)
                return;

            // Only the crypt is allowed to settle its returning squads and
            // physical loot. Other scenes can advance time and resolve AOS
            // without reactivating or destroying local scene warriors.
            var activeCrypt = Object.FindFirstObjectByType<CryptMap>();
            MissionBoard deliveryBoard = activeCrypt != null
                ? CryptMap.Board : null;
            if (deliveryBoard != null &&
                (deliveryBoard.gameObject.scene != SceneManager.GetActiveScene() ||
                 Gameplay.PersonalExpedition.Active))
                deliveryBoard = null;

            var resolver = deliveryBoard != null
                ? deliveryBoard : _offscreenBoard;
            if (resolver != null)
                resolver.AdvanceJourneys(Time.deltaTime,
                    canDeliver: deliveryBoard != null);
        }
    }
}
