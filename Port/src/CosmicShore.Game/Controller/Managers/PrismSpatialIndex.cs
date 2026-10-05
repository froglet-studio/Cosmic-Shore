using System.Collections.Generic;
// PORT managed-array conversion: using Unity.Burst; using Unity.Collections;
// using Unity.Jobs; using Unity.Mathematics; using Unity.Profiling; — the
// Burst-compiled IJobParallelFor over NativeArray/NativeList becomes a plain struct
// run by a sequential loop over managed arrays; NativeParallelMultiHashMap<int3, int>
// becomes Dictionary<Vector3Int, List<int>>; ProfilerMarkers are dropped. Data
// layout, flag packing, bucket math, and damage logic are otherwise verbatim.
using CosmicShore.Engine;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.Gameplay;
using System;
using System.Linq;


namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Bit flags for prism status, packed into a single byte.
    /// The Burst job only checks bits 0-1 (IsActive + Destroyed).
    /// Bits 2-3 (shields) are only read on the main thread for the small hit set.
    /// </summary>
    public static class PrismFlags
    {
        public const byte IsActive       = 1 << 0; // bit 0
        public const byte Destroyed      = 1 << 1; // bit 1
        public const byte IsShielded     = 1 << 2; // bit 2
        public const byte IsSuperShielded = 1 << 3; // bit 3

        // Mask for the Burst job's early-exit check:
        // Active (bit 0 set) AND not destroyed (bit 1 clear) → value == 0x01
        public const byte JobSkipMask    = IsActive | Destroyed;
        public const byte JobPassValue   = IsActive; // exactly active, not destroyed
    }

    /// <summary>
    /// HOT data: read by every Execute() call in the Burst spatial query job.
    /// 16 bytes — exactly 4 prisms per 64-byte cache line, zero waste.
    ///
    /// Layout:
    ///   offset 0:  Position.x  (4B)
    ///   offset 4:  Position.y  (4B)
    ///   offset 8:  Position.z  (4B)
    ///   offset 12: Flags       (1B)  bit-packed status
    ///   offset 13: _pad        (3B)  alignment to 16B
    ///
    /// For 3000 prisms: 48 KB — fits comfortably in L2,
    /// and on devices with 64KB+ L1D (Snapdragon 8 Gen 2, Apple M-series), in L1.
    /// </summary>
    public struct PrismSpatialData
    {
        public Vector3 Position; // 12B  (PORT managed-array conversion: float3)
        public byte Flags;      // 1B (see PrismFlags)
        public byte _pad0;      // 1B
        public byte _pad1;      // 1B
        public byte _pad2;      // 1B
        // Total: 16B — exactly 4 per 64B cache line
    }

    /// <summary>
    /// COLD data: only read on the main thread for prisms that pass the spatial filter.
    /// Typically a few dozen per frame as the AOE sphere grows — not a cache concern.
    ///
    /// Layout:
    ///   offset 0: Volume  (4B)
    ///   offset 4: Domain  (4B)
    ///   Total: 8B
    /// </summary>
    public struct PrismDamageData
    {
        public float Volume; // 4B
        public int Domain;   // 4B
        // Total: 8B
    }

    /// <summary>
    /// Burst-compiled spatial query over cache-line-packed PrismSpatialData.
    /// Each Execute() reads exactly 16B (one PrismSpatialData entry).
    /// With 4 entries per cache line, a sequential scan of 3000 prisms
    /// touches only 750 cache lines (48KB).
    /// </summary>
    // PORT managed-array conversion: was [BurstCompile] struct … : IJobParallelFor with
    // [ReadOnly] NativeArray fields + NativeList<int>.ParallelWriter output; the same
    // Execute body now runs as a sequential loop in ProcessExplosionFrame.
    public struct AOESpatialQueryJob
    {
        public PrismSpatialData[] Prisms; // PORT managed-array conversion: [ReadOnly] NativeArray<PrismSpatialData>
        public Vector3 Center;            // PORT managed-array conversion: [ReadOnly] float3
        public float RadiusSq;            // PORT managed-array conversion: [ReadOnly]

        public List<int> HitIndices;      // PORT managed-array conversion: NativeList<int>.ParallelWriter

        public void Execute(int index)
        {
            var p = Prisms[index];

            // Single byte check: must be active (bit 0) and not destroyed (bit 1)
            if ((p.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) return;

            float distSq = (p.Position - Center).sqrMagnitude; // PORT managed-array conversion: math.lengthsq
            if (distSq > RadiusSq) return;

            HitIndices.Add(index); // PORT managed-array conversion: AddNoResize
        }
    }

    /// <summary>
    /// THE canonical spatial index of all live prism mass. One registration
    /// lifecycle, multiple query views — see Docs/SPATIAL_INDEX.md before adding
    /// any new spatial query against prisms (Physics.OverlapSphere / CheckBox
    /// against prisms is an anti-pattern; query this index instead).
    ///
    /// Views served:
    ///   1. AOE damage    — Burst brute-force sphere scan over the hot array
    ///                      (ExplosionImpactor.ProcessBatchFrame).
    ///   2. Occupancy     — bucket hash grid + reservation set. Growth systems
    ///                      (GyroidAssembler / WallAssembler / SchwarzPAssembler)
    ///                      call TryReserve at the grow DECISION, before
    ///                      Instantiate — this closes the race that
    ///                      Physics.CheckBox could never close (prism colliders
    ///                      are disabled for the first Prism.waitTime seconds
    ///                      after spawn).
    ///   3. Neighborhood  — QuerySphere (gather live prisms in range) and
    ///                      IsAnyPrismWithin (boolean probe) serve fauna senses
    ///                      (LightFauna / Boid), assembler mate-finding
    ///                      (GyroidAssembler / WallAssembler) and trail passives
    ///                      (ScoutTrailPrismScaler) — no physics broadphase, no
    ///                      per-collider GetComponent, no scratch-array
    ///                      truncation. See Docs/SPATIAL_INDEX.md.
    ///   4. Cell density  — Register/MarkRestored file each prism into its
    ///                      containing cell's per-domain density grids
    ///                      (Cell.AddBlock); MarkDestroyed/Unregister remove it.
    ///                      The coarse view rides the same lifecycle stream as
    ///                      the fine views, so they cannot diverge (Phase 3).
    ///
    /// Data layout (hot/cold split):
    ///   _spatial[i] — PrismSpatialData (16B) — read by Burst job for ALL prisms
    ///   _damage[i]  — PrismDamageData  (8B)  — read on main thread for HIT prisms only
    ///   _prisms[i]  — Prism reference         — managed array for applying damage
    ///   _buckets    — int3 bucket key → index — incremental, prisms are mostly static
    ///
    /// The Burst job scans only _spatial, keeping the working set tight.
    /// Domain/shield/volume data in _damage is never loaded into cache during the scan —
    /// it's only touched for the small set of prisms that actually got hit.
    ///
    /// Registration lifecycle (all main-thread):
    ///   Assembler.GetGrowthInfo()  → TryReserve(pos) BEFORE Instantiate (growth only)
    ///   Prism.CreateBlockCoroutine → Register(prism) → stores index on Prism,
    ///                                consumes the matching reservation, binds the
    ///                                containing cell's density grids
    ///   Prism.SetupDestruction     → MarkDestroyed(index) → AOE skips, bucket
    ///                                freed, cell grids release the prism
    ///   Prism.Restore              → MarkRestored(index) → re-enters AOE +
    ///                                bucket + cell grids
    ///   Prism.OnDisable/OnDestroy  → Unregister(index) → frees slot, releases
    ///                                the cell binding
    ///   PrismTeamManager (steal)   → ForwardDomainChangeToCell(index) re-files
    ///                                the prism in its cell's per-domain grids
    ///   PrismStateManager          → UpdateShieldState(index, ...) on state change
    ///   Assembler movers / fauna   → UpdatePosition(index, pos) — anything that
    ///                                moves a registered prism (gyroid/wall bond
    ///                                steering, fauna body prisms swimming) must
    ///                                keep the stored position honest
    /// </summary>
    public class PrismSpatialIndex : Singleton<PrismSpatialIndex>
    {
        private const int INITIAL_CAPACITY = 4096;
        private const int JOB_BATCH_SIZE = 256;

        /// <summary>
        /// Edge length of the occupancy hash-grid buckets. Sized to the gyroid
        /// bond spacing (~8m: |DeltaPosition| ≈ 2.7 × separationDistance 3) so an
        /// occupancy probe of radius ≤ half-spacing touches at most 8 buckets.
        /// </summary>
        public const float BucketSizeMeters = 8f;

        /// <summary>Quantization step for reservation keys (half bond spacing).</summary>
        private const float ReservationQuantum = 4f;

        /// <summary>
        /// Safety net for reservations that are claimed but never confirmed by a
        /// Register (spawn skipped, prism AOE-killed inside Prism.waitTime, caller
        /// abandoned the GrowthInfo). Spawn-to-register is waitTime (0.6s) plus one
        /// flora grow cadence, so 5s is comfortably past any legitimate confirm.
        /// </summary>
        public const float ReservationTtlSeconds = 5f;

        /// <summary>
        /// Maximum NEW prism hits to process per frame per explosion.
        /// Spreading damage across frames prevents catastrophic frame spikes
        /// (e.g. 2000+ prisms destroyed in one frame → 426ms).
        /// Unprocessed hits are NOT added to alreadyHit and will be
        /// re-found by the Burst spatial query on subsequent frames.
        /// </summary>
        private const int MAX_NEW_HITS_PER_FRAME = 48;

        // Hot: scanned by Burst job every frame during AOE
        private PrismSpatialData[] _spatial; // PORT managed-array conversion: NativeArray<PrismSpatialData>

        // Cold: read only for hit prisms on main thread
        private PrismDamageData[] _damage; // PORT managed-array conversion: NativeArray<PrismDamageData>

        // Managed: Prism references for applying damage callbacks
        private Prism[] _prisms;

        // Managed: the cell whose per-domain density grids each prism is filed in
        // (the coarse view of this same lifecycle), or null — open space, fauna
        // bodies, slot free. Bound on Register/MarkRestored, released on
        // MarkDestroyed/Unregister.
        private Cell[] _cells;

        private int _highWaterMark;
        private readonly Stack<int> _freeList = new(256);
        private List<int> _hitIndices; // PORT managed-array conversion: NativeList<int>

        // Occupancy view: bucket key → registry index, one entry per LIVE
        // (active, not destroyed) prism. Maintained incrementally by
        // Register / MarkDestroyed / MarkRestored / Unregister / UpdatePosition.
        // PORT managed-array conversion: NativeParallelMultiHashMap<int3, int> →
        // Dictionary<Vector3Int, List<int>> (bucket lists are tiny — bond spacing
        // bounds occupancy per 8m cell).
        private Dictionary<Vector3Int, List<int>> _buckets;
        private int _bucketEntryCount;

        // Reservation view: quantized position → claim. Managed dictionary is fine
        // here — reservations are few (bounded by spawn rate × TTL) and main-thread.
        private struct Reservation
        {
            public Vector3 Position;
            public float Expires;
        }
        private readonly Dictionary<Vector3Int, Reservation> _reservations = new();
        private readonly List<Vector3Int> _scratchKeys = new(16);
        private float _nextReservationPrune;

        // PORT managed-array conversion: the source file's ProfilerMarkers
        // (AOE.ProcessExplosion / AOE.BurstJob.Schedule / AOE.ResolveDamage) are
        // dropped — no Unity.Profiling in the port engine.

        public bool IsAvailable => _spatial != null; // PORT managed-array conversion: _spatial.IsCreated
        public int HighWaterMark => _highWaterMark;

        public static PrismSpatialIndex EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[PrismSpatialIndex]");
            go.AddComponent<PrismSpatialIndex>();
            return Instance;
        }

        public override void Awake()
        {
            base.Awake();
            _spatial = new PrismSpatialData[INITIAL_CAPACITY]; // PORT managed-array conversion: new NativeArray<…>(INITIAL_CAPACITY, Allocator.Persistent)
            _damage = new PrismDamageData[INITIAL_CAPACITY];   // PORT managed-array conversion: new NativeArray<…>(INITIAL_CAPACITY, Allocator.Persistent)
            _prisms = new Prism[INITIAL_CAPACITY];
            _cells = new Cell[INITIAL_CAPACITY];
            _hitIndices = new List<int>(512); // PORT managed-array conversion: new NativeList<int>(512, Allocator.Persistent)
            _buckets = new Dictionary<Vector3Int, List<int>>(INITIAL_CAPACITY); // PORT managed-array conversion: NativeParallelMultiHashMap<int3, int>
        }

        #region Bucket grid

        private static Vector3Int BucketKey(Vector3 position) => new( // PORT managed-array conversion: (int3)math.floor(position / BucketSizeMeters)
            Mathf.FloorToInt(position.x / BucketSizeMeters),
            Mathf.FloorToInt(position.y / BucketSizeMeters),
            Mathf.FloorToInt(position.z / BucketSizeMeters));

        private void AddToBucket(int index, Vector3 position)
        {
            // PORT managed-array conversion: capacity doubling handled by Dictionary/List growth.
            var key = BucketKey(position);
            if (!_buckets.TryGetValue(key, out var list))
            {
                list = new List<int>(4);
                _buckets[key] = list;
            }
            list.Add(index);
            _bucketEntryCount++;
        }

        private void RemoveFromBucket(int index, Vector3 position)
        {
            var key = BucketKey(position);
            if (!_buckets.TryGetValue(key, out var list)) return;
            if (list.Remove(index))
                _bucketEntryCount--;
        }

        /// <summary>
        /// True when probing every bucket in the AABB would touch more entries than a
        /// straight scan of the slot array — wide queries (Scout open-space probes reach
        /// 100m → 26³ ≈ 17k bucket lookups) are cheaper as one pass over the hot array.
        /// </summary>
        private bool BucketWalkCostsMoreThanLinearScan(Vector3Int min, Vector3Int max)
        {
            long bucketVolume = (long)(max.x - min.x + 1) * (max.y - min.y + 1) * (max.z - min.z + 1);
            return bucketVolume > _highWaterMark;
        }

        // PORT managed-array conversion: shared helper for the (int3)math.floor((c ± r) / size) key corners.
        private static Vector3Int BucketKeyCorner(Vector3 center, float offset) => new(
            Mathf.FloorToInt((center.x + offset) / BucketSizeMeters),
            Mathf.FloorToInt((center.y + offset) / BucketSizeMeters),
            Mathf.FloorToInt((center.z + offset) / BucketSizeMeters));

        /// <summary>
        /// True if any LIVE prism (active, not destroyed — reservations excluded)
        /// sits within <paramref name="radius"/> of <paramref name="position"/>.
        /// Bucket-accelerated for tight radii, linear hot-array scan for wide ones.
        /// No physics, no allocation.
        /// </summary>
        public bool IsAnyPrismWithin(Vector3 position, float radius)
        {
            if (_buckets == null) return false; // PORT managed-array conversion: !_buckets.IsCreated
            Vector3 center = position;
            float radiusSq = radius * radius;
            Vector3Int min = BucketKeyCorner(center, -radius);
            Vector3Int max = BucketKeyCorner(center, radius);

            if (BucketWalkCostsMoreThanLinearScan(min, max))
            {
                for (int i = 0; i < _highWaterMark; i++)
                {
                    var s = _spatial[i];
                    if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue &&
                        (s.Position - center).sqrMagnitude <= radiusSq)
                        return true;
                }
                return false;
            }

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_buckets.TryGetValue(new Vector3Int(x, y, z), out var bucket))
                    continue;
                for (int i = 0; i < bucket.Count; i++)
                {
                    var s = _spatial[bucket[i]];
                    if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue &&
                        (s.Position - center).sqrMagnitude <= radiusSq)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gathers every LIVE prism (active, not destroyed) within
        /// <paramref name="radius"/> of <paramref name="center"/> into
        /// <paramref name="results"/> (cleared first) and returns the count.
        /// The replacement for Physics.OverlapSphere against prisms: same
        /// population as the AOE view, returns Prism references directly (no
        /// per-collider GetComponent), unbounded (no NonAlloc truncation), and
        /// allocation-free given a reused caller list.
        ///
        /// Results are an unordered snapshot — entries can be destroyed by the
        /// caller's own side effects mid-iteration (consume, steal, convert), so
        /// iterate with a null/destroyed guard, exactly as collider snapshots
        /// required. Main-thread only.
        /// </summary>
        public int QuerySphere(Vector3 center, float radius, List<Prism> results)
        {
            results.Clear();
            if (_buckets == null || _highWaterMark == 0) return 0; // PORT managed-array conversion: !_buckets.IsCreated
            Vector3 c = center;
            float radiusSq = radius * radius;
            Vector3Int min = BucketKeyCorner(c, -radius);
            Vector3Int max = BucketKeyCorner(c, radius);

            if (BucketWalkCostsMoreThanLinearScan(min, max))
            {
                for (int i = 0; i < _highWaterMark; i++)
                {
                    var s = _spatial[i];
                    if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                    if ((s.Position - c).sqrMagnitude > radiusSq) continue;
                    var prism = _prisms[i];
                    if (prism) results.Add(prism);
                }
                return results.Count;
            }

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_buckets.TryGetValue(new Vector3Int(x, y, z), out var bucket))
                    continue;
                for (int i = 0; i < bucket.Count; i++)
                {
                    int idx = bucket[i];
                    var s = _spatial[idx];
                    if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                    if ((s.Position - c).sqrMagnitude > radiusSq) continue;
                    var prism = _prisms[idx];
                    if (prism) results.Add(prism);
                }
            }
            return results.Count;
        }

        #endregion

        #region Reservations

        private static Vector3Int ReservationKey(Vector3 position) => new(
            Mathf.FloorToInt(position.x / ReservationQuantum),
            Mathf.FloorToInt(position.y / ReservationQuantum),
            Mathf.FloorToInt(position.z / ReservationQuantum));

        /// <summary>
        /// Atomically checks that nothing occupies <paramref name="position"/>
        /// (no live prism, no unexpired reservation within
        /// <paramref name="clearRadius"/>) and claims it. Call at the grow
        /// DECISION, before Instantiate — the claim is what closes the
        /// spawn-vs-spawn race that collider-based checks can't see. The claim is
        /// consumed when the spawned prism registers at (or near) the reserved
        /// position, or lapses after <see cref="ReservationTtlSeconds"/>.
        /// </summary>
        public bool TryReserve(Vector3 position, float clearRadius)
        {
            PruneExpiredReservations();
            if (IsAnyPrismWithin(position, clearRadius)) return false;
            if (HasActiveReservationWithin(position, clearRadius)) return false;
            _reservations[ReservationKey(position)] = new Reservation
            {
                Position = position,
                Expires = Time.time + ReservationTtlSeconds
            };
            return true;
        }

        /// <summary>Read-only occupancy probe: live prism or active reservation in range.</summary>
        public bool IsPositionOccupied(Vector3 position, float clearRadius) =>
            IsAnyPrismWithin(position, clearRadius) ||
            HasActiveReservationWithin(position, clearRadius);

        /// <summary>Explicitly cancels a claim made by <see cref="TryReserve"/>.</summary>
        public void ReleaseReservation(Vector3 position) =>
            _reservations.Remove(ReservationKey(position));

        private bool HasActiveReservationWithin(Vector3 position, float clearRadius)
        {
            if (_reservations.Count == 0) return false;
            float radiusSq = clearRadius * clearRadius;
            float now = Time.time;
            Vector3Int min = ReservationKey(position - Vector3.one * clearRadius);
            Vector3Int max = ReservationKey(position + Vector3.one * clearRadius);
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_reservations.TryGetValue(new Vector3Int(x, y, z), out var r)) continue;
                if (r.Expires <= now) continue; // lapsed — prune pass will collect it
                if ((r.Position - position).sqrMagnitude <= radiusSq) return true;
            }
            return false;
        }

        /// <summary>
        /// Called by <see cref="Register"/>: the spawned prism has materialized, so
        /// the claim that protected its site is fulfilled. Matched by proximity, not
        /// exact key — re-parenting under a spindle round-trips the position through
        /// parent matrices, so the registered position can drift a few millimetres
        /// (and across a quantization boundary) from the reserved one.
        /// </summary>
        private void ConsumeReservationNear(Vector3 position)
        {
            if (_reservations.Count == 0) return;
            const float confirmRadiusSq = 2f * 2f;
            _scratchKeys.Clear();
            foreach (var kvp in _reservations)
            {
                if ((kvp.Value.Position - position).sqrMagnitude <= confirmRadiusSq)
                    _scratchKeys.Add(kvp.Key);
            }
            foreach (var key in _scratchKeys)
                _reservations.Remove(key);
        }

        /// <summary>Lazy TTL sweep, amortized to at most one pass per second.</summary>
        private void PruneExpiredReservations()
        {
            if (_reservations.Count == 0 || Time.time < _nextReservationPrune) return;
            _nextReservationPrune = Time.time + 1f;
            float now = Time.time;
            _scratchKeys.Clear();
            foreach (var kvp in _reservations)
            {
                if (kvp.Value.Expires <= now)
                    _scratchKeys.Add(kvp.Key);
            }
            foreach (var key in _scratchKeys)
                _reservations.Remove(key);
        }

        #endregion

        #region Cell density view

        /// <summary>
        /// Files the prism into the per-domain density grids of the cell that
        /// spatially contains it — the COARSE view of this same registration
        /// lifecycle (fauna anti-domain targeting reads the grids; the cell phase
        /// system reads LiveBlockCount). Before Phase 3 these call sites lived in
        /// Prism beside every index call; folding them in here means the fine
        /// (occupancy/AOE) and coarse (density) views are fed by one stream and
        /// cannot diverge.
        ///
        /// Fauna bodies (LightFauna / Boid HealthPrisms) bind as VOLUME-ONLY mass:
        /// "volume is the spine" says ALL prisms feed the cell's volume accounting
        /// (Cell.LiveVolume — phase, dominant domain, HUD), so they enter the
        /// volume membership — but they must NOT enter the targeting grids or
        /// prism counts, otherwise a forager swarm reads as its own "mass
        /// concentration" and seeks itself instead of the trail/flora buildup
        /// (and herbivores would be seeded against inedible "prey"). Only
        /// HealthPrisms can be fauna bodies, so the GetComponentInParent walk is
        /// gated to that subtype to keep ordinary trail-prism registrations cheap.
        /// (They stay in the AOE and occupancy views — they are damageable,
        /// space-occupying mass.)
        ///
        /// Coexists with the flora ownership stream: HealthBlockTracker also
        /// AddBlocks flora health prisms into the LifeForm's host cell.
        /// Cell.AddBlock no-ops on already-tracked prisms and RemoveBlock
        /// tolerates double removal, so the two contributors stay consistent.
        /// </summary>
        private void BindCell(int index, Prism prism, Vector3 position)
        {
            // Fauna bodies are VOLUME, not environment: they feed the cell's
            // per-domain volume sums ("volume is the spine" — all prisms count,
            // whatever their source) but stay out of the targeting grids and
            // prism counts (see the remarks above).
            bool environmentMass = !(prism is HealthPrism && prism.GetComponentInParent<Fauna>() != null);
            var cell = Cell.FindCellContaining(position);
            _cells[index] = cell;
            if (cell) cell.AddBlock(prism, environmentMass);
        }

        /// <summary>
        /// Removes the prism from its bound cell's density grids. Idempotent —
        /// the destroyed→unregistered path calls this twice. The prism ref is
        /// passed (not read from _prisms) so Unregister can unbind before it
        /// frees the slot; Cell.RemoveBlock handles destroyed-but-non-null refs
        /// by design, so no Unity-aliveness gate here.
        /// </summary>
        private void UnbindCell(int index, Prism prism)
        {
            var cell = _cells[index];
            _cells[index] = null;
            if (cell) cell.RemoveBlock(prism);
        }

        /// <summary>
        /// Copies every LIVE prism (active, not destroyed) into
        /// <paramref name="results"/> (cleared first); returns the count. One linear
        /// pass over the managed refs — the iteration view for whole-population
        /// passes (proximity collider-LOD, telemetry). Allocation-free with a
        /// pre-sized caller list; main-thread only.
        /// </summary>
        public int CopyLivePrisms(List<Prism> results)
        {
            results.Clear();
            if (_spatial == null) return 0; // PORT managed-array conversion: !_spatial.IsCreated
            for (int i = 0; i < _highWaterMark; i++)
            {
                if ((_spatial[i].Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                var prism = _prisms[i];
                if (prism) results.Add(prism);
            }
            return results.Count;
        }

        /// <summary>
        /// Re-files a tracked prism whose domain changed (steal / ChangeTeam) in
        /// its bound cell's per-domain grids. Caller:
        /// Prism.HandleTeamChangedForCell only. Deliberately does NOT touch the
        /// AOE cold data — wiring UpdateDomain into the damage view changes AOE
        /// friend/foe results and must be its own tested change (see
        /// Docs/SPATIAL_INDEX.md "Known gaps").
        /// </summary>
        public void ForwardDomainChangeToCell(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var cell = _cells[index];
            var prism = _prisms[index];
            if (cell && prism) cell.NotifyBlockDomainChanged(prism);
        }

        #endregion

        #region Registration

        /// <summary>
        /// Registers a prism for batch AOE processing, growth occupancy, and the
        /// containing cell's density grids. Returns the registry index which
        /// should be stored on the Prism for O(1) updates and unregistration.
        /// </summary>
        public int Register(Prism prism)
        {
            if (_spatial == null) return -1; // PORT managed-array conversion: !_spatial.IsCreated
            int index;
            if (_freeList.Count > 0)
            {
                index = _freeList.Pop();
            }
            else
            {
                index = _highWaterMark++;
                EnsureCapacity(index);
            }

            _prisms[index] = prism;

            // Build flags byte
            byte flags = PrismFlags.IsActive;
            if (prism.prismProperties is { IsShielded: true }) flags |= PrismFlags.IsShielded;
            if (prism.prismProperties is { IsSuperShielded: true }) flags |= PrismFlags.IsSuperShielded;

            Vector3 position = prism.transform.position; // PORT managed-array conversion: (float3)(Vector3)
            _spatial[index] = new PrismSpatialData
            {
                Position = position,
                Flags = flags
            };

            _damage[index] = new PrismDamageData
            {
                Volume = Mathf.Max(prism.prismProperties?.volume ?? 1f, 1f),
                Domain = (int)prism.Domain
            };

            AddToBucket(index, position);
            // The prism this reservation protected has materialized — fulfil it.
            ConsumeReservationNear(prism.transform.position);
            // Coarse view: file into the containing cell's density grids.
            BindCell(index, prism, position);

            return index;
        }

        public void Unregister(int index)
        {
            if (_spatial == null) return; // PORT managed-array conversion: !_spatial.IsCreated
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            // Already freed (e.g. OnDisable then ResetState both fire) — don't
            // double-push the slot onto the free list.
            if (s.Flags == 0 && _prisms[index] == null) return;
            // Live entries hold a bucket slot; destroyed ones were already removed.
            if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
                RemoveFromBucket(index, s.Position);
            // Coarse view: leave the cell grids (no-op if MarkDestroyed already did).
            UnbindCell(index, _prisms[index]);
            s.Flags = 0; // clear all flags including IsActive
            _spatial[index] = s;
            _prisms[index] = null;
            _freeList.Push(index);
        }

        public void MarkDestroyed(int index)
        {
            if (_spatial == null) return; // PORT managed-array conversion: !_spatial.IsCreated
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            if ((s.Flags & PrismFlags.Destroyed) != 0) return; // already destroyed
            // Destroyed mass no longer occupies space — growth may fill the site.
            if ((s.Flags & PrismFlags.IsActive) != 0)
                RemoveFromBucket(index, s.Position);
            s.Flags |= PrismFlags.Destroyed;
            _spatial[index] = s;
            // Coarse view: destroyed mass must stop attracting fauna, and the
            // cell's LiveBlockCount must fall so the phase system can descend
            // (the consumption half of the oscillation).
            UnbindCell(index, _prisms[index]);
        }

        /// <summary>
        /// Re-activates a destroyed entry (trail restore mechanics). Refreshes the
        /// stored position and re-enters the occupancy bucket — restored mass
        /// blocks growth and takes AOE damage again. (Before the spatial-index
        /// unification, Restore never told the registry anything, so restored
        /// prisms stayed permanently invisible to batch AOE.)
        /// </summary>
        public void MarkRestored(int index)
        {
            if (_spatial == null) return; // PORT managed-array conversion: !_spatial.IsCreated
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            if ((s.Flags & PrismFlags.Destroyed) == 0) return; // not destroyed
            var prism = _prisms[index];
            if (prism != null)
                s.Position = prism.transform.position; // PORT managed-array conversion: (float3)(Vector3)
            s.Flags &= unchecked((byte)~PrismFlags.Destroyed);
            _spatial[index] = s;
            if ((s.Flags & PrismFlags.IsActive) != 0)
                AddToBucket(index, s.Position);
            // Coarse view: restored mass re-enters the cell's density grids
            // (re-resolved at the restored position, like the old
            // Prism.RegisterWithCell call this replaces).
            if (prism) BindCell(index, prism, s.Position);
        }

        /// <summary>
        /// Keeps the index honest for the few prisms that move after registration
        /// (gyroid bonding steers existing blocks into bond sites). Cheap when the
        /// bucket key is unchanged; rebuckets otherwise.
        /// </summary>
        public void UpdatePosition(int index, Vector3 position)
        {
            if (_spatial == null) return; // PORT managed-array conversion: !_spatial.IsCreated
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            Vector3 newPosition = position;
            if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
            {
                Vector3Int oldKey = BucketKey(s.Position);
                Vector3Int newKey = BucketKey(newPosition);
                if (!oldKey.Equals(newKey))
                {
                    RemoveFromBucket(index, s.Position);
                    AddToBucket(index, newPosition);
                }
            }
            s.Position = newPosition;
            _spatial[index] = s;
        }

        public void UpdateShieldState(int index, bool shielded, bool superShielded)
        {
            if (_spatial == null) return; // PORT managed-array conversion: !_spatial.IsCreated
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            // Clear shield bits, then set
            s.Flags = (byte)(s.Flags & ~(PrismFlags.IsShielded | PrismFlags.IsSuperShielded));
            if (shielded) s.Flags |= PrismFlags.IsShielded;
            if (superShielded) s.Flags |= PrismFlags.IsSuperShielded;
            _spatial[index] = s;
        }

        public void UpdateDomain(int index, int domain)
        {
            if (_damage == null) return; // PORT managed-array conversion: !_damage.IsCreated
            if (index < 0 || index >= _highWaterMark) return;
            var d = _damage[index];
            d.Domain = domain;
            _damage[index] = d;
        }

        /// <summary>
        /// Updates the cached volume after a prism finishes growing.
        /// </summary>
        public void UpdateVolume(int index, float volume)
        {
            if (_damage == null) return; // PORT managed-array conversion: !_damage.IsCreated
            if (index < 0 || index >= _highWaterMark) return;
            var d = _damage[index];
            d.Volume = volume;
            _damage[index] = d;
        }

        #endregion

        #region Benchmark Support

        /// <summary>
        /// Registers synthetic prism data for benchmarking without requiring a Prism
        /// MonoBehaviour. The managed _prisms[index] slot is null — ProcessExplosionFrame
        /// skips it after the spatial query, so this isolates Burst job cost from damage
        /// application cost. Maintains the live-entry-implies-bucket invariant so the
        /// occupancy view stays consistent with Unregister/MarkDestroyed. Deliberately
        /// NOT filed into the cell density view — synthetic mass must not perturb
        /// Cell.LiveVolume / phase / fauna-targeting accounting.
        /// </summary>
        internal int RegisterSynthetic(Vector3 position, byte flags, float volume, int domain) // PORT managed-array conversion: float3 position
        {
            if (_spatial == null) return -1; // PORT managed-array conversion: !_spatial.IsCreated
            int index;
            if (_freeList.Count > 0)
                index = _freeList.Pop();
            else
            {
                index = _highWaterMark++;
                EnsureCapacity(index);
            }

            _prisms[index] = null;
            _cells[index] = null;
            _spatial[index] = new PrismSpatialData { Position = position, Flags = flags };
            _damage[index] = new PrismDamageData { Volume = volume, Domain = domain };
            if ((flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
                AddToBucket(index, position);
            return index;
        }

        /// <summary>
        /// Clears all registered prisms, buckets, reservations, and cell bindings.
        /// Used by the AOE benchmark to reset between runs — never call during
        /// gameplay.
        /// </summary>
        internal void ClearAll()
        {
            if (_spatial == null) return; // PORT managed-array conversion: !_spatial.IsCreated
            for (int i = 0; i < _highWaterMark; i++)
            {
                // Real prisms registered before the benchmark ran are filed in
                // their cell's density grids — release them so the coarse view
                // doesn't keep counting mass the index dropped.
                UnbindCell(i, _prisms[i]);
                _prisms[i] = null;
                var s = _spatial[i];
                s.Flags = 0;
                _spatial[i] = s;
            }
            _freeList.Clear();
            _highWaterMark = 0;
            _buckets?.Clear(); // PORT managed-array conversion: if (_buckets.IsCreated) _buckets.Clear()
            _bucketEntryCount = 0;
            _reservations.Clear();
        }

        #endregion

        #region AOE Processing

        /// <summary>
        /// Processes one frame of AOE explosion damage.
        ///
        /// Phase 1 (Burst job): Scans _spatial array (16B/prism, 4 per cache line).
        ///   - Checks Flags byte + distance² against all registered prisms.
        ///   - Outputs indices of prisms within radius to _hitIndices.
        ///
        /// Phase 2 (main thread): For each hit index (typically dozens, not thousands):
        ///   - Reads _damage[idx] for domain/shield info (cold data, not in Burst working set).
        ///   - Applies domain logic, shield activation/deactivation, or damage.
        ///   - Syncs results back to registry.
        ///
        /// Returns true if the explosion should continue, false if it should be destroyed
        /// (e.g. hit a super-shielded enemy prism — mirrors original Destroy(gameObject) behavior).
        /// </summary>
        public bool ProcessExplosionFrame(
            Vector3 center,
            float radius,
            float speed,
            float inertia,
            Domains explosionDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            IVessel vessel,
            HashSet<int> alreadyHit)
        {
            if (_highWaterMark == 0 || _spatial == null) return true; // PORT managed-array conversion: !_spatial.IsCreated

            // --- Phase 1: Burst job over hot spatial data ---
            _hitIndices.Clear();

            // Ensure NativeList capacity can hold all prisms — AddNoResize in
            // ParallelWriter will throw if capacity < count, killing the async loop
            // and leaving the explosion stuck at max scale.
            if (_hitIndices.Capacity < _highWaterMark)
                _hitIndices.Capacity = _highWaterMark;

            var job = new AOESpatialQueryJob
            {
                Prisms = _spatial,
                Center = center, // PORT managed-array conversion: (float3)center
                RadiusSq = radius * radius,
                HitIndices = _hitIndices // PORT managed-array conversion: _hitIndices.AsParallelWriter()
            };

            // PORT managed-array conversion: job.Schedule(_highWaterMark, JOB_BATCH_SIZE).Complete();
            for (int index = 0; index < _highWaterMark; index++)
                job.Execute(index);

            // --- Phase 2: Main thread damage logic over cold data + managed refs ---
            bool shouldContinue = true;
            int expDomain = (int)explosionDomain;

            // Cache vessel info to avoid repeated interface property access
            Domains vesselDomain = Domains.Blue;
            string vesselPlayerName = null;
            if (!anonymous && vessel != null)
            {
                var status = vessel.VesselStatus;
                vesselDomain = status.Domain;
                vesselPlayerName = status.Player.Name;
            }

            int newHitCount = 0;
            for (int i = 0; i < _hitIndices.Count; i++) // PORT managed-array conversion: _hitIndices.Length
            {
                int idx = _hitIndices[i];

                // Skip if already hit by this explosion (mirrors OnTriggerEnter once-per-pair behavior)
                if (alreadyHit.Contains(idx)) continue;

                // Cap new damage per frame to spread load across frames.
                // Don't add to alreadyHit — the Burst job will re-find these
                // prisms next frame and we'll process them then.
                if (newHitCount >= MAX_NEW_HITS_PER_FRAME)
                    continue;

                alreadyHit.Add(idx);
                newHitCount++;

                var prism = _prisms[idx];
                if (prism == null || prism.destroyed) continue;

                // Read cold data — only for hit prisms, never pollutes the Burst job's cache
                var flags = _spatial[idx].Flags;
                var dmg = _damage[idx];
                int prismDomain = dmg.Domain;

                // Super-shielded prisms are fully invulnerable. AOE explosions
                // are physically blocked by the shield (shouldContinue = false
                // stops the explosion expanding past this layer) but cause no
                // damage and no state change. Ways to break super-shields will
                // be added later as targeted opt-in mechanics.
                if ((flags & PrismFlags.IsSuperShielded) != 0)
                {
                    shouldContinue = false;
                    continue;
                }

                // Same team (and not affectSelf) or non-destructive: shield the prism
                if ((prismDomain == expDomain && !affectSelf) || !destructive)
                {
                    if (shielding && prismDomain == expDomain)
                        prism.ActivateShield();
                    else
                        prism.ActivateShield(2f);
                    UpdateShieldState(idx, true, false);
                    continue;
                }

                // Compute impact vector (same formula as AOEExplosion.CalculateImpactVector)
                Vector3 prismPos = _spatial[idx].Position; // PORT managed-array conversion: (Vector3)_spatial[idx].Position
                Vector3 direction = (prismPos - center).normalized;
                Vector3 impactVector = direction * speed * inertia;

                // Deal damage
                if (anonymous)
                    prism.Damage(impactVector, Domains.Blue, "🔥GuyFawkes🔥", devastating);
                else
                    prism.Damage(impactVector, vesselDomain, vesselPlayerName, devastating);

                // Sync registry with the result of Damage()
                if (prism.destroyed)
                    MarkDestroyed(idx);
                else
                    UpdateShieldState(idx,
                        prism.prismProperties.IsShielded,
                        prism.prismProperties.IsSuperShielded);
            }

            return shouldContinue;
        }

        #endregion

        #region Capacity

        private void EnsureCapacity(int requiredIndex)
        {
            if (requiredIndex < _spatial.Length) return;

            int newSize = Mathf.NextPowerOfTwo(requiredIndex + 1);

            // Grow hot array
            var newSpatial = new PrismSpatialData[newSize]; // PORT managed-array conversion: NativeArray alloc + NativeArray<…>.Copy + Dispose
            System.Array.Copy(_spatial, newSpatial, _spatial.Length);
            _spatial = newSpatial;

            // Grow cold array
            var newDamage = new PrismDamageData[newSize]; // PORT managed-array conversion: NativeArray alloc + NativeArray<…>.Copy + Dispose
            System.Array.Copy(_damage, newDamage, _damage.Length);
            _damage = newDamage;

            // Grow managed arrays
            var newPrisms = new Prism[newSize];
            System.Array.Copy(_prisms, newPrisms, _prisms.Length);
            _prisms = newPrisms;

            var newCells = new Cell[newSize];
            System.Array.Copy(_cells, newCells, _cells.Length);
            _cells = newCells;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            // PORT managed-array conversion: NativeArray/NativeList/MultiHashMap Dispose() —
            // managed collections are GC-reclaimed; nulling preserves the IsCreated→false contract.
            _spatial = null;
            _damage = null;
            _hitIndices = null;
            _buckets = null;
        }

        #endregion
    }
}
