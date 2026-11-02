using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Switchback's course: an OPEN chain walked through the shell. See
    /// <see cref="RaceCourseSource"/> for why this lives outside the controller.
    /// </summary>
    public sealed class SwitchbackCourseSource : RaceCourseSource
    {
        /// <summary>Where gate 1 sits along the spawn formation's pole - the one point every
        /// pilot on the equatorial spawn ring is equidistant from.</summary>
        public float FirstGateDistance = DefaultFirstGateDistance;

        public const float DefaultFirstGateDistance = 620f;

        public override GameModes Mode => GameModes.Switchback;

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSwitchbackGateTarget()
                : EndConditionOverridesSO.DefaultSwitchbackGateTarget;
        }

        /// <summary>
        /// The walk CAN fail (its constraint is reachability, and a shell too tight for the
        /// requested gate count is a configuration fault), so this backs off - halving the ask
        /// until the walk succeeds, floor 2 - rather than returning nothing. Whatever comes back
        /// is then the authoritative target, so a shortened course still has a finish line.
        /// </summary>
        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            float inner = request.Inner, outer = request.Outer;

            var settings = SwitchbackCourseSettings.ForIntensity(request.Intensity);
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
                    $"[Switchback] Course generation failed for {settings.GateCount} gates in shell " +
                    $"{inner:F0}..{outer:F0} (step {settings.MinStep:F0}..{settings.MaxStep:F0}, " +
                    $"separation {settings.MinSeparation:F0}); retrying with {ask}.");
            }

            failure = "Even a two-gate walk failed - widen the shell or shorten the legs.";
            return null;
        }
    }
}
