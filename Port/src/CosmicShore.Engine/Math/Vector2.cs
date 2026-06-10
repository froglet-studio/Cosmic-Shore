using System;

namespace CosmicShore.Engine
{
    /// <summary>2-component float vector matching the ported code's expected API surface.</summary>
    [Serializable]
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x;
        public float y;

        public Vector2(float x, float y) { this.x = x; this.y = y; }

        public static Vector2 zero => new(0f, 0f);
        public static Vector2 one => new(1f, 1f);
        public static Vector2 up => new(0f, 1f);
        public static Vector2 down => new(0f, -1f);
        public static Vector2 left => new(-1f, 0f);
        public static Vector2 right => new(1f, 0f);

        public float this[int index]
        {
            get => index switch
            {
                0 => x,
                1 => y,
                _ => throw new IndexOutOfRangeException($"Invalid Vector2 index {index}")
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    default: throw new IndexOutOfRangeException($"Invalid Vector2 index {index}");
                }
            }
        }

        public float magnitude => MathF.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;

        public Vector2 normalized
        {
            get
            {
                float mag = magnitude;
                return mag > 1E-05f ? this / mag : zero;
            }
        }

        public void Normalize()
        {
            float mag = magnitude;
            if (mag > 1E-05f) { x /= mag; y /= mag; }
            else { x = 0f; y = 0f; }
        }

        public void Set(float newX, float newY) { x = newX; y = newY; }

        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;

        public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t)
            => new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);

        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
        {
            Vector2 toVector = target - current;
            float dist = toVector.magnitude;
            if (dist <= maxDistanceDelta || dist < float.Epsilon) return target;
            return current + toVector / dist * maxDistanceDelta;
        }

        public static Vector2 Scale(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);

        public static float Angle(Vector2 from, Vector2 to)
        {
            float denominator = MathF.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (denominator < 1E-15f) return 0f;
            float dot = Mathf.Clamp(Dot(from, to) / denominator, -1f, 1f);
            return MathF.Acos(dot) * Mathf.Rad2Deg;
        }

        public static float SignedAngle(Vector2 from, Vector2 to)
        {
            float unsignedAngle = Angle(from, to);
            float sign = Mathf.Sign(from.x * to.y - from.y * to.x);
            return unsignedAngle * sign;
        }

        public static Vector2 ClampMagnitude(Vector2 vector, float maxLength)
        {
            float sqrMag = vector.sqrMagnitude;
            if (sqrMag > maxLength * maxLength)
            {
                float mag = MathF.Sqrt(sqrMag);
                return vector / mag * maxLength;
            }
            return vector;
        }

        public static Vector2 Min(Vector2 a, Vector2 b) => new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        public static Vector2 Max(Vector2 a, Vector2 b) => new(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new(a.x / d, a.y / d);

        public static bool operator ==(Vector2 lhs, Vector2 rhs) => (lhs - rhs).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector2 lhs, Vector2 rhs) => !(lhs == rhs);

        public static implicit operator Vector3(Vector2 v) => new(v.x, v.y, 0f);
        public static implicit operator Vector2(Vector3 v) => new(v.x, v.y);

        public bool Equals(Vector2 other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Vector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y);
        public override string ToString() => $"({x:F2}, {y:F2})";
    }
}
