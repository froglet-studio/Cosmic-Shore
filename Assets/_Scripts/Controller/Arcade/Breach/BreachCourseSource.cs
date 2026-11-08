using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breach's course: the rings of <see cref="BreachCourse"/>, cut per intensity (open water, hard turns, walls,
    /// both); the controller lays the walls from these rings. The walk tries 128 sub-seeds of the match seed before it gives up; the tests hold 400 seeds per intensity.
    /// </summary>
    public sealed class BreachCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.Breach;

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBreachGateTarget()
                : EndConditionOverridesSO.DefaultBreachGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            var settings = BreachCourse.ForIntensity(request.Intensity);
            settings.RingCount = Mathf.Max(2, request.GateCount);
            settings.InnerRadius = request.Inner;
            var course = BreachCourse.Generate(request.Seed, settings);
            if (course == null)
                failure = $"No Breach course of {settings.RingCount} rings fits the shell at intensity {request.Intensity} - " +
                          "the race is cut for 16 rings (BREACH.md); set the target back or re-run the course tests.";
            return course;
        }
    }
}
