using System;

namespace CosmicShore.Engine
{
    internal enum ShapeKind : byte { None, Sphere, Box, Capsule }

    /// <summary>
    /// A collider resolved to world space for one physics step: a sphere, an oriented box (box
    /// and mesh colliders: a mesh is the oriented box of its local bounds) or a capsule.
    /// <see cref="Extents"/> is the half-size of the shape's world AABB (the broadphase box).
    /// </summary>
    internal struct PhysicsShape
    {
        public ShapeKind Kind;
        public Vector3 Center;      // sphere / box center, capsule midpoint
        public Vector3 Extents;     // world AABB half-size
        public Quaternion Rotation; // box orientation
        public Vector3 HalfSize;    // box half-size along its own axes
        public float Radius;        // sphere / capsule radius
        public Vector3 P0, P1;      // capsule segment
        public bool Trigger;
    }

    /// <summary>
    /// The narrow-phase maths shared by the trigger pass, the queries and the contact pass.
    /// Original-engine scaling rules: a sphere's radius scales by the largest |lossyScale|
    /// component; a box by |lossyScale| per axis; a capsule's radius by the larger of its two
    /// cross-axis scales and its height by the axis scale.
    /// </summary>
    internal static class ShapeMath
    {
        /// <summary>Bitwise equality of two shapes (diagnostics and tests: a cached shape against a fresh build).</summary>
        public static bool Same(in PhysicsShape a, in PhysicsShape b)
            => a.Kind == b.Kind && a.Trigger == b.Trigger
            && Same(a.Center, b.Center) && Same(a.Extents, b.Extents) && Same(a.HalfSize, b.HalfSize)
            && Same(a.P0, b.P0) && Same(a.P1, b.P1) && Same(a.Radius, b.Radius)
            && Same(a.Rotation.x, b.Rotation.x) && Same(a.Rotation.y, b.Rotation.y) && Same(a.Rotation.z, b.Rotation.z) && Same(a.Rotation.w, b.Rotation.w);

        static bool Same(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        static bool Same(in Vector3 a, in Vector3 b) => Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z);

        public static bool TryBuild(Collider c, out PhysicsShape sh)
        {
            sh = default;
            sh.Trigger = c.isTrigger;
            switch (c)
            {
                case SphereCollider sphere:
                    sh.Kind = ShapeKind.Sphere;
                    sh.Center = sphere.transform.TransformPoint(sphere.center);
                    sh.Radius = WorldRadius(sphere);
                    sh.Extents = new Vector3(sh.Radius, sh.Radius, sh.Radius);
                    return true;
                case BoxCollider box:
                {
                    var t = box.transform;
                    Vector3 s = t.lossyScale;
                    SetBox(ref sh, t.TransformPoint(box.center), t.rotation,
                        new Vector3(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y), Mathf.Abs(box.size.z * s.z)) * 0.5f);
                    return true;
                }
                case MeshCollider mesh:
                {
                    var shared = mesh.sharedMesh;
                    if (shared is null || shared.IsDestroyed) return false;
                    var t = mesh.transform;
                    Bounds local = shared.bounds;
                    Vector3 s = t.lossyScale;
                    SetBox(ref sh, t.TransformPoint(local.center), t.rotation,
                        new Vector3(Mathf.Abs(local.extents.x * s.x), Mathf.Abs(local.extents.y * s.y), Mathf.Abs(local.extents.z * s.z)));
                    return true;
                }
                case CapsuleCollider capsule:
                {
                    var t = capsule.transform;
                    Vector3 s = t.lossyScale;
                    float ax = Mathf.Abs(s.x), ay = Mathf.Abs(s.y), az = Mathf.Abs(s.z);
                    int dir = capsule.direction is >= 0 and <= 2 ? capsule.direction : 1;
                    float axisScale = dir == 0 ? ax : dir == 1 ? ay : az;
                    float crossScale = dir == 0 ? Mathf.Max(ay, az) : dir == 1 ? Mathf.Max(ax, az) : Mathf.Max(ax, ay);
                    float r = Mathf.Abs(capsule.radius) * crossScale;
                    float half = Mathf.Max(0f, Mathf.Abs(capsule.height) * axisScale * 0.5f - r);
                    Vector3 localAxis = dir == 0 ? Vector3.right : dir == 1 ? Vector3.up : Vector3.forward;
                    Vector3 axis = t.rotation * localAxis;
                    SetCapsule(ref sh, t.TransformPoint(capsule.center) - axis * half, t.TransformPoint(capsule.center) + axis * half, r);
                    return true;
                }
                default:
                    return false;
            }
        }

        public static void SetBox(ref PhysicsShape sh, Vector3 center, Quaternion rotation, Vector3 halfSize)
        {
            sh.Kind = ShapeKind.Box;
            sh.Center = center;
            sh.Rotation = Normalized(rotation);
            sh.HalfSize = halfSize;
            Axes(sh.Rotation, out var ux, out var uy, out var uz);
            sh.Extents = new Vector3(
                MathF.Abs(ux.x) * halfSize.x + MathF.Abs(uy.x) * halfSize.y + MathF.Abs(uz.x) * halfSize.z,
                MathF.Abs(ux.y) * halfSize.x + MathF.Abs(uy.y) * halfSize.y + MathF.Abs(uz.y) * halfSize.z,
                MathF.Abs(ux.z) * halfSize.x + MathF.Abs(uy.z) * halfSize.y + MathF.Abs(uz.z) * halfSize.z);
        }

        public static void SetCapsule(ref PhysicsShape sh, Vector3 p0, Vector3 p1, float radius)
        {
            sh.Kind = ShapeKind.Capsule;
            sh.P0 = p0;
            sh.P1 = p1;
            sh.Radius = radius;
            sh.Center = (p0 + p1) * 0.5f;
            var h = new Vector3(MathF.Abs(p1.x - p0.x), MathF.Abs(p1.y - p0.y), MathF.Abs(p1.z - p0.z)) * 0.5f;
            sh.Extents = h + new Vector3(radius, radius, radius);
        }

        public static void SetSphere(ref PhysicsShape sh, Vector3 center, float radius)
        {
            sh.Kind = ShapeKind.Sphere;
            sh.Center = center;
            sh.Radius = radius;
            sh.Extents = new Vector3(radius, radius, radius);
        }

        /// <summary>Original-engine sphere scaling: radius × max |lossyScale| component.</summary>
        public static float WorldRadius(SphereCollider sphere)
        {
            Vector3 s = sphere.transform.lossyScale;
            return sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        /// <summary>The identity for an unset (all-zero) quaternion, as the original engine treats `default`.</summary>
        public static Quaternion Normalized(Quaternion q)
        {
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (n < 1e-12f) return Quaternion.identity;
            if (MathF.Abs(n - 1f) < 1e-6f) return q;
            float inv = 1f / MathF.Sqrt(n);
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }

        static void Axes(Quaternion q, out Vector3 ux, out Vector3 uy, out Vector3 uz)
        {
            ux = q * Vector3.right;
            uy = q * Vector3.up;
            uz = q * Vector3.forward;
        }

        static Vector3 ToLocal(in PhysicsShape box, Vector3 p) => Quaternion.Inverse(box.Rotation) * (p - box.Center);
        static Vector3 ToWorld(in PhysicsShape box, Vector3 p) => box.Center + box.Rotation * p;

        static Vector3 ClampToBox(Vector3 local, Vector3 h) => new(
            Mathf.Clamp(local.x, -h.x, h.x), Mathf.Clamp(local.y, -h.y, h.y), Mathf.Clamp(local.z, -h.z, h.z));

        public static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return a + ab * t;
        }

        // ── Closest point ────────────────────────────────────────────

        /// <summary>The point on or in the shape nearest to <paramref name="p"/> (p itself when inside).</summary>
        public static Vector3 ClosestPoint(in PhysicsShape sh, Vector3 p)
        {
            switch (sh.Kind)
            {
                case ShapeKind.Sphere:
                {
                    var d = p - sh.Center;
                    float m = d.magnitude;
                    return m <= sh.Radius ? p : sh.Center + d * (sh.Radius / m);
                }
                case ShapeKind.Box:
                    return ToWorld(in sh, ClampToBox(ToLocal(in sh, p), sh.HalfSize));
                case ShapeKind.Capsule:
                {
                    var c = ClosestOnSegment(sh.P0, sh.P1, p);
                    var d = p - c;
                    float m = d.magnitude;
                    return m <= sh.Radius ? p : c + d * (sh.Radius / m);
                }
                default:
                    return p;
            }
        }

        // ── Overlap ──────────────────────────────────────────────────

        public static bool Overlap(in PhysicsShape a, in PhysicsShape b)
        {
            if (a.Kind == ShapeKind.None || b.Kind == ShapeKind.None) return false;
            if (a.Kind == ShapeKind.Sphere) return SphereOverlaps(in b, a.Center, a.Radius);
            if (b.Kind == ShapeKind.Sphere) return SphereOverlaps(in a, b.Center, b.Radius);
            if (a.Kind == ShapeKind.Capsule) return CapsuleOverlaps(in b, a.P0, a.P1, a.Radius);
            if (b.Kind == ShapeKind.Capsule) return CapsuleOverlaps(in a, b.P0, b.P1, b.Radius);
            return BoxBox(in a, in b);
        }

        /// <summary>Does a sphere (center, radius) touch the shape?</summary>
        public static bool SphereOverlaps(in PhysicsShape sh, Vector3 center, float radius)
        {
            switch (sh.Kind)
            {
                case ShapeKind.Sphere:
                {
                    float r = radius + sh.Radius;
                    return (center - sh.Center).sqrMagnitude <= r * r;
                }
                case ShapeKind.Box:
                {
                    var local = ToLocal(in sh, center);
                    return (local - ClampToBox(local, sh.HalfSize)).sqrMagnitude <= radius * radius;
                }
                case ShapeKind.Capsule:
                {
                    float r = radius + sh.Radius;
                    return (center - ClosestOnSegment(sh.P0, sh.P1, center)).sqrMagnitude <= r * r;
                }
                default:
                    return false;
            }
        }

        /// <summary>Does a capsule (segment p0-p1, radius) touch the shape?</summary>
        public static bool CapsuleOverlaps(in PhysicsShape sh, Vector3 p0, Vector3 p1, float radius)
        {
            switch (sh.Kind)
            {
                case ShapeKind.Sphere:
                {
                    float r = radius + sh.Radius;
                    return (sh.Center - ClosestOnSegment(p0, p1, sh.Center)).sqrMagnitude <= r * r;
                }
                case ShapeKind.Capsule:
                {
                    float r = radius + sh.Radius;
                    return SegmentSegmentSqr(p0, p1, sh.P0, sh.P1) <= r * r;
                }
                case ShapeKind.Box:
                    return SegmentBoxSqr(in sh, p0, p1) <= radius * radius;
                default:
                    return false;
            }
        }

        /// <summary>Separating-axis test between two oriented boxes (3 + 3 face axes, 9 edge cross axes).</summary>
        public static bool BoxBox(in PhysicsShape a, in PhysicsShape b)
        {
            Axes(a.Rotation, out var a0, out var a1, out var a2);
            Axes(b.Rotation, out var b0, out var b1, out var b2);
            Span<Vector3> A = stackalloc Vector3[3] { a0, a1, a2 };
            Span<Vector3> B = stackalloc Vector3[3] { b0, b1, b2 };
            Vector3 ea = a.HalfSize, eb = b.HalfSize;
            Vector3 d = b.Center - a.Center;
            const float eps = 1e-6f;

            Span<float> R = stackalloc float[9], AbsR = stackalloc float[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    R[i * 3 + j] = Vector3.Dot(A[i], B[j]);
                    AbsR[i * 3 + j] = MathF.Abs(R[i * 3 + j]) + eps;
                }
            var t = new Vector3(Vector3.Dot(d, a0), Vector3.Dot(d, a1), Vector3.Dot(d, a2));
            float ta(int i) => i == 0 ? t.x : i == 1 ? t.y : t.z;
            float e(Vector3 v, int i) => i == 0 ? v.x : i == 1 ? v.y : v.z;

            for (int i = 0; i < 3; i++)
            {
                float rb = e(eb, 0) * AbsR[i * 3] + e(eb, 1) * AbsR[i * 3 + 1] + e(eb, 2) * AbsR[i * 3 + 2];
                if (MathF.Abs(ta(i)) > e(ea, i) + rb) return false;
            }
            for (int j = 0; j < 3; j++)
            {
                float ra = e(ea, 0) * AbsR[j] + e(ea, 1) * AbsR[3 + j] + e(ea, 2) * AbsR[6 + j];
                float tb = t.x * R[j] + t.y * R[3 + j] + t.z * R[6 + j];
                if (MathF.Abs(tb) > ra + e(eb, j)) return false;
            }
            for (int i = 0; i < 3; i++)
            {
                int i1 = (i + 1) % 3, i2 = (i + 2) % 3;
                for (int j = 0; j < 3; j++)
                {
                    int j1 = (j + 1) % 3, j2 = (j + 2) % 3;
                    float ra = e(ea, i1) * AbsR[i2 * 3 + j] + e(ea, i2) * AbsR[i1 * 3 + j];
                    float rb = e(eb, j1) * AbsR[i * 3 + j2] + e(eb, j2) * AbsR[i * 3 + j1];
                    float tl = ta(i2) * R[i1 * 3 + j] - ta(i1) * R[i2 * 3 + j];
                    if (MathF.Abs(tl) > ra + rb) return false;
                }
            }
            return true;
        }

        /// <summary>Squared distance between two segments (0 when they intersect).</summary>
        public static float SegmentSegmentSqr(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = d1.sqrMagnitude, e = d2.sqrMagnitude, f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-12f && e <= 1e-12f) return r.sqrMagnitude;
            if (a <= 1e-12f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-12f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-12f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return (p1 + d1 * s - (p2 + d2 * t)).sqrMagnitude;
        }

        /// <summary>
        /// Squared distance from a segment to an oriented box. The distance from a convex set is
        /// convex along a line, so a golden-section search over the segment parameter finds its
        /// minimum; a segment that crosses the box (a slab hit) is distance 0.
        /// </summary>
        public static float SegmentBoxSqr(in PhysicsShape box, Vector3 p0, Vector3 p1)
        {
            Vector3 a = ToLocal(in box, p0), b = ToLocal(in box, p1), h = box.HalfSize;
            var dir = b - a;
            float len = dir.magnitude;
            if (len > 1e-9f && SlabHit(a, dir / len, h, out float tHit, out _) && tHit <= len) return 0f;
            float Dist(float s) { var p = a + dir * s; return (p - ClampToBox(p, h)).sqrMagnitude; }
            float lo = 0f, hi = 1f;
            const float g = 0.618034f;
            float x1 = hi - g * (hi - lo), x2 = lo + g * (hi - lo);
            float f1 = Dist(x1), f2 = Dist(x2);
            for (int k = 0; k < 40; k++)
            {
                if (f1 <= f2) { hi = x2; x2 = x1; f2 = f1; x1 = hi - g * (hi - lo); f1 = Dist(x1); }
                else { lo = x1; x1 = x2; f1 = f2; x2 = lo + g * (hi - lo); f2 = Dist(x2); }
            }
            return MathF.Min(MathF.Min(Dist(0f), Dist(1f)), MathF.Min(f1, f2));
        }

        // ── Rays ─────────────────────────────────────────────────────

        /// <summary>Ray (unit direction) against the shape. A ray starting inside hits at distance 0.</summary>
        public static bool Raycast(in PhysicsShape sh, Vector3 o, Vector3 d, out float dist, out Vector3 normal)
        {
            dist = 0f; normal = -d;
            switch (sh.Kind)
            {
                case ShapeKind.Sphere:
                    return RaySphere(o, d, sh.Center, sh.Radius, out dist, out normal);
                case ShapeKind.Box:
                {
                    var lo = ToLocal(in sh, o);
                    var ld = Quaternion.Inverse(sh.Rotation) * d;
                    if (!SlabHit(lo, ld, sh.HalfSize, out dist, out var ln)) return false;
                    normal = sh.Rotation * ln;
                    return true;
                }
                case ShapeKind.Capsule:
                    return RayCapsule(o, d, sh.P0, sh.P1, sh.Radius, out dist, out normal);
                default:
                    return false;
            }
        }

        static bool RaySphere(Vector3 o, Vector3 d, Vector3 c, float r, out float dist, out Vector3 normal)
        {
            dist = 0f; normal = -d;
            var oc = o - c;
            float b = Vector3.Dot(oc, d), cc = oc.sqrMagnitude - r * r;
            if (cc > 0f && b > 0f) return false;
            float disc = b * b - cc;
            if (disc < 0f) return false;
            dist = MathF.Max(0f, -b - MathF.Sqrt(disc));
            normal = (o + d * dist - c).normalized;
            return true;
        }

        /// <summary>Slab test against a box centred at the origin; the normal is the entered face's axis.</summary>
        static bool SlabHit(Vector3 o, Vector3 d, Vector3 h, out float dist, out Vector3 normal)
        {
            dist = 0f; normal = -d;
            float tmin = 0f, tmax = float.PositiveInfinity;
            int axis = -1; float sign = 0f;
            for (int i = 0; i < 3; i++)
            {
                float oi = i == 0 ? o.x : i == 1 ? o.y : o.z;
                float di = i == 0 ? d.x : i == 1 ? d.y : d.z;
                float hi = i == 0 ? h.x : i == 1 ? h.y : h.z;
                if (MathF.Abs(di) < 1e-12f)
                {
                    if (oi < -hi || oi > hi) return false;
                    continue;
                }
                float inv = 1f / di;
                float t1 = (-hi - oi) * inv, t2 = (hi - oi) * inv;
                float s = -1f;
                if (t1 > t2) { (t1, t2) = (t2, t1); s = 1f; }
                if (t1 > tmin) { tmin = t1; axis = i; sign = s; }
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) return false;
            }
            dist = tmin;
            if (axis >= 0)
                normal = axis == 0 ? new Vector3(sign, 0, 0) : axis == 1 ? new Vector3(0, sign, 0) : new Vector3(0, 0, sign);
            return true;
        }

        static bool RayCapsule(Vector3 o, Vector3 d, Vector3 p0, Vector3 p1, float r, out float dist, out Vector3 normal)
        {
            dist = float.PositiveInfinity; normal = -d;
            bool hit = false;
            // Inside: distance 0.
            if ((o - ClosestOnSegment(p0, p1, o)).sqrMagnitude <= r * r) { dist = 0f; return true; }
            // The cylinder body.
            var axis = p1 - p0;
            float len = axis.magnitude;
            if (len > 1e-9f)
            {
                var u = axis / len;
                var m = o - p0;
                var dp = d - u * Vector3.Dot(d, u);
                var mp = m - u * Vector3.Dot(m, u);
                float a = dp.sqrMagnitude, b = 2f * Vector3.Dot(dp, mp), c = mp.sqrMagnitude - r * r;
                if (a > 1e-12f)
                {
                    float disc = b * b - 4f * a * c;
                    if (disc >= 0f)
                    {
                        float t = (-b - MathF.Sqrt(disc)) / (2f * a);
                        if (t >= 0f)
                        {
                            float along = Vector3.Dot(m + d * t, u);
                            if (along >= 0f && along <= len)
                            {
                                dist = t; hit = true;
                                var p = o + d * t;
                                normal = (p - (p0 + u * along)).normalized;
                            }
                        }
                    }
                }
            }
            // The end caps.
            if (RaySphere(o, d, p0, r, out float t0, out var n0) && t0 < dist) { dist = t0; normal = n0; hit = true; }
            if (RaySphere(o, d, p1, r, out float t1, out var n1) && t1 < dist) { dist = t1; normal = n1; hit = true; }
            if (!hit) dist = 0f;
            return hit;
        }

        // ── Sphere contact (the contact pass) ────────────────────────

        /// <summary>
        /// Contact between a sphere (center, radius) and a shape: <paramref name="normal"/> points
        /// from the shape toward the sphere, <paramref name="depth"/> is the penetration (≥ 0 when
        /// touching) and <paramref name="point"/> the contact on the shape's surface.
        /// </summary>
        public static bool SphereContact(Vector3 center, float radius, in PhysicsShape other, out Vector3 normal, out float depth, out Vector3 point)
        {
            normal = Vector3.up; depth = 0f; point = center;
            switch (other.Kind)
            {
                case ShapeKind.Sphere:
                    return RoundContact(center, radius, other.Center, other.Radius, out normal, out depth, out point);
                case ShapeKind.Capsule:
                    return RoundContact(center, radius, ClosestOnSegment(other.P0, other.P1, center), other.Radius, out normal, out depth, out point);
                case ShapeKind.Box:
                {
                    var local = ToLocal(in other, center);
                    var h = other.HalfSize;
                    var clamped = ClampToBox(local, h);
                    var diff = local - clamped;
                    float d2 = diff.sqrMagnitude;
                    if (d2 > radius * radius) return false;
                    Vector3 localNormal;
                    if (d2 > 1e-12f)
                    {
                        float dd = MathF.Sqrt(d2);
                        localNormal = diff / dd;
                        depth = radius - dd;
                    }
                    else
                    {
                        // Center inside the box: leave through the nearest face.
                        float dx = h.x - MathF.Abs(local.x), dy = h.y - MathF.Abs(local.y), dz = h.z - MathF.Abs(local.z);
                        if (dx <= dy && dx <= dz) { localNormal = new Vector3(local.x >= 0 ? 1 : -1, 0, 0); depth = radius + dx; clamped.x = h.x * localNormal.x; }
                        else if (dy <= dz) { localNormal = new Vector3(0, local.y >= 0 ? 1 : -1, 0); depth = radius + dy; clamped.y = h.y * localNormal.y; }
                        else { localNormal = new Vector3(0, 0, local.z >= 0 ? 1 : -1); depth = radius + dz; clamped.z = h.z * localNormal.z; }
                    }
                    normal = other.Rotation * localNormal;
                    point = ToWorld(in other, clamped);
                    return true;
                }
                default:
                    return false;
            }
        }

        static bool RoundContact(Vector3 c, float r, Vector3 oc, float or, out Vector3 normal, out float depth, out Vector3 point)
        {
            var d = c - oc;
            float m = d.magnitude, sum = r + or;
            normal = Vector3.up; depth = 0f; point = c;
            if (m > sum) return false;
            normal = m > 1e-9f ? d / m : Vector3.up;
            depth = sum - m;
            point = oc + normal * or;
            return true;
        }
    }
}
