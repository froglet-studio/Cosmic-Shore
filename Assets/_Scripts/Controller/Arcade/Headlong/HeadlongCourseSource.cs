using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Headlong's course: a closed circuit flown in laps. It CANNOT fail - the solver relaxes
    /// toward a regular octagon rather than walking and backtracking. See
    /// <see cref="HeadlongCircuit"/>.
    /// </summary>
    public sealed class HeadlongCourseSource : RaceCourseSource
    {
        /// <summary>Laps of the ring set that make one race.</summary>
        public int Laps = DefaultLaps;

        public const int DefaultLaps = 3;

        public override GameModes Mode => GameModes.Headlong;

        public override int LapsPerRace(int intensity) => Mathf.Max(1, Laps);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetHeadlongGateTarget()
                : EndConditionOverridesSO.DefaultHeadlongGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            failure = null;
            return HeadlongCourseSource.BuildCircuit(HeadlongCircuitSettings.ForIntensity(request.Intensity),
                                                     request, LapsPerRace(request.Intensity));
        }

        /// <summary>
        /// The shared circuit recipe (Headlong's solver, Redline's settings). The authored target
        /// is the RACE length; the circuit only needs one lap of rings. Rounded UP so a target
        /// that does not divide by the lap count still yields a whole circuit.
        /// </summary>
        internal static List<RaceGate> BuildCircuit(HeadlongCircuitSettings settings,
                                                    in RaceCourseRequest request, int laps)
        {
            settings.InnerRadius = request.Inner;
            settings.OuterRadius = request.Outer;
            settings.FirstGateDirection = Vector3.up;   // the equatorial spawn ring's pole
            settings.GateCount = Mathf.Max(3, Mathf.CeilToInt(request.GateCount / (float)laps));
            return HeadlongCircuit.Generate(request.Seed, settings);
        }
    }
}
