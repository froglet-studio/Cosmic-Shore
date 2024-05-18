using System;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Utility.Singleton;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.Utility
{
    public class PlayFabUtility : SingletonPersistent<PlayFabUtility>
    {
        private static bool IsLogin => AuthenticationManager.PlayFabAccount.AuthContext is not null;

        public static Action<PlayFabError> GettingPlayFabErrors;

        public static DateTime ServerTime;
        /// <summary>
        /// Get current time from the server
        /// </summary>
        private void GetCurrentTime()
        {
            if (!IsLogin) return;
            
            var request = new GetTimeRequest();
            PlayFabClientAPI.GetTime(request, OnGettingCurrentTime, HandleErrorReport);
        }

        private void OnGettingCurrentTime(GetTimeResult result)
        {
            if (result == null) return;

            ServerTime = result.Time;
            Debug.Log($"Catalog manager - OnGettingCurrentTime() - The time is: {result.Time}");
        }
        
        #region Situation Handling

        /// <summary>
        /// Handle Error Report, for all the orther PlayFab integrations
        /// </summary>
        /// <param name="error">PlayFab Error</param>
        public static void HandleErrorReport(PlayFabError error)
        {
            GettingPlayFabErrors?.Invoke(error);
            // Keep the error message here if there will be unit tests.
            Debug.LogErrorFormat("PlayFabUtility - error code: {0} message: {1}", error.Error, error.ErrorMessage);
        }
        #endregion
    }
}
