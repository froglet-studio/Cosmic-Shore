using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's course: four movement questions, one per intensity (<see cref="BrittleStarTimeCourse"/>). A
    /// closed lap that cannot fail - any rung that cannot be laid falls back to the Gearbox, Headlong's solver.
    /// </summary>
    public sealed class BrittleStarTimeCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 2;

        public override GameModes Mode => GameModes.BrittleStarTime;

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBrittleStarTimeGateTarget()
                : EndConditionOverridesSO.DefaultBrittleStarTimeGateTarget;
        }

        /// <summary>
        /// I1 is the Gearbox (Headlong's solver); I2-I4 lay their rung's shape (<see cref="BrittleStarTimeCourse.BuildLap"/>).
        /// A shape that cannot be laid in this shell, or a race target that is not two laps of eight, flies the Gearbox
        /// instead, so the race always has a course.
        /// </summary>
        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            int laps = LapsPerRace(request.Intensity);
            int perLap = Mathf.Max(3, Mathf.CeilToInt(request.GateCount / (float)laps));
            if (BrittleStarTimeCourse.BuildLap(request.Intensity, request.Seed, request.Inner, request.Outer, perLap,
                                               out var lap))
                return lap;
            return HeadlongCourseSource.BuildCircuit(BrittleStarTimeCourse.Gearbox(), request, laps);
        }
    }
}
