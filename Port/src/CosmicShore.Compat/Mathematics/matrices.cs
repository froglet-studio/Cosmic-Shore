using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Unity.Mathematics
{
    /// <summary>Column-major 3x3 float matrix (c0..c2 are columns). Note: like Unity.Mathematics,
    /// <c>*</c> is COMPONENTWISE; the matrix product is <c>math.mul</c>.</summary>
    [Serializable]
    public partial struct float3x3 : IEquatable<float3x3>, IFormattable
    {
        public float3 c0, c1, c2;

        public static readonly float3x3 identity = new float3x3(1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f);
        public static readonly float3x3 zero;

        public float3x3(float3 c0, float3 c1, float3 c2) { this.c0 = c0; this.c1 = c1; this.c2 = c2; }

        /// <summary>Arguments in ROW-major order (m{row}{col}).</summary>
        public float3x3(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22)
        {
            c0 = new float3(m00, m10, m20);
            c1 = new float3(m01, m11, m21);
            c2 = new float3(m02, m12, m22);
        }

        public float3x3(float v) { c0 = v; c1 = v; c2 = v; }

        /// <summary>Rotation matrix of a unit quaternion.</summary>
        public float3x3(quaternion q)
        {
            float4 v = q.value;
            float x = v.x, y = v.y, z = v.z, w = v.w;
            float xx = x * x, yy = y * y, zz = z * z, xy = x * y, xz = x * z, yz = y * z, wx = w * x, wy = w * y, wz = w * z;
            c0 = new float3(1f - 2f * (yy + zz), 2f * (xy + wz), 2f * (xz - wy));
            c1 = new float3(2f * (xy - wz), 1f - 2f * (xx + zz), 2f * (yz + wx));
            c2 = new float3(2f * (xz + wy), 2f * (yz - wx), 1f - 2f * (xx + yy));
        }

        public float3x3(float4x4 f4x4) { c0 = f4x4.c0.xyz; c1 = f4x4.c1.xyz; c2 = f4x4.c2.xyz; }

        public static float3x3 AxisAngle(float3 axis, float angle) => new float3x3(quaternion.AxisAngle(axis, angle));
        public static float3x3 Scale(float s) => new float3x3(s, 0f, 0f, 0f, s, 0f, 0f, 0f, s);
        public static float3x3 Scale(float3 v) => new float3x3(v.x, 0f, 0f, 0f, v.y, 0f, 0f, 0f, v.z);
        public static float3x3 LookRotation(float3 forward, float3 up) => new float3x3(quaternion.LookRotation(forward, up));
        public static float3x3 RotateX(float angle) => new float3x3(quaternion.RotateX(angle));
        public static float3x3 RotateY(float angle) => new float3x3(quaternion.RotateY(angle));
        public static float3x3 RotateZ(float angle) => new float3x3(quaternion.RotateZ(angle));

        public static implicit operator float3x3(float v) => new float3x3(v);

        public static float3x3 operator +(float3x3 a, float3x3 b) => new float3x3(a.c0 + b.c0, a.c1 + b.c1, a.c2 + b.c2);
        public static float3x3 operator -(float3x3 a, float3x3 b) => new float3x3(a.c0 - b.c0, a.c1 - b.c1, a.c2 - b.c2);
        public static float3x3 operator *(float3x3 a, float3x3 b) => new float3x3(a.c0 * b.c0, a.c1 * b.c1, a.c2 * b.c2);
        public static float3x3 operator *(float3x3 a, float b) => new float3x3(a.c0 * b, a.c1 * b, a.c2 * b);
        public static float3x3 operator *(float a, float3x3 b) => new float3x3(a * b.c0, a * b.c1, a * b.c2);
        public static float3x3 operator /(float3x3 a, float b) => new float3x3(a.c0 / b, a.c1 / b, a.c2 / b);
        public static float3x3 operator -(float3x3 a) => new float3x3(-a.c0, -a.c1, -a.c2);

        [UnscopedRef] public ref float3 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return ref c0;
                    case 1: return ref c1;
                    case 2: return ref c2;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        public readonly bool Equals(float3x3 rhs) => c0.Equals(rhs.c0) && c1.Equals(rhs.c1) && c2.Equals(rhs.c2);
        public override readonly bool Equals(object o) => o is float3x3 m && Equals(m);
        public override readonly int GetHashCode() => HashCode.Combine(c0, c1, c2);
        public override readonly string ToString() => string.Format(CultureInfo.InvariantCulture,
            "float3x3({0}f, {1}f, {2}f,  {3}f, {4}f, {5}f,  {6}f, {7}f, {8}f)",
            c0.x, c1.x, c2.x, c0.y, c1.y, c2.y, c0.z, c1.z, c2.z);
        public readonly string ToString(string format, IFormatProvider formatProvider) => ToString();
    }

    /// <summary>Column-major 4x4 float matrix (c0..c3 are columns; c3.xyz is the translation of an
    /// affine transform). <c>*</c> is COMPONENTWISE; the product is <c>math.mul</c>.</summary>
    [Serializable]
    public partial struct float4x4 : IEquatable<float4x4>, IFormattable
    {
        public float4 c0, c1, c2, c3;

        public static readonly float4x4 identity = new float4x4(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f);
        public static readonly float4x4 zero;

        public float4x4(float4 c0, float4 c1, float4 c2, float4 c3) { this.c0 = c0; this.c1 = c1; this.c2 = c2; this.c3 = c3; }

        /// <summary>Arguments in ROW-major order (m{row}{col}).</summary>
        public float4x4(float m00, float m01, float m02, float m03,
                        float m10, float m11, float m12, float m13,
                        float m20, float m21, float m22, float m23,
                        float m30, float m31, float m32, float m33)
        {
            c0 = new float4(m00, m10, m20, m30);
            c1 = new float4(m01, m11, m21, m31);
            c2 = new float4(m02, m12, m22, m32);
            c3 = new float4(m03, m13, m23, m33);
        }

        public float4x4(float v) { c0 = v; c1 = v; c2 = v; c3 = v; }

        public float4x4(float3x3 rotation, float3 translation)
        {
            c0 = new float4(rotation.c0, 0f);
            c1 = new float4(rotation.c1, 0f);
            c2 = new float4(rotation.c2, 0f);
            c3 = new float4(translation, 1f);
        }

        public float4x4(quaternion rotation, float3 translation) : this(new float3x3(rotation), translation) { }

        public static implicit operator float4x4(float v) => new float4x4(v);

        public static float4x4 TRS(float3 translation, quaternion rotation, float3 scale)
        {
            float3x3 r = new float3x3(rotation);
            return new float4x4(
                new float4(r.c0 * scale.x, 0f),
                new float4(r.c1 * scale.y, 0f),
                new float4(r.c2 * scale.z, 0f),
                new float4(translation, 1f));
        }

        public static float4x4 Translate(float3 vector) => new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(vector, 1f));
        public static float4x4 Scale(float s) => Scale(new float3(s));
        public static float4x4 Scale(float x, float y, float z) => Scale(new float3(x, y, z));
        public static float4x4 Scale(float3 v) => new float4x4(new float4(v.x, 0f, 0f, 0f), new float4(0f, v.y, 0f, 0f), new float4(0f, 0f, v.z, 0f), new float4(0f, 0f, 0f, 1f));
        public static float4x4 AxisAngle(float3 axis, float angle) => new float4x4(quaternion.AxisAngle(axis, angle), float3.zero);
        public static float4x4 RotateX(float angle) => new float4x4(quaternion.RotateX(angle), float3.zero);
        public static float4x4 RotateY(float angle) => new float4x4(quaternion.RotateY(angle), float3.zero);
        public static float4x4 RotateZ(float angle) => new float4x4(quaternion.RotateZ(angle), float3.zero);
        public static float4x4 EulerZXY(float3 xyz) => new float4x4(quaternion.EulerZXY(xyz), float3.zero);
        public static float4x4 LookAt(float3 eye, float3 target, float3 up)
        {
            float3x3 rot = float3x3.LookRotation(math.normalize(target - eye), up);
            return new float4x4(rot, eye);
        }

        public static float4x4 operator +(float4x4 a, float4x4 b) => new float4x4(a.c0 + b.c0, a.c1 + b.c1, a.c2 + b.c2, a.c3 + b.c3);
        public static float4x4 operator -(float4x4 a, float4x4 b) => new float4x4(a.c0 - b.c0, a.c1 - b.c1, a.c2 - b.c2, a.c3 - b.c3);
        public static float4x4 operator *(float4x4 a, float4x4 b) => new float4x4(a.c0 * b.c0, a.c1 * b.c1, a.c2 * b.c2, a.c3 * b.c3);
        public static float4x4 operator *(float4x4 a, float b) => new float4x4(a.c0 * b, a.c1 * b, a.c2 * b, a.c3 * b);
        public static float4x4 operator *(float a, float4x4 b) => new float4x4(a * b.c0, a * b.c1, a * b.c2, a * b.c3);
        public static float4x4 operator /(float4x4 a, float b) => new float4x4(a.c0 / b, a.c1 / b, a.c2 / b, a.c3 / b);
        public static float4x4 operator -(float4x4 a) => new float4x4(-a.c0, -a.c1, -a.c2, -a.c3);

        [UnscopedRef] public ref float4 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return ref c0;
                    case 1: return ref c1;
                    case 2: return ref c2;
                    case 3: return ref c3;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        public readonly bool Equals(float4x4 rhs) => c0.Equals(rhs.c0) && c1.Equals(rhs.c1) && c2.Equals(rhs.c2) && c3.Equals(rhs.c3);
        public override readonly bool Equals(object o) => o is float4x4 m && Equals(m);
        public override readonly int GetHashCode() => HashCode.Combine(c0, c1, c2, c3);
        public override readonly string ToString() => string.Format(CultureInfo.InvariantCulture,
            "float4x4({0}f, {1}f, {2}f, {3}f,  {4}f, {5}f, {6}f, {7}f,  {8}f, {9}f, {10}f, {11}f,  {12}f, {13}f, {14}f, {15}f)",
            c0.x, c1.x, c2.x, c3.x, c0.y, c1.y, c2.y, c3.y, c0.z, c1.z, c2.z, c3.z, c0.w, c1.w, c2.w, c3.w);
        public readonly string ToString(string format, IFormatProvider formatProvider) => ToString();
    }
}
