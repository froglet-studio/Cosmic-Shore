using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using CosmicShore.Utility.Singleton;

namespace CosmicShore.Core
{
    public class MaterialStateManager : SingletonPersistent<MaterialStateManager>
    {
        private const int BATCH_SIZE = 128;
        
        // Track all registered animators
        private readonly HashSet<MaterialPropertyAnimator> registeredAnimators = new HashSet<MaterialPropertyAnimator>();
        // Track only actively animating ones
        private readonly HashSet<MaterialPropertyAnimator> activeAnimators = new HashSet<MaterialPropertyAnimator>();
        // List to maintain stable indices for the job system
        private readonly List<MaterialPropertyAnimator> activeAnimatorsList = new List<MaterialPropertyAnimator>();
        private NativeArray<MaterialAnimationData> animationData;
        private readonly List<(MaterialPropertyAnimator animator, float4 brightColor, float4 darkColor, float3 spread)> propertyUpdateQueue = 
            new List<(MaterialPropertyAnimator, float4, float4, float3)>(32);

        private MaterialPropertyBlock sharedPropertyBlock;

        private const string BRIGHT_COLOR_PROP = "_BrightColor";
        private const string DARK_COLOR_PROP = "_DarkColor";
        private const string SPREAD_PROP = "_Spread";

        public override void Awake()
        {
            base.Awake();
            sharedPropertyBlock = new MaterialPropertyBlock();
            animationData = new NativeArray<MaterialAnimationData>(32, Allocator.Persistent);
        }

        // Original registration methods for compatibility
        public void RegisterAnimator(MaterialPropertyAnimator animator)
        {
            if (animator == null) return;
            registeredAnimators.Add(animator);
            
            // If animator is already animating, add to active set
            if (animator.IsAnimating)
            {
                activeAnimators.Add(animator);
                EnsureCapacity();
            }
        }

        public void UnregisterAnimator(MaterialPropertyAnimator animator)
        {
            if (animator == null) return;
            registeredAnimators.Remove(animator);
            activeAnimators.Remove(animator);
        }

        // Internal methods to manage active animation state
        internal void OnAnimatorStartAnimating(MaterialPropertyAnimator animator)
        {
            if (animator == null || !animator.enabled || !registeredAnimators.Contains(animator)) return;
            activeAnimators.Add(animator);
            EnsureCapacity();
        }

        internal void OnAnimatorStopAnimating(MaterialPropertyAnimator animator)
        {
            if (animator == null) return;
            activeAnimators.Remove(animator);
        }

        private void EnsureCapacity()
        {
            if (activeAnimators.Count > animationData.Length)
            {
                var newSize = Mathf.Max(32, Mathf.NextPowerOfTwo(activeAnimators.Count));
                var newArray = new NativeArray<MaterialAnimationData>(newSize, Allocator.Persistent);
                if (animationData.IsCreated)
                {
                    if (animationData.Length > 0)
                    {
                        NativeArray<MaterialAnimationData>.Copy(animationData, newArray, animationData.Length);
                    }
                    animationData.Dispose();
                }
                animationData = newArray;
            }
        }

        private void Update()
        {
            if (Time.deltaTime == 0 || activeAnimators.Count == 0) return;

            // Update our stable index list
            activeAnimatorsList.Clear();
            activeAnimatorsList.AddRange(activeAnimators);

            int animatingCount = 0;
            foreach (var animator in activeAnimatorsList)
            {
                if (animator == null || !animator.enabled || !animator.IsAnimating || animator.MeshRenderer == null) continue;

                animationData[animatingCount] = new MaterialAnimationData
                {
                    progress = animator.AnimationProgress,
                    duration = animator.Duration,
                    startBrightColor = ToFloat4(animator.StartBrightColor),
                    targetBrightColor = ToFloat4(animator.TargetBrightColor),
                    startDarkColor = ToFloat4(animator.StartDarkColor),
                    targetDarkColor = ToFloat4(animator.TargetDarkColor),
                    startSpread = animator.StartSpread,
                    targetSpread = animator.TargetSpread,
                    animatorIndex = animatingCount // Store index instead of reference
                };
                animatingCount++;
            }

            if (animatingCount == 0) return;

            propertyUpdateQueue.Clear();

            var job = new UpdateAnimationsJob
            {
                data = animationData,
                deltaTime = Time.deltaTime
            };

            var handle = job.Schedule(animatingCount, BATCH_SIZE);
            handle.Complete();

            // Process results and queue property updates
            for (int i = 0; i < animatingCount; i++)
            {
                var data = animationData[i];
                var animator = activeAnimatorsList[data.animatorIndex];
                if (animator != null && animator.enabled && animator.MeshRenderer != null)
                {
                    animator.AnimationProgress = data.progress;

                    float t = math.smoothstep(0f, 1f, data.progress);
                    var brightColor = math.lerp(data.startBrightColor, data.targetBrightColor, t);
                    var darkColor = math.lerp(data.startDarkColor, data.targetDarkColor, t);
                    var spread = math.lerp(data.startSpread, data.targetSpread, t);

                    propertyUpdateQueue.Add((animator, brightColor, darkColor, spread));

                    if (data.progress >= 0.999f)
                    {
                        animator.IsAnimating = false;
                        activeAnimators.Remove(animator);
                        
                        if (animator.OnAnimationComplete != null)
                        {
                            try
                            {
                                animator.OnAnimationComplete.Invoke();
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError($"Error in animation completion callback: {e.Message}");
                            }
                            animator.OnAnimationComplete = null;
                        }
                    }
                }
            }

            // Batch apply property updates
            if (propertyUpdateQueue.Count > 0)
            {
                if (sharedPropertyBlock == null)
                {
                    sharedPropertyBlock = new MaterialPropertyBlock();
                }

                foreach (var (animator, brightColor, darkColor, spread) in propertyUpdateQueue)
                {
                    sharedPropertyBlock.SetColor(BRIGHT_COLOR_PROP, ToColor(brightColor));
                    sharedPropertyBlock.SetColor(DARK_COLOR_PROP, ToColor(darkColor));
                    sharedPropertyBlock.SetVector(SPREAD_PROP, new Vector4(spread.x, spread.y, spread.z, 0));
                    animator.MeshRenderer.SetPropertyBlock(sharedPropertyBlock);
                }
            }
        }

        private static float4 ToFloat4(Color color) => new float4(color.r, color.g, color.b, color.a);
        private static Color ToColor(float4 f4) => new Color(f4.x, f4.y, f4.z, f4.w);

        private void OnDisable()
        {
            CleanupResources();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (animationData.IsCreated)
            {
                animationData.Dispose();
            }
            registeredAnimators.Clear();
            activeAnimators.Clear();
            activeAnimatorsList.Clear();
            propertyUpdateQueue.Clear();
        }
    }

    public struct MaterialAnimationData
    {
        public float4 startBrightColor;
        public float4 targetBrightColor;
        public float4 startDarkColor;
        public float4 targetDarkColor;
        public float3 startSpread;
        public float3 targetSpread;
        public float progress;
        public float duration;
        public int animatorIndex; // Store index instead of reference
    }

    [Unity.Burst.BurstCompile]
    public struct UpdateAnimationsJob : IJobParallelFor
    {
        public NativeArray<MaterialAnimationData> data;
        [ReadOnly] public float deltaTime;

        public void Execute(int i)
        {
            var item = data[i];
            item.progress = math.min(1f, item.progress + deltaTime / item.duration);
            data[i] = item;
        }
    }
}
