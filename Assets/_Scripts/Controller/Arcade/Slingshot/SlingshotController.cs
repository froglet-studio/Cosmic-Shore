using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slingshot - the Stoat-only circuit race (<c>Arcade/SLINGSHOT.md</c>). A closed loop of
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
    /// replicated press path (<c>StoatSlingExecutor.AutopilotSling</c>).</para>
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
    }
}
