using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Decoy Run's course: the grouped rings of <see cref="DecoyRunCourse"/>, each group on its own flat from I3. The walk
    /// tries 64 sub-seeds of the match seed before it gives up; the tests hold 400 seeds per intensity.
    /// </summary>
    public sealed class DecoyRunCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.DecoyRun;

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetDecoyRunGateTarget()
                : EndConditionOverridesSO.DefaultDecoyRunGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            var settings = DecoyRunCourse.ForIntensity(request.Intensity);
            settings.RingCount = Mathf.Max(2, request.GateCount);
            settings.InnerRadius = request.Inner;
            var course = DecoyRunCourse.Generate(request.Seed, settings);
            if (course == null)
                failure = $"No Decoy Run course of {settings.RingCount} rings fits the shell at intensity {request.Intensity} - " +
                          "the race is cut for 12 rings (DECOY_RUN.md); set the target back or re-run the course tests.";
            return course;
        }
    }
}
