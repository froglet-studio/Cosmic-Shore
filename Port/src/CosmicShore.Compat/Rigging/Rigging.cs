using System;
using System.Collections;
using System.Collections.Generic;

// UnityEngine.Animations.Rigging (the live-src sync maps it to CosmicShore.Engine.Animations.Rigging).
//
// INERT: the port has no Animator / animation-job pipeline, so no constraint ever moves a
// transform. The components exist so prefabs that carry them load, and so game code that reads
// or writes their weights and data (SharkJawDriver's jaw weights) runs against real state.
// Serialized field names mirror the original (m_Weight, m_Data, …) so a reflection-based
// importer fills them.
namespace CosmicShore.Engine.Animations.Rigging
{
    /// <summary>A transform + weight pair, as listed in a constraint's source objects.</summary>
    [Serializable]
    public struct WeightedTransform
    {
        public Transform transform;
        public float weight;

        public WeightedTransform(Transform transform, float weight)
        {
            this.transform = transform;
            this.weight = Math.Clamp(weight, 0f, 1f);
        }

        public static WeightedTransform Default(float weight) => new WeightedTransform(null, weight);
    }

    /// <summary>Up to <see cref="k_MaxLength"/> weighted transforms (the original's fixed capacity).</summary>
    [Serializable]
    public struct WeightedTransformArray : IList<WeightedTransform>, IList
    {
        public const int k_MaxLength = 8;

        List<WeightedTransform> m_Items;

        public WeightedTransformArray(int size)
        {
            if (size < 0 || size > k_MaxLength) throw new ArgumentOutOfRangeException(nameof(size), $"WeightedTransformArray holds at most {k_MaxLength} items.");
            m_Items = new List<WeightedTransform>(size);
            for (int i = 0; i < size; i++) m_Items.Add(default);
        }

        List<WeightedTransform> Items => m_Items ??= new List<WeightedTransform>();

        public int Count => Items.Count;
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;

        public WeightedTransform this[int index]
        {
            get => Items[index];
            set => Items[index] = value;
        }

        object IList.this[int index]
        {
            get => Items[index];
            set => Items[index] = (WeightedTransform)value;
        }

        public void Add(WeightedTransform value)
        {
            if (Items.Count >= k_MaxLength) throw new ArgumentException($"WeightedTransformArray holds at most {k_MaxLength} items.");
            Items.Add(value);
        }

        int IList.Add(object value) { Add((WeightedTransform)value); return Count - 1; }
        public void Clear() => Items.Clear();
        public bool Contains(WeightedTransform value) => Items.Contains(value);
        bool IList.Contains(object value) => value is WeightedTransform w && Contains(w);
        public int IndexOf(WeightedTransform value) => Items.IndexOf(value);
        int IList.IndexOf(object value) => value is WeightedTransform w ? IndexOf(w) : -1;
        public void Insert(int index, WeightedTransform value)
        {
            if (Items.Count >= k_MaxLength) throw new ArgumentException($"WeightedTransformArray holds at most {k_MaxLength} items.");
            Items.Insert(index, value);
        }
        void IList.Insert(int index, object value) => Insert(index, (WeightedTransform)value);
        public bool Remove(WeightedTransform value) => Items.Remove(value);
        void IList.Remove(object value) { if (value is WeightedTransform w) Remove(w); }
        public void RemoveAt(int index) => Items.RemoveAt(index);
        public void CopyTo(WeightedTransform[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);
        void ICollection.CopyTo(Array array, int index) => ((ICollection)Items).CopyTo(array, index);
        public IEnumerator<WeightedTransform> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();

        public float GetWeight(int index) => Items[index].weight;
        public void SetWeight(int index, float weight) { var w = Items[index]; w.weight = Math.Clamp(weight, 0f, 1f); Items[index] = w; }
        public Transform GetTransform(int index) => Items[index].transform;
        public void SetTransform(int index, Transform transform) { var w = Items[index]; w.transform = transform; Items[index] = w; }
    }

    /// <summary>What every rig constraint exposes.</summary>
    public interface IRigConstraint
    {
        float weight { get; set; }
        bool IsValid();
    }

    /// <summary>
    /// Base of the port's rig constraints (the original is generic over its animation job; the
    /// port has no jobs, so the data type is the only parameter).
    /// </summary>
    public abstract class RigConstraint<TData> : MonoBehaviour, IRigConstraint where TData : struct
    {
        [SerializeField, Range(0f, 1f)] protected float m_Weight = 1f;
        [SerializeField] protected TData m_Data;

        public float weight
        {
            get => m_Weight;
            set => m_Weight = Math.Clamp(value, 0f, 1f);
        }

        public ref TData data => ref m_Data;

        public virtual bool IsValid() => true;

        /// <summary>Resets to defaults (the original's inspector Reset).</summary>
        public virtual void Reset()
        {
            m_Weight = 1f;
            m_Data = default;
        }
    }

    [Serializable]
    public struct MultiAimConstraintData
    {
        public enum Axis { X = 0, X_NEG = 1, Y = 2, Y_NEG = 3, Z = 4, Z_NEG = 5 }
        public enum WorldUpType { None = 0, SceneUp = 1, ObjectUp = 2, ObjectRotationUp = 3, Vector = 4 }

        public Transform constrainedObject;
        public WeightedTransformArray sourceObjects;
        public bool maintainOffset;
        public Vector3 offset;
        public Vector2 limits;
        public Axis aimAxis;
        public Axis upAxis;
        public WorldUpType worldUpType;
        public Transform worldUpObject;
        public Axis worldUpAxis;
        public bool constrainedXAxis;
        public bool constrainedYAxis;
        public bool constrainedZAxis;
    }

    /// <summary>Rotates the constrained object to aim at its weighted sources (inert in the port).</summary>
    public class MultiAimConstraint : RigConstraint<MultiAimConstraintData>
    {
        public override bool IsValid() => m_Data.constrainedObject != null && m_Data.sourceObjects.Count > 0;
    }

    [Serializable]
    public struct DampedTransformData
    {
        public Transform constrainedObject;
        public Transform sourceObject;
        public float dampPosition;
        public float dampRotation;
        public bool maintainAim;
    }

    /// <summary>Makes the constrained object lag behind its source (inert in the port).</summary>
    public class DampedTransform : RigConstraint<DampedTransformData>
    {
        public override bool IsValid() => m_Data.constrainedObject != null && m_Data.sourceObject != null;
    }

    [Serializable]
    public struct MultiParentConstraintData
    {
        public Transform constrainedObject;
        public WeightedTransformArray sourceObjects;
        public bool maintainPositionOffset;
        public bool maintainRotationOffset;
        public bool constrainedPositionXAxis, constrainedPositionYAxis, constrainedPositionZAxis;
        public bool constrainedRotationXAxis, constrainedRotationYAxis, constrainedRotationZAxis;
    }

    /// <summary>Parents the constrained object to weighted sources (inert in the port).</summary>
    public class MultiParentConstraint : RigConstraint<MultiParentConstraintData>
    {
        public override bool IsValid() => m_Data.constrainedObject != null && m_Data.sourceObjects.Count > 0;
    }

    [Serializable]
    public struct MultiPositionConstraintData
    {
        public Transform constrainedObject;
        public WeightedTransformArray sourceObjects;
        public bool maintainOffset;
        public Vector3 offset;
        public bool constrainedXAxis, constrainedYAxis, constrainedZAxis;
    }

    public class MultiPositionConstraint : RigConstraint<MultiPositionConstraintData>
    {
        public override bool IsValid() => m_Data.constrainedObject != null && m_Data.sourceObjects.Count > 0;
    }

    [Serializable]
    public struct MultiRotationConstraintData
    {
        public Transform constrainedObject;
        public WeightedTransformArray sourceObjects;
        public bool maintainOffset;
        public Vector3 offset;
        public bool constrainedXAxis, constrainedYAxis, constrainedZAxis;
    }

    public class MultiRotationConstraint : RigConstraint<MultiRotationConstraintData>
    {
        public override bool IsValid() => m_Data.constrainedObject != null && m_Data.sourceObjects.Count > 0;
    }

    [Serializable]
    public struct TwoBoneIKConstraintData
    {
        public Transform root;
        public Transform mid;
        public Transform tip;
        public Transform target;
        public Transform hint;
        public float targetPositionWeight;
        public float targetRotationWeight;
        public float hintWeight;
        public bool maintainTargetPositionOffset;
        public bool maintainTargetRotationOffset;
    }

    public class TwoBoneIKConstraint : RigConstraint<TwoBoneIKConstraintData>
    {
        public override bool IsValid() => m_Data.root != null && m_Data.mid != null && m_Data.tip != null && m_Data.target != null;
    }

    [Serializable]
    public struct OverrideTransformData
    {
        public enum Space { World = 0, Local = 1, Pivot = 2 }

        public Transform constrainedObject;
        public Transform sourceObject;
        public Space space;
        public Vector3 position;
        public Vector3 rotation;
        public float positionWeight;
        public float rotationWeight;
    }

    public class OverrideTransform : RigConstraint<OverrideTransformData>
    {
        public override bool IsValid() => m_Data.constrainedObject != null;
    }

    /// <summary>A group of constraints evaluated together, blended by <see cref="weight"/>.</summary>
    public class Rig : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] protected float m_Weight = 1f;

        public float weight
        {
            get => m_Weight;
            set => m_Weight = Math.Clamp(value, 0f, 1f);
        }

        /// <summary>The constraints under this rig (collected from children, as the original does at build time).</summary>
        public IRigConstraint[] GetConstraints() => GetComponentsInChildren<IRigConstraint>(true);
    }

    /// <summary>A rig entry in a <see cref="RigBuilder"/>.</summary>
    [Serializable]
    public class RigLayer
    {
        [SerializeField] Rig m_Rig;
        [SerializeField] bool m_Active = true;

        public RigLayer(Rig rig, bool active = true) { m_Rig = rig; m_Active = active; }
        public Rig rig => m_Rig;
        public bool active { get => m_Active; set => m_Active = value; }
        public string name => m_Rig != null ? m_Rig.name : "no-name";
        public bool IsValid() => m_Rig != null;
    }

    /// <summary>
    /// Builds the rig graph on the Animator. The port has no Animator graph: <see cref="Build()"/>
    /// only validates and records that the rig was built.
    /// </summary>
    public class RigBuilder : MonoBehaviour
    {
        [SerializeField] List<RigLayer> m_RigLayers = new();

        public List<RigLayer> layers
        {
            get => m_RigLayers ??= new List<RigLayer>();
            set => m_RigLayers = value;
        }

        /// <summary>Port: true after a successful <see cref="Build()"/> until <see cref="Clear"/>.</summary>
        public bool isBuilt { get; private set; }

        public bool Build()
        {
            isBuilt = true;
            foreach (var layer in layers)
                if (layer == null || !layer.IsValid()) { isBuilt = false; break; }
            return isBuilt;
        }

        public void Clear() => isBuilt = false;
        public void Evaluate(float deltaTime) { }
        public void SyncLayers() { }
    }
}
