// The slice of UnityEngine the omni turns harness compiles the SHIPPED CrystalSpaceSquares / RhombusSkinBaker /
// OmniCrystalCadence against: value maths exactly as Unity defines it, and a Mesh that stores what it is given
// (blend shapes included) so a test can read the frames back. Behaviours are inert shells.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public static float Abs(float v) => Math.Abs(v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Acos(float v) => (float)Math.Acos(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Repeat(float t, float l) => t - (float)Math.Floor(t / l) * l;
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 one => new(1f, 1f, 1f);
        public static Vector3 up => new(0f, 1f, 0f);
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float Angle(Vector3 a, Vector3 b) { float d = Dot(a.normalized, b.normalized); d = d > 1f ? 1f : d < -1f ? -1f : d; return (float)(Math.Acos(d) * 180.0 / Math.PI); }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new(a.x / s, a.y / s, a.z / s);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        public override string ToString() => $"({x:F5}, {y:F5}, {z:F5})";
    }

    public struct Vector3Int
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
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
        public static Quaternion AngleAxis(float degrees, Vector3 axis)
        {
            var a = axis.normalized;
            double h = degrees * Math.PI / 360.0;
            float s = (float)Math.Sin(h);
            return new Quaternion(a.x * s, a.y * s, a.z * s, (float)Math.Cos(h));
        }
        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            var f = from.normalized; var t = to.normalized; var c = Vector3.Cross(f, t); float d = Vector3.Dot(f, t);
            if (d < -0.999999f) { var ax = Vector3.Cross(new Vector3(1, 0, 0), f); if (ax.sqrMagnitude < 1e-6f) ax = Vector3.Cross(new Vector3(0, 1, 0), f); return AngleAxis(180f, ax); }
            var q = new Quaternion(c.x, c.y, c.z, 1f + d); float m = (float)Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
        public static Quaternion Inverse(Quaternion q) => new(-q.x, -q.y, -q.z, q.w);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var u = new Vector3(q.x, q.y, q.z);
            return u * (2f * Vector3.Dot(u, v)) + v * (q.w * q.w - Vector3.Dot(u, u)) + Vector3.Cross(u, v) * (2f * q.w);
        }
        public static Quaternion operator *(Quaternion a, Quaternion b) => new(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
    }

    public struct Matrix4x4
    {
        public Vector3 t;
        public static Matrix4x4 identity => new();
        public static Matrix4x4 Translate(Vector3 v) => new() { t = v };
    }

    public struct BoneWeight
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;
    }

    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public Vector3 extents => size * 0.5f;
        public void Expand(Vector3 amount) { size = size + amount; }
        public void Expand(float amount) { size = size + Vector3.one * amount; }
    }

    public class Object
    {
        public string name { get; set; } = "";
        public static T Instantiate<T>(T original) where T : Object => (T)original.Clone();
        protected virtual Object Clone() => (Object)MemberwiseClone();
        public static void Destroy(Object o) { }
        public static void DestroyImmediate(Object o) { }
        public int GetInstanceID() => GetHashCode();
        public static implicit operator bool(Object o) => o != null;
    }

    public class Mesh : Object
    {
        public Vector3[] vertices { get => (Vector3[])v.Clone(); set => v = (Vector3[])value.Clone(); }
        public Vector3[] normals { get => (Vector3[])n.Clone(); set => n = (Vector3[])value.Clone(); }
        public BoneWeight[] boneWeights { get; set; } = new BoneWeight[0];
        public Matrix4x4[] bindposes { get; set; } = new Matrix4x4[0];
        public Bounds bounds { get; set; }
        public bool isReadable => true;
        public int subMeshCount => subs.Count;
        public int vertexCount => v.Length;
        Vector3[] v = new Vector3[0], n = new Vector3[0];
        List<int[]> subs = new();
        public readonly List<(string name, List<(float weight, Vector3[] dv, Vector3[] dn)> frames)> shapes = new();
        public int[] GetTriangles(int s) => (int[])subs[s].Clone();
        public void SetTriangles(List<int> t, int s, bool calc = true) { while (subs.Count <= s) subs.Add(new int[0]); subs[s] = t.ToArray(); }
        public void SetTriangles(int[] t, int s) { while (subs.Count <= s) subs.Add(new int[0]); subs[s] = (int[])t.Clone(); }
        public int GetBlendShapeIndex(string name) => shapes.FindIndex(x => x.name == name);
        public int blendShapeCount => shapes.Count;
        public void AddBlendShapeFrame(string name, float weight, Vector3[] dv, Vector3[] dn, Vector3[] dt)
        {
            int i = GetBlendShapeIndex(name);
            if (i < 0) { shapes.Add((name, new())); i = shapes.Count - 1; }
            var f = shapes[i].frames;
            if (f.Count > 0 && weight <= f[f.Count - 1].weight) throw new ArgumentException($"blend shape '{name}': frame weights must rise ({weight} after {f[f.Count - 1].weight})");
            if (dv.Length != v.Length) throw new ArgumentException($"blend shape '{name}': {dv.Length} deltas for {v.Length} vertices");
            f.Add((weight, (Vector3[])dv.Clone(), dn == null ? null : (Vector3[])dn.Clone()));
        }
        protected override Object Clone()
        {
            var m = (Mesh)MemberwiseClone();
            m.subs = new List<int[]>(subs);
            m.GetType().GetField("shapes").SetValue(m, new List<(string, List<(float, Vector3[], Vector3[])>)>(shapes));
            return m;
        }
        /// <summary>Unity's blend: the frames bracketing the weight, linearly.</summary>
        public Vector3[] Posed(int shape, float weight)
        {
            var result = vertices; var frames = shapes[shape].frames;
            if (weight <= 0f) return result;
            int hi = frames.FindIndex(f => f.weight >= weight); if (hi < 0) hi = frames.Count - 1;
            float w0 = hi == 0 ? 0f : frames[hi - 1].weight, w1 = frames[hi].weight, a = (weight - w0) / (w1 - w0);
            for (int i = 0; i < result.Length; i++)
            {
                var d0 = hi == 0 ? Vector3.zero : frames[hi - 1].dv[i];
                result[i] = result[i] + d0 + (frames[hi].dv[i] - d0) * a;
            }
            return result;
        }
    }

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() => default;
        public T GetComponentInParent<T>() => default;
        public T[] GetComponentsInChildren<T>(bool all = false) => new T[0];
        public bool TryGetComponent<T>(out T c) { c = default; return false; }
    }
    public class Transform : Component
    {
        public Transform parent; public Vector3 position, localPosition;
        public new GameObject gameObject = new GameObject("");
        public void SetParent(Transform p, bool keep) { parent = p; }
    }
    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string n) { name = n; }
        public int layer;
        Transform t;
        public new Transform transform => t ??= new Transform();
    }
    public class Behaviour : Component { public bool enabled { get; set; } = true; }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject, new() => new T(); }
    public class Renderer : Component { public bool isVisible => true; public Bounds localBounds { get; set; } }
    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh { get; set; }
        public Transform[] bones { get; set; } = new Transform[0];
        public Transform rootBone { get; set; }
        public void SetBlendShapeWeight(int i, float w) { }
    }
    public static class Application { public static bool isPlaying => false; }
    public static class Time { public static float time; }

    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class MinAttribute : Attribute { public MinAttribute(float a) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
}

namespace Unity.Profiling
{
    public readonly struct ProfilerMarker
    {
        public ProfilerMarker(string n) { }
        public AutoScope Auto() => new();
        public readonly struct AutoScope : System.IDisposable { public void Dispose() { } }
    }
}

namespace CosmicShore.Utility
{
    public static class CSDebug
    {
        public static void LogError(string m, UnityEngine.Object o = null) => System.Console.Error.WriteLine("LogError: " + m);
        public static void LogWarning(string m, UnityEngine.Object o = null) => System.Console.Error.WriteLine("LogWarning: " + m);
    }
}
