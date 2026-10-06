// Ported from Assets/_Scripts/Controller/Managers/PrismScaleManager.cs
// PORT managed-array conversion: using Unity.Collections; using Unity.Jobs;
// using Unity.Mathematics; dropped — UpdateScalesJob (formerly a [BurstCompile]
// IJobParallelFor) is a plain struct whose Execute(i) runs in a sequential loop
// over the manager's managed array; float3/math.* → Vector3/Mathf with the
// clamp(growthRate·dt)/snap semantics preserved exactly (precedent: the
// PrismSpatialIndex / BlockDensityGrid managed-array ports).
using CosmicShore.Engine;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using System.Linq;
namespace CosmicShore.Gameplay
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

            // PORT managed-array conversion: job.Schedule(scalingCount, BATCH_SIZE) +
            // handle.Complete() → sequential Execute over the same data.
            for (int jobIndex = 0; jobIndex < scalingCount; jobIndex++)
                job.Execute(jobIndex);

            // Apply results to the correct block using scalingAnimators[i]
            for (int i = 0; i < scalingCount; i++)
            {
                var data = animationData[i];
                var block = scalingAnimators[i];

                if (block == null || !block.enabled)
                    continue;

                var sqrDistance = (data.targetScale - data.currentScale).sqrMagnitude; // PORT managed-array conversion: math.lengthsq((float3)(…))

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

    // PORT managed-array conversion: was [Unity.Burst.BurstCompile] struct … : IJobParallelFor;
    // Execute(i) math is unchanged and run sequentially by the manager.
    public struct UpdateScalesJob
    {
        public ScaleAnimationData[] data;    // PORT managed-array conversion: NativeArray<ScaleAnimationData>
        public float deltaTime;              // PORT managed-array conversion: [ReadOnly]
        public float completionThresholdSqr; // PORT managed-array conversion: [ReadOnly]

        public void Execute(int i)
        {
            var item = data[i];

            var diff = item.targetScale - item.currentScale; // PORT managed-array conversion: (float3)
            var sqrDistance = diff.sqrMagnitude;             // PORT managed-array conversion: math.lengthsq

            if (sqrDistance > completionThresholdSqr)
            {
                // You can tune these, but keeping your original clamp behavior:
                var lerpSpeed = Mathf.Clamp(item.growthRate * deltaTime, 0.05f, 0.1f); // PORT managed-array conversion: math.clamp
                item.currentScale = Vector3.LerpUnclamped(item.currentScale, item.targetScale, lerpSpeed); // PORT managed-array conversion: math.lerp (unclamped, componentwise)
            }
            else
            {
                item.currentScale = item.targetScale;
            }

            data[i] = item;
        }
    }
}
