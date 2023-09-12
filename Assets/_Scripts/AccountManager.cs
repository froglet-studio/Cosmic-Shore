using StarWriter.Utility.Singleton;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine.Serialization;

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

    public static string PlayerId;
    private static string _playFabId;
    public static PlayFabAuthenticationContext AuthenticationContext;
    public static string EntityType;
    static List<string> Adjectives;
    static List<string> Nouns;

    [SerializeField] private TMPro.TMP_Text displayName;
 
    public static string PlayerDisplayName = "";

    public void SetPlayerDisplayName(string playerName)
    {
        PlayFab.PlayFabClientAPI.UpdateUserTitleDisplayName(
            new PlayFab.ClientModels.UpdateUserTitleDisplayNameRequest()
            {
                DisplayName = playerName
            },
            (UpdateUserTitleDisplayNameResult result) =>
            {
                PlayerDisplayName = result.DisplayName;
                displayName.text = result.DisplayName;
            },
            (PlayFabError error) =>
            {
                Debug.Log(error.GenerateErrorReport());
            }
        );
    }

    private void GenerateRandomDisplayName()
    {
        int adjectiveIndex = Random.Range(0, Adjectives.Count);
        string adjective = Adjectives[adjectiveIndex];
        int nounIndex = Random.Range(0, Nouns.Count);
        string noun = Nouns[nounIndex];

        string name = adjective + noun;
        Debug.Log($"Display Name: {name}");
        SetPlayerDisplayName(name);
    }

    public string GetPlayerDisplayName()
    {
        return PlayerDisplayName;
    }
    

    void LoadPlayerProfile()
    {
        PlayFab.PlayFabClientAPI.GetPlayerProfile(
            new PlayFab.ClientModels.GetPlayerProfileRequest()
            {
                PlayFabId = _playFabId,
            },
            result =>
            {
                Debug.Log($"Load Player Profile: {result.PlayerProfile.DisplayName}");
                if (displayName != null)
                    displayName.text = result.PlayerProfile.DisplayName;
            },
            error =>
            {
                Debug.Log(error.GenerateErrorReport());
            }
        );
    }

    void LoadTitleData()
    {
        PlayFabClientAPI.GetTitleData(
            new GetTitleDataRequest()
            {
                AuthenticationContext = AuthenticationContext,
            },
            result =>
            {
                foreach (var item in result.Data.Keys)
                {
                    Debug.Log(item);
                }
                if (result.Data == null || !result.Data.ContainsKey("DefaultDisplayNameAdjectives"))
                    Debug.Log("No DefaultDisplayNameAdjectives");
                else
                {
                    Debug.Log("DefaultDisplayNameAdjectives: " + result.Data["DefaultDisplayNameAdjectives"]);
                    Debug.Log("DefaultDisplayNameNouns: " + result.Data["DefaultDisplayNameNouns"]);
                    //string jsonString = "[\"String1\", \"String2\", \"String3\"]";
                    //var deserialized = JsonConvert.DeserializeObject(result.Data);
                    Adjectives = new(JsonConvert.DeserializeObject<string[]>(result.Data["DefaultDisplayNameAdjectives"]));

                    Nouns = new(JsonConvert.DeserializeObject<string[]>(result.Data["DefaultDisplayNameNouns"]));
                }
            },
            error =>
            {
                Debug.Log("Got error getting titleData:");
                Debug.Log(error.GenerateErrorReport());
            }
        );
    }

    public void AnonymousLogin()
    {
        if (AuthenticationContext != null)
        {
            Debug.LogError("No authentication context provided.");
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
}
