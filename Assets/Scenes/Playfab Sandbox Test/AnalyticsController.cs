using System;
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
    public static Action<PlayFabError> GeneratingErrorReport;

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
            }, HandleErrorReport);
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
            }, HandleErrorReport);
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
            }, HandleErrorReport);
    }

    /// <summary>
    /// Get User Read Only Data By Keys
    /// <param name="readOnlyKeys"> User data read only key list</param>
    /// </summary>
    public void GetUserReadOnlyData(in List<string> readOnlyKeys)
    {
        _playFabClientInstanceAPI.GetUserReadOnlyData(
            new GetUserDataRequest()
            {
                Keys = readOnlyKeys
            }, (result) =>
            {
                if (result == null)
                {
                    Debug.LogWarning($"{nameof(AnalyticsController)} - {nameof(GetUserReadOnlyData)} - Unable to retrieve data or no data available");
                    return;
                };
                
                Debug.Log($"{nameof(AnalyticsController)} - {nameof(GetUserReadOnlyData)} success.");
            }, HandleErrorReport
            );
    }

    /// <summary>
    /// Send Player Event
    /// Send player event given event name, custom data body, optional custom tags and timestamp
    /// <param name="playerEvent"> Player Event</param>
    /// </summary>
    public void SendPlayerEvent(in PlayerEvent playerEvent)
    {
        _playFabClientInstanceAPI.WritePlayerEvent(
            new WriteClientPlayerEventRequest()
            {
                Body = playerEvent.Body,
                CustomTags = playerEvent.CustomTag,
                EventName = playerEvent.EventName,
                Timestamp = playerEvent.Timestamp
            }, (result) =>
            {
                if (result == null) return;
                Debug.Log($"{nameof(AnalyticsController)} - {nameof(SendPlayerEvent)} success.");
            },HandleErrorReport);
    }

    /// <summary>
    /// Handle PlayFab Error Report
    /// Generate error report and raise the event
    /// <param name="error"> PlayFab Error</param>
    /// </summary>
    private void HandleErrorReport(PlayFabError error = null)
    {
        if (error == null) return;
        Debug.LogError(error.GenerateErrorReport());
        GeneratingErrorReport.Invoke(error);
    }
}
