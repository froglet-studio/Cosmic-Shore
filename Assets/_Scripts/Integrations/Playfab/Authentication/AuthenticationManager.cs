using System;
using System.Collections;
using System.Collections.Generic;
using System.Security;
using CosmicShore.Integrations.Architectures.EventBus;
using CosmicShore.Integrations.PlayFab.PlayerData;
using CosmicShore.Integrations.PlayFab.PlayerModels;
using CosmicShore.Integrations.PlayFabV2.Models;
using CosmicShore.Utility.Singleton;
using JetBrains.Annotations;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;


namespace CosmicShore.Integrations.PlayFab.Authentication
{
    public class AuthenticationManager : SingletonPersistent<AuthenticationManager>
    {
        public static PlayFabAccount PlayFabAccount { get; private set; } = new();
        public static PlayerSession PlayerSession { get; private set; } = new();
        
        public static event Action OnLoginSuccess;
 
        public static event Action OnLoginError;

        public static event Action OnRegisterSuccess;

        public static List<string> Adjectives;
        public static List<string> Nouns;
        
        public override void Awake()
        {
            base.Awake();
            
            StartCoroutine(LoginCoroutine());
        }

        IEnumerator LoginCoroutine()
        {
            yield return new WaitForSeconds(1f);

            AnonymousLogin();
        }

        #region Player Profile
        
        /// <summary>
        /// Load Default Adjectives and Nouns
        /// Get a list of adjectives and nouns stored in Playfab title data for random name generation
        /// </summary>
        public void LoadRandomNameList()
        {
            if (Adjectives != null && Nouns != null)
            {
                Debug.Log("AuthenticationManager - Names are already retrieved.");
                return;
            }
            
            PlayFabClientAPI.GetTitleData(
                new GetTitleDataRequest
                {
                    AuthenticationContext = PlayFabAccount.AuthContext
                }, 
                result =>
                {
                    if (result.Data != null)
                    {
                        Adjectives = new(JsonConvert.DeserializeObject<string[]>(result.Data["DefaultDisplayNameAdjectives"]));
                        Nouns = new(JsonConvert.DeserializeObject<string[]>(result.Data["DefaultDisplayNameNouns"]));
                        
                        Debug.Log("AuthenticationManager - Default name list loaded.");
                        Debug.Log($"AuthenticationManager - Default adjectives: {Adjectives}");
                        Debug.Log($"AuthenticationManager - Default nouns: {Nouns}");
                    }
                            
                }, 
                (error) =>
                {
                    Debug.LogError(error.GenerateErrorReport());
                }
            );
        }
        #endregion

        #region Anonymous Login
        
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
        void AndroidLogin()
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
        void IOSLogin()
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
        void CustomIDLogin()
        {
            PlayFabClientAPI.LoginWithCustomID(
                new LoginWithCustomIDRequest
                {
                    CreateAccount = true,
                    CustomId = SystemInfo.deviceUniqueIdentifier
                }, 
                HandleLoginSuccess, 
                HandleLoginError
                );
        }

        void HandleLoginSuccess(LoginResult loginResult = null)
        {
            if (loginResult == null) return;
            
            PlayFabAccount ??= new();
            
            PlayFabAccount.ID = loginResult.PlayFabId;
            PlayFabAccount.AuthContext = loginResult.AuthenticationContext;
            //PlayerProfile.IsNewlyCreated = loginResult.NewlyCreated;

            Debug.Log($"AuthenticationManager - Logged in - Newly Created: {loginResult.NewlyCreated.ToString()}");
            Debug.Log($"AuthenticationManager - Play Fab Id: {PlayFabAccount.ID}");
            Debug.Log($"AuthenticationManager - Entity Type: {PlayFabAccount.AuthContext.EntityType}");
            Debug.Log($"AuthenticationManager - Entity Id: {PlayFabAccount.AuthContext.EntityId}");

            OnLoginSuccess?.Invoke();
            LoginEventBus.Publish(LoginType.Success);
        }

        void HandleLoginError(PlayFabError loginError = null)
        {
            Debug.LogError("AuthenticationManager - " + loginError.GenerateErrorReport());
            OnLoginError?.Invoke();
            LoginEventBus.Publish(LoginType.Fail);
        }
        #endregion

        #region Unlinking

        /// <summary>
        /// Unlink Anonymous Login
        /// Unlink based on the device unique identifier
        /// Can be tested on Unlink Anonymous Login button
        /// Reframe from clicking on it too much, it will abandon the anonymous account, next time login will create a whole new account.
        /// </summary>
        public void UnlinkCustomID()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        UnlinkAndroidCustomId();
#elif UNITY_IPHONE || UNITY_IOS && !UNITY_EDITOR
        UnlinkIOSCustomId();
#else
            UnlinkCustomId();
#endif
        }

        private void UnlinkAndroidCustomId()
        {
            PlayFabClientAPI.UnlinkAndroidDeviceID(new UnlinkAndroidDeviceIDRequest()
            {
                AndroidDeviceId = PlayerSession.IsRemembered? PlayerSession.LoginId : SystemInfo.deviceUniqueIdentifier
            }, (result) =>
            {
                if(PlayerSession.IsRemembered)
                    PlayerSession.ForgetMe();
                Debug.Log("Android Device Unlinked.");
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
        }

        private void UnlinkIOSCustomId()
        {
            PlayFabClientAPI.UnlinkIOSDeviceID(new UnlinkIOSDeviceIDRequest()
            {
                DeviceId = PlayerSession.IsRemembered? PlayerSession.LoginId : SystemInfo.deviceUniqueIdentifier
            }, (result) =>
            {
                if(PlayerSession.IsRemembered)
                    PlayerSession.ForgetMe();
                Debug.Log("IOS Device Unlinked.");
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
        }

        private void UnlinkCustomId()
        {
            PlayFabClientAPI.UnlinkCustomID(new UnlinkCustomIDRequest()
            {
                
                CustomId = PlayerSession.IsRemembered? PlayerSession.LoginId : SystemInfo.deviceUniqueIdentifier
            }, (result) =>
            {
                if(PlayerSession.IsRemembered)
                    PlayerSession.ForgetMe();
                Debug.Log("Custom Device Unlinked.");
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
        }

        #endregion

        #region WIP Email Login

        /// <summary>
        /// Email Login logic
        /// Make sure password stays in memory no longer than necessary
        /// </summary>
        public void EmailLogin([NotNull] string email, [NotNull] SecureString password, Action<PlayFabError> resultCallback)
        {
            if (email == null) throw new ArgumentNullException(nameof(email));
            if (password == null) throw new ArgumentNullException(nameof(password));
            PlayFabClientAPI.LoginWithEmailAddress(
                new LoginWithEmailAddressRequest()
                {
                    TitleId = PlayFabSettings.TitleId,
                    Email = email,
                    Password = password.ToString()
                },
                (result) =>
                {
                    PlayFabAccount.AuthContext = result.AuthenticationContext;
                    password?.Dispose();
                    Debug.Log("Logged in with email.");
                    PlayFabClientAPI.GetAccountInfo(
                        new GetAccountInfoRequest()
                        {
                            Email = email,
                            PlayFabId = PlayFabAccount.AuthContext.PlayFabId
                        },
                        (result) =>
                        {
                            Debug.Log($"PlayFab ID: {result.AccountInfo.PlayFabId}");
                            Debug.Log($"Player email retrieved: {result.AccountInfo.PrivateInfo.Email}");
                        }, null);
                },
                (error) =>
                {
                    Debug.Log(error.GenerateErrorReport());
                    resultCallback?.Invoke(error);
                }
                );
        }
        
        #endregion

        #region WIP Email Username Register

        /// <summary>
        /// Email Registration
        /// Make sure password stays in memory no longer than necessary
        /// </summary>
        public void RegisterWithEmail([NotNull] string email, [NotNull] SecureString password, Action<PlayFabError> resultCallback = null)
        {
            // Silent login first, if successful continue logging in with the anonymous account by adding username, email and password
            AnonymousLogin();
            
            var request = new AddUsernamePasswordRequest();
            
            request.Username = string.IsNullOrEmpty(PlayerDataController.PlayerProfile.Email) ? email : PlayerDataController.PlayerProfile.Email;
            request.Email = email;
            request.Password = password.ToString();
            
            PlayFabClientAPI.AddUsernamePassword(
                request, result =>
                {
                    PlayerDataController.PlayerProfile.Email = result.Username;
                    if (PlayerSession.IsRemembered)
                    {
                        // If the session is asked to be remembered, replace the custom id with newly generated Guid
                        PlayerSession.LoginId = Guid.NewGuid().ToString();
                        PlayFabClientAPI.LinkCustomID(
                            new LinkCustomIDRequest
                            {
                                CustomId = PlayerSession.IsRemembered? PlayerSession.LoginId : SystemInfo.deviceUniqueIdentifier,
                                // True if another user is already linked to the custom ID, unlink the other user and re-link.
                                ForceLink = PlayerSession.IsForceLink
                            },
                            null, null
                            );
                    }
                    Debug.Log("Register with email succeeded.");
                    Debug.Log($"Player username {result.Username}");
                    OnRegisterSuccess?.Invoke();
                }, (error) =>
                {
                    Debug.Log(error.GenerateErrorReport());
                    resultCallback?.Invoke(error);
                }
            );
        }
        
        #endregion

        
    }
}