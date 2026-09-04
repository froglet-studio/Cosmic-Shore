using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Per-biome up/down thresholds that drive <see cref="Cell"/>'s phase transitions.
    ///
    /// VOLUME IS THE SPINE (locked invariant - CLAUDE.md ▸ Ecosystem Design Principles):
    /// the phase ladder climbs on the cell's live per-domain prism VOLUME
    /// (<c>Cell.LiveVolume</c> - every prism contributes: trail, flora, fauna bodies).
    /// With the 3-phase ladder (Calm → Restless → Frenzy) there are two boundaries to
    /// author: where fauna start hunting (Restless) and the frenzy ceiling (Frenzy).
    /// Each has an UpEnter (climb threshold) and a DownExit (fall threshold); the gap
    /// is the hysteresis band - values in the band leave the phase unchanged.
    ///
    /// The legacy COUNT fields remain as the rare perf BACKSTOP the invariant allows:
    /// a runaway prism COUNT (many tiny prisms - colliders/instances, not mass) forces
    /// Frenzy so growth freezes, even while volume is below the volume ladder.
    ///
    /// Authoring: biomes whose volume fields are zero (every pre-volume asset) derive
    /// them from the count fields × <see cref="NominalPrismVolume"/>, keeping each
    /// biome proportional until it is retuned in-editor.
    /// </summary>
    [Serializable]
    public struct CellPhaseThresholds
    {
        /// <summary>
        /// Volume of one nominal prism (the 4×4×1 flora leaf - lossyScale product).
        /// The single conversion anchor for deriving volume-scale values from legacy
        /// count-scale ones (phase thresholds, FaunaFoodFloor). Retune per-biome in
        /// the editor rather than changing this constant.
        /// </summary>
        public const float NominalPrismVolume = 16f;

        [Tooltip("Count BACKSTOP ladder (perf): a runaway prism COUNT forces Frenzy regardless of volume. " +
                 "Also the derivation source (×16) for biomes whose volume fields below are still zero.")]
        [Min(0)] public int RestlessEnter;
        [Min(0)] public int RestlessExit;
        [Min(0)] public int FrenzyEnter;
        [Min(0)] public int FrenzyExit;

        [Tooltip("VOLUME ladder (the spine). Zero = derive from the count fields × NominalPrismVolume (16).")]
        [Min(0)] public float RestlessEnterVolume;
        [Min(0)] public float RestlessExitVolume;
        [Min(0)] public float FrenzyEnterVolume;
        [Min(0)] public float FrenzyExitVolume;

        public static CellPhaseThresholds Default => new CellPhaseThresholds
        {
            RestlessEnter = 8000, RestlessExit = 7500,
            FrenzyEnter = 15000, FrenzyExit = 14000,
        }.WithDerivedVolumeScale();

        public float GetEnterVolume(CellPhase phase) => phase switch
        {
            CellPhase.Calm => 0f,
            CellPhase.Restless => RestlessEnterVolume,
            CellPhase.Frenzy => FrenzyEnterVolume,
            _ => float.MaxValue,
        };

        public float GetExitVolume(CellPhase phase) => phase switch
        {
            CellPhase.Calm => 0f,
            CellPhase.Restless => RestlessExitVolume,
            CellPhase.Frenzy => FrenzyExitVolume,
            _ => 0f,
        };

        /// <summary>
        /// True when every threshold is zero - the deserialized state of a CellConfig
        /// asset that existed before <c>PhaseThresholds</c> was added (or one whose
        /// legacy 5-phase fields were dropped by the 3-phase migration). Lets callers
        /// substitute <see cref="Default"/> instead of letting every cell collapse
        /// straight to Frenzy because the measure >= 0 satisfies every UpEnter.
        /// </summary>
        public bool IsAllZero =>
            RestlessEnter == 0 && RestlessExit == 0 &&
            FrenzyEnter == 0 && FrenzyExit == 0 &&
            RestlessEnterVolume == 0f && RestlessExitVolume == 0f &&
            FrenzyEnterVolume == 0f && FrenzyExitVolume == 0f;

        /// <summary>
        /// Returns this table with any zeroed volume field derived from its count
        /// counterpart × <see cref="NominalPrismVolume"/> - the migration path for
        /// every CellConfig authored before volume became the spine. A biome with
        /// explicit volume values is returned untouched.
        /// </summary>
        public CellPhaseThresholds WithDerivedVolumeScale()
        {
            var t = this;
            if (t.RestlessEnterVolume <= 0f) t.RestlessEnterVolume = t.RestlessEnter * NominalPrismVolume;
            if (t.RestlessExitVolume <= 0f) t.RestlessExitVolume = t.RestlessExit * NominalPrismVolume;
            if (t.FrenzyEnterVolume <= 0f) t.FrenzyEnterVolume = t.FrenzyEnter * NominalPrismVolume;
            if (t.FrenzyExitVolume <= 0f) t.FrenzyExitVolume = t.FrenzyExit * NominalPrismVolume;
            return t;
        }
    }

    public static class CellPhaseRules
    {
        // Ordered ascending so an int index walks Calm → Frenzy.
        static readonly CellPhase[] Order =
        {
            CellPhase.Calm,
            CellPhase.Restless,
            CellPhase.Frenzy,
        };

        /// <summary>
        /// Resolves a new phase from the cell's live VOLUME (the spine), its live
        /// prism COUNT (the perf backstop), the current phase, and a per-biome
        /// threshold table (volume fields already derived - see
        /// <see cref="CellPhaseThresholds.WithDerivedVolumeScale"/>). The volume
        /// ladder climbs while volume meets the next phase's enter threshold and
        /// descends below the current phase's exit threshold (asymmetric thresholds
        /// produce hysteresis; multi-step transitions resolve in one call). The count
        /// backstop then forces Frenzy - with its own hysteresis on the count exit -
        /// when sheer prism count is a perf hazard even at low volume.
        /// </summary>
        public static CellPhase Compute(float volume, int count, CellPhase current, in CellPhaseThresholds thresholds)
        {
            int idx = IndexOf(current);
            if (idx < 0) idx = 0;

            while (idx + 1 < Order.Length &&
                   volume >= thresholds.GetEnterVolume(Order[idx + 1]))
                idx++;

            while (idx > 0 &&
                   volume < thresholds.GetExitVolume(Order[idx]))
                idx--;

            var phase = Order[idx];

            // Count backstop: many tiny prisms are a collider/instance load problem
            // even when their summed volume is small. FrenzyEnter/FrenzyExit (counts)
            // force and hold Frenzy until the count falls back through the band.
            if (thresholds.FrenzyEnter > 0 &&
                (count >= thresholds.FrenzyEnter ||
                 (current == CellPhase.Frenzy && count >= thresholds.FrenzyExit)))
                return CellPhase.Frenzy;

            return phase;
        }

        static int IndexOf(CellPhase phase)
        {
            for (int i = 0; i < Order.Length; i++)
                if (Order[i] == phase) return i;
            return -1;
        }
    }
}
