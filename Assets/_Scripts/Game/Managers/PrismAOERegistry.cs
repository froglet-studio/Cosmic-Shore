using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.ECS;
using CosmicShore.Utilities;

namespace CosmicShore.Game
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
    /// Maintains hot/cold split NativeArrays of prism data for Burst-compiled AOE damage.
    ///
    /// Data layout (hot/cold split):
    ///   _spatial[i] — PrismSpatialData (16B) — read by Burst job for ALL prisms
    ///   _damage[i]  — PrismDamageData  (8B)  — read on main thread for HIT prisms only
    ///   _prisms[i]  — Prism reference         — managed array for applying damage
    ///
    /// The Burst job scans only _spatial, keeping the working set tight.
    /// Domain/shield/volume data in _damage is never loaded into cache during the scan —
    /// it's only touched for the small set of prisms that actually got hit.
    ///
    /// Registration lifecycle:
    ///   Prism.Initialize()   → Register(prism) → stores index on Prism
    ///   Prism.ReturnToPool() → Unregister(index) → frees slot
    ///   PrismStateManager    → UpdateShieldState(index, ...) on state change
    /// </summary>
    public class PrismAOERegistry : Singleton<PrismAOERegistry>
    {
        private const int INITIAL_CAPACITY = 4096;
        private const int JOB_BATCH_SIZE = 256;

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

        // Bridge references (parallel to _prisms) for syncing state to ECS companion entities.
        // Populated during Register(), null for prisms without PrismEntityBridge.
        private PrismEntityBridge[] _bridges;

        // Cached ECS query for the hybrid path — avoids per-frame EntityQuery allocation.
        private EntityQuery _ecsQuery;
        private World _ecsQueryWorld;

        // --- ProfilerMarkers ---
        private static readonly ProfilerMarker s_processExplosion = new("AOE.ProcessExplosion");
        private static readonly ProfilerMarker s_burstJobSchedule = new("AOE.BurstJob.Schedule");
        private static readonly ProfilerMarker s_burstJobScheduleECS = new("AOE.BurstJob.ScheduleECS");
        private static readonly ProfilerMarker s_resolveDamageLegacy = new("AOE.ResolveDamage.Legacy");
        private static readonly ProfilerMarker s_resolveDamageECS = new("AOE.ResolveDamage.ECS");

        public bool IsAvailable => _spatial.IsCreated;
        public int HighWaterMark => _highWaterMark;

        public static PrismAOERegistry EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[PrismAOERegistry]");
            go.AddComponent<PrismAOERegistry>();
            return Instance;
        }

        public override void Awake()
        {
            base.Awake();
            _spatial = new NativeArray<PrismSpatialData>(INITIAL_CAPACITY, Allocator.Persistent);
            _damage = new NativeArray<PrismDamageData>(INITIAL_CAPACITY, Allocator.Persistent);
            _prisms = new Prism[INITIAL_CAPACITY];
            _bridges = new PrismEntityBridge[INITIAL_CAPACITY];
            _hitIndices = new NativeList<int>(512, Allocator.Persistent);
        }

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
            _bridges[index] = prism.TryGetComponent(out PrismEntityBridge bridge) ? bridge : null;

            // Build flags byte
            byte flags = PrismFlags.IsActive;
            if (prism.prismProperties is { IsShielded: true }) flags |= PrismFlags.IsShielded;
            if (prism.prismProperties is { IsSuperShielded: true }) flags |= PrismFlags.IsSuperShielded;

            _spatial[index] = new PrismSpatialData
            {
                Position = (float3)(Vector3)prism.transform.position,
                Flags = flags
            };

            _damage[index] = new PrismDamageData
            {
                Volume = Mathf.Max(prism.prismProperties?.volume ?? 1f, 1f),
                Domain = (int)prism.Domain
            };

            // Create companion entity if bridge is present and ECS is enabled
            if (_bridges[index] != null)
            {
                _bridges[index].CreateCompanionEntity(
                    _spatial[index].Position,
                    _spatial[index].Flags,
                    _damage[index].Volume,
                    _damage[index].Domain,
                    index);
            }

            return index;
        }

        public void Unregister(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            _bridges[index]?.DestroyCompanionEntity();
            var s = _spatial[index];
            s.Flags = 0; // clear all flags including IsActive
            _spatial[index] = s;
            _prisms[index] = null;
            _bridges[index] = null;
            _freeList.Push(index);
        }

        public void MarkDestroyed(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            s.Flags |= PrismFlags.Destroyed;
            _spatial[index] = s;
            _bridges[index]?.MarkDestroyed();
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
            _bridges[index]?.UpdateFlags(s.Flags);
        }

        public void UpdateDomain(int index, int domain)
        {
            if (!_damage.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var d = _damage[index];
            d.Domain = domain;
            _damage[index] = d;
            _bridges[index]?.UpdateDamageData(d.Volume, d.Domain);
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
            _bridges[index]?.UpdateDamageData(d.Volume, d.Domain);
        }

        #endregion

        #region Benchmark Support

        /// <summary>
        /// Registers synthetic prism data for benchmarking without requiring a Prism MonoBehaviour.
        /// The managed _prisms[index] slot is null — ResolveDamage will skip it after the
        /// spatial query, so this isolates Burst job cost from damage application cost.
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
            _bridges[index] = null;
            _spatial[index] = new PrismSpatialData { Position = position, Flags = flags };
            _damage[index] = new PrismDamageData { Volume = volume, Domain = domain };
            return index;
        }

        /// <summary>
        /// Clears all registered prisms. Used by benchmark to reset between runs.
        /// </summary>
        internal void ClearAll()
        {
            if (!_spatial.IsCreated) return;
            for (int i = 0; i < _highWaterMark; i++)
            {
                _prisms[i] = null;
                _bridges[i] = null;
                var s = _spatial[i];
                s.Flags = 0;
                _spatial[i] = s;
            }
            _freeList.Clear();
            _highWaterMark = 0;
        }

        #endregion

        #region AOE Processing

        /// <summary>
        /// Processes one frame of AOE explosion damage.
        /// Delegates to ECS or legacy path based on PrismEntityBridge.UseECS toggle.
        ///
        /// Returns true if the explosion should continue, false if it should be destroyed
        /// (e.g. hit a super-shielded enemy prism).
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
            using (s_processExplosion.Auto())
            {
                if (PrismEntityBridge.UseECS)
                {
                    return ProcessExplosionFrameECS(
                        center, radius, speed, inertia,
                        explosionDomain, affectSelf, destructive, devastating, shielding,
                        anonymous, vessel, alreadyHit);
                }

                return ProcessExplosionFrameLegacy(
                    center, radius, speed, inertia,
                    explosionDomain, affectSelf, destructive, devastating, shielding,
                    anonymous, vessel, alreadyHit);
            }
        }

        /// <summary>
        /// Legacy path: Burst job over manually-managed NativeArrays.
        /// </summary>
        private bool ProcessExplosionFrameLegacy(
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
            if (_highWaterMark == 0 || !_spatial.IsCreated) return true;

            // --- Phase 1: Burst job over hot spatial data ---
            _hitIndices.Clear();

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
            return ResolveDamageLegacy(
                center, speed, inertia, explosionDomain, affectSelf,
                destructive, devastating, shielding, anonymous, vessel, alreadyHit);
        }

        /// <summary>
        /// ECS path: Burst job reads from EntityQuery-sourced NativeArrays.
        /// Same AOESpatialQueryJob, different data source. Falls back to legacy on ECS failure.
        ///
        /// AOESpatial has identical 16B layout to PrismSpatialData — the NativeArray is
        /// reinterpreted zero-cost via NativeArray.Reinterpret() for the Burst job.
        /// Hit indices map to managed Prism[] via AOEManagedRef.ManagedIndex.
        /// </summary>
        private bool ProcessExplosionFrameECS(
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
            if (!_spatial.IsCreated) return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return ProcessExplosionFrameLegacy(
                    center, radius, speed, inertia,
                    explosionDomain, affectSelf, destructive, devastating, shielding,
                    anonymous, vessel, alreadyHit);
            }

            if (_ecsQueryWorld != world)
            {
                _ecsQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<AOESpatial, AOEDamage, AOEManagedRef>()
                    .Build(world.EntityManager);
                _ecsQueryWorld = world;
            }

            int entityCount = _ecsQuery.CalculateEntityCount();
            if (entityCount == 0) return true;

            // Get parallel arrays from ECS — TempJob is a fast bump allocator, ~0 cost
            var ecsSpatial = _ecsQuery.ToComponentDataArray<AOESpatial>(Allocator.TempJob);
            var ecsDamage = _ecsQuery.ToComponentDataArray<AOEDamage>(Allocator.TempJob);
            var ecsManagedRefs = _ecsQuery.ToComponentDataArray<AOEManagedRef>(Allocator.TempJob);

            // Reinterpret AOESpatial (IComponentData, 16B) as PrismSpatialData (plain struct, 16B)
            // Same memory layout — position (float3, 12B) + flags (byte, 1B) + padding (3B)
            var spatialForJob = ecsSpatial.Reinterpret<PrismSpatialData>();

            // --- Phase 1: Same Burst job, ECS-sourced data ---
            _hitIndices.Clear();
            if (_hitIndices.Capacity < entityCount)
                _hitIndices.Capacity = entityCount;

            using (s_burstJobScheduleECS.Auto())
            {
                var job = new AOESpatialQueryJob
                {
                    Prisms = spatialForJob,
                    Center = (float3)center,
                    RadiusSq = radius * radius,
                    HitIndices = _hitIndices.AsParallelWriter()
                };

                job.Schedule(entityCount, JOB_BATCH_SIZE).Complete();
            }

            // --- Phase 2: Damage resolution using managed refs to map back to Prism[] ---
            bool shouldContinue = ResolveDamageECS(
                center, speed, inertia, explosionDomain, affectSelf,
                destructive, devastating, shielding, anonymous, vessel, alreadyHit,
                ecsSpatial, ecsDamage, ecsManagedRefs);

            ecsSpatial.Dispose();
            ecsDamage.Dispose();
            ecsManagedRefs.Dispose();

            return shouldContinue;
        }

        /// <summary>
        /// Phase 2 damage resolution for the legacy path.
        /// Hit indices from the Burst job index directly into the registry's parallel arrays.
        /// </summary>
        private bool ResolveDamageLegacy(
            Vector3 center,
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
            using var _ = s_resolveDamageLegacy.Auto();
            bool shouldContinue = true;
            int expDomain = (int)explosionDomain;

            Domains vesselDomain = Domains.None;
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

                var flags = _spatial[idx].Flags;
                var dmg = _damage[idx];
                int prismDomain = dmg.Domain;

                if ((prismDomain != expDomain || affectSelf) && (flags & PrismFlags.IsSuperShielded) != 0)
                {
                    prism.DeactivateShields();
                    UpdateShieldState(idx, false, false);
                    shouldContinue = false;
                }

                if ((prismDomain == expDomain && !affectSelf) || !destructive)
                {
                    if (shielding && prismDomain == expDomain)
                        prism.ActivateShield();
                    else
                        prism.ActivateShield(2f);
                    UpdateShieldState(idx, true, false);
                    continue;
                }

                Vector3 prismPos = (Vector3)_spatial[idx].Position;
                Vector3 direction = (prismPos - center).normalized;
                Vector3 impactVector = direction * speed * inertia;

                if (anonymous)
                    prism.Damage(impactVector, Domains.None, "\U0001f525GuyFawkes\U0001f525", devastating);
                else
                    prism.Damage(impactVector, vesselDomain, vesselPlayerName, devastating);

                if (prism.destroyed)
                    MarkDestroyed(idx);
                else
                    UpdateShieldState(idx,
                        prism.prismProperties.IsShielded,
                        prism.prismProperties.IsSuperShielded);
            }

            return shouldContinue;
        }

        /// <summary>
        /// Phase 2 damage resolution for the ECS path.
        /// Hit indices are into the EntityQuery snapshot arrays; AOEManagedRef maps back to _prisms[].
        /// </summary>
        private bool ResolveDamageECS(
            Vector3 center,
            float speed,
            float inertia,
            Domains explosionDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            IVessel vessel,
            HashSet<int> alreadyHit,
            NativeArray<AOESpatial> ecsSpatial,
            NativeArray<AOEDamage> ecsDamage,
            NativeArray<AOEManagedRef> ecsManagedRefs)
        {
            using var _ = s_resolveDamageECS.Auto();
            bool shouldContinue = true;
            int expDomain = (int)explosionDomain;

            Domains vesselDomain = Domains.None;
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
                int ecsIdx = _hitIndices[i];
                int managedIdx = ecsManagedRefs[ecsIdx].ManagedIndex;

                // Skip if already hit by this explosion (mirrors OnTriggerEnter once-per-pair behavior)
                if (alreadyHit.Contains(managedIdx)) continue;

                // Cap new damage per frame to spread load across frames.
                // Don't add to alreadyHit — the Burst job will re-find these
                // prisms next frame and we'll process them then.
                if (newHitCount >= MAX_NEW_HITS_PER_FRAME)
                    continue;

                alreadyHit.Add(managedIdx);
                newHitCount++;

                var prism = _prisms[managedIdx];
                if (prism == null || prism.destroyed) continue;

                var flags = ecsSpatial[ecsIdx].Flags;
                int prismDomain = ecsDamage[ecsIdx].Domain;

                if ((prismDomain != expDomain || affectSelf) && (flags & PrismFlags.IsSuperShielded) != 0)
                {
                    prism.DeactivateShields();
                    UpdateShieldState(managedIdx, false, false);
                    shouldContinue = false;
                }

                if ((prismDomain == expDomain && !affectSelf) || !destructive)
                {
                    if (shielding && prismDomain == expDomain)
                        prism.ActivateShield();
                    else
                        prism.ActivateShield(2f);
                    UpdateShieldState(managedIdx, true, false);
                    continue;
                }

                Vector3 prismPos = new Vector3(
                    ecsSpatial[ecsIdx].Position.x,
                    ecsSpatial[ecsIdx].Position.y,
                    ecsSpatial[ecsIdx].Position.z);
                Vector3 direction = (prismPos - center).normalized;
                Vector3 impactVector = direction * speed * inertia;

                if (anonymous)
                    prism.Damage(impactVector, Domains.None, "\U0001f525GuyFawkes\U0001f525", devastating);
                else
                    prism.Damage(impactVector, vesselDomain, vesselPlayerName, devastating);

                if (prism.destroyed)
                    MarkDestroyed(managedIdx);
                else
                    UpdateShieldState(managedIdx,
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

            // Grow managed arrays
            var newPrisms = new Prism[newSize];
            System.Array.Copy(_prisms, newPrisms, _prisms.Length);
            _prisms = newPrisms;

            var newBridges = new PrismEntityBridge[newSize];
            System.Array.Copy(_bridges, newBridges, _bridges.Length);
            _bridges = newBridges;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (_spatial.IsCreated) _spatial.Dispose();
            if (_damage.IsCreated) _damage.Dispose();
            if (_hitIndices.IsCreated) _hitIndices.Dispose();
        }

        #endregion
    }
}
