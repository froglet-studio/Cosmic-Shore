using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Round 11a-2 (Docs/SWARM_FAUNA.md §19.2, §19.4): the per-frame body pose of every shown swarm member, off the main
    /// thread and Burst-compiled. One <see cref="SwarmBodyPose.PoseMatrix"/> per shown slot - the SAME static function the
    /// harness runs (R11d) - written straight into the float4x4 array <c>PrismRenderService.SetTransformsBatch</c> reads.
    /// <c>SwarmFauna</c> schedules it as soon as the frame's display alpha is known and hands its handle to the transform
    /// write as a dependency, so the main thread pays a schedule and a wait, not the pose.
    /// </summary>
    [BurstCompile]
    public struct SwarmPoseJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SwarmInstance> Instances;   // the published frame (copied once per tick)
        [ReadOnly] public NativeArray<int> Shown;                 // shown slots, compact (SwarmEntityLedger.Shown)
        [WriteOnly] public NativeArray<float4x4> Matrices;        // one per shown slot, in Shown order
        public float Alpha, Clock, BloomTicks;
        public SVector3 Up, UpAlt;

        public void Execute(int k)
        {
            SwarmBodyPose.PoseMatrix(Instances[Shown[k]], Alpha, Clock, BloomTicks, Up, UpAlt, out var m);
            Matrices[k] = new float4x4(new float4(m.C0X, m.C0Y, m.C0Z, m.C0W), new float4(m.C1X, m.C1Y, m.C1Z, m.C1W),
                                       new float4(m.C2X, m.C2Y, m.C2Z, m.C2W), new float4(m.C3X, m.C3Y, m.C3Z, m.C3W));
        }

        /// <summary>Members per worker batch: a pose is ~60 flops, so small batches would be all scheduling overhead.</summary>
        public const int BatchSize = 128;
    }
}
