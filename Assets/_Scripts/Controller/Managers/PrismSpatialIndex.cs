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
        public float3 Position; // 12B
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
    ///   3. (Phase 2)     — QuerySphere/FindNearest for fauna senses, mate
    ///                      finding, trail passives. See Docs/SPATIAL_INDEX.md.
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
    ///                                consumes the matching reservation
    ///   Prism.SetupDestruction     → MarkDestroyed(index) → AOE skips, bucket freed
    ///   Prism.Restore              → MarkRestored(index) → re-enters AOE + bucket
    ///   Prism.OnDisable/OnDestroy  → Unregister(index) → frees slot
    ///   PrismStateManager          → UpdateShieldState(index, ...) on state change
    ///   GyroidAssembler movers     → UpdatePosition(index, pos) when steering blocks
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
        private NativeArray<PrismSpatialData> _spatial;

        // Cold: read only for hit prisms on main thread
        private NativeArray<PrismDamageData> _damage;

        // Managed: Prism references for applying damage callbacks
        private Prism[] _prisms;

        private int _highWaterMark;
        private readonly Stack<int> _freeList = new(256);
        private NativeList<int> _hitIndices;

        // Occupancy view: bucket key → registry index, one entry per LIVE
        // (active, not destroyed) prism. Maintained incrementally by
        // Register / MarkDestroyed / MarkRestored / Unregister / UpdatePosition.
        private NativeParallelMultiHashMap<int3, int> _buckets;
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

        // --- ProfilerMarkers ---
        // Note: the source branch (PrismAOERegistry on development) had two more
        // markers, AOE.BurstJob.ScheduleECS and AOE.ResolveDamage.ECS — bleeding-edge
        // has no ECS companion-entity path, so they have no code to attach to.
        private static readonly ProfilerMarker s_processExplosion = new("AOE.ProcessExplosion");
        private static readonly ProfilerMarker s_burstJobSchedule = new("AOE.BurstJob.Schedule");
        private static readonly ProfilerMarker s_resolveDamage = new("AOE.ResolveDamage");

        public bool IsAvailable => _spatial.IsCreated;
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
            _spatial = new NativeArray<PrismSpatialData>(INITIAL_CAPACITY, Allocator.Persistent);
            _damage = new NativeArray<PrismDamageData>(INITIAL_CAPACITY, Allocator.Persistent);
            _prisms = new Prism[INITIAL_CAPACITY];
            _hitIndices = new NativeList<int>(512, Allocator.Persistent);
            _buckets = new NativeParallelMultiHashMap<int3, int>(INITIAL_CAPACITY, Allocator.Persistent);
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
        /// True if any LIVE prism (active, not destroyed — reservations excluded)
        /// sits within <paramref name="radius"/> of <paramref name="position"/>.
        /// O(buckets covered), no physics, no allocation.
        /// </summary>
        public bool IsAnyPrismWithin(Vector3 position, float radius)
        {
            if (!_buckets.IsCreated) return false;
            float3 center = position;
            float radiusSq = radius * radius;
            int3 min = (int3)math.floor((center - radius) / BucketSizeMeters);
            int3 max = (int3)math.floor((center + radius) / BucketSizeMeters);

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

        #region Registration

        /// <summary>
        /// Registers a prism for batch AOE processing. Returns the registry index
        /// which should be stored on the Prism for O(1) updates and unregistration.
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

            AddToBucket(index, position);
            // The prism this reservation protected has materialized — fulfil it.
            ConsumeReservationNear(prism.transform.position);

            return index;
        }

        public void Unregister(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            // Already freed (e.g. OnDisable then ResetState both fire) — don't
            // double-push the slot onto the free list.
            if (s.Flags == 0 && _prisms[index] == null) return;
            // Live entries hold a bucket slot; destroyed ones were already removed.
            if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
                RemoveFromBucket(index, s.Position);
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
            // Destroyed mass no longer occupies space — growth may fill the site.
            if ((s.Flags & PrismFlags.IsActive) != 0)
                RemoveFromBucket(index, s.Position);
            s.Flags |= PrismFlags.Destroyed;
            _spatial[index] = s;
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
                AddToBucket(index, s.Position);
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
        /// MonoBehaviour. The managed _prisms[index] slot is null — ProcessExplosionFrame
        /// skips it after the spatial query, so this isolates Burst job cost from damage
        /// application cost. Maintains the live-entry-implies-bucket invariant so the
        /// occupancy view stays consistent with Unregister/MarkDestroyed.
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
            _spatial[index] = new PrismSpatialData { Position = position, Flags = flags };
            _damage[index] = new PrismDamageData { Volume = volume, Domain = domain };
            if ((flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
                AddToBucket(index, position);
            return index;
        }

        /// <summary>
        /// Clears all registered prisms, buckets, and reservations. Used by the
        /// AOE benchmark to reset between runs — never call during gameplay.
        /// </summary>
        internal void ClearAll()
        {
            if (!_spatial.IsCreated) return;
            for (int i = 0; i < _highWaterMark; i++)
            {
                _prisms[i] = null;
                var s = _spatial[i];
                s.Flags = 0;
                _spatial[i] = s;
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
            using var processScope = s_processExplosion.Auto();

            if (_highWaterMark == 0 || !_spatial.IsCreated) return true;

            // --- Phase 1: Burst job over hot spatial data ---
            _hitIndices.Clear();

            // Ensure NativeList capacity can hold all prisms — AddNoResize in
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

            // Grow managed array
            var newPrisms = new Prism[newSize];
            System.Array.Copy(_prisms, newPrisms, _prisms.Length);
            _prisms = newPrisms;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (_spatial.IsCreated) _spatial.Dispose();
            if (_damage.IsCreated) _damage.Dispose();
            if (_hitIndices.IsCreated) _hitIndices.Dispose();
            if (_buckets.IsCreated) _buckets.Dispose();
        }

        #endregion
    }
}
