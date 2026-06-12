using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Integer 3-component vector (voxel/grid indices). Engine addition for
    /// vessel-layer V11 (BlockDensityGrid voxel indexing). Mirrors the slice of the
    /// original engine API the ported code uses: component access, RoundToInt /
    /// FloorToInt / CeilToInt construction from a Vector3, arithmetic, equality, and
    /// the implicit widening conversion to Vector3.
    /// </summary>
    [Serializable]
    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x;
        public int y;
        public int z;

        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3Int zero => new(0, 0, 0);
        public static Vector3Int one => new(1, 1, 1);

        public int this[int index]
        {
            get => index switch
            {
                0 => x,
                1 => y,
                2 => z,
                _ => throw new IndexOutOfRangeException($"Invalid Vector3Int index: {index}"),
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException($"Invalid Vector3Int index: {index}");
                }
            }
        }

        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public int sqrMagnitude => x * x + y * y + z * z;

        /// <summary>Per-component Mathf.RoundToInt (round-half-to-even, same as the original engine).</summary>
        public static Vector3Int RoundToInt(Vector3 v) =>
            new(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));

        public static Vector3Int FloorToInt(Vector3 v) =>
            new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));

        public static Vector3Int CeilToInt(Vector3 v) =>
            new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y), Mathf.CeilToInt(v.z));

        public static implicit operator Vector3(Vector3Int v) => new(v.x, v.y, v.z);

        public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3Int operator -(Vector3Int a) => new(-a.x, -a.y, -a.z);
        public static Vector3Int operator *(Vector3Int a, int d) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3Int operator *(int d, Vector3Int a) => new(a.x * d, a.y * d, a.z * d);

        public static bool operator ==(Vector3Int a, Vector3Int b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Vector3Int a, Vector3Int b) => !(a == b);

        public bool Equals(Vector3Int other) => this == other;
        public override bool Equals(object obj) => obj is Vector3Int other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public override string ToString() => $"({x}, {y}, {z})";
    }
}
