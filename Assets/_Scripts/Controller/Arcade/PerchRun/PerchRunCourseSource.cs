using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Perch Run's course: the rings of <see cref="PerchRunCourse"/>, cut per intensity (open darts, hairpins, blockers,
    /// doubles). The walk tries 128 sub-seeds of the match seed before it gives up; the tests hold 400 seeds per intensity.
    /// </summary>
    public sealed class PerchRunCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.PerchRun;

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetPerchRunGateTarget()
                : EndConditionOverridesSO.DefaultPerchRunGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            var settings = PerchRunCourse.ForIntensity(request.Intensity);
            settings.RingCount = Mathf.Max(2, request.GateCount);
            settings.InnerRadius = request.Inner;
            var course = PerchRunCourse.Generate(request.Seed, settings);
            if (course == null)
                failure = $"No Perch Run course of {settings.RingCount} rings fits the shell at intensity {request.Intensity} - " +
                          "the race is cut for 16 rings (PERCH_RUN.md); set the target back or re-run the course tests.";
            return course;
        }
    }
}
