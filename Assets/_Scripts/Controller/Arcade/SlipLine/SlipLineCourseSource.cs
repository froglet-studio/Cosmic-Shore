using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slip Line's course: the rings of <see cref="SlipLineCourse"/>, cut per intensity (three rails a leg, one coloured rail,
    /// leaning rails, chains); the controller lays the rails from these rings. The walk tries 128 sub-seeds of the match seed before it gives up; the tests hold 400 seeds per intensity.
    /// </summary>
    public sealed class SlipLineCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.SlipLine;

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSlipLineGateTarget()
                : EndConditionOverridesSO.DefaultSlipLineGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            var settings = SlipLineCourse.ForIntensity(request.Intensity);
            settings.RingCount = Mathf.Max(2, request.GateCount);
            settings.InnerRadius = request.Inner;
            var course = SlipLineCourse.Generate(request.Seed, settings);
            if (course == null)
                failure = $"No Slip Line course of {settings.RingCount} rings fits the shell at intensity {request.Intensity} - " +
                          "the race is cut for 16 rings (SLIP_LINE.md); set the target back or re-run the course tests.";
            return course;
        }
    }
}
