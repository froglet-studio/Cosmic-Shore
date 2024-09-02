using System;
using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.PlayerData;
using CosmicShore.Models;
using Newtonsoft.Json;
using PlayFab.ClientModels;
using UnityEngine;

namespace CosmicShore.App.Systems.Xp
{
    /// <summary>
    /// Captain Xp Data
    /// Contains Captain class elements - Space, Time, Charge, Mass
    /// </summary>
    [System.Serializable]
    public struct XpData
    {
        public int Space;
        public int Time;
        public int Charge;
        public int Mass;

        public XpData(int space, int time, int mass, int charge)
        {
            Space = space;
            Time = time;
            Mass = mass;
            Charge = charge;
        }
    }

    public class XpHandler
    {

        /// <summary>
        /// Delegate invoked when captain data (xp, encountered) Loaded
        /// </summary>
        public static Action OnCaptainDataLoaded;

        /// <summary>
        /// Captain Xp key for querying data from PlayFab data storage, not used for PlayFab API calls now.
        /// </summary>
        private const string ClassXpKey = "ClassXP";

        /// <summary>
        /// Encountered Captains key for querying data from PlayFab data storage, not used for PlayFab API calls now.
        /// </summary>
        private const string EncounteredCaptainsKey = "EncounteredCaptains";

        /// <summary>
        /// Class Xp Data
        /// Used for storing Captain Xp Data for each Ship type.
        /// </summary>
        public static Dictionary<ShipTypes, XpData> ClassXpData;

        /// <summary>
        /// Encountered Captain Data
        /// Used for storing Encountered Captains for each Ship type.
        /// </summary>
        public static Dictionary<ShipTypes, List<Element>> EncounteredCaptainsData;

        /// <summary>
        /// A wrapper to get player data for now.
        /// </summary>
        public static void LoadCaptainXpData()
        {
            if (ClassXpData == null)
            {
                // For now we don't pass any keys, pull all player data and query locally.
                PlayerDataController.Instance.GetPlayerData();
            }
        }

        public static void IssueXP(Captain captain, int amount)
        {
            Debug.Log($"XPHandler.IssueXP {captain.Name}, {amount}");

            if (!ClassXpData.ContainsKey(captain.Ship.Class))
                ClassXpData.Add(captain.Ship.Class, new XpData (0, 0, 0, 0));

            var xpData = ClassXpData[captain.Ship.Class];
            xpData.Space += captain.PrimaryElement == Element.Space ? amount : 0;
            xpData.Time += captain.PrimaryElement == Element.Time ? amount : 0;
            xpData.Mass += captain.PrimaryElement == Element.Mass ? amount : 0;
            xpData.Charge += captain.PrimaryElement == Element.Charge ? amount : 0;
            ClassXpData[captain.Ship.Class] = xpData;

            // TODO: Security - Move to cloud script and store in internal data
            var dataContent = new Dictionary<string, string>
            {
                { ClassXpKey, JsonConvert.SerializeObject(ClassXpData) }
            };

            PlayerDataController.Instance.UpdatePlayerData(dataContent, OnCaptainDataLoaded);

            Debug.Log($"IssueXP Success - {JsonConvert.SerializeObject(ClassXpData)}");
        }

        public static void EncounterCaptain(Captain captain)
        {
            if (EncounteredCaptainsData.ContainsKey(captain.Ship.Class))
            {
                if (EncounteredCaptainsData[captain.Ship.Class].Contains(captain.PrimaryElement)){ return; }

                EncounteredCaptainsData[captain.Ship.Class].Add(captain.PrimaryElement);
            }
            else
            {
                EncounteredCaptainsData[captain.Ship.Class] = new() { captain.PrimaryElement };
            }

            // TODO: Security - Move to cloud script and store in internal data
            var dataContent = new Dictionary<string, string>
            {
                { EncounteredCaptainsKey, JsonConvert.SerializeObject(EncounteredCaptainsData) }
            };

            PlayerDataController.Instance.UpdatePlayerData(dataContent, OnCaptainDataLoaded);

            Debug.Log($"Encounter Captain Success - {JsonConvert.SerializeObject(EncounteredCaptainsData)}");
        }


        public static int GetCaptainXP(Captain captain)
        {
            if (!ClassXpData.ContainsKey(captain.Ship.Class))
                return 0;

            switch (captain.PrimaryElement) {
                case Element.Space: return ClassXpData[captain.Ship.Class].Space;
                case Element.Time: return ClassXpData[captain.Ship.Class].Time;
                case Element.Mass: return ClassXpData[captain.Ship.Class].Mass;
                case Element.Charge: return ClassXpData[captain.Ship.Class].Charge;
            }

            return 0;
        }
        
        /// <summary>
        /// Process user data result upon pulling player data, convert the result to Class Xp Data, and log them in the console.
        /// </summary>
        /// <param name="result">Query result for player data</param>
        public static void OnLoadCaptainXpData(GetUserDataResult result)
        {
            ClassXpData = ConvertResultToCaptainXpData(result);
            EncounteredCaptainsData = ConvertResultToEncounteredCaptainData(result);

            foreach (var key in ClassXpData.Keys)
                Debug.Log($"OnLoadCaptainXpData - ClassXpData.ShipClassXpData.Keys: {key}");
            
            Debug.Log($"OnLoadCaptainXpData - Custom Data: {result.CustomData}");

            OnCaptainDataLoaded?.Invoke();
        }

        /// <summary>
        /// A helper class convert player data result to Class Xp Data
        /// if the data does not exist, return null.
        /// </summary>
        /// <param name="result">Player data query result</param>
        /// <returns></returns>
        static Dictionary<ShipTypes, XpData> ConvertResultToCaptainXpData(GetUserDataResult result)
        {
                return result.Data.ContainsKey(ClassXpKey) ?
                    (Dictionary<ShipTypes, XpData>)JsonConvert.DeserializeObject(result.Data[ClassXpKey].Value, typeof(Dictionary<ShipTypes, XpData>)) :
                    new();
        }

        /// <summary>
        /// A helper class convert player data result to Encountered Captain Data
        /// if the data does not exist, return null.
        /// </summary>
        /// <param name="result">Player data query result</param>
        /// <returns></returns>
        static Dictionary<ShipTypes, List<Element>> ConvertResultToEncounteredCaptainData(GetUserDataResult result)
        {
            return result.Data.ContainsKey(EncounteredCaptainsKey) ?
                (Dictionary<ShipTypes, List<Element>>)JsonConvert.DeserializeObject(result.Data[EncounteredCaptainsKey].Value, typeof(Dictionary<ShipTypes, List<Element>>)) :
                new();
        }
    }
}
