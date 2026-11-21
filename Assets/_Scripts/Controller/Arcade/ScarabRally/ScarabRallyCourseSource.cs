using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Scarab Rally's course: Switchback's open walk (<see cref="SwitchbackCourse"/>) on the Scarab's
    /// settings (<see cref="ScarabRallyCourse.ForIntensity"/>). The walk can fail, so this backs off
    /// exactly as <see cref="SwitchbackCourseSource"/> does.
    /// </summary>
    public sealed class ScarabRallyCourseSource : RaceCourseSource
    {
        /// <summary>Where gate 1 sits along the spawn formation's pole - Switchback's distance, the
        /// one point every pilot on the equatorial spawn ring is equidistant from.</summary>
        public float FirstGateDistance = SwitchbackCourseSource.DefaultFirstGateDistance;

        public override GameModes Mode => GameModes.ScarabRally;

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetScarabRallyGateTarget()
                : EndConditionOverridesSO.DefaultScarabRallyGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            float inner = request.Inner, outer = request.Outer;

            var settings = ScarabRallyCourse.ForIntensity(request.Intensity);
            settings.InnerRadius = inner;
            settings.OuterRadius = outer;
            settings.FirstGateDirection = Vector3.up;   // the equatorial spawn ring's pole
            settings.FirstGateDistance = Mathf.Clamp(FirstGateDistance, inner, outer);

            int ask = Mathf.Max(2, request.GateCount);
            while (ask >= 2)
            {
                settings.GateCount = ask;
                var course = SwitchbackCourse.Generate(request.Seed, settings);
                if (course != null && course.Count >= ask) return course;

                if (ask == 2) break;
                ask = Mathf.Max(2, ask / 2);
                CSDebug.LogWarning(
                    $"[Scarab Rally] Course generation failed for {settings.GateCount} gates in shell " +
                    $"{inner:F0}..{outer:F0}; retrying with {ask}.");
            }

            failure = "Even a two-gate walk failed - widen the shell or shorten the legs.";
            return null;
        }
    }
}
