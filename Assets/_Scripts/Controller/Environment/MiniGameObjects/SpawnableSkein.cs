using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Skein" - the arena of <see cref="GameModes.Skein"/>: a trefoil-knot cable of open,
    /// aimed prism rails an Urchin grinds and launches off.
    ///
    /// <para>It is the second environment in the game built to be RIDDEN rather than flown through
    /// or shot at, and it divides labour with Hijack's Switchyard deliberately: those burrs are
    /// <see cref="PrismscapeDimension.Volume"/> and reward the marble ROLL; every segment here is
    /// <see cref="PrismscapeDimension.Trail"/> and rewards the GRIND. Adding a solid here would
    /// give the pilot somewhere to stop, and a race with a pond in it stops being a race.</para>
    ///
    /// <para><b>The geometry lives in <see cref="SkeinCourse"/>, not here</b>, because
    /// <c>Tools/Build/skein_budget.py</c> proves that generator offline and this class must lay
    /// exactly what was proved. Everything this file adds is the LAY: one open Trail per rail,
    /// built up front, laid sequentially inside one arena-build bracket.</para>
    ///
    /// <para><b>ORDER IS LOAD-BEARING.</b> Each rail's prisms are added to its Trail in index order
    /// ALONG the race direction. <c>TrailFollower.Attach</c> seeds its direction from
    /// <c>dot(Course, HeadingAt)</c>, so a rail laid against the flow makes "Backward" unrelated to
    /// "the wrong way" and the whole arrival-angle analysis undefined - and a pilot who attaches
    /// backward is carried back up the course at 150 u/s, costing 164-184 u to recover.</para>
    ///
    /// <para><b>COLLIDER BUDGET: every prism is <see cref="PrismKind.Plain"/>.</b> Zero always-on
    /// mesh colliders are authored, so the active count is bounded by PrismColliderLodManager's
    /// radius rather than by the 5.4k-10.9k population. A MASS-5 pilot brings rail prisms up
    /// shielded by riding them, which costs no collider either (a shield swaps the mesh and the
    /// mass, never the collider) - but its GEOMETRY reaches 1.5 x leafSize = 9 u laterally, which
    /// is why skein_budget.prove_shield_clearance asserts that armour can never fuse two lanes.</para>
    ///
    /// <para>DETERMINISM: closed form plus a specified xorshift32 on the authored seed, so every
    /// peer lays a byte-identical cable with nothing to synchronise.</para>
    /// </summary>
    public class SpawnableSkein : CellEnvironmentSpawnableBase
    {
        [Header("Skein")]
        [Tooltip("Rails in the cable. THE intensity dial: 5 / 6 / 7 / 9, one prefab variant each. " +
                 "Both ends are derived - at two outer strands a ring on the outer shell is a coin " +
                 "flip, and above 9 the tightest same-shell separation stops clearing the ring " +
                 "mouth, which is the exclusivity the whole mode rests on.")]
        [SerializeField, Range(5, 9)] int strandCount = 5;

        [Tooltip("Prism scale. (6,6,8) rather than the Track Projector's (3,3,6) because attach is " +
                 "a PhysX trigger sampled once per 0.04 s fixed step while MoveShip teleports in " +
                 "Update: the sample step is 6.00 u at the 150 u/s a launch carries, against a " +
                 "3.46-3.53 u catch window for a 3-wide rail - about 41% of perpendicular " +
                 "re-attaches MISS, frame-rate dependently, with nothing logged. Re-run " +
                 "Tools/Build/skein_budget.py after any change.")]
        [SerializeField] Vector3 prismScale = new(6f, 6f, 8f);

        [Tooltip("0 = use DefaultSeed. Non-zero pins the cable, which is how a reported arena is " +
                 "reproduced. NOTE: this is authored per prefab, so the cable is the same every " +
                 "match at a given intensity - see SKEIN.md for the per-match variation follow-up.")]
        [SerializeField] int cableSeed;

        readonly List<SkeinRail> _rails = new();
        readonly List<Vector3[]> _positions = new();
        readonly List<Quaternion[]> _rotations = new();
        readonly List<int> _railLayStart = new();

        protected override int DefaultSeed => 20260909;
        protected override int LayCapacity => 14000;

        /// <summary>The rings this cable's course wants, published so the controller lays exactly
        /// the course the arena was built around rather than regenerating a second one.</summary>
        public IReadOnlyList<SkeinGate> Gates => _gates;
        readonly List<SkeinGate> _gates = new();

        /// <summary>The seed this cable was actually built from, and the settings it used.
        /// Published so SkeinController lays the rings this ARENA was built around rather than
        /// regenerating a second course from its own copy of the numbers - two derivations of one
        /// course is exactly the drift this avoids.</summary>
        public int CableSeed => cableSeed != 0 ? cableSeed : (seed != 0 ? seed : DefaultSeed);
        public SkeinCourseSettings CourseSettings => BuildCourseSettings();

        SkeinCourseSettings BuildCourseSettings()
        {
            var s = SkeinCourseSettings.ForIntensity(IntensityForStrandCount(strandCount));
            s.StrandCount = strandCount;
            return s;
        }

        static int IntensityForStrandCount(int n) => n <= 5 ? 1 : n == 6 ? 2 : n == 7 ? 3 : 4;

        protected override void BuildEnvironment()
        {
            _rails.Clear(); _positions.Clear(); _rotations.Clear();
            _railLayStart.Clear(); _gates.Clear();

            int seed = cableSeed != 0 ? cableSeed : (seed != 0 ? seed : DefaultSeed);
            var settings = BuildCourseSettings();

            // A walk that cannot lay the full ring course is a DESIGNED path, not an error -
            // measured 231/240 seeds lay one first try and every failure recovered inside 4
            // re-rolls. Shipping a short course would be a target naming a ring that does not
            // exist, i.e. a match that cannot end.
            SkeinCourse.SkeinBuild build = null;
            for (int attempt = 0; attempt < 6 && build == null; attempt++)
                build = SkeinCourse.BuildAll(unchecked(seed + attempt * 7919), settings);

            if (build == null)
            {
                CSDebug.LogError($"[Skein] Could not lay a {settings.GateCount}-ring cable at " +
                                 $"N={strandCount} after 6 seeds. The arena is EMPTY; check " +
                                 "Tools/Build/skein_budget.py against these settings.");
                return;
            }

            for (int i = 0; i < build.Rails.Count; i++)
            {
                var rail = build.Rails[i];
                var pos = build.Positions[i];
                var rot = build.Rotations[i];

                _railLayStart.Add(_cachedLays.Count);
                for (int k = 0; k < pos.Length; k++)
                    Emit(pos[k], rot[k], prismScale, rail.Domain);

                _rails.Add(rail);
                _positions.Add(pos);
                _rotations.Add(rot);
            }
            _gates.AddRange(build.Gates);

            CSDebug.Log($"[Skein] Cable seed {seed}: {_rails.Count} rails, {_cachedLays.Count} prisms, " +
                        $"{build.Gates.Count} rings, N={strandCount}.");
        }

        /// <summary>
        /// One OPEN Trail per rail, and one per rail rather than one shared: a shared Trail has ONE
        /// pair of ends, so every rail but one could never launch - Hijack's finding, and here the
        /// launch is the whole mode rather than one of its verbs.
        /// </summary>
        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (prism == null || _cachedLays == null || _rails.Count == 0) return;

            var railTrails = new Trail[_rails.Count];
            for (int i = 0; i < _rails.Count; i++)
            {
                // isLoop FALSE explicitly: an open ribbon is what has ends to launch off.
                var t = new Trail(false) { Dimension = PrismscapeDimension.Trail };
                trails.Add(t);
                railTrails[i] = t;
            }

            if (Application.isPlaying) LayAsync(container, railTrails).Forget();
            else LaySync(container, railTrails);
        }

        async UniTaskVoid LayAsync(GameObject container, Trail[] railTrails)
        {
            // Announced BEFORE the first await: the arena-ready gate must already be closed when
            // the first frame of the build ticks it. Sequential rather than concurrent - the lay
            // budget is a shared per-frame counter, so N concurrent lays still place one frame's
            // worth of prisms but each first requests its own async clone batch, i.e. the whole
            // arena cloned in one frame, which is the exact spike the budget exists to prevent.
            PrismTrailBuilder.BeginArenaBuild();
            try
            {
                for (int i = 0; i < _rails.Count; i++)
                {
                    if (!container) return;
                    await PrismTrailBuilder.LayBudgetedAsync(
                        prism, RailLays(i), container.transform, railTrails[i],
                        $"{container.name}::RAIL{i:000}", LayBudgetMsPerFrame);
                }
            }
            finally
            {
                PrismTrailBuilder.EndArenaBuild();
            }
        }

        void LaySync(GameObject container, Trail[] railTrails)
        {
            for (int i = 0; i < _rails.Count; i++)
                PrismTrailBuilder.LaySync(prism, RailLays(i), container.transform,
                                          railTrails[i], $"{container.name}::RAIL{i:000}");
        }

        /// <summary>One rail's lays, decimated INSIDE the rail. The base applies preview thinning
        /// to the whole cached list at once; doing that here would stride across rail boundaries
        /// and hand a rail somebody else's prisms.</summary>
        List<PrismLay> RailLays(int i)
        {
            int start = _railLayStart[i];
            int count = _positions[i].Length;
            var slice = new List<PrismLay>(count);
            for (int k = 0; k < count; k++) slice.Add(_cachedLays[start + k]);
            return PrismLayDecimation.Apply(slice);
        }

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(strandCount, prismScale, cableSeed, /* layout revision */ 1);
    }
}
