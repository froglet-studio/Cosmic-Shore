using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Time - the Serpent-only circuit race, and the Serpent's TIME card. A closed loop of
    /// switch rings is cut through the cell and every pilot flies LAPS of it in order; the first
    /// domain whose lead runner threads the last gate of the last lap wins, scored on finish TIME.
    ///
    /// <para><b>The mode is a question about fuel.</b> The Serpent cruises at 60 u/s. Its Time ability
    /// is a tank of four Solid Fuel Pellets: each press burns one for 3 s, burns overlap and stack
    /// (one 160 u/s, four 460 u/s), and one pellet comes back every ~3.6 s. So every straight asks
    /// how many pellets it is worth, and every tight corner whether to arrive with a stack still
    /// burning. See <see cref="SerpentTimeCourse"/> and SERPENTTIME.md.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no ability, no cell, no metric and no circuit
    /// generator - Thresher Time's shape (Headlong's solver), with <see cref="SerpentTimeCourse"/> supplying
    /// the cut.</para>
    /// </summary>
    public class SerpentTimeController : GateRaceController
    {
        [Header("Serpent Time circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = SerpentTimeCourseSource.DefaultLaps;

        protected override string ModeName => "Serpent Time";

        /// <summary>Regatta's gate steering, which made the AI race Switchback and Warpline: commit from
        /// the approach point and aim through the ring's centre.</summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new SerpentTimeCourseSource { Laps = laps };
    }
}
