// The few UnityEngine types BlackHoleConfigSO.cs and BlackHoleToolModel.cs touch, so the SHIPPED
// files compile and RUN under plain .NET (the engine's reference DLLs are metadata-only: every
// body is `throw null`). Members mirror Unity's names and semantics exactly; nothing here is the
// logic under test — that is the config's formulas and the tool model's reflection.
using System;

namespace UnityEngine
{
    public class Object { }
    public class ScriptableObject : Object { }
    public class Material : Object { }
    public class MonoBehaviour : Object { }

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    public class PropertyAttribute : Attribute { }
    public class HeaderAttribute : PropertyAttribute { public readonly string header; public HeaderAttribute(string header) { this.header = header; } }
    public class TooltipAttribute : PropertyAttribute { public readonly string tooltip; public TooltipAttribute(string tooltip) { this.tooltip = tooltip; } }
    public sealed class RangeAttribute : PropertyAttribute { public readonly float min, max; public RangeAttribute(float min, float max) { this.min = min; this.max = max; } }
    public sealed class MinAttribute : PropertyAttribute { public readonly float min; public MinAttribute(float min) { this.min = min; } }
    public sealed class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }

    public static class Mathf
    {
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Round(float f) => (float)Math.Round(f);
        public static float Abs(float f) => Math.Abs(f);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : zero; } }
        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : z;
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }
}
