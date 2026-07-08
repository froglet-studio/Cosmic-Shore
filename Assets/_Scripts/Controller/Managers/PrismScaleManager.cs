using UnityEngine;
using Unity.Mathematics;
using Unity.Profiling;
using System.Collections.Generic;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class PrismScaleManager : AdaptiveAnimationManager<PrismScaleManager, PrismScaleAnimator, ScaleAnimationData>
    {
        // The profiler collapses every AdaptiveAnimationManager instance into one
        // generic Update row — this marker attributes this manager's share.
        static readonly ProfilerMarker s_processMarker = new("PrismScaleManager.Process");

        // This is a squared distance threshold check (since we use lengthsq)
        private const float COMPLETION_THRESHOLD_SQR = 0.01f;

        private readonly List<(PrismScaleAnimator block, Vector3 scale)> completionQueue =
            new List<(PrismScaleAnimator, Vector3)>(32);

        protected override bool IsAnimatorActive(PrismScaleAnimator animator) => animator.IsScaling;
        protected override bool IsAnimatorValid(PrismScaleAnimator animator) => animator != null && animator.enabled;

        internal void OnBlockStartScaling(PrismScaleAnimator prism) => OnAnimatorStart(prism);
        internal void OnBlockStopScaling(PrismScaleAnimator prism) => OnAnimatorStop(prism);

        protected override void ProcessAnimationFrame(float deltaTime)
        {
            using var _ = s_processMarker.Auto();

            // Refresh stable list — the snapshot also makes activeAnimators safe to
            // mutate (stale-prune here, completion callbacks below).
            activeAnimatorsList.Clear();
            activeAnimatorsList.AddRange(activeAnimators);

            completionQueue.Clear();

            // Single fused pass. The per-element step is a few flops, so the old
            // build-NativeArray → Schedule → Complete → apply round-trip cost more
            // in scheduling and copying than the math it parallelized; the easing
            // below is byte-identical to what the Burst job computed.
            for (int i = 0; i < activeAnimatorsList.Count; i++)
            {
                var block = activeAnimatorsList[i];
                if (block == null || !block.IsScaling)
                {
                    // Stale entry (destroyed / stopped) — prune instead of a second
                    // whole-list cleanup walk at the end.
                    activeAnimators.Remove(block);
                    continue;
                }
                if (!block.enabled) continue;

                var targetScale = Vector3.Min(
                    Vector3.Max(block.TargetScale, block.MinScale),
                    block.MaxScale
                );

                float3 current = block.transform.localScale;
                float3 target = targetScale;

                // The old step was a clamped lerp FRACTION per processed tick
                // (clamp(GrowthRate·dt, 0.05, 0.1)) fed an effectively fixed 20 ms dt
                // by the base manager — growth tempo was defined by processing
                // cadence, so stepping a grower less often visibly slowed it.
                // Recast the same tempo as a rate: k is the per-second equivalent of
                // the old per-tick fraction, and 1 − exp(−k·dt) reproduces it at the
                // nominal cadence (0.0488 vs 0.05 at dt = 0.02) while staying correct
                // at any dt — the prerequisite for slicing this pass.
                const float dtNominal = 0.02f;
                float k = math.clamp(block.GrowthRate * dtNominal, 0.05f, 0.1f) / dtNominal;
                float alpha = 1f - math.exp(-k * deltaTime);
                var next = math.lerp(current, target, alpha);

                if (math.lengthsq(target - next) <= COMPLETION_THRESHOLD_SQR)
                {
                    // Reached target this step — snap + finalize in the completion
                    // pass (callbacks may start/stop other animators).
                    completionQueue.Add((block, targetScale));
                    continue;
                }

                block.transform.localScale = (Vector3)next;
                // Keep the companion render entity's matrix in lockstep with
                // the growth animation (no-op on the legacy path)...
                block.OwnerPrism?.SyncRenderTransform();
                // ...and refresh the volume cache here (O(growing)/frame) so the
                // cell's per-domain aggregation never has to read lossyScale for
                // the whole population (O(all)/recompute — the old ~23ms hotspot).
                block.OwnerPrism?.RefreshVolumeCache();
            }

            // Process completions
            for (int i = 0; i < completionQueue.Count; i++)
            {
                var (block, targetScale) = completionQueue[i];
                if (block == null || !block.enabled) continue;

                // Hit target exactly
                block.transform.localScale = targetScale;
                block.OwnerPrism?.SyncRenderTransform();
                // Final settled volume — last refresh until this prism scales again.
                block.OwnerPrism?.RefreshVolumeCache();

                // Stop scaling (may call back into manager depending on your base class)
                block.IsScaling = false;

                // Ensure this animator is not left in the active set
                activeAnimators.Remove(block);

                block.ExecuteOnScaleComplete();
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
        public float growthRate;
    }
}
