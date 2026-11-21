using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slingshot's course: a closed circuit on the Stoat's cut (<see cref="SlingshotCourse.ForIntensity"/>), laid by Headlong's solver.
    /// </summary>
    public sealed class SlingshotCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 2;

        public override GameModes Mode => GameModes.Slingshot;

        /// <summary>The scene's laps, or the Time Race Prototyper's when its course for this race is on.</summary>
        public override int LapsPerRace(int intensity) => TimeRaceCourseConfigSO.LapsFor(Mode, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            int shipped = overrides != null
                ? overrides.GetSlingshotGateTarget()
                : EndConditionOverridesSO.DefaultSlingshotGateTarget;
            return TimeRaceCourseConfigSO.GateTargetFor(Mode, shipped);
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            return HeadlongCourseSource.BuildCircuit(TimeRaceCourseConfigSO.Circuit(Mode, request.Intensity), request,
                                                     LapsPerRace(request.Intensity));
        }
    }
}
