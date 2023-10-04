using System.Collections.Generic;
using _Scripts._Core.Playfab_Models;
using PlayFab;
using PlayFab.ClientModels;
using StarWriter.Utility.Singleton;
using UnityEngine;

public class AnalyticsController : SingletonPersistent<AnalyticsController>
{
    static PlayFabDataInstanceAPI _playFabDataInstanceAPI;
    private static PlayFabClientInstanceAPI _playFabClientInstanceAPI;

    private void Start()
    {
        // Load Player Client Instance API
        AuthenticationManager.OnLoginSuccess += InitializePlayerClientInstanceAPI;
    }

    private void OnDisable()
    {
        AuthenticationManager.OnLoginSuccess -= InitializePlayerClientInstanceAPI;
    }

    /// <summary>
    /// Initialize Player Data Instance API, not used right now, it manages entity data file upload and download
    /// </summary>
    private void InitializePlayerDataInstanceAPI()
    {
        _playFabDataInstanceAPI ??= new PlayFabDataInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
    }
    
    /// <summary>
    /// Initialize Player Client Instance API with authentication context
    /// </summary>
    private void InitializePlayerClientInstanceAPI()
    {
        _playFabClientInstanceAPI ??= new PlayFabClientInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
    }
    
    /// <summary>
    /// Get User Customized Data
    /// <param name="keys"> User data key list</param>
    /// </summary>
    public void GetUserData(in List<string> keys)
    {
        _playFabClientInstanceAPI.GetUserData(
            new GetUserDataRequest()
            {
                PlayFabId = AuthenticationManager.PlayerAccount.PlayFabId,
                Keys = keys
            }, (result) =>
            {
                if (result == null)
                {
                    Debug.LogWarning($"{nameof(AnalyticsController)} - {nameof(GetUserData)} - no user data available.");
                    return;
                }
                
                foreach (var pair in result.Data)
                {
                    Debug.Log($"{nameof(AnalyticsController)} - {nameof(GetUserData)} - key: {pair.Key} value: {pair.Value.Value}");
                }
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
    }

    /// <summary>
    /// Set/Update User Customized Data
    /// <param name="userData"> User data key-value pairs</param>
    /// </summary>
    public void SetUserData(in Dictionary<string, string> userData)
    {
        _playFabClientInstanceAPI.UpdateUserData(
            new UpdateUserDataRequest()
            {
                Data = userData,
                Permission = UserDataPermission.Public
            }, (result) =>
            {
                if (result == null)
                {
                    Debug.LogWarning($"{nameof(AnalyticsController)} - {nameof(SetUserData)} - Unable to retrieve data or no data available");
                    return;
                };
                
                Debug.Log($"{nameof(AnalyticsController)} - {nameof(SetUserData)} success.");
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
    }

    /// <summary>
    /// Delete User Data By Keys
    /// <param name="keysToRemove"> User data key list</param>
    /// </summary>
    public void DeleteUserDataByKeys(in List<string> keysToRemove)
    {
        _playFabClientInstanceAPI.UpdateUserData(
            new UpdateUserDataRequest()
            {
                KeysToRemove = keysToRemove
            }, (result) =>
            {
                if (result == null) return;
                Debug.Log($"{nameof(AnalyticsController)} - {nameof(DeleteUserDataByKeys)} data successfully deleted by keys.");
            }, (error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
    }
}
