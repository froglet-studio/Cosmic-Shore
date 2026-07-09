using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Integer 2-component vector. Engine addition for the UI arc (RectMask2D softness;
    /// Arc D will use it for integer screen coordinates). Mirrors <see cref="Vector3Int"/>:
    /// component access, Round/Floor/Ceil construction from a Vector2, arithmetic,
    /// equality, and the implicit widening conversion to Vector2.
    /// </summary>
    [Serializable]
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x;
        public int y;

        public Vector2Int(int x, int y) { this.x = x; this.y = y; }

        public static Vector2Int zero => new(0, 0);
        public static Vector2Int one => new(1, 1);

        public int this[int index]
        {
            get => index switch
            {
                0 => x,
                1 => y,
                _ => throw new IndexOutOfRangeException($"Invalid Vector2Int index: {index}"),
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    default: throw new IndexOutOfRangeException($"Invalid Vector2Int index: {index}");
                }
            }
        }

        public float magnitude => MathF.Sqrt(x * x + y * y);
        public int sqrMagnitude => x * x + y * y;

        public static Vector2Int RoundToInt(Vector2 v) => new(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
        public static Vector2Int FloorToInt(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static Vector2Int CeilToInt(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));

        public static implicit operator Vector2(Vector2Int v) => new(v.x, v.y);

        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.x - b.x, a.y - b.y);
        public static Vector2Int operator -(Vector2Int a) => new(-a.x, -a.y);
        public static Vector2Int operator *(Vector2Int a, int d) => new(a.x * d, a.y * d);
        public static Vector2Int operator *(int d, Vector2Int a) => new(a.x * d, a.y * d);

        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);

        public bool Equals(Vector2Int other) => this == other;
        public override bool Equals(object obj) => obj is Vector2Int other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y);
        public override string ToString() => $"({x}, {y})";
    }
}
