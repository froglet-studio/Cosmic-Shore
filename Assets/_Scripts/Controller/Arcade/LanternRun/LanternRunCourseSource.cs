using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// LanternRun's course: Headlong's solver cut to <see cref="LanternRunCourse.ForIntensity"/>. A closed circuit that
    /// cannot fail - the shared solver relaxes toward a regular ring. The clusters are laid from the course afterwards
    /// (<see cref="LanternRunController"/>), on every peer.
    /// </summary>
    public sealed class LanternRunCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 2;

        public override GameModes Mode => GameModes.LanternRun;

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetLanternRunGateTarget()
                : EndConditionOverridesSO.DefaultLanternRunGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            return HeadlongCourseSource.BuildCircuit(LanternRunCourse.ForIntensity(request.Intensity),
                                                     request, LapsPerRace(request.Intensity));
        }
    }
}
