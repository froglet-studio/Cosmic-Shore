// Stub surface for the elemental-transfer harness. Every signature here is COPIED VERBATIM from
// the shipped file named above it, so a wrong member name or arity in the code under test is a
// compile error rather than something a human discovers in the editor. It is deliberately the
// MINIMUM surface the files under test touch - a bigger stub is a bigger chance of the stub and
// the project disagreeing without anyone noticing.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized => this;
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => a * d;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 ClampMagnitude(Vector3 v, float max) => v;
    }
    public struct Quaternion { public static Quaternion identity => default; }
    public static class Mathf
    {
        public const float Epsilon = 1e-5f;
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Abs(float f) => Math.Abs(f);
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 1e-6f;
        public static float MoveTowards(float a, float b, float d) => a;
    }
    public static class Time { public static float deltaTime => 0.016f; public static float time => 0f; }
    public static class Random
    {
        public static Vector3 onUnitSphere => new Vector3(0, 1, 0);
        public static Quaternion rotation => Quaternion.identity;
        public static int Range(int a, int b) => a;
    }
    public class Object
    {
        public string name;
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => original;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
    }
    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponentInChildren<T>(bool includeInactive) => default;
        public T[] GetComponentsInChildren<T>(bool includeInactive) => new T[0];
        public bool TryGetComponent<T>(out T component) { component = default; return false; }
    }
    public class Behaviour : Component { public bool enabled { get; set; } }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public class Transform : Component { public Vector3 position { get; set; } public Vector3 localScale { get; set; } }
    public class GameObject : Object
    {
        public T AddComponent<T>() where T : Component, new() => new T();
        public void SetActive(bool v) { }
    }
    public class Renderer : Component { }
    public class AttrBase : Attribute { }
    public class SerializeField : AttrBase { }
    public class TooltipAttribute : AttrBase { public TooltipAttribute(string s) { } }
    public class HeaderAttribute : AttrBase { public HeaderAttribute(string s) { } }
    public class DisallowMultipleComponent : AttrBase { }
    public class MinAttribute : AttrBase { public MinAttribute(float f) { } }
    public class RangeAttribute : AttrBase { public RangeAttribute(float a, float b) { } }
    public class CreateAssetMenuAttribute : AttrBase { public string fileName; public string menuName; }
}

namespace CosmicShore.Utility
{
    // Assets/_Scripts/Utility/CSDebug.cs
    public static class CSDebug
    {
        public static void LogError(string m) { }
        public static void LogWarning(string m) { }
    }
    // Assets/_Scripts/Utility/PrismOcclusionCorridor.cs:152
    public static class PrismOcclusionCorridor
    {
        public static float MeasureCircumscribedRadius(UnityEngine.Transform vessel) => 1f;
    }
    // Assets/_Scripts/Utility/FadeIn.cs
    public class FadeIn : UnityEngine.MonoBehaviour { public void StartFadeIn() { } }
}

namespace CosmicShore.ScriptableObjects
{
    // Assets/_Scripts/ScriptableObjects/ElementalCrystalSetSO.cs
    public class ElementalCrystalSetSO : UnityEngine.ScriptableObject
    {
        public const string ResourcePath = "ElementalCrystalSet";
        public CosmicShore.Gameplay.SkimmerCrystalEffectSO[] CollectionEffects => null;
        public CosmicShore.Gameplay.Crystal GetPrefab(CosmicShore.Data.Element element) => null;
        public static ElementalCrystalSetSO Load() => null;
    }
}
