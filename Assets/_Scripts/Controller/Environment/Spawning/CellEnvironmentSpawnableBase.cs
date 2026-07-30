using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shared plumbing for the authored CELL ENVIRONMENTS - the large deterministic prism
    /// gardens a <c>CellConfigDataSO.EnvironmentPrefab</c> (or a SegmentSpawner slot) spawns:
    /// Atlantis and the freestyle six (Yggdra, Daedala, Orrery, Zephyr, Caldera, Geode).
    /// One definition of the lay/stream/noise contract so seven generators cannot drift:
    ///
    ///   • DETERMINISM - clients build environments locally with no seed sync, so all
    ///     randomness flows from the serialized seed through one System.Random plus the
    ///     seeded noise in <see cref="PaintingStrokeToolkit"/>; a seed of 0 falls back to the
    ///     subclass's fixed <see cref="DefaultSeed"/>, never time.
    ///   • STREAMING - per-prism domain + kind via <see cref="PrismLay"/>, streamed with
    ///     <see cref="PrismTrailBuilder.LayBudgetedAsync"/>. Behind a game load the arena
    ///     gate raises the slice to 250ms; in ungated contexts (a cell blooming its garden
    ///     into the live Menu_Main lava lamp) the small authored slice keeps the frame tax
    ///     gentle and the world grows in over tens of seconds.
    ///   • Subclasses implement <see cref="BuildEnvironment"/> (Emit everything) and
    ///     <see cref="BuildParameterHash"/> (their own generation-affecting params); the base
    ///     hashes seed/density/clearance and owns the SpawnableBase cache contract.
    /// </summary>
    public abstract class CellEnvironmentSpawnableBase : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] protected Prism prism;

        /// <summary>Laying slice per frame in UNGATED contexts only - behind a game load the
        /// connecting screen holds and PrismTrailBuilder.EffectiveLayBudget raises the slice to
        /// 250ms regardless, so this small value costs gated loads nothing.</summary>
        protected const float LayBudgetMsPerFrame = 8f;

        [Header("Population")]
        [Tooltip("Scales the population-heavy families (each environment applies it to its biggest counts). 1 = the authored weight.")]
        [Range(0.5f, 1.3f)]
        [SerializeField] protected float density = 1f;

        [Header("Spawn Clearance")]
        [Tooltip("No prism is laid within this radius of any clearance point (player spawn pads). 0/empty = no clearance.")]
        [SerializeField] protected float spawnClearRadius = 0f;

        [Tooltip("Clearance points in the structure's local space.")]
        [SerializeField] protected Vector3[] spawnClearPoints = System.Array.Empty<Vector3>();

        /// <summary>Golden angle in radians - the phyllotaxis constant (same value as
        /// PrismGeometry.AddShellPatch) behind canopies, whorls, and placement spirals.</summary>
        protected const float GoldenAngle = 2.39996323f;

        // Generation state (valid during GenerateTrailData; _cachedLays persists for
        // SpawnLeafObjects, mirroring SpawnableGyroid/SpawnableSchwarzPSurface).
        protected List<PrismLay> _cachedLays;
        protected System.Random _r;
        protected int _noiseSeed;

        /// <summary>Fixed fallback seed used when the serialized seed is 0 - generation must
        /// never time-seed (clients must agree on every prism).</summary>
        protected abstract int DefaultSeed { get; }

        /// <summary>Pre-size for the lay list (avoid growth churn on big builds).</summary>
        protected virtual int LayCapacity => 40000;

        /// <summary>Emit the whole environment via <see cref="Emit"/>.</summary>
        protected abstract void BuildEnvironment();

        /// <summary>Hash of every SUBCLASS parameter that affects generation (the base already
        /// covers seed, density, and clearance). Bump a constant here to invalidate caches
        /// after a code-level layout change.</summary>
        protected abstract int BuildParameterHash();

        protected override SpawnTrailData[] GenerateTrailData()
        {
            _noiseSeed = seed != 0 ? seed : DefaultSeed;
            _r = new System.Random(_noiseSeed);
            _cachedLays = new List<PrismLay>(LayCapacity);

            BuildEnvironment();

            var points = new SpawnPoint[_cachedLays.Count];
            for (int i = 0; i < _cachedLays.Count; i++)
                points[i] = _cachedLays[i].Point;

            return new[] { new SpawnTrailData(points, false, domain) };
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (prism == null || _cachedLays == null) return;

            var trail = new Trail();

            // Streamed + batched at play time (tens of thousands of prisms; laying the 25k
            // geodesic shells in one frame measured ~95s). Behind a game load the arena-ready
            // gate holds the connecting screen until every prism is revealed and grown; in
            // ungated contexts the structure blooms in over frames. Edit-mode spawns stay
            // synchronous.
            if (Application.isPlaying)
                PrismTrailBuilder.LayBudgetedAsync(prism, _cachedLays, container.transform, trail,
                    $"{container.name}::BLOCK", LayBudgetMsPerFrame).Forget();
            else
                PrismTrailBuilder.LaySync(prism, _cachedLays, container.transform, trail, $"{container.name}::BLOCK");

            trails.Add(trail);
        }

        protected override int GetParameterHash()
        {
            // Clearance point VALUES feed Emit's rejection test, so they must invalidate the
            // SpawnableBase cache too - length alone would serve a stale arena after a pad edit.
            int clearHash = System.HashCode.Combine(spawnClearRadius, spawnClearPoints?.Length ?? 0, seed);
            if (spawnClearPoints != null)
                for (int i = 0; i < spawnClearPoints.Length; i++)
                    clearHash = System.HashCode.Combine(clearHash, spawnClearPoints[i]);
            return System.HashCode.Combine(density, clearHash, BuildParameterHash());
        }

        // ── Shared helpers (one deterministic vocabulary for all environments) ──

        protected float RangeF(float min, float max) => (float)(_r.NextDouble() * (max - min) + min);

        /// <summary>One uniform jitter factor per prism (min-clamped so no axis falls under the
        /// prism scale animator's 0.5 floor and silently clamps).</summary>
        protected Vector3 Jit(Vector3 s, float amt = 0.2f)
        {
            float k = 1f + RangeF(-amt, amt);
            return new Vector3(Mathf.Max(0.5f, s.x * k), Mathf.Max(0.5f, s.y * k), Mathf.Max(0.5f, s.z * k));
        }

        /// <summary>Order-independent per-index hash in [0,1) - stable decoration values that do
        /// not disturb the shared System.Random stream.</summary>
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

        /// <summary>Seeded 3D value noise remapped to [0,1] (PaintingStrokeToolkit returns ~[-1,1]).</summary>
        protected float N01(float x, float y, float z, int seedOffset) =>
            0.5f * (PaintingStrokeToolkit.ValueNoise(new Vector3(x, y, z), _noiseSeed + seedOffset) + 1f);

        protected Vector3 Curl(Vector3 p, float freq, int seedOffset) =>
            PaintingStrokeToolkit.CurlNoise(p, freq, _noiseSeed + seedOffset);

        /// <summary>Population count scaled by the density knob.</summary>
        protected int Scaled(int n) => Mathf.Max(1, Mathf.RoundToInt(n * density));

        protected void Emit(Vector3 pos, Quaternion rot, Vector3 scale, Domains dom, PrismKind kind = PrismKind.Plain)
        {
            if (spawnClearPoints != null && spawnClearRadius > 0f)
            {
                float rr = spawnClearRadius * spawnClearRadius;
                for (int i = 0; i < spawnClearPoints.Length; i++)
                    if ((pos - spawnClearPoints[i]).sqrMagnitude < rr)
                        return;
            }
            _cachedLays.Add(new PrismLay(new SpawnPoint(pos, rot, scale), dom, kind));
        }
    }
}
