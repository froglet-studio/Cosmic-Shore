using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Waystation's course: an open chain of ring clusters. It CANNOT fail - a cluster is a
    /// capped-turn walk from its own centre and a hop is clamped back into the shell.
    /// </summary>
    public sealed class WaystationCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.Waystation;

        /// <summary>
        /// ROUNDED UP to whole clusters, here rather than in the course: this is the number the
        /// turn monitor publishes as the finish line before a course exists, so the two have to
        /// agree from the first frame.
        /// </summary>
        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            int asked = overrides != null
                ? overrides.GetWaystationRingTarget()
                : EndConditionOverridesSO.DefaultWaystationRingTarget;

            int per = WaystationCourseSettings.ForIntensity(intensity).RingsPerCluster;
            return WaystationCourse.ClusterCount(asked, per) * Mathf.Max(1, per);
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            var settings = WaystationCourseSettings.ForIntensity(request.Intensity);
            settings.InnerRadius = request.Inner;
            settings.OuterRadius = request.Outer;
            settings.RingTarget = Mathf.Max(settings.RingsPerCluster, request.GateCount);
            settings.FirstClusterDirection = Vector3.up;   // the equatorial spawn ring's pole

            var course = WaystationCourse.Generate(request.Seed, settings);
            if (course != null && course.Count > 0)
            {
                failure = null;
                return course;
            }

            failure =
                $"WaystationCourse returned nothing for {settings.RingTarget} rings in shell " +
                $"{request.Inner:F0}..{request.Outer:F0} - it has no failure path, so this is a " +
                "settings fault (RingsPerCluster or RingTarget at zero).";
            return null;
        }
    }
}
