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
        [SerializeField, Min(1)] int laps = 3;

        protected override string ModeName => "Headlong";

        protected override int LapsPerRace => Mathf.Max(1, laps);

        public override int AuthoredGateTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetHeadlongGateTarget()
                : EndConditionOverridesSO.DefaultHeadlongGateTarget;
        }

        /// <summary>
        /// A closed circuit, and it CANNOT fail - the generator relaxes toward a regular octagon
        /// rather than walking and backtracking, so there is no back-off ladder here and no seed
        /// that produces a course nobody can fly. See <see cref="HeadlongCircuit"/>.
        /// </summary>
        protected override List<RaceGate> BuildCourse(int seed, int gateCount, float inner, float outer)
        {
            var settings = HeadlongCircuitSettings.ForIntensity(Intensity);
            settings.InnerRadius = inner;
            settings.OuterRadius = outer;
            settings.FirstGateDirection = Vector3.up;   // the equatorial spawn ring's pole

            // The authored target is the RACE length; the circuit only needs one lap of rings.
            // Round UP so a target that does not divide by the lap count still yields a whole
            // circuit, and let RaceLength be the honest authority afterwards.
            int perLap = Mathf.Max(3, Mathf.CeilToInt(gateCount / (float)LapsPerRace));
            settings.GateCount = perLap;

            return HeadlongCircuit.Generate(seed, settings);
        }
    }
}
