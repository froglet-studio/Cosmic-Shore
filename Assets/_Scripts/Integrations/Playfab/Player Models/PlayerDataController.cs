using System;
using CosmicShore.Integrations.Playfab.Authentication;
using CosmicShore.Utility.Singleton;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.App.Systems.Clout;
using UnityEngine;
using Newtonsoft.Json;

namespace CosmicShore.Integrations.Playfab.Player_Models
{
    public struct ShardData
    {
        public int Space;
        public int Time;
        public int Charge;
        public int Mass;

        public ShardData(int space, int time, int charge, int mass)
        {
            Space = space;
            Time = time;
            Charge = charge;
            Mass = mass;
        }
    }

    public class PlayerDataController : SingletonPersistent<PlayerDataController>
    {
        const string ShardDataKey = "Shards";
        const string ShipCloutKey = "ShipClout";
        private const string MasterCloutKey = "MasterClout";
        
        static PlayFabClientInstanceAPI _playFabClientInstanceAPI;
        
        // Shard data
        [HideInInspector] public Dictionary<ShipTypes, ShardData> PlayerShardData;
        
        // Clout data
        Clout playerClout;
        
        // Clout related event
        public static event Action<Clout> OnLoadingPlayerClout;
        
        static void InitializePlayerClientInstanceAPI()
        {
            // Change API instance upon auth context changes
            if(_playFabClientInstanceAPI?.authenticationContext!= AuthenticationManager.PlayerAccount.AuthContext)
                _playFabClientInstanceAPI = new PlayFabClientInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
            
            // Make API instance singleton
            else
                _playFabClientInstanceAPI ??= new PlayFabClientInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
        }

        void Start()
        {
            AuthenticationManager.OnLoginSuccess += LoadShardData;
            AuthenticationManager.OnLoginSuccess += LoadClout;
        }

        void LoadShardData()
        {
            InitializePlayerClientInstanceAPI();

            _playFabClientInstanceAPI.GetUserData(
                new GetUserDataRequest()
                {
                    PlayFabId = AuthenticationManager.PlayerAccount.PlayFabId,
                    Keys = new List<string> { ShardDataKey }
                },
                (result) =>
                {
                    Debug.Log($"LoadShardData - Data: {result.Data}");
                    Debug.Log($"LoadShardData - Data.Keys: {result.Data.Keys.Count}");
                    foreach (var key in result.Data.Keys)
                    {
                        Debug.Log($"LoadShardData - Data: Key:{key}, Value:{result.Data[key]}");
                        Debug.Log($"LoadShardData - Data: json:{result.Data[key].ToJson()}");
                        Debug.Log($"LoadShardData - Data: Value:{result.Data[key].Value}");

                        PlayerShardData = (Dictionary<ShipTypes, ShardData>)JsonConvert.DeserializeObject(result.Data[key].Value, typeof(Dictionary<ShipTypes, ShardData>));

                        Debug.Log($"LoadShardData - shardData.Keys: {PlayerShardData.Keys.Count}");
                        Debug.Log($"LoadShardData - shardData[Dolphin].Space: {PlayerShardData[ShipTypes.Dolphin].Space}");
                        Debug.Log($"LoadShardData - shardData[Dolphin].Time: {PlayerShardData[ShipTypes.Dolphin].Time}");
                        Debug.Log($"LoadShardData - shardData[Dolphin].Mass: {PlayerShardData[ShipTypes.Dolphin].Mass}");
                        Debug.Log($"LoadShardData - shardData[Dolphin].Charge: {PlayerShardData[ShipTypes.Dolphin].Charge}");

                        foreach (var key2 in PlayerShardData.Keys)
                            Debug.Log($"LoadShardData - shardData.ShipShardData.Keys: {key2}");
                    }
                    
                    Debug.Log($"LoadShardData - Custom Data: {result.CustomData}");
                },HandleErrorReport
            );
        }

        void LoadClout()
        {
            InitializePlayerClientInstanceAPI();
            _playFabClientInstanceAPI.GetUserData(
                new GetUserDataRequest()
                {
                    PlayFabId = AuthenticationManager.PlayerAccount.PlayFabId,
                    Keys = new List<string> { ShipCloutKey, MasterCloutKey }
                },OnLoadingClout
                ,HandleErrorReport
            );
        }

        void OnLoadingClout(GetUserDataResult result)
        {
            if (result == null || result.Data?.Count == 0)
            {
                Debug.Log($"LoadClout - Nothing to see here.");
                return;
            }

            var data = result.Data;
            
            Debug.Log($"LoadClout - Data.Keys: {data.Keys.Count}");
            
            // Get player master clout value
            if (data.TryGetValue(MasterCloutKey, out var masterCloutRecord))
            {
                playerClout.MasterCloutValue = (int)JsonConvert.DeserializeObject(masterCloutRecord.Value, typeof(int))!;
            }
            
            // Get ship clout values
            if (data.TryGetValue(ShipCloutKey, out var shipCloutRecord))
            {
                playerClout.shipClouts = (Dictionary<ShipTypes, int>)JsonConvert.DeserializeObject(shipCloutRecord.Value, typeof(Dictionary<ShipTypes, int>));
                Debug.Log($"LoadClout - CloutData.Keys: {playerClout.shipClouts.Keys.Count}");
                Debug.Log($"LoadClout - CloutData[Dolphin]: {playerClout.shipClouts[ShipTypes.Dolphin]}");
            }
            
            // 
            OnLoadingPlayerClout?.Invoke(playerClout);

            Debug.Log($"LoadClout - Player Clout loaded");
        }

        public void UpdatePlayerShardData(Dictionary<ShipTypes, ShardData> playerShardData)
        {
            InitializePlayerClientInstanceAPI();
     
            Dictionary<string, string> shardData = playerShardData.Keys.ToDictionary(key => key.ToString(), key => playerShardData[key].ToString());

            _playFabClientInstanceAPI.UpdateUserData(
                new UpdateUserDataRequest()
                {
                    Data = shardData,
                    Permission = UserDataPermission.Public
                }, (result) =>
                {
                    if (result == null)
                    {
                        Debug.LogWarning($"{nameof(PlayerDataController)} - {nameof(UpdatePlayerShardData)} - Unable to retrieve data or no data available");
                        return;
                    };
                
                    Debug.Log($"{nameof(PlayerDataController)} - {nameof(UpdatePlayerShardData)} success.");
                },HandleErrorReport
                );
        }

        public void UpdatePlayerClout(Clout playerClout)
        {
            InitializePlayerClientInstanceAPI();

            Dictionary<string, string> cloutData = new()
            {
                {MasterCloutKey, playerClout.MasterCloutValue.ToString()},
                {ShipCloutKey, JsonConvert.SerializeObject(playerClout.shipClouts)}
            };

            _playFabClientInstanceAPI.UpdateUserData(
                new UpdateUserDataRequest()
                {
                    Data = cloutData,
                    Permission = UserDataPermission.Public
                }, (result) =>
                {
                    if (result == null)
                    {
                        Debug.LogWarning(
                            $"{nameof(PlayerDataController)} - {nameof(UpdatePlayerClout)} - Unable to retrieve data or no data available");
                        return;
                    }

                    Debug.Log($"{nameof(PlayerDataController)} - {nameof(UpdatePlayerClout)} success.");
                },
                HandleErrorReport);
        }

        
        #region Error Handling
    
        /// <summary>
        /// Handle PlayFab Error Report
        /// Generate error report and raise the event
        /// <param name="error"> PlayFab Error</param>
        /// </summary>
        private void HandleErrorReport(PlayFabError error = null)
        {
            if (error == null) return;
            Debug.LogError(error.GenerateErrorReport());
            // GeneratingErrorReport?.Invoke(error);
        }
    
        #endregion
    }
}