using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Redline's course: Headlong's solver cut to the Manta's settings
    /// (<see cref="RedlineCourse.ForIntensity"/>).
    /// </summary>
    public sealed class RedlineCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        /// <summary>8 since 2026-11-11 (5 before): Soar lit from the gun makes a lap ~5-11 s (REDLINE.md section 0).</summary>
        public const int DefaultLaps = 8;

        public override GameModes Mode => GameModes.Redline;

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetRedlineGateTarget()
                : EndConditionOverridesSO.DefaultRedlineGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            return HeadlongCourseSource.BuildCircuit(RedlineCourse.ForIntensity(request.Intensity),
                                                     request, LapsPerRace(request.Intensity));
        }
    }
}
