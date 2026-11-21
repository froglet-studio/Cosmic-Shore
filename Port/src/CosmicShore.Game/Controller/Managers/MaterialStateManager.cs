// Ported from Assets/_Scripts/Controller/Managers/MaterialStateManager.cs
// PORT managed-array conversion: using Unity.Collections; using Unity.Jobs;
// using Unity.Mathematics; dropped — UpdateAnimationsJob (formerly a
// [BurstCompile] IJobParallelFor) is a plain struct whose Execute(i) runs in a
// sequential loop over the manager's managed array; float4/float3/math.* →
// Vector4/Vector3/Mathf with per-element math preserved exactly (precedent:
// the PrismSpatialIndex / BlockDensityGrid managed-array ports).
using CosmicShore.Engine;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using System;

namespace CosmicShore.Gameplay
{
    public class MaterialStateManager : AdaptiveAnimationManager<MaterialStateManager, MaterialPropertyAnimator, MaterialAnimationData>
    {
        private readonly List<(MaterialPropertyAnimator animator, Vector4 brightColor, Vector4 darkColor, Vector3 spread)> propertyUpdateQueue =
            new List<(MaterialPropertyAnimator, Vector4, Vector4, Vector3)>(32); // PORT managed-array conversion: float4/float4/float3 tuple elements

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

            // PORT managed-array conversion: job.Schedule(animatingCount, BATCH_SIZE) +
            // handle.Complete() → sequential Execute over the same data.
            for (int jobIndex = 0; jobIndex < animatingCount; jobIndex++)
                job.Execute(jobIndex);

            // Process results and queue property updates
            for (int i = 0; i < animatingCount; i++)
            {
                var data = animationData[i];
                var animator = activeAnimatorsList[data.animatorIndex];
                if (animator != null && animator.enabled && animator.MeshRenderer != null)
                {
                    animator.AnimationProgress = data.progress;

                    float t = Mathf.SmoothStep(0f, 1f, data.progress); // PORT managed-array conversion: math.smoothstep — identical clamp + cubic
                    var brightColor = Vector4.LerpUnclamped(data.startBrightColor, data.targetBrightColor, t); // PORT managed-array conversion: math.lerp (unclamped, componentwise)
                    var darkColor = Vector4.LerpUnclamped(data.startDarkColor, data.targetDarkColor, t);       // PORT managed-array conversion: math.lerp
                    var spread = Vector3.LerpUnclamped(data.startSpread, data.targetSpread, t);                // PORT managed-array conversion: math.lerp

                    propertyUpdateQueue.Add((animator, brightColor, darkColor, spread));

                    if (data.progress >= 0.99f)
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
                                CSDebug.LogError($"Error in animation completion callback: {e.Message}");
                            }
                            animator.OnAnimationComplete = null;
                        }
                    }
                }
            }


            // Validate all remaining active animators are actually animating. Reuse the
            // per-frame snapshot list instead of allocating activeAnimators.ToArray() each frame.
            foreach (var animator in activeAnimatorsList)
            {
                if (animator != null && !animator.IsAnimating)
                    activeAnimators.Remove(animator);
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

        private static Vector4 ToFloat4(Color color) => new Vector4(color.r, color.g, color.b, color.a); // PORT managed-array conversion: float4 return type
        private static Color ToColor(Vector4 f4) => new Color(f4.x, f4.y, f4.z, f4.w); // PORT managed-array conversion: float4 parameter
    }

    public struct MaterialAnimationData
    {
        public Vector4 startBrightColor;  // PORT managed-array conversion: float4
        public Vector4 targetBrightColor; // PORT managed-array conversion: float4
        public Vector4 startDarkColor;    // PORT managed-array conversion: float4
        public Vector4 targetDarkColor;   // PORT managed-array conversion: float4
        public Vector3 startSpread;       // PORT managed-array conversion: float3
        public Vector3 targetSpread;      // PORT managed-array conversion: float3
        public float progress;
        public float duration;
        public int animatorIndex;
    }

    // PORT managed-array conversion: was [Unity.Burst.BurstCompile] struct … : IJobParallelFor;
    // Execute(i) is unchanged and run sequentially by the manager.
    public struct UpdateAnimationsJob
    {
        public MaterialAnimationData[] data; // PORT managed-array conversion: NativeArray<MaterialAnimationData>
        public float deltaTime;              // PORT managed-array conversion: [ReadOnly]

        public void Execute(int i)
        {
            var item = data[i];
            item.progress = Mathf.Min(1f, item.progress + deltaTime / item.duration); // PORT managed-array conversion: math.min
            data[i] = item;
        }
    }
}
