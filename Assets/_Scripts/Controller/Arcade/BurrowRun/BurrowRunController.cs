using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Burrow Run - the Jewel Wasp's TIME race (<c>Arcade/BURROWRUN.md</c>): a closed loop of eight switch rings cut
    /// through the barren race cell, flown in two laps; the first domain whose lead runner threads the last gate of the
    /// last lap wins, scored on finish TIME (golf). It was built as another hull's race and re-seated on the wasp on
    /// 2026-11-22 (Yash); the doc says what that left open.
    ///
    /// <para><b>What the mode does NOT add:</b> no ability, no cell, no scoring metric and no circuit generator -
    /// Warpline's shape and Slingshot's cut (<see cref="SlingshotCourse"/>). It changes nothing on the wasp and presses
    /// nothing for its AI: the gate race's own steering flies an AI hull (<see cref="GateRaceController"/>).</para>
    /// </summary>
    public class BurrowRunController : GateRaceController
    {
        [Header("Burrow Run circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = BurrowRunCourseSource.DefaultLaps;

        protected override string ModeName => "BurrowRun";

        protected override RaceCourseSource CreateCourseSource() =>
            new BurrowRunCourseSource { Laps = laps };

        /// <summary>Regatta's gate steering, as Warpline flies it: commit from the approach point, aim through the ring's
        /// centre, re-attack a missed mouth.</summary>
        protected override bool AICommitFromApproachPoint => true;
    }
}
