using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// <b>Breakwater</b> — the Sparrow-only station race. A polar START GATE plus a closed circuit
    /// of danger-woven dishes, flown in order, twice; the first DOMAIN whose LEAD RUNNER is home
    /// wins. Arriving at a station you choose how to open it: fire a skyburst and vaporise a door,
    /// flip to turret stance and saw the weave, or thread the eye and take nothing but nerve.
    ///
    /// <para><b>It is a <see cref="GateRaceController"/>, and almost all of it is.</b> The ordered
    /// course, the geometry broadcast, the rings, the crossing detection, the
    /// owner-detects/server-records round trip, the AI steering, the lead-runner fold and the final
    /// scores all live in the platform, shared with Switchback and Headlong. What is left here is
    /// the three things that are actually this mode: which course to generate, that the course has
    /// a lead-in gate, and the STATIONS the rings are the mouths of.</para>
    ///
    /// <para><b>The lead-in gate is the one thing Breakwater asked the platform for.</b>
    /// <see cref="LeadInGates"/> is 1: crossing 0 is the start gate and every crossing after it
    /// wraps over the circuit alone, so the gate is threaded once and never re-offered. The
    /// alternative — folding it into the circuit — is the "sent me backward through the rings I
    /// came" defect this mode's own play-test produced, and the reason the gate sits off the
    /// circuit at all is a structural fairness result recorded in BREAKWATER.md.</para>
    ///
    /// <para><b>This class was 1,255 lines and hand-rolled every one of those platform jobs.</b>
    /// It was written before <c>GateRaceController</c> existed and argued, in the doc, that the
    /// shared seam would be too thin to be worth it. Headlong disproved that; the retraction is in
    /// BREAKWATER.md under "What is NOT shared".</para>
    /// </summary>
    public class BreakwaterController : GateRaceController
    {
        [Header("Breakwater")]
        [Tooltip("The station geometry — dishes, collars, danger plugs and the shoals strung " +
                 "along the legs. INSTANTIATED rather than spawned off the asset, because this " +
                 "one is handed the match's course and writing per-match state onto a prefab " +
                 "asset is an edit to that asset in the Editor.")]
        [SerializeField] SpawnableBreakwater arenaPrefab;

        [Tooltip("Laps of the CIRCUIT. The start gate is not part of a lap, so a race is " +
                 "1 + (stations - 1) x laps crossings. Authored in the end-condition overrides; " +
                 "this is only the fallback for a missing asset.")]
        [SerializeField, Min(1)] int lapsFallback = BreakwaterCourseSettings.DefaultLaps;

        protected override string ModeName => "Breakwater";

        /// <summary>The start gate. See the class summary — this single 1 is the whole of what
        /// this mode needed the shared platform to learn.</summary>
        protected override int LeadInGates => 1;

        protected override int LapsPerRace
        {
            get
            {
                var overrides = ScriptableObjects.EndConditionOverridesSO.Instance;
                return Mathf.Max(1, overrides != null ? overrides.GetBreakwaterLaps() : lapsFallback);
            }
        }

        /// <summary>
        /// The RACE length in crossings — 29 by default (a start gate plus fourteen stations flown
        /// twice). The base treats this as the finish line and hands it back to
        /// <see cref="BuildCourse"/>, which inverts it to a station count, so the number that ends
        /// the turn and the number of stations laid are one authored value read once.
        /// </summary>
        public override int AuthoredGateTarget()
        {
            var overrides = ScriptableObjects.EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBreakwaterCrossingTarget()
                : BreakwaterCourseSettings.CrossingTarget(
                      ScriptableObjects.EndConditionOverridesSO.DefaultBreakwaterStationTarget,
                      BreakwaterCourseSettings.DefaultLaps);
        }

        /// <summary>
        /// <paramref name="gateCount"/> is the race length; stations are what get laid. Inverting
        /// <see cref="BreakwaterCourseSettings.CrossingTarget"/> here rather than reading the
        /// station key a second time is what keeps the two from ever disagreeing.
        /// </summary>
        int StationsFor(int crossings) =>
            LeadInGates + Mathf.Max(1, (crossings - LeadInGates)) / Mathf.Max(1, LapsPerRace);

        protected override List<RaceGate> BuildCourse(int seed, int gateCount, float inner, float outer)
        {
            int stations = Mathf.Max(3, StationsFor(gateCount));
            var settings = BreakwaterCourseSettings.ForIntensity(Intensity);
            settings.StationCount = stations;
            settings.InnerRadius = inner;
            settings.OuterRadius = outer;

            // PHASE 1 — RESEED, never shorten. Halving a station count answers a rare roll by
            // shipping that ONE match a race half the length of every other, which the players in
            // it can see and cannot explain. Since the circuit replaced the walk this is belt and
            // braces rather than a live path — the loop closes by construction and its amplitude
            // shrink bottoms out on a regular zigzag ring, which is legal (measured: 0 failures in
            // 1,600 courses). It is kept because the settings it is handed are AUTHORABLE.
            int usedSeed = seed;
            List<BreakwaterStation> course = null;
            for (int attempt = 0; ; attempt++)
            {
                course = BreakwaterCourse.Generate(usedSeed, settings);
                if (course != null && course.Count >= settings.StationCount) break;

                course = null;
                if (attempt >= BreakwaterCourse.ReseedAttempts) break;

                usedSeed = DeriveNextSeed(usedSeed);
                CSDebug.LogWarning(
                    $"[Breakwater] Course generation failed for {settings.StationCount} stations " +
                    $"in shell {settings.InnerRadius:F0}..{settings.OuterRadius:F0}; reseeding to " +
                    $"{usedSeed} ({attempt + 1}/{BreakwaterCourse.ReseedAttempts}).");
            }

            // PHASE 2 — only once every reseed has failed: SHORTEN, floor 3. A shell genuinely too
            // tight is a CONFIGURATION fault and both knobs that cause it are authorable, so it
            // must degrade rather than hang. Floor 3, not 2: a course is a start gate plus a
            // CIRCUIT, and a two-station circuit is a line rather than a loop.
            if (course == null)
            {
                int ask = settings.StationCount;
                while (ask > 3)
                {
                    ask = Mathf.Max(3, ask / 2);
                    settings.StationCount = ask;
                    course = BreakwaterCourse.Generate(usedSeed, settings);
                    if (course != null && course.Count >= ask) break;
                    course = null;
                }

                if (course != null)
                    CSDebug.LogWarning(
                        $"[Breakwater] Course generation failed at {stations} stations after " +
                        $"{BreakwaterCourse.ReseedAttempts} reseeds; racing to {course.Count} " +
                        "instead. Widen the shell or shorten the legs.");
            }

            // Null falls through to the base, which reports it and releases the connecting panel.
            if (course == null) return null;

            var gates = new List<RaceGate>(course.Count);
            for (int i = 0; i < course.Count; i++)
                gates.Add(new RaceGate(course[i].Position, course[i].Axis, course[i].PortRadius));
            return gates;
        }

        /// <summary>
        /// The next seed after a failed roll. DERIVED rather than re-rolled, so a PINNED seed
        /// stays reproducible all the way through its fallback chain. (Numerical Recipes' LCG
        /// constants — any full-period mix would do; what matters is that it is a function of the
        /// input.)
        /// </summary>
        static int DeriveNextSeed(int seed) => unchecked(seed * 1664525 + 1013904223);

        /// <summary>
        /// The rings stand; hang the stations they are the mouths of. Called by the platform while
        /// the connecting panel still covers the screen, and BEFORE it releases — which is what
        /// the hand-off depends on: <c>Spawn</c> reaches
        /// <c>SpawnableBreakwater.LaySegmentsAsync</c>, which opens its OWN arena-build bracket
        /// before its first await, so the gate is already held by the lay itself by the time this
        /// returns and the panel never sees a moment with nothing pending.
        ///
        /// <para>The prism CONTAINER is left where <c>Spawn</c> puts it — the scene root, at world
        /// identity. The course is already in world coordinates, and a prism's emitted position is
        /// local to that container, so container-local IS world; re-parenting it under a
        /// controller that is not itself at the origin would slide the whole arena off the course
        /// it was built for.</para>
        /// </summary>
        protected override void OnCourseRaised()
        {
            if (arenaPrefab == null)
            {
                CSDebug.LogError("[Breakwater] No arena prefab assigned — the course has rings but " +
                                 "no stations, so every port is an open hoop and the mode's whole " +
                                 "fire/saw/thread choice is missing. Assign SpawnableBreakwater on " +
                                 "the controller.");
                return;
            }

            var arena = Instantiate(arenaPrefab, transform);
            arena.name = "BreakwaterArena";

            // Before Spawn, never after: Spawn runs the generation that reads it. The RaceGate the
            // platform carries and the BreakwaterStation the builder wants hold the same three
            // values, so the conversion is total.
            var stations = new List<BreakwaterStation>(_course.Count);
            for (int i = 0; i < _course.Count; i++)
                stations.Add(new BreakwaterStation(_course[i].Position, _course[i].Axis, _course[i].Radius));
            arena.SetCourse(stations);

            // The pads in the COURSE'S frame — the course was offset onto the cell before it was
            // broadcast, so these must be too, and every peer computes them from the same cell it
            // built the rest of the arena around.
            arena.SetSpawnPads(BreakwaterCourse.SpawnPadRing(ResolveCellCentre(),
                                                             BreakwaterCourseSettings.DefaultSpawnRingRadius));

            var container = arena.Spawn(Intensity);
            if (container) container.name = "BreakwaterArena (prisms)";
        }
    }
}
