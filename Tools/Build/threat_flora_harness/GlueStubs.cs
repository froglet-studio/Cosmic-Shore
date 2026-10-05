// TYPE-CHECK STUBS for the threat-flora glue (ThreatGrove / SnapTrapFlora / PhysarumSclerotium /
// ThreatGroveConfigSO). Every member here was copied by hand from the REAL declaration it stands for (file noted
// where it is not obvious), with its real accessibility, so this catches a misspelt member, a wrong argument, an
// accessibility mistake or a bad override against THOSE signatures. It cannot catch a signature that has since
// changed in the real file, and it proves nothing about runtime behaviour (Docs/THREAT_FLORA.md §6).
#pragma warning disable CS0067, CS0649, CS0169, CS0414, CS0108
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object { public string name; public int GetInstanceID() => 0; public static void Destroy(Object o) { } public static implicit operator bool(Object o) => o != null; }
    public class Component : Object { public Transform transform; public GameObject gameObject; public T GetComponentInParent<T>() => default; public bool TryGetComponent<T>(out T c) { c = default; return false; } }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public class GameObject : Object { public GameObject(string n) { } public Transform transform; public T AddComponent<T>() where T : Component => default; }
    public class Transform : Component
    {
        public Vector3 position; public Quaternion rotation; public Transform parent;
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetPositionAndRotation(Vector3 p, Quaternion q) { }
    }
    public class Collider : Component { }
    public class Renderer : Component { public Material sharedMaterial; }
    public class MeshRenderer : Renderer { }
    public class Shader : Object { public static int PropertyToID(string n) => 0; }
    public class Material : Object { public bool HasProperty(int n) => false; public Vector4 GetVector(int n) => default; }
    public struct Color { public float r, g, b, a; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 zero, one, up, down, left, right, forward, back;
        public float sqrMagnitude => 0; public float magnitude => 0; public Vector3 normalized => this;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a) => a;
        public static Vector3 operator *(Vector3 a, float b) => a; public static Vector3 operator *(float b, Vector3 a) => a;
        public static Vector3 operator /(Vector3 a, float b) => a;
        public static float Dot(Vector3 a, Vector3 b) => 0;
    }
    public struct Vector4 { public float x, y, z, w; }
    public struct Quaternion { public static Quaternion LookRotation(Vector3 f) => default; public static Quaternion LookRotation(Vector3 f, Vector3 u) => default; }
    public static class Mathf { public const float PI = 3.14159265f; public static float Max(float a, float b) => a; public static int Max(int a, int b) => a; public static float Abs(float a) => a; public static float Sqrt(float a) => a; public static float Cos(float a) => a; public static float Sin(float a) => a; public static float Tan(float a) => a; }
    public static class Random { public static Vector3 insideUnitSphere; public static float Range(float a, float b) => a; }
    public static class Time { public static float time, deltaTime; public static int frameCount; }
    public static class Physics { public static int OverlapSphereNonAlloc(Vector3 p, float r, Collider[] res, int mask) => 0; }
    public struct LayerMask { public static int GetMask(params string[] names) => 0; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration = 4 }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float f) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
}

namespace Unity.Mathematics
{
    public struct float3 { public float x, y, z; public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct float4 { public float x, y, z, w; public float4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } }
}

namespace Unity.Profiling
{
    public readonly struct ProfilerMarker
    {
        public ProfilerMarker(string name) { }
        public AutoScope Auto() => default;
        public readonly struct AutoScope : IDisposable { public void Dispose() { } }
    }
}

namespace CosmicShore.Data
{
    public enum Element { None = 0, Charge = 1, Mass = 2, Space = 3, Time = 4, Omni = 5 }          // Data/Enums/Element.cs
    public enum Domains { Jade = 1, Ruby = 2, Blue = 3, Gold = 4 }                                  // Data/Enums/Domains.cs
    public enum PrismKind { Plain = 0, Danger = 1, Shielded = 2, SuperShielded = 3 }               // Data/Enums/PrismKind.cs
}

namespace CosmicShore.Utility
{
    public enum CSLogChannel { Ecology = 1 << 20 }
    public static class CSDebug { public static void LogVerbose(CSLogChannel c, object m) { } public static bool IsVerbose(CSLogChannel c) => false; public static void LogWarning(object m) { } }
    public static class PrismClock { public static float Now => 0f; }                              // Utility/PrismClock.cs
}

namespace CosmicShore.ScriptableObjects
{
    using UnityEngine;
    using CosmicShore.Data;
    // SO_ColorSet.cs:240
    public class SO_ColorSet : ScriptableObject
    {
        public bool TryGetPrismKindColors(Domains domain, PrismKind kind, out Color bright, out Color dark) { bright = default; dark = default; return false; }
    }
}

// PrismRenderService.cs / PrismRenderHandle (CosmicShore.ECS)
namespace CosmicShore.ECS
{
    using Unity.Mathematics;
    public struct PrismRenderHandle { }
    public static class PrismRenderService
    {
        public static bool StampFlight(in PrismRenderHandle handle, float startTime, float duration, in float3 velocity) => false;   // :1228
        public static void SetColors(in PrismRenderHandle handle, in float4 bright, in float4 dark, in float3 spread) { }             // :1117
        public static bool StampColorTransition(in PrismRenderHandle handle, float startTime, float duration,
            in float4 startBright, in float4 startDark, in float3 startSpread) => false;                                               // :1198
        public static float4 ToFloat4(UnityEngine.Color c) => default;                                                                 // :1921
    }
}

namespace CosmicShore.Gameplay
{
    using UnityEngine;
    using CosmicShore.Data;

    public class ThemeManagerDataContainerSO : ScriptableObject { public CosmicShore.ScriptableObjects.SO_ColorSet ColorSet; }   // Controller/Managers
    public class PrismProperties { public bool IsDangerous, IsShielded, IsSuperShielded; }

    // Controller/Vessel/Prism.cs
    public class Prism : MonoBehaviour
    {
        public PrismProperties prismProperties; public bool destroyed;
        internal CosmicShore.ECS.PrismRenderHandle RenderHandle;
        public Domains Domain => default;
        public Vector3 TargetScale { get; set; }
        public float Volume => 0;
        public bool IsCreationComplete { get; private set; }
        public void AdmitTargetScale(Vector3 target) { }
        public virtual void Initialize(string playerName = "") { }
        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false, bool byCreature = false) { }
        public void MakeDangerous() { }
        public void DeactivateShields() { }
        public void ChangeTeam(Domains domain) { }
        public void NotifyPositionChanged() { }
    }

    // Controller/Environment/HealthPrism.cs
    public class HealthPrism : Prism { public LifeForm LifeForm; public Fauna ResolveOwnerFauna() => null; }

    public class Crystal : MonoBehaviour { }
    public class Spindle : MonoBehaviour { }
    public interface IVesselStatus { }
    public class Fauna : MonoBehaviour { public static bool IsShieldedMass(Prism prism) => false; }   // Fauna.cs (public since round 11c)

    // Controller/Environment/Cell.cs
    public class Cell : MonoBehaviour
    {
        public int ID;
        public bool IsPreyForHerbivore(Vector3 position, Domains faunaDomain, Domains preyDomain) => false;
    }

    // Utility/Singleton.cs + Controller/Managers/PrismSpatialIndex.cs:1012
    public class Singleton<T> : MonoBehaviour where T : Component { public static T Instance { get; private set; } }
    public class PrismSpatialIndex : Singleton<PrismSpatialIndex> { public int QuerySphere(Vector3 center, float radius, List<Prism> results) => 0; }

    // FloraAndFauna/Builders/BuilderRegistry.cs (round 11e)
    public static class BuilderRegistry { public static bool IsBuilt(Prism prism) => false; public static bool IsCarried(Prism prism) => false; }

    // Utility/PoolsAndBuffers/EnvironmentPrismPool.cs:75
    public static class EnvironmentPrismPool
    {
        public static T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Prism => prefab;
    }

    // Controller/Environment/FloraAndFauna/LifeForm.cs - the members the glue touches, real accessibility
    public abstract class LifeForm : MonoBehaviour
    {
        [SerializeField] protected HealthPrism healthPrism;
        public Domains domain;
        public Domains Domain => domain;
        public Element Element => default;
        public Transform HeartTransform => null;
        public bool IsDying => false;
        protected Crystal crystal;
        protected Cell cell;
        public virtual void AddHealthBlock(HealthPrism healthPrism) { }
        public virtual void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "") { }
        public Spindle AddSpindle() => null;
        protected virtual void Die(string killerName = "") { }
    }

    // Controller/Environment/FloraAndFauna/Flora.cs
    public abstract class Flora : LifeForm
    {
        public abstract void Grow();
        public abstract void Plant();
        protected bool TryGetPlantPositionOverride(out Vector3 position) { position = default; return false; }   // :119
        protected virtual bool PrismSizeFixedByGrowthRule => false;                                               // :386
        public override void AddHealthBlock(HealthPrism healthPrism) { }                                           // :388
        protected virtual int PrismBudget => 0;                                                                   // :479
        protected override void Die(string killerName = "") { }                                                   // :507
        protected void NotifyGrew(int prisms = 1) { }                                                             // :547
        protected bool TrySpawnOneOffspring() => false;                                                           // :638
        protected virtual bool TryResolveOffspringPlacement(out Vector3 position, out Quaternion rotation, out Vector3? up)
        { position = default; rotation = default; up = null; return false; }                                     // :694
        protected virtual void ConfigureOffspring(Flora child) { }                                                // :710
        protected virtual void OnDestroy() { }                                                                    // :742
        public override void RemoveHealthBlock(HealthPrism healthPrism, string killername = "") { }               // :751
    }
}
