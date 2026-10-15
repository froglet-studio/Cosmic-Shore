using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thresher Time's course: Headlong's solver cut to the Thresher's settings
    /// (<see cref="ThresherTimeCourse.ForIntensity"/>). A closed circuit that cannot fail - the
    /// shared solver relaxes toward a regular ring rather than walking and backtracking.
    /// </summary>
    public sealed class ThresherTimeCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 3;

        public override GameModes Mode => GameModes.ThresherTime;

        public override string ModeName => "Thresher Time";

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetThresherTimeGateTarget()
                : EndConditionOverridesSO.DefaultThresherTimeGateTarget;
        }

        /// <summary>
        /// The authored target is the RACE length; the circuit only needs one lap of rings
        /// (<see cref="HeadlongCourseSource.BuildCircuit"/> rounds up).
        /// the equatorial spawn ring's pole.
        /// </summary>
        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            var settings = ThresherTimeCourse.ForIntensity(request.Intensity);
            return HeadlongCourseSource.BuildCircuit(settings, request, LapsPerRace(request.Intensity));
        }
    }
}
