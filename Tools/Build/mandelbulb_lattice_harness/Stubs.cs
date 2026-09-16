// UnityEngine stubs, transcribed against exactly the members MandelbulbLattice.cs uses.
// A harness, not Unity. Unlike the Regatta harness this one DOES assert bit-exactness against the
// offline model: MandelbulbLattice deliberately computes membership in double precision (a
// boolean over a discrete set can flip on a rounding difference), and none of the stubbed members
// below participate in that arithmetic - they only carry integer addresses and sum unit vectors.
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : zero; } }
        public override string ToString() => $"({x:R}, {y:R}, {z:R})";
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3Int zero => new(0, 0, 0);
        public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static implicit operator Vector3(Vector3Int v) => new(v.x, v.y, v.z);
        public bool Equals(Vector3Int o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3Int v && Equals(v);
        public override int GetHashCode() => (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
        public override string ToString() => $"({x}, {y}, {z})";
    }
}
