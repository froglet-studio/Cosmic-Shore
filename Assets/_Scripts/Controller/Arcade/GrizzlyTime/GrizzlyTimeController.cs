using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Time - the Grizzly-only circuit race. A closed loop of switch rings is cut through
    /// the cell and every pilot flies LAPS of it in order; the first domain whose lead runner
    /// clears the last gate of the last lap wins, scored on finish TIME (golf).
    ///
    /// <para><b>The mode is a question about riding your own blasts.</b> The Grizzly cruises at
    /// 50 u/s on a 30 u turning circle - the slowest and tightest hull in the fleet - and
    /// everything past cruise comes from its trigger bombs: fire, freeze, detonate, and a Grizzly
    /// inside its own blast is launched AWAY FROM THE BOMB, a world-space push up to the launch's
    /// own 300 u/s ceiling (about 350 u/s with cruise, on a 211 u circle while it lasts; a full
    /// launch carries about 300 u). So where you stand when you blow a bomb is how you steer.</para>
    ///
    /// <para><b>The powder kegs</b> (2026-11-11, <see cref="GrizzlyTimeKegs"/>, the partial
    /// GrizzlyTimeController.Kegs.cs, Docs/GrizzlyTime/POWDER_KEGS_SPEC.md) give that steering a
    /// target: a keg beside every ring. Freeze a bomb in it, get round to its far side, blow it -
    /// the keg bursts, the gate is yours, and the blast throws you down the next leg. The ring
    /// mouth still clears the gate. I2 marks each keg's exit lane with chalk (the bank shot), I3
    /// shields the core (strip it from range, then blow it), I4 tightens the credit and the regrow.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new weapon, no new ability, no change to the
    /// vessel (CLAUDE.md, "A game mode never changes the vessel"), no new scoring metric and no
    /// circuit generator - Redline's shape on Headlong's solver, with <see cref="GrizzlyTimeCourse"/>
    /// supplying the Grizzly's cut. Rush is a short burst (about 75-80 u), Dig In is a hard stop,
    /// and a blast that catches a rival silences it for 8 s - a silenced pilot cannot open kegs,
    /// so it threads the mouths until it recovers (the fleet's silence rule, SILENCE.md).</para>
    /// </summary>
    public partial class GrizzlyTimeController : GateRaceController
    {
        [Header("Grizzly Time circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = GrizzlyTimeCourseSource.DefaultLaps;

        protected override string ModeName => "Grizzly Time";

        /// <summary>The AI commits to a ring from its approach point (Regatta's steering): the scene's approach lead (420)
        /// sits outside its commit distance (380), which without this is the orbit shape (an AI circling a ring it
        /// cannot quite line up). Redline went from 10/24 to 24/24 offline finishes on this one switch.</summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new GrizzlyTimeCourseSource { Laps = laps };
    }
}
