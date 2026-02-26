using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Ship
{
    /// <summary>
    /// Top-level container persisted under UGSKeys.VesselStats in Cloud Save.
    /// Maps vessel type name → that vessel's lifetime stats.
    /// </summary>
    [Serializable]
    public class VesselStatsCloudData
    {
        public Dictionary<string, VesselLifetimeStats> Vessels = new();

        public VesselLifetimeStats GetOrCreate(string vesselType)
        {
            if (!Vessels.TryGetValue(vesselType, out var stats))
            {
                stats = new VesselLifetimeStats();
                Vessels[vesselType] = stats;
            }
            return stats;
        }
    }

    /// <summary>
    /// Per-vessel lifetime stats. Contains common stats tracked by VesselTelemetry
    /// plus a dictionary for vessel-specific custom counters.
    /// </summary>
    [Serializable]
    public class VesselLifetimeStats
    {
        // ── Common (all vessels) ──
        public float BestDriftTime;
        public float BestBoostTime;
        public int   TotalPrismsDamaged;
        public int   GamesPlayed;

        // ── Vessel-specific counters ──
        // Key = stat name (e.g. "PrismBlocksShot"), Value = lifetime total
        public Dictionary<string, int> Counters = new();

        public void IncrementCounter(string key, int amount)
        {
            Counters.TryGetValue(key, out int current);
            Counters[key] = current + amount;
        }
    }
}
