using CosmicShore.Integrations.Playfab.Authentication;
using CosmicShore.Utility.Singleton;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
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
        const string CloutKey = "Clout";
        static PlayFabClientInstanceAPI _playFabClientInstanceAPI;
        public Dictionary<ShipTypes, ShardData> PlayerShardData;
        public Dictionary<ShipTypes, int> PlayerClout;

        void InitializePlayerClientInstanceAPI()
        {
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
                },
                (error) =>
                {
                    Debug.LogError($"LoadShardData: {error.ErrorMessage}");
                }
            );
        }

        void LoadClout()
        {
            InitializePlayerClientInstanceAPI();

            _playFabClientInstanceAPI.GetUserData(
                new GetUserDataRequest()
                {
                    PlayFabId = AuthenticationManager.PlayerAccount.PlayFabId,
                    Keys = new List<string> { CloutKey }
                },
                (result) =>
                {
                    Debug.Log($"LoadClout - Data: {result.Data}");
                    Debug.Log($"LoadClout - Data.Keys: {result.Data.Keys.Count}");
                    foreach (var key in result.Data.Keys)
                    {
                        Debug.Log($"LoadClout - Data: Key:{key}, Value:{result.Data[key]}");
                        Debug.Log($"LoadClout - Data: json:{result.Data[key].ToJson()}");
                        Debug.Log($"LoadClout - Data: Value:{result.Data[key].Value}");

                        PlayerClout = (Dictionary<ShipTypes, int>)JsonConvert.DeserializeObject(result.Data[key].Value, typeof(Dictionary<ShipTypes, int>));

                        Debug.Log($"LoadClout - CloutData.Keys: {PlayerClout.Keys.Count}");
                        Debug.Log($"LoadClout - CloutData[Dolphin]: {PlayerClout[ShipTypes.Dolphin]}");

                        foreach (var key2 in PlayerClout.Keys)
                            Debug.Log($"LoadClout - shardData.ShipShardData.Keys: {key2}");
                    }

                    Debug.Log($"LoadClout - Custom Data: {result.CustomData}");
                },
                (error) =>
                {
                    Debug.LogError($"LoadClout: {error.ErrorMessage}");
                }
            );
        }

        public void UpdatePlayerShardData(Dictionary<ShipTypes, ShardData> playerShardData)
        {

        }

        public void UpdatePlayerClout(Dictionary<ShipTypes, int> playerClout)
        {

        }
    }
}