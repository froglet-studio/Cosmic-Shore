using System.Collections.Generic;
using _Scripts._Core.Enums;

namespace _Scripts._Core.Playfab_Models.Player_Models
{
    /// <summary>
    /// Vessel Data Accessor
    /// It's very similar to Leaderboard Data Accessor
    /// TODO: we could further abstract the method in the future if there more use cases
    /// </summary>
    public class VesselDataAccessor
    {
        private static readonly string VesselDataFileName = "VesselData.data";

        static Dictionary<Vessels, List<VesselData>> VesselUpgradeLevels;

        /// <summary>
        /// Save Vessel Data
        /// </summary>
        /// <param name="shipTypes">Ship Types</param>
        /// <param name="vesselDataList">Vessel Data List</param>
        public void Save(Vessels vessel, List<VesselData> vesselDataList)
        {
            VesselUpgradeLevels??= Load();

            VesselUpgradeLevels[vessel] = vesselDataList;

            DataAccessor dataAccessor = new(VesselDataFileName);
            dataAccessor.Save(VesselUpgradeLevels);
        }

        /// <summary>
        /// Load Vessel Data
        /// </summary>
        /// <returns>Dictionary of Ship Types and Vessel Data List</returns>
        public Dictionary<Vessels, List<VesselData>> Load()
        {
            DataAccessor dataAccessor = new(VesselDataFileName);
            return dataAccessor.Load<Dictionary<Vessels, List<VesselData>>>();
        }
        
        // TODO: other functions that will help load the data to the front-end
    }
}