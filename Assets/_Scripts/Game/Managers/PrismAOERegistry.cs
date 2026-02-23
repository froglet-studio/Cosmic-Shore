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
    /// Tightly-packed prism data for Burst-compiled AOE spatial queries.
    /// 32 bytes — two entries fit in a single 64-byte cache line.
    /// </summary>
    public struct PrismAOEData
    {
        public float3 Position;       // 12B
        public float Volume;          // 4B
        public int Domain;            // 4B
        public byte IsShielded;       // 1B
        public byte IsSuperShielded;  // 1B
        public byte Destroyed;        // 1B
        public byte IsActive;         // 1B
        // 8B implicit padding → 32B total
    }

    /// <summary>
    /// Burst-compiled spatial query: filters prisms within an AOE radius.
    /// Runs over contiguous cache-friendly memory instead of scattered MonoBehaviours.
    /// The hot loop touches only Position (12B) + 2 status bytes per prism,
    /// giving ~2.6 prisms per L1 cache line vs ~0 with MonoBehaviour pointer chasing.
    /// </summary>
    [BurstCompile]
    public struct AOESpatialQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PrismAOEData> Prisms;
        [ReadOnly] public float3 Center;
        [ReadOnly] public float RadiusSq;

        public NativeList<int>.ParallelWriter HitIndices;

        public void Execute(int index)
        {
            var p = Prisms[index];
            if (p.IsActive == 0 || p.Destroyed != 0) return;

            float distSq = math.lengthsq(p.Position - Center);
            if (distSq > RadiusSq) return;

            HitIndices.AddNoResize(index);
        }
    }

    /// <summary>
    /// Maintains a cache-friendly NativeArray of prism data for Burst-compiled AOE damage.
    /// Replaces the per-prism Physics OnTriggerEnter → GetComponent → AcceptImpactee chain
    /// with a single Burst job over contiguous memory.
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

        private NativeArray<PrismAOEData> _data;
        private Prism[] _prisms;
        private int _highWaterMark;
        private readonly Stack<int> _freeList = new(256);
        private NativeList<int> _hitIndices;

        public bool IsAvailable => _data.IsCreated;

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
            _data = new NativeArray<PrismAOEData>(INITIAL_CAPACITY, Allocator.Persistent);
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
            _data[index] = new PrismAOEData
            {
                Position = (float3)(Vector3)prism.transform.position,
                Volume = Mathf.Max(prism.prismProperties?.volume ?? 1f, 1f),
                Domain = (int)prism.Domain,
                IsShielded = (byte)(prism.prismProperties is { IsShielded: true } ? 1 : 0),
                IsSuperShielded = (byte)(prism.prismProperties is { IsSuperShielded: true } ? 1 : 0),
                Destroyed = 0,
                IsActive = 1
            };

            return index;
        }

        public void Unregister(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var d = _data[index];
            d.IsActive = 0;
            _data[index] = d;
            _prisms[index] = null;
            _freeList.Push(index);
        }

        public void MarkDestroyed(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var d = _data[index];
            d.Destroyed = 1;
            _data[index] = d;
        }

        public void UpdateShieldState(int index, bool shielded, bool superShielded)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var d = _data[index];
            d.IsShielded = (byte)(shielded ? 1 : 0);
            d.IsSuperShielded = (byte)(superShielded ? 1 : 0);
            _data[index] = d;
        }

        public void UpdateDomain(int index, int domain)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var d = _data[index];
            d.Domain = domain;
            _data[index] = d;
        }

        /// <summary>
        /// Updates the cached volume after a prism finishes growing.
        /// </summary>
        public void UpdateVolume(int index, float volume)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var d = _data[index];
            d.Volume = volume;
            _data[index] = d;
        }

        #endregion

        #region AOE Processing

        /// <summary>
        /// Processes one frame of AOE explosion damage using a Burst-compiled spatial query.
        /// The job runs over contiguous PrismAOEData (cache-line packed), then domain/shield
        /// logic is applied on the main thread for the much smaller hit set.
        /// </summary>
        /// <param name="center">Explosion world position</param>
        /// <param name="radius">Current explosion radius this frame</param>
        /// <param name="speed">Explosion speed (MaxScale / Duration)</param>
        /// <param name="inertia">Explosion inertia multiplier</param>
        /// <param name="explosionDomain">Team that owns the explosion</param>
        /// <param name="affectSelf">Should explosion damage same-team prisms?</param>
        /// <param name="destructive">Should explosion destroy prisms?</param>
        /// <param name="devastating">Should explosion ignore shields?</param>
        /// <param name="shielding">Should explosion shield same-team prisms?</param>
        /// <param name="anonymous">Is this an anonymous explosion (no vessel)?</param>
        /// <param name="vessel">The vessel that caused the explosion (null if anonymous)</param>
        /// <param name="alreadyHit">Per-explosion set tracking which prism indices were already processed</param>
        /// <returns>Number of newly-hit prisms this frame</returns>
        public int ProcessExplosionFrame(
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
            if (_highWaterMark == 0) return 0;

            // --- Burst job: spatial filter over contiguous memory ---
            _hitIndices.Clear();

            var job = new AOESpatialQueryJob
            {
                Prisms = _data,
                Center = (float3)center,
                RadiusSq = radius * radius,
                HitIndices = _hitIndices.AsParallelWriter()
            };

            var handle = job.Schedule(_highWaterMark, JOB_BATCH_SIZE);
            handle.Complete();

            // --- Main thread: apply domain/shield/damage logic to hit prisms ---
            // This is the small set (typically a few dozen new hits per frame as the sphere grows).
            int newHits = 0;
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

                var data = _data[idx];
                int prismDomain = data.Domain;

                // Super-shielded + different team: deactivate super shield only
                if ((prismDomain != expDomain || affectSelf) && data.IsSuperShielded != 0)
                {
                    prism.DeactivateShields();
                    UpdateShieldState(idx, false, false);
                    newHits++;
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
                    newHits++;
                    continue;
                }

                // Compute impact vector (same formula as AOEExplosion.CalculateImpactVector)
                Vector3 direction = ((Vector3)data.Position - center).normalized;
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

                newHits++;
            }

            return newHits;
        }

        #endregion

        #region Capacity

        private void EnsureCapacity(int requiredIndex)
        {
            if (requiredIndex < _data.Length) return;

            int newSize = Mathf.NextPowerOfTwo(requiredIndex + 1);

            var newData = new NativeArray<PrismAOEData>(newSize, Allocator.Persistent);
            NativeArray<PrismAOEData>.Copy(_data, newData, _data.Length);
            _data.Dispose();
            _data = newData;

            var newPrisms = new Prism[newSize];
            System.Array.Copy(_prisms, newPrisms, _prisms.Length);
            _prisms = newPrisms;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (_data.IsCreated) _data.Dispose();
            if (_hitIndices.IsCreated) _hitIndices.Dispose();
        }

        #endregion
    }
}
