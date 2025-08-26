using CosmicShore.App.Systems.UserActions;
using CosmicShore.Utilities;

#if !UNITY_WEBGL
using CosmicShore.SOAP;
using Firebase;
using Firebase.Analytics;
#endif
using System;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Integrations.Firebase.Controller
{
    public class FirebaseAnalyticsController : SingletonPersistent<FirebaseAnalyticsController>
    {
#if !UNITY_WEBGL
        private static bool _analyticsEnabled = true;
        private static DependencyStatus _dependencyStatus = DependencyStatus.Available;

        private static Action _dependencyResolved;
        
        [SerializeField] ScriptableEventMiniGameData _onStartMiniGame;
        [SerializeField] ScriptableEventMiniGameData _onEndMiniGame;
        
        #region Firebase Analytics Controller Initialization and Enabling

        private void OnEnable()
        {
            _onStartMiniGame.OnRaised += LogEventMiniGameStart;
            _onEndMiniGame.OnRaised +=  LogEventMiniGameEnd;
        }

        private void Start()
        {
            CheckDependencies();
            _dependencyResolved += InitializeFirebaseAnalytics;
            UserActionSystem.Instance.OnUserActionCompleted += LogEventUserCompleteAction;
        }

        private void OnDisable()
        {
            _onStartMiniGame.OnRaised -= LogEventMiniGameStart;
            _onEndMiniGame.OnRaised -=  LogEventMiniGameEnd;
            _dependencyResolved -= InitializeFirebaseAnalytics;
            if(UserActionSystem.Instance) UserActionSystem.Instance.OnUserActionCompleted -= LogEventUserCompleteAction;
        }


        /// <summary>
        /// Check Dependencies
        /// Check for necessary dependencies and try to resolve them for Android and IOS
        /// </summary>
        private static void CheckDependencies()
        {
#if UNITY_ANDROID
            CheckAndroidDependencies();
#endif
        
#if UNITY_IOS
            CheckIOSDependencies();
#endif
        }

        /// <summary>
        /// Check Android Dependencies 
        /// Check for necessary dependencies and try to resolve them for Android
        /// </summary>
        private static void CheckAndroidDependencies()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    _dependencyStatus = fixTask.Result;
                    if (_dependencyStatus == DependencyStatus.Available)
                    {
                        Debug.Log("Dependency resolved for Android, now proceed with Firebase");
                        _dependencyResolved?.Invoke();
                    }
                    else
                    {
                        Debug.LogErrorFormat("Firebase dependency not resolved for Android. {0}", _dependencyStatus);
                    }
                });
        }

        /// <summary>
        /// Check iOS Dependencies 
        /// Check for necessary dependencies and try to resolve them for iOS
        /// </summary>
        private static void CheckIOSDependencies()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    _dependencyStatus = fixTask.Result;
                    if (_dependencyStatus == DependencyStatus.Available)
                    {
                        Debug.Log("Dependency resolved for IOS, now proceed with Firebase");
                        _dependencyResolved?.Invoke();
                    }
                    else
                    {
                        Debug.LogErrorFormat("Firebase dependency not resolved for IOS. {0}", _dependencyStatus);
                    }
                });
        }
        
        /// <summary>
        /// Initialize Firebase Analytics
        /// </summary>
        private static void InitializeFirebaseAnalytics()
        {
            // Set analytics enabled true
            _analyticsEnabled = true;
            
            // Enable Firebase Analytics Data Collection
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(_analyticsEnabled);
            
            // Set User Id on device unique identifier
            FirebaseAnalytics.SetUserId(SystemInfo.deviceUniqueIdentifier);
            
            // Set default session duration values.
            FirebaseAnalytics.SetSessionTimeoutDuration(new TimeSpan(0, 30, 0));
            
            //Also log app open upon initialization
            LogEventAppOpen();
            Debug.Log("Firebase Analytics Controller running...");
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
        private static void LogEventAppOpen()
        {
            if (!_analyticsEnabled) return;
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAppOpen);
            Debug.Log("Firebase logged App Open.");
        }

        /// <summary>
        /// Log Event Screen View
        /// Actually it's automatically logging event screen view
        /// </summary>
        public static void LogEventScreenView()
        {
            if (!_analyticsEnabled) return;
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventScreenView); 
            Debug.Log("Firebase logged Screen View Event");
        }

        #endregion

        #region UI Action Events

        public static void LogEventUserCompleteAction(UserAction action)
        {
            if (!_analyticsEnabled) return;

            var parameters = new[]
            {
                new Parameter(FirebaseAnalytics.ParameterContent, action.Label),
                new Parameter(FirebaseAnalytics.ParameterContentType, action.ActionType.ToString()),
                new Parameter(FirebaseAnalytics.ParameterValue, action.Value)
            };
            
            FirebaseAnalytics.LogEvent("user_ui_action", parameters);
            Debug.LogFormat("{0} - {1} - event: {2} logged.", nameof(FirebaseAnalyticsController), nameof(LogEventUserCompleteAction), action.Label);
        }

        #endregion

        #region Mini Game Events

        void LogEventMiniGameStart(MiniGameDataSO data)
        {
            if (!_analyticsEnabled) return;
            
            // Event parameters for Firebase
            var parameters = new[] {
                new Parameter(FirebaseAnalytics.ParameterLevel, nameof(GameModes)),
                new Parameter(FirebaseAnalytics.ParameterLevelName, data.GameMode.ToString()),
                new Parameter(FirebaseAnalytics.ParameterCharacter, data.SelectedShipClass.ToString()),
                new Parameter(FirebaseAnalytics.ParameterQuantity, data.SelectedPlayerCount),
                new Parameter(FirebaseAnalytics.ParameterIndex, data.SelectedIntensity)
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
        
        void LogEventMiniGameEnd(MiniGameDataSO data)
        {
            if (!_analyticsEnabled) return;
            
            // Event parameters for Firebase
            var parameters = new [] {
                new Parameter(FirebaseAnalytics.ParameterLevel, nameof(GameModes)),
                new Parameter(FirebaseAnalytics.ParameterLevelName, data.GameMode.ToString()),
                new Parameter(FirebaseAnalytics.ParameterCharacter, data.SelectedShipClass.ToString()),
                new Parameter(FirebaseAnalytics.ParameterQuantity, data.SelectedPlayerCount),
                new Parameter(FirebaseAnalytics.ParameterIndex, data.SelectedIntensity),
                new Parameter(FirebaseAnalytics.ParameterScore, 0) // data.HighScore)   // Get HighScore from MiniGameDataSO
            };
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, parameters);
            Debug.Log("Firebase logged mini game end stats.");
        }

        #endregion

#endif
    }
}