using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Jobs
{
    /// <summary>
    /// Original contract (UnityEngine.Jobs.TransformAccessArray): the transforms a transform job reads or
    /// writes, by index. A struct over a shared list, so copies (a job's field) see the same transforms, as
    /// Unity's handle does. The port runs transform jobs synchronously on the calling thread
    /// (<c>IJobParallelForTransformExtensions</c> in CosmicShore.Compat), in index order.
    /// </summary>
    public struct TransformAccessArray : IDisposable
    {
        List<Transform> _list;

        public TransformAccessArray(int capacity, int desiredJobCount = -1) { _list = new List<Transform>(Math.Max(0, capacity)); }
        public TransformAccessArray(Transform[] transforms, int desiredJobCount = -1) { _list = new List<Transform>(transforms ?? Array.Empty<Transform>()); }

        public bool isCreated => _list != null;
        public int length => _list?.Count ?? 0;
        public int capacity { get => _list?.Capacity ?? 0; set { if (_list != null && value >= _list.Count) _list.Capacity = value; } }

        public Transform this[int index] { get => _list[index]; set => _list[index] = value; }

        public void Add(Transform transform) => _list.Add(transform);
        public void RemoveAtSwapBack(int index)
        {
            int last = _list.Count - 1;
            _list[index] = _list[last];
            _list.RemoveAt(last);
        }
        public void SetTransforms(Transform[] transforms) { _list.Clear(); if (transforms != null) _list.AddRange(transforms); }
        public void Dispose() { _list = null; }

        /// <summary>The access a job sees for index <paramref name="i"/> (engine side of the job runner).</summary>
        public TransformAccess Access(int i) => new TransformAccess(_list[i]);
    }

    /// <summary>
    /// Original contract (UnityEngine.Jobs.TransformAccess): one transform as a job sees it. Here it is
    /// the transform itself, read and written on the calling thread.
    /// </summary>
    public readonly struct TransformAccess
    {
        readonly Transform _t;
        public TransformAccess(Transform t) { _t = t; }

        public bool isValid => _t != null;
        public Vector3 position { get => _t.position; set => _t.position = value; }
        public Quaternion rotation { get => _t.rotation; set => _t.rotation = value; }
        public Vector3 localPosition { get => _t.localPosition; set => _t.localPosition = value; }
        public Quaternion localRotation { get => _t.localRotation; set => _t.localRotation = value; }
        public Vector3 localScale { get => _t.localScale; set => _t.localScale = value; }
        public Matrix4x4 localToWorldMatrix => _t.localToWorldMatrix;
        public Matrix4x4 worldToLocalMatrix => _t.worldToLocalMatrix;
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) => _t.SetPositionAndRotation(position, rotation);
        public void GetPositionAndRotation(out Vector3 position, out Quaternion rotation) { position = _t.position; rotation = _t.rotation; }
    }

    /// <summary>Original contract (UnityEngine.Jobs.IJobParallelForTransform).</summary>
    public interface IJobParallelForTransform { void Execute(int index, TransformAccess transform); }
}
