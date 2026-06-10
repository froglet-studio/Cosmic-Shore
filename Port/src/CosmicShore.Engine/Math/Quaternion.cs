using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Rotation quaternion matching the original engine's conventions:
    /// Euler(x, y, z) composes intrinsic Y (yaw) → X (pitch) → Z (roll),
    /// i.e. q = qY * qX * qZ, with angles in degrees. All tuned rotation
    /// values from the Unity-era assets remain valid.
    /// </summary>
    [Serializable]
    public struct Quaternion : IEquatable<Quaternion>
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        public static Quaternion identity => new(0f, 0f, 0f, 1f);

        const float kEpsilon = 1E-06f;

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => new(
            lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
            lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
            lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
            lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);

        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            float x2 = rotation.x * 2f, y2 = rotation.y * 2f, z2 = rotation.z * 2f;
            float xx = rotation.x * x2, yy = rotation.y * y2, zz = rotation.z * z2;
            float xy = rotation.x * y2, xz = rotation.x * z2, yz = rotation.y * z2;
            float wx = rotation.w * x2, wy = rotation.w * y2, wz = rotation.w * z2;

            return new Vector3(
                (1f - (yy + zz)) * point.x + (xy - wz) * point.y + (xz + wy) * point.z,
                (xy + wz) * point.x + (1f - (xx + zz)) * point.y + (yz - wx) * point.z,
                (xz - wy) * point.x + (yz + wx) * point.y + (1f - (xx + yy)) * point.z);
        }

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            Vector3 n = axis.normalized;
            float half = angle * Mathf.Deg2Rad * 0.5f;
            float s = MathF.Sin(half);
            return new Quaternion(n.x * s, n.y * s, n.z * s, MathF.Cos(half));
        }

        public static Quaternion Euler(float x, float y, float z)
            => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);

        public static Quaternion Euler(Vector3 euler) => Euler(euler.x, euler.y, euler.z);

        /// <summary>Euler angles in degrees, each component in [0, 360). Inverse of <see cref="Euler(float,float,float)"/>.</summary>
        public Vector3 eulerAngles
        {
            get
            {
                Quaternion q = normalized;
                // Rotation matrix elements for R = Ry * Rx * Rz.
                float m12 = 2f * (q.y * q.z - q.w * q.x);

                float xDeg, yDeg, zDeg;
                float sinX = Mathf.Clamp(-m12, -1f, 1f);
                xDeg = MathF.Asin(sinX) * Mathf.Rad2Deg;

                if (MathF.Abs(sinX) < 0.9999995f)
                {
                    float m02 = 2f * (q.x * q.z + q.w * q.y);
                    float m22 = 1f - 2f * (q.x * q.x + q.y * q.y);
                    float m10 = 2f * (q.x * q.y + q.w * q.z);
                    float m11 = 1f - 2f * (q.x * q.x + q.z * q.z);
                    yDeg = MathF.Atan2(m02, m22) * Mathf.Rad2Deg;
                    zDeg = MathF.Atan2(m10, m11) * Mathf.Rad2Deg;
                }
                else
                {
                    // Gimbal lock: pitch at ±90°, fold roll into yaw.
                    float m20 = 2f * (q.x * q.z - q.w * q.y);
                    float m00 = 1f - 2f * (q.y * q.y + q.z * q.z);
                    yDeg = MathF.Atan2(-m20, m00) * Mathf.Rad2Deg;
                    zDeg = 0f;
                }

                return new Vector3(Wrap360(xDeg), Wrap360(yDeg), Wrap360(zDeg));
            }
        }

        static float Wrap360(float deg) => deg < 0f ? deg + 360f : deg;

        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards)
        {
            Vector3 f = forward.normalized;
            if (f.sqrMagnitude < kEpsilon) return identity;

            Vector3 r = Vector3.Cross(upwards, f).normalized;
            if (r.sqrMagnitude < kEpsilon)
            {
                // forward parallel to up: pick an arbitrary perpendicular right axis.
                r = Vector3.Cross(Vector3.forward, f).normalized;
                if (r.sqrMagnitude < kEpsilon) r = Vector3.right;
            }
            Vector3 u = Vector3.Cross(f, r);

            // Column-major basis (r, u, f) → quaternion (Shepperd's method).
            float m00 = r.x, m01 = u.x, m02 = f.x;
            float m10 = r.y, m11 = u.y, m12 = f.y;
            float m20 = r.z, m21 = u.z, m22 = f.z;

            float trace = m00 + m11 + m22;
            Quaternion q;
            if (trace > 0f)
            {
                float s = MathF.Sqrt(trace + 1f) * 2f;
                q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
                q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
                q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                float s = MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
                q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }
            return q.normalized;
        }

        public static Quaternion FromToRotation(Vector3 fromDirection, Vector3 toDirection)
        {
            Vector3 from = fromDirection.normalized;
            Vector3 to = toDirection.normalized;
            float dot = Mathf.Clamp(Vector3.Dot(from, to), -1f, 1f);

            if (dot > 1f - kEpsilon) return identity;
            if (dot < -1f + kEpsilon)
            {
                // Opposite vectors: 180° about any axis perpendicular to 'from'.
                Vector3 axis = Vector3.Cross(Vector3.right, from);
                if (axis.sqrMagnitude < kEpsilon) axis = Vector3.Cross(Vector3.up, from);
                return AngleAxis(180f, axis.normalized);
            }

            Vector3 cross = Vector3.Cross(from, to);
            float s = MathF.Sqrt((1f + dot) * 2f);
            float invS = 1f / s;
            return new Quaternion(cross.x * invS, cross.y * invS, cross.z * invS, s * 0.5f).normalized;
        }

        public static Quaternion Inverse(Quaternion rotation)
        {
            float lengthSq = Dot(rotation, rotation);
            if (lengthSq < kEpsilon) return identity;
            float inv = 1f / lengthSq;
            return new Quaternion(-rotation.x * inv, -rotation.y * inv, -rotation.z * inv, rotation.w * inv);
        }

        public Quaternion normalized
        {
            get
            {
                float mag = MathF.Sqrt(Dot(this, this));
                if (mag < Mathf.Epsilon) return identity;
                return new Quaternion(x / mag, y / mag, z / mag, w / mag);
            }
        }

        public void Normalize()
        {
            Quaternion n = normalized;
            x = n.x; y = n.y; z = n.z; w = n.w;
        }

        public static float Angle(Quaternion a, Quaternion b)
        {
            float dot = MathF.Abs(Dot(a, b));
            return IsEqualUsingDot(dot) ? 0f : MathF.Acos(Mathf.Clamp(dot, -1f, 1f)) * 2f * Mathf.Rad2Deg;
        }

        static bool IsEqualUsingDot(float dot) => dot > 1f - kEpsilon;

        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => Slerp(a, b, Mathf.Clamp01(t));

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            return SlerpUnclamped(a, b, t);
        }

        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            float dot = Dot(a, b);
            // Take the shortest arc.
            if (dot < 0f)
            {
                b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
                dot = -dot;
            }

            float t0, t1;
            if (dot > 0.9995f)
            {
                // Nearly parallel: lerp + normalize.
                t0 = 1f - t;
                t1 = t;
            }
            else
            {
                float theta = MathF.Acos(Mathf.Clamp(dot, -1f, 1f));
                float sinTheta = MathF.Sin(theta);
                t0 = MathF.Sin((1f - t) * theta) / sinTheta;
                t1 = MathF.Sin(t * theta) / sinTheta;
            }

            return new Quaternion(
                a.x * t0 + b.x * t1,
                a.y * t0 + b.y * t1,
                a.z * t0 + b.z * t1,
                a.w * t0 + b.w * t1).normalized;
        }

        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta)
        {
            float angle = Angle(from, to);
            if (angle == 0f) return to;
            return SlerpUnclamped(from, to, Mathf.Min(1f, maxDegreesDelta / angle));
        }

        public static bool operator ==(Quaternion lhs, Quaternion rhs) => IsEqualUsingDot(MathF.Abs(Dot(lhs, rhs)));
        public static bool operator !=(Quaternion lhs, Quaternion rhs) => !(lhs == rhs);

        public bool Equals(Quaternion other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override bool Equals(object obj) => obj is Quaternion other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y, z, w);
        public override string ToString() => $"({x:F5}, {y:F5}, {z:F5}, {w:F5})";

        public static implicit operator System.Numerics.Quaternion(Quaternion q) => new(q.x, q.y, q.z, q.w);
        public static implicit operator Quaternion(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    }
}
