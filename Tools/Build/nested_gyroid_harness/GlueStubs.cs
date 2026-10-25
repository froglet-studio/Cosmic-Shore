// TYPE-CHECK STUBS for the nested-gyroid Unity glue (NestedGyroidFlora, NestedGyroidConfigSO, ILayeredPrismscape,
// PrismscapeTopology, BlockscapeFollower, NestedGyroidColony). Every member stands for a REAL declaration and keeps its real
// signature and accessibility (copied by hand on 2026-10-06), so this catches a misspelt member, a wrong argument,
// a bad override or an accessibility mistake against THOSE signatures. It cannot catch a signature that has since
// changed in the real file, and it proves nothing about runtime behaviour - that is the Editor's job
// (Docs/UNITY_VERIFICATION_CHECKLIST.md, nested gyroid entry). Prism.cs / PrismRenderService.cs are NOT covered.
#pragma warning disable CS0067, CS0649, CS0169, CS0414, CS0108, CS0626, CS0660, CS0661
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public int GetInstanceID() => 0;
        public static implicit operator bool(Object o) => o != null;
    }
    public class Component : Object
    {
        public Transform transform; public GameObject gameObject;
        public bool TryGetComponent<T>(out T c) { c = default; return false; }
        public T GetComponent<T>() => default;
        public T[] GetComponentsInChildren<T>(bool includeInactive) => null;
    }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(IEnumerator e) => null; }
    public class Coroutine { }
    public class ScriptableObject : Object { }
    public class GameObject : Object { }
    public class Transform : Component
    {
        public Vector3 position, localScale, lossyScale, forward;
        public Quaternion rotation;
        public int childCount;
        public Matrix4x4 worldToLocalMatrix, localToWorldMatrix;
        public Transform GetChild(int i) => null;
        public Vector3 TransformPoint(Vector3 p) => p;
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { }
    }
    public class Collider : Component { public bool enabled; }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class Mesh : Object { public Bounds bounds; }
    public struct Bounds { public Vector3 min, max; }
    public struct Matrix4x4
    {
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => a;
        public Vector3 MultiplyPoint3x4(Vector3 p) => p;
    }
    public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } }
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 one;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 zero, up, forward;
        public float sqrMagnitude => 0; public float magnitude => 0; public Vector3 normalized => this;
        public void Normalize() { }
        public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a) => a;
        public static Vector3 operator *(Vector3 a, float b) => a; public static Vector3 operator *(float b, Vector3 a) => a;
        public static Vector3 operator /(Vector3 a, float b) => a;
        public static bool operator ==(Vector3 a, Vector3 b) => true; public static bool operator !=(Vector3 a, Vector3 b) => false;
        public static float Dot(Vector3 a, Vector3 b) => 0;
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v;
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
    }
    public struct Quaternion
    {
        public static Quaternion LookRotation(Vector3 f, Vector3 u) => default;
        public static Quaternion operator *(Quaternion a, Quaternion b) => a;
        public static Vector3 operator *(Quaternion a, Vector3 b) => b;
    }
    public struct Color { public float r, g, b, a; }
    public static class Mathf
    {
        public static float Max(float a, float b) => a; public static int Max(int a, int b) => a;
        public static float Min(float a, float b) => a; public static int Min(int a, int b) => a;
        public static float Abs(float a) => a; public static float Sign(float a) => a; public static float Exp(float a) => a;
        public static float Lerp(float a, float b, float t) => a; public static float Clamp01(float a) => a;
        public static int RoundToInt(float a) => 0; public static int FloorToInt(float a) => 0;
    }
    public static class Random
    {
        public static Quaternion rotationUniform; public static float value;
        public static int Range(int minInclusive, int maxExclusive) => minInclusive;
        public static float Range(float min, float max) => min;
    }
    public static class Time { public static float deltaTime, time; public static int frameCount; }
    public enum QueryTriggerInteraction { UseGlobal = 0, Ignore = 1, Collide = 2 }
    public static class Physics
    {
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation,
            int mask, QueryTriggerInteraction q) => 0;
    }
    public enum RuntimeInitializeLoadType { SubsystemRegistration = 4 }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class SerializeField : Attribute { }
    public class HideInInspector : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float f) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
}

namespace CosmicShore.Utility
{
    using UnityEngine;
    public enum CSLogChannel { Ecology = 1 << 20 }
    public static class CSDebug
    {
        public static bool IsVerbose(CSLogChannel channel) => false;
        public static void LogVerbose(CSLogChannel channel, object message) { }
        public static void LogWarning(object message, Object context) { }
        public static void LogError(object message, Object context) { }
    }
    public static class SafeLookRotation
    {
        public static bool TrySet(Transform target, Vector3 forward, Vector3 up, Object context = null, bool logError = true) => true;
    }
    // FloraConfigurationSO.cs
    public class FloraVariantTuning { public float LatticeScale = -1f; public int MaxTotalSpawnedObjects = -1; public float MaxTotalSpawnedObjectsScale; }
    public class FloraConfigurationSO : ScriptableObject { public CosmicShore.Data.Element Element = CosmicShore.Data.Element.None; }
    // FloraReproductionRules.cs
    public static class FloraReproductionRules
    {
        public static float ReproductionRateFor(CosmicShore.Data.Element element) => 1f;
        public static float ScaleCostPerChild(float authoredCost, float rate) => authoredCost;
    }
}

namespace CosmicShore.Gameplay
{
    using UnityEngine;
    using CosmicShore.Utility;
    public enum Domains { Unassigned = 0, Jade = 1, Ruby = 2, Gold = 4, Blue = 5 }
    public class Cell : MonoBehaviour
    {
        public bool FloraGrowingEnabled => true;
        public bool FloraPlantingEnabled => FloraGrowingEnabled;
        public float CurrentFaunaSpawnPeriod => 0f;
        public bool IsFloraAtCap(FloraConfigurationSO config) => false;
        public bool NucleusIsControlZone => true;
        public float ExpectedNucleusWorldRadius => 0f;
    }
    public class Trail { public List<Prism> TrailList; public CosmicShore.Data.PrismscapeDimension Dimension; }
    public class PrismProperties { public bool IsDangerous; }   // PrismProperties.cs
    public class Prism : MonoBehaviour
    {
        public Trail Trail; public bool destroyed; public Domains Domain;
        [SerializeField] public PrismProperties prismProperties;
        public void MakeDangerous() { }
        public void ChangeTeam(Domains d) { }
        public virtual void Initialize(string playerName = "") { }
        public void AdmitTargetScale(Vector3 target) { }
        public Vector3 TargetScale { get; set; }
        public void SetColorShade(float gain) { }
    }
    public class HealthPrism : Prism { public LifeForm LifeForm; }
    public class Spindle : MonoBehaviour { public LifeForm LifeForm; }
    public struct SpawnPoint { public SpawnPoint(Vector3 position, Quaternion rotation, Vector3 scale) { } }
    public static class EnvironmentPrismPool
    {
        public static T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Prism => prefab;
    }
    public class PrismSpatialIndex : MonoBehaviour
    {
        public static PrismSpatialIndex Instance;
        public bool IsAvailable => true;
        public int QuerySphere(Vector3 center, float radius, List<Prism> results) => 0;
    }
    public class VesselTransformer : MonoBehaviour { public float SpeedMultiplier => 1f; }
    public interface IVesselStatus
    {
        Vector3 Course { get; set; }
        Domains Domain { get; }
        VesselTransformer VesselTransformer { get; }
        float Speed { get; set; }
    }
    public abstract class LifeForm : MonoBehaviour
    {
        [SerializeField] protected HealthPrism healthPrism;
        [SerializeField] protected Spindle spindle;
        public Domains domain;
        protected Cell cell;
        public virtual void Initialize(Cell cell) { }
        public virtual void AddHealthBlock(HealthPrism healthPrism) { }
        public virtual void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "") { }
        public Spindle AddSpindle() => null;
        public virtual void RemoveSpindle(Spindle spindle) { }
        public virtual void ApplyVariantTuning(FloraVariantTuning tuning) { }
        protected virtual void Die(string killerName = "") { }
    }
    public abstract class Flora : LifeForm
    {
        [SerializeField] protected float growPeriod = 3f;
        protected Vector3 LeafSize { get; set; }
        public abstract void Grow();
        public abstract void Plant();
        public virtual bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into) => false;
        protected bool TryGetPlantPositionOverride(out Vector3 position) { position = default; return false; }
        protected Vector3 ResolveDispersalPoint(float legacyRadius) => default;
        protected virtual bool PrismSizeFixedByGrowthRule => false;
        protected virtual int PrismBudget => 0;
        protected void NotifyGrew(int prisms = 1) { }
        public FloraConfigurationSO SourceConfig => null;
        protected override void Die(string killerName = "") { }
        protected bool TrySpawnOneOffspring() => false;
        protected virtual bool TryResolveOffspringPlacement(out Vector3 position, out Quaternion rotation, out Vector3? up)
        { position = default; rotation = default; up = null; return true; }
        protected virtual void ConfigureOffspring(Flora child) { }
        protected Vector3 ClampToPlantingBand(Vector3 point) => point;
        protected virtual void OnDestroy() { }
        public override void AddHealthBlock(HealthPrism healthPrism) { }
        public override void Initialize(Cell cell) { }
        public override void ApplyVariantTuning(FloraVariantTuning tuning) { }
        public override void RemoveHealthBlock(HealthPrism healthPrism, string killername = "") { }
    }
}
