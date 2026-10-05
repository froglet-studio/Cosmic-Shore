using System;

namespace CosmicShore.Content.Models
{
    /// <summary>Double-precision 3-vector for import-time math (FBX stores doubles; Unity rounds to float at the end).</summary>
    public readonly struct DVec3
    {
        public readonly double X, Y, Z;
        public DVec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static DVec3 operator +(DVec3 a, DVec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static DVec3 operator -(DVec3 a, DVec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static DVec3 operator *(DVec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static DVec3 operator -(DVec3 a) => new(-a.X, -a.Y, -a.Z);
        public static double Dot(DVec3 a, DVec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static DVec3 Cross(DVec3 a, DVec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public DVec3 Normalized { get { double l = Length; return l > 1e-300 ? this * (1 / l) : default; } }
        public double this[int i] => i == 0 ? X : i == 1 ? Y : Z;
        public override string ToString() => $"({X:G6}, {Y:G6}, {Z:G6})";
    }

    /// <summary>Double-precision affine 4×4 (row-major, column vectors: p' = M·p).</summary>
    public struct DMat4
    {
        public double M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23, M30, M31, M32, M33;

        public static DMat4 Identity => new() { M00 = 1, M11 = 1, M22 = 1, M33 = 1 };

        public static DMat4 Translate(DVec3 t) { var m = Identity; m.M03 = t.X; m.M13 = t.Y; m.M23 = t.Z; return m; }
        public static DMat4 Scale(DVec3 s) => new() { M00 = s.X, M11 = s.Y, M22 = s.Z, M33 = 1 };

        public static DMat4 RotX(double deg) { double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r); var m = Identity; m.M11 = c; m.M12 = -s; m.M21 = s; m.M22 = c; return m; }
        public static DMat4 RotY(double deg) { double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r); var m = Identity; m.M00 = c; m.M02 = s; m.M20 = -s; m.M22 = c; return m; }
        public static DMat4 RotZ(double deg) { double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r); var m = Identity; m.M00 = c; m.M01 = -s; m.M10 = s; m.M11 = c; return m; }

        /// <summary>
        /// FBX Euler rotation. <paramref name="order"/> is the FbxEuler::EOrder enum
        /// (0 XYZ, 1 XZY, 2 YZX, 3 YXZ, 4 ZXY, 5 ZYX, 6 SphericXYZ); "XYZ" rotates about X
        /// first, so the matrix is Rz·Ry·Rx.
        /// </summary>
        public static DMat4 Euler(DVec3 deg, long order = 0)
        {
            DMat4 x = RotX(deg.X), y = RotY(deg.Y), z = RotZ(deg.Z);
            return order switch
            {
                1 => y * z * x,   // XZY
                2 => x * z * y,   // YZX
                3 => z * x * y,   // YXZ
                4 => y * x * z,   // ZXY
                5 => x * y * z,   // ZYX
                _ => z * y * x,   // XYZ (and SphericXYZ)
            };
        }

        public static DMat4 operator *(DMat4 a, DMat4 b)
        {
            DMat4 r;
            r.M00 = a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20 + a.M03 * b.M30;
            r.M01 = a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21 + a.M03 * b.M31;
            r.M02 = a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22 + a.M03 * b.M32;
            r.M03 = a.M00 * b.M03 + a.M01 * b.M13 + a.M02 * b.M23 + a.M03 * b.M33;
            r.M10 = a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20 + a.M13 * b.M30;
            r.M11 = a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31;
            r.M12 = a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32;
            r.M13 = a.M10 * b.M03 + a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33;
            r.M20 = a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20 + a.M23 * b.M30;
            r.M21 = a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31;
            r.M22 = a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32;
            r.M23 = a.M20 * b.M03 + a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33;
            r.M30 = a.M30 * b.M00 + a.M31 * b.M10 + a.M32 * b.M20 + a.M33 * b.M30;
            r.M31 = a.M30 * b.M01 + a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31;
            r.M32 = a.M30 * b.M02 + a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32;
            r.M33 = a.M30 * b.M03 + a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33;
            return r;
        }

        public DVec3 Point(DVec3 p) => new(M00 * p.X + M01 * p.Y + M02 * p.Z + M03, M10 * p.X + M11 * p.Y + M12 * p.Z + M13, M20 * p.X + M21 * p.Y + M22 * p.Z + M23);
        public DVec3 Vector(DVec3 v) => new(M00 * v.X + M01 * v.Y + M02 * v.Z, M10 * v.X + M11 * v.Y + M12 * v.Z, M20 * v.X + M21 * v.Y + M22 * v.Z);
        public DVec3 Translation => new(M03, M13, M23);
        public DVec3 Column(int c) => c switch { 0 => new(M00, M10, M20), 1 => new(M01, M11, M21), _ => new(M02, M12, M22) };

        public double Det3 => M00 * (M11 * M22 - M12 * M21) - M01 * (M10 * M22 - M12 * M20) + M02 * (M10 * M21 - M11 * M20);

        /// <summary>General affine inverse (the bottom row is assumed 0,0,0,1).</summary>
        public DMat4 Inverse()
        {
            double det = Det3;
            if (Math.Abs(det) < 1e-300) return Identity;
            double id = 1 / det;
            DMat4 r = default;
            r.M00 = (M11 * M22 - M12 * M21) * id;
            r.M01 = (M02 * M21 - M01 * M22) * id;
            r.M02 = (M01 * M12 - M02 * M11) * id;
            r.M10 = (M12 * M20 - M10 * M22) * id;
            r.M11 = (M00 * M22 - M02 * M20) * id;
            r.M12 = (M02 * M10 - M00 * M12) * id;
            r.M20 = (M10 * M21 - M11 * M20) * id;
            r.M21 = (M01 * M20 - M00 * M21) * id;
            r.M22 = (M00 * M11 - M01 * M10) * id;
            r.M03 = -(r.M00 * M03 + r.M01 * M13 + r.M02 * M23);
            r.M13 = -(r.M10 * M03 + r.M11 * M13 + r.M12 * M23);
            r.M23 = -(r.M20 * M03 + r.M21 * M13 + r.M22 * M23);
            r.M33 = 1;
            return r;
        }

        /// <summary>Inverse-transpose of the linear part (for normals).</summary>
        public DMat4 NormalMatrix()
        {
            var inv = Inverse();
            DMat4 r = Identity;
            r.M00 = inv.M00; r.M01 = inv.M10; r.M02 = inv.M20;
            r.M10 = inv.M01; r.M11 = inv.M11; r.M12 = inv.M21;
            r.M20 = inv.M02; r.M21 = inv.M12; r.M22 = inv.M22;
            return r;
        }

        public static DMat4 FromArray(double[] a)
        {
            // FBX stores matrices column-major (16 doubles, translation in 12..14).
            if (a == null || a.Length < 16) return Identity;
            return new DMat4
            {
                M00 = a[0], M10 = a[1], M20 = a[2], M30 = a[3],
                M01 = a[4], M11 = a[5], M21 = a[6], M31 = a[7],
                M02 = a[8], M12 = a[9], M22 = a[10], M32 = a[11],
                M03 = a[12], M13 = a[13], M23 = a[14], M33 = a[15],
            };
        }

        /// <summary>
        /// Decompose into translation, rotation (unit quaternion x,y,z,w) and scale. A
        /// negative determinant folds into a negative X scale (Unity's convention).
        /// </summary>
        public void Decompose(out DVec3 t, out (double x, double y, double z, double w) q, out DVec3 s)
        {
            t = Translation;
            DVec3 c0 = Column(0), c1 = Column(1), c2 = Column(2);
            double sx = c0.Length, sy = c1.Length, sz = c2.Length;
            if (Det3 < 0) sx = -sx;
            s = new DVec3(sx, sy, sz);
            DVec3 r0 = sx != 0 ? c0 * (1 / sx) : new DVec3(1, 0, 0);
            DVec3 r1 = sy != 0 ? c1 * (1 / sy) : new DVec3(0, 1, 0);
            DVec3 r2 = sz != 0 ? c2 * (1 / sz) : new DVec3(0, 0, 1);
            q = QuatFromBasis(r0, r1, r2);
        }

        /// <summary>Quaternion of the rotation whose columns are <paramref name="c0"/>, <paramref name="c1"/>, <paramref name="c2"/>.</summary>
        public static (double x, double y, double z, double w) QuatFromBasis(DVec3 c0, DVec3 c1, DVec3 c2)
        {
            double m00 = c0.X, m10 = c0.Y, m20 = c0.Z, m01 = c1.X, m11 = c1.Y, m21 = c1.Z, m02 = c2.X, m12 = c2.Y, m22 = c2.Z;
            double tr = m00 + m11 + m22, x, y, z, w;
            if (tr > 0)
            {
                double s = Math.Sqrt(tr + 1) * 2;
                w = 0.25 * s; x = (m21 - m12) / s; y = (m02 - m20) / s; z = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                double s = Math.Sqrt(1 + m00 - m11 - m22) * 2;
                w = (m21 - m12) / s; x = 0.25 * s; y = (m01 + m10) / s; z = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                double s = Math.Sqrt(1 + m11 - m00 - m22) * 2;
                w = (m02 - m20) / s; x = (m01 + m10) / s; y = 0.25 * s; z = (m12 + m21) / s;
            }
            else
            {
                double s = Math.Sqrt(1 + m22 - m00 - m11) * 2;
                w = (m10 - m01) / s; x = (m02 + m20) / s; y = (m12 + m21) / s; z = 0.25 * s;
            }
            double n = Math.Sqrt(x * x + y * y + z * z + w * w);
            return (x / n, y / n, z / n, w / n);
        }
    }
}
