using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Warpline - the MEERKAT's TIME race (<c>Arcade/WARPLINE.md</c>; the Stoat's until Yash's call of 2026-10-27, "Stoat
    /// will use Slingshot and Meerkat will use Warpline"). A closed loop of five switch rings is cut through the barren race
    /// cell and every pilot flies LAPS of it in order; the first domain whose lead runner threads the last gate of the last
    /// lap wins, scored on finish TIME (golf).
    ///
    /// <para><b>The mode is a question about the tunnel.</b> The Meerkat flies one-thumbed at a 60 u/s cruise; its only
    /// speed past that is its wormhole (<c>R_VesselActions/MEERKAT.md</c>): the right stick bends the reticle toward the next
    /// ring, LT shortens it, and RT forms the tunnel and dives in, rushing to 2.5 x the entry speed at the throat. Five rings
    /// a lap make every leg (~750 u) longer than one straight 600 u tunnel, so each leg asks how to dig it: one long
    /// tunnel and a turn, or a bent one onto the ring.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new ability, no cell, no scoring metric and no circuit generator -
    /// Slingshot's shape and Slingshot's cut (<see cref="SlingshotCourse"/>) with fewer, longer legs. The AI forms its
    /// tunnels through the replicated press path (<c>MeerkatSolitonExecutor.Autopilot</c>).</para>
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
        /// a missed mouth. Measured on the Stoat, which raced here until 2026-10-27: with the scene steering no AI Stoat
        /// finished any of Yash's 11 races (its 180 u approach point sat outside the 160 u commit). Burrow Run flies the
        /// Meerkat on the same steering.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;
    }
}
