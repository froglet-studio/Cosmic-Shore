using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using CosmicShore.Utility.Singleton;

namespace CosmicShore.Core
{
    public abstract class AdaptiveAnimationManager<TManager, TAnimator, TAnimationData> : SingletonPersistent<TManager>
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

        // Performance monitoring
        private readonly Queue<float> frameTimeHistory = new Queue<float>();
        private float lastIntervalUpdateTime;
        private int currentFrameCount = 0;
        private int currentFrameInterval = BASE_FRAME_INTERVAL;

        public override void Awake()
        {
            base.Awake();
            InitializeAnimationData(32); // Start with default capacity
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

            float capacityFactor = Mathf.Log(1 + capacity / 100f, 2);
            float performanceFactor = avgFrameTime / TARGET_FRAME_TIME;
            float weightedFactor = (capacityFactor * 0.7f) + (performanceFactor * 0.3f);

            int newInterval = Mathf.Clamp(
                Mathf.RoundToInt(weightedFactor * BASE_FRAME_INTERVAL),
                BASE_FRAME_INTERVAL,
                MAX_FRAME_INTERVAL
            );

            if (newInterval != currentFrameInterval)
            {
                currentFrameInterval += (newInterval > currentFrameInterval) ? 1 : -1;

                if (Mathf.Abs(newInterval - currentFrameInterval) > 1)
                {
                    Debug.Log($"Animation update interval adjusted: {currentFrameInterval} " +
                            $"(Capacity: {capacity}, Avg Frame: {avgFrameTime * 1000:F1}ms, " +
                            $"Max Frame: {maxFrameTime * 1000:F1}ms)");
                }
            }
        }

        protected virtual void Update()
        {
            if (Time.deltaTime == 0 || activeAnimators.Count == 0) return;

            currentFrameCount++;
            if (currentFrameCount % currentFrameInterval != 0) return;
            currentFrameCount = 0;

            float effectiveDeltaTime = Time.deltaTime * currentFrameInterval;

            ProcessAnimationFrame(effectiveDeltaTime);
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
