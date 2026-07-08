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

        // A stalled grower (editor pause, load hitch, budget starvation) steps with
        // its full elapsed dt when its slice arrives — capped so catch-up reads as
        // fast growth, never as a pop to target (continuity of existence).
        private const float MAX_STEP_DT = 0.5f;

        [Header("Slicing")]
        [Tooltip("Max growers stepped per processed tick. A grazing frenzy can push " +
                 "thousands of growers active at once; the rotating window bounds the " +
                 "per-frame native interop (transform writes, render-entity sync, volume " +
                 "cache) while per-grower true-dt easing preserves growth tempo.")]
        [SerializeField, Min(50)] private int maxGrowersPerFrame = 300;
        private int _sliceCursor;

        private readonly List<(PrismScaleAnimator block, Vector3 scale)> completionQueue =
            new List<(PrismScaleAnimator, Vector3)>(32);

        protected override bool IsAnimatorActive(PrismScaleAnimator animator) => animator.IsScaling;
        protected override bool IsAnimatorValid(PrismScaleAnimator animator) => animator != null && animator.enabled;

        internal void OnBlockStartScaling(PrismScaleAnimator prism)
        {
            // Stamp before activation so the first sliced step measures from the
            // growth start, not from a stale (or default-zero) time.
            prism.LastStepTime = Time.time;
            OnAnimatorStart(prism);
        }

        internal void OnBlockStopScaling(PrismScaleAnimator prism) => OnAnimatorStop(prism);

        protected override void ProcessAnimationFrame(float deltaTime)
        {
            using var _ = s_processMarker.Auto();

            // Refresh stable list — the snapshot also makes activeAnimators safe to
            // mutate (stale-prune here, completion callbacks below).
            activeAnimatorsList.Clear();
            activeAnimatorsList.AddRange(activeAnimators);

            completionQueue.Clear();

            // Sliced fused pass. The per-element math is a few flops; the real cost
            // is per-grower native interop (transform read/write, render-entity
            // sync, volume-cache refresh), and a grazing frenzy can push thousands
            // of growers active at once. Step at most maxGrowersPerFrame per
            // processed tick through a rotating window. Each grower steps with its
            // TRUE elapsed dt (dt-linear easing keeps tempo exact), so slicing
            // changes when a grower steps, never how fast it grows. The volume
            // cache then refreshes at the slice rate — safe: its consumer (the
            // cell's per-domain aggregation) is itself on a 0.25 s sliced cadence.
            int count = activeAnimatorsList.Count;
            if (count == 0) return;

            int budget = math.min(count, maxGrowersPerFrame);
            if (_sliceCursor >= count) _sliceCursor = 0;

            float now = Time.time;

            for (int step = 0; step < budget; step++)
            {
                var block = activeAnimatorsList[(_sliceCursor + step) % count];
                if (block == null || !block.IsScaling)
                {
                    // Stale entry (destroyed / stopped) — prune instead of a second
                    // whole-list cleanup walk at the end.
                    activeAnimators.Remove(block);
                    continue;
                }
                if (!block.enabled) continue;

                // True elapsed time since this grower's last step. Unseeded stamps
                // (registered mid-scale before any start event) fall back to the
                // tick dt rather than measuring from t=0 — a huge dt would snap the
                // prism to target in one step (pop-in). Capped so post-hitch
                // catch-up reads as fast growth, never a pop.
                float dt = block.LastStepTime > 0f ? now - block.LastStepTime : deltaTime;
                block.LastStepTime = now;
                if (dt <= 0f) dt = deltaTime;
                else if (dt > MAX_STEP_DT) dt = MAX_STEP_DT;

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
                // at any dt — which is what makes the sliced window tempo-safe.
                const float dtNominal = 0.02f;
                float k = math.clamp(block.GrowthRate * dtNominal, 0.05f, 0.1f) / dtNominal;
                float alpha = 1f - math.exp(-k * dt);
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
                // ...and refresh the volume cache here (O(stepped)/frame) so the
                // cell's per-domain aggregation never has to read lossyScale for
                // the whole population (O(all)/recompute — the old ~23ms hotspot).
                block.OwnerPrism?.RefreshVolumeCache();
            }

            _sliceCursor = (_sliceCursor + budget) % count;

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
