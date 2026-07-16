using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;

namespace CosmicShore.Gameplay
{
    public abstract class AdaptiveAnimationManager<TManager, TAnimator, TAnimationData> : Singleton<TManager>
        where TManager : AdaptiveAnimationManager<TManager, TAnimator, TAnimationData>
        where TAnimator : MonoBehaviour
        where TAnimationData : struct
    {
        protected const int BATCH_SIZE = 128;

        // Performance tuning constants
        protected const float TARGET_FRAME_TIME = 1f / 60f;
        protected const float MAX_FRAME_TIME = 1f / 30f;
        protected const int BASE_FRAME_INTERVAL = 1;
        protected const int MAX_FRAME_INTERVAL = 12;
        protected const int FRAME_TIME_SAMPLE_SIZE = 60;

        // Animation tracking
        protected readonly HashSet<TAnimator> registeredAnimators = new HashSet<TAnimator>();
        protected readonly HashSet<TAnimator> activeAnimators = new HashSet<TAnimator>();
        protected readonly List<TAnimator> activeAnimatorsList = new List<TAnimator>();
        protected NativeArray<TAnimationData> animationData;

        /// <summary>Number of live (registered, enabled) animators - read-only, allocation-free. Used by the performance benchmark to correlate frame cost with object load.</summary>
        public int RegisteredAnimatorCount => registeredAnimators.Count;

        /// <summary>Number of animators currently mid-animation - read-only, allocation-free.</summary>
        public int ActiveAnimatorCount => activeAnimators.Count;

        // Performance monitoring
        private readonly Queue<float> frameTimeHistory = new Queue<float>();
        private float lastIntervalUpdateTime;
        private float accumulatedTime = 0f;
        private int currentFrameInterval = BASE_FRAME_INTERVAL;

        // DiagnosticsHUD live load telemetry — one profiler row aggregates every
        // AdaptiveAnimationManager instance (the generic type collapses), so the
        // HUD stat is what attributes cost to a manager and an animator count.
        // Strings rebuild only when the count changes (no per-frame GC).
        static readonly string s_statLabel = typeof(TManager).Name;
        private int _lastReportedActive = -1;

        public override void Awake()
        {
            base.Awake();
            InitializeAnimationData(32);
        }

        protected virtual void InitializeAnimationData(int capacity)
        {
            animationData = new NativeArray<TAnimationData>(capacity, Allocator.Persistent);
        }

        public virtual void RegisterAnimator(TAnimator animator)
        {
            if (animator == null) return;
            registeredAnimators.Add(animator);

            if (IsAnimatorActive(animator))
            {
                activeAnimators.Add(animator);
                EnsureCapacity();
            }
        }

        public virtual void UnregisterAnimator(TAnimator animator)
        {
            if (animator == null) return;
            registeredAnimators.Remove(animator);
            activeAnimators.Remove(animator);
        }

        protected virtual void OnAnimatorStart(TAnimator animator)
        {
            if (animator == null || !IsAnimatorValid(animator) || !registeredAnimators.Contains(animator)) return;
            activeAnimators.Add(animator);
            EnsureCapacity();
        }

        protected virtual void OnAnimatorStop(TAnimator animator)
        {
            if (animator == null) return;
            activeAnimators.Remove(animator);

            // If this was the last active animator, reset our monitoring state
            if (activeAnimators.Count == 0)
            {
                frameTimeHistory.Clear();
                currentFrameInterval = BASE_FRAME_INTERVAL;
                accumulatedTime = 0f;
                lastIntervalUpdateTime = 0f;
            }
        }

        protected virtual void EnsureCapacity()
        {
            if (activeAnimators.Count > animationData.Length)
            {
                var newSize = Mathf.Max(32, Mathf.NextPowerOfTwo(activeAnimators.Count));
                var newArray = new NativeArray<TAnimationData>(newSize, Allocator.Persistent);

                if (animationData.IsCreated)
                {
                    if (animationData.Length > 0)
                    {
                        NativeArray<TAnimationData>.Copy(animationData, newArray, animationData.Length);
                    }
                    animationData.Dispose();
                }

                animationData = newArray;
                UpdateFrameInterval(newSize);
            }
        }

        protected virtual void UpdateFrameInterval(int capacity)
        {
            // Don't waste cycles monitoring if nothing is animating
            if (activeAnimators.Count == 0)
            {
                frameTimeHistory.Clear();
                currentFrameInterval = BASE_FRAME_INTERVAL;
                accumulatedTime = 0f;
                return;
            }

            if (Time.realtimeSinceStartup - lastIntervalUpdateTime < 0.5f)
                return;

            lastIntervalUpdateTime = Time.realtimeSinceStartup;

            frameTimeHistory.Enqueue(Time.deltaTime);
            if (frameTimeHistory.Count > FRAME_TIME_SAMPLE_SIZE)
                frameTimeHistory.Dequeue();

            float avgFrameTime = 0f;
            float maxFrameTime = 0f;
            foreach (float frameTime in frameTimeHistory)
            {
                avgFrameTime += frameTime;
                maxFrameTime = Mathf.Max(maxFrameTime, frameTime);
            }
            avgFrameTime /= frameTimeHistory.Count;

            // More aggressive capacity scaling
            float capacityFactor = capacity / 50f; // Start scaling earlier and more aggressively

            // Scale based on both capacity and frame time pressure
            float performancePressure = avgFrameTime / TARGET_FRAME_TIME;
            performancePressure = Mathf.Pow(performancePressure, 1.5f); // Exponential scaling for performance pressure

            // Higher baseline interval for large numbers of objects
            float baseInterval = Mathf.Max(BASE_FRAME_INTERVAL, capacityFactor);

            // Combine factors multiplicatively instead of weighted average
            float scaleFactor = baseInterval * (1f + performancePressure);

            // Calculate new interval with smoother clamping
            int newInterval = Mathf.Clamp(
                Mathf.RoundToInt(scaleFactor),
                Mathf.Max(BASE_FRAME_INTERVAL, Mathf.FloorToInt(capacityFactor)),
                MAX_FRAME_INTERVAL
            );

            // Smooth transition to new interval
            if (newInterval != currentFrameInterval)
            {
                currentFrameInterval += (newInterval > currentFrameInterval) ? 1 : -1;
            }
        }

        protected virtual void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Guarded here (not just inside SetStat) so release builds never pay
            // the string interpolation.
            if (activeAnimators.Count != _lastReportedActive)
            {
                _lastReportedActive = activeAnimators.Count;
                DiagnosticsHUD.SetStat("Animators", s_statLabel,
                    $"{_lastReportedActive} / {registeredAnimators.Count} reg");
            }
#endif

            // Early exit if nothing is animating
            if (activeAnimators.Count == 0)
            {
                // Ensure we're not accumulating time when idle
                accumulatedTime = 0f;
                return;
            }

            // Accumulate time with protection against spikes
            float deltaTime = Mathf.Min(Time.deltaTime, MAX_FRAME_TIME);
            accumulatedTime += deltaTime;

            // Check if enough time has accumulated for an update
            float updateInterval = Time.fixedDeltaTime * currentFrameInterval;
            if (accumulatedTime < updateInterval) return;

            // Calculate how many updates we should perform
            int updateSteps = Mathf.FloorToInt(accumulatedTime / updateInterval);
            float effectiveDeltaTime = updateInterval; // Use fixed time step

            // Perform update and consume accumulated time
            ProcessAnimationFrame(effectiveDeltaTime);
            accumulatedTime -= updateInterval * updateSteps;
        }

        protected abstract void ProcessAnimationFrame(float deltaTime);
        protected abstract bool IsAnimatorActive(TAnimator animator);
        protected abstract bool IsAnimatorValid(TAnimator animator);

        protected virtual void OnDisable()
        {
            CleanupResources();
        }

        protected virtual void OnDestroy()
        {
            CleanupResources();
        }

        private bool _disposed;

        protected virtual void CleanupResources()
        {
            if (_disposed) return;
            _disposed = true;

            if (animationData.IsCreated)
            {
                animationData.Dispose();
            }
            registeredAnimators.Clear();
            activeAnimators.Clear();
            activeAnimatorsList.Clear();
        }
    }
}