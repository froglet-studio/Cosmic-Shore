using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time - the Brittle Star-only circuit race, and its TIME card. A closed loop of
    /// switch rings flown in LAPS; the first domain whose lead runner threads the last gate of the
    /// last lap wins, scored on finish TIME.
    ///
    /// <para><b>The mode is a question about Speed Mode.</b> The Brittle Star cruises at 45 u/s; Speed
    /// Mode (A) runs it at six times that, 270 u/s (x8 at Time 10), and lights all five arms. Its turn
    /// barely grows with speed, so Speed Mode flies a ~145 u circle against cruise's ~30 u: a corner
    /// tighter than that is a choice between swinging wide in Speed Mode and dropping to Cruise to
    /// turn in. See <see cref="BrittleStarTimeCourse"/> and BRITTLESTARTIME.md.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no ability, no cell, no metric and no circuit
    /// generator - Thresher Time's shape (Headlong's solver), with <see cref="BrittleStarTimeCourse"/> supplying
    /// the cut.</para>
    /// </summary>
    public class BrittleStarTimeController : GateRaceController
    {
        [Header("Brittle Star Time circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = BrittleStarTimeCourseSource.DefaultLaps;

        protected override string ModeName => "Brittle Star Time";

        /// <summary>Regatta's gate steering, which made the AI race Switchback and Warpline: commit from
        /// the approach point and aim through the ring's centre.</summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new BrittleStarTimeCourseSource { Laps = laps };
    }
}
