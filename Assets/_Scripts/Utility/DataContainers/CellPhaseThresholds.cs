using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Per-biome up/down thresholds that drive <see cref="Cell"/>'s phase transitions.
    /// Each phase has an UpEnter (climb threshold from the previous phase) and a DownExit
    /// (fall threshold back to the previous phase). The gap between them is the
    /// hysteresis band; LiveBlockCount values in the band leave the phase unchanged.
    /// Sprout has no entry threshold — it's the starting state at zero prisms.
    /// </summary>
    [Serializable]
    public struct CellPhaseThresholds
    {
        [Min(0)] public int QuietEnter;
        [Min(0)] public int QuietExit;
        [Min(0)] public int SettledEnter;
        [Min(0)] public int SettledExit;
        [Min(0)] public int RestlessEnter;
        [Min(0)] public int RestlessExit;
        [Min(0)] public int FrozenEnter;
        [Min(0)] public int FrozenExit;
        [Min(0)] public int RabidEnter;
        [Min(0)] public int RabidExit;

        public static CellPhaseThresholds Default => new()
        {
            QuietEnter = 1000, QuietExit = 800,
            SettledEnter = 4000, SettledExit = 3500,
            RestlessEnter = 8000, RestlessExit = 7500,
            FrozenEnter = 10000, FrozenExit = 9500,
            RabidEnter = 15000, RabidExit = 14000,
        };

        public int GetEnterThreshold(CellPhase phase) => phase switch
        {
            CellPhase.Sprout => 0,
            CellPhase.Quiet => QuietEnter,
            CellPhase.Settled => SettledEnter,
            CellPhase.Restless => RestlessEnter,
            CellPhase.Frozen => FrozenEnter,
            CellPhase.Rabid => RabidEnter,
            _ => int.MaxValue,
        };

        public int GetExitThreshold(CellPhase phase) => phase switch
        {
            CellPhase.Sprout => 0,
            CellPhase.Quiet => QuietExit,
            CellPhase.Settled => SettledExit,
            CellPhase.Restless => RestlessExit,
            CellPhase.Frozen => FrozenExit,
            CellPhase.Rabid => RabidExit,
            _ => 0,
        };

        /// <summary>
        /// True when every threshold is zero — the deserialized state of a CellConfig
        /// asset that existed before <c>PhaseThresholds</c> was added. Lets callers
        /// substitute <see cref="Default"/> instead of letting every cell collapse
        /// straight to Rabid because count >= 0 satisfies every UpEnter.
        /// </summary>
        public bool IsAllZero =>
            QuietEnter == 0 && QuietExit == 0 &&
            SettledEnter == 0 && SettledExit == 0 &&
            RestlessEnter == 0 && RestlessExit == 0 &&
            FrozenEnter == 0 && FrozenExit == 0 &&
            RabidEnter == 0 && RabidExit == 0;
    }

    public static class CellPhaseRules
    {
        // Ordered ascending so an int index walks Sprout → Rabid.
        static readonly CellPhase[] Order =
        {
            CellPhase.Sprout,
            CellPhase.Quiet,
            CellPhase.Settled,
            CellPhase.Restless,
            CellPhase.Frozen,
            CellPhase.Rabid,
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
