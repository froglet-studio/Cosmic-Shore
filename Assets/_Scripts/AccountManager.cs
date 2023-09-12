using StarWriter.Utility.Singleton;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;

public class AccountManager : SingletonPersistent<AccountManager>
{

    public delegate void OnLoginSuccessEvent();
    public static event OnLoginSuccessEvent onLoginSuccess;

    public static string PlayerId;
    public static string PlayFabId;
    public static PlayFabAuthenticationContext AuthenticationContext;
    public static string EntityType;
    static List<string> Adjectives;
    static List<string> Nouns;

    [SerializeField] TMPro.TMP_Text DisplayName;
 
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
                DisplayName.text = result.DisplayName;
            },
            (PlayFabError error) =>
            {
                Debug.Log(error.GenerateErrorReport());
            }
        );
    }

    public void GenerateRandomDisplayName()
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

    public void SetRandomDisplayName()
    {
        int adjectiveIndex = Random.Range(0, Adjectives.Count);
        string adjective = Adjectives[adjectiveIndex];
        int nounIndex = Random.Range(0, Nouns.Count);
        string noun = Nouns[nounIndex];

        string name = adjective + noun;
        Debug.Log($"Display Name: {name}");
    }

    void Start()
    {
        StartCoroutine(DoTheThingsCoroutine());
    }

    IEnumerator DoTheThingsCoroutine()
    {
        Login();

        yield return new WaitForSeconds(1);

        LoadPlayerProfile();
    }

    void LoadPlayerProfile()
    {
        PlayFab.PlayFabClientAPI.GetPlayerProfile(
            new PlayFab.ClientModels.GetPlayerProfileRequest()
            {
                PlayFabId = PlayFabId,
            },
            result =>
            {
                Debug.Log($"Load Player Profile: {result.PlayerProfile.DisplayName}");
                if (DisplayName != null)
                    DisplayName.text = result.PlayerProfile.DisplayName;
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

    void Login()
    {
        if (AuthenticationContext != null)
            return;

#if (UNITY_ANDROID || UNITY_EDITOR)
        PlayFabClientAPI.LoginWithAndroidDeviceID(
            new LoginWithAndroidDeviceIDRequest()
            {
                CreateAccount = true,
                AndroidDeviceId = SystemInfo.deviceUniqueIdentifier
            }, 
            result =>
            {
                
                PlayerId = result.EntityToken.Entity.Id;
                PlayFabId = result.PlayFabId;
                AuthenticationContext = result.AuthenticationContext;
                EntityType = result.EntityToken.Entity.Type;
                Debug.Log($"Logged in: {result.PlayFabId}");
                Debug.Log($"Entity Type: {EntityType}");
                Debug.Log($"PlayerId: {PlayerId}");

                onLoginSuccess?.Invoke();
                LoadTitleData();
            }, 
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
        );
#endif
    }
}
