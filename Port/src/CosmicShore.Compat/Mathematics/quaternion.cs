using System;
using System.Globalization;

namespace Unity.Mathematics
{
    /// <summary>Euler rotation order: the order in which the axis rotations are APPLIED.
    /// Unity's default (and UnityEngine.Quaternion.Euler's) is ZXY: z first, then x, then y.</summary>
    public enum RotationOrder : byte
    {
        XYZ = 0,
        XZY = 1,
        YXZ = 2,
        YZX = 3,
        ZXY = 4,
        ZYX = 5,
        Default = ZXY,
    }

    /// <summary>A unit quaternion stored as <c>value = (x, y, z, w)</c>, w the scalar part —
    /// the same layout as the engine's <c>Quaternion</c>, so conversion is a field copy.</summary>
    [Serializable]
    public partial struct quaternion : IEquatable<quaternion>, IFormattable
    {
        public float4 value;

        public static readonly quaternion identity = new quaternion(0f, 0f, 0f, 1f);

        public quaternion(float x, float y, float z, float w) { value = new float4(x, y, z, w); }
        public quaternion(float4 value) { this.value = value; }

        /// <summary>From an orthonormal rotation matrix (columns are the rotated basis vectors).</summary>
        public quaternion(float3x3 m)
        {
            float3 u = m.c0, v = m.c1, w = m.c2;
            float trace = u.x + v.y + w.z;
            if (trace > 0f)
            {
                float s = MathF.Sqrt(trace + 1f) * 2f;
                value = new float4((v.z - w.y) / s, (w.x - u.z) / s, (u.y - v.x) / s, 0.25f * s);
            }
            else if (u.x > v.y && u.x > w.z)
            {
                float s = MathF.Sqrt(1f + u.x - v.y - w.z) * 2f;
                value = new float4(0.25f * s, (v.x + u.y) / s, (w.x + u.z) / s, (v.z - w.y) / s);
            }
            else if (v.y > w.z)
            {
                float s = MathF.Sqrt(1f + v.y - u.x - w.z) * 2f;
                value = new float4((v.x + u.y) / s, 0.25f * s, (w.y + v.z) / s, (w.x - u.z) / s);
            }
            else
            {
                float s = MathF.Sqrt(1f + w.z - u.x - v.y) * 2f;
                value = new float4((w.x + u.z) / s, (w.y + v.z) / s, 0.25f * s, (u.y - v.x) / s);
            }
            value = math.normalize(value);
        }

        /// <summary>From the rotation part of a 4x4 matrix.</summary>
        public quaternion(float4x4 m) : this(new float3x3(m.c0.xyz, m.c1.xyz, m.c2.xyz)) { }

        public static implicit operator quaternion(float4 v) => new quaternion(v);

        public static quaternion AxisAngle(float3 axis, float angle)
        {
            math.sincos(0.5f * angle, out float s, out float c);
            return new quaternion(new float4(axis * s, c));
        }

        public static quaternion RotateX(float angle) { math.sincos(0.5f * angle, out float s, out float c); return new quaternion(s, 0f, 0f, c); }
        public static quaternion RotateY(float angle) { math.sincos(0.5f * angle, out float s, out float c); return new quaternion(0f, s, 0f, c); }
        public static quaternion RotateZ(float angle) { math.sincos(0.5f * angle, out float s, out float c); return new quaternion(0f, 0f, s, c); }

        public static quaternion EulerXYZ(float3 xyz) => math.mul(RotateZ(xyz.z), math.mul(RotateY(xyz.y), RotateX(xyz.x)));
        public static quaternion EulerXZY(float3 xyz) => math.mul(RotateY(xyz.y), math.mul(RotateZ(xyz.z), RotateX(xyz.x)));
        public static quaternion EulerYXZ(float3 xyz) => math.mul(RotateZ(xyz.z), math.mul(RotateX(xyz.x), RotateY(xyz.y)));
        public static quaternion EulerYZX(float3 xyz) => math.mul(RotateX(xyz.x), math.mul(RotateZ(xyz.z), RotateY(xyz.y)));
        public static quaternion EulerZXY(float3 xyz) => math.mul(RotateY(xyz.y), math.mul(RotateX(xyz.x), RotateZ(xyz.z)));
        public static quaternion EulerZYX(float3 xyz) => math.mul(RotateX(xyz.x), math.mul(RotateY(xyz.y), RotateZ(xyz.z)));

        public static quaternion EulerXYZ(float x, float y, float z) => EulerXYZ(new float3(x, y, z));
        public static quaternion EulerXZY(float x, float y, float z) => EulerXZY(new float3(x, y, z));
        public static quaternion EulerYXZ(float x, float y, float z) => EulerYXZ(new float3(x, y, z));
        public static quaternion EulerYZX(float x, float y, float z) => EulerYZX(new float3(x, y, z));
        public static quaternion EulerZXY(float x, float y, float z) => EulerZXY(new float3(x, y, z));
        public static quaternion EulerZYX(float x, float y, float z) => EulerZYX(new float3(x, y, z));

        /// <summary>Angles in RADIANS (unlike UnityEngine.Quaternion.Euler, which takes degrees).</summary>
        public static quaternion Euler(float3 xyz, RotationOrder order = RotationOrder.ZXY)
        {
            switch (order)
            {
                case RotationOrder.XYZ: return EulerXYZ(xyz);
                case RotationOrder.XZY: return EulerXZY(xyz);
                case RotationOrder.YXZ: return EulerYXZ(xyz);
                case RotationOrder.YZX: return EulerYZX(xyz);
                case RotationOrder.ZXY: return EulerZXY(xyz);
                case RotationOrder.ZYX: return EulerZYX(xyz);
                default: return identity;
            }
        }
        public static quaternion Euler(float x, float y, float z, RotationOrder order = RotationOrder.Default) => Euler(new float3(x, y, z), order);

        /// <summary>Rotation whose +z looks along <paramref name="forward"/> with +y as close to
        /// <paramref name="up"/> as possible. Inputs are expected normalized and non-collinear.</summary>
        public static quaternion LookRotation(float3 forward, float3 up)
        {
            float3 t = math.normalize(math.cross(up, forward));
            return new quaternion(new float3x3(t, math.cross(forward, t), forward));
        }

        /// <summary>Like <see cref="LookRotation"/> but tolerant of unnormalized / degenerate
        /// input: returns identity when forward is zero or collinear with up.</summary>
        public static quaternion LookRotationSafe(float3 forward, float3 up)
        {
            float forwardLengthSq = math.dot(forward, forward);
            float upLengthSq = math.dot(up, up);
            if (forwardLengthSq < math.FLT_MIN_NORMAL || upLengthSq < math.FLT_MIN_NORMAL) return identity;
            forward *= 1f / MathF.Sqrt(forwardLengthSq);
            up *= 1f / MathF.Sqrt(upLengthSq);
            float3 t = math.cross(up, forward);
            float tLengthSq = math.dot(t, t);
            if (tLengthSq < 1e-12f || !math.isfinite(tLengthSq)) return identity;
            t *= 1f / MathF.Sqrt(tLengthSq);
            return new quaternion(new float3x3(t, math.cross(forward, t), forward));
        }

        public readonly bool Equals(quaternion x) => value.Equals(x.value);
        public override readonly bool Equals(object x) => x is quaternion q && Equals(q);
        public override readonly int GetHashCode() => value.GetHashCode();
        public override readonly string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "quaternion({0}f, {1}f, {2}f, {3}f)", value.x, value.y, value.z, value.w);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            string.Format("quaternion({0}f, {1}f, {2}f, {3}f)", value.x.ToString(format, formatProvider),
                value.y.ToString(format, formatProvider), value.z.ToString(format, formatProvider), value.w.ToString(format, formatProvider));
    }
}
