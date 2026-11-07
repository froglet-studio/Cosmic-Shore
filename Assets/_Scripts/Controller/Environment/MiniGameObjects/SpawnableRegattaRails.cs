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
    /// other authored environment. One prefab variant per circuit; each is an entry of Regatta's
    /// course pool (<see cref="RegattaCoursePoolSO"/>), which the host deals from once per match.
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
    /// <para><b>Lifeforms are a course SETTING, off by default.</b> The shipped courses reference the
    /// barren spawn profile (Skein's reason: a race whose obstacles differ per machine is not a race).
    /// A course may instead prepare planting sites beside the spine and ask its cell for a fauna
    /// corridor (<see cref="RegattaLifeformSiteSettings"/>, <c>Tools/Build/regatta_lifeforms.py</c>);
    /// the species and counts are then its cell config's own spawn profile, with networked plants
    /// (FloraNetworkSync) so every peer roots the same plant at the same site. The arena never
    /// spawns a lifeform itself: the Cell owns the ecology.</para>
    ///
    /// <para><b>The seed lives HERE.</b> The controller re-derives the rings from
    /// <see cref="CourseSeed"/> and <see cref="CourseIntensity"/> through the same pure call
    /// (<see cref="RegattaCourse.BuildGates"/>), so the rails and the rings cannot disagree, and
    /// it can do so during the spawn chain off the prefab ASSET (nothing here has to be spawned
    /// to answer). A given prefab is the same circuit every match; the VARIETY comes from the pool
    /// dealing a different prefab (cell config) per match.</para>
    /// </summary>
    public class SpawnableRegattaRails : CellEnvironmentSpawnableBase, ICellFaunaCorridorSource
    {
        [Header("Regatta")]
        [Tooltip("Which of RegattaCourse's circuit rows (RegattaCourse.ForIntensity) this variant lays. The " +
                 "rows were once one per intensity; since 2026-11-07 a prefab is a COURSE-POOL entry " +
                 "(RegattaCoursePoolSO) and intensity no longer picks it. The cell config that points at it " +
                 "authors the same number as EnvironmentIntensity.")]
        [SerializeField, Range(1, 4)] int courseIntensity = 1;

        [Tooltip("0 = RegattaCourse.DefaultSeed. Non-zero pins the circuit, which is how a " +
                 "reported course is reproduced. The per-intensity seed is derived from it.")]
        [SerializeField] int courseSeed;

        [Header("Course features")]
        [Tooltip("Everything about this course beyond its rings (RegattaCourseFeatures): stretches of rail that are " +
                 "shielded, plain, neutral-coloured, missing (a gap) or swung off the line (a detour); gates with a " +
                 "second ring; crystals on the line. EMPTY = the course as it was before 2026-11-06, prism for prism. " +
                 "A course search proposes these (Tools/Compute/evaluators/regatta_features.py); never hand-tune them " +
                 "into a shipped variant without the Regatta owner.")]
        [SerializeField] RegattaCourseFeatures courseFeatures = new();

        [Tooltip("Prism scale, laid with +z down the rail. Skein's (6,6,8): the 6 u cross-section " +
                 "is the attach catch window the Urchin's 0.04 s trigger sample needs at 300 u/s. " +
                 "z is the NOMINAL length: each prism is laid at the length that keeps it flush " +
                 "with its two neighbours (RegattaCourse.FlushRailLengths) - 8 u on a straight, " +
                 "a little less where a bend would cross two boxes' inner corners.")]
        [SerializeField] Vector3 prismScale = new(6f, 6f, 8f);

        [Tooltip("Distance of each lane from the spine. Must clear the tightest mouth by a prism.")]
        [SerializeField, Min(1f)] float laneOffset = 22f;

        [Tooltip("Full turns the braid makes per lap. A whole number closes the braid AND makes " +
                 "the lanes equal in length.")]
        [SerializeField, Min(0f)] float laneTwistTurnsPerLap = 1f;

        [Tooltip("Nominal centre-to-centre prism spacing along a lane; equals the prism's nominal " +
                 "length, and the per-prism fit trims each length to its real neighbours, so the " +
                 "ribbon is continuous and flush rather than overlapping.")]
        [SerializeField, Min(1f)] float prismSpacing = 8f;

        [Header("Lifeforms (off by default - Tools/Build/regatta_lifeforms.py)")]
        [Tooltip("Where this course's lifeforms may live: planting sites per leg beside the spine and the " +
                 "fauna corridor round it. WHICH species and HOW MANY are the cell config's spawn profile. " +
                 "Empty sites and a 0 corridor are the shipped course exactly. Written by " +
                 "Tools/Build/author_regatta_assets.py - never by hand.")]
        [SerializeField] RegattaLifeformSiteSettings lifeforms = new();

        readonly List<RaceGate> _gates = new();
        readonly List<int> _laneLayStart = new();
        readonly List<int> _laneLayCount = new();

        public int CourseIntensity => courseIntensity;

        public RegattaLifeformSiteSettings Lifeforms => lifeforms;

        /// <summary>The seed the planting sites are sown from - the circuit's own, offset so the sites are
        /// not correlated with the ring draws.</summary>
        public int SiteSeed => unchecked(CourseSeed * 31 + courseIntensity);

        /// <summary>Sites prepared on the last build (cell-local). Empty on every shipped course.</summary>
        public int SownSiteCount { get; private set; }

        public override bool PreparesPlantingSites => lifeforms != null && lifeforms.HasSites;

        /// <summary>The seed the circuit is generated from: the authored pin, else the base
        /// spawnable's seed, else the course's default.</summary>
        public int CourseSeed => courseSeed != 0 ? courseSeed : (seed != 0 ? seed : DefaultSeed);

        /// <summary>The rings this arena laid its rails through, cell-local. Empty before the
        /// build; use <see cref="BuildGatesNow"/> for an answer that needs no build.</summary>
        public IReadOnlyList<RaceGate> Gates => _gates;

        public HeadlongCircuitSettings CourseSettings => RegattaCourse.ForIntensity(courseIntensity);

        // One RUN per trail: a lane with no gap is one closed run (as before features existed); a gap splits a lane
        // into open runs, laid in lane order starting after a gap. _laneLayStart / _laneLayCount are per run.
        readonly List<int> _runLane = new();
        readonly List<bool> _runClosed = new();
        readonly List<Trail> _runTrails = new();
        int _laneCount;

        /// <summary>This course's features: <see cref="RegattaCourseFeatures.Override"/> when a benchmark or test set
        /// one, else the prefab's own. Read by the controller (alternate rings, line crystals) off the same asset.</summary>
        public RegattaCourseFeatures Features => RegattaCourseFeatures.Override ?? courseFeatures ?? RegattaCourseFeatures.None;

        /// <summary>Lanes laid this build (3), 0 before it.</summary>
        public int LaneCount => _laneCount;

        /// <summary>The team colour lane <paramref name="lane"/> is laid in (a neutral stretch is still part of it).</summary>
        public static Domains LaneDomain(int lane) => RegattaCourse.LaneDomains[lane % RegattaCourse.LaneDomains.Length];

        /// <summary>
        /// Lane <paramref name="lane"/>'s live prisms in lay order. With no gap this IS the lane's one trail list (the
        /// live list, as every reader used before gaps existed); with gaps it is the runs joined in order, a new list.
        /// Null before the lane is laid.
        /// </summary>
        public List<Prism> LanePrisms(int lane)
        {
            List<Prism> joined = null, single = null;
            int runs = 0;
            for (int r = 0; r < _runTrails.Count && r < _runLane.Count; r++)
            {
                if (_runLane[r] != lane) continue;
                var list = _runTrails[r]?.TrailList;
                if (list == null) continue;
                runs++;
                if (runs == 1) { single = list; continue; }
                if (joined == null) { joined = new List<Prism>(single); }
                joined.AddRange(list);
            }
            return runs <= 1 ? single : joined;
        }

        /// <summary>The lane laid in <paramref name="domain"/>'s colour, or -1.</summary>
        public int LaneOf(Domains domain)
        {
            for (int lane = 0; lane < _laneCount; lane++)
                if (LaneDomain(lane) == domain) return lane;
            return -1;
        }

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

        /// <summary>Never baked (PrismLayoutBake): its lay is split into trails by build state the
        /// bake does not carry, and its fit is closed-form and cheap.</summary>
        protected override bool BakesLayout => false;

        protected override int LayCapacity => 8192;

        protected override void BuildEnvironment()
        {
            _gates.Clear();
            _laneLayStart.Clear();
            _laneLayCount.Clear();
            _runLane.Clear();
            _runClosed.Clear();

            var gates = BuildGatesNow();
            _gates.AddRange(gates);

            var features = Features;
            var layout = RegattaCourse.BuildRails(gates, RailSettings, features);
            _laneCount = layout.LanePositions.Count;
            for (int lane = 0; lane < layout.LanePositions.Count; lane++)
            {
                var pos = layout.LanePositions[lane];
                var rot = layout.LaneRotations[lane];
                var dom = LaneDomain(lane);

                // Flush, not overlapping (Docs/ECOSYSTEM.md 49.10): each prism's length is fitted
                // to its two neighbours at authoring time - the cross-section stays the catch
                // window, only the length on a bend gives way. Same count, same poses.
                var len = RegattaCourse.FlushRailLengths(pos, rot, prismScale);
                var plan = RegattaRailPlan.ForLane(layout.LaneLegParams[lane], gates.Count, features);
                for (int r = 0; r < plan.Runs.Count; r++)
                {
                    var run = plan.Runs[r];
                    int start = _cachedLays.Count;
                    for (int q = 0; q < run.Count; q++)
                    {
                        int k = (run.Start + q) % pos.Length;
                        var prismDomain = plan.Neutral[k] ? RegattaCourseFeatures.NeutralDomain : dom;
                        Emit(pos[k], rot[k], new Vector3(prismScale.x, prismScale.y, len[k]), prismDomain, plan.Kinds[k]);
                    }
                    _laneLayStart.Add(start);
                    _laneLayCount.Add(_cachedLays.Count - start);
                    _runLane.Add(lane);
                    _runClosed.Add(run.Closed);
                }
            }

            // Prepared ground beside the spine, through the base's Sow so the spawn clearance applies to a
            // root exactly as it does to a prism. None on a shipped course (lifeforms authors no sites).
            SownSiteCount = 0;
            if (lifeforms != null && lifeforms.HasSites)
            {
                var sites = new List<FloraPlantingSite>(lifeforms.RequestedSites);
                RegattaLifeformSites.Sow(_gates, lifeforms, laneOffset, SiteSeed, sites);
                int before = _plantingSites?.Count ?? 0;
                foreach (var site in sites) Sow(site.Position, site.Up, site.Kind);
                SownSiteCount = (_plantingSites?.Count ?? 0) - before;
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Regatta] Circuit seed {CourseSeed} intensity {courseIntensity}: {_gates.Count} rings, " +
                $"{layout.LanePositions.Count} rails, {_cachedLays.Count} prisms, spine {layout.SpineLength:F0} u, " +
                $"{SownSiteCount} planting sites.");
        }

        /// <summary>The fauna corridor round the spine, when this course authors one (cell-local, closed).
        /// Pure off the prefab's own seed, so it needs no build.</summary>
        public bool TryGetFaunaCorridor(List<Vector3> pathLocal, out float radius, out bool closed, out float goalStandoff)
        {
            pathLocal?.Clear();
            radius = lifeforms != null ? lifeforms.faunaCorridorRadius : 0f;
            goalStandoff = lifeforms != null ? lifeforms.faunaCorridorStandoff : 0f;
            closed = true;
            if (pathLocal == null || radius <= 0f) return false;
            pathLocal.AddRange(RegattaLifeformSites.SpineSamples(BuildGatesNow(), 16));
            return pathLocal.Count > 2;
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

            // One trail per run: closed for a lane with no gap (today's), open for each run a gap leaves, so a rider
            // runs off its end and flies the gap (a hole inside one trail is bridged at full pace).
            var laneTrails = new Trail[_laneLayStart.Count];
            _runTrails.Clear();
            for (int i = 0; i < laneTrails.Length; i++)
            {
                bool closed = i >= _runClosed.Count || _runClosed[i];
                var t = new Trail(closed) { Dimension = PrismscapeDimension.Trail };
                trails.Add(t);
                laneTrails[i] = t;
                _runTrails.Add(t);
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
            System.HashCode.Combine(System.HashCode.Combine(courseIntensity, courseSeed, prismScale, laneOffset,
                                                            laneTwistTurnsPerLap, prismSpacing,
                                                            lifeforms?.ParameterHash() ?? 0, /* layout revision */ 2),
                                    Features.IsEmpty ? 0 : Features.ParameterHash());
    }
}
