using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative phase + dominant-domain replication for a <see cref="Cell"/>.
    /// Cell drives its own phase compute locally (see <see cref="Cell.Update"/>); this
    /// component's job is replication only:
    ///
    ///   - Server: mirror Cell.Phase + Cell.DominantDomain into NetworkVariables every
    ///     tick so clients see the authoritative values.
    ///   - Client: on NetworkVariable change, overwrite Cell's locally-computed values
    ///     with the server's via <see cref="Cell.ApplyAuthoritativePhaseAndDomain"/>.
    ///
    /// Flora and fauna spawning is non-deterministic per-side (each client runs its own
    /// IntensityWiseLifeSpawner with local Random.value rolls), so per-side LiveBlockCount
    /// drifts. Server replication keeps shared gameplay rules (fauna goals, weights,
    /// danger immunity) consistent on top of that drift.
    ///
    /// This component is OPTIONAL: Cell works without it in single-player or in scenes
    /// where the Cell GameObject has no NetworkObject. Add it (alongside a NetworkObject)
    /// to scene-placed Cells in networked game scenes (HexRace, Joust, etc.) to get
    /// server-authoritative phase across all clients.
    /// </summary>
    [RequireComponent(typeof(Cell))]
    public class CellNetworkSync : NetworkBehaviour
    {
        [Tooltip("Server-side mirror interval. Lower = phase changes propagate faster, " +
                 "higher = less bandwidth.")]
        [Min(0.05f)] [SerializeField] float serverMirrorIntervalSeconds = 0.5f;

        [SerializeField] Cell cell;

        readonly NetworkVariable<int> _netLiveBlockCount = new(0);
        readonly NetworkVariable<CellPhase> _netPhase = new(CellPhase.Calm);
        readonly NetworkVariable<Domains> _netDominantDomain = new(Domains.Blue);

        float _nextMirrorAt;

        void Awake()
        {
            if (!cell) cell = GetComponent<Cell>();
        }

        public override void OnNetworkSpawn()
        {
            _netPhase.OnValueChanged += OnNetPhaseChanged;
            _netDominantDomain.OnValueChanged += OnNetDominantDomainChanged;

            // Late-joiners arrive with NetworkVariables already at server's last value
            // but no OnValueChanged event. Apply once on spawn so the joining client's
            // Cell lines up with server immediately rather than waiting for the next
            // server-side mirror tick.
            if (!IsServer && cell)
            {
                // Pin the client's DominantDomain read to the server's answer - fauna
                // spawn color and node-control consumers must match the server even
                // when client-local trail reconstruction drifts near the nucleus
                // boundary. Cell.Update keeps recomputing phase locally; the pinned
                // dominant read overrides only the control answer.
                cell.SetReplicatedDominantDomain(_netDominantDomain.Value);
                cell.ApplyAuthoritativePhaseAndDomain(_netPhase.Value, _netDominantDomain.Value);
            }

            _nextMirrorAt = 0f;
        }

        public override void OnNetworkDespawn()
        {
            _netPhase.OnValueChanged -= OnNetPhaseChanged;
            _netDominantDomain.OnValueChanged -= OnNetDominantDomainChanged;

            // Release the pinned control read - after despawn the Cell is local-only
            // again (single-player fallback semantics).
            if (!IsServer && cell)
                cell.SetReplicatedDominantDomain(null);
        }

        void Update()
        {
            // Server-side mirror. Cell.Update already ran the threshold rules locally
            // and set Cell.Phase / wrote runtime SO - we just push the result onto the
            // wire so clients can reconcile.
            if (!IsServer || !IsSpawned) return;
            if (!cell) return;
            if (Time.time < _nextMirrorAt) return;
            _nextMirrorAt = Time.time + Mathf.Max(0.05f, serverMirrorIntervalSeconds);

            int liveCount = cell.LiveBlockCount;
            var phase = cell.Phase;
            var dominant = cell.DominantDomain;

            if (_netLiveBlockCount.Value != liveCount) _netLiveBlockCount.Value = liveCount;
            if (_netPhase.Value != phase) _netPhase.Value = phase;
            if (_netDominantDomain.Value != dominant) _netDominantDomain.Value = dominant;
        }

        void OnNetPhaseChanged(CellPhase _, CellPhase next)
        {
            // Server already updated its local Cell via Cell.Update; replication on the
            // server's side is a no-op. Only clients need to overwrite their (drifting)
            // local Cell with the server's authoritative phase.
            if (IsServer) return;
            if (cell) cell.ApplyAuthoritativePhaseAndDomain(next, _netDominantDomain.Value);
        }

        void OnNetDominantDomainChanged(Domains _, Domains next)
        {
            if (IsServer) return;
            if (cell)
            {
                cell.SetReplicatedDominantDomain(next);
                cell.ApplyAuthoritativePhaseAndDomain(_netPhase.Value, next);
            }
        }
    }
}
