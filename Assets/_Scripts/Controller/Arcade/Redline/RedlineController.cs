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

        protected override RaceCourseSource CreateCourseSource() =>
            new RedlineCourseSource { Laps = laps };
    }
}
