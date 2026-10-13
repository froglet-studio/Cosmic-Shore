using System;
using System.Collections.Generic;
using Unity.Jobs;

// UnityEngine.Jobs (original contract): jobs over a set of transforms. It is engine API, but its
// schedules return Unity.Jobs.JobHandle, which lives here - so it sits beside the job types, the
// way Compat already carries the other engine namespaces that need a package type.
namespace CosmicShore.Engine.Jobs
{
    /// <summary>
    /// One transform as a transform job sees it. A destroyed transform is not valid: reading it
    /// gives defaults and writing it does nothing, as in Unity.
    /// </summary>
    public struct TransformAccess
    {
        readonly Transform _t;

        internal TransformAccess(Transform t) => _t = t;

        public bool isValid => _t != null;

        public Vector3 position
        {
            get => isValid ? _t.position : default;
            set { if (isValid) _t.position = value; }
        }

        public Quaternion rotation
        {
            get => isValid ? _t.rotation : Quaternion.identity;
            set { if (isValid) _t.rotation = value; }
        }

        public Vector3 localPosition
        {
            get => isValid ? _t.localPosition : default;
            set { if (isValid) _t.localPosition = value; }
        }

        public Quaternion localRotation
        {
            get => isValid ? _t.localRotation : Quaternion.identity;
            set { if (isValid) _t.localRotation = value; }
        }

        public Vector3 localScale
        {
            get => isValid ? _t.localScale : Vector3.one;
            set { if (isValid) _t.localScale = value; }
        }

        public Matrix4x4 localToWorldMatrix => isValid ? _t.localToWorldMatrix : Matrix4x4.identity;
        public Matrix4x4 worldToLocalMatrix => isValid ? _t.worldToLocalMatrix : Matrix4x4.identity;

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { if (isValid) _t.SetPositionAndRotation(position, rotation); }
        public void SetLocalPositionAndRotation(Vector3 localPosition, Quaternion localRotation) { if (isValid) _t.SetLocalPositionAndRotation(localPosition, localRotation); }

        public void GetPositionAndRotation(out Vector3 position, out Quaternion rotation)
        {
            position = this.position;
            rotation = this.rotation;
        }

        public void GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = this.localPosition;
            localRotation = this.localRotation;
        }
    }

    /// <summary>The transforms a transform job runs over, in order; a removal swaps the last one in.</summary>
    public struct TransformAccessArray : IDisposable
    {
        List<Transform> _list;

        public TransformAccessArray(int capacity, int desiredJobCount = -1) => _list = new List<Transform>(Math.Max(0, capacity));

        public TransformAccessArray(Transform[] transforms, int desiredJobCount = -1) => _list = new List<Transform>(transforms);

        public static void Allocate(int capacity, int desiredJobCount, out TransformAccessArray array) => array = new TransformAccessArray(capacity, desiredJobCount);

        public bool isCreated => _list != null;
        public int length => _list?.Count ?? 0;

        public int capacity
        {
            get => _list?.Capacity ?? 0;
            set { if (_list != null) _list.Capacity = Math.Max(value, _list.Count); }
        }

        public Transform this[int index]
        {
            get => List[index];
            set => List[index] = value;
        }

        public void Add(Transform transform) => List.Add(transform);

        public void RemoveAtSwapBack(int index)
        {
            var list = List;
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }

        public void SetTransforms(Transform[] transforms)
        {
            List.Clear();
            List.AddRange(transforms);
        }

        public void Dispose() => _list = null;

        List<Transform> List => _list ?? throw new InvalidOperationException("The TransformAccessArray has been disposed or was never created.");

        internal TransformAccess Access(int index) => new(_list[index]);
    }

    public interface IJobParallelForTransform
    {
        void Execute(int index, TransformAccess transform);
    }

    /// <summary>
    /// The port runs a transform job at Schedule, on the calling thread, in index order (as every
    /// job here does), so the handle it returns is already complete.
    /// </summary>
    public static class IJobParallelForTransformExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, TransformAccessArray transforms, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform
        {
            Run(jobData, transforms);
            return default;
        }

        public static JobHandle ScheduleByRef<T>(ref this T jobData, TransformAccessArray transforms, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform
        {
            Run(jobData, transforms);
            return default;
        }

        public static JobHandle ScheduleReadOnly<T>(this T jobData, TransformAccessArray transforms, int batchSize, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform
        {
            Run(jobData, transforms);
            return default;
        }

        public static JobHandle ScheduleReadOnlyByRef<T>(ref this T jobData, TransformAccessArray transforms, int batchSize, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform
        {
            Run(jobData, transforms);
            return default;
        }

        public static void RunReadOnly<T>(this T jobData, TransformAccessArray transforms)
            where T : struct, IJobParallelForTransform => Run(jobData, transforms);

        public static void RunReadOnlyByRef<T>(ref this T jobData, TransformAccessArray transforms)
            where T : struct, IJobParallelForTransform => Run(jobData, transforms);

        static void Run<T>(T jobData, TransformAccessArray transforms) where T : struct, IJobParallelForTransform
        {
            for (int i = 0; i < transforms.length; i++) jobData.Execute(i, transforms.Access(i));
        }
    }
}
