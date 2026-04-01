using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    public class PrismScaleManager : AdaptiveAnimationManager<PrismScaleManager, PrismScaleAnimator, ScaleAnimationData>
    {
        // This is a squared distance threshold check (since we use lengthsq)
        private const float COMPLETION_THRESHOLD_SQR = 0.01f;

        private readonly List<(PrismScaleAnimator block, Vector3 scale)> completionQueue =
            new List<(PrismScaleAnimator, Vector3)>(32);

        // ✅ This list is the critical fix: animators aligned 1:1 with animationData indices.
        private readonly List<PrismScaleAnimator> scalingAnimators =
            new List<PrismScaleAnimator>(256);

        protected override bool IsAnimatorActive(PrismScaleAnimator animator) => animator.IsScaling;
        protected override bool IsAnimatorValid(PrismScaleAnimator animator) => animator != null && animator.enabled;

        internal void OnBlockStartScaling(PrismScaleAnimator prism) => OnAnimatorStart(prism);
        internal void OnBlockStopScaling(PrismScaleAnimator prism) => OnAnimatorStop(prism);

        protected override void ProcessAnimationFrame(float deltaTime)
        {
            // Refresh stable list
            activeAnimatorsList.Clear();
            activeAnimatorsList.AddRange(activeAnimators);

            scalingAnimators.Clear();
            completionQueue.Clear();

            int scalingCount = 0;

            // Build contiguous job input + aligned animator list
            for (int i = 0; i < activeAnimatorsList.Count; i++)
            {
                var block = activeAnimatorsList[i];
                if (block == null || !block.enabled || !block.IsScaling) continue;

                var targetScale = Vector3.Min(
                    Vector3.Max(block.TargetScale, block.MinScale),
                    block.MaxScale
                );

                // Make sure our NativeArray is large enough (AdaptiveAnimationManager usually allocs it)
                animationData[scalingCount] = new ScaleAnimationData
                {
                    currentScale = block.transform.localScale,
                    targetScale = targetScale,
                    growthRate = block.GrowthRate
                };

                scalingAnimators.Add(block);
                scalingCount++;
            }

            if (scalingCount == 0)
                return;

            var job = new UpdateScalesJob
            {
                data = animationData,
                deltaTime = deltaTime,
                completionThresholdSqr = COMPLETION_THRESHOLD_SQR
            };

            var handle = job.Schedule(scalingCount, BATCH_SIZE);
            handle.Complete();

            // Apply results to the correct block using scalingAnimators[i]
            for (int i = 0; i < scalingCount; i++)
            {
                var data = animationData[i];
                var block = scalingAnimators[i];

                if (block == null || !block.enabled)
                    continue;

                var sqrDistance = math.lengthsq((float3)(data.targetScale - data.currentScale));

                if (sqrDistance <= COMPLETION_THRESHOLD_SQR)
                {
                    completionQueue.Add((block, data.targetScale));
                }
                else
                {
                    block.transform.localScale = data.currentScale;
                }
            }

            // Process completions
            for (int i = 0; i < completionQueue.Count; i++)
            {
                var (block, targetScale) = completionQueue[i];
                if (block == null || !block.enabled) continue;

                // Hit target exactly
                block.transform.localScale = targetScale;

                // Stop scaling (may call back into manager depending on your base class)
                block.IsScaling = false;

                // Ensure this animator is not left in the active set
                activeAnimators.Remove(block);

                block.ExecuteOnScaleComplete();
            }

            // Cleanup: remove any animators that are no longer scaling
            foreach (var animator in activeAnimatorsList)
            {
                if (animator == null || !animator.IsScaling)
                    activeAnimators.Remove(animator);
            }
        }

        protected override void CleanupResources()
        {
            base.CleanupResources();
            completionQueue.Clear();
            scalingAnimators.Clear();
        }
    }

    public struct ScaleAnimationData
    {
        public Vector3 currentScale;
        public Vector3 targetScale;
        public float growthRate;
    }

    [Unity.Burst.BurstCompile]
    public struct UpdateScalesJob : IJobParallelFor
    {
        public NativeArray<ScaleAnimationData> data;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float completionThresholdSqr;

        public void Execute(int i)
        {
            var item = data[i];

            var diff = (float3)(item.targetScale - item.currentScale);
            var sqrDistance = math.lengthsq(diff);

            if (sqrDistance > completionThresholdSqr)
            {
                // You can tune these, but keeping your original clamp behavior:
                var lerpSpeed = math.clamp(item.growthRate * deltaTime, 0.05f, 0.1f);
                item.currentScale = math.lerp((float3)item.currentScale, (float3)item.targetScale, lerpSpeed);
            }
            else
            {
                item.currentScale = item.targetScale;
            }

            data[i] = item;
        }
    }
}
