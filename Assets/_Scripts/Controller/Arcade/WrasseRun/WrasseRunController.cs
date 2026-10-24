using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wrasse Run - the Wrasse and Grouper's circuit race (Garrett, 2026-10-20: "make a wrasse and grouper racing game based
    /// around the wrasse collecting rings and trying to favor time crystals (among others) to give the pilot the boost
    /// needed to get to the next ring collector and align the wrasse to collect them"). A closed loop of switch rings is cut
    /// through the cell and every crew flies LAPS of it in order; the first domain whose lead runner threads the last gate
    /// of the last lap wins, on finish TIME (golf).
    ///
    /// <para><b>The Wrasse collects the rings.</b> A ring counts only when the navigator's kite threads it
    /// (<see cref="KiteNavigator"/> is the hull's <see cref="IRingThreadProbe"/>): the Grouper flies the hull so the next
    /// ring lies in the kite's plane and on its board, and the Wrasse swings the kite through the mouth. The hull flying
    /// through on its own does nothing. A hull with no kite seated (an AI crew: the seat runs only on the local human's
    /// hull) threads with the hull, so an all-AI domain can still finish.</para>
    ///
    /// <para><b>Time is the fuel.</b> The Grouper cruises at 60 u/s; its boosts come from the Wrasse's Time loops (each
    /// saved boost is 1.5 s at ×5 throttle, 260 u/s). While a ring is lit, Time motes come more often among the others and
    /// the AI squad mate prefers Time loops (KiteNavigatorConfigSO's race fields).</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no ability, no cell, no metric and no circuit generator - Thresher Time's
    /// shape on the shared <see cref="HeadlongCircuit"/>, with <see cref="WrasseRunCourse"/> supplying the Wrasse's cut.
    /// See <c>Assets/_Scripts/Controller/Arcade/WRASSERUN.md</c>.</para>
    /// </summary>
    public class WrasseRunController : GateRaceController
    {
        [Header("Wrasse Run circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = WrasseRunCourseSource.DefaultLaps;

        protected override string ModeName => "Wrasse Run";

        /// <summary>The kite collects the rings; the hull only carries it to them (an AI hull with no kite still threads).</summary>
        protected override bool HullThreadsWhileProbeLive => false;

        /// <summary>The Wrasse's circuit (<see cref="WrasseRunCourseSource"/>): Headlong's solver,
        /// <see cref="WrasseRunCourse.ForIntensity"/>'s settings, target/laps rings per lap.</summary>
        protected override RaceCourseSource CreateCourseSource() =>
            new WrasseRunCourseSource { Laps = laps };
    }
}
