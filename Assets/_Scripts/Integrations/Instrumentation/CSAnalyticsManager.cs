using CosmicShore.Integrations.Instrumentation.DataCollectors;
using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using CosmicShore.Utility.Singleton;

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
        private readonly CSUtilitiesFirebase _utilitiesFirebase = new ();
        private readonly CSStoreDataCollector _storeDataCollector = new();
        private readonly CSPlayerDataCollector _playerDataCollector = new();
        private readonly CSDailyChallengeDataCollector _dailyChallengeDataCollector = new();
        private readonly CSArcadeDataCollector _arcadeDataCollector = new();
        private readonly CSMissionDataCollector _missionDataCollector = new();
        private readonly CSTrainingDataCollector _trainingDataCollector = new();
        private readonly CSDeviceDataCollector _deviceDataCollector = new();
        
        private void Start()
        {
            InitSDK();
        }

        public void InitSDK()
        {
            _utilitiesFirebase.InitSDK();
            
            // The InitSDK functions are pretty much empty, they are for initializing additional APIS in individual Collectors
            _storeDataCollector.InitSDK();
            _playerDataCollector.InitSDK();
            _dailyChallengeDataCollector.InitSDK();
            _arcadeDataCollector.InitSDK();
            _missionDataCollector.InitSDK();
            _trainingDataCollector.InitSDK();
            _deviceDataCollector.InitSDK();
        }

        public void LogEventUpgradeCaptain()
        {
            _playerDataCollector.LogEventUpgradeCaptain();
        }

        public void LogEventPurchaseCaptain(string captainName)
        {
            _storeDataCollector.LogEventPurchaseCaptain(captainName);
        }

        public void LogEventPurchaseArcadeGame(string arcadeGameName)
        {
            _storeDataCollector.LogEventPurchaseArcadeGame(arcadeGameName);
        }

        public void LogEventPurchaseMission(string missionName)
        {
            _storeDataCollector.LogEventPurchaseMission(missionName);
        }

        public void LogEventWatchAd()
        {
            _storeDataCollector.LogEventWatchAd();
        }

        public void LogEventRedeemDailyReward()
        {
            _storeDataCollector.LogEventRedeemDailyReward();
        }

        public void LogEventStartDailyChallenge()
        {
            _dailyChallengeDataCollector.LogEventStartDailyChallenge();
        }

        public void LogEventCompleteDailyChallenge()
        {
            _dailyChallengeDataCollector.LogEventCompleteDailyChallenge();
        }

        public void LogEventStartArcadeGame()
        {
            _arcadeDataCollector.LogEventStartArcadeGame();
        }

        public void LogEventCompleteArcadeGame()
        {
            _arcadeDataCollector.LogEventCompleteArcadeGame();
        }

        public void LogEventStartMission()
        {
            _missionDataCollector.LogEventStartMission();
        }

        public void LogEventCompleteMission()
        {
            _missionDataCollector.LogEventCompleteMission();
        }

        public void LogEventStartTraining()
        {
            _trainingDataCollector.LogEventStartTraining();
        }

        public void LogEventCompleteTraining()
        {
            _trainingDataCollector.LogEventCompleteTraining();
        }

        public void LogEventAppOpen()
        {
            _deviceDataCollector.LogEventAppOpen();
        }

        public void LogEventAppClose()
        {
            _deviceDataCollector.LogEventAppClose();
        }
    }
}
