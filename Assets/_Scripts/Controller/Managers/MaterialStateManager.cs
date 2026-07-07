using UnityEngine;
using Unity.Mathematics;
using Unity.Profiling;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using System;

namespace CosmicShore.Gameplay
{
    public class MaterialStateManager : AdaptiveAnimationManager<MaterialStateManager, MaterialPropertyAnimator, MaterialAnimationData>
    {
        // The profiler collapses every AdaptiveAnimationManager instance into one
        // generic Update row — this marker attributes this manager's share.
        static readonly ProfilerMarker s_processMarker = new("MaterialStateManager.Process");

        // Animators that finished this frame — callbacks run after the pass since
        // they may start/stop other animators.
        private readonly List<MaterialPropertyAnimator> completedAnimators =
            new List<MaterialPropertyAnimator>(32);

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
            using var _ = s_processMarker.Auto();

            // Update our stable index list — the snapshot also makes activeAnimators
            // safe to mutate (stale-prune here, completion callbacks below).
            activeAnimatorsList.Clear();
            activeAnimatorsList.AddRange(activeAnimators);

            completedAnimators.Clear();
            sharedPropertyBlock ??= new MaterialPropertyBlock();

            // Single fused pass: advance progress, lerp colors, apply. The old Burst
            // job only did the one-FMA progress advance, so its Schedule/Complete
            // overhead exceeded the work at any realistic animator count — and its
            // compacted animatorIndex indexed the UNcompacted snapshot list, so a
            // gate-skipped animator (dead renderer mid-animation) shifted every
            // later animator's colors onto the wrong object. Working directly on
            // the animator reference removes that class of bug.
            for (int i = 0; i < activeAnimatorsList.Count; i++)
            {
                var animator = activeAnimatorsList[i];
                if (animator == null || !animator.IsAnimating)
                {
                    // Stale entry (destroyed / stopped) — prune instead of a second
                    // whole-list validation walk at the end.
                    activeAnimators.Remove(animator);
                    continue;
                }
                if (!animator.enabled || animator.MeshRenderer == null) continue;

                float progress = math.min(1f, animator.AnimationProgress + deltaTime / animator.Duration);
                animator.AnimationProgress = progress;

                float t = math.smoothstep(0f, 1f, progress);
                // Endpoint float4s are cached on the animator at (re)target time —
                // no per-frame Color property reads or conversions here.
                var brightColor = math.lerp(animator.StartBright4, animator.TargetBright4, t);
                var darkColor = math.lerp(animator.StartDark4, animator.TargetDark4, t);
                var spread = math.lerp(animator.StartSpread, animator.TargetSpread, t);

                var bright = ToColor(brightColor);
                var dark = ToColor(darkColor);

                // Track the displayed values — the interruption start-state
                // for re-targeted animations on both render paths.
                animator.CurrentBrightColor = bright;
                animator.CurrentDarkColor = dark;
                animator.CurrentSpread = new Vector3(spread.x, spread.y, spread.z);

                // Instanced path: colors sink into the companion entity's
                // DOTS-instanced overrides. Legacy path: per-renderer MPB.
                var prism = animator.CachedPrism;
                if (prism != null && prism.UsesEntityColorSink)
                {
                    CosmicShore.ECS.PrismRenderService.SetColors(
                        in prism.RenderHandle, in brightColor, in darkColor, in spread);
                }
                else
                {
                    sharedPropertyBlock.SetColor(BRIGHT_COLOR_PROP, bright);
                    sharedPropertyBlock.SetColor(DARK_COLOR_PROP, dark);
                    sharedPropertyBlock.SetVector(SPREAD_PROP, new Vector4(spread.x, spread.y, spread.z, 0));
                    animator.MeshRenderer.SetPropertyBlock(sharedPropertyBlock);
                }

                if (progress >= 0.99f)
                {
                    animator.IsAnimating = false;
                    activeAnimators.Remove(animator);
                    completedAnimators.Add(animator);
                }
            }

            // Completion callbacks after the pass — they may start/stop animators.
            for (int i = 0; i < completedAnimators.Count; i++)
            {
                var animator = completedAnimators[i];
                if (animator == null || animator.OnAnimationComplete == null) continue;
                try
                {
                    animator.OnAnimationComplete.Invoke();
                }
                catch (System.Exception e)
                {
                    CSDebug.LogError($"Error in animation completion callback: {e.Message}");
                }
                animator.OnAnimationComplete = null;
            }
        }

        protected override void CleanupResources()
        {
            base.CleanupResources();
            completedAnimators.Clear();
        }

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
}