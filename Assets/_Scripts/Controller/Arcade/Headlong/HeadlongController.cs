using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Headlong - the Rhino-only circuit race. A closed loop of switch rings is cut through the
    /// cell and every pilot flies LAPS of it in order; the first domain whose lead runner
    /// threads the last gate of the last lap wins.
    ///
    /// <para><b>The mode is a question about one gesture.</b> The Rhino's ramp boost engages only
    /// while the pilot holds full throttle and near-zero stick, and it takes 6.1 s to wind up to
    /// 910 u/s. Every corner on this circuit is therefore a decision: thread it inside the
    /// FLAT-OUT radius (~410 u - see <see cref="HeadlongCircuitSettings.FlatOutRadius"/>) and
    /// keep the boost, or turn properly, lose it, and pay six seconds. Intensity is how many
    /// corners let you choose: at level 1 every corner has half again the room you need, and at
    /// level 4 several of them cannot be held at speed at all.</para>
    ///
    /// <para><b>Why the Rhino and not another hull.</b> Its turn radius CONVERGES with speed
    /// (<c>RotationThrottleScaler</c> 0.4, so <c>R -> 180/(pi*r)</c> = 143 u), which is unique in
    /// the fleet - every other vessel's turning circle grows without bound. A circuit whose
    /// corners are cut at a fixed radius is therefore a course only this vessel gets BETTER at as
    /// it accelerates, which is the thing worth showing. See R_VesselActions/RHINO_RAMP_BOOST.md.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new weapon, no new ability, no cell of its own,
    /// and nothing bespoke about laps - a lap is <see cref="GateRaceController.RingIndexFor"/>
    /// wrapping an index the race was already keeping. The Rhino's shipped kit supplies the
    /// racing; its sword is the interference.</para>
    /// </summary>
    public class HeadlongController : GateRaceController
    {
        [Header("Headlong circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = HeadlongCourseSource.DefaultLaps;

        protected override string ModeName => "Headlong";

        protected override RaceCourseSource CreateCourseSource() =>
            new HeadlongCourseSource { Laps = laps };

        /// <summary>
        /// Headlong flies Regatta's gate steering (<see cref="GateRaceController.AICommitFromApproachPoint"/>), as every
        /// other gate race already does: commit no earlier than the approach point, which then always lies inside the
        /// commit radius; aim through the ring's CENTRE from where the pilot is; re-attack at once after a missed mouth.
        /// With the shipped steering (the scene's 300 / 260 / 220, the switch off) the offline Rhino model stalled on 7
        /// of the 12 Headlong courses whatever its drive (Docs/Reports/RHINO_HEADLONG_I4_STALLS.md): on a leg over 650 u
        /// the 300 u approach point sits outside the 260 u commit radius and the AI circles it, and after a missed I3-I4
        /// mouth the far-side point lies on the pilot's own side, so it loiters there and the orbit break-off never
        /// fires (each swap between the two points reads as a new target). Switched on, the same drive finishes all 12
        /// (<c>python3 Tools/Build/rhino_drive_sim.py --headlong</c>). A model, not the game: playtest before shipping.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;
    }
}
