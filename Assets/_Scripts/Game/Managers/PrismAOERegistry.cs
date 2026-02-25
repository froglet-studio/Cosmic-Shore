using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using CosmicShore.Core;
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

        // Hot: scanned by Burst job every frame during AOE
        private NativeArray<PrismSpatialData> _spatial;

        // Cold: read only for hit prisms on main thread
        private NativeArray<PrismDamageData> _damage;

        // Managed: Prism references for applying damage callbacks
        private Prism[] _prisms;

        private int _highWaterMark;
        private readonly Stack<int> _freeList = new(256);
        private NativeList<int> _hitIndices;

        public bool IsAvailable => _spatial.IsCreated;

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
            _hitIndices = new NativeList<int>(512, Allocator.Persistent);
        }

        #region Registration

        /// <summary>
        /// Registers a prism for batch AOE processing. Returns the registry index
        /// which should be stored on the Prism for O(1) updates and unregistration.
        /// </summary>
        public int Register(Prism prism)
        {
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

            return index;
        }

        public void Unregister(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            s.Flags = 0; // clear all flags including IsActive
            _spatial[index] = s;
            _prisms[index] = null;
            _freeList.Push(index);
        }

        public void MarkDestroyed(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            s.Flags |= PrismFlags.Destroyed;
            _spatial[index] = s;
        }

        public void UpdateShieldState(int index, bool shielded, bool superShielded)
        {
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
            if (index < 0 || index >= _highWaterMark) return;
            var d = _damage[index];
            d.Volume = volume;
            _damage[index] = d;
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
            if (_highWaterMark == 0) return true;

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
                Center = (float3)center,
                RadiusSq = radius * radius,
                HitIndices = _hitIndices.AsParallelWriter()
            };

            var handle = job.Schedule(_highWaterMark, JOB_BATCH_SIZE);
            handle.Complete();

            // --- Phase 2: Main thread damage logic over cold data + managed refs ---
            bool shouldContinue = true;
            int expDomain = (int)explosionDomain;

            // Cache vessel info to avoid repeated interface property access
            Domains vesselDomain = Domains.None;
            string vesselPlayerName = null;
            if (!anonymous && vessel != null)
            {
                var status = vessel.VesselStatus;
                vesselDomain = status.Domain;
                vesselPlayerName = status.Player.Name;
            }

            for (int i = 0; i < _hitIndices.Length; i++)
            {
                int idx = _hitIndices[i];

                // Skip if already hit by this explosion (mirrors OnTriggerEnter once-per-pair behavior)
                if (!alreadyHit.Add(idx)) continue;

                var prism = _prisms[idx];
                if (prism == null || prism.destroyed) continue;

                // Read cold data — only for hit prisms, never pollutes the Burst job's cache
                var flags = _spatial[idx].Flags;
                var dmg = _damage[idx];
                int prismDomain = dmg.Domain;

                // Super-shielded + different team: deactivate super shield and destroy explosion.
                // Mirrors original ExecuteCommonPrismCommands which calls Destroy(gameObject)
                // and intentionally falls through to the damage/shield logic below.
                if ((prismDomain != expDomain || affectSelf) && (flags & PrismFlags.IsSuperShielded) != 0)
                {
                    prism.DeactivateShields();
                    UpdateShieldState(idx, false, false);
                    shouldContinue = false;
                    // Fall through — original code does NOT return/continue here
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
                    prism.Damage(impactVector, Domains.None, "🔥GuyFawkes🔥", devastating);
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
        }

        #endregion
    }
}
