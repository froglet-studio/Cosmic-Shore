using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.PlayerData;
using Newtonsoft.Json;
using PlayFab.ClientModels;
using UnityEngine;

namespace CosmicShore.App.Systems.Xp
{
    /// <summary>
    /// Captain Xp Data
    /// Contains Captain class elements - Space, Time, Charge, Mass
    /// </summary>
    public struct CaptainXpData
    {
        public int Space;
        public int Time;
        public int Charge;
        public int Mass;

        public CaptainXpData(int space, int time, int charge, int mass)
        {
            Space = space;
            Time = time;
            Charge = charge;
            Mass = mass;
        }
    }

    public class XpHandler
    {
        /// <summary>
        /// Captain Xp key for querying data from PlayFab data storage, not used for now.
        /// </summary>
        private const string CaptainXpKey = "CaptainXP";

        /// <summary>
        /// Class Xp Data
        /// Used for storing Captain Xp Data for each Ship type.
        /// </summary>
        public static Dictionary<ShipTypes, CaptainXpData> ClassXpData = new();

        /// <summary>
        /// A wrapper to get player data for now.
        /// </summary>
        public static void LoadCaptainXpData()
        {
            // Fow now we don't pass any keys, pull all player data and query locally.
            PlayerDataController.Instance.GetPlayerData();
        }
        
        /// <summary>
        /// Process user data result upon pulling player data, convert the result to Class Xp Data, and log them in the console.
        /// </summary>
        /// <param name="result">Query result for player data</param>
        public static void OnLoadCaptainXpData(GetUserDataResult result)
        {
            ClassXpData = ConvertResultToCaptainXpData(result);
            foreach (var key in ClassXpData.Keys)
                Debug.Log($"OnLoadCaptainXpData - ClassXpData.ShipClassXpData.Keys: {key}");
            
            Debug.Log($"OnLoadCaptainXpData - Custom Data: {result.CustomData}");
        }

        /// <summary>
        /// A helper class convert player data result to Class Xp Data
        /// if the data does not exist, return null.
        /// </summary>
        /// <param name="result">Player data query result</param>
        /// <returns></returns>
        private static Dictionary<ShipTypes, CaptainXpData> ConvertResultToCaptainXpData(GetUserDataResult result)
        {
                return result.Data.ContainsKey(CaptainXpKey)?
                    (Dictionary<ShipTypes, CaptainXpData>)JsonConvert
                    .DeserializeObject(result.Data[CaptainXpKey].Value,
                    typeof(Dictionary<ShipTypes, CaptainXpData>))
                    : null;
        }
    }
}
