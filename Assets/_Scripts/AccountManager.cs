using System;
using StarWriter.Utility.Singleton;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using System.Collections.Generic;
using System.Security;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Unity.Collections.LowLevel.Unsafe;
using Random = UnityEngine.Random;


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

/// <summary>
/// Account Manager
/// Manages anonymous, recoverable login and account register and Account Unlink
/// </summary>
public class AccountManager : SingletonPersistent<AccountManager>
{

    public delegate void LoginSuccessEvent();
    public static event LoginSuccessEvent OnOnLoginSuccess;
    
    public static PlayFabAuthenticationContext AuthenticationContext;

    private static List<string> _adjectives;
    private static List<string> _nouns;

    private void Start()
    {
        AnonymousLogin();
    }

    /// <summary>
    /// Anonymous Login
    /// Manages anonymous, recoverable login and account register and Account Unlink
    /// </summary>
    public void AnonymousLogin()
    {
        if (AuthenticationContext != null)
        {
            Debug.LogWarning("Authentication context information exists.\n You are already logged in.");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidLogin();
        
#elif UNITY_IOS || UNITY_IPHONE && !UNITY_EDITOR
        IOSLogin();
#else
        CustomIDLogin();
#endif
    }

    private void AndroidLogin()
    {
        PlayFabClientAPI.LoginWithAndroidDeviceID(
            new LoginWithAndroidDeviceIDRequest()
            {
                CreateAccount = true,
                AndroidDeviceId = SystemInfo.deviceUniqueIdentifier
            }, 
            result =>
            {
                AuthenticationContext = result.AuthenticationContext;
                Debug.Log("Logged in with Android.");
                
                Debug.Log($"Play Fab Id: {AuthenticationContext.PlayFabId}");
                Debug.Log($"Entity Type: {AuthenticationContext.EntityType}");
                Debug.Log($"Entity Id: {AuthenticationContext.EntityId}");
                Debug.Log($"Session Ticket: {AuthenticationContext.ClientSessionTicket}");

                OnOnLoginSuccess?.Invoke();
            }, 
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
        );
    }

    private void IOSLogin()
    {
        PlayFabClientAPI.LoginWithIOSDeviceID(
            new LoginWithIOSDeviceIDRequest()
            {
                CreateAccount = true,
                DeviceId = SystemInfo.deviceUniqueIdentifier
            }, (result) =>
            {
                AuthenticationContext = result.AuthenticationContext;
                Debug.Log("Logged in with IOS.");
                Debug.Log($"Play Fab Id: {AuthenticationContext.PlayFabId}");
                Debug.Log($"Entity Type: {AuthenticationContext.EntityType}");
                Debug.Log($"Entity Id: {AuthenticationContext.EntityId}");
                Debug.Log($"Session Ticket: {AuthenticationContext.ClientSessionTicket}");
                
                OnOnLoginSuccess?.Invoke();
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
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
            }, (result) =>
            {
                AuthenticationContext = result.AuthenticationContext;
                Debug.Log("Logged in with Custom ID.");
                Debug.Log($"Play Fab Id: {AuthenticationContext.PlayFabId}");
                Debug.Log($"Entity Type: {AuthenticationContext.EntityType}");
                Debug.Log($"Entity Id: {AuthenticationContext.EntityId}");
                Debug.Log($"Session Ticket: {AuthenticationContext.ClientSessionTicket}");
                
                OnOnLoginSuccess?.Invoke();
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
            );
    }

    public void OnEmailLogin()
    {
        var email = "yeah@froglet.studio";
        var chars = new[] { 't', 'h', 'i', 's' };
        var password = new SecureString();
        foreach (var c in chars)
        {
            password.AppendChar(c);
        }
        
        EmailLogin(email, password);
    }

    /// <summary>
    /// Email Login
    /// Make sure password stays in memory no longer than necessary
    /// </summary>
    private void EmailLogin([NotNull] string email, [NotNull] SecureString password)
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
                AuthenticationContext = result.AuthenticationContext;
                password?.Dispose();
                Debug.Log("Logged in with email.");
                PlayFabClientAPI.GetAccountInfo(
                    new GetAccountInfoRequest()
                    {
                        Email = email,
                        PlayFabId = AuthenticationContext.PlayFabId
                    },
                    (GetAccountInfoResult result) =>
                    {
                        Debug.Log($"PlayFab ID: {result.AccountInfo.PlayFabId}");
                        Debug.Log($"Player email retrieved: {result.AccountInfo.PrivateInfo.Email}");
                    }, null);
            },
            (error)=>
                    {
                        Debug.Log(error.GenerateErrorReport());
                    }
            );
    }

    /// <summary>
    /// Update player display name with random generated one
    /// Can be tested by clicking Generate Random Name button
    /// </summary>
    public void OnUpdateNewPlayerTitleName()
    {
        var displayName = GenerateRandomDisplayName();
        UpdatePlayerTitleDisplayName(displayName);
    }
    

    private void UpdatePlayerTitleDisplayName(string playerName)
    {
        playerName = string.IsNullOrEmpty(playerName) ? GenerateRandomDisplayName() : playerName;
        PlayFabClientAPI.UpdateUserTitleDisplayName(
            new UpdateUserTitleDisplayNameRequest()
            {
                DisplayName = playerName
            },
            (UpdateUserTitleDisplayNameResult result) =>
            {
                Debug.Log($"Updated player display name to {result.DisplayName}");
            },
            (PlayFabError error) =>
            {
                Debug.Log(error.GenerateErrorReport());
            }
        );
    }

    private string GenerateRandomDisplayName()
    {
        var adjectiveIndex = Random.Range(0, _adjectives.Count);
        var adjective = _adjectives[adjectiveIndex];
        var nounIndex = Random.Range(0, _nouns.Count);
        var noun = _nouns[nounIndex];

        var name = $"{adjective} {noun}";
        Debug.Log($"Random Generated Name: {name}");
        return name;
    }
    
    

    public void LoadPlayerProfile()
    {
        if (AuthenticationContext == null)
        {
            Debug.LogWarning("Not logged in.");
            return;
        }
        
        PlayFab.PlayFabClientAPI.GetPlayerProfile(
            new GetPlayerProfileRequest()
            {
                PlayFabId = AuthenticationContext?.PlayFabId,
            },
            result =>
            {
                Debug.Log($"Load Player Profile: {result.PlayerProfile.DisplayName}");
            },
            error =>
            {
                Debug.Log(error.GenerateErrorReport());
            }
        );
    }

    /// <summary>
    /// Load Player Title Name Random Generate List from Content -> Title Data
    /// </summary>
    public void LoadTitleData()
    {
        if (AuthenticationContext == null)
        {
            Debug.LogWarning("Not logged in.");
            return;
        }
        
        PlayFabClientAPI.GetTitleData(
            new GetTitleDataRequest()
            {
                AuthenticationContext = AuthenticationContext,
            },
            result =>
            {
                foreach (var item in result.Data.Keys)
                {
                    Debug.Log($"Player data key: {item}");
                }
                if (result.Data == null || !result.Data.ContainsKey("DefaultDisplayNameAdjectives"))
                    Debug.Log("No DefaultDisplayNameAdjectives");
                else
                {
                    Debug.Log("DefaultDisplayNameAdjectives: " + result.Data["DefaultDisplayNameAdjectives"]);
                    Debug.Log("DefaultDisplayNameNouns: " + result.Data["DefaultDisplayNameNouns"]);
                    //string jsonString = "[\"String1\", \"String2\", \"String3\"]";
                    //var deserialized = JsonConvert.DeserializeObject(result.Data);
                    _adjectives = new(JsonConvert.DeserializeObject<string[]>(result.Data["DefaultDisplayNameAdjectives"]));

                    _nouns = new(JsonConvert.DeserializeObject<string[]>(result.Data["DefaultDisplayNameNouns"]));
                }
            },
            error =>
            {
                Debug.Log("Got error getting titleData:");
                Debug.Log(error.GenerateErrorReport());
            }
        );
    }
    
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
        AuthenticationContext = null;
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
}
