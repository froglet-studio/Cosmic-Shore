using System;

namespace _Scripts._Core.Playfab_Models.Player_Models
{
    /// <summary>
    /// Vessel Data
    /// Maps vessel id and vessel upgrade level
    /// </summary>
    [Serializable]
    public class VesselData
    {
        public string vesselId;
        public int upgradeLevel;
    }
}