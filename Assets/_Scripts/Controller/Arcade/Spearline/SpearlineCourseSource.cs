using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spearline's course: Headlong's closed circuit (<see cref="HeadlongCircuit"/>, cut for this airframe's 840 u/s ramp
    /// and its turn), flown in laps. It cannot fail. The controller lays the seals from these rings.
    /// </summary>
    public sealed class SpearlineCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 3;

        public override GameModes Mode => GameModes.Spearline;

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSpearlineGateTarget()
                : EndConditionOverridesSO.DefaultSpearlineGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            return HeadlongCourseSource.BuildCircuit(HeadlongCircuitSettings.ForIntensity(request.Intensity),
                                                     request, LapsPerRace(request.Intensity));
        }
    }
}
