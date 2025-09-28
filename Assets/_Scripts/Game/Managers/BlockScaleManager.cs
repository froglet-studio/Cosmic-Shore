using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Core
{
    public class BlockScaleManager : AdaptiveAnimationManager<BlockScaleManager, PrismScaleAnimator, ScaleAnimationData>
    {
        private const float COMPLETION_THRESHOLD = 0.01f;
        private readonly List<(PrismScaleAnimator block, Vector3 scale)> completionQueue =
            new List<(PrismScaleAnimator, Vector3)>(32);

        protected override bool IsAnimatorActive(PrismScaleAnimator animator) =>
            animator.IsScaling;

        protected override bool IsAnimatorValid(PrismScaleAnimator animator) =>
            animator.enabled;

        internal void OnBlockStartScaling(PrismScaleAnimator prism) =>
            OnAnimatorStart(prism);

        internal void OnBlockStopScaling(PrismScaleAnimator prism) =>
            OnAnimatorStop(prism);

        protected override void ProcessAnimationFrame(float deltaTime)
        {
            // Update our stable index list
            activeAnimatorsList.Clear();
            activeAnimatorsList.AddRange(activeAnimators);

            int scalingCount = 0;
            foreach (var block in activeAnimatorsList)
            {
                if (block == null || !block.enabled || !block.IsScaling) continue;

                var targetScale = Vector3.Min(Vector3.Max(block.TargetScale, block.MinScale), block.MaxScale);
                animationData[scalingCount] = new ScaleAnimationData
                {
                    currentScale = block.transform.localScale,
                    targetScale = targetScale,
                    growthRate = block.GrowthRate,
                    minScale = block.MinScale,
                    maxScale = block.MaxScale,
                    blockIndex = scalingCount
                };
                scalingCount++;
            }

            if (scalingCount == 0) return;

            completionQueue.Clear();

            var job = new UpdateScalesJob
            {
                data = animationData,
                deltaTime = deltaTime,
                completionThreshold = COMPLETION_THRESHOLD
            };

            var handle = job.Schedule(scalingCount, BATCH_SIZE);
            handle.Complete();

            // Process results and queue completions
            for (int i = 0; i < scalingCount; i++)
            {
                var data = animationData[i];
                var block = activeAnimatorsList[data.blockIndex];
                if (block != null && block.enabled)
                {
                    var sqrDistance = math.lengthsq(data.targetScale - data.currentScale);

                    if (sqrDistance <= COMPLETION_THRESHOLD)
                    {
                        completionQueue.Add((block, data.targetScale));
                    }
                    else
                    {
                        block.transform.localScale = data.currentScale;
                    }
                }
            }

            // Process completions
            foreach (var (block, targetScale) in completionQueue)
            {
                block.transform.localScale = targetScale;
                block.IsScaling = false;

                // Set scale one final time to ensure we hit target exactly
                block.transform.localScale = targetScale;

                bool wasRemoved = activeAnimators.Remove(block);

                // Validate removal
                if (!wasRemoved)
                {
                    // Check if it's actually in the set
                    bool contains = activeAnimators.Contains(block);
                }

                block.ExecuteOnScaleComplete();
            }

            // Validate all remaining active animators are actually scaling
            foreach (var animator in activeAnimators.ToArray())
            {
                if (!animator.IsScaling)
                {
                    activeAnimators.Remove(animator);
                }
            }
        }

        protected override void CleanupResources()
        {
            base.CleanupResources();
            completionQueue.Clear();
        }
    }

    public struct ScaleAnimationData
    {
        public Vector3 currentScale;
        public Vector3 targetScale;
        public Vector3 minScale;
        public Vector3 maxScale;
        public float growthRate;
        public int blockIndex;
    }

    [Unity.Burst.BurstCompile]
    public struct UpdateScalesJob : IJobParallelFor
    {
        public NativeArray<ScaleAnimationData> data;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float completionThreshold;

        public void Execute(int i)
        {
            var item = data[i];

            var diff = item.targetScale - item.currentScale;
            var sqrDistance = math.lengthsq(diff);

            if (sqrDistance > completionThreshold)
            {
                var lerpSpeed = math.clamp(
                    item.growthRate * deltaTime,
                    0.05f,
                    0.1f
                );

                item.currentScale = math.lerp(item.currentScale, item.targetScale, lerpSpeed);
            }
            else
            {
                item.currentScale = item.targetScale;
            }

            data[i] = item;
        }
    }
}