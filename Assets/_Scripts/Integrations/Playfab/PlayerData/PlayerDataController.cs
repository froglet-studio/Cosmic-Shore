using System;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using CosmicShore.App.Systems.Xp;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Utility;
using CosmicShore.Utility.Singleton;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.PlayerData
{
    public class PlayerDataController : SingletonPersistent<PlayerDataController>
    {
        
        private const string DisplayNamePlayerPrefKey = "DisplayName";
        private const string ProfileIconIdPlayerPrefKey = "ProfileIconId";
        public static PlayerProfile PlayerProfile { get; private set; } = new();
        public static event Action OnProfileLoaded;
        public static event Action OnPlayerDisplayNameUpdated;
        public static event Action OnPlayerAvatarUpdated;
        public static event Action<GetUserDataResult> OnGettingPlayerData;

        private static PlayFabClientInstanceAPI _playFabClientInstanceAPI;
        
        private void Start()
        {
            //LoadPlayerProfileOffline();
            AuthenticationManager.OnLoginSuccess += XpHandler.LoadCaptainXpData;
            AuthenticationManager.OnLoginSuccess += LoadPlayerProfile;
            OnGettingPlayerData += XpHandler.OnLoadCaptainXpData;
        }

        public void OnDestroy()
        {
            AuthenticationManager.OnLoginSuccess -= XpHandler.LoadCaptainXpData;
            AuthenticationManager.OnLoginSuccess -= LoadPlayerProfile;
            OnGettingPlayerData -= XpHandler.OnLoadCaptainXpData;
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
                    PlayerProfile.Update(result.PlayerProfile.DisplayName, result.PlayerProfile.AvatarUrl);
                    
                    Debug.Log($"PlayerDataController - LoadPlayerProfile - Avatar url {result.PlayerProfile.AvatarUrl}");
                    Debug.Log($"PlayerDataController - LoadPlayerProfile - local Avatar url {PlayerProfile.AvatarUrl}");
                    Debug.Log($"PlayerDataController - LoadPlayerProfile - Profile Icon id {PlayerProfile.ProfileIconId}");

                    if (string.IsNullOrEmpty(result.PlayerProfile.AvatarUrl))
                        SetPlayerAvatar(new System.Random().Next(1,19));

                    PlayerPrefs.SetString(DisplayNamePlayerPrefKey, PlayerProfile.DisplayName);
                    PlayerPrefs.SetString(ProfileIconIdPlayerPrefKey, PlayerProfile.AvatarUrl);
                    PlayerPrefs.Save();

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
        /// Set the AvatarURL property of PlayFab. 
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

        #region Update Player Data

        /// <summary>
        /// Update non-essential player data such as favorites and some local settings.
        /// Read-only and internal data are handled on the server side to prevent hacking and cheating.
        /// </summary>
        /// <param name="playerData">A list of string we want to save on player data</param>
        public void UpdatePlayerData(Dictionary<string, string> playerData, Action successCallback=null)
        {
            InitializePlayerClientInstanceAPI();
            var request = new UpdateUserDataRequest { Data = playerData };
            _playFabClientInstanceAPI.UpdateUserData(request,
                successCallback == null ? OnUpdatePlayerData : (result) => successCallback?.Invoke(),
                PlayFabUtility.HandleErrorReport);
        }

        private void OnUpdatePlayerData(UpdateUserDataResult result)
        {
            if (result == null) return;
            Debug.Log("PlayerDataController - OnUpdatePlayerData - Player data updated.");
        }

        #endregion

        #region Load Player Data
        
        /// <summary>
        /// Get player data from PlayFab player data storage.
        /// When data keys is null, pull all player data.
        /// For now, we are pulling all data from data storage and query the values locally.
        /// Because PlayFab will return error when the key does not exist.
        /// For first time player it will happen a lot.
        /// </summary>
        /// <param name="dataKeys">key for values to be queried</param>
        public void GetPlayerData(List<string> dataKeys = null)
        {
            InitializePlayerClientInstanceAPI();
            var request = new GetUserDataRequest();
            request.Keys = dataKeys ?? new();
            
            _playFabClientInstanceAPI.GetUserData(request, 
                OnGettingPlayerData, 
                PlayFabUtility.GettingPlayFabErrors);
        }

        #endregion
    }
}