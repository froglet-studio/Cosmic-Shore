using System;

namespace CosmicShore.Engine.Rendering
{
    /// <summary>
    /// Original-contract 4×4 transformation matrix (the mesh arc). Field layout matches
    /// the original engine: <c>mRC</c> is row R, column C, column-major storage in memory
    /// is irrelevant headless — only the algebra matters. Focused surface: TRS
    /// composition (the instanced-draw path), point/vector multiplication, columns,
    /// identity/zero, and matrix product. Extend as ported call sites need more.
    ///
    /// Lives under Engine.Rendering (not the engine root the original used) for the same
    /// reason as <see cref="PrimitiveType"/>: the Silk.NET client imports
    /// System.Numerics (which has its own Matrix4x4) alongside CosmicShore.Engine, so a
    /// root-namespace Matrix4x4 would CS0104 every unqualified use there. Ported call
    /// sites resolve it through <c>using CosmicShore.Engine.Rendering;</c> — the same
    /// directive the original's <c>using UnityEngine.Rendering;</c> maps to.
    /// </summary>
    public struct Matrix4x4 : IEquatable<Matrix4x4>
    {
        public float m00, m01, m02, m03;
        public float m10, m11, m12, m13;
        public float m20, m21, m22, m23;
        public float m30, m31, m32, m33;

        public static Matrix4x4 identity => new()
        {
            m00 = 1f, m11 = 1f, m22 = 1f, m33 = 1f,
        };

        public static Matrix4x4 zero => default;

        /// <summary>Compose a translate·rotate·scale transform (the original TRS contract).</summary>
        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s)
        {
            // Rotation matrix from the (assumed normalized) quaternion.
            float x = q.x, y = q.y, z = q.z, w = q.w;
            float x2 = x + x, y2 = y + y, z2 = z + z;
            float xx = x * x2, yy = y * y2, zz = z * z2;
            float xy = x * y2, xz = x * z2, yz = y * z2;
            float wx = w * x2, wy = w * y2, wz = w * z2;

            Matrix4x4 m = identity;
            m.m00 = (1f - (yy + zz)) * s.x;
            m.m10 = (xy + wz) * s.x;
            m.m20 = (xz - wy) * s.x;

            m.m01 = (xy - wz) * s.y;
            m.m11 = (1f - (xx + zz)) * s.y;
            m.m21 = (yz + wx) * s.y;

            m.m02 = (xz + wy) * s.z;
            m.m12 = (yz - wx) * s.z;
            m.m22 = (1f - (xx + yy)) * s.z;

            m.m03 = pos.x;
            m.m13 = pos.y;
            m.m23 = pos.z;
            return m;
        }

        /// <summary>Transform a point (translation applied; assumes an affine matrix — the fast original path).</summary>
        public Vector3 MultiplyPoint3x4(Vector3 point) => new(
            m00 * point.x + m01 * point.y + m02 * point.z + m03,
            m10 * point.x + m11 * point.y + m12 * point.z + m13,
            m20 * point.x + m21 * point.y + m22 * point.z + m23);

        /// <summary>Transform a point with full perspective divide (original generic path).</summary>
        public Vector3 MultiplyPoint(Vector3 point)
        {
            Vector3 result = MultiplyPoint3x4(point);
            float w = m30 * point.x + m31 * point.y + m32 * point.z + m33;
            if (Mathf.Abs(w) > 1e-12f && Mathf.Abs(w - 1f) > 1e-12f)
                result *= 1f / w;
            return result;
        }

        /// <summary>Transform a direction (translation ignored).</summary>
        public Vector3 MultiplyVector(Vector3 vector) => new(
            m00 * vector.x + m01 * vector.y + m02 * vector.z,
            m10 * vector.x + m11 * vector.y + m12 * vector.z,
            m20 * vector.x + m21 * vector.y + m22 * vector.z);

        public Vector4 GetColumn(int index) => index switch
        {
            0 => new Vector4(m00, m10, m20, m30),
            1 => new Vector4(m01, m11, m21, m31),
            2 => new Vector4(m02, m12, m22, m32),
            3 => new Vector4(m03, m13, m23, m33),
            _ => throw new IndexOutOfRangeException("Invalid matrix column index!"),
        };

        public void SetColumn(int index, Vector4 column)
        {
            switch (index)
            {
                case 0: m00 = column.x; m10 = column.y; m20 = column.z; m30 = column.w; break;
                case 1: m01 = column.x; m11 = column.y; m21 = column.z; m31 = column.w; break;
                case 2: m02 = column.x; m12 = column.y; m22 = column.z; m32 = column.w; break;
                case 3: m03 = column.x; m13 = column.y; m23 = column.z; m33 = column.w; break;
                default: throw new IndexOutOfRangeException("Invalid matrix column index!");
            }
        }

        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            Matrix4x4 r = default;
            r.m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20 + a.m03 * b.m30;
            r.m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21 + a.m03 * b.m31;
            r.m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22 + a.m03 * b.m32;
            r.m03 = a.m00 * b.m03 + a.m01 * b.m13 + a.m02 * b.m23 + a.m03 * b.m33;

            r.m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20 + a.m13 * b.m30;
            r.m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21 + a.m13 * b.m31;
            r.m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22 + a.m13 * b.m32;
            r.m13 = a.m10 * b.m03 + a.m11 * b.m13 + a.m12 * b.m23 + a.m13 * b.m33;

            r.m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20 + a.m23 * b.m30;
            r.m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21 + a.m23 * b.m31;
            r.m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22 + a.m23 * b.m32;
            r.m23 = a.m20 * b.m03 + a.m21 * b.m13 + a.m22 * b.m23 + a.m23 * b.m33;

            r.m30 = a.m30 * b.m00 + a.m31 * b.m10 + a.m32 * b.m20 + a.m33 * b.m30;
            r.m31 = a.m30 * b.m01 + a.m31 * b.m11 + a.m32 * b.m21 + a.m33 * b.m31;
            r.m32 = a.m30 * b.m02 + a.m31 * b.m12 + a.m32 * b.m22 + a.m33 * b.m32;
            r.m33 = a.m30 * b.m03 + a.m31 * b.m13 + a.m32 * b.m23 + a.m33 * b.m33;
            return r;
        }

        public bool Equals(Matrix4x4 other)
            => m00 == other.m00 && m01 == other.m01 && m02 == other.m02 && m03 == other.m03
            && m10 == other.m10 && m11 == other.m11 && m12 == other.m12 && m13 == other.m13
            && m20 == other.m20 && m21 == other.m21 && m22 == other.m22 && m23 == other.m23
            && m30 == other.m30 && m31 == other.m31 && m32 == other.m32 && m33 == other.m33;

        public override bool Equals(object obj) => obj is Matrix4x4 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            HashCode.Combine(m00, m01, m02, m03),
            HashCode.Combine(m10, m11, m12, m13),
            HashCode.Combine(m20, m21, m22, m23),
            HashCode.Combine(m30, m31, m32, m33));

        public static bool operator ==(Matrix4x4 a, Matrix4x4 b) => a.Equals(b);
        public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => !a.Equals(b);

        public override string ToString()
            => $"{m00:F5}\t{m01:F5}\t{m02:F5}\t{m03:F5}\n{m10:F5}\t{m11:F5}\t{m12:F5}\t{m13:F5}\n{m20:F5}\t{m21:F5}\t{m22:F5}\t{m23:F5}\n{m30:F5}\t{m31:F5}\t{m32:F5}\t{m33:F5}\n";
    }
}
