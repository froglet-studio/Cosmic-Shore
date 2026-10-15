using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thresher Time - the Thresher-only circuit race. A closed loop of switch rings is cut through
    /// the cell and every pilot flies LAPS of it in order; the first domain whose lead runner
    /// threads the last gate of the last lap wins, scored on finish TIME (golf).
    ///
    /// <para><b>The mode is a question about slinging.</b> The Thresher cruises at 125 u/s on a
    /// fixed 120 deg/s turn - a 60 u circle. Everything past cruise comes from its plant: pull LT
    /// all the way and the ball stops and becomes a pivot, the ship orbits it, RT winds the chain
    /// in and spins the orbit up by angular momentum, and letting go throws the hull off the
    /// circle at the orbit's speed - up to the ball's own 479 u/s ceiling. That launch is the
    /// hull's OWN momentum: grip keeps it on the nose, so the hull turns all of it at the same
    /// 120 deg/s, on a circle that grows with the speed (229 u at the ceiling), while the
    /// throttle bleeds it back to cruise. Every corner asks <i>how much sling is this corner
    /// worth?</i> - none of them at level 1, one at 2, two at 3, three at 4. See
    /// <see cref="ThresherTimeCourse"/> for the curve and the ladder.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new ability, no cell of its own, no new scoring
    /// metric and no circuit generator - Grizzly Time's shape (Redline's on Headlong's solver),
    /// with <see cref="ThresherTimeCourse"/> supplying the Thresher's cut. The Thresher's shipped
    /// kit is the racing: the Plant is the sling, the Winch is the wind-in, and the ball smashes
    /// whatever a rival lays across the line.</para>
    /// </summary>
    public class ThresherTimeController : GateRaceController
    {
        [Header("Thresher Time circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = ThresherTimeCourseSource.DefaultLaps;

        protected override string ModeName => "Thresher Time";

        protected override RaceCourseSource CreateCourseSource() =>
            new ThresherTimeCourseSource { Laps = laps };
    }
}
