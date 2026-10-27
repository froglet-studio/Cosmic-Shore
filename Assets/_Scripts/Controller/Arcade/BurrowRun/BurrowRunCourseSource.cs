using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Burrow Run's course: Slingshot's circuit (<see cref="SlingshotCourse.ForIntensity"/>, the Vessel Studio's eight-ring
    /// circle, its intensity ladder) flown in laps.
    /// </summary>
    public sealed class BurrowRunCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 2;

        public override GameModes Mode => GameModes.BurrowRun;

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBurrowRunGateTarget()
                : EndConditionOverridesSO.DefaultBurrowRunGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            return HeadlongCourseSource.BuildCircuit(SlingshotCourse.ForIntensity(request.Intensity), request, LapsPerRace(request.Intensity));
        }
    }
}
