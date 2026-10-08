// A minimal UnityEngine stand-in: just enough surface to compile the SHIPPED
// CrystalMorphMeshBuilder, IcosphereMeshGenerator and their edit-mode suites unchanged, and run
// them. The maths is written to Unity's documented semantics (Vector3 == is approximate,
// Bounds.Contains is inclusive, Mesh.vertices THROWS on an unreadable mesh, LookRotation builds
// z = forward, x = up × z). Nothing here is clever; anything the suites do not touch is absent.
using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16 = 0, UInt32 = 1 }
}

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    [Flags] public enum HideFlags { None = 0, DontSave = 52 }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
        public static bool Approximately(float a, float b) =>
            Abs(b - a) < Max(1e-6f * Max(Abs(a), Abs(b)), float.Epsilon * 8f);
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new(0f, 0f);
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public bool Equals(Vector3Int o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3Int v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }

    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 one => new(1f, 1f, 1f);
        public static Vector3 up => new(0f, 1f, 0f);
        public static Vector3 right => new(1f, 0f, 0f);
        public static Vector3 forward => new(0f, 0f, 1f);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new(a.x / s, a.y / s, a.z / s);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public bool Equals(Vector3 o) => x.Equals(o.x) && y.Equals(o.y) && z.Equals(o.z);
        public override bool Equals(object o) => o is Vector3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) =>
            new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * Mathf.Clamp01(t);
        public static Vector3 Min(Vector3 a, Vector3 b) => new(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public override string ToString() => $"({x:F4}, {y:F4}, {z:F4})";
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new(0f, 0f, 0f, 1f);

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var u = new Vector3(q.x, q.y, q.z);
            return u * (2f * Vector3.Dot(u, v)) + v * (q.w * q.w - Vector3.Dot(u, u)) + Vector3.Cross(u, v) * (2f * q.w);
        }

        /// <summary>z = forward, x = up × z, y = z × x — Unity's convention.</summary>
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 zA = forward.normalized;
            Vector3 xA = Vector3.Cross(up, zA).normalized;
            Vector3 yA = Vector3.Cross(zA, xA);
            float m00 = xA.x, m01 = yA.x, m02 = zA.x;
            float m10 = xA.y, m11 = yA.y, m12 = zA.y;
            float m20 = xA.z, m21 = yA.z, m22 = zA.z;
            float tr = m00 + m11 + m22;
            if (tr > 0f)
            {
                float s = (float)Math.Sqrt(tr + 1f) * 2f;
                return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            if (m00 > m11 && m00 > m22)
            {
                float s = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f;
                return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            if (m11 > m22)
            {
                float s = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f;
                return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            {
                float s = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f;
                return new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }
        }
    }

    public struct Matrix4x4
    {
        // Row-major storage: m[r, c].
        float[,] _m;
        float[,] M => _m ??= Ident();

        static float[,] Ident() => new float[4, 4] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } };

        public static Matrix4x4 identity => new() { _m = Ident() };

        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s)
        {
            Vector3 cx = q * new Vector3(s.x, 0f, 0f);
            Vector3 cy = q * new Vector3(0f, s.y, 0f);
            Vector3 cz = q * new Vector3(0f, 0f, s.z);
            return new Matrix4x4
            {
                _m = new float[4, 4]
                {
                    { cx.x, cy.x, cz.x, pos.x },
                    { cx.y, cy.y, cz.y, pos.y },
                    { cx.z, cy.z, cz.z, pos.z },
                    { 0, 0, 0, 1 },
                },
            };
        }

        public static Matrix4x4 Translate(Vector3 v) => TRS(v, Quaternion.identity, Vector3.one);

        public Vector3 MultiplyPoint3x4(Vector3 p)
        {
            var m = M;
            return new Vector3(m[0, 0] * p.x + m[0, 1] * p.y + m[0, 2] * p.z + m[0, 3],
                               m[1, 0] * p.x + m[1, 1] * p.y + m[1, 2] * p.z + m[1, 3],
                               m[2, 0] * p.x + m[2, 1] * p.y + m[2, 2] * p.z + m[2, 3]);
        }

        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new float[4, 4];
            var ma = a.M; var mb = b.M;
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 4; k++) sum += ma[i, k] * mb[k, j];
                    r[i, j] = sum;
                }
            return new Matrix4x4 { _m = r };
        }
    }

    public struct Bounds
    {
        public Vector3 center;
        public Vector3 extents;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; extents = size * 0.5f; }
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;
        public void Encapsulate(Vector3 p)
        {
            Vector3 lo = Vector3.Min(min, p), hi = Vector3.Max(max, p);
            center = (lo + hi) * 0.5f;
            extents = (hi - lo) * 0.5f;
        }
        public bool Contains(Vector3 p)
        {
            Vector3 lo = min, hi = max;
            return p.x >= lo.x && p.x <= hi.x && p.y >= lo.y && p.y <= hi.y && p.z >= lo.z && p.z <= hi.z;
        }
    }

    public class Object
    {
        static int s_nextId = 1;
        readonly int _id = s_nextId++;
        bool _destroyed;
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public int GetInstanceID() => _id;
        public static void DestroyImmediate(Object o) { if (o != null) o._destroyed = true; }
        public static void Destroy(Object o) { if (o != null) o._destroyed = true; }
        public static implicit operator bool(Object o) => o != null && !o._destroyed;
    }

    public sealed class Mesh : Object
    {
        Vector3[] _verts = Array.Empty<Vector3>();
        Vector3[] _normals = Array.Empty<Vector3>();
        Vector4[] _tangents = Array.Empty<Vector4>();
        Vector2[] _uv0 = Array.Empty<Vector2>();
        readonly Dictionary<int, Vector4[]> _uvs = new();
        int[] _tris = Array.Empty<int>();
        bool _readable = true;

        public Rendering.IndexFormat indexFormat { get; set; }
        public Bounds bounds { get; set; }
        public bool isReadable => _readable;
        public int vertexCount => _verts.Length;

        void Guard() { if (!_readable) throw new InvalidOperationException("Not allowed to access vertices on mesh (Read/Write off)"); }

        public Vector3[] vertices { get { Guard(); return (Vector3[])_verts.Clone(); } set { _verts = (Vector3[])value.Clone(); RecalculateBounds(); } }
        public Vector3[] normals { get { Guard(); return (Vector3[])_normals.Clone(); } set => _normals = (Vector3[])value.Clone(); }
        public Vector4[] tangents { get { Guard(); return (Vector4[])_tangents.Clone(); } set => _tangents = (Vector4[])value.Clone(); }
        public Vector2[] uv { get { Guard(); return (Vector2[])_uv0.Clone(); } set => _uv0 = (Vector2[])value.Clone(); }
        public int[] triangles { get { Guard(); return (int[])_tris.Clone(); } set { _tris = (int[])value.Clone(); RecalculateBounds(); } }

        public void Clear() { _verts = Array.Empty<Vector3>(); _normals = Array.Empty<Vector3>(); _tangents = Array.Empty<Vector4>(); _uv0 = Array.Empty<Vector2>(); _uvs.Clear(); _tris = Array.Empty<int>(); }
        public void SetVertices(List<Vector3> v) => vertices = v.ToArray();
        public void SetVertices(Vector3[] v) => vertices = v;
        public void SetNormals(Vector3[] n) => normals = n;
        public void SetNormals(List<Vector3> n) => normals = n.ToArray();
        public void SetTangents(Vector4[] t) => tangents = t;
        public void SetUVs(int channel, Vector2[] uv)
        {
            if (channel == 0) _uv0 = (Vector2[])uv.Clone();
            else { var a = new Vector4[uv.Length]; for (int i = 0; i < uv.Length; i++) a[i] = new Vector4(uv[i].x, uv[i].y, 0, 0); _uvs[channel] = a; }
        }
        public void SetUVs(int channel, Vector4[] uv) => _uvs[channel] = (Vector4[])uv.Clone();
        public void SetUVs(int channel, List<Vector4> uv) => _uvs[channel] = uv.ToArray();
        public void GetUVs(int channel, List<Vector4> into)
        {
            Guard();
            into.Clear();
            if (_uvs.TryGetValue(channel, out var a)) into.AddRange(a);
        }
        public void SetTriangles(int[] t, int submesh, bool calculateBounds = true) { _tris = (int[])t.Clone(); if (calculateBounds) RecalculateBounds(); }
        public void SetTriangles(List<int> t, int submesh, bool calculateBounds = true) => SetTriangles(t.ToArray(), submesh, calculateBounds);
        public void UploadMeshData(bool markNoLongerReadable) { if (markNoLongerReadable) _readable = false; }

        public void RecalculateBounds()
        {
            if (_verts.Length == 0) { bounds = default; return; }
            var lo = _verts[0]; var hi = _verts[0];
            foreach (var v in _verts) { lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v); }
            bounds = new Bounds((lo + hi) * 0.5f, hi - lo);
        }

        public void RecalculateNormals()
        {
            var n = new Vector3[_verts.Length];
            for (int t = 0; t < _tris.Length; t += 3)
            {
                Vector3 fn = Vector3.Cross(_verts[_tris[t + 1]] - _verts[_tris[t]], _verts[_tris[t + 2]] - _verts[_tris[t]]);
                n[_tris[t]] += fn; n[_tris[t + 1]] += fn; n[_tris[t + 2]] += fn;
            }
            for (int i = 0; i < n.Length; i++) n[i] = n[i].normalized;
            _normals = n;
        }
    }
}
