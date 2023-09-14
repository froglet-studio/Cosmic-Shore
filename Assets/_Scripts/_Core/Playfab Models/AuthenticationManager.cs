using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using StarWriter.Utility.Singleton;

namespace _Scripts._Core.Playfab_Models
{
    
    public class AuthenticationManager : SingletonPersistent<AuthenticationManager>
    {
        public static PlayerAccount PlayerAccount;
        public static PlayerProfile PlayerProfile;
        
        public delegate void LoginSuccessEvent();
        public static event LoginSuccessEvent OnLoginSuccess;

        public delegate void LoginErrorEvent();

        public static event LoginErrorEvent OnLoginError;

        public delegate void ProfileLoaded();
        public static event ProfileLoaded OnProfileLoaded;

        public static List<string> Adjectives;
        public static List<string> Nouns;
        
        /// <summary>
        /// Set Player Display Name
        /// Update player display name, we can assume the account is already created here
        /// </summary>
        public void SetPlayerDisplayName(string displayName)
        {
            PlayFabClientAPI.UpdateUserTitleDisplayName(
                new UpdateUserTitleDisplayNameRequest()
                    {
                        DisplayName = displayName
                    },(result)=>
                    {
                        PlayerAccount.PlayerDisplayName = result.DisplayName;
                        Debug.Log($"Successful updated player display name: {PlayerAccount.PlayerDisplayName}");
                    }, 
                    (error) =>
                    {
                        Debug.LogError(error.GenerateErrorReport());
                    }
                );
        }

        /// <summary>
        /// Load Player Profile
        /// Load player profile using Playfab Id and return player display name
        /// </summary>
        public void LoadPlayerProfile()
        {
            PlayFabClientAPI.GetPlayerProfile(
                new GetPlayerProfileRequest()
                {
                    PlayFabId = PlayerAccount.PlayFabId
                }, (result) =>
                {
                    // The result will get publisher id, title id, player id (also called playfab id in other requests) and display name
                    PlayerProfile = PlayerProfile ?? new PlayerProfile();
                    PlayerProfile.DisplayName = result.PlayerProfile.DisplayName;
                    
                    // TODO: It might be good to retrieve player avatar url here 
                    
                    Debug.Log("Successfully retrieved player profile");
                    Debug.Log($"Player id: {PlayerProfile.DisplayName}");
                },
                (error) =>
                {
                    Debug.LogError(error.GenerateErrorReport());
                }
                );
        }
        
        /// <summary>
        /// Load Default Adjectives and Nouns
        /// Get a list of adjectives and nouns stored in Playfab title data for random name generation
        /// </summary>
        public void LoadRandomNameList()
        {
            if (Adjectives != null && Nouns != null)
            {
                Debug.Log("Names are already retrieved.");
                return;
            }
            
            PlayFabClientAPI.GetTitleData(
                new GetTitleDataRequest()
                {
                    AuthenticationContext = PlayerAccount.AuthContext
                }, 
                (result) =>
                {
                    if ( result is { Data: not null })
                    {
                            Adjectives = new(JsonConvert.DeserializeObject<string[]>(
                                    result.Data["DefaultDisplayNameAdjectives"]));
                            Nouns = new(JsonConvert.DeserializeObject<string[]>(
                                result.Data["DefaultDisplayNameNouns"]));
                            Debug.Log("Default name list loaded.");
                            Debug.Log($"Default adjectives: {Adjectives}");
                            Debug.Log($"Default nouns: {Nouns}");
                    }
                            
                }, 
                (error) =>
                {
                    Debug.LogError(error.GenerateErrorReport());
                }
                );
        }

        
        /// <summary>
        /// Anonymous Login
        /// If successful, populate player account with playfab id, auth context and newly created flag, no custom id for now
        /// </summary>
        public void AnonymousLogin()
        {
        #if UNITY_ANDROID && !UNITY_EDITOR
            AndroidLogin();
            
        #elif UNITY_IOS || UNITY_IPHONE && !UNITY_EDITOR
            IOSLogin();
        #else
            CustomIDLogin();
        #endif
        }
        
        
        /// <summary>
        /// Android Login
        /// Take Android device unique identifier id as device id and login
        /// </summary>
        private void AndroidLogin()
        {
            PlayFabClientAPI.LoginWithAndroidDeviceID(
                new LoginWithAndroidDeviceIDRequest()
                {
                    CreateAccount = true,
                    AndroidDeviceId = SystemInfo.deviceUniqueIdentifier
                }, 
                HandleLoginSuccess, 
                HandleLoginError
            );
        }

        /// <summary>
        /// IOS Login
        /// Take IOS device unique identifier id as device id and login
        /// </summary>
        private void IOSLogin()
        {
            PlayFabClientAPI.LoginWithIOSDeviceID(
                new LoginWithIOSDeviceIDRequest()
                {
                    CreateAccount = true,
                    DeviceId = SystemInfo.deviceUniqueIdentifier
                }, 
                HandleLoginSuccess, 
                HandleLoginError
                );
        }

        /// <summary>
        /// Custom ID Login
        /// For now custom ID login is used on PC
        /// </summary>
        private void CustomIDLogin()
        {
            PlayFabClientAPI.LoginWithCustomID(
                new LoginWithCustomIDRequest()
                {
                    CreateAccount = true,
                    CustomId = SystemInfo.deviceUniqueIdentifier
                }, 
                HandleLoginSuccess, 
                HandleLoginError
                );
        }

        private void HandleLoginSuccess(LoginResult loginResult)
        {
            PlayerAccount = PlayerAccount ?? new PlayerAccount();
            PlayerAccount.PlayFabId = loginResult.PlayFabId;
            PlayerAccount.AuthContext = loginResult.AuthenticationContext;
            PlayerAccount.IsNewlyCreated = loginResult.NewlyCreated;
            Debug.Log("Logged in with Android.");
                    
            Debug.Log($"Play Fab Id: {PlayerAccount.PlayFabId}");
            Debug.Log($"Entity Type: {PlayerAccount.AuthContext.EntityType}");
            Debug.Log($"Entity Id: {PlayerAccount.AuthContext.EntityId}");
            Debug.Log($"Session Ticket: {PlayerAccount.AuthContext.ClientSessionTicket}");

            OnLoginSuccess?.Invoke();
        }

        private void HandleLoginError(PlayFabError loginError)
        {
            Debug.LogError(loginError.GenerateErrorReport());
            OnLoginError?.Invoke();
        }
    }
}

