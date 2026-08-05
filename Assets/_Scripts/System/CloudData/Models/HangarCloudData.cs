using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Everything the player owns and has done with a vessel, in one record per vessel.
    /// Cloud key: <c>HANGAR_DATA</c>.
    ///
    /// This absorbs the former <c>VESSEL_STATS</c> key. Previously, answering "how much has
    /// this player flown the vessel they unlocked?" meant correlating two Cloud Save payloads
    /// client-side on a bare string key, with nothing guaranteeing the two agreed on the
    /// vessel-name spelling. See Docs/Analytics/DATA_ARCHITECTURE.md §3.2.
    ///
    /// Writers: <c>VesselUnlockSystem</c> (unlock), the menu vessel-selection controller
    /// (<see cref="SelectedVessel"/>), and <c>UGSStatsManager.ReportVesselTelemetry</c> (stats).
    ///
    /// JSON example:
    /// {
    ///   "SchemaVersion": 1,
    ///   "SelectedVessel": "Squirrel",
    ///   "PreferredVessel": "Squirrel",
    ///   "Vessels": {
    ///     "Squirrel": {
    ///       "Unlocked": true, "UnlockedUtcMs": 1784063404202, "LastUsedUtcMs": 1784930112000,
    ///       "GamesPlayed": 37, "FlightTimeSeconds": 4820.6,
    ///       "BestDriftTimeSeconds": 19.834, "BestBoostTimeSeconds": 32.292,
    ///       "TotalPrismsDamaged": 726,
    ///       "Counters": { "JoustsWon": 37, "PrismsStolen": 12193 }
    ///     }
    ///   }
    /// }
    /// </summary>
    [Serializable]
    public class HangarCloudData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Last vessel the player deliberately selected. PREFERENCE tier.</summary>
        public string SelectedVessel = "";

        /// <summary>
        /// The vessel with the most flight time - "most hours played", derived, never chosen.
        /// Recomputed by <see cref="RecomputePreferredVessel"/> whenever a vessel record changes.
        /// </summary>
        public string PreferredVessel = "";

        public Dictionary<string, VesselRecord> Vessels = new();

        /// <summary>
        /// Fetches (or creates) a vessel record. Blank names are rejected: <c>SO_Vessel.Name</c>
        /// is authored data and at least one asset ships blank, which is how the old flat
        /// <c>UnlockedVessels</c> list ended up containing an empty string.
        /// Returns null for a blank name - callers must null-check.
        /// </summary>
        public VesselRecord GetOrCreate(string vesselName)
        {
            if (string.IsNullOrWhiteSpace(vesselName))
                return null;

            if (!Vessels.TryGetValue(vesselName, out var record))
            {
                record = new VesselRecord();
                Vessels[vesselName] = record;
            }
            return record;
        }

        public bool IsVesselUnlocked(string vesselName) =>
            !string.IsNullOrWhiteSpace(vesselName) &&
            Vessels.TryGetValue(vesselName, out var r) &&
            r.Unlocked;

        public void UnlockVessel(string vesselName, long nowUtcMs)
        {
            var record = GetOrCreate(vesselName);
            if (record == null || record.Unlocked)
                return;

            record.Unlocked = true;
            record.UnlockedUtcMs = nowUtcMs;
        }

        public void LockVessel(string vesselName)
        {
            if (string.IsNullOrWhiteSpace(vesselName)) return;
            if (Vessels.TryGetValue(vesselName, out var record))
            {
                record.Unlocked = false;
                record.UnlockedUtcMs = 0;
            }
        }

        public IEnumerable<string> UnlockedVesselNames()
        {
            foreach (var kvp in Vessels)
                if (kvp.Value != null && kvp.Value.Unlocked)
                    yield return kvp.Key;
        }

        public int UnlockedVesselCount()
        {
            int count = 0;
            foreach (var kvp in Vessels)
                if (kvp.Value != null && kvp.Value.Unlocked)
                    count++;
            return count;
        }

        /// <summary>
        /// Recomputes <see cref="PreferredVessel"/> as the most-flown vessel. Ties break on
        /// games played, then on most recently used, so the value is stable and useful even
        /// before flight time has accumulated.
        /// </summary>
        public void RecomputePreferredVessel()
        {
            string best = "";
            float bestFlight = -1f;
            int bestGames = -1;
            long bestUsed = -1;

            foreach (var kvp in Vessels)
            {
                var r = kvp.Value;
                if (r == null) continue;

                bool sameFlight = Approximately(r.FlightTimeSeconds, bestFlight);
                bool better = r.FlightTimeSeconds > bestFlight
                    || (sameFlight && r.GamesPlayed > bestGames)
                    || (sameFlight && r.GamesPlayed == bestGames && r.LastUsedUtcMs > bestUsed);

                if (!better) continue;

                best = kvp.Key;
                bestFlight = r.FlightTimeSeconds;
                bestGames = r.GamesPlayed;
                bestUsed = r.LastUsedUtcMs;
            }

            PreferredVessel = best;
        }

        // Local epsilon compare - this model is plain data and deliberately has no UnityEngine dependency.
        static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;
    }

    /// <summary>
    /// One vessel: ownership (PROGRESSION tier) plus lifetime play stats (TELEMETRY tier).
    /// </summary>
    [Serializable]
    public class VesselRecord
    {
        // ── Ownership ──
        public bool Unlocked;
        public long UnlockedUtcMs;
        public long LastUsedUtcMs;

        // ── Lifetime stats ──
        public int GamesPlayed;

        /// <summary>
        /// Time at the stick in this vessel, pause and background excluded. Drives
        /// <see cref="HangarCloudData.PreferredVessel"/>. Written from the flight clock at game end.
        /// </summary>
        public float FlightTimeSeconds;

        public float BestDriftTimeSeconds;
        public float BestBoostTimeSeconds;
        public int TotalPrismsDamaged;

        /// <summary>
        /// Vessel-specific counters, keyed by stat name (e.g. "PrismBlocksShot").
        /// Deliberately free-form: this is the one part of the old schema that was already
        /// right, and it is why Sparrow and Squirrel can carry different stats with no
        /// schema change. Do not formalise it.
        /// </summary>
        public Dictionary<string, int> Counters = new();

        public void IncrementCounter(string key, int amount)
        {
            if (string.IsNullOrEmpty(key)) return;
            Counters.TryGetValue(key, out int current);
            Counters[key] = current + amount;
        }

        /// <summary>Raises a counter to <paramref name="value"/> only if it is a new best.</summary>
        public void RaiseCounterToBest(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;
            Counters.TryGetValue(key, out int current);
            if (value > current)
                Counters[key] = value;
        }
    }
}
