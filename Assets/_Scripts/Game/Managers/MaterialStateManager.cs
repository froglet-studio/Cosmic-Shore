using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    public class MaterialStateManager : AdaptiveAnimationManager<MaterialStateManager, MaterialPropertyAnimator, MaterialAnimationData>
    {
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
        }

        protected override bool IsAnimatorActive(MaterialPropertyAnimator animator) =>
            animator.IsAnimating;

        protected override bool IsAnimatorValid(MaterialPropertyAnimator animator) =>
            animator.enabled && animator.MeshRenderer != null;

        internal void OnAnimatorStartAnimating(MaterialPropertyAnimator animator) =>
            OnAnimatorStart(animator);

        internal void OnAnimatorStopAnimating(MaterialPropertyAnimator animator) =>
            OnAnimatorStop(animator);

        protected override void ProcessAnimationFrame(float deltaTime)
        {
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
                    animatorIndex = animatingCount
                };
                animatingCount++;
            }

            if (animatingCount == 0) return;

            propertyUpdateQueue.Clear();

            var job = new UpdateAnimationsJob
            {
                data = animationData,
                deltaTime = deltaTime
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

        protected override void CleanupResources()
        {
            base.CleanupResources();
            propertyUpdateQueue.Clear();
        }

        private static float4 ToFloat4(Color color) => new float4(color.r, color.g, color.b, color.a);
        private static Color ToColor(float4 f4) => new Color(f4.x, f4.y, f4.z, f4.w);
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
        public int animatorIndex;
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