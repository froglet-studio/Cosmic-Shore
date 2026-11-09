// The Vector2 surface SerpentineGate.cs and SerpentHoodLayoutTests.cs use, beside race_course_source_harness's
// UnityMath.cs (which has Vector3, Quaternion and Mathf but no Vector2).
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new(0f, 0f);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float s) => new(a.x * s, a.y * s);
        public static Vector2 operator *(float s, Vector2 a) => new(a.x * s, a.y * s);
        public static Vector2 operator /(Vector2 a, float s) => new(a.x / s, a.y / s);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public override string ToString() => $"({x:F2}, {y:F2})";
    }
}
