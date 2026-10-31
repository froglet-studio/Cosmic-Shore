using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// WAYSTATION PROTOTYPE's course: Waystation's clusters (<see cref="WaystationCourse"/>, called and never edited)
    /// with a Drop Tower in every gap but the last (<see cref="WaystationTower"/>). The authored target is a number of
    /// CLUSTERS; the finish line is every ring the course lays, towers included, because a tower's length depends on
    /// its gap.
    /// </summary>
    public sealed class WaystationPrototypeCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.WaystationPrototype;

        public override string ModeName => "Waystation Prototype";

        int _laidIntensity = -1;
        int _laidRings;

        /// <summary>Clusters a race lays, from the End Game Conditions window.</summary>
        public static int ClusterTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return Mathf.Max(2, overrides != null
                ? overrides.GetWaystationPrototypeClusterTarget()
                : EndConditionOverridesSO.DefaultWaystationPrototypeClusterTarget);
        }

        /// <summary>
        /// The rings the last course built at this intensity laid, which is the finish line; before any course exists,
        /// the cluster rings alone. <c>RaceGateTurnMonitor</c> reads the laid course in any case
        /// (<c>GateRaceController.AuthoritativeGateCount</c>); answering the same number here keeps it from warning that
        /// the two disagree.
        /// </summary>
        public override int AuthoredGateTarget(int intensity)
        {
            if (intensity == _laidIntensity && _laidRings > 0) return _laidRings;
            return ClusterTarget() * Mathf.Max(1, WaystationCourseSettings.ForIntensity(intensity).RingsPerCluster);
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            var settings = WaystationCourseSettings.ForIntensity(request.Intensity);
            settings.InnerRadius = request.Inner;
            settings.OuterRadius = request.Outer;
            // CLUSTER rings, from the window - never request.GateCount, which is this source's own answer and includes
            // the towers once a course has been laid.
            settings.RingTarget = ClusterTarget() * Mathf.Max(1, settings.RingsPerCluster);
            settings.FirstClusterDirection = Vector3.up;   // the equatorial spawn ring's pole

            var course = WaystationTower.Build(request.Seed, settings, request.Intensity, out _);
            if (course != null && course.Count > 0)
            {
                _laidIntensity = request.Intensity;
                _laidRings = course.Count;
                failure = null;
                return course;
            }

            failure =
                $"WaystationTower returned nothing for {settings.RingTarget} cluster rings in shell " +
                $"{request.Inner:F0}..{request.Outer:F0} - Waystation's clusters have no failure path, so this is a " +
                "settings fault (RingsPerCluster or the cluster target at zero).";
            return null;
        }
    }
}
