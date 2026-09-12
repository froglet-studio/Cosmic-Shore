using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shared plumbing for the authored CELL ENVIRONMENTS - the large deterministic prism
    /// gardens a <c>CellConfigDataSO.EnvironmentPrefab</c> (or a SegmentSpawner slot) spawns:
    /// Atlantis, the freestyle seven (Yggdra, Daedala, Orrery, Zephyr, Caldera, Geode, Ourobor)
    /// and the GARDEN cell Hesperides (which also SOWS planting sites - see
    /// <see cref="PlantingSites"/>). One definition of the lay/stream/noise contract so nine
    /// generators cannot drift:
    ///
    ///   • DETERMINISM - clients build environments locally with no seed sync, so all
    ///     randomness flows from the serialized seed through one System.Random plus the
    ///     seeded noise in <see cref="PaintingStrokeToolkit"/>; a seed of 0 falls back to the
    ///     subclass's fixed <see cref="DefaultSeed"/>, never time.
    ///   • STREAMING - per-prism domain + kind via <see cref="PrismLay"/>, streamed with
    ///     <see cref="PrismTrailBuilder.LayBudgetedAsync"/>. Behind a game load the arena
    ///     gate raises the slice to 250ms; cell-spawned environments in gate-less scenes
    ///     (Menu_Main freestyle) raise an <see cref="EnvironmentLoadVeil"/> that brackets the
    ///     same gate machinery - building a big world UNDER live gameplay crashed the menu,
    ///     so the tiny ungated slice below is only a last-resort fallback.
    ///   • Subclasses implement <see cref="BuildEnvironment"/> (Emit everything) and
    ///     <see cref="BuildParameterHash"/> (their own generation-affecting params); the base
    ///     hashes seed/density/clearance and owns the SpawnableBase cache contract.
    /// </summary>
    public abstract class CellEnvironmentSpawnableBase : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] protected Prism prism;

        /// <summary>Laying slice per frame when NO gate holds - a last-resort fallback only,
        /// since both the game connecting screens and the freestyle EnvironmentLoadVeil raise
        /// the gate (PrismTrailBuilder.EffectiveLayBudget then boosts the slice to 250ms).</summary>
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

        /// <summary>
        /// The per-prism lay list from the most recent generation - pose <b>and domain and
        /// kind</b>, which <see cref="SpawnableBase.GetTrailData"/>'s <see cref="SpawnTrailData"/>
        /// cannot carry (it holds one domain for a whole trail). Exposed so a PREVIEW consumer -
        /// the freestyle Cell Selector's scale models - can render an environment's real structure
        /// and its real domain composition without spawning a single prism.
        ///
        /// Null until a generation has run: call <see cref="SpawnableBase.GetTrailData"/> first
        /// (it is cached, so this costs the generation math at most once). Read-only - the list
        /// is live generation state, not a copy.
        /// </summary>
        public IReadOnlyList<PrismLay> CachedLays => _cachedLays;

        /// <summary>
        /// Spots this environment has prepared for LIVING flora - see <see cref="FloraPlantingSite"/>.
        /// Empty for every structural environment (the freestyle seven author none); a GARDEN
        /// environment sows them with <see cref="Sow"/> from the same seeded math that lays its
        /// beds, so the planting and the architecture cannot drift apart. <see cref="Cell"/> reads
        /// the list once the build starts and hands the sites to its ordinary flora spawner -
        /// the environment never spawns a lifeform itself.
        ///
        /// Null until a generation has run (call <see cref="SpawnableBase.GetTrailData"/> first).
        /// </summary>
        public IReadOnlyList<FloraPlantingSite> PlantingSites => _plantingSites;

        protected List<FloraPlantingSite> _plantingSites;

        /// <summary>
        /// Drop the generated point data (both the <see cref="SpawnableBase"/> trail cache and
        /// <see cref="CachedLays"/>). For a PREVIEW consumer that generated an environment only to
        /// sample it: a freestyle cell's lay list is tens of thousands of structs, and holding
        /// seven of them so the menu can show seven thumbnails is a bad trade on a mobile target -
        /// re-generating on load is a small fraction of the lay cost. Safe during an in-flight
        /// lay: <c>LayBudgetedAsync</c> holds its own reference to the list it was handed.
        /// </summary>
        public void ReleaseGeneratedData()
        {
            InvalidateCache();
            _cachedLays = null;
            _plantingSites = null;
        }

        protected override SpawnTrailData[] GenerateTrailData()
        {
            _noiseSeed = seed != 0 ? seed : DefaultSeed;
            _r = new System.Random(_noiseSeed);
            _cachedLays = new List<PrismLay>(LayCapacity);
            _plantingSites = new List<FloraPlantingSite>();

            BuildEnvironment();

            var points = new SpawnPoint[_cachedLays.Count];
            for (int i = 0; i < _cachedLays.Count; i++)
                points[i] = _cachedLays[i].Point;

            return new[] { new SpawnTrailData(points, false, domain) };
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (prism == null || _cachedLays == null) return;

            // Preview thinning (PrismLayDecimation) applies HERE, not in SpawnPrismTrail — this
            // family bypasses that path entirely, which is how every authored world used to build
            // at full density in the mode preview. Outside a decimation scope this returns
            // _cachedLays untouched; inside one it hands the builder a strided copy, leaving the
            // cached list whole for the miniature builder and the planting model.
            var lays = PrismLayDecimation.Apply(_cachedLays);

            var trail = new Trail();

            // Streamed + batched at play time (tens of thousands of prisms; laying the 25k
            // geodesic shells in one frame measured ~95s). Behind a game load the arena-ready
            // gate holds the connecting screen until every prism is revealed and grown; in
            // ungated contexts the structure blooms in over frames. Edit-mode spawns stay
            // synchronous.
            if (Application.isPlaying)
                PrismTrailBuilder.LayBudgetedAsync(prism, lays, container.transform, trail,
                    $"{container.name}::BLOCK", LayBudgetMsPerFrame).Forget();
            else
                PrismTrailBuilder.LaySync(prism, lays, container.transform, trail, $"{container.name}::BLOCK");

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

        /// <summary>
        /// Prepare a planting site (see <see cref="PlantingSites"/>). Sites respect the same spawn
        /// clearance as prisms - a pad the player spawns on should not have a tree growing out of
        /// it either.
        /// </summary>
        protected void Sow(Vector3 pos, Vector3 up, FloraSiteKind kind = FloraSiteKind.Bed)
        {
            if (_plantingSites == null) return;
            if (spawnClearPoints != null && spawnClearRadius > 0f)
            {
                float rr = spawnClearRadius * spawnClearRadius;
                for (int i = 0; i < spawnClearPoints.Length; i++)
                    if ((pos - spawnClearPoints[i]).sqrMagnitude < rr)
                        return;
            }
            _plantingSites.Add(new FloraPlantingSite(pos, up, kind));
        }
    }
}
