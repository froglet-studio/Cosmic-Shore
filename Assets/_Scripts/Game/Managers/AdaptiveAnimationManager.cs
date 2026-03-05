using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using CosmicShore.Utilities;



namespace CosmicShore.Core
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
        protected bool activeListDirty = true;
        protected NativeArray<TAnimationData> animationData;

        // Performance monitoring
        private readonly Queue<float> frameTimeHistory = new Queue<float>();
        private float lastIntervalUpdateTime;
        private float accumulatedTime = 0f;
        private int currentFrameInterval = BASE_FRAME_INTERVAL;

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
                if (activeAnimators.Add(animator))
                    activeListDirty = true;
                EnsureCapacity();
            }
        }

        public virtual void UnregisterAnimator(TAnimator animator)
        {
            if (animator == null) return;
            registeredAnimators.Remove(animator);
            if (activeAnimators.Remove(animator))
                activeListDirty = true;
        }

        protected virtual void OnAnimatorStart(TAnimator animator)
        {
            if (animator == null || !IsAnimatorValid(animator) || !registeredAnimators.Contains(animator)) return;
            if (activeAnimators.Add(animator))
                activeListDirty = true;
            EnsureCapacity();
        }

        protected virtual void OnAnimatorStop(TAnimator animator)
        {
            if (animator == null) return;
            if (activeAnimators.Remove(animator))
                activeListDirty = true;

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

            if (Time.realtimeSinceStartup - lastIntervalUpdateTime < 0.25f)
                return;

            lastIntervalUpdateTime = Time.realtimeSinceStartup;

            frameTimeHistory.Enqueue(Time.deltaTime);
            if (frameTimeHistory.Count > FRAME_TIME_SAMPLE_SIZE)
                frameTimeHistory.Dequeue();

            float avgFrameTime = 0f;
            foreach (float frameTime in frameTimeHistory)
                avgFrameTime += frameTime;
            avgFrameTime /= frameTimeHistory.Count;

            // Scale based on both capacity and frame time pressure
            float capacityFactor = capacity / 50f;
            float performancePressure = avgFrameTime / TARGET_FRAME_TIME;
            performancePressure = Mathf.Pow(performancePressure, 1.5f);

            float baseInterval = Mathf.Max(BASE_FRAME_INTERVAL, capacityFactor);
            float scaleFactor = baseInterval * (1f + performancePressure);

            int newInterval = Mathf.Clamp(
                Mathf.RoundToInt(scaleFactor),
                Mathf.Max(BASE_FRAME_INTERVAL, Mathf.FloorToInt(capacityFactor)),
                MAX_FRAME_INTERVAL
            );

            // Respond quickly when under pressure (jump up fast, ease down slowly)
            if (newInterval > currentFrameInterval)
            {
                // Under pressure — jump up aggressively (halve the gap each tick)
                currentFrameInterval += Mathf.Max(1, (newInterval - currentFrameInterval + 1) / 2);
                currentFrameInterval = Mathf.Min(currentFrameInterval, MAX_FRAME_INTERVAL);
            }
            else if (newInterval < currentFrameInterval)
            {
                currentFrameInterval -= 1; // ease down slowly
            }
        }

        protected virtual void Update()
        {
            // Early exit if nothing is animating
            if (activeAnimators.Count == 0)
            {
                accumulatedTime = 0f;
                return;
            }

            // Periodically re-evaluate throttle based on actual frame performance
            UpdateFrameInterval(activeAnimators.Count);

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

        protected void RefreshActiveListIfDirty()
        {
            if (!activeListDirty) return;
            activeAnimatorsList.Clear();
            activeAnimatorsList.AddRange(activeAnimators);
            activeListDirty = false;
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

        protected virtual void CleanupResources()
        {
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