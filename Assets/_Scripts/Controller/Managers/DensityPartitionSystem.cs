using System.Diagnostics;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One pre-computed densest-region answer for a single anti-domain bucket
    /// (i.e. the densest region of all prisms NOT belonging to the friendly
    /// domain that's asking). Sent across the wire as one atomic payload so
    /// position, density, owning-cell id, and version always replicate together.
    /// </summary>
    public struct PartitionSolution : INetworkSerializable, System.IEquatable<PartitionSolution>
    {
        public Vector3 Position;
        public float Density;
        public int CellId;

        // Stride at the time of compute, so consumers can re-derive the bucket
        // bounds for AOE / nav purposes without round-tripping to the cell.
        public float Stride;

        // Friendly domain this solution is "anti" to, packed as int for serializer.
        public int AntiOfDomain;

        // Monotonic counter so consumers can tell whether a poll returned a
        // new answer or the same one they saw last frame.
        public uint Version;

        public bool HasResult => Density > 0f;

        public Domains AntiOfDomainEnum => (Domains)AntiOfDomain;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Density);
            serializer.SerializeValue(ref CellId);
            serializer.SerializeValue(ref Stride);
            serializer.SerializeValue(ref AntiOfDomain);
            serializer.SerializeValue(ref Version);
        }

        public bool Equals(PartitionSolution other) =>
            Position.Equals(other.Position)
            && Density == other.Density
            && CellId == other.CellId
            && Stride == other.Stride
            && AntiOfDomain == other.AntiOfDomain
            && Version == other.Version;

        public override bool Equals(object obj) => obj is PartitionSolution o && Equals(o);

        public override int GetHashCode() =>
            Position.GetHashCode() ^ Density.GetHashCode() ^ CellId ^ AntiOfDomain ^ (int)Version;
    }

    /// <summary>
    /// Network-synced periodic aggregator over per-Cell density grids.
    ///
    /// Computes three "anti-domain" solutions on the server every
    /// <see cref="recomputeIntervalSeconds"/> and replicates them as
    /// <see cref="NetworkVariable{T}"/>s so any reader (AI, fauna, vessel
    /// abilities) can poll the latest answer with no per-reader recompute cost:
    ///
    /// <list type="bullet">
    ///   <item>Anti-Jade = densest region of {Ruby ∪ Gold} prisms.</item>
    ///   <item>Anti-Ruby = densest region of {Jade ∪ Gold} prisms.</item>
    ///   <item>Anti-Gold = densest region of {Jade ∪ Ruby} prisms.</item>
    /// </list>
    ///
    /// The buckets aren't recomputed here — <see cref="Cell"/> already keeps a
    /// per-team <see cref="BlockCountDensityGrid"/> where the per-domain bucket
    /// stores every block NOT belonging to that domain (the existing
    /// "anti-domain" semantic). This system only picks the strongest centroid
    /// across all active cells, stamps a version, and broadcasts.
    ///
    /// Hybrid event entry: <see cref="RequestImmediateRecompute"/> is the
    /// cooldowned event path. Many simultaneous callers within
    /// <see cref="eventCooldownSeconds"/> coalesce to a single recompute.
    /// </summary>
    public class DensityPartitionSystem : NetworkBehaviour
    {
        [Header("Recompute cadence")]
        [Tooltip("Seconds between server-side recomputes. Default 0.5s (2 Hz) " +
                 "comfortably covers fauna seeking and AI target choice without " +
                 "burning CPU on every frame.")]
        [SerializeField] float recomputeIntervalSeconds = 0.5f;

        [Tooltip("Minimum seconds between RequestImmediateRecompute() responses. " +
                 "Many event-driven callers within the cooldown coalesce to a " +
                 "single recompute, preventing thrash on volatile prism volumes.")]
        [SerializeField] float eventCooldownSeconds = 0.25f;

        [Header("Diagnostics")]
        [Tooltip("If true, log each recompute's millisecond cost.")]
        [SerializeField] bool verboseProfiling;

        // ── Networked solutions (server writes, everyone reads) ─────────────
        // Default read perm = Everyone, default write perm = Server. We use the
        // explicit constructor so non-spawned writes don't throw on the host
        // before OnNetworkSpawn (NetworkVariable buffers writes pre-spawn).
        readonly NetworkVariable<PartitionSolution> _antiJade =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<PartitionSolution> _antiRuby =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<PartitionSolution> _antiGold =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Local cache mirrors of the above ────────────────────────────────
        // Server fills these directly each Recompute() so reads work even
        // before/without OnNetworkSpawn. Clients overwrite them from the
        // OnValueChanged callback so reads are O(1) (no Value-getter call).
        PartitionSolution _localAntiJade;
        PartitionSolution _localAntiRuby;
        PartitionSolution _localAntiGold;

        // ── Server-only tick state ──────────────────────────────────────────
        float _nextRecomputeAt;
        float _earliestEventRecomputeAt;
        uint _version;

        // ── Diagnostics ─────────────────────────────────────────────────────
        public uint Version => _version;
        public int LastRecomputeCellsScanned { get; private set; }
        public float LastRecomputeMillis { get; private set; }
        public float RecomputeIntervalSeconds => recomputeIntervalSeconds;
        public float EventCooldownSeconds => eventCooldownSeconds;
        public float NextRecomputeIn => Mathf.Max(0f, _nextRecomputeAt - Time.time);

        // ── Singleton-ish accessor ──────────────────────────────────────────
        // Lazy scene scan so any AI / fauna / ability can fetch the system
        // without manual wiring. Only one instance is expected per game scene.
        static DensityPartitionSystem _active;
        public static DensityPartitionSystem Active
        {
            get
            {
                if (_active != null) return _active;
                _active = FindFirstObjectByType<DensityPartitionSystem>(FindObjectsInactive.Exclude);
                return _active;
            }
        }

        void Awake()
        {
            if (_active != null && _active != this)
            {
                CSDebug.LogWarning($"[DensityPartitionSystem] Multiple instances detected; " +
                                   $"keeping the original on '{_active.gameObject.name}' and " +
                                   $"ignoring '{gameObject.name}'.");
                enabled = false;
                return;
            }
            _active = this;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_active == this) _active = null;
        }

        public override void OnNetworkSpawn()
        {
            // Seed the local cache from the current networked value so a
            // client that joins mid-session reads the latest answer instead
            // of an empty default.
            _localAntiJade = _antiJade.Value;
            _localAntiRuby = _antiRuby.Value;
            _localAntiGold = _antiGold.Value;

            _antiJade.OnValueChanged += OnAntiJadeChanged;
            _antiRuby.OnValueChanged += OnAntiRubyChanged;
            _antiGold.OnValueChanged += OnAntiGoldChanged;
        }

        public override void OnNetworkDespawn()
        {
            _antiJade.OnValueChanged -= OnAntiJadeChanged;
            _antiRuby.OnValueChanged -= OnAntiRubyChanged;
            _antiGold.OnValueChanged -= OnAntiGoldChanged;
        }

        void OnAntiJadeChanged(PartitionSolution _, PartitionSolution next) => _localAntiJade = next;
        void OnAntiRubyChanged(PartitionSolution _, PartitionSolution next) => _localAntiRuby = next;
        void OnAntiGoldChanged(PartitionSolution _, PartitionSolution next) => _localAntiGold = next;

        // ─────────────────────────────────────────────────────────────────────
        //  Read API — call from anywhere (server or client)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the densest centroid of prisms NOT in <paramref name="friendlyDomain"/>.
        /// I.e. for a Jade-team consumer, returns the densest cluster of
        /// Ruby+Gold prisms. <see cref="PartitionSolution.HasResult"/> is false
        /// when no enemy prisms are tracked anywhere.
        /// </summary>
        public PartitionSolution GetAntiDomainSolution(Domains friendlyDomain) =>
            friendlyDomain switch
            {
                Domains.Jade => _localAntiJade,
                Domains.Ruby => _localAntiRuby,
                Domains.Gold => _localAntiGold,
                _ => default,
            };

        public bool TryGetAntiDomainSolution(Domains friendlyDomain, out PartitionSolution solution)
        {
            solution = GetAntiDomainSolution(friendlyDomain);
            return solution.HasResult;
        }

        /// <summary>
        /// Convenience for AI / fauna / vessel abilities that just want the
        /// current anti-domain answer without null-checking <see cref="Active"/>.
        /// Returns a default <see cref="PartitionSolution"/> (HasResult == false)
        /// if no system is in the scene yet.
        /// </summary>
        public static PartitionSolution GetSolutionForDomain(Domains friendlyDomain)
        {
            var system = Active;
            return system == null ? default : system.GetAntiDomainSolution(friendlyDomain);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Server tick
        // ─────────────────────────────────────────────────────────────────────

        bool IsAuthoritative
        {
            // Treat "no NetworkManager at all" as local-only authoritative so
            // the system is still useful in edit-mode tests and any scene that
            // might run without Netcode. Otherwise: only the server computes.
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm == null) return true;
                return IsSpawned && IsServer;
            }
        }

        void Update()
        {
            if (!IsAuthoritative) return;
            if (Time.time < _nextRecomputeAt) return;

            _nextRecomputeAt = Time.time + recomputeIntervalSeconds;
            Recompute();
        }

        /// <summary>
        /// Cooldowned event entry — for callers that want fresher answers
        /// after a known mass change (e.g. a big explosion or a big spawn).
        /// Returns true if a recompute actually ran. The cooldown coalesces
        /// many simultaneous callers down to a single recompute.
        /// </summary>
        public bool RequestImmediateRecompute()
        {
            if (!IsAuthoritative) return false;
            if (Time.time < _earliestEventRecomputeAt) return false;

            _earliestEventRecomputeAt = Time.time + eventCooldownSeconds;
            // Slide the periodic deadline too so we don't double-recompute
            // milliseconds after an event.
            _nextRecomputeAt = Time.time + recomputeIntervalSeconds;
            Recompute();
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Compute
        // ─────────────────────────────────────────────────────────────────────

        void Recompute()
        {
            // Always measure so the toolbox shows live cost; verboseProfiling
            // only gates the log line.
            var sw = Stopwatch.StartNew();

            _version++;

            var bestJade = new PartitionSolution { AntiOfDomain = (int)Domains.Jade, Version = _version };
            var bestRuby = new PartitionSolution { AntiOfDomain = (int)Domains.Ruby, Version = _version };
            var bestGold = new PartitionSolution { AntiOfDomain = (int)Domains.Gold, Version = _version };

            int scanned = 0;
            var cells = Cell.ActiveCells;
            foreach (var cell in cells)
            {
                if (cell == null) continue;
                if (cell.countGrids == null || cell.countGrids.Count == 0) continue;
                scanned++;

                EvaluateCellAntiDomain(cell, Domains.Jade, ref bestJade);
                EvaluateCellAntiDomain(cell, Domains.Ruby, ref bestRuby);
                EvaluateCellAntiDomain(cell, Domains.Gold, ref bestGold);
            }

            // Local cache update — read API hits this path on both server and
            // (eventually) client. Server replicates to clients via the
            // NetworkVariable assignments below.
            _localAntiJade = bestJade;
            _localAntiRuby = bestRuby;
            _localAntiGold = bestGold;

            if (IsSpawned && IsServer)
            {
                // NetworkVariable does its own change detection via IEquatable,
                // but the equality check here saves a tiny amount of dirty-bit
                // bookkeeping when nothing's moved between recomputes.
                if (!_antiJade.Value.Equals(bestJade)) _antiJade.Value = bestJade;
                if (!_antiRuby.Value.Equals(bestRuby)) _antiRuby.Value = bestRuby;
                if (!_antiGold.Value.Equals(bestGold)) _antiGold.Value = bestGold;
            }

            sw.Stop();
            LastRecomputeMillis = (float)sw.Elapsed.TotalMilliseconds;
            LastRecomputeCellsScanned = scanned;

            if (verboseProfiling)
            {
                CSDebug.Log($"[DensityPartitionSystem] v{_version} scanned {scanned} cells in " +
                            $"{LastRecomputeMillis:F2}ms — antiJ={bestJade.Density:F0} " +
                            $"antiR={bestRuby.Density:F0} antiG={bestGold.Density:F0}");
            }
        }

        static void EvaluateCellAntiDomain(Cell cell, Domains friendlyDomain, ref PartitionSolution best)
        {
            // Cell.countGrids[friendly] holds every block NOT in `friendly`
            // (see Cell.AddBlock — friendly is the one team it skips). So the
            // densest region of that grid is the anti-friendly answer.
            if (!cell.countGrids.TryGetValue(friendlyDomain, out var grid) || grid == null)
                return;

            var pos = grid.FindDensestRegion();
            int density = grid.GetDensityAtPosition(pos);
            if (density <= 0) return;
            if (density <= best.Density) return;

            best.Position = pos;
            best.Density = density;
            best.CellId = cell.ID;
            best.Stride = grid.Stride;
        }
    }
}
