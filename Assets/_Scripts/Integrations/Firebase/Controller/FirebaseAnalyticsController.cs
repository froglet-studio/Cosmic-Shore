#nullable enable
using System;
using System.Collections.Generic;
using CosmicShore.App.Systems.CTA;
using CosmicShore.App.Systems.UserActions;
using Firebase;
using Firebase.Analytics;
using CosmicShore.Utility.Singleton;
using UnityEngine;

namespace CosmicShore.Integrations.Firebase.Controller
{
    public class FirebaseAnalyticsController : SingletonPersistent<FirebaseAnalyticsController>
    {
        private static bool _analyticsEnabled = true;
        private FirebaseApp _app;
        private Dictionary<string, object?> serviceDict;
        

        #region Firebase Analytics Controller Initialization and Enabling
        
        private void OnEnable()
        {
            FirebaseHelper.DependencyResolved += InitializeFirebaseAnalytics;
            UserActionSystem.Instance.OnUserActionCompleted += LogEventUserCompleteAction;
        }

        private void OnDisable()
        {
            FirebaseHelper.DependencyResolved -= InitializeFirebaseAnalytics;
            UserActionSystem.Instance.OnUserActionCompleted -= LogEventUserCompleteAction;
            _analyticsEnabled = false;
        }
        
        /// <summary>
        /// Initialize Firebase Analytics
        /// </summary>
        private void InitializeFirebaseAnalytics()
        {
            // Firebase analytics initialization
            _app = FirebaseApp.DefaultInstance;
            
            // Enable Firebase Analytics Data Collection TODO: ask player consent for data collection
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            
            // Set User Id on device unique identifier
            FirebaseAnalytics.SetUserId(SystemInfo.deviceUniqueIdentifier);
            
            // Set analytics enabled true
            _analyticsEnabled = true;
            
            // Set default session duration values.
            FirebaseAnalytics.SetSessionTimeoutDuration(new TimeSpan(0, 30, 0));
            
            //Also log app open upon initialization
            LogEventAppOpen();
            Debug.Log("Firebase Analytics Controller running...");
        }
        
        #endregion

        #region Analytics Tooling For Unity Services

        private void MappingDictionary()
        {
            // TODO: generalise mapping dictionary for Unity Services
        }
        

        #endregion
        #region Ad Measurement

        /// <summary>
        /// Log Event Add Impression
        /// </summary>
        public static void LogEventAdImpression()
        {
            if (!_analyticsEnabled) return;
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAdImpression);
            Debug.Log("Firebase logged add impression.");
        }

        #endregion

        #region Application Events
        /// <summary>
        /// Log Event App Open
        /// </summary>
        private void LogEventAppOpen()
        {
            if (!_analyticsEnabled) return;
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAppOpen);
            Debug.Log("Firebase logged App Open.");
        }

        /// <summary>
        /// Log Event Screen View
        /// Actually it's automatically logging event screen view
        /// </summary>
        public void LogEventScreenView()
        {
            if (!_analyticsEnabled) return;
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventScreenView); 
            Debug.Log("Firebase logged Screen View Event");
        }

        #endregion

        #region UI Action Events

        public void LogEventUserCompleteAction(UserAction action)
        {
            if (!_analyticsEnabled) return;

            var parameters = new[]
            {
                new Parameter("user_action_completed", action.Label),
                new Parameter("user_action_type", action.ActionType.ToString()),
                new Parameter("user_action_value", action.Value)
            };
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventScreenView, parameters);
            Debug.LogFormat("{0} - {1} - Firebase logged.", nameof(FirebaseAnalyticsController), nameof(LogEventUserCompleteAction));
        }

        #endregion

        #region Mini Game Events
        
        /// <summary>
        /// Log Event Mini Game Start
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="ship"></param>
        /// <param name="playerCount"></param>
        /// <param name="intensity"></param>
        public static void LogEventMiniGameStart(MiniGames mode, ShipTypes ship, int playerCount, int intensity)
        {
            if (!_analyticsEnabled) return;
            
            // Event parameters for Firebase
            var parameters = new[] {
                new Parameter(FirebaseAnalytics.ParameterLevel, nameof(MiniGames)),
                new Parameter(FirebaseAnalytics.ParameterLevelName, mode.ToString()),
                new Parameter(FirebaseAnalytics.ParameterCharacter, ship.ToString()),
                new Parameter("mini_game_player_count", playerCount),
                new Parameter("mini_game_intensity", intensity),
            };
            
            // // Event dictionary for Unity Analytics Service
            // var dict = new Dictionary<string, object?>
            // {
            //     { FirebaseAnalytics.ParameterLevel, nameof(MiniGames) },
            //     { FirebaseAnalytics.ParameterLevelName, mode.ToString()},
            //     { FirebaseAnalytics.ParameterCharacter, ship.ToString()},
            //     { "mini_game_player_count", playerCount},
            //     { "mini_game_intensity", intensity}
            // };
            
            // Log event in Firebase
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart, parameters);
            Debug.LogFormat("{0} - {1} - Firebase logged mini game start stats.", nameof(FirebaseAnalyticsController), nameof(LogEventMiniGameStart));
            
            // Log event in Unity Analytics
            // UnityAnalytics.Instance.LogFirebaseEvents(FirebaseAnalytics.EventLevelStart, dict);
            // Debug.LogFormat("{0} - {1} - Unity Service logged mini game start stats.", nameof(FirebaseAnalyticsController), nameof(LogEventMiniGameStart));
            
        }

        /// <summary>
        /// Log Event Mini Game End
        /// </summary>
        /// <param name="mode">Mini Game Mode</param>
        /// <param name="ship">Ship Type</param>
        /// <param name="playerCount">Player Count</param>
        /// <param name="intensity">Intensity</param>
        /// <param name="highScore">HighScore</param>
        public static void LogEventMiniGameEnd(MiniGames mode, ShipTypes ship, int playerCount, int intensity, int highScore)
        {
            if (!_analyticsEnabled) return;
            
            // Event parameters for Firebase
            var parameters = new [] {
                new Parameter(FirebaseAnalytics.ParameterLevel, nameof(MiniGames)),
                new Parameter(FirebaseAnalytics.ParameterLevelName, mode.ToString()),
                new Parameter(FirebaseAnalytics.ParameterCharacter, ship.ToString()),
                new Parameter("mini_game_player_count", playerCount),
                new Parameter("mini_game_intensity", intensity),
                new Parameter(FirebaseAnalytics.ParameterScore, highScore)
            };
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, parameters);
            Debug.Log("Firebase logged mini game end stats.");
        }

        #endregion
    
    }
}