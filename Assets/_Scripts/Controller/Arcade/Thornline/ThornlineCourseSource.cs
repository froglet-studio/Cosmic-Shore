using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thornline's course: the thorn chain of <see cref="ThornlineCourse"/>, whose rings sit at the middle of every leg.
    /// The walk tries 64 sub-seeds of the match seed before it gives up; the tests hold 400 seeds per intensity.
    /// </summary>
    public sealed class ThornlineCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.Thornline;

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetThornlineGateTarget()
                : EndConditionOverridesSO.DefaultThornlineGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            var settings = ThornlineCourse.ForIntensity(request.Intensity);
            settings.RingCount = Mathf.Max(2, request.GateCount);
            settings.InnerRadius = request.Inner;
            var course = ThornlineCourse.Generate(request.Seed, settings, new List<Vector3>());
            if (course == null)
                failure = $"No thorn chain of {settings.RingCount} rings fits the shell at intensity {request.Intensity} - " +
                          "the race is cut for 16 rings (THORNLINE.md); set the target back or re-run the course tests.";
            return course;
        }
    }
}
