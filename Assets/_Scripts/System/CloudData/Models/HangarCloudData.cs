using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists vessel unlock state and hangar preferences to UGS Cloud Save.
    /// Replaces the runtime-only SO_Vessel.isLocked that resets on app restart.
    ///
    /// JSON example:
    /// {
    ///   "UnlockedVessels": ["Squirrel", "Sparrow"],
    ///   "VesselPreferences": {
    ///     "Squirrel": { "LastUsedTicks": 638765000000000000, "Favorited": true },
    ///     "Sparrow":  { "LastUsedTicks": 638764000000000000, "Favorited": false }
    ///   },
    ///   "SelectedVessel": "Squirrel"
    /// }
    /// </summary>
    [Serializable]
    public class HangarCloudData
    {
        public List<string> UnlockedVessels = new();
        public Dictionary<string, VesselPreference> VesselPreferences = new();
        public string SelectedVessel = "";

        public bool IsVesselUnlocked(string vesselName)
        {
            return UnlockedVessels.Contains(vesselName);
        }

        public void UnlockVessel(string vesselName)
        {
            if (!UnlockedVessels.Contains(vesselName))
                UnlockedVessels.Add(vesselName);
        }

        public void LockVessel(string vesselName)
        {
            UnlockedVessels.Remove(vesselName);
        }

        public VesselPreference GetOrCreatePreference(string vesselName)
        {
            if (!VesselPreferences.TryGetValue(vesselName, out var pref))
            {
                pref = new VesselPreference();
                VesselPreferences[vesselName] = pref;
            }
            return pref;
        }
    }

    [Serializable]
    public class VesselPreference
    {
        public long LastUsedTicks;
        public bool Favorited;
    }
}
