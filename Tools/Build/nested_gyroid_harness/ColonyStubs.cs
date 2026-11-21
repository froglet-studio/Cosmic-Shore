// RUNTIME STUBS so the SHIPPED NestedGyroidColony.cs runs in the harness (Driver.cs, colony gates). Only what the
// colony book touches: UnityEngine.Object's bool conversion, a Vector3 that adds, a Quaternion that is the identity
// (the harness lays its colonies in the lattice frame), a SEEDABLE Random.Range and a settable Time.time. Cell,
// FloraConfigurationSO and NestedGyroidFlora are bare keys - the book never reads them.
#pragma warning disable CS0649
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Vector3 operator *(Quaternion q, Vector3 v) => v;
    }
    public static class Random
    {
        public static System.Random Rng = new System.Random(1);
        public static int Range(int minInclusive, int maxExclusive) => Rng.Next(minInclusive, maxExclusive);
    }
    public static class Time { public static float time; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration = 4 }
    public class RuntimeInitializeOnLoadMethodAttribute : System.Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }
}

namespace CosmicShore.Utility
{
    public class FloraConfigurationSO : UnityEngine.Object { }
}

namespace CosmicShore.Gameplay
{
    public class Cell : UnityEngine.Object { }
    public class NestedGyroidFlora : UnityEngine.Object { public readonly int Id; public NestedGyroidFlora(int id) { Id = id; } }
}
