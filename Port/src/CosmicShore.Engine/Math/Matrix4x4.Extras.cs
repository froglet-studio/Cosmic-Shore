using System;

namespace CosmicShore.Engine
{
    public partial struct Matrix4x4
    {
        public Matrix4x4(Vector4 column0, Vector4 column1, Vector4 column2, Vector4 column3)
        {
            m00 = column0.x; m10 = column0.y; m20 = column0.z; m30 = column0.w;
            m01 = column1.x; m11 = column1.y; m21 = column1.z; m31 = column1.w;
            m02 = column2.x; m12 = column2.y; m22 = column2.z; m32 = column2.w;
            m03 = column3.x; m13 = column3.y; m23 = column3.z; m33 = column3.w;
        }

        public float this[int row, int column]
        {
            get => this[row + column * 4];
            set => this[row + column * 4] = value;
        }

        /// <summary>Column-major linear index (original contract: index = row + column*4).</summary>
        public float this[int index]
        {
            get => index switch
            {
                0 => m00, 1 => m10, 2 => m20, 3 => m30, 4 => m01, 5 => m11, 6 => m21, 7 => m31,
                8 => m02, 9 => m12, 10 => m22, 11 => m32, 12 => m03, 13 => m13, 14 => m23, 15 => m33,
                _ => throw new IndexOutOfRangeException("Invalid matrix index!"),
            };
            set
            {
                switch (index)
                {
                    case 0: m00 = value; break; case 1: m10 = value; break; case 2: m20 = value; break; case 3: m30 = value; break;
                    case 4: m01 = value; break; case 5: m11 = value; break; case 6: m21 = value; break; case 7: m31 = value; break;
                    case 8: m02 = value; break; case 9: m12 = value; break; case 10: m22 = value; break; case 11: m32 = value; break;
                    case 12: m03 = value; break; case 13: m13 = value; break; case 14: m23 = value; break; case 15: m33 = value; break;
                    default: throw new IndexOutOfRangeException("Invalid matrix index!");
                }
            }
        }

        public Vector4 GetRow(int index) => new(this[index, 0], this[index, 1], this[index, 2], this[index, 3]);
        public void SetRow(int index, Vector4 row) { this[index, 0] = row.x; this[index, 1] = row.y; this[index, 2] = row.z; this[index, 3] = row.w; }
        public Vector3 GetPosition() => new(m03, m13, m23);
        public void SetTRS(Vector3 pos, Quaternion q, Vector3 s) => this = TRS(pos, q, s);
        public bool isIdentity => this == identity;
        public static bool ValidTRS() => true;

        public Vector3 lossyScale => new(
            new Vector3(m00, m10, m20).magnitude, new Vector3(m01, m11, m21).magnitude, new Vector3(m02, m12, m22).magnitude);

        public Quaternion rotation
        {
            get
            {
                var s = lossyScale;
                if (s.x == 0 || s.y == 0 || s.z == 0) return Quaternion.identity;
                var f = new Vector3(m02, m12, m22) / s.z;
                var u = new Vector3(m01, m11, m21) / s.y;
                return Quaternion.LookRotation(f, u);
            }
        }

        public Matrix4x4 transpose
        {
            get
            {
                var r = this;
                (r.m01, r.m10) = (m10, m01); (r.m02, r.m20) = (m20, m02); (r.m03, r.m30) = (m30, m03);
                (r.m12, r.m21) = (m21, m12); (r.m13, r.m31) = (m31, m13); (r.m23, r.m32) = (m32, m23);
                return r;
            }
        }

        public float determinant
        {
            get
            {
                float a = m00, b = m01, c = m02, d = m03, e = m10, f = m11, g = m12, h = m13;
                float i = m20, j = m21, k = m22, l = m23, m = m30, n = m31, o = m32, p = m33;
                float kp_lo = k * p - l * o, jp_ln = j * p - l * n, jo_kn = j * o - k * n;
                float ip_lm = i * p - l * m, io_km = i * o - k * m, in_jm = i * n - j * m;
                return a * (f * kp_lo - g * jp_ln + h * jo_kn) - b * (e * kp_lo - g * ip_lm + h * io_km)
                     + c * (e * jp_ln - f * ip_lm + h * in_jm) - d * (e * jo_kn - f * io_km + g * in_jm);
            }
        }

        /// <summary>General 4×4 inverse (cofactor expansion); zero matrix when singular (original contract).</summary>
        public Matrix4x4 inverse => Inverse(this);

        public static Matrix4x4 Inverse(Matrix4x4 m)
        {
            float[] a = { m.m00, m.m01, m.m02, m.m03, m.m10, m.m11, m.m12, m.m13, m.m20, m.m21, m.m22, m.m23, m.m30, m.m31, m.m32, m.m33 };
            var inv = new float[16];
            inv[0] = a[5] * a[10] * a[15] - a[5] * a[11] * a[14] - a[9] * a[6] * a[15] + a[9] * a[7] * a[14] + a[13] * a[6] * a[11] - a[13] * a[7] * a[10];
            inv[4] = -a[4] * a[10] * a[15] + a[4] * a[11] * a[14] + a[8] * a[6] * a[15] - a[8] * a[7] * a[14] - a[12] * a[6] * a[11] + a[12] * a[7] * a[10];
            inv[8] = a[4] * a[9] * a[15] - a[4] * a[11] * a[13] - a[8] * a[5] * a[15] + a[8] * a[7] * a[13] + a[12] * a[5] * a[11] - a[12] * a[7] * a[9];
            inv[12] = -a[4] * a[9] * a[14] + a[4] * a[10] * a[13] + a[8] * a[5] * a[14] - a[8] * a[6] * a[13] - a[12] * a[5] * a[10] + a[12] * a[6] * a[9];
            inv[1] = -a[1] * a[10] * a[15] + a[1] * a[11] * a[14] + a[9] * a[2] * a[15] - a[9] * a[3] * a[14] - a[13] * a[2] * a[11] + a[13] * a[3] * a[10];
            inv[5] = a[0] * a[10] * a[15] - a[0] * a[11] * a[14] - a[8] * a[2] * a[15] + a[8] * a[3] * a[14] + a[12] * a[2] * a[11] - a[12] * a[3] * a[10];
            inv[9] = -a[0] * a[9] * a[15] + a[0] * a[11] * a[13] + a[8] * a[1] * a[15] - a[8] * a[3] * a[13] - a[12] * a[1] * a[11] + a[12] * a[3] * a[9];
            inv[13] = a[0] * a[9] * a[14] - a[0] * a[10] * a[13] - a[8] * a[1] * a[14] + a[8] * a[2] * a[13] + a[12] * a[1] * a[10] - a[12] * a[2] * a[9];
            inv[2] = a[1] * a[6] * a[15] - a[1] * a[7] * a[14] - a[5] * a[2] * a[15] + a[5] * a[3] * a[14] + a[13] * a[2] * a[7] - a[13] * a[3] * a[6];
            inv[6] = -a[0] * a[6] * a[15] + a[0] * a[7] * a[14] + a[4] * a[2] * a[15] - a[4] * a[3] * a[14] - a[12] * a[2] * a[7] + a[12] * a[3] * a[6];
            inv[10] = a[0] * a[5] * a[15] - a[0] * a[7] * a[13] - a[4] * a[1] * a[15] + a[4] * a[3] * a[13] + a[12] * a[1] * a[7] - a[12] * a[3] * a[5];
            inv[14] = -a[0] * a[5] * a[14] + a[0] * a[6] * a[13] + a[4] * a[1] * a[14] - a[4] * a[2] * a[13] - a[12] * a[1] * a[6] + a[12] * a[2] * a[5];
            inv[3] = -a[1] * a[6] * a[11] + a[1] * a[7] * a[10] + a[5] * a[2] * a[11] - a[5] * a[3] * a[10] - a[9] * a[2] * a[7] + a[9] * a[3] * a[6];
            inv[7] = a[0] * a[6] * a[11] - a[0] * a[7] * a[10] - a[4] * a[2] * a[11] + a[4] * a[3] * a[10] + a[8] * a[2] * a[7] - a[8] * a[3] * a[6];
            inv[11] = -a[0] * a[5] * a[11] + a[0] * a[7] * a[9] + a[4] * a[1] * a[11] - a[4] * a[3] * a[9] - a[8] * a[1] * a[7] + a[8] * a[3] * a[5];
            inv[15] = a[0] * a[5] * a[10] - a[0] * a[6] * a[9] - a[4] * a[1] * a[10] + a[4] * a[2] * a[9] + a[8] * a[1] * a[6] - a[8] * a[2] * a[5];
            double det = (double)a[0] * inv[0] + (double)a[1] * inv[4] + (double)a[2] * inv[8] + (double)a[3] * inv[12];
            if (Math.Abs(det) < 1e-30) return zero;
            float invDet = (float)(1.0 / det);
            return new Matrix4x4
            {
                m00 = inv[0] * invDet, m01 = inv[1] * invDet, m02 = inv[2] * invDet, m03 = inv[3] * invDet,
                m10 = inv[4] * invDet, m11 = inv[5] * invDet, m12 = inv[6] * invDet, m13 = inv[7] * invDet,
                m20 = inv[8] * invDet, m21 = inv[9] * invDet, m22 = inv[10] * invDet, m23 = inv[11] * invDet,
                m30 = inv[12] * invDet, m31 = inv[13] * invDet, m32 = inv[14] * invDet, m33 = inv[15] * invDet,
            };
        }

        public static Matrix4x4 Transpose(Matrix4x4 m) => m.transpose;
        public static float Determinant(Matrix4x4 m) => m.determinant;
        public static Matrix4x4 Translate(Vector3 v) => TRS(v, Quaternion.identity, Vector3.one);
        public static Matrix4x4 Rotate(Quaternion q) => TRS(Vector3.zero, q, Vector3.one);
        public static Matrix4x4 Scale(Vector3 s) => TRS(Vector3.zero, Quaternion.identity, s);

        /// <summary>OpenGL-convention perspective projection (original contract; fov in degrees, vertical).</summary>
        public static Matrix4x4 Perspective(float fov, float aspect, float zNear, float zFar)
        {
            float f = 1f / MathF.Tan(fov * Mathf.Deg2Rad * 0.5f);
            return new Matrix4x4
            {
                m00 = f / aspect, m11 = f,
                m22 = (zFar + zNear) / (zNear - zFar), m23 = 2f * zFar * zNear / (zNear - zFar),
                m32 = -1f,
            };
        }

        public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float zNear, float zFar)
        {
            var m = identity;
            m.m00 = 2f / (right - left); m.m11 = 2f / (top - bottom); m.m22 = -2f / (zFar - zNear);
            m.m03 = -(right + left) / (right - left); m.m13 = -(top + bottom) / (top - bottom); m.m23 = -(zFar + zNear) / (zFar - zNear);
            return m;
        }

        public static Matrix4x4 Frustum(float left, float right, float bottom, float top, float zNear, float zFar)
        {
            return new Matrix4x4
            {
                m00 = 2f * zNear / (right - left), m02 = (right + left) / (right - left),
                m11 = 2f * zNear / (top - bottom), m12 = (top + bottom) / (top - bottom),
                m22 = -(zFar + zNear) / (zFar - zNear), m23 = -2f * zFar * zNear / (zFar - zNear),
                m32 = -1f,
            };
        }

        /// <summary>An object-to-world "look at" TRS (original contract: forward = target - from).</summary>
        public static Matrix4x4 LookAt(Vector3 from, Vector3 to, Vector3 up)
            => TRS(from, Quaternion.LookRotation(to - from, up), Vector3.one);

        public static Vector4 operator *(Matrix4x4 m, Vector4 v) => new(
            m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03 * v.w,
            m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13 * v.w,
            m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23 * v.w,
            m.m30 * v.x + m.m31 * v.y + m.m32 * v.z + m.m33 * v.w);

        public Plane TransformPlane(Plane plane)
        {
            var it = inverse.transpose;
            var v = it * new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            return new Plane(new Vector3(v.x, v.y, v.z), v.w);
        }
    }
}
