// The first-party surface the photographed generators sit on.
//
// The REAL CellEnvironmentSpawnableBase drags in UniTask, Prism, PrismTrailBuilder and the whole
// spawn pipeline, none of which changes what an arena EMITS. What does change it is the protected
// generation surface, so every member of that surface here reproduces the shipped one exactly
// (same seeding, same System.Random draw order in Jit, same value noise in N01, same clearance test
// in Emit). The lay path (SpawnLeafObjects, the async builders, Trail, PrismTrailBuilder) is present
// only so the generators COMPILE - the harness never calls it, and every stand-in below that could
// be reached by mistake logs an error, which fails the run.
//
// Files compiled from the repo rather than copied here (see run.sh): the Data enums, SpawnPoint,
// SpawnTrailData, FloraPlantingSite, and every generator and course source.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CosmicShore.Data;
using UnityEngine;

namespace Cysharp.Threading.Tasks
{
    // `async UniTaskVoid` / `async UniTask` need a method builder to compile; these run the body
    // synchronously, which is never exercised (the lay path is not called by the harness).
    [AsyncMethodBuilder(typeof(UniTaskVoidBuilder))]
    public struct UniTaskVoid { public void Forget() { } }

    [AsyncMethodBuilder(typeof(UniTaskBuilder))]
    public struct UniTask
    {
        public void Forget() { }
        public Awaiter GetAwaiter() => default;
        public static UniTask CompletedTask => default;
        public static UniTask Yield() => default;
        public static UniTask Yield(PlayerLoopTiming t) => default;
        public static UniTask NextFrame() => default;
        public static UniTask Delay(int ms) => default;
        public struct Awaiter : INotifyCompletion
        {
            public bool IsCompleted => true;
            public void GetResult() { }
            public void OnCompleted(Action a) => a();
        }
    }
    public enum PlayerLoopTiming { Update = 0 }

    public struct UniTaskVoidBuilder
    {
        public static UniTaskVoidBuilder Create() => default;
        public UniTaskVoid Task => default;
        public void Start<T>(ref T sm) where T : IAsyncStateMachine => sm.MoveNext();
        public void SetStateMachine(IAsyncStateMachine sm) { }
        public void SetResult() { }
        public void SetException(Exception e) => throw e;
        public void AwaitOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : INotifyCompletion where TS : IAsyncStateMachine { }
        public void AwaitUnsafeOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : ICriticalNotifyCompletion where TS : IAsyncStateMachine { }
    }
    public struct UniTaskBuilder
    {
        public static UniTaskBuilder Create() => default;
        public UniTask Task => default;
        public void Start<T>(ref T sm) where T : IAsyncStateMachine => sm.MoveNext();
        public void SetStateMachine(IAsyncStateMachine sm) { }
        public void SetResult() { }
        public void SetException(Exception e) => throw e;
        public void AwaitOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : INotifyCompletion where TS : IAsyncStateMachine { }
        public void AwaitUnsafeOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : ICriticalNotifyCompletion where TS : IAsyncStateMachine { }
    }
}

namespace CosmicShore.Utility
{
    [AttributeUsage(AttributeTargets.Field)] public class CSLogChannelLabelAttribute : Attribute { public CSLogChannelLabelAttribute(string s) { } }

    public static class CSDebug
    {
        public static void Log(object o) => Console.Error.WriteLine($"[log] {o}");
        public static void LogWarning(object o) => Console.Error.WriteLine($"[warn] {o}");
        public static void LogError(object o) { Console.Error.WriteLine($"[error] {o}"); Harness.Faults++; }
        // Channelled telemetry: the channel enum itself is EXTRACTED from the real CSDebug.cs into
        // the build directory by run.sh, so a generator naming any channel compiles unchanged.
        public static bool IsVerbose(CSLogChannel c) => false;
        public static void LogVerbose(CSLogChannel c, object o) { }
    }
}

namespace CosmicShore.Gameplay
{
    using Cysharp.Threading.Tasks;

    public readonly struct PrismLay
    {
        public readonly SpawnPoint Point;
        public readonly Domains Domain;
        public readonly PrismKind Kind;
        public PrismLay(SpawnPoint point, Domains domain, PrismKind kind = PrismKind.Plain)
        { Point = point; Domain = domain; Kind = kind; }
    }

    /// <summary>Inert: only SpawnableWaypointTrack.Spawn touches these members, and the harness
    /// reads that generator through its PUBLIC preview API (GetPreviewBlocks) instead.</summary>
    public class Prism : MonoBehaviour
    {
        public string ownerID;
        public Vector3 TargetScale;
        public void ChangeTeam(Domains d) { }
        public void Initialize() { }
        public void AssignTrail(Trail t) { }
    }

    [Serializable]
    public class CrystalPositionSet
    {
        public List<Vector3> positions;
    }

    public class Trail
    {
        public PrismscapeDimension Dimension;
        public Trail() { }
        public Trail(bool isLoop) { }
        public void Add(Prism p) { }
    }

    /// <summary>Lay-path stand-in. Unreachable from the harness; loud if that ever changes.</summary>
    public static class PrismTrailBuilder
    {
        static void Unreached() => UnityEngine.Debug.LogError("PrismTrailBuilder reached from the card-art harness");
        public static void BeginArenaBuild() => Unreached();
        public static void EndArenaBuild() { }
        public static UniTask LayBudgetedAsync(Prism p, IEnumerable<PrismLay> lays, Transform c, Trail t, string n, float ms, bool admitAuthoredScale = false)
        { Unreached(); return default; }
        public static void LaySync(Prism p, IEnumerable<PrismLay> lays, Transform c, Trail t, string n, bool admitAuthoredScale = false) => Unreached();
        public static void WatchForReveal(Prism p) => Unreached();
    }

    /// <summary>The Switchyard registers its rails and burrs with the yard in SpawnLeafObjects,
    /// which the harness never calls. `params object[]` so a signature change there cannot break
    /// the build of a picture that does not depend on it.</summary>
    public class HijackYard : MonoBehaviour
    {
        public void AddBurr(params object[] a) { }
        public void AddRail(params object[] a) { }
    }

    public static class PrismLayDecimation
    {
        public static List<PrismLay> Apply(List<PrismLay> lays) => lays;
    }

    /// <summary>
    /// VERBATIM copy of the value noise <c>CellEnvironmentSpawnableBase.N01</c> reaches in
    /// <c>Assets/_Scripts/Controller/Toys/PaintingStrokeToolkit.cs</c> (copied because that file is
    /// ~1,000 lines and pulls in the ScriptableObjects assembly). The driver HASHES the real file
    /// into the render manifest, so a change there invalidates every committed card.
    /// CurlNoise is not copied: no photographed arena calls it, and if one starts to it fails the
    /// run instead of silently photographing a curl-free world.
    /// </summary>
    public static class PaintingStrokeToolkit
    {
        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647 + seed * 971);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x800000 - 1f; // [-1,1)
            }
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f); // smootherstep

        public static float ValueNoise(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = Fade(p.x - x0), fy = Fade(p.y - y0), fz = Fade(p.z - z0);

            float c000 = Hash(x0, y0, z0, seed), c100 = Hash(x0 + 1, y0, z0, seed);
            float c010 = Hash(x0, y0 + 1, z0, seed), c110 = Hash(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash(x0, y0, z0 + 1, seed), c101 = Hash(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash(x0, y0 + 1, z0 + 1, seed), c111 = Hash(x0 + 1, y0 + 1, z0 + 1, seed);

            float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
            float y0v = Mathf.Lerp(x00, x10, fy), y1v = Mathf.Lerp(x01, x11, fy);
            return Mathf.Lerp(y0v, y1v, fz);
        }

        public static Vector3 CurlNoise(Vector3 p, float freq, int seed)
        {
            UnityEngine.Debug.LogError("CurlNoise reached: copy it into the card-art PlatformShim before photographing this arena");
            return Vector3.zero;
        }
    }

    /// <summary>The shipped SpawnableBase's serialized surface, minus the spawn pipeline.</summary>
    public abstract class SpawnableBase : MonoBehaviour
    {
        [SerializeField] protected int seed;
        [SerializeField] public Domains domain = Domains.Blue;
        [SerializeField] protected List<SpawnableBase> children = new();
        [SerializeField] protected GameObject leafPrefab;
        [SerializeField] protected bool layAcrossFrames;
        [SerializeField] protected float layBudgetMsPerFrame = 6f;
        protected System.Random rng;
        protected List<Trail> trails = new();
        public int intensityLevel = 1;

        protected abstract int GetParameterHash();
        protected virtual SpawnPoint[] GeneratePoints() => null;
        protected virtual SpawnTrailData[] GenerateTrailData()
        {
            var points = GeneratePoints();
            if (points == null || points.Length == 0) return Array.Empty<SpawnTrailData>();
            return new[] { new SpawnTrailData(points, false, domain) };
        }
        protected virtual void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container) { }
        protected void SpawnPrismTrail(SpawnPoint[] points, GameObject container, Prism prismPrefab, bool isLoop = false, Domains? trailDomain = null) =>
            UnityEngine.Debug.LogError("SpawnPrismTrail reached from the card-art harness");
        protected void SpawnPrismTrail(SpawnPoint[] points, GameObject container, bool isLoop = false, Domains? trailDomain = null) =>
            UnityEngine.Debug.LogError("SpawnPrismTrail reached from the card-art harness");
        public virtual GameObject Spawn(int intensity = 1) { UnityEngine.Debug.LogError("Spawn reached from the card-art harness"); return null; }
        public IReadOnlyList<Trail> GetTrails() => trails;
        protected void InvalidateCache() { }

        public SpawnTrailData[] GenerateForHarness() => GenerateTrailData();
    }

    /// <summary>Same protected generation surface as the shipped base; Emit records instead of laying.</summary>
    public abstract class CellEnvironmentSpawnableBase : SpawnableBase
    {
        [SerializeField] protected Prism prism;
        protected const float LayBudgetMsPerFrame = 8f;
        [SerializeField] protected float density = 1f;
        [SerializeField] protected float spawnClearRadius = 0f;
        [SerializeField] protected Vector3[] spawnClearPoints = Array.Empty<Vector3>();
        protected const float GoldenAngle = 2.39996323f;
        protected List<PrismLay> _cachedLays;
        protected System.Random _r;
        protected int _noiseSeed;
        protected List<FloraPlantingSite> _plantingSites;

        protected abstract int DefaultSeed { get; }
        protected virtual int LayCapacity => 40000;
        protected abstract void BuildEnvironment();
        protected virtual bool AdmitsAuthoredPrismScale => false;
        protected abstract int BuildParameterHash();

        public IReadOnlyList<PrismLay> CachedLays => _cachedLays;
        public IReadOnlyList<FloraPlantingSite> PlantingSites => _plantingSites;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            _noiseSeed = seed != 0 ? seed : DefaultSeed;
            _r = new System.Random(_noiseSeed);
            _cachedLays = new List<PrismLay>(LayCapacity);
            _plantingSites = new List<FloraPlantingSite>();
            BuildEnvironment();
            var points = new SpawnPoint[_cachedLays.Count];
            for (int i = 0; i < _cachedLays.Count; i++) points[i] = _cachedLays[i].Point;
            return new[] { new SpawnTrailData(points, false, domain) };
        }

        protected override int GetParameterHash() => HashCode.Combine(density, seed, BuildParameterHash());

        protected float RangeF(float min, float max) => (float)(_r.NextDouble() * (max - min) + min);

        protected Vector3 Jit(Vector3 s, float amt = 0.2f)
        {
            float k = 1f + RangeF(-amt, amt);
            return new Vector3(Mathf.Max(0.5f, s.x * k), Mathf.Max(0.5f, s.y * k), Mathf.Max(0.5f, s.z * k));
        }

        protected static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n;
                h = (h ^ 61u) ^ (h >> 16);
                h *= 9u;
                h ^= h >> 4;
                h *= 0x27d4eb2du;
                h ^= h >> 15;
                return (h & 0xffffffu) / (float)0x1000000;
            }
        }

        protected float N01(float x, float y, float z, int seedOffset) =>
            0.5f * (PaintingStrokeToolkit.ValueNoise(new Vector3(x, y, z), _noiseSeed + seedOffset) + 1f);

        protected Vector3 Curl(Vector3 p, float freq, int seedOffset) =>
            PaintingStrokeToolkit.CurlNoise(p, freq, _noiseSeed + seedOffset);

        protected int Scaled(int n) => Mathf.Max(1, Mathf.RoundToInt(n * density));

        protected void Emit(Vector3 pos, Quaternion rot, Vector3 scale, Domains dom, PrismKind kind = PrismKind.Plain)
        {
            if (spawnClearPoints != null && spawnClearRadius > 0f)
            {
                float rr = spawnClearRadius * spawnClearRadius;
                for (int i = 0; i < spawnClearPoints.Length; i++)
                    if ((pos - spawnClearPoints[i]).sqrMagnitude < rr) return;
            }
            _cachedLays.Add(new PrismLay(new SpawnPoint(pos, rot, scale), dom, kind));
        }

        protected void Sow(Vector3 pos, Vector3 up, FloraSiteKind kind = FloraSiteKind.Bed) =>
            _plantingSites?.Add(new FloraPlantingSite(pos, up, kind));
    }
}
