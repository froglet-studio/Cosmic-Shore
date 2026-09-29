using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breakwater's course: a polar START GATE (the platform's one lead-in) plus a closed circuit
    /// of stations. The stations themselves - the dishes and the danger weave - are hung on the
    /// course by <c>BreakwaterController.OnCourseRaised</c>; this is the rings only.
    /// </summary>
    public sealed class BreakwaterCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the circuit when the end-condition overrides asset is missing.</summary>
        public int LapsFallback = BreakwaterCourseSettings.DefaultLaps;

        /// <summary>The shipped scene's inner fallback (420, not the platform's 480).</summary>
        public const float SceneInnerRadiusFallback = 420f;

        public BreakwaterCourseSource() => InnerRadiusFallback = SceneInnerRadiusFallback;

        public override GameModes Mode => GameModes.Breakwater;

        public override int LeadInGates => 1;

        public override int LapsPerRace(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return Mathf.Max(1, overrides != null ? overrides.GetBreakwaterLaps() : LapsFallback);
        }

        /// <summary>The RACE length in crossings - a start gate plus the stations flown per lap.</summary>
        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBreakwaterCrossingTarget()
                : BreakwaterCourseSettings.CrossingTarget(
                      EndConditionOverridesSO.DefaultBreakwaterStationTarget,
                      BreakwaterCourseSettings.DefaultLaps);
        }

        /// <summary>Inverts <see cref="BreakwaterCourseSettings.CrossingTarget"/>, so the number that
        /// ends the turn and the number of stations laid are one authored value.</summary>
        int StationsFor(int crossings, int intensity) =>
            LeadInGates + Mathf.Max(1, (crossings - LeadInGates)) / Mathf.Max(1, LapsPerRace(intensity));

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            int stations = Mathf.Max(3, StationsFor(request.GateCount, request.Intensity));
            var settings = BreakwaterCourseSettings.ForIntensity(request.Intensity);
            settings.StationCount = stations;
            settings.InnerRadius = request.Inner;
            settings.OuterRadius = request.Outer;

            // PHASE 1 — RESEED, never shorten. Halving a station count answers a rare roll by
            // shipping that ONE match a race half the length of every other. Since the circuit
            // replaced the walk this is belt and braces (measured: 0 failures in 1,600 courses),
            // kept because the settings it is handed are AUTHORABLE.
            int usedSeed = request.Seed;
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

            // PHASE 2 — only once every reseed has failed: SHORTEN, floor 3 (a start gate plus a
            // circuit; a two-station circuit is a line rather than a loop).
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

            if (course == null)
            {
                failure = "Every reseed and every shortening failed - widen the shell or shorten the legs.";
                return null;
            }

            var gates = new List<RaceGate>(course.Count);
            for (int i = 0; i < course.Count; i++)
                gates.Add(new RaceGate(course[i].Position, course[i].Axis, course[i].PortRadius));
            return gates;
        }

        /// <summary>
        /// The next seed after a failed roll. DERIVED rather than re-rolled, so a PINNED seed
        /// stays reproducible all the way through its fallback chain (Numerical Recipes' LCG).
        /// </summary>
        static int DeriveNextSeed(int seed) => unchecked(seed * 1664525 + 1013904223);
    }
}
