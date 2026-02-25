using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using CosmicShore.Utilities;

namespace CosmicShore.Game
{
    /// <summary>
    /// Centralized Jobs-based manager for prism explosion and implosion VFX.
    /// Replaces per-instance UniTask async loops with batched Burst-compiled updates.
    /// Follows the same pattern as MaterialStateManager and PrismScaleManager.
    /// </summary>
    public class PrismEffectsManager : Singleton<PrismEffectsManager>
    {
        private static bool _instanceDestroyed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instanceDestroyed = false;

        /// <summary>
        /// Ensures a PrismEffectsManager instance exists. If none was placed in the scene,
        /// creates one automatically so explosion/implosion effects don't silently fail.
        /// Returns null during scene teardown to avoid spawning objects from OnDestroy.
        /// </summary>
        public static PrismEffectsManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            // Don't auto-create after the previous instance was destroyed (scene teardown)
            if (_instanceDestroyed) return null;

            var go = new GameObject("[PrismEffectsManager]");
            go.AddComponent<PrismEffectsManager>();
            Debug.LogWarning("[PrismEffectsManager] No instance found in scene — auto-created. " +
                             "Consider adding one to the scene to avoid this overhead.");
            return Instance;
        }

        private const int BATCH_SIZE = 128;
        private const int INITIAL_CAPACITY = 64;

        // Explosion tracking — HashSet for O(1) Add/Remove/Contains
        private readonly HashSet<PrismExplosion> activeExplosionSet = new(INITIAL_CAPACITY);
        private readonly List<PrismExplosion> tempExplosionList = new(INITIAL_CAPACITY);
        private readonly List<PrismExplosion> explosionCompletionQueue = new(32);
        private NativeArray<ExplosionJobData> explosionJobData;

        // Implosion tracking — same pattern
        private readonly HashSet<PrismImplosion> activeImplosionSet = new(INITIAL_CAPACITY);
        private readonly List<PrismImplosion> tempImplosionList = new(INITIAL_CAPACITY);
        private readonly List<PrismImplosion> implosionCompletionQueue = new(32);
        private NativeArray<ImplosionJobData> implosionJobData;

        // Shared property block for batched shader updates
        private MaterialPropertyBlock sharedMPB;

        // Shader property IDs (cached)
        private static readonly int ExplosionAmountID = Shader.PropertyToID("_ExplosionAmount");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
        private static readonly int ImplosionProgressID = Shader.PropertyToID("_State");
        private static readonly int ConvergencePointID = Shader.PropertyToID("_Location");

        public override void Awake()
        {
            base.Awake();
            _instanceDestroyed = false;
            sharedMPB = new MaterialPropertyBlock();
            explosionJobData = new NativeArray<ExplosionJobData>(INITIAL_CAPACITY, Allocator.Persistent);
            implosionJobData = new NativeArray<ImplosionJobData>(INITIAL_CAPACITY, Allocator.Persistent);
        }

        #region Registration

        public void RegisterExplosion(PrismExplosion explosion)
        {
            if (explosion == null) return;
            if (activeExplosionSet.Add(explosion))
                EnsureExplosionCapacity();
        }

        public void UnregisterExplosion(PrismExplosion explosion)
        {
            activeExplosionSet.Remove(explosion);
        }

        public void RegisterImplosion(PrismImplosion implosion)
        {
            if (implosion == null) return;
            if (activeImplosionSet.Add(implosion))
                EnsureImplosionCapacity();
        }

        public void UnregisterImplosion(PrismImplosion implosion)
        {
            activeImplosionSet.Remove(implosion);
        }

        #endregion

        #region Capacity

        private void EnsureExplosionCapacity()
        {
            if (activeExplosionSet.Count <= explosionJobData.Length) return;
            var newSize = Mathf.NextPowerOfTwo(activeExplosionSet.Count);
            var newArray = new NativeArray<ExplosionJobData>(newSize, Allocator.Persistent);
            if (explosionJobData.IsCreated) explosionJobData.Dispose();
            explosionJobData = newArray;
        }

        private void EnsureImplosionCapacity()
        {
            if (activeImplosionSet.Count <= implosionJobData.Length) return;
            var newSize = Mathf.NextPowerOfTwo(activeImplosionSet.Count);
            var newArray = new NativeArray<ImplosionJobData>(newSize, Allocator.Persistent);
            if (implosionJobData.IsCreated) implosionJobData.Dispose();
            implosionJobData = newArray;
        }

        #endregion

        private void Update()
        {
            float dt = Time.deltaTime;
            if (activeExplosionSet.Count > 0) ProcessExplosions(dt);
            if (activeImplosionSet.Count > 0) ProcessImplosions(dt);
        }

        #region Explosion Processing

        private void ProcessExplosions(float deltaTime)
        {
            tempExplosionList.Clear();
            explosionCompletionQueue.Clear();

            int count = 0;
            foreach (var exp in activeExplosionSet)
            {
                if (exp == null || !exp.IsActive)
                {
                    explosionCompletionQueue.Add(exp);
                    continue;
                }

                explosionJobData[count] = new ExplosionJobData
                {
                    initialPosition = exp.InitialPosition,
                    velocity = exp.Velocity,
                    speed = exp.Speed,
                    elapsed = exp.Elapsed,
                    maxDuration = exp.MaxDuration
                };
                tempExplosionList.Add(exp);
                count++;
            }

            if (count == 0)
            {
                // Still process completion queue to clean up null/inactive entries
                ProcessExplosionCompletions();
                return;
            }

            var job = new UpdateExplosionsJob
            {
                data = explosionJobData,
                deltaTime = deltaTime
            };

            var handle = job.Schedule(count, BATCH_SIZE);
            handle.Complete();

            // Apply results — tempExplosionList[i] is aligned 1:1 with explosionJobData[i]
            for (int i = 0; i < count; i++)
            {
                var data = explosionJobData[i];
                var exp = tempExplosionList[i];
                if (exp == null) continue;

                exp.Elapsed = data.elapsed;

                // Update position
                float3 newPos = data.currentPosition;
                if (!math.any(math.isnan(newPos)))
                    exp.transform.position = new Vector3(newPos.x, newPos.y, newPos.z);

                // Update shader properties (read-modify-write to preserve team colors)
                var renderer = exp.Renderer;
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(sharedMPB);
                    sharedMPB.SetFloat(ExplosionAmountID, data.explosionAmount);
                    sharedMPB.SetFloat(OpacityID, data.opacity);
                    renderer.SetPropertyBlock(sharedMPB);
                }

                if (data.elapsed >= data.maxDuration)
                {
                    explosionCompletionQueue.Add(exp);
                }
            }

            ProcessExplosionCompletions();
        }

        private void ProcessExplosionCompletions()
        {
            // O(1) per removal with HashSet (was O(n) with List)
            for (int i = 0; i < explosionCompletionQueue.Count; i++)
            {
                var exp = explosionCompletionQueue[i];
                activeExplosionSet.Remove(exp);
                if (exp != null && exp.IsActive)
                    exp.OnEffectComplete();
            }
        }

        #endregion

        #region Implosion Processing

        private void ProcessImplosions(float deltaTime)
        {
            tempImplosionList.Clear();
            implosionCompletionQueue.Clear();

            int count = 0;
            foreach (var imp in activeImplosionSet)
            {
                if (imp == null || !imp.IsActive)
                {
                    implosionCompletionQueue.Add(imp);
                    continue;
                }

                implosionJobData[count] = new ImplosionJobData
                {
                    targetPosition = imp.TargetPosition,
                    elapsed = imp.Elapsed,
                    maxDuration = imp.Duration,
                    progress = imp.Progress,
                    isGrowing = imp.IsGrowing ? 1 : 0,
                    growDelayRemaining = imp.GrowDelayRemaining
                };
                tempImplosionList.Add(imp);
                count++;
            }

            if (count == 0)
            {
                ProcessImplosionCompletions();
                return;
            }

            var job = new UpdateImplosionsJob
            {
                data = implosionJobData,
                deltaTime = deltaTime
            };

            var handle = job.Schedule(count, BATCH_SIZE);
            handle.Complete();

            // Apply results — tempImplosionList[i] is aligned 1:1 with implosionJobData[i]
            for (int i = 0; i < count; i++)
            {
                var data = implosionJobData[i];
                var imp = tempImplosionList[i];
                if (imp == null) continue;

                imp.Elapsed = data.elapsed;
                imp.Progress = data.progress;
                imp.GrowDelayRemaining = data.growDelayRemaining;

                // Update shader properties (read-modify-write to preserve team colors)
                var renderer = imp.Renderer;
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(sharedMPB);
                    sharedMPB.SetFloat(ImplosionProgressID, data.progress);
                    sharedMPB.SetVector(ConvergencePointID,
                        new Vector4(data.targetPosition.x, data.targetPosition.y, data.targetPosition.z, 0));
                    renderer.SetPropertyBlock(sharedMPB);
                }

                if (data.isComplete == 1)
                {
                    implosionCompletionQueue.Add(imp);
                }
            }

            ProcessImplosionCompletions();
        }

        private void ProcessImplosionCompletions()
        {
            // O(1) per removal with HashSet (was O(n) with List)
            for (int i = 0; i < implosionCompletionQueue.Count; i++)
            {
                var imp = implosionCompletionQueue[i];
                activeImplosionSet.Remove(imp);
                if (imp != null && imp.IsActive)
                    imp.OnEffectComplete();
            }
        }

        #endregion

        #region Cleanup

        private void OnDisable()
        {
            activeExplosionSet.Clear();
            activeImplosionSet.Clear();
        }

        private void OnDestroy()
        {
            _instanceDestroyed = true;
            if (explosionJobData.IsCreated) explosionJobData.Dispose();
            if (implosionJobData.IsCreated) implosionJobData.Dispose();
            activeExplosionSet.Clear();
            activeImplosionSet.Clear();
            tempExplosionList.Clear();
            tempImplosionList.Clear();
        }

        #endregion
    }

    #region Job Data Structs

    public struct ExplosionJobData
    {
        public float3 initialPosition;
        public float3 velocity;
        public float speed;
        public float elapsed;
        public float maxDuration;
        // Computed outputs
        public float3 currentPosition;
        public float explosionAmount;
        public float opacity;
    }

    public struct ImplosionJobData
    {
        public float3 targetPosition;
        public float elapsed;
        public float maxDuration;
        public float progress;
        public int isGrowing;
        public float growDelayRemaining;
        // Computed output
        public int isComplete;
    }

    #endregion

    #region Burst Jobs

    [Unity.Burst.BurstCompile]
    public struct UpdateExplosionsJob : IJobParallelFor
    {
        public NativeArray<ExplosionJobData> data;
        [ReadOnly] public float deltaTime;

        public void Execute(int i)
        {
            var item = data[i];
            item.elapsed += deltaTime;
            item.currentPosition = item.initialPosition + item.elapsed * item.velocity;
            item.explosionAmount = item.speed * item.elapsed;
            item.opacity = 1f - (item.elapsed / item.maxDuration);
            data[i] = item;
        }
    }

    [Unity.Burst.BurstCompile]
    public struct UpdateImplosionsJob : IJobParallelFor
    {
        public NativeArray<ImplosionJobData> data;
        [ReadOnly] public float deltaTime;

        public void Execute(int i)
        {
            var item = data[i];

            // Handle grow delay — don't start animation until delay expires
            if (item.isGrowing == 1 && item.growDelayRemaining > 0f)
            {
                item.growDelayRemaining -= deltaTime;
                data[i] = item;
                return;
            }

            item.elapsed += deltaTime;
            float t = math.clamp(item.elapsed / item.maxDuration, 0f, 1f);

            if (item.isGrowing == 1)
                item.progress = 1f - t; // Growing: 1 -> 0
            else
                item.progress = t; // Imploding: 0 -> 1

            item.isComplete = item.elapsed >= item.maxDuration ? 1 : 0;
            data[i] = item;
        }
    }

    #endregion
}
