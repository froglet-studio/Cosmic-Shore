// The slice of UnityEngine the SHIPPED DepthRatchet.cs, CrystalPoseJitter.cs, CrystalDepthPrismProfileSO.cs and
// CrystalDepthPrisms.cs compile against, with the value maths exactly as Unity defines it (float ops, Lerp clamps,
// normalized below 1e-5 is zero, Pow/Sqrt through double). Behaviours, renderers and materials are inert shells.
//
// Why not ../../crystal_depth_harness/UnityStub.cs or ../../omni_turns_harness/UnityStub.cs: CrystalDepthPrisms.cs
// needs Mathf.Lerp + Mathf.Pow together, Vector4, ScriptableObject, Transform.childCount / GetChild / localRotation /
// InverseTransformPoint, Mesh.GetUVs and MeshRenderer, and neither existing stub has all of them on its (non-partial)
// types; this folder may not edit them. Nothing under Assets/ is copied: the shipped files are compiled by path.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Abs(float v) => Math.Abs(v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 one => new(1f, 1f, 1f);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new(a.x / s, a.y / s, a.z / s);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new(0f, 0f, 0f, 1f);
        public static Vector3 operator *(Quaternion q, Vector3 v) => v;          // poses are never exercised here
        public static Quaternion operator *(Quaternion a, Quaternion b) => b;
    }

    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Pose(Vector3 p, Quaternion r) { position = p; rotation = r; }
    }

    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public Vector3 extents => size * 0.5f;
    }

    public class Object
    {
        public string name { get; set; } = "";
        public static void Destroy(Object o) { }
        public static implicit operator bool(Object o) => o != null;
    }

    public class Component : Object
    {
        public Transform transform => null;
        public T GetComponentInChildren<T>(bool all) => default;
        public T[] GetComponentsInChildren<T>(bool all) => new T[0];
        public bool TryGetComponent<T>(out T c) { c = default; return false; }
        public T GetComponent<T>() => default;
    }

    public class Transform : Component
    {
        public Transform parent;
        public Vector3 position, localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public int childCount => 0;
        public Transform GetChild(int i) => null;
        public Vector3 InverseTransformPoint(Vector3 p) => p;
        public void SetLocalPositionAndRotation(Vector3 p, Quaternion r) { localPosition = p; localRotation = r; }
    }

    public class Behaviour : Component { public bool enabled { get; set; } = true; }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject, new() => new T(); }
    public class Camera : Behaviour { }
    public class Material : Object { public bool HasProperty(int id) => false; public float GetFloat(int id) => 0f; }
    public class MaterialPropertyBlock { public void SetVectorArray(int id, Vector4[] v) { } public void SetFloat(int id, float v) { } }
    public static class Shader { public static int PropertyToID(string n) => n.GetHashCode(); }
    public class Renderer : Component
    {
        public bool isVisible => true;
        public Bounds localBounds { get; set; }
        public Material sharedMaterial { get; set; }
        public void GetPropertyBlock(MaterialPropertyBlock b) { }
        public void SetPropertyBlock(MaterialPropertyBlock b) { }
    }
    public class MeshRenderer : Renderer { }
    public class SkinnedMeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh sharedMesh { get; set; } }

    public class Mesh : Object
    {
        Vector3[] _v = new Vector3[0];
        readonly Dictionary<int, Vector4[]> _uv = new();
        public Vector3[] vertices { get => (Vector3[])_v.Clone(); set => _v = (Vector3[])value.Clone(); }
        public bool isReadable => true;
        public int vertexCount => _v.Length;
        public Bounds bounds { get; set; }
        public void SetUVs(int channel, Vector4[] uv) => _uv[channel] = (Vector4[])uv.Clone();
        public void GetUVs(int channel, List<Vector4> into) { into.Clear(); if (_uv.TryGetValue(channel, out var a)) into.AddRange(a); }
    }

    public static class Time { public static float time; }

    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class MinAttribute : Attribute { public MinAttribute(float a) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
}

namespace Unity.Profiling
{
    public readonly struct ProfilerMarker
    {
        public ProfilerMarker(string n) { }
        public AutoScope Auto() => new();
        public readonly struct AutoScope : IDisposable { public void Dispose() { } }
    }
}

namespace CosmicShore.Utility
{
    public static class CSDebug
    {
        public static void LogError(string m, UnityEngine.Object o = null) => Console.Error.WriteLine("LogError: " + m);
    }

    // Only CrystalDepthPrisms.Start calls it (never run here); the real file needs the skinned-mesh surface.
    public static class RhombusSkinBaker
    {
        public static bool TryCutPlates(UnityEngine.SkinnedMeshRenderer r, int corners, out string problem) { problem = "stub"; return false; }
    }
}

namespace CosmicShore.Gameplay
{
    public class CameraManager { public static UnityEngine.Camera ViewCamera => null; }
}
