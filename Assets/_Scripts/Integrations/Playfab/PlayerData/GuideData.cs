using System;

namespace CosmicShore.Integrations.PlayFab.PlayerModels
{
    /// <summary>
    /// guide Data
    /// Maps guide id and guide upgrade level
    /// </summary>
    [Serializable]
    public struct GuideData
    {
        public string guideId;
        public int upgradeLevel;

        public GuideData(string guideId, int upgradeLevel)
        {
            this.guideId = guideId;
            this.upgradeLevel = upgradeLevel;
        }
    }
}