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
        private const int BATCH_SIZE = 128;
        private const float COMPLETION_THRESHOLD = 0.0001f;
        
        // Track all registered blocks
        private readonly HashSet<BlockScaleAnimator> registeredBlocks = new HashSet<BlockScaleAnimator>();
        // Track only actively scaling blocks
        private readonly HashSet<BlockScaleAnimator> activeScalingBlocks = new HashSet<BlockScaleAnimator>();
        // List to maintain stable indices for the job system
        private readonly List<BlockScaleAnimator> activeBlocksList = new List<BlockScaleAnimator>();
        private NativeArray<ScaleAnimationData> scaleData;
        private readonly List<(BlockScaleAnimator block, Vector3 scale)> completionQueue = new List<(BlockScaleAnimator, Vector3)>(32);

        public override void Awake()
        {
            base.Awake();
            scaleData = new NativeArray<ScaleAnimationData>(32, Allocator.Persistent);
        }

        // Original registration methods for compatibility
        public void RegisterBlock(BlockScaleAnimator block)
        {
            if (block == null) return;
            registeredBlocks.Add(block);
            
            // If block is already scaling, add to active set
            if (block.IsScaling)
            {
                activeScalingBlocks.Add(block);
                EnsureCapacity();
            }
        }

        public void UnregisterBlock(BlockScaleAnimator block)
        {
            if (block == null) return;
            registeredBlocks.Remove(block);
            activeScalingBlocks.Remove(block);
        }

        // Internal methods to manage active scaling state
        internal void OnBlockStartScaling(BlockScaleAnimator block)
        {
            if (block == null || !block.enabled || !registeredBlocks.Contains(block)) return;
            activeScalingBlocks.Add(block);
            EnsureCapacity();
        }

        internal void OnBlockStopScaling(BlockScaleAnimator block)
        {
            if (block == null) return;
            activeScalingBlocks.Remove(block);
        }

        private void EnsureCapacity()
        {
            if (activeScalingBlocks.Count > scaleData.Length)
            {
                var newSize = Mathf.Max(32, Mathf.NextPowerOfTwo(activeScalingBlocks.Count));
                var newArray = new NativeArray<ScaleAnimationData>(newSize, Allocator.Persistent);
                if (scaleData.IsCreated)
                {
                    if (scaleData.Length > 0)
                    {
                        NativeArray<ScaleAnimationData>.Copy(scaleData, newArray, scaleData.Length);
                    }
                    scaleData.Dispose();
                }
                scaleData = newArray;
            }
        }

        private void Update()
        {
            if (Time.deltaTime == 0 || activeScalingBlocks.Count == 0) return;

            // Update our stable index list
            activeBlocksList.Clear();
            activeBlocksList.AddRange(activeScalingBlocks);

            int scalingCount = 0;
            foreach (var block in activeBlocksList)
            {
                if (block == null || !block.enabled || !block.IsScaling) continue;

                var targetScale = Vector3.Min(Vector3.Max(block.TargetScale, block.MinScale), block.MaxScale);
                scaleData[scalingCount] = new ScaleAnimationData
                {
                    currentScale = block.transform.localScale,
                    targetScale = targetScale,
                    growthRate = block.GrowthRate,
                    minScale = block.MinScale,
                    maxScale = block.MaxScale,
                    blockIndex = scalingCount // Store index instead of reference
                };
                scalingCount++;
            }

            if (scalingCount == 0) return;

            completionQueue.Clear();

            var job = new UpdateScalesJob
            {
                data = scaleData,
                deltaTime = Time.deltaTime,
                completionThreshold = COMPLETION_THRESHOLD
            };

            var handle = job.Schedule(scalingCount, BATCH_SIZE);
            handle.Complete();

            // Process results and queue completions
            for (int i = 0; i < scalingCount; i++)
            {
                var data = scaleData[i];
                var block = activeBlocksList[data.blockIndex];
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

            // Process completions and remove completed blocks
            for (int i = 0; i < completionQueue.Count; i++)
            {
                var (block, targetScale) = completionQueue[i];
                block.transform.localScale = targetScale;
                block.IsScaling = false;
                activeScalingBlocks.Remove(block);
                
                if (block.OnScaleComplete != null)
                {
                    try
                    {
                        block.OnScaleComplete.Invoke();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error in scale completion callback: {e.Message}");
                    }
                    block.OnScaleComplete = null;
                }
            }
        }

        private void OnDisable()
        {
            CleanupResources();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (scaleData.IsCreated)
            {
                scaleData.Dispose();
            }
            registeredBlocks.Clear();
            activeScalingBlocks.Clear();
            activeBlocksList.Clear();
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
        public int blockIndex; // Store index instead of reference
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
                    item.growthRate * deltaTime * math.sqrt(sqrDistance), 
                    0.01f, 
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
