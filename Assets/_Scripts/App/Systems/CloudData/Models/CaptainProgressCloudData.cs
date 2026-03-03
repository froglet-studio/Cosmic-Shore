using System;
using System.Collections.Generic;

namespace CosmicShore.App.Systems.CloudData.Models
{
    /// <summary>
    /// Persists captain progression (XP, level, unlock/encounter state) to UGS Cloud Save.
    /// Replaces the disabled PlayFab CaptainManager + XpHandler system.
    ///
    /// JSON example:
    /// {
    ///   "Captains": {
    ///     "Ava_Squirrel": {
    ///       "XP": 250,
    ///       "Level": 3,
    ///       "Unlocked": true,
    ///       "Encountered": true,
    ///       "UpgradeCount": 2
    ///     },
    ///     "Rex_Sparrow": {
    ///       "XP": 80,
    ///       "Level": 1,
    ///       "Unlocked": true,
    ///       "Encountered": true,
    ///       "UpgradeCount": 0
    ///     },
    ///     "Kai_Manta": {
    ///       "XP": 0,
    ///       "Level": 0,
    ///       "Unlocked": false,
    ///       "Encountered": true,
    ///       "UpgradeCount": 0
    ///     }
    ///   }
    /// }
    /// </summary>
    [Serializable]
    public class CaptainProgressCloudData
    {
        public Dictionary<string, CaptainState> Captains = new();

        public CaptainState GetOrCreate(string captainName)
        {
            if (!Captains.TryGetValue(captainName, out var state))
            {
                state = new CaptainState();
                Captains[captainName] = state;
            }
            return state;
        }

        public bool IsCaptainUnlocked(string captainName)
        {
            return Captains.TryGetValue(captainName, out var s) && s.Unlocked;
        }

        public bool IsCaptainEncountered(string captainName)
        {
            return Captains.TryGetValue(captainName, out var s) && s.Encountered;
        }

        public void EncounterCaptain(string captainName)
        {
            GetOrCreate(captainName).Encountered = true;
        }

        public void UnlockCaptain(string captainName)
        {
            var state = GetOrCreate(captainName);
            state.Unlocked = true;
            state.Encountered = true;
            if (state.Level < 1)
                state.Level = 1;
        }

        public int AddXP(string captainName, int amount)
        {
            var state = GetOrCreate(captainName);
            state.XP += amount;
            return state.XP;
        }
    }

    [Serializable]
    public class CaptainState
    {
        public int XP;
        public int Level;
        public bool Unlocked;
        public bool Encountered;
        public int UpgradeCount;
    }
}
