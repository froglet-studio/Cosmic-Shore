using System;

namespace Unity.Jobs
{
    /// <summary>
    /// Original contract: a handle to scheduled work. The port executes jobs synchronously
    /// at Schedule (deterministic, index order), so every handle is already complete.
    /// </summary>
    public struct JobHandle : IEquatable<JobHandle>
    {
        public bool IsCompleted => true;
        public void Complete() { }
        public static void ScheduleBatchedJobs() { }
        public static void CompleteAll(ref JobHandle job0, ref JobHandle job1) { }
        public static void CompleteAll(ref JobHandle job0, ref JobHandle job1, ref JobHandle job2) { }
        public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1) => default;
        public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1, JobHandle job2) => default;
        public static JobHandle CombineDependencies(CosmicShore.Engine.Collections.NativeArray<JobHandle> jobs) => default;
        public static bool CheckFenceIsDependencyOrDidSyncFence(JobHandle jobHandle, JobHandle dependsOn) => true;
        public bool Equals(JobHandle other) => true;
        public override bool Equals(object obj) => obj is JobHandle;
        public override int GetHashCode() => 0;
    }

    public interface IJob { void Execute(); }
    public interface IJobParallelFor { void Execute(int index); }
    public interface IJobFor { void Execute(int index); }
    public interface IJobParallelForBatch { void Execute(int startIndex, int count); }

    public static class IJobExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, JobHandle dependsOn = default) where T : struct, IJob { jobData.Execute(); return default; }
        public static void Run<T>(this T jobData) where T : struct, IJob => jobData.Execute();
    }

    public static class IJobParallelForExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, IJobParallelFor
        {
            for (int i = 0; i < arrayLength; i++) jobData.Execute(i);
            return default;
        }
        public static void Run<T>(this T jobData, int arrayLength) where T : struct, IJobParallelFor { for (int i = 0; i < arrayLength; i++) jobData.Execute(i); }
    }

    public static class IJobForExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, int arrayLength, JobHandle dependency) where T : struct, IJobFor { for (int i = 0; i < arrayLength; i++) jobData.Execute(i); return default; }
        public static JobHandle ScheduleParallel<T>(this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependency) where T : struct, IJobFor { for (int i = 0; i < arrayLength; i++) jobData.Execute(i); return default; }
        public static void Run<T>(this T jobData, int arrayLength) where T : struct, IJobFor { for (int i = 0; i < arrayLength; i++) jobData.Execute(i); }
    }

    public static class IJobParallelForBatchExtensions
    {
        public static JobHandle ScheduleBatch<T>(this T jobData, int arrayLength, int minIndicesPerJobCount, JobHandle dependsOn = default) where T : struct, IJobParallelForBatch
        {
            int batch = Math.Max(1, minIndicesPerJobCount);
            for (int i = 0; i < arrayLength; i += batch) jobData.Execute(i, Math.Min(batch, arrayLength - i));
            return default;
        }
    }
}

namespace Unity.Jobs.LowLevel.Unsafe
{
    /// <summary>
    /// The job system's switches. The port runs every job as managed code at Schedule, so the job
    /// compiler (Burst for jobs) is never on - matching <see cref="Unity.Burst.BurstCompiler.IsEnabled"/>.
    /// Read by the game's diagnostics (RunEnvironment, BurstProbe); setting them changes nothing here.
    /// </summary>
    public static class JobsUtility
    {
        public static bool JobCompilerEnabled { get => false; set { } }
        public static bool JobDebuggerEnabled { get; set; }
    }
}
