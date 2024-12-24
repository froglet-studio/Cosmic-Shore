using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using CosmicShore.Utility.Singleton;

namespace CosmicShore.Core
{
    public class BlockScaleManager : SingletonPersistent<BlockScaleManager>

    {
        private readonly List<BlockScaleAnimator> activeBlocks = new List<BlockScaleAnimator>();
        private NativeArray<ScaleAnimationData> scaleData;

        public void RegisterBlock(BlockScaleAnimator block)
        {
            if (!activeBlocks.Contains(block))
                activeBlocks.Add(block);
        }

        public void UnregisterBlock(BlockScaleAnimator block)
        {
            activeBlocks.Remove(block);
        }

        private void Update()
        {
            if (activeBlocks.Count == 0) return;

            if (!scaleData.IsCreated || scaleData.Length != activeBlocks.Count)
            {
                if (scaleData.IsCreated) scaleData.Dispose();
                scaleData = new NativeArray<ScaleAnimationData>(activeBlocks.Count, Allocator.TempJob);
            }

            // Update scale data for job
            for (int i = 0; i < activeBlocks.Count; i++)
            {
                var block = activeBlocks[i];
                scaleData[i] = new ScaleAnimationData
                {
                    currentScale = block.transform.localScale,
                    targetScale = block.TargetScale,
                    growthRate = block.GrowthRate,
                    minScale = block.MinScale,
                    maxScale = block.MaxScale
                };
            }

            // Schedule and run the job
            var job = new UpdateScalesJob
            {
                data = scaleData,
                deltaTime = Time.deltaTime
            };

            var handle = job.Schedule(scaleData.Length, 64);
            handle.Complete();

            // Apply results back to transforms
            for (int i = 0; i < activeBlocks.Count; i++)
            {
                var block = activeBlocks[i];
                if (block.IsScaling)
                {
                    ApplyScaleUpdate(block, i);
                }
            }
        }

        private void ApplyScaleUpdate(BlockScaleAnimator block, int dataIndex)
        {
            var data = scaleData[dataIndex];
            var sqrDistance = math.lengthsq(data.targetScale - data.currentScale);

            if (sqrDistance <= 0.05f)
            {
                block.transform.localScale = data.targetScale;
                block.IsScaling = false;
                block.OnScaleComplete?.Invoke();
            }
            else
            {
                block.transform.localScale = data.currentScale;
            }
        }

        private void OnDestroy()
        {
            if (scaleData.IsCreated)
            {
                scaleData.Dispose();
            }
        }
    }

    public struct ScaleAnimationData
    {
        public Vector3 currentScale;   
        public Vector3 targetScale;    
        public Vector3 minScale;       
        public Vector3 maxScale;
        public float growthRate;
    }

    [Unity.Burst.BurstCompile]
    public struct UpdateScalesJob : IJobParallelFor
    {
        public NativeArray<ScaleAnimationData> data;
        public float deltaTime;

        public void Execute(int i)
        {
            var item = data[i];

            // Clamp target scale
            item.targetScale = math.clamp(item.targetScale, item.minScale, item.maxScale);

            var sqrDistance = math.lengthsq(item.targetScale - item.currentScale);
            var lerpSpeed = math.clamp(item.growthRate * deltaTime * sqrDistance, 0.05f, 0.2f);

            item.currentScale = math.lerp(item.currentScale, item.targetScale, lerpSpeed);

            data[i] = item;
        }
    }
}