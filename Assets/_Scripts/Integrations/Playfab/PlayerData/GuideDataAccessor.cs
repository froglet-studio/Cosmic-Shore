using System.Collections.Generic;
using CosmicShore.Integrations.Enums;

namespace CosmicShore.Integrations.PlayFab.PlayerModels
{
    /// <summary>
    /// Guide Data Accessor
    /// It's very similar to Leaderboard Data Accessor
    /// TODO: we could further abstract the method in the future if there more use cases
    /// </summary>
    public class GuideDataAccessor
    {
        private static readonly string GuideDataFileName = "GuideData.data";

        static Dictionary<GuideLevel, List<GuideData>> GuideUpgradeLevels;

        /// <summary>
        /// Save Guide Data
        /// </summary>
        /// <param name="shipTypes">Ship Types</param>
        /// <param name="guideDataList">Guide Data List</param>
        public void Save(GuideLevel guideLevel, List<GuideData> guideDataList)
        {
            GuideUpgradeLevels??= Load();

            GuideUpgradeLevels[guideLevel] = guideDataList;

            DataAccessor dataAccessor = new(GuideDataFileName);
            dataAccessor.Save(GuideUpgradeLevels);
        }

        /// <summary>
        /// Load Guide Data
        /// </summary>
        /// <returns>Dictionary of Ship Types and Guide Data List</returns>
        public Dictionary<GuideLevel, List<GuideData>> Load()
        {
            DataAccessor dataAccessor = new(GuideDataFileName);
            return dataAccessor.Load<Dictionary<GuideLevel, List<GuideData>>>();
        }
    }
}