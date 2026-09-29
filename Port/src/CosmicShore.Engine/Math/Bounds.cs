using System;

namespace CosmicShore.Engine
{
    /// <summary>Axis-aligned bounding box (original engine contract: center + extents).</summary>
    [Serializable]
    public struct Bounds : IEquatable<Bounds>
    {
        public Vector3 center;
        Vector3 _extents;

        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            _extents = size * 0.5f;
        }

        public Vector3 extents
        {
            get => _extents;
            set => _extents = value;
        }

        public Vector3 size
        {
            get => _extents * 2f;
            set => _extents = value * 0.5f;
        }

        public Vector3 min
        {
            get => center - _extents;
            set => SetMinMax(value, max);
        }

        public Vector3 max
        {
            get => center + _extents;
            set => SetMinMax(min, value);
        }

        public void SetMinMax(Vector3 min, Vector3 max)
        {
            _extents = (max - min) * 0.5f;
            center = min + _extents;
        }

        public void Encapsulate(Vector3 point)
            => SetMinMax(Vector3.Min(min, point), Vector3.Max(max, point));

        public void Encapsulate(Bounds bounds)
        {
            Encapsulate(bounds.min);
            Encapsulate(bounds.max);
        }

        public bool Contains(Vector3 point)
        {
            Vector3 lo = min, hi = max;
            return point.x >= lo.x && point.x <= hi.x
                && point.y >= lo.y && point.y <= hi.y
                && point.z >= lo.z && point.z <= hi.z;
        }

        /// <summary>Slab-test ray intersection; distance is from the ray origin (0 when it starts inside).</summary>
        public bool IntersectRay(Ray ray, out float distance)
        {
            distance = 0f;
            float tMin = float.NegativeInfinity, tMax = float.PositiveInfinity;
            Vector3 o = ray.origin, d = ray.direction, mn = min, mx = max;
            for (int i = 0; i < 3; i++)
            {
                float oi = o[i], di = d[i];
                if (MathF.Abs(di) < 1e-12f) { if (oi < mn[i] || oi > mx[i]) return false; continue; }
                float t1 = (mn[i] - oi) / di, t2 = (mx[i] - oi) / di;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = MathF.Max(tMin, t1); tMax = MathF.Min(tMax, t2);
                if (tMin > tMax) return false;
            }
            if (tMax < 0f) return false;
            distance = tMin < 0f ? 0f : tMin;
            return true;
        }

        public bool IntersectRay(Ray ray) => IntersectRay(ray, out _);

        public Vector3 ClosestPoint(Vector3 point) => new(
            Mathf.Clamp(point.x, min.x, max.x), Mathf.Clamp(point.y, min.y, max.y), Mathf.Clamp(point.z, min.z, max.z));

        public float SqrDistance(Vector3 point) => (ClosestPoint(point) - point).sqrMagnitude;

        public void Expand(float amount) { var e = extents; e += new Vector3(amount, amount, amount) * 0.5f; extents = e; }
        public void Expand(Vector3 amount) { extents += amount * 0.5f; }

        public bool Intersects(Bounds other)
        {
            Vector3 aMin = min, aMax = max, bMin = other.min, bMax = other.max;
            return aMin.x <= bMax.x && aMax.x >= bMin.x
                && aMin.y <= bMax.y && aMax.y >= bMin.y
                && aMin.z <= bMax.z && aMax.z >= bMin.z;
        }

        public bool Equals(Bounds other) => center.Equals(other.center) && _extents.Equals(other._extents);
        public override bool Equals(object obj) => obj is Bounds other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(center, _extents);
        public static bool operator ==(Bounds a, Bounds b) => a.Equals(b);
        public static bool operator !=(Bounds a, Bounds b) => !a.Equals(b);

        public override string ToString() => $"Center: {center}, Extents: {_extents}";
    }
}
