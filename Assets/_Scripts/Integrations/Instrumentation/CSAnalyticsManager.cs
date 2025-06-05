using System;
using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation.DataCollectors;
using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using CosmicShore.Utilities;


namespace CosmicShore.Integrations.Instrumentation
{
    
    public class CSAnalyticsManager : SingletonPersistent<CSAnalyticsManager>, 
        IStoreAnalyzable, 
        IDailyChallengeAnalyzable, 
        IPlayerAnalyzable, 
        IArcadeAnalyzable, 
        IMissionAnalyzable, 
        ITrainingAnalyzable,
        IDeviceAnalyzale
    {
        private readonly CSUtilitiesFirebase _utilitiesFirebase = new CSUtilitiesFirebase(); // This one doesn't have a specific interface yet
        private readonly IStoreAnalyzable _storeDataCollector = new CSStoreDataCollector();
        private readonly IPlayerAnalyzable _playerDataCollector = new CSPlayerDataCollector();
        private readonly IDailyChallengeAnalyzable _dailyChallengeDataCollector = new CSDailyChallengeDataCollector();
        private readonly IArcadeAnalyzable _arcadeDataCollector = new CSArcadeDataCollector();
        private readonly IMissionAnalyzable _missionDataCollector = new CSMissionDataCollector();
        private readonly ITrainingAnalyzable _trainingDataCollector = new CSTrainingDataCollector();
        private readonly IDeviceAnalyzale _deviceDataCollector = new CSDeviceDataCollector();
        
        private async void Start()
        {
            // await InitSDK(); // This is commented out since the analytics system is not currently used and it causes an infinite hang on Android
            await Task.Delay(1);    // Hide console warning until this is connected
        }

        public async Task InitSDK()
        {
            try
            {
                await _utilitiesFirebase.InitSDK();

                // The InitSDK functions are pretty much empty, they are for initializing additional APIS in individual Collectors
                await _storeDataCollector.InitSDK();
                await _playerDataCollector.InitSDK();
                await _dailyChallengeDataCollector.InitSDK();
                await _arcadeDataCollector.InitSDK();
                await _missionDataCollector.InitSDK();
                await _trainingDataCollector.InitSDK();
                await _deviceDataCollector.InitSDK();
            }
            catch 
            { 
            
            }
        }

        // Ready to go
        /// <summary>
        /// Log Event Upgrade Captain
        /// Upgrade event with corresponding parameters are sent to Analytics APIs
        /// </summary>
        /// <param name="captainName"></param>
        /// <param name="captainLevel"></param>
        /// <param name="shipType">Ship type enums should be converted to string before call the event log</param>
        public void LogEventUpgradeCaptain(string captainName, int captainLevel, string shipType)
        {
            _playerDataCollector.LogEventUpgradeCaptain(captainName, captainLevel, shipType);
        }

        // Ready to go 
        public void LogEventPurchaseCaptain(string captainName)
        {
            _storeDataCollector.LogEventPurchaseCaptain(captainName);
        }

        // Ready to go
        public void LogEventPurchaseArcadeGame(string arcadeGameName)
        {
            _storeDataCollector.LogEventPurchaseArcadeGame(arcadeGameName);
        }

        // Ready to go
        public void LogEventPurchaseMission(string missionName)
        {
            _storeDataCollector.LogEventPurchaseMission(missionName);
        }

        // Ready to go
        public async Task LogEventWatchAd()
        {
            await _storeDataCollector.LogEventWatchAd();
        }

        // Ready to go
        public async Task LogEventRedeemDailyReward()
        {
            await _storeDataCollector.LogEventRedeemDailyReward();
        }
        
        // Ready to go
        /// <summary>
        /// Log Event Start Daily Challenge
        /// </summary>
        /// <param name="gameType">Game type enum should be converted to string before call the log event</param>
        /// <param name="intensity"></param>
        /// <param name="shipType">Game type enum should be converted to string before call the log event</param>
        /// <param name="captainName">Game type enum should be converted to string before call the log event</param>
        public void LogEventStartDailyChallenge(string gameType, int intensity, string shipType, string captainName)
        {
            _dailyChallengeDataCollector.LogEventStartDailyChallenge(gameType, intensity, shipType, captainName);
        }

        // Ready to go
        /// <summary>
        /// Log Event Complete Daily Challenge
        /// </summary>
        /// <param name="gameType">Game type enum should be converted to string before call the log event</param>
        /// <param name="intensity"></param>
        /// <param name="shipType">Game type enum should be converted to string before call the log event</param>
        /// <param name="captainName">Game type enum should be converted to string before call the log event</param>
        /// <param name="score"></param>
        /// <param name="reward"></param>
        /// <param name="playTime"></param>
        public void LogEventCompleteDailyChallenge(
            string gameType, int intensity, string shipType, string captainName, int score, int reward, DateTime playTime)
        {
            _dailyChallengeDataCollector.LogEventCompleteDailyChallenge(
                gameType, intensity, shipType, captainName, score, reward, playTime);
        }

        // Ready to go
        /// <summary>
        /// Log Event Start Arcade Game
        /// </summary>
        /// <param name="gameType">Game type enum should be converted to string before call the log event</param>
        /// <param name="intensity">Game intensity</param>
        /// <param name="shipType">Ship type enum should be converted to string before call the log event</param>
        public void LogEventStartArcadeGame(string gameType, int intensity, string shipType)
        {
            _arcadeDataCollector.LogEventStartArcadeGame(gameType, intensity, shipType);
        }

        // Ready to go
        /// <summary>
        /// Log Event Complete Arcade Game
        /// </summary>
        /// <param name="gameType">Convert enum to string</param>
        /// <param name="intensity"></param>
        /// <param name="shipType">Convert enum to string</param>
        /// <param name="score"></param>
        /// <param name="reward"></param>
        /// <param name="playTime"></param>
        public void LogEventCompleteArcadeGame(string gameType, int intensity, string shipType, int score, int reward, DateTime playTime)
        {
            _arcadeDataCollector.LogEventCompleteArcadeGame(gameType, intensity, shipType, score, reward, playTime);
        }

        // Ready to go
        /// <summary>
        /// Log Event Start Mission
        /// </summary>
        /// <param name="gameType">Convert enum to string</param>
        /// <param name="intensity"></param>
        /// <param name="shipType">Convert enum to string</param>
        /// <param name="captainName">Convert enum to string</param>
        /// <param name="numberOfPlayers"></param>
        public void LogEventStartMission(string gameType, int intensity, string shipType, string captainName, int numberOfPlayers)
        {
            _missionDataCollector.LogEventStartMission(gameType, intensity, shipType, captainName, numberOfPlayers);
        }

        // Ready to go
        /// <summary>
        /// Log Event Complete Mission
        /// </summary>
        /// <param name="gameType">Convert enum to string</param>
        /// <param name="intensity"></param>
        /// <param name="shipType">Convert enum to string</param>
        /// <param name="captainName">Convert enum to string</param>
        /// <param name="numberOfPlayers"></param>
        /// <param name="score"></param>
        /// <param name="reward"></param>
        /// <param name="playTime"></param>
        public void LogEventCompleteMission(string gameType, int intensity, string shipType, string captainName, 
            int numberOfPlayers, int score, int reward, DateTime playTime)
        {
            _missionDataCollector.LogEventCompleteMission(gameType, intensity, shipType, captainName, numberOfPlayers, score, reward, playTime);
        }

        // Ready to go
        /// <summary>
        /// Log Event Start Training
        /// </summary>
        /// <param name="gameType">Game type enum should be converted to string before call the log event</param>
        /// <param name="intensity"></param>
        /// <param name="shipType">Game type enum should be converted to string before call the log event</param>
        public void LogEventStartTraining(string gameType, int intensity, string shipType)
        {
            _trainingDataCollector.LogEventStartTraining(gameType, intensity, shipType);
        }

        // Ready to go
        /// <summary>
        /// Log Event Complete Training
        /// </summary>
        /// <param name="gameType">Game type enum should be converted to string before call the log event</param>
        /// <param name="intensity"></param>
        /// <param name="shipType">Game type enum should be converted to string before call the log event</param>
        /// <param name="score"></param>
        /// <param name="reward"></param>
        /// <param name="playTime"></param>
        public void LogEventCompleteTraining(
            string gameType, int intensity, string shipType, int score, int reward, DateTime playTime)
        {
            _trainingDataCollector.LogEventCompleteTraining(
                gameType, intensity, shipType, score, reward, playTime);
        }

        // Ready to go
        public void LogEventAppOpen()
        {
            _deviceDataCollector.LogEventAppOpen();
        }

        // Ready to go
        public void LogEventAppClose()
        {
            _deviceDataCollector.LogEventAppClose();
        }
    }
}
