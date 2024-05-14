using System;
using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.PlayerModels;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.EventsModels;
using CosmicShore.Utility.Singleton;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.PlayStream
{
    public class AnalyticsController : SingletonPersistent<AnalyticsController>
    {
        static PlayFabDataInstanceAPI _playFabDataInstanceAPI;
        private static PlayFabClientInstanceAPI _playFabClientInstanceAPI;
        private static PlayFabEventsInstanceAPI _playFabEventsInstanceAPI;
        public static Action<PlayFabError> GeneratingErrorReport;

        // private AuthenticationManager _authManager;
        //
        // public AnalyticsController(AuthenticationManager authManager)
        // {
        //     _authManager = authManager;
        // }

        private void Start()
        {
            // Load Player Client Instance API
            AuthenticationManager.OnLoginSuccess += InitializePlayerClientInstanceAPI;
            AuthenticationManager.OnLoginSuccess += InitializeEventsInstanceAPI;
        }

        private void OnDestroy()
        {
            AuthenticationManager.OnLoginSuccess -= InitializeEventsInstanceAPI;
            AuthenticationManager.OnLoginSuccess -= InitializePlayerClientInstanceAPI;
        
        }

        #region API Instance Initialization
        /// <summary>
        /// Initialize Player Data Instance API, not used right now, it manages entity data file upload and download
        /// </summary>
        private void InitializePlayerDataInstanceAPI()
        {
            _playFabDataInstanceAPI ??= new PlayFabDataInstanceAPI(AuthenticationManager.PlayFabAccount.AuthContext);
        }
    
        /// <summary>
        /// Initialize Player Client Instance API with authentication context
        /// </summary>
        private void InitializePlayerClientInstanceAPI()
        {
            _playFabClientInstanceAPI ??= new PlayFabClientInstanceAPI(AuthenticationManager.PlayFabAccount.AuthContext);
        }

        private void InitializeEventsInstanceAPI()
        {
            _playFabEventsInstanceAPI ??= new PlayFabEventsInstanceAPI(AuthenticationManager.PlayFabAccount.AuthContext);
        }
        #endregion

        #region User Data Operations
        /// <summary>
        /// Get User Customized Data
        /// <param name="keys"> User data key list</param>
        /// </summary>
        public void GetUserData(in List<string> keys)
        {
            _playFabClientInstanceAPI.GetUserData(
                new GetUserDataRequest()
                {
                    PlayFabId = AuthenticationManager.PlayFabAccount.ID,
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
                    }

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
        #endregion

        // Read only data can no longer be updated via API requests, they can only be configured in PlayFab dashboard
        #region Read Only Data Operations
    
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
                    }

                    Debug.Log($"{nameof(AnalyticsController)} - {nameof(GetUserReadOnlyData)} - success.");
                    foreach (var data in result.Data)
                    {
                        Debug.Log($"{nameof(AnalyticsController)} - {nameof(GetUserReadOnlyData)} - key: {data.Key} value: {data.Value.Value}");
                    }
                }, HandleErrorReport
            );
        }

        /// <summary>
        /// Get User Publisher Read Only Data By Keys
        /// <param name="readOnlyKeys"> User publisher data read only key list</param>
        /// </summary>
        public void GetPublisherReadOnlyData(in List<string> readOnlyKeys)
        {
            _playFabClientInstanceAPI.GetUserPublisherReadOnlyData(
                new GetUserDataRequest()
                {
                    Keys = readOnlyKeys,
                    PlayFabId = AuthenticationManager.PlayFabAccount.ID
                }, (result) =>
                {
                    if (result == null) return;
                    Debug.Log($"{nameof(AnalyticsController)} - {nameof(GetPublisherReadOnlyData)} - success.");
                    foreach (var data in result.Data)
                    {
                        Debug.Log($"{nameof(AnalyticsController)} - {nameof(GetPublisherReadOnlyData)} - key: {data.Key} value: {data.Value.Value}");
                    }
                },HandleErrorReport);
        }
        #endregion

        #region PlayStream Event Handling
    
        /// <summary>
        /// Send Player Event
        /// Send player event given event name, custom data body, optional custom tags and timestamp
        /// Player event will show up in data explore after certain amount of time, meaning it's not real time data.
        /// <param name="playerEvent"> Player Event</param>
        /// </summary>
        public void SendPlayerEvent(in PlayerEvent playerEvent)
        {
            _playFabClientInstanceAPI.WritePlayerEvent(
                new WriteClientPlayerEventRequest()
                {
                    Body = playerEvent.Body,
                    CustomTags = playerEvent.CustomTags,
                    EventName = playerEvent.EventName,
                    Timestamp = playerEvent.Timestamp
                }, (result) =>
                {
                    if (result == null) return;
                    Debug.Log($"{nameof(AnalyticsController)} - {nameof(SendPlayerEvent)} success.");
                    Debug.Log($"{nameof(AnalyticsController)} - {nameof(SendPlayerEvent)} - event id: {result.EventId}");
                },HandleErrorReport);
        }

        /// <summary>
        /// Write PlayStream Events
        /// Write a collection of PlayStream Events to PlayFab data storage.
        /// <param name="eventsModel"> PlayStream Events</param>
        /// </summary>
        public void WritePlayStreamEvents(in EventsModel eventsModel)
        {
            _playFabEventsInstanceAPI.WriteEvents(
                new WriteEventsRequest()
                {
                    Events = eventsModel.EventContents,
                    CustomTags = eventsModel.CustomTags
                }, (result) =>
                {
                    if (result == null)
                    {
                        Debug.LogWarning($"{nameof(AnalyticsController)} - {nameof(WritePlayStreamEvents)} no result.");
                        return;
                    }

                    foreach (var eventId in result.AssignedEventIds)
                    {
                        Debug.LogWarning($"{nameof(AnalyticsController)} - {nameof(WritePlayStreamEvents)} - assigned event id: {eventId}.");
                    }
                },HandleErrorReport
            );
        }
    
        #endregion



        #region Error Handling
    
        /// <summary>
        /// Handle PlayFab Error Report
        /// Generate error report and raise the event
        /// <param name="error"> PlayFab Error</param>
        /// </summary>
        private void HandleErrorReport(PlayFabError error = null)
        {
            if (error == null) return;
            Debug.LogError(error.GenerateErrorReport());
            GeneratingErrorReport?.Invoke(error);
        }
    
        #endregion
    }
}
