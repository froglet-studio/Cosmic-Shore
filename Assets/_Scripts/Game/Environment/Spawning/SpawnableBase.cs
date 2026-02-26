using CosmicShore.Game.Ship;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Models.Enums;
using CosmicShore.Game.Environment;
using System.Linq;
namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Unified base class for spatial pattern generation and object spawning.
    ///
    /// Unified composable, cacheable system for spatial pattern generation
    /// and object spawning.
    ///
    /// Key features:
    ///   1. Generates SpawnTrailData[] — position + rotation + scale per object, grouped by trail
    ///   2. Caches results until parameters change (via GetParameterHash)
    ///   3. Supports nesting via children list — tree structure of unlimited depth
    ///   4. Can instantiate any prefab at leaf positions (prisms, crystals, flora, fauna, vessels)
    ///
    /// Subclasses implement ONE of:
    ///   - GeneratePoints()    — returns SpawnPoint[] for single-trail patterns (most common)
    ///   - GenerateTrailData() — returns SpawnTrailData[] for multi-trail patterns
    /// Plus:
    ///   - GetParameterHash()  — returns a hash of all parameters that affect generation
    /// </summary>
    public abstract class SpawnableBase : MonoBehaviour
    {
        [Header("Spawnable Base")]
        [SerializeField] protected int seed;
        [SerializeField] public Domains domain = Domains.Blue;

        [Header("Tree Structure")]
        [Tooltip("Child generators to evaluate at each generated point. " +
                 "When non-empty, this node is an internal node (positions children). " +
                 "When empty, this node is a leaf (instantiates leafPrefab).")]
        [SerializeField] protected List<SpawnableBase> children = new();

        [Header("Leaf Spawning")]
        [Tooltip("Prefab to instantiate at each generated point when this is a leaf node. " +
                 "Can be a Prism (gets trail management), Crystal, Flora, Fauna, Vessel, or any prefab.")]
        [SerializeField] protected GameObject leafPrefab;

        // Cache
        private SpawnTrailData[] _cachedTrails;
        private int _cachedHash;
        private bool _cacheValid;

        // Runtime state
        protected System.Random rng;
        protected List<Trail> trails = new();
        [FormerlySerializedAs("intenstyLevel")]
        public int intensityLevel = 1;

        #region Abstract / Virtual Generation

        /// <summary>
        /// Compute a hash of all parameters that affect generation output.
        /// Cache is invalidated when this hash changes.
        /// Include seed, dimensions, counts — anything that changes the output.
        /// </summary>
        protected abstract int GetParameterHash();

        /// <summary>
        /// Generate spawn points for a single-trail pattern.
        /// Override this for the common case of one trail per spawnable.
        /// The base class wraps the result in a SpawnTrailData with IsLoop=false.
        /// </summary>
        protected virtual SpawnPoint[] GeneratePoints()
        {
            return null;
        }

        /// <summary>
        /// Generate trail data for multi-trail patterns.
        /// Override this instead of GeneratePoints() when the spawnable
        /// produces multiple trails (e.g., ellipsoid, cylinder, linked rings).
        /// Default implementation wraps GeneratePoints() in a single trail.
        /// </summary>
        protected virtual SpawnTrailData[] GenerateTrailData()
        {
            var points = GeneratePoints();
            if (points == null || points.Length == 0)
                return System.Array.Empty<SpawnTrailData>();

            return new[] { new SpawnTrailData(points, false, domain) };
        }

        #endregion

        #region Cached Access

        /// <summary>
        /// Get trail data, using cache if parameters haven't changed.
        /// </summary>
        public SpawnTrailData[] GetTrailData()
        {
            int hash = GetParameterHash();
            if (_cacheValid && _cachedTrails != null && hash == _cachedHash)
                return _cachedTrails;

            rng = seed != 0 ? new System.Random(seed) : new System.Random();
            _cachedTrails = GenerateTrailData();
            _cachedHash = hash;
            _cacheValid = true;
            return _cachedTrails;
        }

        /// <summary>
        /// Convenience: get spawn points from the first trail.
        /// Useful when you know there's only one trail.
        /// </summary>
        public SpawnPoint[] GetSpawnPoints()
        {
            var data = GetTrailData();
            if (data == null || data.Length == 0)
                return System.Array.Empty<SpawnPoint>();
            return data[0].Points;
        }

        /// <summary>
        /// Force regeneration on next access.
        /// </summary>
        public void InvalidateCache()
        {
            _cacheValid = false;
            _cachedTrails = null;
        }

        #endregion

        #region Spawning

        /// <summary>
        /// Spawn the full tree, returning a container GameObject with all instantiated objects.
        /// </summary>
        public virtual GameObject Spawn(int intensity = 1)
        {
            intensityLevel = intensity;
            trails.Clear();

            var container = new GameObject(name);
            var trailData = GetTrailData();

            if (children.Count > 0)
            {
                SpawnChildren(trailData, container, intensity);
            }
            else
            {
                SpawnLeafObjects(trailData, container);
            }

            return container;
        }

        /// <summary>
        /// Internal node: spawn children at each generated point.
        /// Normalizes point scales so any spawnable can serve as a parent —
        /// leaf-mode spawnables produce absolute block scales (e.g., pumpkinWidth * sin(t) ≈ 100)
        /// that would make child containers absurdly large without normalization.
        /// Generators designed for nesting (e.g., ConcentricLayersGenerator) already produce
        /// scales in the 0-1 range and pass through unchanged.
        /// </summary>
        private void SpawnChildren(SpawnTrailData[] trailData, GameObject container, int intensity)
        {
            // Find maximum scale component across all points.
            // Used to normalize scales into a sane range for child containers.
            float maxScaleComponent = 0f;
            foreach (var td in trailData)
                foreach (var point in td.Points)
                {
                    maxScaleComponent = Mathf.Max(maxScaleComponent,
                        Mathf.Max(Mathf.Abs(point.Scale.x),
                            Mathf.Max(Mathf.Abs(point.Scale.y), Mathf.Abs(point.Scale.z))));
                }

            // Only normalize when scales exceed 1 — preserves behavior for generators
            // that already produce normalized scales (ConcentricLayersGenerator etc.)
            float scaleNormalizer = maxScaleComponent > 1f ? maxScaleComponent : 1f;

            foreach (var td in trailData)
            {
                foreach (var point in td.Points)
                {
                    foreach (var child in children)
                    {
                        var childObj = child.Spawn(intensity);
                        childObj.transform.SetParent(container.transform, false);
                        childObj.transform.localPosition = point.Position;
                        childObj.transform.localRotation = point.Rotation;
                        childObj.transform.localScale = point.Scale / scaleNormalizer;
                        trails.AddRange(child.GetTrails());
                    }
                }
            }
        }

        /// <summary>
        /// Leaf node: instantiate objects at each generated point.
        /// Detects Prism prefabs and handles trail management automatically.
        /// Override for custom leaf behavior.
        /// </summary>
        protected virtual void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (leafPrefab == null) return;

            var prismComponent = leafPrefab.GetComponent<Prism>();
            if (prismComponent != null)
            {
                foreach (var td in trailData)
                {
                    SpawnPrismTrail(td.Points, container, td.IsLoop, td.Domain);
                }
            }
            else
            {
                foreach (var td in trailData)
                {
                    foreach (var point in td.Points)
                    {
                        var obj = Instantiate(leafPrefab, container.transform);
                        obj.transform.localPosition = point.Position;
                        obj.transform.localRotation = point.Rotation;
                        obj.transform.localScale = point.Scale;
                    }
                }
            }
        }

        /// <summary>
        /// Instantiate Prism blocks along a trail with full initialization.
        /// Accepts an explicit Prism prefab, or falls back to leafPrefab.
        /// Reusable by multi-trail subclasses that override SpawnLeafObjects.
        /// </summary>
        protected void SpawnPrismTrail(SpawnPoint[] points, GameObject container,
            Prism prismPrefab, bool isLoop = false, Domains? trailDomain = null)
        {
            if (prismPrefab == null) return;

            var trail = new Trail(isLoop);
            var actualDomain = trailDomain ?? domain;

            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var block = Instantiate(prismPrefab, container.transform);
                block.ChangeTeam(actualDomain);
                block.ownerID = $"{container.name}::{i}";
                block.transform.localPosition = point.Position;
                block.transform.localRotation = point.Rotation;
                block.TargetScale = point.Scale;
                block.Trail = trail;
                block.Initialize();
                trail.Add(block);
            }

            trails.Add(trail);
        }

        /// <summary>
        /// Overload using leafPrefab for prism trail spawning.
        /// </summary>
        protected void SpawnPrismTrail(SpawnPoint[] points, GameObject container,
            bool isLoop = false, Domains? trailDomain = null)
        {
            if (leafPrefab == null) return;
            var prism = leafPrefab.GetComponent<Prism>();
            if (prism != null)
                SpawnPrismTrail(points, container, prism, isLoop, trailDomain);
        }

        #endregion

        #region Backward Compatibility

        /// <summary>
        /// Spawn with default intensity.
        /// </summary>
        public virtual GameObject Spawn(Vector3 position, Quaternion rotation, Domains domain)
        {
            transform.position = position;
            transform.rotation = rotation;
            this.domain = domain;
            return Spawn();
        }

        /// <summary>
        /// Spawn at a specific position/rotation with domain and intensity.
        /// </summary>
        public virtual GameObject Spawn(Vector3 position, Quaternion rotation, Domains domain, int intensity = 1)
        {
            transform.position = position;
            transform.rotation = rotation;
            this.domain = domain;
            return Spawn(intensity);
        }

        public void SetSeed(int newSeed)
        {
            seed = newSeed;
            InvalidateCache();
        }

        public virtual List<Trail> GetTrails() => trails;

        #endregion
    }
}
