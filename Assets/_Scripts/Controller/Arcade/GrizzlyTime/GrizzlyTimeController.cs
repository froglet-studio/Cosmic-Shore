using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Time - the Grizzly-only circuit race. A closed loop of switch rings is cut through
    /// the cell and every pilot flies LAPS of it in order; the first domain whose lead runner
    /// threads the last gate of the last lap wins, scored on finish TIME (golf).
    ///
    /// <para><b>The mode is a question about riding your own blasts.</b> The Grizzly cruises at
    /// 50 u/s on a 30 u turning circle - the slowest and tightest hull in the fleet - and
    /// everything past cruise comes from its trigger bombs: fire, freeze, detonate, and a Grizzly
    /// inside its own blast is launched along its nose, a world-space shove up to the vessel's
    /// 100 u/s ceiling. Launching flat out it holds 150 u/s on a 90 u circle; coasting it pivots
    /// on 30 u, then has to fire and ride the next bomb. Every corner asks <i>how much launch is
    /// this corner worth?</i> - none of them at level 1, one at 2, two at 3, three at 4. See
    /// <see cref="GrizzlyTimeCourse"/> for the curve and the ladder.</para>
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
        [SerializeField, Min(1)] int laps = GrizzlyTimeCourseSource.DefaultLaps;

        protected override string ModeName => "Grizzly Time";

        protected override RaceCourseSource CreateCourseSource() =>
            new GrizzlyTimeCourseSource { Laps = laps };
    }
}
