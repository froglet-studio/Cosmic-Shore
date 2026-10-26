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

        /// <summary>
        /// Warpline flies Regatta's gate steering (<see cref="GateRaceController.AICommitFromApproachPoint"/>), as
        /// Switchback, Breakwater and Waystation do. The scene's 180 u approach lead lay outside its 160 u commit radius,
        /// and a Stoat warped to ~140 u/s turns at a fixed 48 deg/s (a 167 u circle): in Yash's 15 recorded races
        /// (2026-10-25..27) the AI Stoats crossed a ring's plane 10-180 u off its 26-48 u mouth and looped 3,000-7,800 u
        /// back round, finishing none of 30 races. Offline on those courses (<c>Tools/Build/switchback_dolphin_sim.py
        /// --mode Warpline</c>): none of 15 finished with the shipped steering; all 15 with this one and the warp let go
        /// 250 u out (<c>Resources/AIBoostPolicies/StoatDipoleConfig_Warpline</c>).
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new WarplineCourseSource { Laps = laps };
    }
}
