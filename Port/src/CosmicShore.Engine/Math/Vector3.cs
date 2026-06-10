using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// 3-component float vector with the field names and API surface the ported
    /// gameplay code expects (left-handed, +z forward, +y up — same coordinate
    /// conventions as the original game so all tuned values carry over unchanged).
    /// </summary>
    [Serializable]
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0f; }

        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 one => new(1f, 1f, 1f);
        public static Vector3 up => new(0f, 1f, 0f);
        public static Vector3 down => new(0f, -1f, 0f);
        public static Vector3 left => new(-1f, 0f, 0f);
        public static Vector3 right => new(1f, 0f, 0f);
        public static Vector3 forward => new(0f, 0f, 1f);
        public static Vector3 back => new(0f, 0f, -1f);
        public static Vector3 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public static Vector3 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        public float this[int index]
        {
            get => index switch
            {
                0 => x,
                1 => y,
                2 => z,
                _ => throw new IndexOutOfRangeException($"Invalid Vector3 index {index}")
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException($"Invalid Vector3 index {index}");
                }
            }
        }

        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;

        public Vector3 normalized
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
            if (mag > 1E-05f) { x /= mag; y /= mag; z /= mag; }
            else { x = 0f; y = 0f; z = 0f; }
        }

        public void Set(float newX, float newY, float newZ) { x = newX; y = newY; z = newZ; }

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Vector3 Cross(Vector3 a, Vector3 b) => new(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x);

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t)
            => new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);

        public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            float dot = Mathf.Clamp(Dot(a.normalized, b.normalized), -1f, 1f);
            float theta = MathF.Acos(dot) * t;
            Vector3 relative = (b - a * dot).normalized;
            float magLerp = Mathf.Lerp(a.magnitude, b.magnitude, t);
            return (a.normalized * MathF.Cos(theta) + relative * MathF.Sin(theta)) * magLerp;
        }

        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            Vector3 toVector = target - current;
            float dist = toVector.magnitude;
            if (dist <= maxDistanceDelta || dist < float.Epsilon) return target;
            return current + toVector / dist * maxDistanceDelta;
        }

        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity,
            float smoothTime, float maxSpeed, float deltaTime)
        {
            float vx = currentVelocity.x, vy = currentVelocity.y, vz = currentVelocity.z;
            // Component-wise critically damped springs share the same clamped change vector
            // in the reference implementation; component-wise is equivalent for our usage.
            float ox = Mathf.SmoothDamp(current.x, target.x, ref vx, smoothTime, maxSpeed, deltaTime);
            float oy = Mathf.SmoothDamp(current.y, target.y, ref vy, smoothTime, maxSpeed, deltaTime);
            float oz = Mathf.SmoothDamp(current.z, target.z, ref vz, smoothTime, maxSpeed, deltaTime);
            currentVelocity = new Vector3(vx, vy, vz);
            return new Vector3(ox, oy, oz);
        }

        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime)
            => SmoothDamp(current, target, ref currentVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);

        public static Vector3 Scale(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public void Scale(Vector3 scale) { x *= scale.x; y *= scale.y; z *= scale.z; }

        public static Vector3 Project(Vector3 vector, Vector3 onNormal)
        {
            float sqrMag = Dot(onNormal, onNormal);
            if (sqrMag < Mathf.Epsilon) return zero;
            return onNormal * (Dot(vector, onNormal) / sqrMag);
        }

        public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
            => vector - Project(vector, planeNormal);

        public static Vector3 Reflect(Vector3 inDirection, Vector3 inNormal)
            => inDirection - 2f * Dot(inDirection, inNormal) * inNormal;

        public static float Angle(Vector3 from, Vector3 to)
        {
            float denominator = MathF.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (denominator < 1E-15f) return 0f;
            float dot = Mathf.Clamp(Dot(from, to) / denominator, -1f, 1f);
            return MathF.Acos(dot) * Mathf.Rad2Deg;
        }

        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float unsignedAngle = Angle(from, to);
            float sign = Mathf.Sign(Dot(axis, Cross(from, to)));
            return unsignedAngle * sign;
        }

        public static Vector3 ClampMagnitude(Vector3 vector, float maxLength)
        {
            float sqrMag = vector.sqrMagnitude;
            if (sqrMag > maxLength * maxLength)
            {
                float mag = MathF.Sqrt(sqrMag);
                return vector / mag * maxLength;
            }
            return vector;
        }

        public static Vector3 Min(Vector3 a, Vector3 b) => new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);

        // Same tolerance contract as the original engine: equality is approximate.
        public static bool operator ==(Vector3 lhs, Vector3 rhs) => (lhs - rhs).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 lhs, Vector3 rhs) => !(lhs == rhs);

        public bool Equals(Vector3 other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is Vector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";

        public static implicit operator System.Numerics.Vector3(Vector3 v) => new(v.x, v.y, v.z);
        public static implicit operator Vector3(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    }
}
