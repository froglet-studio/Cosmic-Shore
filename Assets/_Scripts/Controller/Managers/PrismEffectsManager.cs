using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Centralized Jobs-based manager for prism explosion and implosion VFX.
    /// Replaces per-instance UniTask async loops with batched Burst-compiled updates.
    /// Follows the same pattern as MaterialStateManager and PrismScaleManager.
    /// </summary>
    public class PrismEffectsManager : Singleton<PrismEffectsManager>
    {
        private static bool _isQuitting;

        private void OnApplicationQuit() => _isQuitting = true;

        /// <summary>
        /// Ensures a PrismEffectsManager instance exists. If none was placed in the scene,
        /// creates one automatically so explosion/implosion effects don't silently fail.
        /// </summary>
        public static PrismEffectsManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            if (_isQuitting) return null;

            var go = new GameObject("[PrismEffectsManager]");
            go.AddComponent<PrismEffectsManager>();
            Debug.LogWarning("[PrismEffectsManager] No instance found in scene — auto-created. " +
                             "Consider adding one to the scene to avoid this overhead.");
            return Instance;
        }

        private const int BATCH_SIZE = 128;
        private const int INITIAL_CAPACITY = 64;

        // Hard ceiling on CONCURRENTLY animating VFX. The per-frame spawn cap
        // (PrismFactory, 64/frame) bounds new effects but NOT live ones: each
        // explosion lasts 5s and each implosion 2s, so a sustained fauna swarm-eat
        // accumulates thousands of active effects, and ProcessExplosions/Implosions'
        // per-effect property-block apply is O(active) — profiled at ~97ms/frame.
        // When full we recycle the OLDEST (longest-animating, hence nearly finished)
        // so every death still animates out (continuity law) and only the oldest is
        // truncated under extreme load — imperceptible in a frenzy of hundreds.
        // See PRISM_PERFORMANCE_AUDIT.md rec 5.
        private const int MAX_ACTIVE_EFFECTS = 256;

        // Explosion tracking
        private readonly List<PrismExplosion> activeExplosions = new(INITIAL_CAPACITY);
        private readonly List<PrismExplosion> tempExplosionList = new(INITIAL_CAPACITY);
        private readonly List<PrismExplosion> explosionCompletionQueue = new(32);
        private NativeArray<ExplosionJobData> explosionJobData;

        // Implosion tracking
        private readonly List<PrismImplosion> activeImplosions = new(INITIAL_CAPACITY);
        private readonly List<PrismImplosion> tempImplosionList = new(INITIAL_CAPACITY);
        private readonly List<PrismImplosion> implosionCompletionQueue = new(32);
        private NativeArray<ImplosionJobData> implosionJobData;

        /// <summary>Concurrently active explosion VFX — read-only, allocation-free. Used by the performance benchmark.</summary>
        public int ActiveExplosionCount => activeExplosions.Count;

        /// <summary>Concurrently active implosion VFX — read-only, allocation-free. Used by the performance benchmark.</summary>
        public int ActiveImplosionCount => activeImplosions.Count;

        // Shared property block for batched shader updates
        private MaterialPropertyBlock sharedMPB;

        // Shader property IDs (cached)
        private static readonly int ExplosionAmountID = Shader.PropertyToID("_ExplosionAmount");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
        private static readonly int ImplosionProgressID = Shader.PropertyToID("_State");
        private static readonly int ConvergencePointID = Shader.PropertyToID("_Location");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Diagnostics (Editor / Dev builds only)")]
        [Tooltip("Seconds between the leaked-VFX safety audit. The audit uses FindObjectsByType (a full-scene scan), so keep this infrequent. Set <= 0 to disable.")]
        [SerializeField] private float zombieAuditIntervalSeconds = 5f;
        private float _nextZombieAuditTime;
#endif

        public override void Awake()
        {
            base.Awake();
            sharedMPB = new MaterialPropertyBlock();
            explosionJobData = new NativeArray<ExplosionJobData>(INITIAL_CAPACITY, Allocator.Persistent);
            implosionJobData = new NativeArray<ImplosionJobData>(INITIAL_CAPACITY, Allocator.Persistent);
        }

        #region Registration

        public void RegisterExplosion(PrismExplosion explosion)
        {
            if (explosion == null || activeExplosions.Contains(explosion)) return;
            // Bound concurrent active VFX — recycle the oldest (front of the list,
            // longest-running) to make room so the per-frame apply stays O(cap).
            while (activeExplosions.Count >= MAX_ACTIVE_EFFECTS)
            {
                var oldest = activeExplosions[0];
                activeExplosions.RemoveAt(0);
                if (oldest != null) oldest.OnEffectComplete(); // already removed — Unregister is a no-op
            }
            activeExplosions.Add(explosion);
            EnsureExplosionCapacity();
        }

        public void UnregisterExplosion(PrismExplosion explosion)
        {
            activeExplosions.Remove(explosion);
        }

        public void RegisterImplosion(PrismImplosion implosion)
        {
            if (implosion == null || activeImplosions.Contains(implosion)) return;
            // Bound concurrent active VFX — recycle the oldest to keep apply O(cap).
            while (activeImplosions.Count >= MAX_ACTIVE_EFFECTS)
            {
                var oldest = activeImplosions[0];
                activeImplosions.RemoveAt(0);
                if (oldest != null) oldest.OnEffectComplete(); // already removed — Unregister is a no-op
            }
            activeImplosions.Add(implosion);
            EnsureImplosionCapacity();
        }

        public void UnregisterImplosion(PrismImplosion implosion)
        {
            activeImplosions.Remove(implosion);
        }

        #endregion

        #region Capacity

        private void EnsureExplosionCapacity()
        {
            if (activeExplosions.Count <= explosionJobData.Length) return;
            var newSize = Mathf.NextPowerOfTwo(activeExplosions.Count);
            var newArray = new NativeArray<ExplosionJobData>(newSize, Allocator.Persistent);
            if (explosionJobData.IsCreated) explosionJobData.Dispose();
            explosionJobData = newArray;
        }

        private void EnsureImplosionCapacity()
        {
            if (activeImplosions.Count <= implosionJobData.Length) return;
            var newSize = Mathf.NextPowerOfTwo(activeImplosions.Count);
            var newArray = new NativeArray<ImplosionJobData>(newSize, Allocator.Persistent);
            if (implosionJobData.IsCreated) implosionJobData.Dispose();
            implosionJobData = newArray;
        }

        #endregion

        private void Update()
        {
            float dt = Time.deltaTime;
            if (activeExplosions.Count > 0) ProcessExplosions(dt);
            if (activeImplosions.Count > 0) ProcessImplosions(dt);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Safety audit: detect explosion / implosion VFX with enabled renderers
            // that aren't actively managed. Catches "zombie" pool instances whose
            // OnReturnToPool callback chain failed to deactivate the GameObject.
            // Uses FindObjectsByType (a full-scene scan), so it runs on an infrequent
            // time-based throttle rather than every frame — running it once per second
            // showed up as a recurring spike in dev-build profiling.
            if (zombieAuditIntervalSeconds > 0f && Time.unscaledTime >= _nextZombieAuditTime)
            {
                _nextZombieAuditTime = Time.unscaledTime + zombieAuditIntervalSeconds;
                var allExplosions = FindObjectsByType<PrismExplosion>(FindObjectsSortMode.None);
                foreach (var exp in allExplosions)
                {
                    if (exp.Renderer != null && exp.Renderer.enabled && !exp.IsActive)
                        exp.Renderer.enabled = false;
                    // Entity-path zombies: companion entity left visible without
                    // an active animation driving it.
                    if (!exp.IsActive && exp.UsesEntityRenderPath)
                        CosmicShore.ECS.PrismRenderService.SetVisible(in exp.RenderHandle, false);
                }

                var allImplosions = FindObjectsByType<PrismImplosion>(FindObjectsSortMode.None);
                int activeGameObjects = 0;
                int zombies = 0;
                int healthy = 0;
                foreach (var imp in allImplosions)
                {
                    if (!imp) continue;
                    bool goActive = imp.gameObject.activeSelf;
                    if (!goActive) continue;

                    activeGameObjects++;
                    if (!imp.IsActive)
                    {
                        zombies++;
                        // Active GameObject + IsActive=false = orphaned by the pool
                        // callback chain. Force-deactivate immediately rather than
                        // waiting 4s for the per-instance watchdog.
                        imp.gameObject.SetActive(false);
                    }
                    else
                    {
                        healthy++;
                    }
                }

                // Periodic stats: lets us distinguish "many legitimate implosions
                // playing at once" (expected when the squirrel circles the crystal
                // and fauna swarm-eat its trail) from "leak accumulating zombies".
                // Only log when count is interesting (>0 zombies or >32 healthy)
                // so the console isn't spammy on quiet scenes.
                if (zombies > 0 || activeGameObjects > 32)
                {
                    CSDebug.Log($"[PrismEffectsManager] Active implosions: total={activeGameObjects} healthy={healthy} zombies={zombies} (manager-tracked={activeImplosions.Count})");
                }
            }
#endif
        }

        #region Explosion Processing

        private void ProcessExplosions(float deltaTime)
        {
            tempExplosionList.Clear();
            explosionCompletionQueue.Clear();

            int count = 0;
            for (int i = 0; i < activeExplosions.Count; i++)
            {
                var exp = activeExplosions[i];
                if (exp == null || !exp.IsActive) continue;

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

            if (count == 0) return;

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

                if (exp.UsesEntityRenderPath)
                {
                    // Instanced path: shader params + matrix sink into the
                    // companion entity. First frame also unhides it (same
                    // no-flash contract as the renderer-enable below).
                    exp.SyncRenderTransform();
                    CosmicShore.ECS.PrismRenderService.SetExplosionParams(
                        in exp.RenderHandle, data.velocity, data.explosionAmount, data.opacity);
                    exp.EnableVisual();
                }
                else
                {
                    // Legacy path: per-renderer MPB (read-modify-write to preserve team colors)
                    var renderer = exp.Renderer;
                    if (renderer != null)
                    {
                        renderer.GetPropertyBlock(sharedMPB);
                        sharedMPB.SetFloat(ExplosionAmountID, data.explosionAmount);
                        sharedMPB.SetFloat(OpacityID, data.opacity);
                        renderer.SetPropertyBlock(sharedMPB);

                        // Enable renderer on first animated frame — TriggerExplosion disables it
                        // to prevent a one-frame flash of the unanimated mesh.
                        if (!renderer.enabled)
                            renderer.enabled = true;
                    }
                }

                if (data.elapsed >= data.maxDuration)
                {
                    explosionCompletionQueue.Add(exp);
                }
            }

            // Process completions after iteration to avoid list mutation during traversal
            for (int i = 0; i < explosionCompletionQueue.Count; i++)
            {
                var exp = explosionCompletionQueue[i];
                activeExplosions.Remove(exp);
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
            for (int i = 0; i < activeImplosions.Count; i++)
            {
                var imp = activeImplosions[i];
                if (imp == null || !imp.IsActive) continue;

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

            if (count == 0) return;

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

                if (imp.UsesEntityRenderPath)
                {
                    // Instanced path: progress + convergence point sink into the
                    // companion entity's overrides.
                    CosmicShore.ECS.PrismRenderService.SetImplosionParams(
                        in imp.RenderHandle, data.progress, data.targetPosition);
                }
                else
                {
                    // Legacy path: per-renderer MPB (read-modify-write to preserve team colors)
                    var renderer = imp.Renderer;
                    if (renderer != null)
                    {
                        renderer.GetPropertyBlock(sharedMPB);
                        sharedMPB.SetFloat(ImplosionProgressID, data.progress);
                        sharedMPB.SetVector(ConvergencePointID,
                            new Vector4(data.targetPosition.x, data.targetPosition.y, data.targetPosition.z, 0));
                        renderer.SetPropertyBlock(sharedMPB);
                    }
                }

                if (data.isComplete == 1)
                {
                    implosionCompletionQueue.Add(imp);
                }
            }

            // Process completions after iteration
            for (int i = 0; i < implosionCompletionQueue.Count; i++)
            {
                var imp = implosionCompletionQueue[i];
                activeImplosions.Remove(imp);
                imp.OnEffectComplete();
            }
        }

        #endregion

        #region Cleanup

        private void OnDisable()
        {
            activeExplosions.Clear();
            activeImplosions.Clear();
        }

        private void OnDestroy()
        {
            if (explosionJobData.IsCreated) explosionJobData.Dispose();
            if (implosionJobData.IsCreated) implosionJobData.Dispose();
            activeExplosions.Clear();
            activeImplosions.Clear();
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
