using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Redline - the Manta-only circuit race. A closed loop of switch rings is cut through the
    /// cell and every pilot flies LAPS of it in order; the first domain whose lead runner threads
    /// the last gate of the last lap wins.
    ///
    /// <para><b>The mode is a question about two triggers.</b> The Manta's Soar is the overlap of
    /// its triggers and its Yastri turn is their difference, so a pilot holding one flat and
    /// easing the other is trading boost for yaw on one linear scale - and every corner asks
    /// <i>how much Soar is this corner worth?</i> Speed lags the answer by a 1.5/s lerp, so a
    /// boost given up costs about two seconds to win back. Intensity is how many corners a lap
    /// asks that question at: none at level 1, one at 2, two at 3, and at 4 two hairpins plus a
    /// knife-edge sweeper a pilot can hold flat out only by being exact. See
    /// <see cref="RedlineCourse"/> for the curve and the ladder.</para>
    ///
    /// <para><b>Why the Manta and not another hull.</b> Cruise 180, x4 on Soar to 720 (x1.3
    /// again at Time 10), and a turning circle that CONVERGES (RotationThrottleScaler 0.2 -
    /// 237 u at full boost, 286 u asymptotically). It is the fleet's fast hull, and a course cut
    /// around its bounded radius is a course it flies at the speed the mode is named for.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new weapon, no new ability, no cell of its
    /// own, no new scoring metric, and no generator of its own - the circuit solver is
    /// <see cref="HeadlongCircuit"/>, shared, with <see cref="RedlineCourse"/> supplying the
    /// Manta's cut. The Manta's shipped kit supplies the racing; its Yastri turn trails (laid
    /// across the line at Mass 5, shielded) are the interference, and a crystal on the course
    /// fires its Kabloom the ordinary way.</para>
    /// </summary>
    public class RedlineController : GateRaceController
    {
        [Header("Redline circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = RedlineCourseSource.DefaultLaps;

        protected override string ModeName => "Redline";

        /// <summary>
        /// The AI Mantas fly the Regatta steering (commit from the approach point, through the ring's centre, break off
        /// and re-attack after a missed mouth), as ten other gate races do. On the old steering they lined up on a
        /// point 320 u past the mouth on its AXIS and circled after a miss: offline
        /// (<c>Tools/Build/manta_redline_sim.py</c>, which ports this AI line for line) they finished 4 of 12 races
        /// over I1-I4 and none at I3 or I4, where the mouths are 72 and 44 u; on this steering they finish 12 of 12
        /// (2026-11-03, REDLINE.md section 5). The AI's throttle stays the skill throttle: a floor of 1 measured
        /// faster at I1 and lost races at I4.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new RedlineCourseSource { Laps = laps };
    }
}
