using System;
using CosmicShore.Data;
using CosmicShore.Engine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Per-biome up/down thresholds that drive <see cref="Cell"/>'s phase transitions.
    /// With the 3-phase ladder (Calm → Restless → Frenzy) there are just two boundaries
    /// to author: where fauna start hunting (Restless) and where the cell hits the frenzy
    /// ceiling (Frenzy). Each has an UpEnter (climb threshold from the previous phase) and
    /// a DownExit (fall threshold back to the previous phase). The gap between them is the
    /// hysteresis band; LiveBlockCount values in the band leave the phase unchanged.
    /// Calm has no entry threshold — it's the starting state at zero prisms.
    /// </summary>
    [Serializable]
    public struct CellPhaseThresholds
    {
        [Min(0)] public int RestlessEnter;
        [Min(0)] public int RestlessExit;
        [Min(0)] public int FrenzyEnter;
        [Min(0)] public int FrenzyExit;

        public static CellPhaseThresholds Default => new()
        {
            RestlessEnter = 8000, RestlessExit = 7500,
            FrenzyEnter = 15000, FrenzyExit = 14000,
        };

        public int GetEnterThreshold(CellPhase phase) => phase switch
        {
            CellPhase.Calm => 0,
            CellPhase.Restless => RestlessEnter,
            CellPhase.Frenzy => FrenzyEnter,
            _ => int.MaxValue,
        };

        public int GetExitThreshold(CellPhase phase) => phase switch
        {
            CellPhase.Calm => 0,
            CellPhase.Restless => RestlessExit,
            CellPhase.Frenzy => FrenzyExit,
            _ => 0,
        };

        /// <summary>
        /// True when every threshold is zero — the deserialized state of a CellConfig
        /// asset that existed before <c>PhaseThresholds</c> was added (or one whose
        /// legacy 5-phase fields were dropped by the 3-phase migration). Lets callers
        /// substitute <see cref="Default"/> instead of letting every cell collapse
        /// straight to Frenzy because count >= 0 satisfies every UpEnter.
        /// </summary>
        public bool IsAllZero =>
            RestlessEnter == 0 && RestlessExit == 0 &&
            FrenzyEnter == 0 && FrenzyExit == 0;
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
        /// Resolves a new phase from a live block count, the current phase, and a
        /// per-biome threshold table. Climbs while the count meets the next phase's
        /// UpEnter, then descends while the count is below the current phase's
        /// DownExit. The asymmetric thresholds produce hysteresis at every boundary.
        /// Multi-step transitions (e.g., a count spike that justifies skipping a
        /// phase) resolve in one call.
        /// </summary>
        public static CellPhase Compute(int count, CellPhase current, in CellPhaseThresholds thresholds)
        {
            int idx = IndexOf(current);
            if (idx < 0) idx = 0;

            while (idx + 1 < Order.Length &&
                   count >= thresholds.GetEnterThreshold(Order[idx + 1]))
                idx++;

            while (idx > 0 &&
                   count < thresholds.GetExitThreshold(Order[idx]))
                idx--;

            return Order[idx];
        }

        static int IndexOf(CellPhase phase)
        {
            for (int i = 0; i < Order.Length; i++)
                if (Order[i] == phase) return i;
            return -1;
        }
    }
}
