// TYPE-CHECK STUBS for the swarm glue (SwarmFauna / SwarmTadpoleFauna / SwarmFaunaConfigSO /
// SwarmPlanLibrary). Every member here was copied by hand from the REAL declaration it stands for
// (file:line noted where it is not obvious) - so this catches a misspelt member, a wrong argument,
// an accessibility mistake or a bad override against THOSE signatures. It cannot catch a signature
// that has since changed in the real file, and it proves nothing about runtime behaviour.
#pragma warning disable CS0067, CS0649, CS0169, CS0414, CS0108
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object { public string name; public HideFlags hideFlags; public int GetInstanceID() => 0; public static void Destroy(Object o) { } public static T Instantiate<T>(T o, Vector3 p, Quaternion r) where T : Object => o; public static implicit operator bool(Object o) => o != null; }
    [Flags] public enum HideFlags { None = 0, DontSave = 52 }
    public class Component : Object { public Transform transform; public GameObject gameObject; public T GetComponent<T>() => default; public T GetComponentInParent<T>(bool includeInactive = false) => default; public T GetComponentInChildren<T>(bool b = false) => default; public T[] GetComponentsInChildren<T>(bool b = false) => default; public bool TryGetComponent<T>(out T c) { c = default; return false; } }
    public class Behaviour : Component { public bool enabled; public bool isActiveAndEnabled; }
    public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(System.Collections.IEnumerator e) => null; public void StopAllCoroutines() { } }
    public class Coroutine { }
    public class ScriptableObject : Object { }
    public class TextAsset : Object { public string text; }
    public class GameObject : Object { public GameObject(string n) { } public bool activeInHierarchy; public Scene scene; public int layer; public Transform transform; public T AddComponent<T>() where T : Component => default; }
    public struct Scene { public bool isLoaded; }
    public class Transform : Component { public Vector3 position, localPosition, localScale; public Vector3 forward => default; public Vector3 up => default; public Vector3 right => default; public Quaternion rotation, localRotation; public Matrix4x4 localToWorldMatrix, worldToLocalMatrix; public void SetPositionAndRotation(Vector3 p, Quaternion q) { } public Vector3 InverseTransformPoint(Vector3 p) => p; public void SetParent(Transform p, bool worldPositionStays) { } public Transform parent; }
    public class Renderer : Component { public bool enabled; }
    public class SkinnedMeshRenderer : Renderer { public Mesh sharedMesh; }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class Mesh : Object { public int subMeshCount; }
    public class Shader : Object { public static int PropertyToID(string n) => 0; }
    public class Material : Object { public Material(Shader s) { } public bool HasProperty(string n) => false; public bool HasProperty(int n) => false; public Vector4 GetVector(int n) => default; public float GetFloat(int n) => 0; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; } public static implicit operator Vector4(Color c) => default; }
    public struct Matrix4x4 { public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33; public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) => default; public Vector4 GetColumn(int i) => default; public static Matrix4x4 identity; public static Matrix4x4 Rotate(Quaternion q) => default; public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => a; }
    public struct Bounds { public Bounds(Vector3 c, Vector3 s) { } public void SetMinMax(Vector3 min, Vector3 max) { } public Vector3 center => default; public Vector3 extents => default; }
    public struct Vector3Int { public int x, y, z; public Vector3Int(int a, int b, int c) { x = a; y = b; z = c; } }   // builders (BuilderRegistry)
    public sealed class MaterialPropertyBlock { public void SetFloat(int n, float v) { } public void SetVector(int n, Vector4 v) { } public void SetColor(int n, Color c) { } public void SetMatrix(int n, Matrix4x4 m) { } public void SetBuffer(int n, GraphicsBuffer b) { } public void SetVectorArray(int n, Vector4[] v) { } }
    public sealed class GraphicsBuffer : IDisposable
    {
        public enum Target { Structured = 16 }
        public GraphicsBuffer(Target t, int count, int stride) { }
        public void SetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count) { }
        public void Release() { } public void Dispose() { }
    }
    public struct RenderParams { public RenderParams(Material m) { worldBounds = default; matProps = null; shadowCastingMode = default; receiveShadows = false; layer = 0; } public Bounds worldBounds; public MaterialPropertyBlock matProps; public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows; public int layer; }
    public static class Graphics { public static void RenderMeshPrimitives(in RenderParams rp, Mesh mesh, int submeshIndex, int instanceCount = 1) { } }
    public static class SystemInfo { public static bool supportsComputeShaders; public static int maxComputeBufferInputsVertex; public static UnityEngine.Rendering.GraphicsDeviceType graphicsDeviceType; }
    public enum RuntimePlatform { WebGLPlayer = 17 }
    public static class Application { public static RuntimePlatform platform; }
    public class Camera : Behaviour { public static Camera main; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration = 4 }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public class Collider : Component { }
    public struct Vector3 { public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; } public static Vector3 one, zero, right, forward, up; public float sqrMagnitude => 0; public float magnitude => 0; public Vector3 normalized => this; public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a; public static Vector3 operator *(Vector3 a, float b) => a; public static Vector3 operator *(float b, Vector3 a) => a; public static Vector3 operator /(Vector3 a, float b) => a; public static Vector3 Cross(Vector3 a, Vector3 b) => a; public static Vector3 Max(Vector3 a, Vector3 b) => a; public static Vector3 Min(Vector3 a, Vector3 b) => a; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a; public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => a; public static float Dot(Vector3 a, Vector3 b) => 0; public static bool operator ==(Vector3 a, Vector3 b) => true; public static bool operator !=(Vector3 a, Vector3 b) => false; public override bool Equals(object o) => true; public override int GetHashCode() => 0; }
    public struct Vector4 { public float x, y, z, w; public Vector4(float a, float b, float c, float d) { x = a; y = b; z = c; w = d; } public static implicit operator Vector4(Vector3 v) => default; }
    public struct Quaternion { public static Quaternion LookRotation(Vector3 f, Vector3 u) => default; public static Quaternion identity; public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t) => a; }   // + identity / SlerpUnclamped (wearer bodies)
    public static class Mathf { public const float PI = 3.14159265f; public static int RoundToInt(float f) => 0; public static float Max(float a, float b) => a; public static int Max(int a, int b) => a; public static float Min(float a, float b) => a; public static float Abs(float a) => a; public static float Clamp01(float a) => a; public static int Clamp(int v, int a, int b) => v; public static float Pow(float a, float b) => a; public static float Sqrt(float a) => a; public static int NextPowerOfTwo(int v) => v; }
    public static class Random { public static int Range(int a, int b) => a; public static Vector3 onUnitSphere; public static float value; }
    public static class Time { public static float time, deltaTime, unscaledTime; public static int frameCount; }
    public static class Physics { public static int OverlapSphereNonAlloc(Vector3 p, float r, Collider[] res, int mask) => 0; }
    public static class JsonUtility { public static T FromJson<T>(string s) => default; }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float f) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off = 0 }
    public enum GraphicsDeviceType { Direct3D11 = 2 }
}

namespace Unity.Profiling
{
    public readonly struct ProfilerMarker
    {
        public ProfilerMarker(string name) { }
        public AutoScope Auto() => default;
        public void Begin() { } public void End() { }
        public readonly struct AutoScope : IDisposable { public void Dispose() { } }
    }
}

namespace FMODUnity
{
    public struct EventReference { public bool IsNull => true; }
    public class StudioEventEmitter : UnityEngine.MonoBehaviour { public EventReference EventReference; public void Play() { } }
}

namespace CosmicShore.Data
{
    public enum Element { None = 0, Charge = 1, Mass = 2, Space = 3, Time = 4, Omni = 5 }
    public enum Domains { Jade = 1, Ruby = 2, Blue = 3, Gold = 4 }
    public enum PrismKind { Plain = 0, Danger = 1, Shielded = 2, SuperShielded = 3 }
    public enum FaunaDiet { Herbivore = 0, Predator = 1 }
}

namespace CosmicShore.Utility
{
    public enum CSLogChannel { Ecology = 1 << 20 }
    public static class PrismClock { public static float Now => 0f; }   // Utility/PrismClock.cs:33
    public static class CSDebug { public static void LogVerbose(CSLogChannel c, object m) { } public static bool IsVerbose(CSLogChannel c) => false; public static void LogWarning(object m) { } public static void LogError(object m) { } }
    public class FaunaConfigurationSO : UnityEngine.ScriptableObject { public float BandInnerRadius, BandOuterRadius; }
}

namespace CosmicShore.ScriptableObjects
{
    using UnityEngine;
    using CosmicShore.Data;
    // SO_MaterialSet.cs:13-22 (the base material set the theme clones from)
    public class SO_MaterialSet : ScriptableObject { public Material BlockMaterial, ShieldedBlockMaterial, DangerousBlockMaterial; }
    // SO_ColorSet.cs:16, :240; DomainColorSet fields
    public class DomainColorSet { public Color DullCrystalColor, BrightCrystalColor; }
    public class SO_ColorSet : ScriptableObject
    {
        public bool TryGetColorSetByDomain(Domains domain, out DomainColorSet colorSet) { colorSet = null; return false; }
        public bool TryGetPrismKindColors(Domains domain, PrismKind kind, out Color bright, out Color dark) { bright = default; dark = default; return false; }
    }
    // ElementalCrystalSetSO.cs:82, :94
    public class ElementalCrystalSetSO : ScriptableObject
    {
        public CosmicShore.Gameplay.Crystal GetPrefab(Element element) => null;
        public static ElementalCrystalSetSO Load() => null;
    }
}

namespace CosmicShore.Core
{
    public class AudioSystem : UnityEngine.MonoBehaviour { public static AudioSystem Instance { get; private set; } public void PlaySFXEvent(FMODUnity.EventReference r, UnityEngine.Vector3 p) { } }
}

namespace CosmicShore.Gameplay.Audio { public class EmitterSfxVolumeBinder : UnityEngine.MonoBehaviour { } }

namespace CosmicShore.Gameplay
{
    using UnityEngine;
    using CosmicShore.Data;
    using CosmicShore.Utility;

    public class PrismProperties { public bool IsDangerous, IsShielded, IsSuperShielded; public float speedDebuffAmount; public Trail Trail; public float TimeCreated; }
    public class Trail { }
    // ThemeManagerDataContainerSO.cs:9-12
    public class ThemeManagerDataContainerSO : ScriptableObject { public CosmicShore.ScriptableObjects.SO_ColorSet ColorSet; public CosmicShore.ScriptableObjects.SO_MaterialSet BaseMaterialSet;
        public Dictionary<Domains, CosmicShore.ScriptableObjects.SO_MaterialSet> TeamMaterialSets; }
    public class Prism : MonoBehaviour
    {
        public PrismProperties prismProperties; public bool destroyed;
        public Vector3 TargetScale { get; set; } public float Volume => 0; public Domains Domain => default;
        public bool IsCreationComplete { get; private set; }
        public void AdmitTargetScale(Vector3 t) { } public void ChangeSize() { }
        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false, bool byCreature = false) { }
        public void MakeDangerous() { } public void DeactivateShields() { } public void ActivateShield() { }
        public void ChangeTeam(Domains d) { } public virtual void Initialize(string playerName = "") { }
        public void NotifyPositionChanged() { }
        public void CompleteGrowthImmediately() { }
        public void CompleteCreationImmediately() { }   // Prism.cs (round 8)
        public void SetOwnerHidden(bool hidden) { }
        public bool OwnerHidden => false;
        // Prism.cs:27, :195, :225, :1672 (builders)
        public Trail Trail; public string PlayerName { get; internal set; } internal CosmicShore.ECS.PrismRenderHandle RenderHandle; internal int SpatialIndexId = -1;   // Prism.cs:100 (wearer bodies)
        public void Steal(string playerName, Domains domain, bool superSteal = false) { }
    }
    public class HealthPrism : Prism { public LifeForm LifeForm; public Fauna ResolveOwnerFauna() => null; public void LeaveAsSkeleton(Transform t) { } }
    public class Crystal : MonoBehaviour { public bool IsEmbedded => false; public void SetEmbeddedIn(ILifeFormEntity o) { } }
    public static class LifeFormCrystal { public static Crystal EnsureElementalCrystal(Component owner, Element e) => null; }
    public interface ILifeFormEntity { }
    public interface IVesselStatus { Vector3 Course { get; set; } float Speed { get; set; } string PlayerName { get; } Domains Domain { get; } }
    public class LifeForm : MonoBehaviour { public Element Element => default; public Domains Domain => default; public bool IsDying => false; public Transform HeartTransform => null; }
    public class Flora : LifeForm { }
    public static class FloraHeartRegistry { public static Flora NearestToPoint(Vector3 from, Predicate<Flora> reject) => null; }
    public class Cell : MonoBehaviour { public float MembraneRadius => 0; public void RegisterSpawnedObject(GameObject o) { } public bool IsInsideNucleus(Vector3 p) => false;
        public void BindVirtualMass(int spatialIndexId, Domains domain) { }   // Cell.cs (round 11a)
        // Cell.cs (round 8)
        public static int VolumeSlotOf(Domains d) => 0; public void SetVirtualVolume(Object source, double[] bySlot) { } public void ClearVirtualVolume(Object source) { } }
    // PrismSpatialIndex.cs:678-700 - the virtual-entry contract (PR #944 + round 11a)
    public interface IVirtualPrismOwner { Prism MaterialiseVirtualPrism(int slot); }
    public interface IVirtualPrismBudget { bool HasMaterialiseBudget(int slot); }
    public class PrismSpatialIndex
    {
        public static PrismSpatialIndex Instance => null;
        public static PrismSpatialIndex EnsureInstance() => null; public bool IsAvailable => true; public int QuerySphere(Vector3 c, float r, List<Prism> res) => 0;
        public int VirtualCount { get; private set; }
        // PrismSpatialIndex.cs RegisterVirtual (round 11a adds boundingRadius)
        public int RegisterVirtual(IVirtualPrismOwner owner, int slot, Unity.Mathematics.float3 position, int domain,
            float volume = 1f, bool shielded = false, bool superShielded = false, float boundingRadius = 0f) => -1;
        public void Unregister(int index) { }
        public void SetVirtualSuspended(int index, bool suspended) { }
        public void UpdatePositionsBatch(Unity.Collections.NativeArray<int> indices, Unity.Collections.NativeArray<Unity.Mathematics.float3> positions, int count) { }
        public void UpdatePosition(int index, Vector3 position) { }
        public void UpdateCellVolume(int index, float volume) { }
        public void UpdateVolume(int index, float volume) { }
        public void SetVirtualRadius(int index, float radius) { }
        public void UpdateShieldState(int index, bool shielded, bool superShielded) { }
        public void UpdateDomain(int index, int domain) { }
        public Prism ResolvePrism(int index, bool materialise) => null;
        public bool IsLiveVirtual(int index) => false;
        public bool TryGetVirtual(int index, out IVirtualPrismOwner owner, out int slot) { owner = null; slot = -1; return false; }
        public bool TryGetVirtualEntry(int index, out Vector3 position, out Domains domain, out float boundingRadius) { position = default; domain = default; boundingRadius = 0; return false; }
        public bool HasMaterialiseBudget(int index) => true;
        public int QuerySphereVirtualIds(Vector3 center, float radius, List<int> results) => 0;
        // PrismSpatialIndex.cs TryReserve / ReleaseReservation (builders' claim-before-place)
        public bool TryReserve(Vector3 position, float clearRadius) => true; public void ReleaseReservation(Vector3 position) { }
    }
    public class FaunaNetworkSync { public static void ServerSpawn(Fauna f) { } }

    // Fauna.cs - only the members the swarm glue touches, with their real accessibility
    public abstract class Fauna : MonoBehaviour, ILifeFormEntity
    {
        public Domains domain;
        Vector3 _goal;
        public Vector3 Goal { get => _goal; set => _goal = value; }
        public bool IsInsideBand(Vector3 p) => true;
        protected bool HasBand => false;
        protected bool IsPreyForMe(Vector3 position, Domains preyDomain) => true;
        protected bool IsPreyForMe(Vector3 position, Domains preyDomain, Domains eaterDomain) => true;   // Fauna.cs (round 8)
        public void SetTeam(Domains domain) { }                                                       // Fauna.cs
        protected virtual bool AcceptsTeamRecolour => true;                                           // Fauna.cs (round 8)
        protected virtual void OnTeamChanged() { }                                                    // Fauna.cs (round 8)
        public Cell HostCell => null;
        public FaunaConfigurationSO SourceConfig => null;
        protected virtual void ProvisionHeart(Element element) { }
        protected virtual void OnDestroy() { }
        public virtual float CurrentSpeed => 0f;
        public float HeartWorldScale => 0;
        public void ApplyHeartSize(float s) { }
        public static bool IsShieldedMass(Prism prism) => false;          // Fauna.cs (public since round 11c)
        protected static readonly Collider[] OverlapScratch = new Collider[256];
        protected static readonly List<Prism> FeedScratch = new(64);
        protected static int NonPrismOverlapMask => 0;
        public static int VesselSenseMask => NonPrismOverlapMask;   // Fauna.cs (round 11f)
        protected HealthPrism[] CacheBodyPrisms() => null;
        protected HealthPrism[] BodyPrisms => null;
        public virtual void OnBodyPrismExploded(HealthPrism prism, string killerName) { }
        protected void NotifyBodyPrismsMoved() { }
        protected virtual void Awake() { }
        protected virtual void Start() { }
        public virtual void Initialize(Cell cell) { }
        protected bool DespawnOrDestroy() => false;
        protected Crystal crystal;
        public Crystal LivingHeart => null;                 // Fauna.cs (round 11a)
        protected void Die(string killerName = "") { }
        protected void LeaveSkeleton() { }
        public const string StarvationKiller = "starvation";
        protected virtual void OnDeath(string killerName = "") { }
        protected Transform DevourTarget { get; private set; }
        public virtual bool Predated(string predatorName, Transform devourTarget) => false;
        protected virtual Vector3 ResolveGoal() => Goal;
        protected void BackdateSpawn(float ageSeconds) { }   // Fauna.cs (round 8)
        public virtual void NotifyHunted() { }              // Fauna.cs (round 8)
        public float PredationImmunitySeconds => 0f;        // Fauna.cs (round 8)
        public FaunaDiet Diet => default;                   // Fauna.cs:168
        public bool IsAlivePrey => true;                    // Fauna.cs:1096
        public bool IsPredationImmune => false;             // Fauna.cs:181
        public bool Jousted(string killerName) => false;    // Fauna.cs:498 (builders)
    }

    // ThreatFlora/ThreatGrove.cs (round 11c) - the two statics the builders' glue asks
    public sealed class ThreatGrove : MonoBehaviour
    {
        public static bool IsGroveTissue(Prism prism) => false;
        public static Vector3 OutsideGroves(Vector3 p, float clearance) => p;
    }
}

// Unity.Collections / Unity.Mathematics - only the members the glue touches (com.unity.collections 2.x,
// com.unity.mathematics 1.x signatures)
namespace Unity.Collections
{
    public sealed class ReadOnlyAttribute : Attribute { }
    public sealed class WriteOnlyAttribute : Attribute { }
    public enum Allocator { Invalid = 0, None = 1, Temp = 2, TempJob = 3, Persistent = 4 }
    public enum NativeArrayOptions { UninitializedMemory = 0, ClearMemory = 1 }
    public struct NativeArray<T> : IDisposable where T : struct
    {
        public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) { Length = length; }
        public int Length { get; }
        public bool IsCreated => false;
        public T this[int index] { get => default; set { } }
        public void Dispose() { }
        public void CopyFrom(T[] array) { }
        public NativeArray<U> Reinterpret<U>() where U : struct => default;
        public NativeArray<U> Reinterpret<U>(int expectedTypeSize) where U : struct => default;
        public static void Copy(T[] src, int srcIndex, NativeArray<T> dst, int dstIndex, int length) { }
    }
}

namespace Unity.Mathematics
{
    public struct float3 { public float x, y, z; public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static implicit operator float3(UnityEngine.Vector3 v) => default; }
    public struct float4 { public float x, y, z, w; public float4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } }
    public struct float4x4 { public float4 c0, c1, c2, c3; public float4x4(float4 c0, float4 c1, float4 c2, float4 c3) { this.c0 = c0; this.c1 = c1; this.c2 = c2; this.c3 = c3; }
        public float4x4(float m00, float m01, float m02, float m03, float m10, float m11, float m12, float m13, float m20, float m21, float m22, float m23, float m30, float m31, float m32, float m33) { c0 = c1 = c2 = c3 = default; } }
}

// PrismRenderService.cs / PrismRenderHandle.cs (PR #944 + round 11a SetLooksBatch) - the entity render API
namespace CosmicShore.ECS
{
    using Unity.Collections;
    public enum PrismRenderOverrideSet { Prism = 0, Explosion = 1, Implosion = 2, Slice = 3 }
    public struct PrismRenderHandle { public static readonly PrismRenderHandle Invalid = default; }
    public static class PrismRenderService
    {
        public static bool Enabled => false;
        public static bool CreateBatch(UnityEngine.Mesh mesh, UnityEngine.Material material, int layer,
            NativeArray<Unity.Mathematics.float4x4> localToWorld, NativeArray<PrismRenderHandle> outHandles,
            PrismRenderOverrideSet overrideSet = PrismRenderOverrideSet.Prism) => false;
        public static void SetTransformsBatch(NativeArray<PrismRenderHandle> handles, NativeArray<Unity.Mathematics.float4x4> localToWorld, int count = -1) { }
        public static void SetTransformsBatch(NativeArray<PrismRenderHandle> handles, NativeArray<Unity.Mathematics.float4x4> localToWorld, int count, Unity.Jobs.JobHandle dependsOn) { }
        public static void SetLooksBatch(NativeArray<PrismRenderHandle> handles, NativeArray<byte> lookIndex, int count, UnityEngine.Material[] looks) { }
        public static void QueueVisible(in PrismRenderHandle handle, bool visible) { }
        public static bool IsHandleUsable(in PrismRenderHandle handle) => false;   // PrismRenderService.cs:312 (wearer bodies)
        public static void Destroy(ref PrismRenderHandle handle) { }
        // PrismRenderService.cs StampFlight / ClearFlightStamp / EncapsulateBoundsPoint (builders' settle flight)
        public static bool StampFlight(in PrismRenderHandle handle, float startTime, float duration, in Unity.Mathematics.float3 velocity) => false;
        public static void ClearFlightStamp(in PrismRenderHandle handle) { }
        public static void EncapsulateBoundsPoint(in PrismRenderHandle handle, in Unity.Mathematics.float3 objectPoint, float padding) { }
    }
}

// Unity.Jobs / Unity.Burst - the members SwarmPoseJob and its scheduling touch (com.unity.jobs / burst signatures)
namespace Unity.Burst { public sealed class BurstCompileAttribute : Attribute { } }
namespace Unity.Jobs
{
    public struct JobHandle
    {
        public void Complete() { }
        public bool IsCompleted => true;
        public static void ScheduleBatchedJobs() { }
        public static JobHandle CombineDependencies(JobHandle a, JobHandle b) => default;
    }
    public interface IJobParallelFor { void Execute(int index); }
    public static class IJobParallelForExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, IJobParallelFor => default;
        public static void Run<T>(this T jobData, int arrayLength) where T : struct, IJobParallelFor { }
    }
}
