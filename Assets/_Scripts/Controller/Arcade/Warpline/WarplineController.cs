using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
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
    /// lime and the hull flies down it up to 4× faster (Time scales the boost) — and a sink laid on the
    /// line to the next ring throws the hull out of the source, further on. The barren cell has nothing
    /// to skim, so warp is the only speed past cruise. Five rings a lap, so every leg is long enough to
    /// lay a pair and ride it; every corner asks which pole to pull, and how far.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new ability, no cell, no scoring metric and no
    /// circuit generator — Slingshot's shape and Slingshot's cut (<see cref="SlingshotCourse"/>) with
    /// fewer, longer legs. The autopilot lays its pairs through the replicated press path
    /// (<c>StoatDipoleExecutor.Autopilot</c>).</para>
    /// </summary>
    public class WarplineController : GateRaceController
    {
        [Header("Warpline circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = 2;

        protected override string ModeName => "Warpline";

        protected override int LapsPerRace => Mathf.Max(1, laps);

        public override int AuthoredGateTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetWarplineGateTarget()
                : EndConditionOverridesSO.DefaultWarplineGateTarget;
        }

        /// <summary>The Stoat's circuit (<see cref="SlingshotCourse.ForIntensity"/>) with the race's own ring
        /// count: target/laps rings, so the same base circle is cut into fewer, longer legs.</summary>
        protected override List<RaceGate> BuildCourse(int seed, int gateCount, float inner, float outer)
        {
            var settings = SlingshotCourse.ForIntensity(Intensity);
            settings.InnerRadius = inner;
            settings.OuterRadius = outer;
            settings.FirstGateDirection = Vector3.up;   // the equatorial spawn ring's pole

            // Round UP so a target that does not divide by the lap count still yields a whole circuit.
            settings.GateCount = Mathf.Max(3, Mathf.CeilToInt(gateCount / (float)LapsPerRace));
            return HeadlongCircuit.Generate(seed, settings);
        }
    }
}
