using System.Collections.Generic;

namespace _Scripts._Core.Playfab_Models.Player_Models
{
    public class VesselDataAccessor
    {
        private static readonly string VesselDataFileName = "VesselData.data";

        static Dictionary<ShipTypes, List<VesselData>> ShipUpgradeLevels;

        public static void Save(ShipTypes shipTypes, List<VesselData> vesselDataList)
        {
            ShipUpgradeLevels??= Load();

            ShipUpgradeLevels[shipTypes] = vesselDataList;

            DataAccessor dataAccessor = new(VesselDataFileName);
            dataAccessor.Save(ShipUpgradeLevels);
        }

        private static Dictionary<ShipTypes, List<VesselData>> Load()
        {
            DataAccessor dataAccessor = new(VesselDataFileName);
            return dataAccessor.Load<Dictionary<ShipTypes, List<VesselData>>>();
        }
        
        // TODO: other functions that will help load the data to the front-end
    }
}