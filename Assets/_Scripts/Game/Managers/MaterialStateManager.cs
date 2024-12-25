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
        private readonly List<MaterialPropertyAnimator> activeAnimators = new List<MaterialPropertyAnimator>();
        private NativeArray<MaterialAnimationData> animationData;

        private static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");

        public void RegisterAnimator(MaterialPropertyAnimator animator)
        {
            if (!activeAnimators.Contains(animator))
                activeAnimators.Add(animator);
        }

        public void UnregisterAnimator(MaterialPropertyAnimator animator)
        {
            activeAnimators.Remove(animator);
        }

        private void Update()
        {
            if (activeAnimators.Count == 0) return;

            // Initialize or resize animation data array if needed
            if (!animationData.IsCreated || animationData.Length != activeAnimators.Count)
            {
                if (animationData.IsCreated) animationData.Dispose();
                animationData = new NativeArray<MaterialAnimationData>(activeAnimators.Count, Allocator.TempJob);
            }

            // Update animation data for job
            for (int i = 0; i < activeAnimators.Count; i++)
            {
                var animator = activeAnimators[i];
                animationData[i] = new MaterialAnimationData
                {
                    progress = animator.AnimationProgress,
                    duration = animator.Duration,
                    startBrightColor = animator.StartBrightColor,
                    targetBrightColor = animator.TargetBrightColor,
                    startDarkColor = animator.StartDarkColor,
                    targetDarkColor = animator.TargetDarkColor,
                    startSpread = animator.StartSpread,
                    targetSpread = animator.TargetSpread
                };
            }

            // Schedule and run the job
            var job = new UpdateAnimationsJob
            {
                data = animationData,
                deltaTime = Time.deltaTime
            };

            var handle = job.Schedule(animationData.Length, 64);
            handle.Complete();

            // Apply results back to property blocks
            for (int i = 0; i < activeAnimators.Count; i++)
            {
                var animator = activeAnimators[i];
                if (animator.IsAnimating)
                {
                    ApplyAnimationToPropertyBlock(animator, i);
                }
            }
        }

        private void ApplyAnimationToPropertyBlock(MaterialPropertyAnimator animator, int dataIndex)
        {
            var data = animationData[dataIndex];
            animator.AnimationProgress = data.progress;

            float t = Mathf.SmoothStep(0f, 1f, data.progress);

            animator.PropertyBlock.SetColor(BrightColorId, Color.Lerp(data.startBrightColor, data.targetBrightColor, t));
            animator.PropertyBlock.SetColor(DarkColorId, Color.Lerp(data.startDarkColor, data.targetDarkColor, t));
            animator.PropertyBlock.SetVector(SpreadId, Vector3.Lerp(data.startSpread, data.targetSpread, t));

            animator.MeshRenderer.SetPropertyBlock(animator.PropertyBlock);

            if (data.progress >= 1f)
            {
                animator.IsAnimating = false;
                animator.OnAnimationComplete?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (animationData.IsCreated)
            {
                animationData.Dispose();
            }
        }
    }

    public struct MaterialAnimationData
    {
        public Color startBrightColor;  
        public Color targetBrightColor; 
        public Color startDarkColor;    
        public Color targetDarkColor;   
        public Vector3 startSpread;     
        public Vector3 targetSpread;    
        public float progress;
        public float duration;
    }

    [Unity.Burst.BurstCompile]
    public struct UpdateAnimationsJob : IJobParallelFor
    {
        public NativeArray<MaterialAnimationData> data;
        public float deltaTime;

        public void Execute(int i)
        {
            var item = data[i];
            item.progress = math.min(1f, item.progress + deltaTime / item.duration);
            data[i] = item;
        }
    }
}
