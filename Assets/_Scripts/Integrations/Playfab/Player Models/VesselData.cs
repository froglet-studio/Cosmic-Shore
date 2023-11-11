using System;

namespace CosmicShore._Core.Playfab_Models.Player_Models
{
    /// <summary>
    /// Vessel Data
    /// Maps vessel id and vessel upgrade level
    /// </summary>
    [Serializable]
    public struct VesselData
    {
        public string vesselId;
        public int upgradeLevel;

        public VesselData(string vesselId, int upgradeLevel)
        {
            this.vesselId = vesselId;
            this.upgradeLevel = upgradeLevel;
        }
    }
}