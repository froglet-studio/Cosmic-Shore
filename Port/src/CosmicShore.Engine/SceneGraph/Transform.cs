using System.Collections;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>A transform's local → world point mapping, captured once (<see cref="Transform.PointToWorld"/>).</summary>
    public readonly struct PointToWorldMap
    {
        readonly Vector3 _position, _scale;
        readonly Quaternion _rotation;

        internal PointToWorldMap(Vector3 position, Quaternion rotation, Vector3 scale)
            => (_position, _rotation, _scale) = (position, rotation, scale);

        public Vector3 Apply(Vector3 point) => _position + _rotation * Vector3.Scale(_scale, point);
    }

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
    /// properties rather than fields. <see cref="localRotation"/> is a plain property (it
    /// was a field; it became a property so a write can report a pose change).
    /// </summary>
    public partial class Transform : Component, IEnumerable
    {
        readonly List<Transform> _children = new();

        Vector3 _localPosition = Vector3.zero;
        Quaternion _localRotation = Quaternion.identity;
        Vector3 _localScale = Vector3.one;

        // Every local write reports a possible world-pose change (MarkMoved: a no-op unless a
        // render backend tracks changes; see RenderChangeTracking).
        public virtual Vector3 localPosition { get => _localPosition; set { _localPosition = value; MarkMoved(); } }
        public Quaternion localRotation { get => _localRotation; set { _localRotation = value; MarkMoved(); } }
        public virtual Vector3 localScale { get => _localScale; set { _localScale = value; MarkMoved(); } }

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

        /// <summary>Move to the end of the parent's child list (original contract: renders last / laid out last). No-op at the root.</summary>
        public void SetAsLastSibling()
        {
            if (_parent is null) return;
            _parent._children.Remove(this);
            _parent._children.Add(this);
        }

        /// <summary>Move to the front of the parent's child list (original contract: renders first / laid out first). No-op at the root.</summary>
        public void SetAsFirstSibling()
        {
            if (_parent is null) return;
            _parent._children.Remove(this);
            _parent._children.Insert(0, this);
        }

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

        /// <summary>
        /// True when this transform's world pose is DRIVEN rather than composed through its
        /// parent — a root screen-space Canvas (original contract: the engine places an overlay
        /// canvas over the screen whatever it is parented under, e.g. a vessel HUD canvas that
        /// lives inside the 3D vessel). World math then treats it as a root.
        /// </summary>
        internal virtual bool IsWorldRoot => false;

        /// <summary>The world rotation of a driven root (identity for a screen-space canvas).</summary>
        internal virtual Quaternion DrivenWorldRotation => localRotation;

        /// <summary>The parent world math composes through (null for a driven root).</summary>
        Transform WorldParent => IsWorldRoot ? null : parent;

        Quaternion SelfRotation => IsWorldRoot ? DrivenWorldRotation : localRotation;

        public Vector3 position
        {
            get => WorldPosition;
            set => localPosition = WorldParent is null ? value : WorldParent.InverseTransformPoint(value);
        }

        public Quaternion rotation
        {
            get => WorldRotation;
            set
            {
                if (IsWorldRoot) return; // driven
                localRotation = parent is null ? value : Quaternion.Inverse(parent.rotation) * value;
            }
        }

        public Vector3 lossyScale => WorldLossyScale;

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
        public Vector3 TransformPoint(Vector3 point) => PointToWorld.Apply(point);

        /// <summary>
        /// Port engine extension: <see cref="TransformPoint"/> with the world pose read once, for
        /// mapping many points (a UI mesh's vertices). Each pose read re-validates the parent chain,
        /// and a RectTransform's re-derives its anchored position, so per point that was three chain
        /// walks. Bit-identical to calling TransformPoint per point while the pose holds.
        /// </summary>
        public PointToWorldMap PointToWorld => new(position, rotation, lossyScale);

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
            if (newParent is not null && newParent.IsChildOf(this))
            {
                // Original contract: a transform cannot become a child of its own descendant.
                Debug.LogError($"Cannot set the parent of '{name}' to its own child '{newParent.name}'.");
                if (System.Environment.GetEnvironmentVariable("CS_PORT_TRACE_PARENT") == "1")
                    System.Console.WriteLine($"[trace-parent] {System.Environment.StackTrace}");
                return;
            }

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
            gameObject.RefreshHierarchyActive();
            MarkMoved();

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
            ReleaseWorldCacheForDestroy();
            gameObject.RefreshHierarchyActive();
            MarkMoved();
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
            // The GameObject, its parent and its children's objects are the same before and
            // after: every effective activity is unchanged (and the GameObject still points at
            // the OLD transform here, so it must not be recomputed through it).
            MarkMoved();

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
