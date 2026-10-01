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
    public class Object { public string name; public static void Destroy(Object o) { } public static T Instantiate<T>(T o, Vector3 p, Quaternion r) where T : Object => o; public static implicit operator bool(Object o) => o != null; }
    public class Component : Object { public Transform transform; public GameObject gameObject; public T GetComponent<T>() => default; public T GetComponentInParent<T>() => default; public T GetComponentInChildren<T>(bool b = false) => default; public T[] GetComponentsInChildren<T>(bool b = false) => default; public bool TryGetComponent<T>(out T c) { c = default; return false; } }
    public class Behaviour : Component { public bool enabled; public bool isActiveAndEnabled; }
    public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(System.Collections.IEnumerator e) => null; public void StopAllCoroutines() { } }
    public class Coroutine { }
    public class ScriptableObject : Object { }
    public class TextAsset : Object { public string text; }
    public class GameObject : Object { public bool activeInHierarchy; public Scene scene; public T AddComponent<T>() where T : Component => default; }
    public struct Scene { public bool isLoaded; }
    public class Transform : Component { public Vector3 position, localPosition, localScale; public Quaternion rotation; public void SetPositionAndRotation(Vector3 p, Quaternion q) { } }
    public class Collider : Component { }
    public struct Vector3 { public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; } public static Vector3 one, zero, right, forward, up; public float sqrMagnitude => 0; public float magnitude => 0; public Vector3 normalized => this; public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a; public static Vector3 operator *(Vector3 a, float b) => a; public static Vector3 operator *(float b, Vector3 a) => a; public static Vector3 operator /(Vector3 a, float b) => a; public static Vector3 Cross(Vector3 a, Vector3 b) => a; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a; public static float Dot(Vector3 a, Vector3 b) => 0; public static bool operator ==(Vector3 a, Vector3 b) => true; public static bool operator !=(Vector3 a, Vector3 b) => false; public override bool Equals(object o) => true; public override int GetHashCode() => 0; }
    public struct Vector4 { public float x, y, z, w; public Vector4(float a, float b, float c, float d) { x = a; y = b; z = c; w = d; } }
    public struct Quaternion { public static Quaternion LookRotation(Vector3 f, Vector3 u) => default; }
    public static class Mathf { public static float Max(float a, float b) => a; public static int Max(int a, int b) => a; public static float Min(float a, float b) => a; public static float Abs(float a) => a; public static float Clamp01(float a) => a; }
    public static class Random { public static int Range(int a, int b) => a; public static Vector3 onUnitSphere; }
    public static class Time { public static float time, deltaTime; }
    public static class Physics { public static int OverlapSphereNonAlloc(Vector3 p, float r, Collider[] res, int mask) => 0; }
    public static class JsonUtility { public static T FromJson<T>(string s) => default; }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float f) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
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
}

namespace CosmicShore.Utility
{
    public enum CSLogChannel { Ecology = 1 << 20 }
    public static class CSDebug { public static void LogVerbose(CSLogChannel c, object m) { } public static void LogWarning(object m) { } public static void LogError(object m) { } }
    public class FaunaConfigurationSO : UnityEngine.ScriptableObject { public float BandInnerRadius, BandOuterRadius; }
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

    public class PrismProperties { public bool IsDangerous, IsShielded, IsSuperShielded; }
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
    }
    public class HealthPrism : Prism { public LifeForm LifeForm; public Fauna ResolveOwnerFauna() => null; public void LeaveAsSkeleton(Transform t) { } }
    public class Crystal : MonoBehaviour { public bool IsEmbedded => false; public void SetEmbeddedIn(ILifeFormEntity o) { } }
    public static class LifeFormCrystal { public static Crystal EnsureElementalCrystal(Component owner, Element e) => null; }
    public interface ILifeFormEntity { }
    public interface IVesselStatus { Vector3 Course { get; set; } float Speed { get; set; } }
    public class LifeForm : MonoBehaviour { public Element Element => default; public bool IsDying => false; public Transform HeartTransform => null; }
    public class Flora : LifeForm { }
    public static class FloraHeartRegistry { public static Flora NearestToPoint(Vector3 from, Predicate<Flora> reject) => null; }
    public class Cell : MonoBehaviour { public float MembraneRadius => 0; public void RegisterSpawnedObject(GameObject o) { } public bool IsInsideNucleus(Vector3 p) => false; }
    public class PrismSpatialIndex { public static PrismSpatialIndex EnsureInstance() => null; public bool IsAvailable => true; public int QuerySphere(Vector3 c, float r, List<Prism> res) => 0; }
    public class FaunaNetworkSync { public static void ServerSpawn(Fauna f) { } }

    // Fauna.cs - only the members the swarm glue touches, with their real accessibility
    public abstract class Fauna : MonoBehaviour, ILifeFormEntity
    {
        public Domains domain;
        Vector3 _goal;
        public Vector3 Goal { get => _goal; set => _goal = value; }
        public bool IsInsideBand(Vector3 p) => true;
        protected bool IsPreyForMe(Vector3 position, Domains preyDomain) => true;
        public Cell HostCell => null;
        public FaunaConfigurationSO SourceConfig => null;
        protected virtual void ProvisionHeart(Element element) { }
        protected virtual void OnDestroy() { }
        public virtual float CurrentSpeed => 0f;
        public float HeartWorldScale => 0;
        public void ApplyHeartSize(float s) { }
        protected static bool IsShieldedMass(Prism prism) => false;
        protected static readonly Collider[] OverlapScratch = new Collider[256];
        protected static readonly List<Prism> FeedScratch = new(64);
        protected static int NonPrismOverlapMask => 0;
        protected HealthPrism[] CacheBodyPrisms() => null;
        protected HealthPrism[] BodyPrisms => null;
        public virtual void OnBodyPrismExploded(HealthPrism prism, string killerName) { }
        protected void NotifyBodyPrismsMoved() { }
        protected virtual void Awake() { }
        protected virtual void Start() { }
        public virtual void Initialize(Cell cell) { }
        protected bool DespawnOrDestroy() => false;
        protected Crystal crystal;
        protected void Die(string killerName = "") { }
        protected void LeaveSkeleton() { }
        public const string StarvationKiller = "starvation";
        protected virtual void OnDeath(string killerName = "") { }
        protected Transform DevourTarget { get; private set; }
        public virtual bool Predated(string predatorName, Transform devourTarget) => false;
        protected virtual Vector3 ResolveGoal() => Goal;
    }
}
