using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using CosmicShore.App.Systems.UserJourney;
using CosmicShore.Integrations.Playfab.PlayerModels;
using CosmicShore.Integrations.PlayFabV2.Models;
using JetBrains.Annotations;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using VContainer.Unity;

namespace CosmicShore.Integrations.Playfab.Authentication
{
    public class AuthenticationManager : IInitializable, IDisposable
    {
        public PlayFabAccount PlayFabAccount { get; set; }
        // public static PlayerProfile PlayerProfile;
        public UserProfile UserProfile { get; set; }
        public PlayerSession PlayerSession { get; set; }
        
        public event Action OnLoginSuccess;
 
        public event Action OnLoginError;

        // public delegate void ProfileLoaded();
        public event Action OnProfileLoaded;

        public event Action OnRegisterSuccess;

        public static List<string> Adjectives;
        public static List<string> Nouns;
        
        public AuthenticationManager(PlayFabAccount account, UserProfile profile, PlayerSession session)
        {
            PlayFabAccount = account;
            UserProfile = profile;
            PlayerSession = session;
        }
        public void Initialize()
        {
            AnonymousLogin();
            OnLoginSuccess += LoadPlayerProfile;
        }

        public void Dispose()
        {
            PlayFabAccount = null;
            UserProfile = null;
            PlayerSession = null;
            OnLoginSuccess -= LoadPlayerProfile;
        }

        #region Player Profile
        
        /// <summary>
        /// Set Player Display Name
        /// Update player display name, we can assume the account is already created here
        /// </summary>
        public void SetPlayerDisplayName(string displayName, Action<UpdateUserTitleDisplayNameResult> callback = null)
        {
            var request = new UpdateUserTitleDisplayNameRequest();
            request.DisplayName = displayName;
            PlayFabClientAPI.UpdateUserTitleDisplayName(request,
                (result) =>
                {
                    Debug.Log($"AuthenticationManager - Successful updated player display name: {UserProfile.DisplayName}");
                    UserProfile.DisplayName = result.DisplayName;
                    callback?.Invoke(result);
                }, 
                (error) =>
                {
                    Debug.LogError(error.GenerateErrorReport());
                }
            );
        }

        public void SePlayerAvatar(string avatarUrl)
        {
            var request = new UpdateAvatarUrlRequest();
            request.ImageUrl = avatarUrl;
            PlayFabClientAPI.UpdateAvatarUrl(
                request,
                result =>
                {
                    UserProfile.AvatarUrl = avatarUrl;
                    Debug.Log("Authentication Manager - Successfully updated player avatar.");
                },
                error =>
                {
                    Debug.LogError(error.GenerateErrorReport());
                });
        }


        /// <summary>
        /// Load Player Profile
        /// Load player profile using Playfab Id and return player display name
        /// </summary>
        public void LoadPlayerProfile()
        {
            var request = new GetPlayerProfileRequest();
            request.PlayFabId = PlayFabAccount.ID;
            request.ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true,
                ShowAvatarUrl = true
            };
            
            PlayFabClientAPI.GetPlayerProfile(request, 
                result =>
                {
                    // The result will get publisher id, title id, player id (also called playfab id in other requests) and display name
                    UserProfile.DisplayName = result.PlayerProfile.DisplayName;
                    // TODO: It might be good to retrieve player avatar url here 
                    UserProfile.AvatarUrl =
                        string.IsNullOrEmpty(result.PlayerProfile.AvatarUrl)
                            ? result.PlayerProfile.AvatarUrl
                            : AvatarLinks.Icons.First();
                    
                    Debug.Log("AuthenticationManager - Successfully retrieved player profile");
                    Debug.Log($"AuthenticationManager - Player id: {UserProfile.UniqueID}");

                    OnProfileLoaded?.Invoke();
                },
                error =>
                {
                    Debug.LogError(error.GenerateErrorReport());
                    Debug.Log($"AuthenticationManager - PlayFabId = {PlayFabAccount.ID}");
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
                Debug.Log("AuthenticationManager - Names are already retrieved.");
                return;
            }
            
            PlayFabClientAPI.GetTitleData(
                new GetTitleDataRequest()
                {
                    AuthenticationContext = PlayFabAccount.AuthContext
                }, 
                (result) =>
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
                new LoginWithCustomIDRequest()
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
            PlayFabAccount ??= new PlayFabAccount();
            if (loginResult != null)
            {
                PlayFabAccount.ID = loginResult.PlayFabId;
                PlayFabAccount.AuthContext = loginResult.AuthenticationContext;
                UserProfile.IsNewlyCreated = loginResult.NewlyCreated;

                Debug.Log($"AuthenticationManager - Logged in - Newly Created: {loginResult.NewlyCreated.ToString()}");
                Debug.Log($"AuthenticationManager - Play Fab Id: {PlayFabAccount.ID}");
                Debug.Log($"AuthenticationManager - Entity Type: {PlayFabAccount.AuthContext.EntityType}");
                Debug.Log($"AuthenticationManager - Entity Id: {PlayFabAccount.AuthContext.EntityId}");
                // Get it when we need to use postman for testing requests
                // Debug.Log($"AuthenticationManager - Entity Token: {loginResult.AuthenticationContext.EntityToken}");
                Debug.Log($"AuthenticationManager - Session Ticket: {PlayFabAccount.AuthContext.ClientSessionTicket}");

                OnLoginSuccess?.Invoke();
            }
        }

        void HandleLoginError(PlayFabError loginError = null)
        {
            Debug.LogError("AuthenticationManager - " + loginError.GenerateErrorReport());
            OnLoginError?.Invoke();
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
            PlayFabClientAPI.AddUsernamePassword(
                new AddUsernamePasswordRequest()
                {
                    // Username is required for registering an account
                    Username = string.IsNullOrEmpty(UserProfile.Email)? email: UserProfile.Email,
                    Email = email,
                    Password = password.ToString()
                    
                }, (AddUsernamePasswordResult result) =>
                {
                    UserProfile.Email = result.Username;
                    if (PlayerSession.IsRemembered)
                    {
                        // If the session is asked to be remembered, replace the custom id with newly generated Guid
                        PlayerSession.LoginId = Guid.NewGuid().ToString();
                        PlayFabClientAPI.LinkCustomID(
                            new LinkCustomIDRequest()
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