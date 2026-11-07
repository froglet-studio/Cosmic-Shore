using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time - the Brittle Star-only circuit race, and its TIME card. A closed loop of
    /// switch rings flown in LAPS; the first domain whose lead runner threads the last gate of the
    /// last lap wins, scored on finish TIME.
    ///
    /// <para><b>The mode is four questions about Speed Mode and the turn, one per intensity.</b> The Brittle Star
    /// cruises at 45 u/s; Speed Mode (A) runs it at six times that, 270 u/s, on a ~145 u circle against cruise's ~30 u.
    /// I1 Gearbox: hold Speed or shift down. I2 Feather slalom: tap A for a speed between. I3 Coast-in hairpins: let go
    /// early and coast in. I4 Corkscrew: start at Time 5 and roll (Speed Roll) to hold Speed through a coil. See
    /// <see cref="BrittleStarTimeCourse"/> and BRITTLESTARTIME.md.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no ability, no cell, no metric and no vessel change - the course
    /// source (<see cref="BrittleStarTimeCourseSource"/>) supplies the shapes, Headlong's solver the Gearbox, and the
    /// card's starting elements the Time 5 start.</para>
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
