using System.Collections.Generic;
using CosmicShore.Utility;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Proximity collider-LOD over the prism population — the collider half of the
    /// ecosystem performance contract (Docs/ECOSYSTEM_MASTERPLAN.md §4). Prism
    /// BoxColliders only matter near the things that physically TOUCH prisms:
    /// vessels (hull, skimmer, crystal-shield triggers) and projectiles in flight.
    /// Everything else stopped needing them when the senses moved onto
    /// <see cref="PrismSpatialIndex"/> (fauna scans, AOE damage, growth occupancy —
    /// see Docs/SPATIAL_INDEX.md), so colliders far from every focus are culled and
    /// restored as foci move. This is what lets cells hold thousands of prisms
    /// (large flyable flora lattices) while the active-collider count stays bounded
    /// by the LOD radius instead of the population.
    ///
    /// Focus points self-register: <see cref="VesselStatus"/> (every vessel, human,
    /// AI, and menu autopilot alike) and <see cref="Projectile"/> (per flight, so
    /// shots fired at distant structures wake the colliders they are about to hit)
    /// call <see cref="RegisterFocus"/>/<see cref="UnregisterFocus"/> from their
    /// enable/disable lifecycle.
    ///
    /// Safety properties:
    ///   - With NO focus registered (tool scenes, teardown) the manager restores
    ///     anything it culled and otherwise leaves collider state to the Prism
    ///     lifecycle — it never blanket-disables.
    ///   - Mound-layer blocks (Boid.NewBlock) never register with the index, so
    ///     their colliders — the only way mound mate-finding sees them — are never
    ///     touched.
    ///   - Cull/restore goes through <see cref="Prism.SetColliderCulledByLod"/>,
    ///     which snapshots and restores the pre-cull collider state so destruction,
    ///     the spawn window, and shield collider-swaps are never fought.
    ///
    /// Lives on the same auto-created GameObject as <see cref="PrismSpatialIndex"/>;
    /// place one in a scene to override the serialized knobs.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismColliderLodManager : Singleton<PrismColliderLodManager>
    {
        [Header("LOD")]
        [Tooltip("Master switch. OFF restores every culled collider and goes idle — the in-editor kill switch if any collider consumer was missed.")]
        [SerializeField] bool lodEnabled = true;

        [Tooltip("Prism colliders stay enabled within this distance of any focus (vessel / projectile). " +
                 "Must comfortably exceed focus speed × tick so fast vessels never outrun their collider bubble.")]
        [Min(50f)] [SerializeField] float lodRadiusMeters = 200f;

        [Tooltip("Seconds between LOD sweeps. At 0.25s a 100 u/s vessel moves 25m per sweep — well inside the radius margin.")]
        [Min(0.05f)] [SerializeField] float tickIntervalSeconds = 0.25f;

        // Focus registry: vessels + in-flight projectiles. Main-thread only, tiny.
        static readonly List<Transform> s_foci = new(16);

        /// <summary>Active colliders after the last sweep (telemetry — EcosystemPerfProbe).</summary>
        public static int LastNearCount { get; private set; }

        /// <summary>Live prisms seen in the last sweep (telemetry — EcosystemPerfProbe).</summary>
        public static int LastLiveCount { get; private set; }

        public static void RegisterFocus(Transform focus)
        {
            if (focus && !s_foci.Contains(focus)) s_foci.Add(focus);
        }

        public static void UnregisterFocus(Transform focus)
        {
            s_foci.Remove(focus);
        }

        float _nextTickAt;
        bool _culledAnything;
        readonly List<Prism> _liveScratch = new(4096);
        readonly List<Prism> _queryScratch = new(1024);
        readonly HashSet<Prism> _nearSet = new();

        public static PrismColliderLodManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            // Ride the spatial index's GameObject — one bootstrap path, present in
            // exactly the scenes that have prisms.
            var host = PrismSpatialIndex.EnsureInstance();
            if (host == null) return null;
            return host.gameObject.AddComponent<PrismColliderLodManager>();
        }

        void Update()
        {
            if (Time.time < _nextTickAt) return;
            _nextTickAt = Time.time + Mathf.Max(0.05f, tickIntervalSeconds);
            Sweep();
        }

        void Sweep()
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;

            // Prune destroyed foci (pool teardown without OnDisable, scene unload).
            for (int i = s_foci.Count - 1; i >= 0; i--)
                if (!s_foci[i]) s_foci.RemoveAt(i);

            // Disabled or nothing to focus on: restore whatever we culled, then idle.
            // Never blanket-cull a focus-less scene — the lifecycle owns collider
            // state when LOD has no opinion.
            if (!lodEnabled || s_foci.Count == 0)
            {
                if (_culledAnything)
                {
                    int n = index.CopyLivePrisms(_liveScratch);
                    for (int i = 0; i < n; i++)
                        _liveScratch[i].SetColliderCulledByLod(false);
                    _culledAnything = false;
                }
                LastNearCount = LastLiveCount = 0;
                return;
            }

            // Union of per-focus neighborhoods → the prisms that keep colliders.
            _nearSet.Clear();
            for (int f = 0; f < s_foci.Count; f++)
            {
                int hits = index.QuerySphere(s_foci[f].position, lodRadiusMeters, _queryScratch);
                for (int i = 0; i < hits; i++)
                    _nearSet.Add(_queryScratch[i]);
            }

            int live = index.CopyLivePrisms(_liveScratch);
            for (int i = 0; i < live; i++)
            {
                var prism = _liveScratch[i];
                prism.SetColliderCulledByLod(!_nearSet.Contains(prism));
            }

            _culledAnything = true;
            LastNearCount = _nearSet.Count;
            LastLiveCount = live;
        }

        void OnDisable()
        {
            // Component toggled off (or teardown): hand collider ownership back.
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || !_culledAnything) return;
            int n = index.CopyLivePrisms(_liveScratch);
            for (int i = 0; i < n; i++)
                _liveScratch[i].SetColliderCulledByLod(false);
            _culledAnything = false;
        }
    }
}
