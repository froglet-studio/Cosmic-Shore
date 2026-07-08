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

        [Tooltip("Upper bound on packed spatial-index entries examined per FRAME while a sweep is in " +
                 "progress. A sweep that can't finish in one slice continues on the following frames, so " +
                 "the per-frame cost is bounded by this budget instead of by the prism population — the " +
                 "whole population is still reclassified once per tick. Foci drift at most " +
                 "speed × sweep-duration between the first and last slice, well inside the radius margin.")]
        [Min(1000)] [SerializeField] int maxPrismChecksPerFrameSlice = 8000;

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
        // One whole-population reconciliation is needed whenever LOD (re)gains an
        // opinion: first sweep, foci returning after an idle restore-all, or
        // re-enable. Steady-state sweeps toggle only near/far TRANSITIONS,
        // detected with per-prism sweep stamps (Prism.LodNearSweepStamp) instead
        // of HashSet bookkeeping, and every sweep — reconcile included — runs as
        // budgeted slices across frames so no single frame pays the population.
        bool _reconcileNextSweep = true;

        // ---- In-progress sweep state (a sweep spans one or more frames) ----
        int _sweepId;
        int _sliceCursor = -1;                    // >= 0 ⇒ a sweep is in progress
        bool _sweepReconcile;
        readonly List<Vector3> _fociSnapshot = new(16);
        readonly List<Prism> _sliceNear = new(1024);
        readonly List<Prism> _sliceFar = new(4096);
        List<Prism> _nearList = new(4096);
        List<Prism> _prevNearList = new(4096);
        readonly List<Prism> _liveScratch = new(4096);

        /// <summary>
        /// Called when a prism's collider comes online outside the sweep cadence
        /// (spawn-window end, trail restore). The transition-based sweep only
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
            if (_sliceCursor >= 0)
            {
                ContinueSweep();
                return;
            }
            if (Time.time < _nextTickAt) return;
            _nextTickAt = Time.time + Mathf.Max(0.05f, tickIntervalSeconds);
            BeginSweep();
        }

        void BeginSweep()
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
                _prevNearList.Clear();
                _reconcileNextSweep = true; // re-reconcile when foci return
                LastNearCount = LastLiveCount = 0;
                return;
            }

            // Snapshot the foci so every slice of this sweep classifies against the
            // same centres. Foci drift between the first and last slice is bounded
            // by the sweep duration — the same tolerance the tick interval already
            // budgets for in the LOD radius margin.
            _fociSnapshot.Clear();
            for (int f = 0; f < s_foci.Count; f++)
                _fociSnapshot.Add(s_foci[f].position);

            _sweepId++;
            _sweepReconcile = _reconcileNextSweep;
            _reconcileNextSweep = false;
            _nearList.Clear();
            _sliceCursor = 0;
            ContinueSweep();
        }

        void ContinueSweep()
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || !lodEnabled)
            {
                // Kill switch / teardown mid-sweep: abort; the next BeginSweep runs
                // the restore-all (disabled) or reconcile (re-enabled) path.
                _sliceCursor = -1;
                _reconcileNextSweep = true;
                return;
            }

            // One budgeted window of the packed array against all foci. Near prisms
            // get stamped + restored; far prisms are only touched on reconcile
            // sweeps (steady state culls far-transitions from the previous near
            // list instead — see below). Newly spawned far prisms are handled by
            // NotifyPrismActivated.
            int hits = index.QueryUnionOfSpheresSlice(
                _fociSnapshot, lodRadiusMeters, _sliceCursor, maxPrismChecksPerFrameSlice,
                _sliceNear, _sweepReconcile ? _sliceFar : null, out int nextIndex);

            for (int i = 0; i < hits; i++)
            {
                var prism = _sliceNear[i];
                prism.LodNearSweepStamp = _sweepId;
                prism.SetColliderCulledByLod(false); // idempotent — no-op when already restored
                _nearList.Add(prism);
            }

            if (_sweepReconcile)
            {
                for (int i = 0; i < _sliceFar.Count; i++)
                    _sliceFar[i].SetColliderCulledByLod(true);
            }

            _sliceCursor = nextIndex;
            if (_sliceCursor >= 0) return; // more slices on the following frames

            // Sweep complete. Prisms near LAST sweep that this sweep never stamped
            // left every bubble — cull them. (Reconcile sweeps classified the whole
            // population explicitly, so the previous list adds nothing.)
            if (!_sweepReconcile)
            {
                for (int i = 0; i < _prevNearList.Count; i++)
                {
                    var prism = _prevNearList[i];
                    if (prism && prism.LodNearSweepStamp != _sweepId)
                        prism.SetColliderCulledByLod(true);
                }
            }

            // Double-buffer the near lists — no per-sweep copy.
            (_prevNearList, _nearList) = (_nearList, _prevNearList);

            _culledAnything = true;
            LastNearCount = _prevNearList.Count;
            LastLiveCount = index.LiveCount;
        }

        void OnDisable()
        {
            // Component toggled off (or teardown): hand collider ownership back.
            _prevNearList.Clear();
            _sliceCursor = -1;
            _reconcileNextSweep = true;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || !_culledAnything) return;
            int n = index.CopyLivePrisms(_liveScratch);
            for (int i = 0; i < n; i++)
                _liveScratch[i].SetColliderCulledByLod(false);
            _culledAnything = false;
        }
    }
}
