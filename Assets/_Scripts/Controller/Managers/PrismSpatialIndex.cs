using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
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

        // Collider-LOD state: this slot was within the LOD radius of a focus at the
        // last classification pass (LodClassifyJob). Owned exclusively by
        // RunLodClassification - not part of JobSkipMask semantics, and reset for
        // free when Register writes fresh flags into a reused slot.
        public const byte LodNear        = 1 << 4; // bit 4

        // Mask for the Burst job's early-exit check:
        // Active (bit 0 set) AND not destroyed (bit 1 clear) → value == 0x01
        public const byte JobSkipMask    = IsActive | Destroyed;
        public const byte JobPassValue   = IsActive; // exactly active, not destroyed
    }

    /// <summary>
    /// HOT data: read by every Execute() call in the Burst spatial query job.
    /// 16 bytes - exactly 4 prisms per 64-byte cache line, zero waste.
    ///
    /// Layout:
    ///   offset 0:  Position.x  (4B)
    ///   offset 4:  Position.y  (4B)
    ///   offset 8:  Position.z  (4B)
    ///   offset 12: Flags       (1B)  bit-packed status
    ///   offset 13: _pad        (3B)  alignment to 16B
    ///
    /// For 3000 prisms: 48 KB - fits comfortably in L2,
    /// and on devices with 64KB+ L1D (Snapdragon 8 Gen 2, Apple M-series), in L1.
    /// </summary>
    public struct PrismSpatialData
    {
        public float3 Position; // 12B
        public byte Flags;      // 1B (see PrismFlags)
        public byte _pad0;      // 1B
        public byte _pad1;      // 1B
        public byte _pad2;      // 1B
        // Total: 16B - exactly 4 per 64B cache line
    }

    /// <summary>
    /// COLD data: only read on the main thread for prisms that pass the spatial filter.
    /// Typically a few dozen per frame as the AOE sphere grows - not a cache concern.
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
    /// Cell-volume summation view data - one entry per slot, packed for the Burst
    /// <see cref="CellVolumeSumJob"/> that replaces Cell's managed per-prism volume
    /// recompute (the old 8000-prisms-per-frame slice was a ~10 ms reader-attributed
    /// frame spike at high prism counts; see Docs/PERFORMANCE_OPTIMIZATION.md).
    ///
    /// Distinct from <see cref="PrismDamageData"/> on purpose: the damage view's
    /// Volume/Domain are registration-time snapshots whose staleness is part of the
    /// tested AOE behavior ("Known gaps" in Docs/SPATIAL_INDEX.md), while this view
    /// must be LIVE - Volume mirrors Prism.CachedVolume (pushed by RefreshVolumeCache,
    /// O(growing)/frame), DomainSlot follows steals via ForwardDomainChangeToCell,
    /// and CellId/EnvMass mirror Cell.AddBlock/RemoveBlock membership exactly
    /// (Cell is the single writer of the binding, so the two cannot diverge).
    ///
    /// Layout:
    ///   offset 0: Volume     (4B)  live CachedVolume mirror
    ///   offset 4: CellId     (2B)  volume-membership cell id, -1 = unbound
    ///   offset 6: DomainSlot (1B)  live domain slot (0 Jade / 1 Ruby / 2 Gold / 3 Blue)
    ///   offset 7: EnvMass    (1B)  1 = environment mass (cell trackedBlocks mirror)
    ///   Total: 8B - 8 entries per 64B cache line
    /// </summary>
    public struct PrismCellData
    {
        public float Volume;    // 4B
        public short CellId;    // 2B
        public byte DomainSlot; // 1B
        public byte EnvMass;    // 1B
        // Total: 8B
    }

    /// <summary>
    /// Burst-compiled spatial query over cache-line-packed PrismSpatialData.
    /// Each Execute() reads exactly 16B (one PrismSpatialData entry).
    /// With 4 entries per cache line, a sequential scan of 3000 prisms
    /// touches only 750 cache lines (48KB).
    /// </summary>
    [BurstCompile]
    public struct AOESpatialQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PrismSpatialData> Prisms;
        [ReadOnly] public float3 Center;
        [ReadOnly] public float RadiusSq;

        public NativeList<int>.ParallelWriter HitIndices;

        public void Execute(int index)
        {
            var p = Prisms[index];

            // Single byte check: must be active (bit 0) and not destroyed (bit 1)
            if ((p.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) return;

            float distSq = math.lengthsq(p.Position - Center);
            if (distSq > RadiusSq) return;

            HitIndices.AddNoResize(index);
        }
    }

    /// <summary>
    /// Burst-compiled collider-LOD classification over the packed hot array.
    /// Maintains the per-slot <see cref="PrismFlags.LodNear"/> bit and emits only
    /// TRANSITIONS (slots whose near/far state changed since the last pass), so the
    /// managed apply that follows is O(changed) instead of O(population). With
    /// Reconcile set (first sweep / LOD re-enable) every live slot is emitted and
    /// the bits are rewritten from scratch. Single-threaded IJob: 25k entries is
    /// ~0.1-0.3 ms in Burst, and ordered appends keep the output deterministic.
    /// </summary>
    [BurstCompile]
    public struct LodClassifyJob : IJob
    {
        public NativeArray<PrismSpatialData> Prisms; // read-write: maintains the LodNear bit
        [ReadOnly] public NativeArray<float3> Centers;
        public int EntryCount;
        public int CenterCount;
        public float NearRadiusSq; // enter threshold: a FAR prism becomes near inside this
        public float FarRadiusSq;  // exit threshold (≥ NearRadiusSq): a NEAR prism becomes far outside this
        public bool Reconcile;
        public NativeList<int> BecameNear;
        public NativeList<int> BecameFar;

        public void Execute()
        {
            for (int i = 0; i < EntryCount; i++)
            {
                var p = Prisms[i];
                if ((p.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;

                bool wasNear = (p.Flags & PrismFlags.LodNear) != 0;

                // Hysteresis: entering the bubble uses the tight radius, leaving it
                // the wide one - prisms in the annulus keep their prior state, so
                // the boundary of a MOVING focus stops emitting near/far flip
                // transitions (and collider re-toggles) every tick. A reconcile has
                // no trusted prior state: classify by the wide radius (collider-on
                // is the safe direction).
                float thresholdSq = (Reconcile || wasNear) ? FarRadiusSq : NearRadiusSq;

                bool near = false;
                for (int cI = 0; cI < CenterCount; cI++)
                {
                    if (math.distancesq(p.Position, Centers[cI]) <= thresholdSq)
                    {
                        near = true;
                        break; // near at least one focus - no need to test the rest
                    }
                }

                if (near == wasNear && !Reconcile) continue;

                if (near)
                {
                    p.Flags |= PrismFlags.LodNear;
                    BecameNear.Add(i);
                }
                else
                {
                    p.Flags = (byte)(p.Flags & ~PrismFlags.LodNear);
                    BecameFar.Add(i);
                }
                Prisms[i] = p;
            }
        }
    }

    /// <summary>
    /// Burst-compiled per-cell volume summation over the packed arrays - the
    /// compute half of Cell.EnsureVolumeFresh ("volume is the spine"). One linear
    /// pass filters slots bound to the target cell (live, not destroyed) and
    /// accumulates the exact sums the old managed per-prism pass produced:
    /// all-source volume by domain, environment volume by domain, environment
    /// volume inside the nucleus by domain, plus the three totals. ~0.1-0.3 ms
    /// at 25k entries vs ~10 ms/frame for the managed 8000-prism slice it
    /// replaces (same collapse as LodClassifyJob). Runs synchronously (.Run()),
    /// like every other query in this index - no job/mutation races.
    /// </summary>
    [BurstCompile]
    public struct CellVolumeSumJob : IJob
    {
        [ReadOnly] public NativeArray<PrismSpatialData> Spatial;
        [ReadOnly] public NativeArray<PrismCellData> CellData;
        public int EntryCount;
        public short CellId;
        public float3 Centre;
        public float NucleusRadiusSqr;
        public NativeArray<float> Results; // layout: PrismSpatialIndex.CellVolume* constants

        public void Execute()
        {
            for (int i = 0; i < PrismSpatialIndex.CellVolumeResultCount; i++)
                Results[i] = 0f;

            for (int i = 0; i < EntryCount; i++)
            {
                var cd = CellData[i];
                if (cd.CellId != CellId) continue;
                var s = Spatial[i];
                if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                float v = cd.Volume;
                if (v <= 0f) continue; // destroyed / not yet grown

                int slot = cd.DomainSlot;
                Results[PrismSpatialIndex.CellVolumeBySlot + slot] += v;
                Results[PrismSpatialIndex.CellVolumeTotal] += v;

                if (cd.EnvMass == 0) continue; // fauna bodies: volume-only mass

                Results[PrismSpatialIndex.CellEnvVolumeBySlot + slot] += v;
                Results[PrismSpatialIndex.CellEnvVolumeTotal] += v;

                // Node control vs feeding ground: environment mass inside the
                // nucleus claims control; everything outside is edible prey.
                // Without a nucleus zone the whole cell is the feeding ground
                // (exterior == environment total, OpposingVolume's else-branch).
                if (NucleusRadiusSqr > 0f &&
                    math.distancesq(s.Position, Centre) <= NucleusRadiusSqr)
                    Results[PrismSpatialIndex.CellNucleusEnvVolumeBySlot + slot] += v;
                else
                    Results[PrismSpatialIndex.CellExteriorEnvVolumeTotal] += v;
            }
        }
    }

    /// <summary>
    /// THE canonical spatial index of all live prism mass. One registration
    /// lifecycle, multiple query views - see Docs/SPATIAL_INDEX.md before adding
    /// any new spatial query against prisms (Physics.OverlapSphere / CheckBox
    /// against prisms is an anti-pattern; query this index instead).
    ///
    /// Views served:
    ///   1. AOE damage    - Burst brute-force sphere scan over the hot array
    ///                      (ExplosionImpactor.ProcessBatchFrame).
    ///   2. Occupancy     - bucket hash grid + reservation set. Growth systems
    ///                      (GyroidAssembler / WallAssembler / SchwarzPAssembler)
    ///                      call TryReserve at the grow DECISION, before
    ///                      Instantiate - this closes the race that
    ///                      Physics.CheckBox could never close (prism colliders
    ///                      are disabled for the first Prism.waitTime seconds
    ///                      after spawn).
    ///   3. Neighborhood  - QuerySphere (gather live prisms in range) and
    ///                      IsAnyPrismWithin (boolean probe) serve fauna senses
    ///                      (LightFauna / Boid), assembler mate-finding
    ///                      (GyroidAssembler / WallAssembler) and trail passives
    ///                      (ScoutTrailPrismScaler) - no physics broadphase, no
    ///                      per-collider GetComponent, no scratch-array
    ///                      truncation. See Docs/SPATIAL_INDEX.md.
    ///   4. Cell density  - Register/MarkRestored file each prism into its
    ///                      containing cell's per-domain density grids
    ///                      (Cell.AddBlock); MarkDestroyed/Unregister remove it.
    ///                      The coarse view rides the same lifecycle stream as
    ///                      the fine views, so they cannot diverge (Phase 3).
    ///
    /// Data layout (hot/cold split):
    ///   _spatial[i]  - PrismSpatialData (16B) - read by Burst job for ALL prisms
    ///   _damage[i]   - PrismDamageData  (8B)  - read on main thread for HIT prisms only
    ///   _cellData[i] - PrismCellData    (8B)  - cell-volume summation view (CellVolumeSumJob)
    ///   _prisms[i]   - Prism reference         - managed array for applying damage
    ///   _buckets     - int3 bucket key → index - incremental, prisms are mostly static
    ///
    /// The Burst job scans only _spatial, keeping the working set tight.
    /// Domain/shield/volume data in _damage is never loaded into cache during the scan -
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
    ///   Assembler movers / fauna   → UpdatePosition(index, pos) - anything that
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

        // --- Cell-volume summation view: result layout (CellVolumeSumJob) ---
        // Domain slots are 0 Jade / 1 Ruby / 2 Gold / 3 Blue, matching
        // Cell's published dictionary order.
        public const int CellDomainSlotCount = 4;
        public const int CellVolumeBySlot = 0;            // [0..3]  all-source volume by domain slot
        public const int CellEnvVolumeBySlot = 4;         // [4..7]  environment volume by domain slot
        public const int CellNucleusEnvVolumeBySlot = 8;  // [8..11] environment volume inside the nucleus by slot
        public const int CellVolumeTotal = 12;
        public const int CellEnvVolumeTotal = 13;
        public const int CellExteriorEnvVolumeTotal = 14;
        public const int CellVolumeResultCount = 15;

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
        private NativeArray<PrismSpatialData> _spatial;

        // Cold: read only for hit prisms on main thread
        private NativeArray<PrismDamageData> _damage;

        // Cell-volume summation view: live volume + cell binding + live domain per
        // slot, scanned by CellVolumeSumJob on each cell's 0.25s recompute.
        private NativeArray<PrismCellData> _cellData;
        private NativeArray<float> _cellVolumeScratch;

        // Async summation snapshot: the live arrays are mutated freely on the main
        // thread (Register / UpdateCellVolume per grower / steals), so a
        // worker-thread sum job reads a point-in-time COPY instead. One snapshot
        // per frame is shared by every cell that schedules that frame; taking a
        // new one first completes all prior readers (they finished long ago -
        // requests are 0.25s apart). Main-thread cost = one memcpy, constant
        // whether or not Burst is active in the editor.
        private NativeArray<PrismSpatialData> _sumSnapSpatial;
        private NativeArray<PrismCellData> _sumSnapCellData;
        private int _sumSnapCount;
        private int _sumSnapFrame = -1;
        private JobHandle _sumSnapReaders;
        private static readonly ProfilerMarker s_volumeSnapshotMarker = new("Cell.VolumeSum.Snapshot");

        // Managed: Prism references for applying damage callbacks
        private Prism[] _prisms;

        // Managed: the cell whose per-domain density grids each prism is filed in
        // (the coarse view of this same lifecycle), or null - open space, fauna
        // bodies, slot free. Bound on Register/MarkRestored, released on
        // MarkDestroyed/Unregister.
        private Cell[] _cells;

        private int _highWaterMark;
        private readonly Stack<int> _freeList = new(256);
        private NativeList<int> _hitIndices;

        // Occupancy view: bucket key → registry index, one entry per LIVE
        // (active, not destroyed) prism. Maintained incrementally by
        // Register / MarkDestroyed / MarkRestored / Unregister / UpdatePosition.
        private NativeParallelMultiHashMap<int3, int> _buckets;
        private int _bucketEntryCount;

        // Reservation view: quantized position → claim. Managed dictionary is fine
        // here - reservations are few (bounded by spawn rate × TTL) and main-thread.
        private struct Reservation
        {
            public Vector3 Position;
            public float Expires;
        }
        private readonly Dictionary<Vector3Int, Reservation> _reservations = new();
        private readonly List<Vector3Int> _scratchKeys = new(16);
        private float _nextReservationPrune;

        // --- ProfilerMarkers ---
        // Note: the source branch (PrismAOERegistry on development) had two more
        // markers, AOE.BurstJob.ScheduleECS and AOE.ResolveDamage.ECS - bleeding-edge
        // has no ECS companion-entity path, so they have no code to attach to.
        private static readonly ProfilerMarker s_processExplosion = new("AOE.ProcessExplosion");
        private static readonly ProfilerMarker s_burstJobSchedule = new("AOE.BurstJob.Schedule");
        private static readonly ProfilerMarker s_resolveDamage = new("AOE.ResolveDamage");

        public bool IsAvailable => _spatial.IsCreated;
        public int HighWaterMark => _highWaterMark;

        /// <summary>
        /// Number of live (active, not destroyed) entries - the O(1) counterpart
        /// of <see cref="CopyLivePrisms"/>'s count, maintained by
        /// Register/MarkDestroyed/MarkRestored/Unregister. Telemetry + LOD sizing;
        /// population-scale consumers must not need an O(N) walk just to count.
        /// </summary>
        public int LiveCount { get; private set; }

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
            _spatial = new NativeArray<PrismSpatialData>(INITIAL_CAPACITY, Allocator.Persistent);
            _damage = new NativeArray<PrismDamageData>(INITIAL_CAPACITY, Allocator.Persistent);
            _cellData = new NativeArray<PrismCellData>(INITIAL_CAPACITY, Allocator.Persistent);
            _cellVolumeScratch = new NativeArray<float>(CellVolumeResultCount, Allocator.Persistent);
            _prisms = new Prism[INITIAL_CAPACITY];
            _cells = new Cell[INITIAL_CAPACITY];
            _hitIndices = new NativeList<int>(512, Allocator.Persistent);
            _buckets = new NativeParallelMultiHashMap<int3, int>(INITIAL_CAPACITY, Allocator.Persistent);
            _lodCenters = new NativeArray<float3>(16, Allocator.Persistent);
            _lodBecameNear = new NativeList<int>(512, Allocator.Persistent);
            _lodBecameFar = new NativeList<int>(512, Allocator.Persistent);
        }

        #region Bucket grid

        private static int3 BucketKey(float3 position) =>
            (int3)math.floor(position / BucketSizeMeters);

        private void AddToBucket(int index, float3 position)
        {
            if (_bucketEntryCount >= _buckets.Capacity)
                _buckets.Capacity = _buckets.Capacity * 2;
            _buckets.Add(BucketKey(position), index);
            _bucketEntryCount++;
        }

        private void RemoveFromBucket(int index, float3 position)
        {
            var key = BucketKey(position);
            if (!_buckets.TryGetFirstValue(key, out int value, out var it)) return;
            do
            {
                if (value != index) continue;
                _buckets.Remove(it);
                _bucketEntryCount--;
                return;
            } while (_buckets.TryGetNextValue(out value, ref it));
        }

        /// <summary>
        /// True when probing every bucket in the AABB would touch more entries than a
        /// straight scan of the slot array - wide queries (Scout open-space probes reach
        /// 100m → 26³ ≈ 17k bucket lookups) are cheaper as one pass over the hot array.
        /// </summary>
        private bool BucketWalkCostsMoreThanLinearScan(int3 min, int3 max)
        {
            long bucketVolume = (long)(max.x - min.x + 1) * (max.y - min.y + 1) * (max.z - min.z + 1);
            return bucketVolume > _highWaterMark;
        }

        /// <summary>
        /// True if any LIVE prism (active, not destroyed - reservations excluded)
        /// sits within <paramref name="radius"/> of <paramref name="position"/>.
        /// Bucket-accelerated for tight radii, linear hot-array scan for wide ones.
        /// No physics, no allocation.
        /// </summary>
        public bool IsAnyPrismWithin(Vector3 position, float radius)
        {
            if (!_buckets.IsCreated) return false;
            float3 center = position;
            float radiusSq = radius * radius;
            int3 min = (int3)math.floor((center - radius) / BucketSizeMeters);
            int3 max = (int3)math.floor((center + radius) / BucketSizeMeters);

            if (BucketWalkCostsMoreThanLinearScan(min, max))
            {
                for (int i = 0; i < _highWaterMark; i++)
                {
                    var s = _spatial[i];
                    if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue &&
                        math.distancesq(s.Position, center) <= radiusSq)
                        return true;
                }
                return false;
            }

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_buckets.TryGetFirstValue(new int3(x, y, z), out int idx, out var it))
                    continue;
                do
                {
                    var s = _spatial[idx];
                    if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue &&
                        math.distancesq(s.Position, center) <= radiusSq)
                        return true;
                } while (_buckets.TryGetNextValue(out idx, ref it));
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
        /// Results are an unordered snapshot - entries can be destroyed by the
        /// caller's own side effects mid-iteration (consume, steal, convert), so
        /// iterate with a null/destroyed guard, exactly as collider snapshots
        /// required. Main-thread only.
        /// </summary>
        public int QuerySphere(Vector3 center, float radius, List<Prism> results)
        {
            results.Clear();
            if (!_buckets.IsCreated || _highWaterMark == 0) return 0;
            float3 c = center;
            float radiusSq = radius * radius;
            int3 min = (int3)math.floor((c - radius) / BucketSizeMeters);
            int3 max = (int3)math.floor((c + radius) / BucketSizeMeters);

            if (BucketWalkCostsMoreThanLinearScan(min, max))
            {
                for (int i = 0; i < _highWaterMark; i++)
                {
                    var s = _spatial[i];
                    if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                    if (math.distancesq(s.Position, c) > radiusSq) continue;
                    var prism = _prisms[i];
                    if (prism) results.Add(prism);
                }
                return results.Count;
            }

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_buckets.TryGetFirstValue(new int3(x, y, z), out int idx, out var it))
                    continue;
                do
                {
                    var s = _spatial[idx];
                    if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                    if (math.distancesq(s.Position, c) > radiusSq) continue;
                    var prism = _prisms[idx];
                    if (prism) results.Add(prism);
                } while (_buckets.TryGetNextValue(out idx, ref it));
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
        /// DECISION, before Instantiate - the claim is what closes the
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
                if (r.Expires <= now) continue; // lapsed - prune pass will collect it
                if ((r.Position - position).sqrMagnitude <= radiusSq) return true;
            }
            return false;
        }

        /// <summary>
        /// Called by <see cref="Register"/>: the spawned prism has materialized, so
        /// the claim that protected its site is fulfilled. Matched by proximity, not
        /// exact key - re-parenting under a spindle round-trips the position through
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
        /// spatially contains it - the COARSE view of this same registration
        /// lifecycle (fauna anti-domain targeting reads the grids; the cell phase
        /// system reads LiveBlockCount). Before Phase 3 these call sites lived in
        /// Prism beside every index call; folding them in here means the fine
        /// (occupancy/AOE) and coarse (density) views are fed by one stream and
        /// cannot diverge.
        ///
        /// Fauna bodies (LightFauna / Boid HealthPrisms) bind as VOLUME-ONLY mass:
        /// "volume is the spine" says ALL prisms feed the cell's volume accounting
        /// (Cell.LiveVolume - phase, dominant domain, HUD), so they enter the
        /// volume membership - but they must NOT enter the targeting grids or
        /// prism counts, otherwise a forager swarm reads as its own "mass
        /// concentration" and seeks itself instead of the trail/flora buildup
        /// (and herbivores would be seeded against inedible "prey"). Only
        /// HealthPrisms can be fauna bodies, so the GetComponentInParent walk is
        /// gated to that subtype to keep ordinary trail-prism registrations cheap.
        /// (They stay in the AOE and occupancy views - they are damageable,
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
            // per-domain volume sums ("volume is the spine" - all prisms count,
            // whatever their source) but stay out of the targeting grids and
            // prism counts (see the remarks above).
            bool environmentMass = !(prism is HealthPrism bodyPrism && bodyPrism.ResolveOwnerFauna() != null);
            var cell = Cell.FindCellContaining(position);
            _cells[index] = cell;
            // Pass the slot index explicitly: during Register the caller hasn't
            // stored the returned id on prism.SpatialIndexId yet, so Cell.AddBlock
            // could not resolve it from the prism.
            if (cell) cell.AddBlock(prism, environmentMass, index);
        }

        /// <summary>
        /// Removes the prism from its bound cell's density grids. Idempotent -
        /// the destroyed→unregistered path calls this twice. The prism ref is
        /// passed (not read from _prisms) so Unregister can unbind before it
        /// frees the slot; Cell.RemoveBlock handles destroyed-but-non-null refs
        /// by design, so no Unity-aliveness gate here.
        /// </summary>
        private void UnbindCell(int index, Prism prism)
        {
            var cell = _cells[index];
            _cells[index] = null;
            if (cell) cell.RemoveBlock(prism, index);
        }

        // Collider-LOD classification scratch (persistent, reused per sweep).
        NativeArray<float3> _lodCenters;
        NativeList<int> _lodBecameNear;
        NativeList<int> _lodBecameFar;

        /// <summary>
        /// Collider-LOD classification view: one Burst pass over the packed hot
        /// array that maintains a per-slot near-any-focus bit
        /// (<see cref="PrismFlags.LodNear"/>) and emits only the prisms whose
        /// near/far state CHANGED since the last pass - or the full classification
        /// when <paramref name="reconcile"/> is true (first sweep / LOD re-enable).
        /// <paramref name="enterRadius"/>/<paramref name="exitRadius"/> form the
        /// hysteresis band: far→near inside enter, near→far outside exit, prior
        /// state preserved in the annulus (kills boundary flapping around moving
        /// foci - the transition count is what the managed apply pays for).
        /// Replaces the managed 8000-entries-per-frame sliced scan, whose per-entry
        /// interop cost made every sweep O(population) on the main thread
        /// (5.5 ms slice frames at 25k prisms); the Burst scan is ~0.1-0.3 ms for
        /// the same population and the managed cost becomes O(transitions).
        /// Runs synchronously (Run - Bursted on the main thread), same as every
        /// other query in this index, so there are no job/mutation races.
        /// The LodNear bit lives in the slot flags and is reset naturally when a
        /// slot is re-registered (Register writes fresh flags), so slot reuse can
        /// at worst cost one idempotent extra transition on the next sweep.
        /// </summary>
        public void RunLodClassification(List<Vector3> centers, float enterRadius, float exitRadius, bool reconcile,
            List<Prism> becameNear, List<Prism> becameFar)
        {
            becameNear.Clear();
            becameFar.Clear();
            if (!_spatial.IsCreated || _highWaterMark == 0 || centers == null || centers.Count == 0) return;

            if (!_lodCenters.IsCreated || _lodCenters.Length < centers.Count)
            {
                if (_lodCenters.IsCreated) _lodCenters.Dispose();
                _lodCenters = new NativeArray<float3>(Mathf.NextPowerOfTwo(centers.Count), Allocator.Persistent);
            }
            for (int cI = 0; cI < centers.Count; cI++)
                _lodCenters[cI] = (float3)centers[cI];

            _lodBecameNear.Clear();
            _lodBecameFar.Clear();
            if (_lodBecameNear.Capacity < _highWaterMark) _lodBecameNear.Capacity = _highWaterMark;
            if (_lodBecameFar.Capacity < _highWaterMark) _lodBecameFar.Capacity = _highWaterMark;

            float farRadius = Mathf.Max(enterRadius, exitRadius);
            new LodClassifyJob
            {
                Prisms = _spatial,
                Centers = _lodCenters,
                EntryCount = _highWaterMark,
                CenterCount = centers.Count,
                NearRadiusSq = enterRadius * enterRadius,
                FarRadiusSq = farRadius * farRadius,
                Reconcile = reconcile,
                BecameNear = _lodBecameNear,
                BecameFar = _lodBecameFar,
            }.Run();

            // Resolve indices to managed refs - O(transitions), the only managed cost.
            for (int i = 0; i < _lodBecameNear.Length; i++)
            {
                var prism = _prisms[_lodBecameNear[i]];
                if (prism) becameNear.Add(prism);
            }
            for (int i = 0; i < _lodBecameFar.Length; i++)
            {
                var prism = _prisms[_lodBecameFar[i]];
                if (prism) becameFar.Add(prism);
            }
        }

        /// <summary>
        /// Copies every LIVE prism (active, not destroyed) into
        /// <paramref name="results"/> (cleared first); returns the count. One linear
        /// pass over the managed refs - the iteration view for whole-population
        /// passes (proximity collider-LOD, telemetry). Allocation-free with a
        /// pre-sized caller list; main-thread only.
        /// </summary>
        public int CopyLivePrisms(List<Prism> results)
        {
            results.Clear();
            if (!_spatial.IsCreated) return 0;
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
        /// Prism.HandleTeamChangedForCell only. Does NOT itself touch the AOE
        /// cold data - the caller pairs this with UpdateDomain so the damage
        /// view's friend/foe domain stays live on steals (the Charge-5 "spare
        /// own domain" unlock depends on it; see Docs/SPATIAL_INDEX.md).
        /// </summary>
        public void ForwardDomainChangeToCell(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var prism = _prisms[index];

            // Keep the summation view's LIVE domain fresh for every registered
            // prism - volume-only mass (fauna bodies) and unbound mass never
            // re-file through NotifyBlockDomainChanged below, but their volume
            // must still re-attribute on a team change ("steals re-attribute
            // next pass", same as the old live prism.Domain read).
            if (prism && _cellData.IsCreated)
            {
                var cd = _cellData[index];
                cd.DomainSlot = DomainToSlot(prism.Domain);
                _cellData[index] = cd;
            }

            var cell = _cells[index];
            if (cell && prism) cell.NotifyBlockDomainChanged(prism);
        }

        // ------------------------------------------------------------------
        //  Cell-volume summation view (CellVolumeSumJob)
        //  Binding is written ONLY by Cell.AddBlock/RemoveBlock (both membership
        //  streams - Register→BindCell and the flora HealthBlockTracker - funnel
        //  through them), so the packed view mirrors the cell's membership
        //  bookkeeping by construction. Volume is pushed by
        //  Prism.RefreshVolumeCache (O(growing)/frame); domain by
        //  ForwardDomainChangeToCell above.
        // ------------------------------------------------------------------

        static byte DomainToSlot(Domains domain) => domain switch
        {
            Domains.Jade => 0,
            Domains.Ruby => 1,
            Domains.Gold => 2,
            _ => 3, // Blue - the "no team" sentinel bucket
        };

        /// <summary>
        /// Binds slot <paramref name="index"/> to <paramref name="cellId"/> in the
        /// summation view. Caller: Cell.AddBlock only (single writer).
        /// <paramref name="environmentMass"/> mirrors the cell's trackedBlocks
        /// membership; <paramref name="domain"/> is the prism's live domain.
        /// </summary>
        public void SetCellBinding(int index, short cellId, bool environmentMass, Domains domain)
        {
            if (!_cellData.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var cd = _cellData[index];
            cd.CellId = cellId;
            cd.EnvMass = environmentMass ? (byte)1 : (byte)0;
            cd.DomainSlot = DomainToSlot(domain);
            _cellData[index] = cd;
        }

        /// <summary>
        /// Releases slot <paramref name="index"/> from <paramref name="cellId"/>'s
        /// summation view. No-op when the slot is bound to a different cell - with
        /// dual membership (flora host-cell stream vs spatial containment) the last
        /// binder owns the slot, and the other cell's RemoveBlock must not evict it.
        /// Caller: Cell.RemoveBlock only (single writer). Idempotent.
        /// </summary>
        public void ClearCellBinding(int index, short cellId)
        {
            if (!_cellData.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var cd = _cellData[index];
            if (cd.CellId != cellId) return;
            cd.CellId = -1;
            cd.EnvMass = 0;
            _cellData[index] = cd;
        }

        /// <summary>
        /// Drops every summation-view binding held by <paramref name="cellId"/> -
        /// the packed counterpart of Cell's bulk membership clears
        /// (Initialize / ResetCell), which reset the cell's bookkeeping without a
        /// per-prism RemoveBlock. O(highWaterMark), rare (scene init / replay reset).
        /// </summary>
        public void ClearAllCellBindings(short cellId)
        {
            if (!_cellData.IsCreated) return;
            for (int i = 0; i < _highWaterMark; i++)
            {
                var cd = _cellData[i];
                if (cd.CellId != cellId) continue;
                cd.CellId = -1;
                cd.EnvMass = 0;
                _cellData[i] = cd;
            }
        }

        /// <summary>
        /// Mirrors a prism's live cached volume into the summation view. Caller:
        /// Prism.RefreshVolumeCache - the same O(growing)/frame cadence that keeps
        /// CachedVolume itself fresh, so settled prisms cost nothing.
        /// </summary>
        public void UpdateCellVolume(int index, float volume)
        {
            if (!_cellData.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var cd = _cellData[index];
            cd.Volume = volume;
            _cellData[index] = cd;
        }

        /// <summary>
        /// One Burst pass summing every live prism bound to <paramref name="cellId"/>
        /// into <paramref name="results"/> (layout: the CellVolume* constants).
        /// Replaces Cell's managed per-prism recompute slice - see
        /// Docs/PERFORMANCE_OPTIMIZATION.md. Returns false (results untouched) when
        /// the index isn't allocated or the buffer is undersized; the caller keeps
        /// its previously published sums.
        /// </summary>
        public bool SumCellVolumes(short cellId, Vector3 centre, float nucleusRadiusSqr, float[] results)
        {
            if (!_spatial.IsCreated || !_cellData.IsCreated || !_cellVolumeScratch.IsCreated) return false;
            if (results == null || results.Length < CellVolumeResultCount) return false;

            new CellVolumeSumJob
            {
                Spatial = _spatial,
                CellData = _cellData,
                EntryCount = _highWaterMark,
                CellId = cellId,
                Centre = centre,
                NucleusRadiusSqr = nucleusRadiusSqr,
                Results = _cellVolumeScratch,
            }.Run();

            for (int i = 0; i < CellVolumeResultCount; i++)
                results[i] = _cellVolumeScratch[i];
            return true;
        }

        /// <summary>
        /// Async counterpart of <see cref="SumCellVolumes"/>: schedules the sum on
        /// a worker thread against a point-in-time snapshot of the packed arrays
        /// and returns immediately. Caller (Cell.EnsureVolumeFresh) completes the
        /// handle lazily on a later read and publishes then - readers keep the
        /// previously published sums meanwhile, the same tolerance the old sliced
        /// pass declared. Main-thread cost is the snapshot memcpy (shared by every
        /// cell that schedules in the same frame), so the pass stays cheap even
        /// when the job executes managed (editor with Burst disabled).
        /// <paramref name="results"/> must be a caller-owned persistent
        /// NativeArray the caller does not read until the handle completes.
        /// </summary>
        public bool TryScheduleCellVolumeSum(short cellId, Vector3 centre, float nucleusRadiusSqr,
            NativeArray<float> results, out JobHandle handle)
        {
            handle = default;
            if (!_spatial.IsCreated || !_cellData.IsCreated) return false;
            if (!results.IsCreated || results.Length < CellVolumeResultCount) return false;

            if (_sumSnapFrame != Time.frameCount)
            {
                using (s_volumeSnapshotMarker.Auto())
                {
                    // Prior readers reference the buffers being overwritten; they
                    // were scheduled ≥ one 0.25s window ago, so this is a no-op wait.
                    _sumSnapReaders.Complete();
                    _sumSnapReaders = default;

                    if (!_sumSnapSpatial.IsCreated || _sumSnapSpatial.Length < _highWaterMark)
                    {
                        if (_sumSnapSpatial.IsCreated) _sumSnapSpatial.Dispose();
                        if (_sumSnapCellData.IsCreated) _sumSnapCellData.Dispose();
                        int size = Mathf.NextPowerOfTwo(Mathf.Max(INITIAL_CAPACITY, _highWaterMark));
                        _sumSnapSpatial = new NativeArray<PrismSpatialData>(size, Allocator.Persistent);
                        _sumSnapCellData = new NativeArray<PrismCellData>(size, Allocator.Persistent);
                    }

                    if (_highWaterMark > 0)
                    {
                        NativeArray<PrismSpatialData>.Copy(_spatial, _sumSnapSpatial, _highWaterMark);
                        NativeArray<PrismCellData>.Copy(_cellData, _sumSnapCellData, _highWaterMark);
                    }
                    _sumSnapCount = _highWaterMark;
                    _sumSnapFrame = Time.frameCount;
                }
            }

            handle = new CellVolumeSumJob
            {
                Spatial = _sumSnapSpatial,
                CellData = _sumSnapCellData,
                EntryCount = _sumSnapCount,
                CellId = cellId,
                Centre = centre,
                NucleusRadiusSqr = nucleusRadiusSqr,
                Results = results,
            }.Schedule();
            // Read-read concurrency across cells is fine; the combined handle only
            // gates the NEXT snapshot overwrite (and teardown).
            _sumSnapReaders = JobHandle.CombineDependencies(_sumSnapReaders, handle);
            return true;
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
            if (!_spatial.IsCreated) return -1;
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

            float3 position = (float3)(Vector3)prism.transform.position;
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

            // Summation view: seed with the live volume cache (CreateBlock refreshes
            // it just before registering; grows are pushed via UpdateCellVolume).
            // CellId stays unbound until BindCell → Cell.AddBlock claims the slot.
            _cellData[index] = new PrismCellData
            {
                Volume = prism.CachedVolume,
                CellId = -1,
                DomainSlot = DomainToSlot(prism.Domain),
                EnvMass = 0,
            };

            AddToBucket(index, position);
            LiveCount++;
            // The prism this reservation protected has materialized - fulfil it.
            ConsumeReservationNear(prism.transform.position);
            // Coarse view: file into the containing cell's density grids.
            BindCell(index, prism, (Vector3)position);

            return index;
        }

        public void Unregister(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            // Already freed (e.g. OnDisable then ResetState both fire) - don't
            // double-push the slot onto the free list.
            if (s.Flags == 0 && _prisms[index] == null) return;
            // Live entries hold a bucket slot; destroyed ones were already removed.
            if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
            {
                RemoveFromBucket(index, s.Position);
                LiveCount--;
            }
            // Coarse view: leave the cell grids (no-op if MarkDestroyed already did).
            UnbindCell(index, _prisms[index]);
            // Summation view hygiene: a freed slot must not keep contributing to a
            // cell's sums through the free-list window (Register re-seeds on reuse).
            if (_cellData.IsCreated)
            {
                var cd = _cellData[index];
                cd.CellId = -1;
                cd.EnvMass = 0;
                _cellData[index] = cd;
            }
            s.Flags = 0; // clear all flags including IsActive
            _spatial[index] = s;
            _prisms[index] = null;
            _freeList.Push(index);
        }

        public void MarkDestroyed(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            if ((s.Flags & PrismFlags.Destroyed) != 0) return; // already destroyed
            // Destroyed mass no longer occupies space - growth may fill the site.
            if ((s.Flags & PrismFlags.IsActive) != 0)
            {
                RemoveFromBucket(index, s.Position);
                LiveCount--;
            }
            s.Flags |= PrismFlags.Destroyed;
            _spatial[index] = s;
            // Coarse view: destroyed mass must stop attracting fauna, and the
            // cell's LiveBlockCount must fall so the phase system can descend
            // (the consumption half of the oscillation).
            UnbindCell(index, _prisms[index]);
        }

        /// <summary>
        /// Re-activates a destroyed entry (trail restore mechanics). Refreshes the
        /// stored position and re-enters the occupancy bucket - restored mass
        /// blocks growth and takes AOE damage again. (Before the spatial-index
        /// unification, Restore never told the registry anything, so restored
        /// prisms stayed permanently invisible to batch AOE.)
        /// </summary>
        public void MarkRestored(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            if ((s.Flags & PrismFlags.Destroyed) == 0) return; // not destroyed
            var prism = _prisms[index];
            if (prism != null)
                s.Position = (float3)(Vector3)prism.transform.position;
            s.Flags &= unchecked((byte)~PrismFlags.Destroyed);
            _spatial[index] = s;
            if ((s.Flags & PrismFlags.IsActive) != 0)
            {
                AddToBucket(index, s.Position);
                LiveCount++;
            }
            // Coarse view: restored mass re-enters the cell's density grids
            // (re-resolved at the restored position, like the old
            // Prism.RegisterWithCell call this replaces).
            if (prism) BindCell(index, prism, (Vector3)s.Position);
        }

        /// <summary>
        /// Keeps the index honest for the few prisms that move after registration
        /// (gyroid bonding steers existing blocks into bond sites). Cheap when the
        /// bucket key is unchanged; rebuckets otherwise.
        /// </summary>
        public void UpdatePosition(int index, Vector3 position)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            float3 newPosition = position;
            if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
            {
                int3 oldKey = BucketKey(s.Position);
                int3 newKey = BucketKey(newPosition);
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
            if (!_spatial.IsCreated) return;
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
            if (!_damage.IsCreated) return;
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
            if (!_damage.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var d = _damage[index];
            d.Volume = volume;
            _damage[index] = d;
        }

        #endregion

        #region Benchmark Support

        /// <summary>
        /// Registers synthetic prism data for benchmarking without requiring a Prism
        /// MonoBehaviour. The managed _prisms[index] slot is null - ProcessExplosionFrame
        /// skips it after the spatial query, so this isolates Burst job cost from damage
        /// application cost. Maintains the live-entry-implies-bucket invariant so the
        /// occupancy view stays consistent with Unregister/MarkDestroyed. Deliberately
        /// NOT filed into the cell density view - synthetic mass must not perturb
        /// Cell.LiveVolume / phase / fauna-targeting accounting.
        /// </summary>
        internal int RegisterSynthetic(float3 position, byte flags, float volume, int domain)
        {
            if (!_spatial.IsCreated) return -1;
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
            // Synthetic mass stays out of the summation view (CellId -1) - it must
            // not perturb Cell.LiveVolume / phase accounting (see remarks above).
            _cellData[index] = new PrismCellData
            {
                Volume = volume,
                CellId = -1,
                DomainSlot = DomainToSlot((Domains)domain),
                EnvMass = 0,
            };
            if ((flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
                AddToBucket(index, position);
            return index;
        }

        /// <summary>
        /// Clears all registered prisms, buckets, reservations, and cell bindings.
        /// Used by the AOE benchmark to reset between runs - never call during
        /// gameplay.
        /// </summary>
        internal void ClearAll()
        {
            if (!_spatial.IsCreated) return;
            for (int i = 0; i < _highWaterMark; i++)
            {
                // Real prisms registered before the benchmark ran are filed in
                // their cell's density grids - release them so the coarse view
                // doesn't keep counting mass the index dropped.
                UnbindCell(i, _prisms[i]);
                _prisms[i] = null;
                var s = _spatial[i];
                s.Flags = 0;
                _spatial[i] = s;
                var cd = _cellData[i];
                cd.CellId = -1;
                cd.EnvMass = 0;
                _cellData[i] = cd;
            }
            _freeList.Clear();
            _highWaterMark = 0;
            if (_buckets.IsCreated) _buckets.Clear();
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
        /// (e.g. hit a super-shielded enemy prism - mirrors original Destroy(gameObject) behavior).
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
            using var processScope = s_processExplosion.Auto();

            if (_highWaterMark == 0 || !_spatial.IsCreated) return true;

            // --- Phase 1: Burst job over hot spatial data ---
            _hitIndices.Clear();

            // Ensure NativeList capacity can hold all prisms - AddNoResize in
            // ParallelWriter will throw if capacity < count, killing the async loop
            // and leaving the explosion stuck at max scale.
            if (_hitIndices.Capacity < _highWaterMark)
                _hitIndices.Capacity = _highWaterMark;

            using (s_burstJobSchedule.Auto())
            {
                var job = new AOESpatialQueryJob
                {
                    Prisms = _spatial,
                    Center = (float3)center,
                    RadiusSq = radius * radius,
                    HitIndices = _hitIndices.AsParallelWriter()
                };

                job.Schedule(_highWaterMark, JOB_BATCH_SIZE).Complete();
            }

            // --- Phase 2: Main thread damage logic over cold data + managed refs ---
            using var resolveScope = s_resolveDamage.Auto();
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
            for (int i = 0; i < _hitIndices.Length; i++)
            {
                int idx = _hitIndices[i];

                // Skip if already hit by this explosion (mirrors OnTriggerEnter once-per-pair behavior)
                if (alreadyHit.Contains(idx)) continue;

                // Cap new damage per frame to spread load across frames.
                // Don't add to alreadyHit - the Burst job will re-find these
                // prisms next frame and we'll process them then.
                if (newHitCount >= MAX_NEW_HITS_PER_FRAME)
                    continue;

                alreadyHit.Add(idx);
                newHitCount++;

                var prism = _prisms[idx];
                if (prism == null || prism.destroyed) continue;

                // Read cold data - only for hit prisms, never pollutes the Burst job's cache
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
                Vector3 prismPos = (Vector3)_spatial[idx].Position;
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
            var newSpatial = new NativeArray<PrismSpatialData>(newSize, Allocator.Persistent);
            NativeArray<PrismSpatialData>.Copy(_spatial, newSpatial, _spatial.Length);
            _spatial.Dispose();
            _spatial = newSpatial;

            // Grow cold array
            var newDamage = new NativeArray<PrismDamageData>(newSize, Allocator.Persistent);
            NativeArray<PrismDamageData>.Copy(_damage, newDamage, _damage.Length);
            _damage.Dispose();
            _damage = newDamage;

            // Grow cell-volume summation view
            var newCellData = new NativeArray<PrismCellData>(newSize, Allocator.Persistent);
            NativeArray<PrismCellData>.Copy(_cellData, newCellData, _cellData.Length);
            _cellData.Dispose();
            _cellData = newCellData;

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
            // In-flight async volume sums read the snapshot buffers - finish them
            // before the memory goes away.
            _sumSnapReaders.Complete();
            if (_sumSnapSpatial.IsCreated) _sumSnapSpatial.Dispose();
            if (_sumSnapCellData.IsCreated) _sumSnapCellData.Dispose();
            if (_spatial.IsCreated) _spatial.Dispose();
            if (_damage.IsCreated) _damage.Dispose();
            if (_cellData.IsCreated) _cellData.Dispose();
            if (_cellVolumeScratch.IsCreated) _cellVolumeScratch.Dispose();
            if (_hitIndices.IsCreated) _hitIndices.Dispose();
            if (_buckets.IsCreated) _buckets.Dispose();
            if (_lodCenters.IsCreated) _lodCenters.Dispose();
            if (_lodBecameNear.IsCreated) _lodBecameNear.Dispose();
            if (_lodBecameFar.IsCreated) _lodBecameFar.Dispose();
            LiveCount = 0;
        }

        #endregion
    }
}
