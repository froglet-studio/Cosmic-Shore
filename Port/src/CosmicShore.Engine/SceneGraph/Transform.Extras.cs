namespace CosmicShore.Engine
{
    public partial class Transform
    {
        public Matrix4x4 localToWorldMatrix => WorldMatrix;

        public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;

        /// <summary>Local vector → world vector (rotation + scale, no translation).</summary>
        public Vector3 TransformVector(Vector3 vector) => rotation * Vector3.Scale(lossyScale, vector);
        public Vector3 TransformVector(float x, float y, float z) => TransformVector(new Vector3(x, y, z));

        public Vector3 InverseTransformVector(Vector3 vector)
        {
            var s = lossyScale;
            var v = Quaternion.Inverse(rotation) * vector;
            return new Vector3(s.x != 0f ? v.x / s.x : 0f, s.y != 0f ? v.y / s.y : 0f, s.z != 0f ? v.z / s.z : 0f);
        }

        public Vector3 InverseTransformVector(float x, float y, float z) => InverseTransformVector(new Vector3(x, y, z));
        public Vector3 TransformPoint(float x, float y, float z) => TransformPoint(new Vector3(x, y, z));
        public Vector3 InverseTransformPoint(float x, float y, float z) => InverseTransformPoint(new Vector3(x, y, z));
        public Vector3 TransformDirection(float x, float y, float z) => TransformDirection(new Vector3(x, y, z));
        public Vector3 InverseTransformDirection(float x, float y, float z) => InverseTransformDirection(new Vector3(x, y, z));

        /// <summary>Rotate about an axis through a world point, keeping the offset (original contract).</summary>
        public void RotateAround(Vector3 point, Vector3 axis, float angle)
        {
            var q = Quaternion.AngleAxis(angle, axis);
            position = point + q * (position - point);
            rotation = q * rotation;
        }

        public void LookAt(Transform target, Vector3 worldUp) => LookAt(target.position, worldUp);

        public void LookAt(Vector3 worldPosition, Vector3 worldUp)
        {
            Vector3 dir = worldPosition - position;
            if (dir.sqrMagnitude > 1E-10f) rotation = Quaternion.LookRotation(dir, worldUp);
        }

        public void Translate(float x, float y, float z, Space relativeTo = Space.Self) => Translate(new Vector3(x, y, z), relativeTo);

        public void Translate(Vector3 translation, Transform relativeTo)
        {
            if (relativeTo) position += relativeTo.TransformDirection(translation);
            else position += translation;
        }

        public int GetSiblingIndex() => parent is null ? (gameObject.scene?.GetRootGameObjects() is { } r ? IndexOfRoot(r) : 0) : parent._children.IndexOf(this);

        int IndexOfRoot(System.Collections.Generic.IReadOnlyList<GameObject> roots)
        {
            for (int i = 0; i < roots.Count; i++) if (ReferenceEquals(roots[i], gameObject)) return i;
            return 0;
        }

        public void SetSiblingIndex(int index)
        {
            if (parent is null) return;
            var list = parent._children;
            list.Remove(this);
            list.Insert(System.Math.Clamp(index, 0, list.Count), this);
        }

        public void DetachChildren()
        {
            foreach (var c in _children.ToArray()) c.SetParent(null, true);
        }

        public bool hasChanged { get; set; } = true;
        public int hierarchyCount => 1 + CountDescendants(this);
        public int hierarchyCapacity { get; set; } = 64;
        static int CountDescendants(Transform t) { int n = 0; foreach (var c in t._children) n += 1 + CountDescendants(c); return n; }

        public void GetPositionAndRotation(out Vector3 position, out Quaternion rotation) { position = this.position; rotation = this.rotation; }
        public void GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localRotation) { localPosition = this.localPosition; localRotation = this.localRotation; }
    }
}
