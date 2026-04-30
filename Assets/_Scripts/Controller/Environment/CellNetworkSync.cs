using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative phase + dominant-domain replication for a <see cref="Cell"/>.
    /// Lives on the same GameObject as Cell. The server polls Cell's local
    /// <see cref="Cell.LiveBlockCount"/> and <see cref="Cell.DominantDomain"/>, runs
    /// the per-biome threshold rules in <see cref="CellPhaseRules"/>, and pushes the
    /// result to NetworkVariables. Clients observe the NetworkVariables via
    /// OnValueChanged and apply the same authoritative state to their local Cell so
    /// downstream consumers (flora gates, fauna behavior) read identical phase + domain
    /// across all machines.
    ///
    /// Flora and fauna spawning is non-deterministic per-side (each client runs its own
    /// IntensityWiseLifeSpawner with local Random.value rolls), so per-side LiveBlockCount
    /// already drifts. Authoritative phase replication keeps shared gameplay rules
    /// (fauna goals, weights, danger immunity) consistent on top of that drift.
    ///
    /// Single-player fallback: if the host's NetworkObject isn't spawned (e.g., a scene
    /// loaded outside the unified Netcode pipeline), this script runs the compute path
    /// locally without touching NetworkVariables. See <see cref="IsAuthoritative"/>.
    /// </summary>
    [RequireComponent(typeof(Cell))]
    public class CellNetworkSync : NetworkBehaviour
    {
        [Tooltip("Authoritative-side compute interval. Lower = more responsive phase " +
                 "transitions, higher = less bandwidth on networked play.")]
        [Min(0.05f)] [SerializeField] float serverTickIntervalSeconds = 0.5f;

        [Tooltip("Optional explicit Cell reference. Auto-resolved via GetComponent in Awake " +
                 "if left null.")]
        [SerializeField] Cell cell;

        // NetworkVariable defaults: server writes, everyone reads.
        readonly NetworkVariable<int> _netLiveBlockCount = new(0);
        readonly NetworkVariable<CellPhase> _netPhase = new(CellPhase.Sprout);
        readonly NetworkVariable<Domains> _netDominantDomain = new(Domains.None);

        float _nextTickAt;

        bool IsAuthoritative => !IsSpawned || IsServer;

        void Awake()
        {
            if (!cell) cell = GetComponent<Cell>();
        }

        public override void OnNetworkSpawn()
        {
            _netPhase.OnValueChanged += OnNetPhaseChanged;
            _netDominantDomain.OnValueChanged += OnNetDominantDomainChanged;

            // New clients joining mid-session arrive with NetworkVariables already at
            // server's last value but no OnValueChanged event. Apply once on spawn so
            // late-joiners line up immediately rather than waiting for the next server tick.
            if (!IsServer && cell)
                cell.ApplyAuthoritativePhaseAndDomain(_netPhase.Value, _netDominantDomain.Value);

            _nextTickAt = 0f;
        }

        public override void OnNetworkDespawn()
        {
            _netPhase.OnValueChanged -= OnNetPhaseChanged;
            _netDominantDomain.OnValueChanged -= OnNetDominantDomainChanged;
        }

        void Update()
        {
            if (!cell) return;
            if (!IsAuthoritative) return;
            if (Time.time < _nextTickAt) return;

            _nextTickAt = Time.time + Mathf.Max(0.05f, serverTickIntervalSeconds);

            var thresholds = ResolveThresholds();
            var liveCount = cell.LiveBlockCount;
            var newPhase = CellPhaseRules.Compute(liveCount, cell.Phase, in thresholds);
            var newDominant = cell.DominantDomain;

            // Apply locally first so the runtime SO and OnPhaseChanged event fire on the
            // authoritative side regardless of network state. Mirror to NetworkVariables
            // only when actually networked.
            cell.ApplyAuthoritativePhaseAndDomain(newPhase, newDominant);

            if (IsSpawned)
            {
                if (_netLiveBlockCount.Value != liveCount) _netLiveBlockCount.Value = liveCount;
                if (_netPhase.Value != newPhase) _netPhase.Value = newPhase;
                if (_netDominantDomain.Value != newDominant) _netDominantDomain.Value = newDominant;
            }
        }

        void OnNetPhaseChanged(CellPhase _, CellPhase next)
        {
            // Server already wrote local state in Update; OnValueChanged on the writer
            // would double-fire OnPhaseChanged. Only clients need to apply.
            if (IsServer) return;
            if (cell) cell.ApplyAuthoritativePhaseAndDomain(next, _netDominantDomain.Value);
        }

        void OnNetDominantDomainChanged(Domains _, Domains next)
        {
            if (IsServer) return;
            if (cell) cell.ApplyAuthoritativePhaseAndDomain(_netPhase.Value, next);
        }

        CellPhaseThresholds ResolveThresholds()
        {
            var cfg = cell ? cell.Config : null;
            if (!cfg) return CellPhaseThresholds.Default;

            // Existing CellConfig assets serialized before PhaseThresholds existed
            // deserialize as struct zero — Unity does not apply the C# initializer.
            // Substitute the Default table so legacy biomes don't snap to Rabid the
            // moment the first prism is added.
            var t = cfg.PhaseThresholds;
            return t.IsAllZero ? CellPhaseThresholds.Default : t;
        }
    }
}
