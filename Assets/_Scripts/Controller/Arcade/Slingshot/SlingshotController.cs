using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slingshot - the Stoat's TIME race (<c>Arcade/SLINGSHOT.md</c>; Yash, 2026-10-27: "Stoat will use Slingshot and
    /// Meerkat will use Warpline"; Warpline was the Stoat's until then). A closed loop of
    /// switch rings is cut through the cell and every pilot flies LAPS of it in order; the first
    /// domain whose lead runner threads the last gate of the last lap wins, scored on finish TIME
    /// (golf).
    ///
    /// <para><b>The mode is a question about where to fall.</b> The Stoat cruises at 60 u/s and
    /// the barren race cell has nothing to skim, so the only speed past cruise is the pull of the
    /// attractor–repulsor wormhole pair it slings on its triggers (LT: attractor left, RT:
    /// attractor right; hold = size): up to 90 u/s more, toward the attractor and off the
    /// repulsor. A sling is a THROW - it drags the hull off its line - so every ring asks where
    /// the pull will have put you when it arrives. The pull moves only the Stoat that slung it
    /// (a vessel may not move an opposing vessel). See <see cref="SlingshotCourse"/>.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new ability, no cell of its own, no new
    /// scoring metric and no circuit generator - Redline's shape on Headlong's solver, with
    /// <see cref="SlingshotCourse"/> supplying the Stoat's cut. The autopilot slings through the
    /// replicated press path (<c>StoatSlingExecutor.AutopilotSling</c>) in Time mode, and lays dipoles
    /// (<c>StoatDipoleExecutor.Autopilot</c>) in Mass mode; it switches itself (<c>StoatModeExecutor</c>).</para>
    /// </summary>
    public class SlingshotController : GateRaceController
    {
        [Header("Slingshot circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = SlingshotCourseSource.DefaultLaps;

        protected override string ModeName => "Slingshot";

        protected override RaceCourseSource CreateCourseSource() =>
            new SlingshotCourseSource { Laps = laps };

        /// <summary>
        /// Regatta's gate steering (<see cref="GateRaceController.AICommitFromApproachPoint"/>), as Warpline flew it for the
        /// Stoat: the scene's 180 u approach point lay outside its 160 u commit radius, Warpline's measured defect (no AI
        /// Stoat finished any of Yash's 11 Warpline races on the scene steering). The Stoat's Warpline-learned pair and sling
        /// numbers start its Slingshot variants (<c>Resources/AIBoostPolicies/StoatDipoleConfig_Slingshot</c>,
        /// <c>StoatSlingConfig_Slingshot</c>, the warp let go 250 u out) until its own Slingshot races replace them.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;
    }
}
