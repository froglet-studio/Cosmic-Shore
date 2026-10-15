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

        /// <summary>The course: a start gate (the platform's one lead-in) plus a closed circuit.
        /// See the class summary - the single lead-in is the whole of what this mode needed the
        /// shared platform to learn.</summary>
        protected override RaceCourseSource CreateCourseSource() =>
            new BreakwaterCourseSource { LapsFallback = lapsFallback };

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

            // Before Spawn, never after: Spawn runs the generation that reads it. The course source
            // does the posing so the arcade card's preview hangs the identical stations. The pads
            // are in the COURSE'S frame - the course was offset onto the cell before it was
            // broadcast, so these must be too.
            CourseSource.PoseCourseStructure(arena, _course, ResolveCellCentre());

            var container = arena.Spawn(Intensity);
            if (container) container.name = "BreakwaterArena (prisms)";
        }
    }
}
