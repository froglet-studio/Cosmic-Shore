using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Burrow Run - the Meerkat's TIME race (<c>Arcade/BURROWRUN.md</c>), the Vessel Studio's Meerkat race
    /// (<c>Docs/Studios/VesselStudio/meerkat.html</c>) in the game: a closed loop of eight switch rings cut through the
    /// barren race cell, flown in two laps; the first domain whose lead runner threads the last gate of the last lap wins,
    /// scored on finish TIME (golf).
    ///
    /// <para><b>The mode is a question about the tunnel.</b> The Meerkat flies one-thumbed at a 60 u/s cruise; its only
    /// speed past that is its wormhole (<c>R_VesselActions/MEERKAT.md</c>): the reticle on its nose is the tunnel RT would
    /// form, the right stick bends it toward the next ring, LT shortens it, and the ride rushes to 2.5 x the entry speed at
    /// the throat and comes out on the tunnel's last heading. Every leg asks how far to bend, and how long a tunnel the
    /// ring leaves room for.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new ability, no cell, no scoring metric and no circuit generator -
    /// Warpline's shape and Slingshot's cut (<see cref="SlingshotCourse"/>), the studio's eight-ring circle. The AI forms
    /// its tunnels through the replicated press path (<c>MeerkatWormholeExecutor.Autopilot</c>).</para>
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
