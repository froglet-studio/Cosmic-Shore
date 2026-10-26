using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Warpline - the Stoat's TIME race (<c>Arcade/WARPLINE.md</c>). A closed loop of switch rings is
    /// cut through the barren race cell and every pilot flies LAPS of it in order; the first domain
    /// whose lead runner threads the last gate of the last lap wins, scored on finish TIME (golf).
    ///
    /// <para><b>The mode is a question about the line.</b> The Stoat flies on its round-15 field
    /// dipole (<c>R_VesselActions/STOAT_DIPOLE.md</c>): the triggers hold a sink–source pair open ahead
    /// of it, and its pathfinder draws the path it will fly. While the poles WARP that path the line is
    /// lime and the hull flies down it up to 5× faster (Time scales the boost) — and a sink laid on the
    /// line to the next ring throws the hull out of the source, further on. The barren cell has nothing
    /// to skim, so warp is the only speed past cruise. Five rings a lap, so every leg is long enough to
    /// lay a pair and ride it; every corner asks which pole to pull, and how far.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new ability, no cell, no scoring metric and no
    /// circuit generator — Slingshot's shape and Slingshot's cut (<see cref="SlingshotCourse"/>) with
    /// fewer, longer legs. The autopilot lays its pairs through the replicated press path
    /// (<c>StoatDipoleExecutor.Autopilot</c>), and switches to Time mode's orbit for wide turns
    /// (<c>StoatModeExecutor</c>, <c>R_VesselActions/STOAT_DIPOLE.md</c> §1a).</para>
    /// </summary>
    public class WarplineController : GateRaceController
    {
        [Header("Warpline circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = WarplineCourseSource.DefaultLaps;

        protected override string ModeName => "Warpline";

        protected override RaceCourseSource CreateCourseSource() =>
            new WarplineCourseSource { Laps = laps };

        /// <summary>
        /// Regatta's gate steering (<see cref="GateRaceController.AICommitFromApproachPoint"/>, 2026-10-26): commit no earlier
        /// than the approach point, which then lies inside the commit radius; aim through the ring's CENTRE; re-attack after
        /// a missed mouth. With the shipped steering no AI Stoat finished any of Yash's 11 Warpline races (22 rivals, 2.6 rings
        /// a minute, stalled on ring 0): its approach point sat 180 u out against a 160 u commit, and on its 167 u turning
        /// circle (48 deg/s at 140 u/s) it circled 10-60 u off the 32-48 u mouths. Offline on those 11 courses
        /// (<c>Tools/Build/switchback_dolphin_sim.py --mode Warpline</c>, no warp): 0 of 11 finished before, 11 of 11 after.
        /// Steering alone still missed 4-7 mouths a race and took twice Yash's time: warped, the hull cannot line up from the
        /// base autopilot's 60 u let-go, so in Warpline it lets go of the warp 250 u out
        /// (<c>Resources/AIBoostPolicies/StoatDipoleConfig_Warpline</c>; with the warp modelled, all 15 of Yash's courses
        /// in 82-92 s with no missed mouth, 2026-10-27).
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;
    }
}
