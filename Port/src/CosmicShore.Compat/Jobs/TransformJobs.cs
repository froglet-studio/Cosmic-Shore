using Unity.Jobs;

namespace CosmicShore.Engine.Jobs
{
    /// <summary>
    /// Original contract (UnityEngine.Jobs.IJobParallelForTransformExtensions). Lives in Compat because it
    /// returns Unity.Jobs.JobHandle. Like every port job it runs at Schedule, synchronously, in index
    /// order, so the handle is already complete; ScheduleReadOnly is the same walk (the job only reads).
    /// </summary>
    public static class IJobParallelForTransformExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, TransformAccessArray transforms, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform { Walk(ref jobData, transforms); return default; }

        public static JobHandle ScheduleByRef<T>(this ref T jobData, TransformAccessArray transforms, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform { Walk(ref jobData, transforms); return default; }

        public static JobHandle ScheduleReadOnly<T>(this T jobData, TransformAccessArray transforms, int batchSize, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform { Walk(ref jobData, transforms); return default; }

        public static void RunReadOnly<T>(this T jobData, TransformAccessArray transforms)
            where T : struct, IJobParallelForTransform => Walk(ref jobData, transforms);

        public static void Run<T>(this T jobData, TransformAccessArray transforms)
            where T : struct, IJobParallelForTransform => Walk(ref jobData, transforms);

        static void Walk<T>(ref T job, TransformAccessArray transforms) where T : struct, IJobParallelForTransform
        {
            int n = transforms.length;
            for (int i = 0; i < n; i++) job.Execute(i, transforms.Access(i));
        }
    }
}
