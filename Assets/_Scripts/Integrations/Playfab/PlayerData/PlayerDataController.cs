using System;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using CosmicShore.App.Systems.Clout;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Utility;
using CosmicShore.Utility.Singleton;
using UnityEngine;
using Newtonsoft.Json;

namespace CosmicShore.Integrations.PlayFab.PlayerData
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
        private const string CaptainXpKey = "CaptainXP";
        private const string ShipCloutKey = "ShipClout";
        private const string MasterCloutKey = "MasterClout";
        private const string DisplayNamePlayerPrefKey = "DisplayName";
        private const string ProfileIconIdPlayerPrefKey = "ProfileIconId";
        public PlayerProfile PlayerProfile { get; private set; } = new("Player", "1");
        public static event Action OnProfileLoaded;
        public static event Action OnPlayerDisplayNameUpdated;
        public static event Action OnPlayerAvatarUpdated;


        static PlayFabClientInstanceAPI _playFabClientInstanceAPI;
        
        // Shard data
        public Dictionary<ShipTypes, ShardData> CaptainXpData = new();
        
        // Clout data
        private Clout _playerClout;
        
        // Clout related event
        public static event Action<Clout> OnLoadingPlayerClout;

        private void Start()
        {
            //LoadPlayerProfileOffline();
            AuthenticationManager.OnLoginSuccess += LoadCaptainXpData;
            AuthenticationManager.OnLoginSuccess += LoadClout;
            AuthenticationManager.OnLoginSuccess += LoadPlayerProfile;
        }

        public void OnDestroy()
        {
            AuthenticationManager.OnLoginSuccess -= LoadCaptainXpData;
            AuthenticationManager.OnLoginSuccess -= LoadClout;
            AuthenticationManager.OnLoginSuccess -= LoadPlayerProfile;
        }
        
        void InitializePlayerClientInstanceAPI()
        {
            // Change API instance upon auth context changes
            if(_playFabClientInstanceAPI?.authenticationContext!= AuthenticationManager.PlayFabAccount.AuthContext)
                _playFabClientInstanceAPI = new PlayFabClientInstanceAPI(AuthenticationManager.PlayFabAccount.AuthContext);
            
            // Make API instance singleton
            else
                _playFabClientInstanceAPI ??= new PlayFabClientInstanceAPI(AuthenticationManager.PlayFabAccount.AuthContext);
        }


        #region PlayerProfile

        /// <summary>
        /// Load Player Profile
        /// Load player profile using Playfab Id and return player display name
        /// </summary>
        public void LoadPlayerProfile()
        {
            var request = new GetPlayerProfileRequest();
            request.PlayFabId = AuthenticationManager.PlayFabAccount.ID;
            request.ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true,
                ShowAvatarUrl = true
            };

            PlayFabClientAPI.GetPlayerProfile(request,
                result =>
                {
                    // The result will get publisher id, title id, player id (also called playfab id in other requests) and display name
                    PlayerProfile.DisplayName = result.PlayerProfile.DisplayName;
                    PlayerProfile.AvatarUrl = result.PlayerProfile.AvatarUrl;

                    if (string.IsNullOrEmpty(result.PlayerProfile.AvatarUrl))
                        SetPlayerAvatar(new System.Random().Next(1,19));

                    PlayerPrefs.SetString(DisplayNamePlayerPrefKey, PlayerProfile.DisplayName);
                    PlayerPrefs.SetString(ProfileIconIdPlayerPrefKey, PlayerProfile.AvatarUrl);

                    Debug.Log("AuthenticationManager - Successfully retrieved player profile");
                    Debug.Log($"AuthenticationManager - Player id: {PlayerProfile.UniqueID}");

                    OnProfileLoaded?.Invoke();
                },PlayFabUtility.HandleErrorReport
            );
        }

        public void LoadPlayerProfileOffline()
        {
            var displayName = PlayerPrefs.HasKey(DisplayNamePlayerPrefKey) ? PlayerPrefs.GetString(DisplayNamePlayerPrefKey) : "Player";
            var avatarUrl = PlayerPrefs.HasKey(ProfileIconIdPlayerPrefKey) ? PlayerPrefs.GetString(ProfileIconIdPlayerPrefKey) : "1";
            PlayerProfile = new(displayName, avatarUrl);
            OnProfileLoaded?.Invoke();
        }

        /// <summary>
        /// Set Player Display Name
        /// Update player display name, we can assume the account is already created here
        /// </summary>
        public void SetPlayerDisplayName(string displayName, Action<UpdateUserTitleDisplayNameResult> callback = null)
        {
            var request = new UpdateUserTitleDisplayNameRequest();
            request.DisplayName = displayName;
            PlayFabClientAPI.UpdateUserTitleDisplayName(request,
                result =>
                {
                    Debug.Log($"AuthenticationManager - Successful updated player display name: {PlayerProfile.DisplayName}");
                    PlayerProfile.DisplayName = result.DisplayName;
                    OnPlayerDisplayNameUpdated?.Invoke();
                    PlayerPrefs.SetString(DisplayNamePlayerPrefKey, result.DisplayName);
                    callback?.Invoke(result);
                },
                PlayFabUtility.HandleErrorReport
            );
        }

        /// <summary>
        /// Set the AvatarURL property of playfab. 
        /// NOTE: Since we are tracking all profile icons with a scriptable object, instead of using a URL we are just setting this to an integer id
        /// </summary>
        /// <param name="avatarId">ID of the player avatar (profile icon)</param>
        public void SetPlayerAvatar(int avatarId)
        {
            var request = new UpdateAvatarUrlRequest();
            request.ImageUrl = avatarId.ToString();
            PlayFabClientAPI.UpdateAvatarUrl(
                request,
                _ =>
                {
                    PlayerProfile.AvatarUrl = avatarId.ToString();
                    Debug.Log("PlayerDataController - Successfully updated player avatar.");
                    PlayerPrefs.SetString(ProfileIconIdPlayerPrefKey, avatarId.ToString());
                    OnPlayerAvatarUpdated?.Invoke();
                },
                PlayFabUtility.HandleErrorReport);
        }
        #endregion

        private void LoadCaptainXpData()
        {
            InitializePlayerClientInstanceAPI();

            _playFabClientInstanceAPI.GetUserData(
                new GetUserDataRequest
                {
                    PlayFabId = AuthenticationManager.PlayFabAccount.ID,
                    Keys = new List<string> { CaptainXpKey }
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

                        CaptainXpData = (Dictionary<ShipTypes, ShardData>)JsonConvert.DeserializeObject(result.Data[key].Value, typeof(Dictionary<ShipTypes, ShardData>));

                        Debug.Log($"LoadShardData - shardData.Keys: {CaptainXpData.Keys.Count}");
                        Debug.Log($"LoadShardData - shardData[Dolphin].Space: {CaptainXpData[ShipTypes.Dolphin].Space}");
                        Debug.Log($"LoadShardData - shardData[Dolphin].Time: {CaptainXpData[ShipTypes.Dolphin].Time}");
                        Debug.Log($"LoadShardData - shardData[Dolphin].Mass: {CaptainXpData[ShipTypes.Dolphin].Mass}");
                        Debug.Log($"LoadShardData - shardData[Dolphin].Charge: {CaptainXpData[ShipTypes.Dolphin].Charge}");

                        foreach (var key2 in CaptainXpData.Keys)
                            Debug.Log($"LoadShardData - shardData.ShipShardData.Keys: {key2}");
                    }
                    
                    Debug.Log($"LoadShardData - Custom Data: {result.CustomData}");
                }, PlayFabUtility.HandleErrorReport
            );
        }

        void LoadClout()
        {
            InitializePlayerClientInstanceAPI();
            _playFabClientInstanceAPI.GetUserData(
                new GetUserDataRequest
                {
                    PlayFabId = AuthenticationManager.PlayFabAccount.ID,
                    Keys = new List<string> { ShipCloutKey, MasterCloutKey }
                },OnLoadingClout
                ,PlayFabUtility.HandleErrorReport
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
                _playerClout.MasterCloutValue = (int)JsonConvert.DeserializeObject(masterCloutRecord.Value, typeof(int))!;
            }
            
            // Get ship clout values
            if (data.TryGetValue(ShipCloutKey, out var shipCloutRecord))
            {
                _playerClout.ShipClouts = (Dictionary<ShipTypes, int>)JsonConvert.DeserializeObject(shipCloutRecord.Value, typeof(Dictionary<ShipTypes, int>));
                Debug.Log($"LoadClout - CloutData.Keys: {_playerClout.ShipClouts.Keys.Count}");
                Debug.Log($"LoadClout - CloutData[Dolphin]: {_playerClout.ShipClouts[ShipTypes.Dolphin]}");
            }
            
            // 
            OnLoadingPlayerClout?.Invoke(_playerClout);

            Debug.Log("LoadClout - Player Clout loaded");
        }

        public void UpdatePlayerClout(Clout playerClout)
        {
            InitializePlayerClientInstanceAPI();

            Dictionary<string, string> cloutData = new()
            {
                {MasterCloutKey, playerClout.MasterCloutValue.ToString()},
                {ShipCloutKey, JsonConvert.SerializeObject(playerClout.ShipClouts)}
            };

            _playFabClientInstanceAPI.UpdateUserData(
                new UpdateUserDataRequest
                {
                    Data = cloutData,
                    Permission = UserDataPermission.Public
                }, result =>
                {
                    if (result == null)
                    {
                        Debug.LogWarning(
                            $"{nameof(PlayerDataController)} - {nameof(UpdatePlayerClout)} - Unable to retrieve data or no data available");
                        return;
                    }

                    Debug.Log($"{nameof(PlayerDataController)} - {nameof(UpdatePlayerClout)} success.");
                },
                PlayFabUtility.HandleErrorReport);
        }
    }
}