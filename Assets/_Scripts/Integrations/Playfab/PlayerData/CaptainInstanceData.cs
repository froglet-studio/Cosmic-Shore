using System;

namespace CosmicShore.Integrations.PlayFab.PlayerModels
{
    /// <summary>
    /// Data container that tracks how the player's captain instance has changed from the vanilla version - e.g how many times the captain has been upgrade
    /// </summary>
    [Serializable]
    public struct CaptainInstanceData
    {
        public string captainId;
        public int upgradeLevel; // Captains current upgrade level

        public CaptainInstanceData(string captainId, int upgradeLevel)
        {
            this.captainId = captainId;
            this.upgradeLevel = upgradeLevel;
        }
    }
}