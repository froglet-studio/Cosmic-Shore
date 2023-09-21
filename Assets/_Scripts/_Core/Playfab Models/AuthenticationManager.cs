using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using StarWriter.Utility.Singleton;
using System.Security;
using JetBrains.Annotations;
using PlayFab.SharedModels;

namespace _Scripts._Core.Playfab_Models
{
    /// <summary>
    /// Authentication methods
    /// Authentication methods references: https://api.playfab.com/documentation/client#Authentication
    /// </summary>
    public enum AuthMethods
    {
        Default,
        Anonymous,
        PlayFabLogin,
        EmailLogin,
        Register
    }

    public class AuthenticationManager : SingletonPersistent<AuthenticationManager>
    {
        public static PlayerAccount PlayerAccount;
        public static PlayerProfile PlayerProfile;
        
        public static EventHandler<LoginResult> LoginSuccess;
 
        public static EventHandler<PlayFabError> LoginError;

        public delegate void ProfileLoaded();
        public static event ProfileLoaded OnProfileLoaded;

        public static List<string> Adjectives;
        public static List<string> Nouns;

        public static PlayerSession PlayerSession;
        

        void Start()
        {
            LoginSuccess += LoadPlayerProfile;
            AnonymousLogin();
        }

        /// <summary>
        /// Set Player Display Name
        /// Update player display name, we can assume the account is already created here
        /// </summary>
        public void SetPlayerDisplayName(string displayName, Action<UpdateUserTitleDisplayNameResult> callback = null)
        {
            PlayFabClientAPI.UpdateUserTitleDisplayName(
                new UpdateUserTitleDisplayNameRequest()
                {
                    DisplayName = displayName
                },
                (result) =>
                {
                    Debug.Log($"AuthenticationManager - Successful updated player display name: {PlayerAccount.PlayerDisplayName}");

                    PlayerAccount.PlayerDisplayName = result.DisplayName;
                    PlayerProfile.DisplayName = result.DisplayName;
                    callback?.Invoke(result);
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
        public void LoadPlayerProfile(object sender, LoginResult result)
        {
            PlayFabClientAPI.GetPlayerProfile(
                new GetPlayerProfileRequest()
                {
                    PlayFabId = PlayerAccount.PlayFabId
                }, 
                (result) =>
                {
                    // The result will get publisher id, title id, player id (also called playfab id in other requests) and display name
                    PlayerProfile ??= new PlayerProfile();
                    PlayerProfile.DisplayName = result.PlayerProfile.DisplayName;
                    
                    // TODO: It might be good to retrieve player avatar url here 
                    
                    Debug.Log("AuthenticationManager - Successfully retrieved player profile");
                    Debug.Log($"AuthenticationManager - Player id: {PlayerProfile.DisplayName}");

                    OnProfileLoaded?.Invoke();
                },
                (error) =>
                {
                    Debug.LogError(error.GenerateErrorReport());
                    Debug.Log($"AuthenticationManager - PlayFabId = {PlayerAccount.PlayFabId}");
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
                    AuthenticationContext = PlayerAccount.AuthContext
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
            PlayerAccount = PlayerAccount ?? new PlayerAccount();
            if (loginResult != null)
            {
                PlayerAccount.PlayFabId = loginResult.PlayFabId;
                PlayerAccount.AuthContext = loginResult.AuthenticationContext;
                PlayerAccount.IsNewlyCreated = loginResult.NewlyCreated;

                Debug.Log($"AuthenticationManager - Logged in - Newly Created: {loginResult.NewlyCreated.ToString()}");
                Debug.Log($"AuthenticationManager - Play Fab Id: {PlayerAccount.PlayFabId}");
                Debug.Log($"AuthenticationManager - Entity Type: {PlayerAccount.AuthContext.EntityType}");
                Debug.Log($"AuthenticationManager - Entity Id: {PlayerAccount.AuthContext.EntityId}");
                Debug.Log($"AuthenticationManager - Session Ticket: {PlayerAccount.AuthContext.ClientSessionTicket}");

                LoginSuccess?.Invoke(this, loginResult);
            }
        }

        void HandleLoginError(PlayFabError loginError = null)
        {
            Debug.LogError("AuthenticationManager - " + loginError.GenerateErrorReport());
            LoginError?.Invoke(this, loginError);
        }


        #region Unlinking

        /// <summary>
        /// Unlink Anonymous Login
        /// Unlink based on the device unique identifier
        /// Can be tested on Unlink Anonymous Login button
        /// Reframe from clicking on it too much, it will abandon the anonymous account, next time login will create a whole new account.
        /// </summary>
        public void UnlinkAnonymousLogin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        UnlinkAndroidLogin();
#elif UNITY_IPHONE || UNITY_IOS && !UNITY_EDITOR
        UnlinkIOSLogin();
#else
            UnlinkCustomIDLogin();
#endif
        }

        private void UnlinkAndroidLogin()
        {
            PlayFabClientAPI.UnlinkAndroidDeviceID(new UnlinkAndroidDeviceIDRequest()
            {
                AndroidDeviceId = SystemInfo.deviceUniqueIdentifier
            }, (result) =>
            {
                Debug.Log("Android Device Unlinked.");
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
        }

        private void UnlinkIOSLogin()
        {
            PlayFabClientAPI.UnlinkIOSDeviceID(new UnlinkIOSDeviceIDRequest()
            {
                DeviceId = SystemInfo.deviceUniqueIdentifier
            }, (result) =>
            {
                Debug.Log("IOS Device Unlinked.");
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
        }

        private void UnlinkCustomIDLogin()
        {
            PlayFabClientAPI.UnlinkCustomID(new UnlinkCustomIDRequest()
            {
                CustomId = SystemInfo.deviceUniqueIdentifier
            }, (result) =>
            {
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
                    PlayerAccount.AuthContext = result.AuthenticationContext;
                    password?.Dispose();
                    Debug.Log("Logged in with email.");
                    PlayFabClientAPI.GetAccountInfo(
                        new GetAccountInfoRequest()
                        {
                            Email = email,
                            PlayFabId = PlayerAccount.AuthContext.PlayFabId
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

        /// <summary>
        /// Email Registration
        /// Make sure password stays in memory no longer than necessary
        /// </summary>
        public void RegisterWithEmail(string email, SecureString password, Action<PlayFabError> resultCallback = null)
        {
            // Silent login first, if successful continue logging in with the anonymous account by adding username, email and password
            AnonymousLogin();
            PlayFabClientAPI.AddUsernamePassword(
                new AddUsernamePasswordRequest()
                {
                    // Username is required for registering an account
                    Username = string.IsNullOrEmpty(PlayerAccount.Username)? email: PlayerAccount.Username,
                    Email = email,
                    Password = password.ToString()
                    
                }, (AddUsernamePasswordResult result) =>
                {
                    PlayerAccount.Username = result.Username;
                    if (PlayerSession.IsRemembered)
                    {
                        // If the session is asked to be remembered, replace the custom id with newly generated Guid
                        PlayerSession.LoginId = Guid.NewGuid().ToString();
                        PlayFabClientAPI.LinkCustomID(
                            new LinkCustomIDRequest()
                            {
                                CustomId = PlayerSession.LoginId,
                                // True if another user is already linked to the custom ID, unlink the other user and re-link.
                                ForceLink = PlayerSession.IsForceLink
                            },
                            null, null
                            );
                    }
                    Debug.Log("Register with email succeeded.");
                    Debug.Log($"Player username {result.Username}");
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