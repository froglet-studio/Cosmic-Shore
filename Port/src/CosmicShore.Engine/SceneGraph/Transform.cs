using System.Collections;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    public enum Space
    {
        World = 0,
        Self = 1,
    }

    /// <summary>
    /// Position/rotation/scale + hierarchy. Local values are authoritative; world values
    /// compose through the parent chain (TRS, no shear — lossyScale is the componentwise
    /// approximation, same caveat as the original engine).
    ///
    /// Unsealed for <see cref="RectTransform"/> (the UI geometry core), which derives
    /// <see cref="localPosition"/> (and, when driven by a root Canvas,
    /// <see cref="localScale"/>) from its anchor state — hence those two are virtual
    /// properties rather than fields. <see cref="localRotation"/> stays a plain field:
    /// nothing drives it.
    /// </summary>
    public class Transform : Component, IEnumerable
    {
        readonly List<Transform> _children = new();

        public virtual Vector3 localPosition { get; set; } = Vector3.zero;
        public Quaternion localRotation = Quaternion.identity;
        public virtual Vector3 localScale { get; set; } = Vector3.one;

        Transform _parent;

        /// <summary>Assignment reparents keeping the world pose (original engine contract); use SetParent for control.</summary>
        public Transform parent
        {
            get => _parent;
            set => SetParent(value);
        }

        public int childCount => _children.Count;
        public Transform GetChild(int index) => _children[index];
        public IEnumerator GetEnumerator() => _children.GetEnumerator();

        /// <summary>
        /// Finds a child by name (original engine contract: direct children only, with
        /// '/'-separated paths descending one level per segment). Returns null when no
        /// child matches.
        /// </summary>
        public Transform Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            int slash = name.IndexOf('/');
            string head = slash < 0 ? name : name[..slash];

            foreach (var child in _children)
            {
                if (child.gameObject.name != head) continue;
                return slash < 0 ? child : child.Find(name[(slash + 1)..]);
            }

            return null;
        }

        public Vector3 position
        {
            get => parent is null ? localPosition : parent.TransformPoint(localPosition);
            set => localPosition = parent is null ? value : parent.InverseTransformPoint(value);
        }

        public Quaternion rotation
        {
            get => parent is null ? localRotation : parent.rotation * localRotation;
            set => localRotation = parent is null ? value : Quaternion.Inverse(parent.rotation) * value;
        }

        public Vector3 lossyScale
            => parent is null ? localScale : Vector3.Scale(parent.lossyScale, localScale);

        public Vector3 forward
        {
            get => rotation * Vector3.forward;
            set => rotation = Quaternion.LookRotation(value);
        }

        public Vector3 up
        {
            get => rotation * Vector3.up;
            set => rotation = Quaternion.FromToRotation(Vector3.up, value);
        }

        public Vector3 right
        {
            get => rotation * Vector3.right;
            set => rotation = Quaternion.FromToRotation(Vector3.right, value);
        }

        public Vector3 eulerAngles
        {
            get => rotation.eulerAngles;
            set => rotation = Quaternion.Euler(value);
        }

        public Vector3 localEulerAngles
        {
            get => localRotation.eulerAngles;
            set => localRotation = Quaternion.Euler(value);
        }

        public Transform root => parent is null ? this : parent.root;

        /// <summary>Local point → world point (includes scale).</summary>
        public Vector3 TransformPoint(Vector3 point)
            => position + rotation * Vector3.Scale(lossyScale, point);

        /// <summary>World point → local point.</summary>
        public Vector3 InverseTransformPoint(Vector3 point)
        {
            Vector3 scale = lossyScale;
            Vector3 unrotated = Quaternion.Inverse(rotation) * (point - position);
            return new Vector3(
                scale.x != 0f ? unrotated.x / scale.x : 0f,
                scale.y != 0f ? unrotated.y / scale.y : 0f,
                scale.z != 0f ? unrotated.z / scale.z : 0f);
        }

        /// <summary>Local direction → world direction (no scale, no translation).</summary>
        public Vector3 TransformDirection(Vector3 direction) => rotation * direction;

        public Vector3 InverseTransformDirection(Vector3 direction) => Quaternion.Inverse(rotation) * direction;

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }

        public void SetLocalPositionAndRotation(Vector3 localPosition, Quaternion localRotation)
        {
            this.localPosition = localPosition;
            this.localRotation = localRotation;
        }

        public void Translate(Vector3 translation, Space relativeTo = Space.Self)
        {
            if (relativeTo == Space.Self) position += TransformDirection(translation);
            else position += translation;
        }

        public void Rotate(Vector3 eulers, Space relativeTo = Space.Self)
        {
            var delta = Quaternion.Euler(eulers);
            if (relativeTo == Space.Self) localRotation *= delta;
            else rotation = delta * rotation;
        }

        public void Rotate(float xAngle, float yAngle, float zAngle, Space relativeTo = Space.Self)
            => Rotate(new Vector3(xAngle, yAngle, zAngle), relativeTo);

        public void Rotate(Vector3 axis, float angle, Space relativeTo = Space.Self)
        {
            var delta = Quaternion.AngleAxis(angle, axis);
            if (relativeTo == Space.Self) localRotation *= delta;
            else rotation = delta * rotation;
        }

        public void LookAt(Transform target) => LookAt(target.position);

        public void LookAt(Vector3 worldPosition)
        {
            Vector3 dir = worldPosition - position;
            if (dir.sqrMagnitude > 1E-10f) rotation = Quaternion.LookRotation(dir);
        }

        public void SetParent(Transform newParent, bool worldPositionStays = true)
        {
            if (newParent == this || ReferenceEquals(newParent, parent)) return;

            bool wasActive = gameObject.activeInHierarchy;
            Vector3 worldPos = default;
            Quaternion worldRot = default;
            if (worldPositionStays)
            {
                worldPos = position;
                worldRot = rotation;
            }

            parent?._children.Remove(this);
            if (parent is null) gameObject.scene?.RemoveRoot(gameObject);

            _parent = newParent;

            if (newParent is not null) newParent._children.Add(this);
            else gameObject.scene?.AddRoot(gameObject);

            if (worldPositionStays)
            {
                position = worldPos;
                rotation = worldRot;
            }

            bool isActive = gameObject.activeInHierarchy;
            if (wasActive != isActive) gameObject.NotifyHierarchyActiveChanged(isActive);
        }

        public bool IsChildOf(Transform potentialAncestor)
        {
            for (Transform t = this; t is not null; t = t.parent)
                if (ReferenceEquals(t, potentialAncestor)) return true;
            return false;
        }

        internal IReadOnlyList<Transform> Children => _children;

        /// <summary>Detach from the parent's child list during GameObject destruction.</summary>
        internal void SetParentForDestroy()
        {
            parent?._children.Remove(this);
            _parent = null;
        }

        /// <summary>
        /// Transform→RectTransform conversion support (original contract: adding a
        /// RectTransform replaces the GameObject's Transform in place). Transplants the
        /// old transform's hierarchy position — parent slot (same child index), children
        /// (same order, reparented in place) — and local pose onto this instance, then
        /// retires the old transform. Hierarchy first, pose last: a RectTransform's
        /// localPosition setter back-solves anchoredPosition against the REAL parent
        /// rect, so the world pose survives the conversion exactly.
        /// </summary>
        internal void AdoptHierarchyFrom(Transform old)
        {
            Vector3 oldLocalPosition = old.localPosition;
            Quaternion oldLocalRotation = old.localRotation;
            Vector3 oldLocalScale = old.localScale;

            // Parent slot, preserving sibling order.
            _parent = old._parent;
            if (_parent is not null)
            {
                int slot = _parent._children.IndexOf(old);
                if (slot >= 0) _parent._children[slot] = this;
                else _parent._children.Add(this); // defensive (should not happen)
            }

            // Children, preserving order.
            foreach (var child in old._children)
            {
                child._parent = this;
                _children.Add(child);
            }
            old._children.Clear();
            old._parent = null;

            localRotation = oldLocalRotation;
            localScale = oldLocalScale;
            localPosition = oldLocalPosition;

            old.destroyedFlag = true;
        }

        internal override void DestroyComponentNow()
        {
            // Transforms are destroyed with their GameObject, never alone.
            destroyedFlag = true;
        }
    }
}
