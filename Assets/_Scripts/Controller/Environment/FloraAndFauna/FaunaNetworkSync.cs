using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative replication for a single <see cref="Fauna"/> creature — the
    /// fauna counterpart of <see cref="CellNetworkSync"/>: replication only, the creature
    /// keeps owning its behavior. (Docs/ECOSYSTEM_NETWORK_SYNC.md)
    ///
    ///   - Server: runs the ONE simulation exactly as today (goals, feeding, starvation,
    ///     predation, reproduction). The spawn seam (<see cref="ServerSpawn"/>) stamps the
    ///     domain and spawns the NetworkObject; a sibling server-authoritative
    ///     NetworkTransform replicates the swim.
    ///   - Client: on spawn this component puts the sibling Fauna into PUPPET mode
    ///     (<see cref="Fauna.EnterPuppetMode"/> — no goals/steering/starvation/predation/
    ///     reproduction), then runs the normal visual Initialize (body prisms recolored to
    ///     the replicated domain + scale-in, elemental crystal provisioned). Puppets keep
    ///     two duties: the movers contract (body prisms track the replicated transform in
    ///     PrismSpatialIndex) and LOCAL GRAZING — the replicated body consumes the prisms
    ///     it passes through on THIS peer, so the prism-count reduction fauna exist for
    ///     lands on every client, and mass conservation keeps its only sink everywhere.
    ///   - Death: the server's sealed <see cref="Fauna.Die"/> flips the replicated life
    ///     state to Withering; every peer then runs the same wither-to-crystal path
    ///     locally (crystal drop + extremities-first wither — continuity law: nothing
    ///     pops out). The server despawns the spent husk only after the wither plus a
    ///     small grace so a client's later-starting wither is never clipped.
    ///
    /// This component is OPTIONAL and inert when never network-spawned: offline scenes,
    /// tool scenes, and manager-spawned fauna behave exactly as before (the
    /// CellNetworkSync optional-component philosophy).
    /// </summary>
    [RequireComponent(typeof(Fauna))]
    public class FaunaNetworkSync : NetworkBehaviour
    {
        enum LifeState : byte
        {
            Alive = 0,
            Withering = 1,
        }

        [SerializeField] Fauna fauna;

        [Tooltip("Extra seconds the SERVER waits after its own wither completes before " +
                 "despawning the husk. Clients start their wither up to ~RTT later, so " +
                 "despawning the moment the server finishes would clip the last wither " +
                 "ring on clients — a pop-out the continuity law forbids.")]
        [Min(0f)] [SerializeField] float despawnGraceSeconds = 0.5f;

        // Written once by ServerSpawn BEFORE NetworkObject.Spawn, so the value rides the
        // spawn payload and late joiners read it in OnNetworkSpawn — no change callback
        // needed (fauna keep their color for life).
        readonly NetworkVariable<Domains> _netDomain = new(Domains.Blue);

        // Alive → Withering, server-write. A NetworkVariable (not an RPC) so a peer that
        // joins mid-wither still sees a withering husk instead of a healthy creature that
        // pops out at the server's despawn.
        readonly NetworkVariable<byte> _netLifeState = new((byte)LifeState.Alive);

        void Awake()
        {
            if (!fauna) fauna = GetComponent<Fauna>();
        }

        // ------------------------------------------------------------------
        //  Authority — the one rule every ecology decision-site checks
        // ------------------------------------------------------------------

        /// <summary>
        /// True when THIS peer runs ecology decisions (spawning, feeding, death,
        /// reproduction). Under the locked EAGER-Relay design the NetworkManager is
        /// always listening and the local player is the server unless they joined a
        /// party — so solo play, offline/tool scenes, and party hosts all simulate
        /// exactly as today; only party CLIENTS become puppet-renderers.
        /// </summary>
        public static bool IsSimAuthority
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return ComputeIsSimAuthority(nm != null && nm.IsListening, nm != null && nm.IsServer);
            }
        }

        /// <summary>Pure rule for <see cref="IsSimAuthority"/> (unit-testable): a peer
        /// simulates unless a network session is live and it is not the server.</summary>
        public static bool ComputeIsSimAuthority(bool networkSessionLive, bool isServer) =>
            !networkSessionLive || isServer;

        // ------------------------------------------------------------------
        //  Spawn seam (server)
        // ------------------------------------------------------------------

        /// <summary>
        /// Networks a freshly-instantiated fauna on the server: stamps the replicated
        /// domain and spawns its NetworkObject (destroyWithScene — Netcode scene loads
        /// clean the population up on every peer). Safe to call from ANY spawn path:
        /// no-ops on clients, in offline scenes, and for species whose prefab has no
        /// NetworkObject yet — which is exactly the per-species rollout gate
        /// (Docs/ECOSYSTEM_NETWORK_SYNC.md §5).
        /// </summary>
        public static void ServerSpawn(Fauna spawned)
        {
            if (!spawned) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) return;
            if (!spawned.TryGetComponent(out NetworkObject netObj)) return;
            if (netObj.IsSpawned) return;

            // Pre-spawn NetworkVariable write: the value is serialized into the spawn
            // payload itself, so clients never see a Blue-then-recolor flicker.
            if (spawned.TryGetComponent(out FaunaNetworkSync sync))
                sync._netDomain.Value = spawned.Domain;

            netObj.Spawn(destroyWithScene: true);
        }

        /// <summary>
        /// Server-side teardown used just before a Netcode scene load (game launch):
        /// despawns every networked fauna registered on <paramref name="host"/> so a
        /// fauna spawn message can never batch into the same tick as the scene-load
        /// message (the known client-side "[Invalid Destroy]" race, see the AI-spawn
        /// lesson in CLAUDE.md). The whole scene is unloading for every peer behind a
        /// fade, so this is scene teardown — the same context in which lifeforms are
        /// destroyed today — not an in-world pop-out.
        /// </summary>
        public static void ServerDespawnBrood(Cell host)
        {
            if (!host) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) return;

            // Snapshot: Despawn destroys → Fauna.OnDestroy unregisters → mutates LiveFauna.
            var snapshot = new List<Fauna>(host.LiveFauna);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var f = snapshot[i];
                if (!f) continue;
                if (f.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                    netObj.Despawn(true);
            }
        }

        // ------------------------------------------------------------------
        //  Death replication (wither-to-crystal on every peer)
        // ------------------------------------------------------------------

        /// <summary>
        /// Called by the sealed <see cref="Fauna.Die"/>. On the server this flips the
        /// replicated life state so every client runs the same crystal-drop + wither
        /// locally; on clients (whose Die was itself replication-triggered) it no-ops.
        /// </summary>
        public void NotifyDied()
        {
            if (!IsSpawned || !IsServer) return;
            if (_netLifeState.Value != (byte)LifeState.Withering)
                _netLifeState.Value = (byte)LifeState.Withering;
        }

        /// <summary>
        /// Removal routing for a spent husk (called from the END of the wither/fade by
        /// the species' removal point). Returns true when the networked path owns the
        /// removal: the SERVER despawns after <see cref="despawnGraceSeconds"/> (grace
        /// covers clients whose wither started ~RTT later); a CLIENT does nothing — the
        /// husk is already invisible and the server's despawn destroys it. Returns
        /// false for never-spawned (offline / manager-spawned) fauna so the legacy
        /// Destroy path runs unchanged.
        /// </summary>
        public bool HandleHuskRemoval()
        {
            if (!IsSpawned) return false;
            if (!IsServer) return true;

            if (despawnGraceSeconds > 0f && isActiveAndEnabled && gameObject.activeInHierarchy)
                StartCoroutine(DespawnAfterGrace());
            else
                SafeDespawn();
            return true;
        }

        IEnumerator DespawnAfterGrace()
        {
            yield return new WaitForSeconds(despawnGraceSeconds);
            SafeDespawn();
        }

        void SafeDespawn()
        {
            if (!IsSpawned) return;
            var nm = NetworkManager;
            if (nm == null || !nm.IsListening || nm.ShutdownInProgress) return;
            if (nm.IsServer) NetworkObject.Despawn(true);
        }

        // ------------------------------------------------------------------
        //  Spawn / despawn lifecycle
        // ------------------------------------------------------------------

        public override void OnNetworkSpawn()
        {
            _netLifeState.OnValueChanged += OnLifeStateChanged;

            // Server: the spawner already ran the full Initialize before Spawn — nothing
            // to do. Clients: puppet-ize BEFORE Initialize so the sim halves (goal
            // coroutine, behavior tick) never start, then run the visual init against
            // the replicated domain. Late joiners take this same path (NGO's sync pass
            // delivers current NetworkVariable values with the spawn).
            if (IsServer) return;

            fauna.EnterPuppetMode();
            fauna.SetTeam(_netDomain.Value);
            fauna.Initialize(ResolveNearestActiveCell(transform.position));

            // Joined mid-wither: run the same local death path now (crystal + wither)
            // so the husk withers instead of popping out at the server's despawn.
            if (_netLifeState.Value == (byte)LifeState.Withering)
                fauna.ApplyReplicatedDeath();
        }

        public override void OnNetworkDespawn()
        {
            _netLifeState.OnValueChanged -= OnLifeStateChanged;
        }

        void OnLifeStateChanged(byte _, byte next)
        {
            // The server already ran its own Die; only clients mirror it.
            if (IsServer) return;
            if (next == (byte)LifeState.Withering)
                fauna.ApplyReplicatedDeath();
        }

        /// <summary>
        /// Host cell for a client puppet: nearest active cell to the replicated spawn
        /// position (cells are scene objects, identical on every peer). Falls back to
        /// null — Fauna's cellData-SO fallback then applies, matching the legacy
        /// single-cell behavior.
        /// </summary>
        static Cell ResolveNearestActiveCell(Vector3 position)
        {
            var cells = Cell.ActiveCellsSnapshot;
            Cell best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (!c) continue;
                float d = (c.transform.position - position).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = c;
                }
            }
            return best;
        }
    }
}
