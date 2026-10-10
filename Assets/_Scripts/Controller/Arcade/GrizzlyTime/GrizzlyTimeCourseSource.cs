using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Time's course: Headlong's solver cut to the Grizzly's settings
    /// (<see cref="GrizzlyTimeCourse.ForIntensity"/>). A closed circuit that cannot fail - the
    /// shared solver relaxes toward a regular ring rather than walking and backtracking.
    /// </summary>
    public sealed class GrizzlyTimeCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 3;

        public override GameModes Mode => GameModes.GrizzlyTime;

        public override string ModeName => "Grizzly Time";

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetGrizzlyTimeGateTarget()
                : EndConditionOverridesSO.DefaultGrizzlyTimeGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            return HeadlongCourseSource.BuildCircuit(GrizzlyTimeCourse.ForIntensity(request.Intensity),
                                                     request, LapsPerRace(request.Intensity));
        }
    }
}
