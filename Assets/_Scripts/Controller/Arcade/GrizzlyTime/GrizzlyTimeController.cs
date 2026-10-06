using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Time - the Grizzly-only circuit race. A closed loop of switch rings is cut through
    /// the cell and every pilot flies LAPS of it in order; the first domain whose lead runner
    /// threads the last gate of the last lap wins, scored on finish TIME (golf).
    ///
    /// <para><b>The mode is a question about the bomb pump.</b> The Grizzly cruises at 50 u/s on
    /// a 30 u turning circle - the slowest and tightest hull in the fleet - and everything past
    /// cruise comes from alternating LT/RT bombs, each kick a world-space shove along the nose
    /// that stacks to the vessel's 100 u/s ceiling. Pumped flat out it holds 150 u/s on a 90 u
    /// circle; lift and it pivots on 30 u, then winds the pump back up. Every corner asks
    /// <i>how much pump is this corner worth?</i> - none of them at level 1, one at 2, two at
    /// 3, three at 4. See <see cref="GrizzlyTimeCourse"/> for the curve and the ladder.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new weapon, no new ability, no cell of its
    /// own, no new scoring metric and no circuit generator - Redline's shape on Headlong's
    /// solver, with <see cref="GrizzlyTimeCourse"/> supplying the Grizzly's cut. The Grizzly's
    /// shipped kit is the racing: Rush is a burst onto a straight, Dig In is a hard stop, and a
    /// bomb blown across a rival's trail breaks it.</para>
    /// </summary>
    public class GrizzlyTimeController : GateRaceController
    {
        [Header("Grizzly Time circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = 2;

        protected override string ModeName => "Grizzly Time";

        protected override int LapsPerRace => Mathf.Max(1, laps);

        public override int AuthoredGateTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetGrizzlyTimeGateTarget()
                : EndConditionOverridesSO.DefaultGrizzlyTimeGateTarget;
        }

        /// <summary>
        /// A closed circuit that cannot fail - the shared solver relaxes toward a regular ring
        /// rather than walking and backtracking. The settings are the Grizzly's
        /// (<see cref="GrizzlyTimeCourse.ForIntensity"/>); the solver is Headlong's.
        /// </summary>
        protected override List<RaceGate> BuildCourse(int seed, int gateCount, float inner, float outer)
        {
            var settings = GrizzlyTimeCourse.ForIntensity(Intensity);
            settings.InnerRadius = inner;
            settings.OuterRadius = outer;
            settings.FirstGateDirection = Vector3.up;   // the equatorial spawn ring's pole

            // The authored target is the RACE length; the circuit only needs one lap of rings.
            // Round UP so a target that does not divide by the lap count still yields a whole
            // circuit, and let RaceLength be the honest authority afterwards.
            int perLap = Mathf.Max(3, Mathf.CeilToInt(gateCount / (float)LapsPerRace));
            settings.GateCount = perLap;

            return HeadlongCircuit.Generate(seed, settings);
        }
    }
}
