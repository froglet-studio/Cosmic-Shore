using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

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
    /// Classification runs in ONE Burst pass per tick
    /// (<see cref="PrismSpatialIndex.RunLodClassification"/>) that emits only
    /// near/far TRANSITIONS via the per-slot LodNear flag bit — the managed cost
    /// here is O(changed colliders), bounded further by a per-frame toggle budget,
    /// never O(population). (The previous managed sliced scan paid ~5.5 ms per
    /// slice frame at 25k prisms.)
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

        [Tooltip("Seconds between LOD classification passes. At 0.25s a 100 u/s vessel moves 25m per pass — well inside the radius margin.")]
        [Min(0.05f)] [SerializeField] float tickIntervalSeconds = 0.25f;

        [Tooltip("Collider cull/restore toggles applied per FRAME from a pass's transition lists. " +
                 "Steady-state transitions are a handful; this bounds the burst cases (first " +
                 "reconcile over a big population, a focus warping across the cell) so no single " +
                 "frame pays thousands of collider toggles. Leftovers continue next frame; a new " +
                 "pass waits until the backlog drains.")]
        [Min(64)] [SerializeField] int maxColliderTogglesPerFrame = 512;

        // Focus registry: vessels + in-flight projectiles. Main-thread only, tiny.
        static readonly List<Transform> s_foci = new(16);

        /// <summary>Active colliders after the last pass (telemetry — EcosystemPerfProbe).</summary>
        public static int LastNearCount { get; private set; }

        /// <summary>Live prisms seen in the last pass (telemetry — EcosystemPerfProbe).</summary>
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
        // One whole-population reconciliation is needed whenever LOD (re)gains an
        // opinion: first pass, foci returning after an idle restore-all, or
        // re-enable. Steady-state passes emit only near/far transitions (the
        // LodNear flag bit in the spatial index is the memory).
        bool _reconcileNextSweep = true;
        int _nearCount;

        readonly List<Vector3> _fociSnapshot = new(16);
        readonly List<Prism> _becameNear = new(1024);
        readonly List<Prism> _becameFar = new(4096);
        int _applyNearCursor = -1; // >= 0 ⇒ transition apply in progress
        int _applyFarCursor = -1;
        readonly List<Prism> _liveScratch = new(4096);

        /// <summary>
        /// Called when a prism's collider comes online outside the pass cadence
        /// (spawn-window end, trail restore). The transition-based pass only
        /// touches prisms whose near/far state CHANGES, so without this a prism
        /// born far from every focus would keep its collider until it crossed a
        /// bubble boundary. O(foci) — no population walk.
        /// </summary>
        public static void NotifyPrismActivated(Prism prism)
        {
            var inst = Instance;
            if (inst == null || !inst.lodEnabled || prism == null) return;
            if (s_foci.Count == 0) return;

            float r2 = inst.lodRadiusMeters * inst.lodRadiusMeters;
            Vector3 p = prism.transform.position;
            for (int i = 0; i < s_foci.Count; i++)
            {
                var focus = s_foci[i];
                if (focus && (focus.position - p).sqrMagnitude <= r2)
                    return; // near a focus — collider stays as the lifecycle set it
            }

            prism.SetColliderCulledByLod(true);
            inst._culledAnything = true;
        }

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
            if (_applyNearCursor >= 0 || _applyFarCursor >= 0)
            {
                // Kill switch mid-apply: abort the backlog; the next pass runs the
                // restore-all (disabled) or reconcile (re-enabled) path.
                if (!lodEnabled)
                {
                    _applyNearCursor = _applyFarCursor = -1;
                    _becameNear.Clear();
                    _becameFar.Clear();
                    _reconcileNextSweep = true;
                }
                else
                {
                    ApplyTransitionSlice();
                    return;
                }
            }
            if (Time.time < _nextTickAt) return;
            _nextTickAt = Time.time + Mathf.Max(0.05f, tickIntervalSeconds);
            RunSweep();
        }

        void RunSweep()
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
                _reconcileNextSweep = true; // re-reconcile when foci return
                _nearCount = 0;
                LastNearCount = LastLiveCount = 0;
                return;
            }

            _fociSnapshot.Clear();
            for (int f = 0; f < s_foci.Count; f++)
                _fociSnapshot.Add(s_foci[f].position);

            bool reconcile = _reconcileNextSweep;
            _reconcileNextSweep = false;

            // One Burst pass over the whole population, transitions out.
            index.RunLodClassification(_fociSnapshot, lodRadiusMeters, reconcile, _becameNear, _becameFar);

            // Running near-count from transition deltas (exact on reconcile).
            if (reconcile) _nearCount = _becameNear.Count;
            else _nearCount += _becameNear.Count - _becameFar.Count;

            _applyNearCursor = 0;
            _applyFarCursor = 0;
            ApplyTransitionSlice();

            LastNearCount = _nearCount;
            LastLiveCount = index.LiveCount;
        }

        void ApplyTransitionSlice()
        {
            int budget = maxColliderTogglesPerFrame;

            while (_applyNearCursor >= 0)
            {
                if (_applyNearCursor >= _becameNear.Count)
                {
                    _applyNearCursor = -1;
                    _becameNear.Clear();
                    break;
                }
                if (budget-- <= 0) return;
                var prism = _becameNear[_applyNearCursor++];
                if (prism) prism.SetColliderCulledByLod(false);
            }

            while (_applyFarCursor >= 0)
            {
                if (_applyFarCursor >= _becameFar.Count)
                {
                    _applyFarCursor = -1;
                    _becameFar.Clear();
                    break;
                }
                if (budget-- <= 0) return;
                var prism = _becameFar[_applyFarCursor++];
                if (prism)
                {
                    prism.SetColliderCulledByLod(true);
                    _culledAnything = true;
                }
            }
        }

        void OnDisable()
        {
            // Component toggled off (or teardown): hand collider ownership back.
            _applyNearCursor = _applyFarCursor = -1;
            _becameNear.Clear();
            _becameFar.Clear();
            _reconcileNextSweep = true;
            _nearCount = 0;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || !_culledAnything) return;
            int n = index.CopyLivePrisms(_liveScratch);
            for (int i = 0; i < n; i++)
                _liveScratch[i].SetColliderCulledByLod(false);
            _culledAnything = false;
        }
    }
}
