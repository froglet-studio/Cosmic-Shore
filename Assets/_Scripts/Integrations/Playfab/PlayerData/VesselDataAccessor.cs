using System.Collections.Generic;
using CosmicShore.Integrations.Enums;

namespace CosmicShore.Integrations.Playfab.Player_Models
{
    /// <summary>
    /// Vessel Data Accessor
    /// It's very similar to Leaderboard Data Accessor
    /// TODO: we could further abstract the method in the future if there more use cases
    /// </summary>
    public class VesselDataAccessor
    {
        private static readonly string VesselDataFileName = "VesselData.data";

        static Dictionary<VesselLevel, List<VesselData>> VesselUpgradeLevels;

        /// <summary>
        /// Save Vessel Data
        /// </summary>
        /// <param name="shipTypes">Ship Types</param>
        /// <param name="vesselDataList">Vessel Data List</param>
        public void Save(VesselLevel vesselLevel, List<VesselData> vesselDataList)
        {
            VesselUpgradeLevels??= Load();

            VesselUpgradeLevels[vesselLevel] = vesselDataList;

            DataAccessor dataAccessor = new(VesselDataFileName);
            dataAccessor.Save(VesselUpgradeLevels);
        }

        /// <summary>
        /// Load Vessel Data
        /// </summary>
        /// <returns>Dictionary of Ship Types and Vessel Data List</returns>
        public Dictionary<VesselLevel, List<VesselData>> Load()
        {
            DataAccessor dataAccessor = new(VesselDataFileName);
            return dataAccessor.Load<Dictionary<VesselLevel, List<VesselData>>>();
        }
        
        // TODO: other functions that will help load the data to the front-end
    }
}