// Opt-in server-side integration probe for REAL gameplay entities.
// Enable only on Multiplayer_Test with -mp-world-probe. It never replaces
// Solo SelectionManager, AOS, Warrior, TugMatch, camera or MapLoader.
using System;
using System.Collections.Generic;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    /// <summary>
    /// Discovers existing live Warriors on the actual Tug map and assigns
    /// server-owned actor IDs. It is NOT a multiplayer transport, client
    /// renderer, or replacement for the existing AOS/combat logic.
    ///
    /// This component is intentionally never auto-added in story scenes.
    /// Later, the authenticated transport can call TryIssueFromConnection
    /// and use the OrderResolved event for broadcast/snapshots.
    /// </summary>
    public sealed class ServerWorldProbe : MonoBehaviour
    {
        private sealed class Entry
        {
            public Warrior Warrior;
            public long ActorId;
            public NetworkActorRole Role;
            public int Owner;
        }

        private readonly Dictionary<int, Entry> _registered =
            new Dictionary<int, Entry>();
        private readonly Dictionary<string, Warrior> _lords =
            new Dictionary<string, Warrior>(StringComparer.Ordinal);
        private readonly List<int> _expired = new List<int>();
        private float _scanElapsed;
        private float _tickCarry;
        private long _tick;
        private bool _initialized;

        public MatchAuthority Authority { get; private set; }
        public ServerWarriorOrderBridge Orders { get; private set; }
        public int RegisteredCount { get { return _registered.Count; } }
        public long Tick { get { return _tick; } }

        public event Action<AuthorizedGameOrder, bool> OrderResolved;

        private void Awake()
        {
            // Extra protection: this is strictly a test-scene feature,
            // never a globally active component in the solo campaign.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                != "Multiplayer_Test")
            {
                enabled = false;
                return;
            }
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;
            var map = MapLoader.Current;
            if (map == null || map.Crypts.Count != 2)
                throw new InvalidOperationException(
                    "Real-world probe requires the two-crypt multiplayer QA map");
            Authority = new MatchAuthority(Guid.NewGuid().ToString("N"),
                map.Size / 2f, 2, 3);
            for (int side = 0; side < 2; side++)
                Authority.RegisterPlayer(side + 1, side,
                    Loc.F("Греховод стороны {0}", side + 1));

            Orders = new ServerWarriorOrderBridge(Authority);
            Orders.OrderResolved += (order, applied) =>
                OrderResolved?.Invoke(order, applied);
            _initialized = true;
            Debug.Log("[Sinbinder MP Probe] Enabled on Multiplayer_Test. Match=" +
                Authority.MatchId + " (diagnostic only; no real-world network transport).");
        }

        private void Update()
        {
            if (!_initialized) return;
            _scanElapsed += Time.unscaledDeltaTime;
            if (_scanElapsed >= 0.5f)
            {
                _scanElapsed = 0f;
                Scan(UnityEngine.Object.FindObjectsByType<Warrior>(
                    FindObjectsSortMode.None));
            }
            // Delayed AOS proposals are delivered at 20 server ticks/sec.
            // Existing AOS/Combat/Unity NavMesh still run only in this
            // authoritative Unity process, not on remote clients.
            Advance(Time.unscaledDeltaTime);
        }

        public void Advance(float dt)
        {
            if (!_initialized) return;
            _tickCarry += Mathf.Clamp(dt, 0f, 0.25f);
            int steps = 0;
            while (_tickCarry >= 0.05f && steps++ < 5)
            {
                _tickCarry -= 0.05f;
                Orders.ProcessDue(++_tick);
            }
            if (steps > 5) _tickCarry = 0f;
        }

        /// <summary>Server-only command entry. No untrusted JSON may
        /// specify which connection authenticated the sender.</summary>
        public bool TryIssueFromConnection(int authenticatedConnection,
            NetworkOrderIntent intent, Func<Warrior, Warrior, bool> visible,
            out AuthorizedGameOrder accepted, out OrderRejection reason)
        {
            accepted = default;
            reason = OrderRejection.NotAllowed;
            if (!_initialized) return false;
            return Orders.TryIssue(authenticatedConnection, intent, _tick,
                visible, ResolveMuffle, out accepted, out reason);
        }

        private float ResolveMuffle(Warrior actor, string lord)
        {
            if (!_lords.TryGetValue(lord, out var speaker) ||
                speaker == null || speaker.IsDead || actor == null)
                return 1f;
            var displacement = speaker.transform.position - actor.transform.position;
            float distance = Mathf.Sqrt(displacement.x * displacement.x +
                                         displacement.z * displacement.z);
            return Voice.Muffle(distance);
        }

        /// <summary>
        /// Called periodically during play, or explicitly in unit tests.
        /// Binds original Warrior instances, including dynamically spawned
        /// residents and newly resurrected Lords, without replacing them.
        /// </summary>
        public void Scan(IEnumerable<Warrior> liveWarriors)
        {
            if (!_initialized || liveWarriors == null) return;
            int previouslyRegistered = _registered.Count;
            _expired.Clear();
            foreach (var pair in _registered)
                if (pair.Value.Warrior == null || pair.Value.Warrior.IsDead)
                    _expired.Add(pair.Key);
            foreach (int key in _expired) Unregister(key);

            foreach (var warrior in liveWarriors)
            {
                if (warrior == null || warrior.IsDead) continue;
                int key = warrior.GetInstanceID();
                if (_registered.ContainsKey(key)) continue;

                var rival = warrior.GetComponent<RivalLord>();
                NetworkActorRole role;
                int owner;
                if (warrior is SinbinderPlayer)
                {
                    role = NetworkActorRole.Lord;
                    owner = 1;
                }
                else if (rival != null)
                {
                    if (rival.Side < 0 || rival.Side > 1) continue;
                    role = NetworkActorRole.Lord;
                    owner = rival.Side + 1;
                }
                else if (warrior.Team == Team.Neutral)
                {
                    role = NetworkActorRole.NeutralWarrior;
                    owner = 0;
                }
                else
                {
                    role = NetworkActorRole.AffiliatedWarrior;
                    owner = warrior.Team == Team.Player ? 1 : 2;
                }

                try
                {
                    long id = Orders.Register(warrior, role, owner);
                    _registered.Add(key, new Entry {
                        Warrior = warrior, ActorId = id,
                        Role = role, Owner = owner
                    });
                    if (role == NetworkActorRole.Lord &&
                        Authority.TryGetLord(owner, out var name))
                        _lords[name] = warrior;
                }
                catch (Exception error)
                {
                    Debug.LogWarning("[Sinbinder MP Probe] Could not register " +
                        warrior.gameObject.name + ": " + error.Message);
                }
            }
            if (_registered.Count != previouslyRegistered)
                Debug.Log("[Sinbinder MP Probe] Registered live entities: " +
                    _registered.Count + " / tick " + _tick);
        }

        private void Unregister(int key)
        {
            if (!_registered.TryGetValue(key, out var entry)) return;
            Orders.Unregister(entry.ActorId);
            _registered.Remove(key);
            if (entry.Role == NetworkActorRole.Lord &&
                Authority.TryGetLord(entry.Owner, out var name) &&
                _lords.TryGetValue(name, out var current) &&
                current == entry.Warrior)
                _lords.Remove(name);
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            Orders.CancelPending();
            _expired.Clear();
            foreach (var key in _registered.Keys)
                _expired.Add(key);
            foreach (var key in _expired)
                Unregister(key);
            _lords.Clear();
            _initialized = false;
        }
    }
}
