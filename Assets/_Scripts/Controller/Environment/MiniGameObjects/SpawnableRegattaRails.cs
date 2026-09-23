using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Regatta's arena: three super-shielded rails - one per playable domain - braided along
    /// a closed circuit of eight rings, laid as ordinary conserved mass by the Cell like every
    /// other authored environment. One prefab variant per intensity.
    ///
    /// <para><b>The rails ARE the racing line, and they are what makes a mixed grid a race.</b>
    /// An Urchin grinds the rail in its own colour at 300 u/s against its 65 u/s cruise, a
    /// Squirrel skims any rail for the boost energy that is its only speed, and both thread
    /// every ring by staying on the rail - the spine crosses each ring plane at the centre along
    /// its axis. Super-shielded, so no cone, gun, plate or spike removes a prism of it and a
    /// rider's speed resource is never shot out from under them: the only thing that can open
    /// a hole is the Rhino's energised sword, and a rider bridges a hole at full pace
    /// (URCHIN_TRAIL_RIDER.md). A shield swaps the mesh and the mass, never the collider, so the
    /// rails cost zero always-on colliders (BREAKWATER.md, verified).</para>
    ///
    /// <para><b>Domain-painted, deliberately.</b> A rail is FRIENDLY terrain only to its own
    /// colour (a rider on a rival's rail crawls at 20 u/s), so a domain race needs a rail per
    /// domain. Painting them all neutral would make every rail hostile to every rider. The twist
    /// (one turn per lap) is what keeps the three lanes the same length.</para>
    ///
    /// <para><b>No food web, by design.</b> Shielded mass is never food anyway, but the barren
    /// spawn profile is referenced for Skein's second reason: fauna are per-peer, and a race
    /// whose obstacles differ per machine is not a race.</para>
    ///
    /// <para><b>The seed lives HERE.</b> The controller re-derives the rings from
    /// <see cref="CourseSeed"/> and <see cref="CourseIntensity"/> through the same pure call
    /// (<see cref="RegattaCourse.BuildGates"/>), so the rails and the rings cannot disagree, and
    /// it can do so during the spawn chain off the prefab ASSET (nothing here has to be spawned
    /// to answer). The cost, as in Skein: a given intensity is the same circuit every match.</para>
    /// </summary>
    public class SpawnableRegattaRails : CellEnvironmentSpawnableBase
    {
        [Header("Regatta")]
        [Tooltip("Which rung of the ladder this variant lays. One prefab per intensity; the cell " +
                 "config that points at it authors the same number as EnvironmentIntensity.")]
        [SerializeField, Range(1, 4)] int courseIntensity = 1;

        [Tooltip("0 = RegattaCourse.DefaultSeed. Non-zero pins the circuit, which is how a " +
                 "reported course is reproduced. The per-intensity seed is derived from it.")]
        [SerializeField] int courseSeed;

        [Tooltip("Prism scale, laid with +z down the rail. Skein's (6,6,8): the 6 u cross-section " +
                 "is the attach catch window the Urchin's 0.04 s trigger sample needs at 300 u/s.")]
        [SerializeField] Vector3 prismScale = new(6f, 6f, 8f);

        [Tooltip("Distance of each lane from the spine. Must clear the tightest mouth by a prism.")]
        [SerializeField, Min(1f)] float laneOffset = 22f;

        [Tooltip("Full turns the braid makes per lap. A whole number closes the braid AND makes " +
                 "the lanes equal in length.")]
        [SerializeField, Min(0f)] float laneTwistTurnsPerLap = 1f;

        [Tooltip("Nominal centre-to-centre prism spacing along a lane; equals the prism's length " +
                 "for a continuous ribbon.")]
        [SerializeField, Min(1f)] float prismSpacing = 8f;

        readonly List<RaceGate> _gates = new();
        readonly List<int> _laneLayStart = new();
        readonly List<int> _laneLayCount = new();

        public int CourseIntensity => courseIntensity;

        /// <summary>The seed the circuit is generated from: the authored pin, else the base
        /// spawnable's seed, else the course's default.</summary>
        public int CourseSeed => courseSeed != 0 ? courseSeed : (seed != 0 ? seed : DefaultSeed);

        /// <summary>The rings this arena laid its rails through, cell-local. Empty before the
        /// build; use <see cref="BuildGatesNow"/> for an answer that needs no build.</summary>
        public IReadOnlyList<RaceGate> Gates => _gates;

        public HeadlongCircuitSettings CourseSettings => RegattaCourse.ForIntensity(courseIntensity);

        public RegattaRailSettings RailSettings => new()
        {
            LaneOffset = laneOffset,
            TwistTurnsPerLap = laneTwistTurnsPerLap,
            PrismSpacing = prismSpacing,
            SamplesPerLeg = RegattaCourse.DefaultRails.SamplesPerLeg,
        };

        /// <summary>The rings, generated from this prefab's own seed and intensity - a pure call
        /// the controller and the spawn line can make off the asset during the spawn chain.</summary>
        public List<RaceGate> BuildGatesNow() => RegattaCourse.BuildGates(CourseSeed, courseIntensity);

        protected override int DefaultSeed => RegattaCourse.DefaultSeed;

        protected override int LayCapacity => 8192;

        protected override void BuildEnvironment()
        {
            _gates.Clear();
            _laneLayStart.Clear();
            _laneLayCount.Clear();

            var gates = BuildGatesNow();
            _gates.AddRange(gates);

            var layout = RegattaCourse.BuildRails(gates, RailSettings);
            for (int lane = 0; lane < layout.LanePositions.Count; lane++)
            {
                var pos = layout.LanePositions[lane];
                var rot = layout.LaneRotations[lane];
                var dom = RegattaCourse.LaneDomains[lane % RegattaCourse.LaneDomains.Length];

                int start = _cachedLays.Count;
                for (int k = 0; k < pos.Length; k++)
                    Emit(pos[k], rot[k], prismScale, dom, PrismKind.SuperShielded);
                _laneLayStart.Add(start);
                _laneLayCount.Add(_cachedLays.Count - start);
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Regatta] Circuit seed {CourseSeed} intensity {courseIntensity}: {_gates.Count} rings, " +
                $"{layout.LanePositions.Count} rails, {_cachedLays.Count} prisms, spine {layout.SpineLength:F0} u.");
        }

        /// <summary>
        /// One CLOSED Trail per lane. Closed because a loop has no ends to launch off - an Urchin
        /// that stays on its rail rides the whole race - and one per lane because a shared trail
        /// would make three parallel ribbons one index space, which is not a thing a rider can
        /// follow.
        /// </summary>
        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (prism == null || _cachedLays == null || _laneLayStart.Count == 0) return;

            var laneTrails = new Trail[_laneLayStart.Count];
            for (int i = 0; i < laneTrails.Length; i++)
            {
                var t = new Trail(true) { Dimension = PrismscapeDimension.Trail };
                trails.Add(t);
                laneTrails[i] = t;
            }

            if (Application.isPlaying) LayAsync(container, laneTrails).Forget();
            else LaySync(container, laneTrails);
        }

        async UniTaskVoid LayAsync(GameObject container, Trail[] laneTrails)
        {
            // Announced BEFORE the first await so the arena-ready gate is already closed when the
            // first frame of the build ticks it; sequential so the per-frame lay budget is one
            // batch rather than three (SpawnableSkein records why).
            PrismTrailBuilder.BeginArenaBuild();
            try
            {
                for (int i = 0; i < laneTrails.Length; i++)
                {
                    if (!container) return;
                    await PrismTrailBuilder.LayBudgetedAsync(
                        prism, LaneLays(i), container.transform, laneTrails[i],
                        $"{container.name}::LANE{i}", LayBudgetMsPerFrame);
                }
            }
            finally
            {
                PrismTrailBuilder.EndArenaBuild();
            }
        }

        void LaySync(GameObject container, Trail[] laneTrails)
        {
            for (int i = 0; i < laneTrails.Length; i++)
                PrismTrailBuilder.LaySync(prism, LaneLays(i), container.transform,
                                          laneTrails[i], $"{container.name}::LANE{i}");
        }

        /// <summary>One lane's lays, decimated INSIDE the lane so a preview stride never hands a
        /// lane its neighbour's prisms.</summary>
        List<PrismLay> LaneLays(int i)
        {
            int start = _laneLayStart[i];
            int count = _laneLayCount[i];
            var slice = new List<PrismLay>(count);
            for (int k = 0; k < count; k++) slice.Add(_cachedLays[start + k]);
            return PrismLayDecimation.Apply(slice);
        }

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(courseIntensity, courseSeed, prismScale, laneOffset,
                                    laneTwistTurnsPerLap, prismSpacing, /* layout revision */ 1);
    }
}
