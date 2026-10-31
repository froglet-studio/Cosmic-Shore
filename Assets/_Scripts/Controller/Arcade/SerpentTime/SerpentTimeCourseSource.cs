using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Time's course: a switchback ladder of rings far apart with hairpin bends (<see cref="SerpentTimeCourse.Build"/>),
    /// relaxed toward a narrower ladder until every leg clears the nucleus.
    /// </summary>
    public sealed class SerpentTimeCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 2;

        public override GameModes Mode => GameModes.SerpentTime;

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSerpentTimeGateTarget()
                : EndConditionOverridesSO.DefaultSerpentTimeGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            int perLap = Mathf.Max(3, Mathf.CeilToInt(request.GateCount / (float)LapsPerRace(request.Intensity)));
            return SerpentTimeCourse.Build(request.Seed, request.Intensity, perLap, request.Inner, request.Outer);
        }
    }
}
