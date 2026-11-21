// Round 8 (Docs/SWARM_FAUNA.md §16.1), kept for round 11a (§19.1, R11a): the smallest stand-in for Unity.Mathematics / Collections / Jobs /
// Burst that the EXTRACTED shipped prism predicates (extract_burst_predicates.py) compile against. Every
// math function below is Unity.Mathematics' own definition (float, the same operation order), so the
// extracted code computes what the shipped code computes on a CPU without Burst's fast-math. What this does
// NOT model: Burst's own codegen (FMA contraction, approximate rsqrt under FloatMode.Fast). The jobs ship with
// the default FloatMode.Default, so the observable difference is at most a contracted multiply-add on a
// boundary sample - which is why R8a reported boundary disagreements separately; R11a (the virtual-entry shapes) demands zero, since both sides run the same extracted arithmetic.
using System;
using System.Collections.Generic;

namespace Unity.Burst { public sealed class BurstCompileAttribute : Attribute { } }
namespace Unity.Jobs { public interface IJobParallelFor { void Execute(int index); } }
namespace Unity.Collections
{
    public sealed class ReadOnlyAttribute : Attribute { }
    public struct NativeArray<T> where T : struct
    {
        public T[] A;
        public NativeArray(T[] a) { A = a; }
        public T this[int i] { get => A[i]; set => A[i] = value; }
        public int Length => A.Length;
    }
    public struct NativeList<T> where T : struct
    {
        public List<T> L;
        public NativeList(List<T> l) { L = l; }
        public ParallelWriter AsParallelWriter() => new ParallelWriter { L = L };
        public struct ParallelWriter { public List<T> L; public void AddNoResize(T v) => L.Add(v); }
    }
}
namespace Unity.Mathematics
{
    public struct float3
    {
        public float x, y, z;
        public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static float3 operator +(float3 a, float3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static float3 operator -(float3 a, float3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static float3 operator -(float3 a) => new(-a.x, -a.y, -a.z);
        public static float3 operator *(float3 a, float b) => new(a.x * b, a.y * b, a.z * b);
        public static float3 operator *(float b, float3 a) => new(a.x * b, a.y * b, a.z * b);
        public static float3 operator -(float3 a, float b) => new(a.x - b, a.y - b, a.z - b);
        public static float3 operator +(float3 a, float b) => new(a.x + b, a.y + b, a.z + b);
        public static implicit operator float3(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
        public static implicit operator System.Numerics.Vector3(float3 v) => new(v.x, v.y, v.z);
    }
    public static class math
    {
        public const float FLT_MIN_NORMAL = 1.175494351e-38F;
        public static float dot(float3 a, float3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float lengthsq(float3 a) => dot(a, a);
        public static float distancesq(float3 a, float3 b) => lengthsq(b - a);
        public static float sqrt(float x) => MathF.Sqrt(x);
        public static float rsqrt(float x) => 1.0f / sqrt(x);
        public static float max(float a, float b) => MathF.Max(a, b);
        public static float min(float a, float b) => MathF.Min(a, b);
        public static float abs(float a) => MathF.Abs(a);
        public static float clamp(float x, float a, float b) => max(a, min(b, x));
        public static float saturate(float x) => clamp(x, 0.0f, 1.0f);
        public static float radians(float x) => x * 0.0174532925f;
        public static float tan(float x) => MathF.Tan(x);
        public static float3 cross(float3 x, float3 y) =>
            new(x.y * y.z - x.z * y.y, x.z * y.x - x.x * y.z, x.x * y.y - x.y * y.x);
        public static float3 normalizesafe(float3 x, float3 defaultvalue = default)
        {
            float len = dot(x, x);
            return len > FLT_MIN_NORMAL ? x * rsqrt(len) : defaultvalue;
        }
    }
}
