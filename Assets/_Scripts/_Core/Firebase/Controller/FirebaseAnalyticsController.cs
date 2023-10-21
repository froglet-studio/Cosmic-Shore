using Firebase;
using Firebase.Analytics;
using StarWriter.Utility.Singleton;
using UnityEngine;

namespace _Scripts._Core.Firebase.Controller
{
    public class FirebaseAnalyticsController : SingletonPersistent<FirebaseAnalyticsController>
    {
        private bool _analyticsEnabled = false;
        private FirebaseApp _app;

        #region Firebase Analytics Controller Initialization and Enabling
        
        private void OnEnable()
        {
            FirebaseHelper.DependencyResolved.AddListener(InitializeFirebaseAnalytics);
        }

        private void OnDisable()
        {
            FirebaseHelper.DependencyResolved.RemoveListener(InitializeFirebaseAnalytics);
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
            
            //Also log app open upon initialization
            LogEventAppOpen();
            Debug.Log("Firebase Analytics Controller running...");
        }
        
        #endregion

        #region Ad Measurement

        /// <summary>
        /// Log Event Add Impression
        /// </summary>
        public void LogEventAdImpression()
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
        /// </summary>
        public void LogEventScreenView()
        {
            if (!_analyticsEnabled) return;
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventScreenView);
            Debug.Log("Firebase logged Screen View Event");
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
        public void LogEventMiniGameStart(MiniGames mode, ShipTypes ship, int playerCount, int intensity)
        {
            if (!_analyticsEnabled) return;

            var parameters = new[] {
                new Parameter(FirebaseAnalytics.ParameterLevel, nameof(MiniGames)),
                new Parameter(FirebaseAnalytics.ParameterLevelName, mode.ToString()),
                new Parameter(FirebaseAnalytics.ParameterCharacter, ship.ToString()),
                new Parameter("mini_game_player_count", playerCount),
                new Parameter("mini_game_intensity", intensity),
            };
            
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart, parameters);
            Debug.Log("Firebase logged mini game start stats.");
            
        }

        /// <summary>
        /// Log Event Mini Game End
        /// </summary>
        /// <param name="mode">Mini Game Mode</param>
        /// <param name="ship">Ship Type</param>
        /// <param name="playerCount">Player Count</param>
        /// <param name="intensity">Intensity</param>
        /// <param name="highScore">HighScore</param>
        public void LogEventMiniGameEnd(MiniGames mode, ShipTypes ship, int playerCount, int intensity, int highScore)
        {
            if (!_analyticsEnabled) return;
            
            
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